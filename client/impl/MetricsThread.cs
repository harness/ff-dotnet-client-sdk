using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using io.harness.ff_dotnet_client_sdk.client.dto;
using io.harness.ff_dotnet_client_sdk.client.impl.dto;
using io.harness.ff_dotnet_client_sdk.client.impl.util;
using io.harness.ff_dotnet_client_sdk.openapi.Api;
using io.harness.ff_dotnet_client_sdk.openapi.Client;
using io.harness.ff_dotnet_client_sdk.openapi.Model;
using Microsoft.Extensions.Logging;
using LogUtils = io.harness.ff_dotnet_client_sdk.client.impl.SdkCodes.LogUtils;

namespace io.harness.ff_dotnet_client_sdk.client.impl
{
    internal class MetricsThread : IDisposable
    {
        private const string FeatureIdentifierAttribute = "featureIdentifier";
        private const string FeatureNameAttribute = "featureName";
        private const string VariationIdentifierAttribute = "variationIdentifier";
        private const string TargetAttribute = "target";
        private const string SdkType = "SDK_TYPE";
        private const string Client = "client";
        private const string SdkLanguage = "SDK_LANGUAGE";
        private const string SdkVersion = "SDK_VERSION";
        private const int MaxFreqMapToRetain = 10_000;
        private readonly ILogger<MetricsThread> _logger;
        private readonly Thread _thread;
        private readonly FfTarget _target;
        private readonly FfConfig _config;
        private readonly int _maxFreqMapSize;
        private readonly FrequencyMap<Analytics> _frequencyMap = new();
        private readonly MetricsApi _api;
        private readonly AuthInfo _authInfo;
        private readonly INetworkChecker _networkChecker;

        private int _evalCounter;
        private int _metricsEvaluationsDropped;
        private volatile bool _abortFlag;

        internal MetricsThread(FfTarget target, FfConfig config, ILoggerFactory loggerFactory, AuthInfo? authInfo)
        {
            if (authInfo == null) throw new ArgumentNullException(nameof(authInfo));
            _logger = loggerFactory.CreateLogger<MetricsThread>();
            _target = target;
            _config = config;
            _maxFreqMapSize = Clamp(config.MetricsCapacity, 2048, MaxFreqMapToRetain);
            _api = MakeClientApi(authInfo, loggerFactory);
            _authInfo = authInfo;
            _networkChecker = config.NetworkChecker;
            _thread = new Thread(Run);
            _thread.Start();
        }

        internal void RegisterEvaluation(string evaluationId, Variation variation)
        {
            var analytics = new Analytics(_target, evaluationId, variation);

            if (_frequencyMap.ContainsKey(analytics) && _frequencyMap.Count() + 1 > _maxFreqMapSize)
            {
                Interlocked.Increment(ref _metricsEvaluationsDropped);
            }
            else
            {
                _frequencyMap.Increment(analytics);
            }

            Interlocked.Increment(ref _evalCounter);
        }


        private static int Clamp(int value, int lower, int higher) {
            return Math.Max(lower, Math.Min(higher, value));
        }

        private void Run()
        {
            SdkCodes.InfoMetricsThreadStarted(_logger, _config.MetricsIntervalInSeconds);

            var delay = TimeSpan.FromSeconds(_config.MetricsIntervalInSeconds);
            do
            {
                try
                {
                    if (_config.Debug)
                        _logger.LogInformation("Pushing metrics to server");

                    if (_networkChecker.IsNetworkAvailable())
                    {
                        FlushMetrics();
                    }
                    else
                    {
                        _logger.LogInformation("Network is offline, skipping metrics post");
                    }

                    Thread.Sleep(delay);
                }
                catch (ApiException ex)
                {
                    SdkCodes.WarnPostingMetricsFailed(_logger, "HTTP code " + ex.ErrorCode);
                    LogUtils.LogException(_config, ex);
                }
                catch (Exception ex)
                {
                    if (!_abortFlag)
                    {
                        SdkCodes.WarnPostingMetricsFailed(_logger, ex.Message);
                        LogUtils.LogException(_config, ex);
                    }
                }

                if (!_abortFlag)
                    Thread.Sleep(delay);

            } while (!_abortFlag);

            FlushMetrics();
            SdkCodes.InfoMetricsThreadExited(_logger);
        }

        private void FlushMetrics()
        {
            var droppedEvaluations = Interlocked.Exchange(ref _metricsEvaluationsDropped, 0);

            if (droppedEvaluations > 0)
            {
                SdkCodes.WarnMetricsBufferFull(_logger, droppedEvaluations);
            }

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Running metrics thread iteration. frequencyMapSize={Count}", _frequencyMap.Count());
            }

