using io.harness.ff_dotnet_client_sdk.client.dto;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Text;
using io.harness.ff_dotnet_client_sdk.client.impl;
using io.harness.ff_dotnet_client_sdk.openapi.Model;

namespace io.harness.ff_dotnet_client_sdk.client
{
    public class FFClient : IDisposable
    {
        private ILoggerFactory _loggerFactory = ConfigBuilder.DefaultLoggerFactory;
        private ILogger<FFClient> _logger = ConfigBuilder.DefaultLoggerFactory.CreateLogger<FFClient>();

        private SdkThread? _sdkThread;
        private MetricsThread? _metricsThread;
        private FfConfig? _configuration;

        public void Initialize(string apiKey, FfConfig config, FFTarget target)
        {
            try {
                _configuration = config;
                _loggerFactory = config.LoggerFactory;
                _logger = _loggerFactory.CreateLogger<FFClient>();

                SdkCodes.LogUtils.Setup(config);

                if (string.IsNullOrEmpty(apiKey)) {
                    SdkCodes.ErrorMissingSdkKey(_logger);
                    throw new FFClientException("Missing SDK key");
                }

                if (target == null || _configuration == null) {
                    throw new FFClientException("Target and configuration must not be null!");
                }

                if (!target.IsValid()) {
                    throw new FFClientException("Target not valid");
                }

                _sdkThread = new SdkThread(apiKey, config, target, _loggerFactory);

                if (config.AnalyticsEnabled) {
                    if (string.IsNullOrEmpty(config.EventUrl)) {
                        throw new FFClientException("Event URL is null or empty");
                    }

                    if (_sdkThread.WaitForInitialization(-1))
                    {
                        _metricsThread = new MetricsThread(target, config, _loggerFactory, _sdkThread.GetAuthInfo());
                    }
                }
            } catch (Exception e) {
                _sdkThread?.Dispose();
                _logger.LogWarning(e, e.Message);
                throw new FFClientException("Initialize failed", e);
            }
        }

        public void WaitForInitialization()
        {
            WaitForInitialization(-1);
        }

        public bool WaitForInitialization(int timeoutMs)
        {
            if (_sdkThread == null) throw new FFClientException("Initialize() not called");
            return _sdkThread.WaitForInitialization(timeoutMs);
        }

        public bool BoolVariation(string evaluationId, bool defaultValue)
        {
            return XVariation(evaluationId, defaultValue, eval => bool.Parse(eval.Value));
        }

        public string StringVariation(string evaluationId, string defaultValue)
        {
            return XVariation(evaluationId, defaultValue, eval => eval.Value);
        }

        public double NumberVariation(string evaluationId, double defaultValue)
        {
            return XVariation(evaluationId, defaultValue, eval => double.Parse(eval.Value));
        }

        public JObject JsonVariation(string evaluationId, JObject defaultValue)
        {
            return XVariation<JObject>(evaluationId, defaultValue, eval => JObject.Parse(eval.Value));
        }

        private T XVariation<T>(string evaluationId, T defaultValue, Func<Evaluation, T> evalToPrimitive)
        {
            var defaultValueStr = defaultValue?.ToString() ?? "null";

            if (_sdkThread == null) {
                SdkCodes.WarnDefaultVariationServed(_logger, evaluationId, defaultValueStr, "Initialize() not called");
                return defaultValue;
            }

            var failureReason = new StringBuilder();
            var evaluation = _sdkThread.GetEvaluationById(evaluationId, failureReason);

            if (evaluation == null || string.IsNullOrEmpty(evaluation.Value))
            {
                failureReason.Append(evaluationId).Append(" not in cache");
                SdkCodes.WarnDefaultVariationServed(_logger, evaluationId, defaultValueStr, failureReason.ToString());
                return defaultValue;
            }

            RegisterEvaluation(evaluationId, evaluation);

            return evalToPrimitive.Invoke(evaluation);
        }

        private void RegisterEvaluation(string evaluationId, Evaluation evaluation)
        {
            if (_configuration?.AnalyticsEnabled ?? false)
            {
                Variation variation = new Variation(evaluation.Identifier, evaluation.Value, evaluationId);
                _metricsThread?.RegisterEvaluation(evaluationId, variation);
            }
        }

        public void Dispose()
        {
            _logger.LogTrace("SDK is shutting down. Waiting for threads to complete");
            _metricsThread?.Dispose();
            _sdkThread?.Dispose();
            _loggerFactory.Dispose();
            _logger.LogTrace("SDK exited");
        }
    }

}