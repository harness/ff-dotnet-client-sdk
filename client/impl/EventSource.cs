using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using io.harness.ff_dotnet_client_sdk.client.impl.dto;
using io.harness.ff_dotnet_client_sdk.openapi.Model;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace io.harness.ff_dotnet_client_sdk.client.impl
{

    interface IEventSourceListener
    {
        /// SSE stream started ok
        void SseStart();
        /// SSE stream ended
        void SseEnd(string reason);
        /// Indicates callback to server required to get flag state
        void SseEvaluationChange(string identifier);
        /// Event includes evaluations payload, cache can be updated immediately with no callback
        void SseEvaluationsUpdate(List<Evaluation> evaluations);
        /// Indicates flag removal
        void SseEvaluationRemove(string identifier);
        /// Indicates creation, change or removal of a target group we want to reload evaluations
        void SseEvaluationReload(List<Evaluation> evaluations);

    }

    internal class EventSource : IDisposable
    {
        private readonly ILogger<EventSource> _logger;
        //private readonly AuthInfo _authInfo;
        private readonly string _url;
        private readonly FFConfig _config;
        private readonly HttpClient _httpClient;
        private readonly IEventSourceListener _callback;
        private const int ReadTimeoutMs = 60_000;

        internal EventSource(AuthInfo authInfo, string url, FFConfig config, IEventSourceListener callback, ILoggerFactory loggerFactory)
        {
            _httpClient = MakeHttpClient(authInfo);
            _url = url;
            _config = config;
            _callback = callback;
            _logger = loggerFactory.CreateLogger<EventSource>();
        }

        private static HttpClient MakeHttpClient(AuthInfo authInfo)
        {
            var httpClient = new HttpClient()
            {
                DefaultRequestHeaders =
                {
                    Authorization = new AuthenticationHeaderValue("Bearer", authInfo.BearerToken)
                    //UserAgent
                },
                Timeout = TimeSpan.FromMilliseconds(ReadTimeoutMs)
            };

            httpClient.DefaultRequestHeaders.Add("API-Key", authInfo.ApiKey);
            return httpClient;
        }

        private static string? ReadLine(Stream stream, int timeoutMs)
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
            using (cancellationTokenSource.Token.Register(stream.Dispose))
            {
                var builder = new StringBuilder();
                int next;
                do
                {
                    next = stream.ReadByte();
                    if (next == -1)
                    {
                        return null;
                    }

                    builder.Append((char)next);
                } while (next != 10);

                return builder.ToString();
            }
        }

        internal async Task Start()
        {
            try
            {
                using var stream = await _httpClient.GetStreamAsync(_url);
                _callback.SseStart();

                while (ReadLine(stream, ReadTimeoutMs) is { } message)
                {
                    if (!message.Contains("domain"))
                    {
                        _logger.LogTrace("Received event source heartbeat");
                        continue;
                    }

                    SdkCodes.InfoStreamEventReceived(_logger, message);

                    var jsonMessage = JObject.Parse("{" + message + "}");
                    ProcessMessage(jsonMessage["data"]);
                }

                _callback.SseEnd("End of stream");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "EventSource threw an error: {Reason}", e.Message);
                if (_config.Debug)
                    Debug.WriteLine(e.ToString());

                _callback.SseEnd(e.Message);
            }

        }

        private void ProcessMessage(JToken? data)
        {
            if (data == null)
            {
                return;
            }

            var domain = data["domain"]?.ToString() ?? "";
            var eventType = data["event"]?.ToString() ?? "";
            var identifier = data["identifier"]?.ToString() ?? "";

            if ("target-segment".Equals(domain))
            {
                // On creation, change or removal of a target group we want to reload evaluations
                if ("delete".Equals(eventType) || "patch".Equals(eventType) || "Equals".Equals(eventType))
                {
                    _callback.SseEvaluationReload(new List<Evaluation>());
                }
            }
            else if ("flag".Equals(domain))
            {
                // On creation or change of a flag we want to send a change event
                if ("create".Equals(eventType) || "patch".Equals(eventType))
                {
                    _callback.SseEvaluationChange(identifier);
                }
                // On deletion of a flag we want to send a remove event
                else if ("delete".Equals(eventType))
                {
                    _callback.SseEvaluationRemove(identifier);
                }
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}