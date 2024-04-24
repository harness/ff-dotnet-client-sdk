using io.harness.ff_dotnet_client_sdk.client;
using io.harness.ff_dotnet_client_sdk.client.dto;
using WireMock.Logging;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;

namespace ff_client_sdk_test;

[TestFixture]
public class FFClientTest
{
    private WireMockServer _server;

    [SetUp]
    public void StartMockServer()
    {
        _server = WireMockServer.Start(new WireMockServerSettings
        {
            Logger = new WireMockConsoleLogger()
        });
    }

    [TearDown]
    public void StopMockServer()
    {
        _server.Stop();
    }

#pragma warning disable CS8625
    [Test]
    public void ShouldThrowExceptionIfApiKeyIsBlankOrNull()
    {
        var target = new FFTarget("dotNETTest", "dotNETTest");
        var config = FfConfig.Builder().Build();
        
        using var client = new FFClient();


        Assert.Throws<FFClientException>(
            delegate { client.Initialize("", config, target); });
        
        Assert.Throws<FFClientException>(
            delegate { client.Initialize(null, config, target); });

        Assert.Throws<FFClientException>(
            delegate { client.Initialize("dummy", null, null); });

        Assert.Throws<FFClientException>(
            delegate { client.Initialize("dummy", config, null); });

        Assert.Throws<FFClientException>(
            delegate { client.Initialize("dummy", null, target); });
    }
#pragma warning restore CS8625
}