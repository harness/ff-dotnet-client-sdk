using System.Diagnostics;
using io.harness.ff_dotnet_client_sdk.client;
using io.harness.ff_dotnet_client_sdk.client.dto;
using Serilog;
using Serilog.Extensions.Logging;

namespace getting_started;
using System;

public static class Program
{

    public static void Main(string[] args)
    {
        string? apiKey = Environment.GetEnvironmentVariable("FF_API_KEY");
        string flagName = Environment.GetEnvironmentVariable("FF_FLAG_NAME") ?? "harnessappdemodarkmode";

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(flagName)) throw new Exception("Please set FF_API_KEY and FF_FLAG_NAME");

        var loggerFactory = new SerilogLoggerFactory(
            new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console()
                .CreateLogger());



        var config = FFConfig.Builder().LoggerFactory(loggerFactory).Debug(true).Build();

        /*
         * Define your target (and attributes for any server side rules you may have)
         */
        FFTarget target = new FFTarget("dotnetclientsdk", ".NET Client SDK",
            new Dictionary<string, string> { { "email", "person@myorg.com" }});

        /*
         * Setup the SDK
         */
        using var client = new FFClient();
        client.Initialize(apiKey, config, target);

        if (!client.WaitForInitialization(600_000))
            throw new Exception("Timed out waiting for SDK to initialize");


        /*
         * Iterate a few times
         */

        for (var i = 1; i < 100; i++)
        {
            var value = client.BoolVariation(flagName, false);
            Console.Out.WriteLine("flag {0} = {1}", flagName, value);

            Thread.Sleep(TimeSpan.FromSeconds(1));
        }

    }
}