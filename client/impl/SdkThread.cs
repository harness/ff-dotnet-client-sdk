using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using io.harness.ff_dotnet_client_sdk.client.dto;
using io.harness.ff_dotnet_client_sdk.client.impl.dto;
using io.harness.ff_dotnet_client_sdk.client.impl.util;
using io.harness.ff_dotnet_client_sdk.openapi.Api;
using io.harness.ff_dotnet_client_sdk.openapi.Client;
using io.harness.ff_dotnet_client_sdk.openapi.Model;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
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
        private readonly FfConfig _config;
        private readonly FfTarget _ffTarget;
        private readonly ConcurrentDictionary<string, Evaluation> _cache;
        private readonly CountdownEvent _sdkReadyLatch = new(1);
        private readonly Thread _thread;
        private readonly INetworkChecker _networkChecker;

        /* Mutable state */
        private ClientApi? _api;
        private AuthInfo? _authInfo;
        private volatile bool _abortFlag;
        private bool _disposed = false;

        internal SdkThread(string apiKey, FfConfig config, FfTarget ffTarget, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<SdkThread>();
            _apiKey = apiKey;
            _config = config;
            _ffTarget = ffTarget;
            _cache = new ConcurrentDictionary<string, Evaluation>();
            _networkChecker = config.NetworkChecker;
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

            if (IsNetworkUnavailable())
                throw new NetworkOffline();
        }

        private AuthInfo Authenticate(ClientApi api, string apiKey, FfTarget ffTarget)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) {
                const string errorMsg = "SDKCODE(init:1002):The SDK has failed to initialize due to a missing or empty API key.";
                _logger.LogError(errorMsg);
                throw new FfClientException(errorMsg);
            }

            if (IsNetworkUnavailable())
            {
                _logger.LogInformation("Will not auth, network offline");
                throw new NetworkOffline();
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

            api.Configuration.DefaultHeaders.Clear();
            AddSdkHeaders(api.Configuration.DefaultHeaders, authInfo);

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
            private readonly FfTarget _target;
            private readonly CountdownEvent _endStreamLatch = new(1);
            private readonly FfConfig _config;

            internal StreamSourceListener(SdkThread sdkThread, ClientApi api, AuthInfo authInfo, FfTarget target, ILoggerFactory loggerFactory, FfConfig config)
            {
                _logger = loggerFactory.CreateLogger<StreamSourceListener>();
                _this = sdkThread;
                _api = api;
                _authInfo = authInfo;
                _target = target;
                _config = config;
            }

            public void SseStart()
            {
                _this.PollOnce(_api, _authInfo);
                SdkCodes.InfoStreamConnected(_logger);
            }

            public void SseEnd(string reason, Exception? cause)
            {
                SdkCodes.InfoStreamStopped(_logger, reason);
                if (cause != null)
                {
                    if (cause is NetworkOffline)
                        _logger.LogInformation("SSE network went offline");
                    else
                        LogUtils.LogExceptionAndWarn(_logger, _config, "Stream end exception", cause);
                }

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
            if (IsNetworkUnavailable()) {
                throw new NetworkOffline();
            }

            if (!_config.StreamEnabled)
            {
                return false;
            }

            var streamUrl = _config.ConfigUrl + "/stream?cluster=" + authInfo.ClusterIdentifier;

            var listener = new StreamSourceListener(this, api, authInfo, _ffTarget, _loggerFactory, _config);
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
            } while  (!_abortFlag);
        }

        private List<Evaluation> PollOnce(ClientApi api, AuthInfo authInfo)
        {
            if (IsNetworkUnavailable())
            {
                throw new NetworkOffline();
            }

            var evaluations =
                api.GetEvaluations(authInfo.Environment, _ffTarget.Identifier, authInfo.ClusterIdentifier);

            foreach (var eval in evaluations)
            {
                RepoSetEvaluation(authInfo.EnvironmentIdentifier, eval.Flag, eval);
                _logger.LogTrace("EnvId={EnvironmentIdentifier} Flag={Flag} Value={Value}", authInfo.EnvironmentIdentifier, eval.Flag, eval.Value);
            }

            if (_config.Debug)
                _logger.LogInformation("Polling got {FlagCount} flags", evaluations.Count);

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
            do {
                try
                {
                    _api?.Dispose();
                    _api = MakeClientApi();
                    MainSdkThread(_api);
                }
                catch (NetworkOffline ex)
                {
                    LogUtils.LogException(_config, ex);
                    WaitForNetworkToGoOnline();
                    continue;
                }
                catch (Exception ex)
                {
                    /* should the sdk thread abort unexpectedly it will be restarted here in 1 minute */

                    if (!_abortFlag)
                    {
                        LogUtils.LogSdkCodeFromException(_logger, ex);
                        LogUtils.LogExceptionAndWarn(_logger, _config, "Root SDK exception handler invoked, SDK will be restarted in 1 minute:", ex);

                        try
                        {
                            Thread.Sleep(TimeSpan.FromMinutes(1));
                        }
                        catch (ThreadInterruptedException tiex)
                        {
                            LogUtils.LogExceptionAndWarn(_logger, _config, "SDK thread interrupted", tiex);
                        }
                    }
                }

            } while (!_abortFlag);

            _logger.LogTrace("SDK thread received an abort signal. Exiting.");
        }

        private ClientApi MakeClientApi()
        {
            var client = TlsUtils.CreateHttpClientWithTls(_config, _loggerFactory);
            client.BaseAddress = new Uri(_config.ConfigUrl);
            client.Timeout = TimeSpan.FromMilliseconds(DefaultTimeoutMs);
            return new ClientApi(client, _config.ConfigUrl);
        }

        internal static void AddSdkHeaders(IDictionary<string, string> headers, AuthInfo authInfo)
        {
            headers.Add("Authorization", "Bearer " + authInfo.BearerToken);
            headers.Add("Harness-SDK-Info", HarnessSdkInfoHeader);
            headers.Add("User-Agent", UserAgentHeader);

            if (!authInfo.AccountId.IsNullOrEmpty())
            {
                headers.Add("Harness-AccountID", authInfo.AccountId);
            }

            if (!authInfo.EnvironmentIdentifier.IsNullOrEmpty())
            {
                headers.Add("Harness-EnvironmentID", authInfo.EnvironmentIdentifier);
            }
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
            if (_disposed) return;

            _abortFlag = true;
            _thread.Interrupt();
            if (!_thread.Join(TimeSpan.FromMinutes(1)))
            {
                _logger.LogWarning("SDK thread did not shutdown correctly");
            }
            _loggerFactory.Dispose();
            _api?.Dispose();
            _disposed = true;
        }

        internal AuthInfo? GetAuthInfo()
        {
            return _authInfo;
        }

        internal bool IsNetworkUnavailable()
        {
            return !_networkChecker.IsNetworkAvailable();
        }

        internal void WaitForNetworkToGoOnline()
        {
            _logger.LogInformation("Network is offline, SDK going to sleep");

            int counter = 30;
            do {
                try {
                    if (_networkChecker.IsNetworkAvailable()) {
                        _logger.LogInformation("Network is online, restarting SDK");
                        return;
                    }
                    Thread.Sleep(TimeSpan.FromSeconds(2));
                } catch (ThreadInterruptedException ex) {
                    if (_abortFlag) return;
                    LogUtils.LogException(_config, ex);
                }
            } while (counter-- > 0);
        }

        internal class NetworkOffline : Exception
        {
            internal NetworkOffline() : base("No Internet") {}
        }
    }
}