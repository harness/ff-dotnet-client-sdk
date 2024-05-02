using System;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace io.harness.ff_dotnet_client_sdk.client
{
    public class FfConfig
    {
        public string ConfigUrl { get; }
        public string EventUrl { get; }
        public int PollIntervalInSeconds { get; }
        public int MetricsIntervalInSeconds { get; }

        public bool StreamEnabled { get; }
        public bool AnalyticsEnabled { get; }

        public int MetricsCapacity { get; }
        public bool Debug { get; }
        public List<X509Certificate2> TlsTrustedCAs { get; }
        public ILoggerFactory LoggerFactory { get; }

        public INetworkChecker NetworkChecker { get; }

        internal FfConfig(string configUrl, string eventUrl, int pollIntervalInSeconds, int metricsIntervalInSeconds, bool streamEnabled, bool analyticsEnabled, int metricsCapacity, bool debug, List<X509Certificate2> tlsTrustedCAs, ILoggerFactory loggerFactory, INetworkChecker networkChecker)
        {
            ConfigUrl = configUrl;
            EventUrl = eventUrl;
            PollIntervalInSeconds =  Math.Max(60, pollIntervalInSeconds);
            MetricsIntervalInSeconds = Math.Max(60, metricsIntervalInSeconds);
            StreamEnabled = streamEnabled;
            AnalyticsEnabled = analyticsEnabled;
            MetricsCapacity = metricsCapacity;
            Debug = debug;
            TlsTrustedCAs = tlsTrustedCAs;
            LoggerFactory = loggerFactory;
            NetworkChecker = networkChecker;
        }

        public static ConfigBuilder Builder()
        {
            return new ConfigBuilder();
        }
    }

    public class ConfigBuilder
    {
        internal static readonly ILoggerFactory DefaultLoggerFactory =  Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning)
                .AddConsole();
        });

        private const int DefaultMetricsCapacity = 1024;
        private const int MinIntervalSeconds = 60;
        private string _configUrl = "https://config.ff.harness.io/api/1.0";
        private string _eventUrl = "https://events.ff.harness.io/api/1.0";
        private int _pollIntervalInSeconds = MinIntervalSeconds;
        private int _metricsIntervalInSeconds = MinIntervalSeconds;
        private bool _streamEnabled = true;
        private bool _analyticsEnabled = true;
        private int _metricsCapacity = DefaultMetricsCapacity;
        private bool _debug = false;
        private List<X509Certificate2> _tlsTrustedCAs = new ();
        private ILoggerFactory _loggerFactory = DefaultLoggerFactory;
        private INetworkChecker _networkChecker = new NullNetworkChecker();

        public FfConfig Build()
        {
            return new FfConfig(_configUrl, _eventUrl, _pollIntervalInSeconds, _metricsIntervalInSeconds, _streamEnabled, _analyticsEnabled, _metricsCapacity, _debug, _tlsTrustedCAs, _loggerFactory, _networkChecker);
        }

        public ConfigBuilder SetPollingInterval(int pollIntervalInSeconds)
        {
            _pollIntervalInSeconds = Math.Max(MinIntervalSeconds, pollIntervalInSeconds);
            return this;
        }

        public ConfigBuilder SetMetricsInterval(int metricsIntervalInSeconds)
        {
            _metricsIntervalInSeconds = Math.Max(MinIntervalSeconds, metricsIntervalInSeconds);
            return this;
        }

        public ConfigBuilder SetStreamEnabled(bool enabled = true)
        {
            _streamEnabled = enabled;
            return this;
        }

        public ConfigBuilder SetAnalyticsEnabled(bool analyticsEnabled = true)
        {
            _analyticsEnabled = analyticsEnabled;
            return this;
        }

        public ConfigBuilder MetricsCapacity(int metricsCapacity)
        {
            _metricsCapacity = Math.Max(DefaultMetricsCapacity, metricsCapacity);
            return this;
        }

        public ConfigBuilder ConfigUrl(string configUrl)
        {
            _configUrl = configUrl;
            return this;
        }

        public ConfigBuilder EventUrl(string eventUrl)
        {
            _eventUrl = eventUrl;
            return this;
        }

        /**
         * <summary>
         * Enables stack trace logging and enables certain INFO level logs that would generally be considered
         * too noisy for logs (e.g. polling)
         * </summary>
         */
        public ConfigBuilder Debug(bool debug)
        {
            _debug = debug;
            return this;
        }

        public ConfigBuilder NetworkChecker(INetworkChecker networkChecker)
        {
            _networkChecker = networkChecker;
            return this;
        }

        /**
          * <summary>
          *     List of trusted CAs - for when the given config/event URLs are signed with a private CA. You
          *     should include intermediate CAs too to allow the HTTP client to build a full trust chain.
          *     <remarks>Requires .NET 5 targets or greater - netstandard and net4xx are not supported.</remarks>
          * </summary>
          */
        public ConfigBuilder TlsTrustedCAs(List<X509Certificate2> certs)
        {
            _tlsTrustedCAs = certs;
            return this;
        }

        public ConfigBuilder LoggerFactory(ILoggerFactory factory)
        {
            _loggerFactory = factory;
            return this;
        }
    }

    public interface INetworkChecker
    {
        bool IsNetworkAvailable();
    }

    internal class NullNetworkChecker : INetworkChecker {
        public bool IsNetworkAvailable()
        {
            return true;
        }
    }

}