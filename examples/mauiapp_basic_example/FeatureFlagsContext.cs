using System.Diagnostics;
using io.harness.ff_dotnet_client_sdk.client;
using io.harness.ff_dotnet_client_sdk.client.dto;
using Microsoft.Extensions.Logging;

namespace MauiApp_basic;

public class FeatureFlagsContext : IFeatureFlagsContext
{
    private readonly FfClient _ffClient;

    private readonly bool _initFailed;
    private readonly Exception _initException;

    internal static readonly string TestFlagIdentifier = "harnessappdemodarkmode";
    internal static readonly string TestApiKey = ""; // <--- ENTER YOUR API KEY HERE

    public FeatureFlagsContext(ILoggerFactory loggerFactory)
    {
        try
        {

            var config = FfConfig.Builder()
                .LoggerFactory(loggerFactory)
                .SetStreamEnabled(true)
                .Debug(true)
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
           _initFailed = true;
           _initException = ex;
        }
    }

    public bool InitFailed(out Exception ex)
    {
        ex = _initException;
        return _initFailed;
    }

    public bool IsFlagEnabled()
    {
        return _ffClient.BoolVariation(TestFlagIdentifier, false);
    }

    public void Dispose()
    {
        _ffClient.Dispose();
    }
}