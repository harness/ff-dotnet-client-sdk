using io.harness.ff_dotnet_client_sdk.client;
using io.harness.ff_dotnet_client_sdk.client.dto;
using Microsoft.Extensions.Logging;

namespace MauiApp_basic;

public class FeatureFlagsContext : IFeatureFlagsContext
{
    private readonly FfClient _ffClient;

    internal static readonly string TestFlagIdentifier = "harnessappdemodarkmode";

    public FeatureFlagsContext(ILoggerFactory loggerFactory)
    {
        var config = FfConfig.Builder()
            .LoggerFactory(loggerFactory)
            .SetStreamEnabled(true)
            .Debug(true)
            .Build();

        FfTarget target = new FfTarget("dotnetclientsdk_maui", ".NET Client SDK MAUI",
            new Dictionary<string, string> { { "email", "person@myorg.com" }});

        var client = new FfClient();
        client.Initialize(Environment.GetEnvironmentVariable("FF_API_KEY"), config, target);
        client.WaitForInitialization();

        _ffClient = client;
    }

    public bool IsTestFlagEnabled()
    {
        return _ffClient.BoolVariation(TestFlagIdentifier, false);
    }

    public void Dispose()
    {
        _ffClient.Dispose();
    }
}