            var metricsSnapshot = _frequencyMap.DrainToDictionary();

            if (metricsSnapshot.Count <= 0) return;
            var metrics = PrepareMessageBody(metricsSnapshot);
            if (metrics.MetricsData.Sum(md => md.Count) <= 0 && metrics.TargetData.Count <= 0) return;
            PostMetrics(metrics);
        }

        private void PostMetrics(Metrics metrics)
        {
            _api.PostMetrics(_authInfo.Environment,  _authInfo.ClusterIdentifier, metrics);
        }

        private long GetCurrentUnixTimestampMillis()
        {
            return (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
        }
        private void SetMetricsAttributes(MetricsData metricsData, string key, string value)
        {
            var metricsAttributes = new KeyValue(key, value);
            metricsData.Attributes.Add(metricsAttributes);
        }

        private Metrics PrepareMessageBody(IDictionary<Analytics, Int64> data)
        {
            var metrics = new Metrics();
            metrics.TargetData = new List<TargetData>();
            metrics.MetricsData = new List<MetricsData>();

            foreach (var analytic in data)
            {
                var metricsData = new MetricsData(GetCurrentUnixTimestampMillis(), (int) analytic.Value, MetricsData.MetricsTypeEnum.FFMETRICS, new());

                SetMetricsAttributes(metricsData, FeatureIdentifierAttribute, analytic.Key.Variation.Name);
                SetMetricsAttributes(metricsData, FeatureNameAttribute, analytic.Key.Variation.Name);
                SetMetricsAttributes(metricsData, VariationIdentifierAttribute, analytic.Key.Variation.Identifier);
                SetMetricsAttributes(metricsData, TargetAttribute, analytic.Key.Target.Identifier);
                SetMetricsAttributes(metricsData, SdkType, Client);
                SetMetricsAttributes(metricsData, SdkLanguage, ".NET");
                SetMetricsAttributes(metricsData, SdkVersion, SdkThread.SdkVersion);
                metrics.MetricsData.Add(metricsData);
            }

            return metrics;
        }

        private class FrequencyMap<TK>
        {
            private readonly ConcurrentDictionary<TK, Int64> _freqMap = new();

            internal void Increment(TK key)
            {
                _freqMap.AddOrUpdate(key, 1, (_, v) => v + 1);
            }

            internal int Count()
            {
                return _freqMap.Count;
            }

            internal Int64 Sum()
            {
                return _freqMap.Values.Sum(v => v);
            }

            internal Dictionary<TK, Int64> DrainToDictionary()
            {
                Dictionary<TK, Int64> snapshot = new();

                // Take a snapshot of the ConcurrentDictionary atomically setting each key's value to 0 as we copy it
                foreach (var kvPair in _freqMap)
                {
                    _freqMap.AddOrUpdate(kvPair.Key, 0, (k, v) =>
                    {
                        snapshot.Add(k, v);
                        return 0;
                    });
                }

                // Clean up entries with 0
                foreach (var kvPair in snapshot)
                {
                    // ConcurrentDictionary doesn't have a RemoveIf value==0
                    if (_freqMap.TryGetValue(kvPair.Key, out var existingVal))
                    {
                        if (existingVal == 0)
                        {
                            _freqMap.TryRemove(kvPair.Key, out _);
                        }
                    }
                }

                return snapshot;
            }

            internal bool ContainsKey(TK key)
            {
                return _freqMap.ContainsKey(key);
            }

        }

        private MetricsApi MakeClientApi(AuthInfo authInfo, ILoggerFactory loggerFactory)
        {
            var client = TlsUtils.CreateHttpClientWithTls(_config, loggerFactory);
            client.BaseAddress = new Uri(_config.EventUrl);
            client.Timeout = TimeSpan.FromMilliseconds(SdkThread.DefaultTimeoutMs);
            var api = new MetricsApi(client, _config.EventUrl);
            api.Configuration.DefaultHeaders.Clear();
            SdkThread.AddSdkHeaders(api.Configuration.DefaultHeaders, authInfo);
            return api;
        }

        public void Dispose()
        {
            _abortFlag = true;
            _thread.Interrupt();
            if (!_thread.Join(TimeSpan.FromMinutes(1)))
            {
                _logger.LogWarning("Metrics thread did not shutdown correctly");
            }
            _api?.Dispose();
        }
    }
}