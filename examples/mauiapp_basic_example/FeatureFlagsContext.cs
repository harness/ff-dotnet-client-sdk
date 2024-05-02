using System.Diagnostics;
using io.harness.ff_dotnet_client_sdk.client;
using io.harness.ff_dotnet_client_sdk.client.dto;
using Microsoft.Extensions.Logging;

namespace MauiApp_basic;

public class FeatureFlagsContext : IFeatureFlagsContext
{
    private readonly FfClient _ffClient;

    private bool _failed;
    private Exception _failedException;

    internal static readonly string TestFlagIdentifier = "harnessappdemodarkmode";
    internal static readonly string TestApiKey = ""; // <--- ENTER YOUR API KEY HERE

    private class MauiNetworkChecker : INetworkChecker
    {
        /*
         * By default, the SDK is generic and doesn't support MAUI network detection out of the box. The SDK assumes it
         * always has a connection to the network. If running under MAUI/iOS, MAUI/Android or similar mobile device then
         * we need to provide a network connection tester like below else you will get a lot of exceptions logged on
         * the device. The SDK will call this function before attempting to poll, connect to a stream or push metrics.
         */
        public bool IsNetworkAvailable()
        {
            return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        }
    }

    public FeatureFlagsContext(ILoggerFactory loggerFactory)
    {
        try
        {

            var config = FfConfig.Builder()
                .LoggerFactory(loggerFactory)
                .SetStreamEnabled(true)
                .Debug(true)
                .NetworkChecker(new MauiNetworkChecker())
                .Build();

            FfTarget target = new FfTarget("dotnetclientsdk_maui", ".NET Client SDK MAUI",
                new Dictionary<string, string> { { "email", "person@myorg.com" } });

            var client = new FfClient();
            client.Initialize(TestApiKey, config, target);
            client.WaitForInitialization();

            _ffClient = client;
        }
        catch (Exception ex)
        {
           Trace.WriteLine("Exception in FeatureFlagsContext: " + ex);
           _failed = true;
           _failedException = ex;
        }
    }

    public bool HasFailed(out Exception ex)
    {
        ex = _failedException;
        return _failed;
    }

    public bool IsFlagEnabled()
    {
        try
        {
            return _ffClient.BoolVariation(TestFlagIdentifier, false);
        }
        catch (Exception ex)
        {
            Trace.WriteLine("Exception in IsFlagEnabled: " + ex);
            _failed = true;
            _failedException = ex;
            return false;
        }
    }

    public void Dispose()
    {
        _ffClient.Dispose();
    }
}