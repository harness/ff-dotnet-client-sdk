using io.harness.ff_dotnet_client_sdk.client;
using io.harness.ff_dotnet_client_sdk.client.dto;
using Microsoft.Extensions.Logging;

namespace MauiApp_basic;

public class FeatureFlagsContext : IFeatureFlagsContext
{
    private readonly FfClient _ffClient;


    private void Run()
    {
        var loggerFactory = ServiceHelper.Services.GetRequiredService <ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<FeatureFlagsContext>();

        logger.LogInformation("THREAD START");
        Thread.Sleep(TimeSpan.FromSeconds(60));
        logger.LogInformation("THREAD DONE");
    }

    public FeatureFlagsContext(ILoggerFactory loggerFactory)
    {

       // var t = new Thread(Run);
       // t.Start();


        var config = FfConfig.Builder()
            .LoggerFactory(loggerFactory)
            .SetStreamEnabled(true)
            .Debug(true)
            .Build();

        FfTarget target = new FfTarget("dotnetclientsdk", ".NET Client SDK MAUI",
            new Dictionary<string, string> { { "email", "person@myorg.com" }});

        var client = new FfClient();
        client.Initialize("a1c75d25-a9c0-4338-88ae-620a3dd86e59", config, target);
        client.WaitForInitialization();

        _ffClient = client;
    }

    public bool IsTestFlagEnabled()
    {
        return _ffClient.BoolVariation("test", false);
    }

    public void Dispose()
    {
        _ffClient.Dispose();
    }
}