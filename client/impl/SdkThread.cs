using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using io.harness.ff_dotnet_client_sdk.client.dto;
using io.harness.ff_dotnet_client_sdk.client.impl.dto;
using io.harness.ff_dotnet_client_sdk.openapi.Api;
using io.harness.ff_dotnet_client_sdk.openapi.Client;
using io.harness.ff_dotnet_client_sdk.openapi.Model;
using Microsoft.Extensions.Logging;
using LogUtils = io.harness.ff_dotnet_client_sdk.client.impl.SdkCodes.LogUtils;

namespace io.harness.ff_dotnet_client_sdk.client.impl
{
    internal class SdkThread : IDisposable
    {
        internal static readonly string SdkVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
        internal static readonly string HarnessSdkInfoHeader = ".NET " + SdkVersion + " Client";
        internal static readonly string UserAgentHeader = ".NET/" + SdkVersion;
        internal const int DefaultTimeoutMs = 60_000;

        private readonly ILogger<SdkThread> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly string _apiKey;
        private readonly FFConfig _config;
        private readonly FFTarget _ffTarget;
        private readonly ConcurrentDictionary<string, Evaluation> _cache;
        private readonly CountdownEvent _sdkReadyLatch = new(1);
        private readonly Thread _thread;

        /* Mutable state */
        private ClientApi? _api;
        private AuthInfo? _authInfo;


        internal SdkThread(string apiKey, FFConfig config, FFTarget ffTarget, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<SdkThread>();
            _apiKey = apiKey;
            _config = config;
            _ffTarget = ffTarget;
            _cache = new ConcurrentDictionary<string, Evaluation>();
            _thread = new Thread(Run);
            _thread.Start();
        }

        internal bool WaitForInitialization(int timeoutMs)
        {
            return _sdkReadyLatch.Wait(timeoutMs);
        }

        internal Evaluation? GetEvaluationById(String evaluationId, StringBuilder failureReason) {
            if (_authInfo == null) {
                failureReason.Append("SDK not authenticated");
                return null;
            }

            var key = MakeCacheKey(_authInfo.EnvironmentIdentifier, evaluationId);
            return _cache.TryGetValue(key, out var eval) ? eval : null;
        }

        private void MainSdkThread(ClientApi api)
        {
            _authInfo = Authenticate(api, _apiKey, _ffTarget);

            if (_authInfo == null) {
                throw new Exception("Authentication failed");
            }

            bool fallbackToPolling;
            try
            {
                fallbackToPolling = !Stream(api, _authInfo);
            }
            catch (ThreadInterruptedException)
            {
                throw; // We're being told to abort by Dispose()
            }
            catch (Exception ex)
            {
                LogUtils.LogExceptionAndWarn(_logger, _config, "Stream failed", ex);
                fallbackToPolling = true;
            }

            if (fallbackToPolling)
            {
                try {
                    Poll(api, _authInfo);
                } catch (Exception ex) {
                    LogUtils.LogExceptionAndWarn(_logger, _config, "Polling failed", ex);
                } finally {
                    SdkCodes.InfoPollingStopped(_logger);
                }

            }
        }

        AuthInfo Authenticate(ClientApi api, string apiKey, FFTarget ffTarget)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) {
                const string errorMsg = "SDKCODE(init:1002):The SDK has failed to initialize due to a missing or empty API key.";
                _logger.LogError(errorMsg);
                throw new FFClientException(errorMsg);
            }

            var authTarget = new AuthenticationRequestTarget(ffTarget.Identifier, ffTarget.Name, false, ffTarget.Attributes);
            var authRequest = new AuthenticationRequest(apiKey, authTarget);

            var authResp = api.Authenticate(authRequest);

            var jwtToken = (JwtSecurityToken) new JwtSecurityTokenHandler().ReadToken(authResp.AuthToken);
            var accountId = jwtToken.Payload.TryGetValue("accountID", out var value) ? value.ToString() : "";
            var environment = jwtToken.Payload["environment"].ToString();
            var cluster = jwtToken.Payload["clusterIdentifier"].ToString();
            var environmentIdentifier = jwtToken.Payload.TryGetValue("environmentIdentifier", out value) ? value.ToString() : environment;
            var project = jwtToken.Payload.TryGetValue("project", out value) ? value.ToString() : "";
            var org = jwtToken.Payload.TryGetValue("organization", out value) ? value.ToString() : "";
            var projectId = jwtToken.Payload.TryGetValue("projectIdentifier", out value) ? value.ToString() : "";

            api.Configuration.DefaultHeaders.Clear();
            api.Configuration.DefaultHeaders.Add("Authorization", "Bearer " + authResp.AuthToken);
            api.Configuration.DefaultHeaders.Add("Harness-EnvironmentID", environmentIdentifier);
            api.Configuration.DefaultHeaders.Add("Harness-AccountID", accountId);
            //api.Configuration.DefaultHeaders.Add("User-Agent", UserAgentHeader);
            api.Configuration.DefaultHeaders.Add("Harness-SDK-Info", HarnessSdkInfoHeader);

            var authInfo = new AuthInfo
            {
                Project = project,
                ProjectIdentifier = projectId,
                AccountId = accountId,
                Environment = environment,
                ClusterIdentifier = cluster,
                EnvironmentIdentifier = environmentIdentifier,
                Organization = org,
                BearerToken = authResp.AuthToken,
                ApiKey = apiKey
            };

            PollOnce(api, authInfo);

            SdkCodes.InfoSdkAuthOk(_logger, SdkVersion);
            _sdkReadyLatch.Signal();

            return authInfo;
        }

        private class StreamSourceListener : IEventSourceListener
        {
            private readonly ILogger<StreamSourceListener> _logger;
            private readonly SdkThread _this;
            private readonly AuthInfo _authInfo;
            private readonly ClientApi _api;
            private readonly FFTarget _target;
            private readonly CountdownEvent _endStreamLatch = new(1);

            internal StreamSourceListener(SdkThread sdkThread, ClientApi api, AuthInfo authInfo, FFTarget target, ILoggerFactory loggerFactory)
            {
                _logger = loggerFactory.CreateLogger<StreamSourceListener>();
                _this = sdkThread;
                _api = api;
                _authInfo = authInfo;
                _target = target;
            }

            public void SseStart()
            {
                _this.PollOnce(_api, _authInfo);
                SdkCodes.InfoStreamConnected(_logger);
            }

            public void SseEnd(string reason)
            {
                SdkCodes.InfoStreamStopped(_logger, reason);
                _endStreamLatch.Signal();
                _this.PollOnce(_api, _authInfo);
            }

            public void SseEvaluationRemove(string identifier)
            {
                _logger.LogTrace("SSE Evaluation remove {Identifier}", identifier);
                _this.RepoRemoveEvaluation(_authInfo.EnvironmentIdentifier, identifier);
            }

            public void SseEvaluationReload(List<Evaluation> evaluations)
            {
                if (evaluations.Count > 0 && evaluations.TrueForAll(IsEvaluationValid))
                {
                    _logger.LogTrace("SSE reload {Count} evaluations from event", evaluations.Count);

                    foreach (var evaluation in evaluations)
                    {
                        _this.RepoSetEvaluation(_authInfo.EnvironmentIdentifier, evaluation.Flag, evaluation);
                    }
                }
                else
                {
                    _logger.LogTrace("SSE reload all evaluations from server");
                    _this.PollOnce(_api, _authInfo);
                }
            }

            public void SseEvaluationsUpdate(List<Evaluation> evaluations)
            {
                _logger.LogTrace("SSE update {Count} evaluations from event", evaluations.Count);

                foreach (var evaluation in evaluations)
                {
                    _this.RepoSetEvaluation(_authInfo.EnvironmentIdentifier, evaluation.Flag, evaluation);
                }
            }

            public void SseEvaluationChange(string identifier)
            {
                _logger.LogTrace("SSE Evaluation {identifier} changed, fetching flag from server",identifier);

                Evaluation evaluation = _api.GetEvaluationByIdentifier(_authInfo.Environment,
                    identifier, _target.Identifier, _authInfo.ClusterIdentifier);

                _this.RepoSetEvaluation(_authInfo.EnvironmentIdentifier, evaluation.Flag, evaluation);
            }

            public void WaitForStreamToEnd()
            {
                _endStreamLatch.Wait();
            }
        }

        bool Stream(ClientApi api, AuthInfo authInfo)
        {
            if (!_config.StreamEnabled)
            {
                return false;
            }

            var streamUrl = _config.ConfigUrl + "/stream?cluster=" + authInfo.ClusterIdentifier;

            var listener = new StreamSourceListener(this, api, authInfo, _ffTarget, _loggerFactory);
            using var eventSource = new EventSource(authInfo, streamUrl, _config, listener, _loggerFactory);
            _ = eventSource.Start();
            listener.WaitForStreamToEnd();
            return true;
        }



        private void Poll(ClientApi api, AuthInfo authInfo)
        {
            var pollDelayInSeconds = Math.Max(_config.PollIntervalInSeconds, 60);

            do
            {
                Thread.Sleep(TimeSpan.FromSeconds(pollDelayInSeconds));

                PollOnce(api, authInfo);
            } while (true); // todo check for interrupted thread
        }

        private List<Evaluation> PollOnce(ClientApi api, AuthInfo authInfo)
        {
            var evaluations =
                api.GetEvaluations(authInfo.Environment, _ffTarget.Identifier, authInfo.ClusterIdentifier);

            foreach (var eval in evaluations)
            {
                RepoSetEvaluation(authInfo.EnvironmentIdentifier, eval.Flag, eval);
                _logger.LogTrace("EnvId={EnvironmentIdentifier} Flag={Flag} Value={Value}", authInfo.EnvironmentIdentifier, eval.Flag, eval.Value);
            }

            return evaluations;
        }

        private void RepoSetEvaluation(string? environmentIdentifier, string flag, Evaluation eval)
        {
            var key = MakeCacheKey(environmentIdentifier, flag);
            _cache.AddOrUpdate(key, eval, (_, _) => eval);
            _logger.LogTrace("Added key {CacheKey} to cache. New cache size: {CacheSize}", key, _cache.Count);
        }

        private void RepoRemoveEvaluation(string authInfoEnvironmentIdentifier, string evaluationFlag)
        {
            string key = MakeCacheKey(authInfoEnvironmentIdentifier, evaluationFlag);
            _cache.TryRemove(key, out _);
            _logger.LogTrace("Removed key {CacheKey} from cache. New cache size: {CacheSize}", key, _cache.Count);
        }

        private void Run()
        {
            bool threadInterrupted = false;

            do {
                try {
                    _api?.Dispose();
                    _api = MakeClientApi();
                    MainSdkThread(_api);
                } catch (ThreadInterruptedException) {
                    // Dispose() will trigger this
                    _logger.LogTrace("SDK thread received an abort signal. Exiting.");
                    threadInterrupted = true;
                } catch (Exception ex) {
                    LogUtils.LogExceptionAndWarn(_logger, _config, "Root SDK exception handler invoked, SDK will be restarted in 1 minute:", ex);
                }

                if (!threadInterrupted)
                {
                    /* should the sdk thread abort unexpectedly it will be restarted here */
                    Thread.Sleep(TimeSpan.FromMinutes(1));
                }
            } while (!threadInterrupted);
        }

        private ClientApi MakeClientApi()
        {
            var apiConfig = new Configuration
            {
                BasePath = _config.ConfigUrl,
                //ClientCertificates =
                //RemoteCertificateValidationCallback =
                UserAgent = UserAgentHeader,
                Timeout = DefaultTimeoutMs,
            };

            var httpClientHandler = new HttpClientHandler()
            {

            };

            var httpClient = new HttpClient()
            {
                Timeout = TimeSpan.FromMilliseconds(DefaultTimeoutMs),
                //DefaultRequestHeaders = {  }
            };

            return new ClientApi(httpClient, _config.ConfigUrl, httpClientHandler);
        }


        private static bool IsEvaluationValid(Evaluation evaluation)
        {
            return !string.IsNullOrEmpty(evaluation.Flag) && !string.IsNullOrEmpty(evaluation.Kind) &&
                   !string.IsNullOrEmpty(evaluation.Identifier) && !string.IsNullOrEmpty(evaluation.Value);
        }

        private static string MakeCacheKey(string? environmentIdentifier, string flag)
        {
            return new StringBuilder().Append(environmentIdentifier).Append('_').Append(flag).ToString();
        }

        public void Dispose()
        {
            _thread.Interrupt();
            if (!_thread.Join(TimeSpan.FromMinutes(1)))
            {
                _logger.LogWarning("SDK thread did not shutdown correctly");
            }
            _loggerFactory.Dispose();
            _api?.Dispose();
        }

        public AuthInfo? GetAuthInfo()
        {
            return _authInfo;
        }
    }
}