.NET client SDK for Feature Flags
========================

## Intro

Use this README to get started with our Feature Flags (FF) SDK for .NET.
This guide outlines the basics of getting started with the SDK and
provides a full code sample for you to try out.

## Which SDK to use

This is a client SDK. For .NET Harness provides both client and server
SDKs. You should choose the correct one for your needs. A client SDK
should typically be used in environments where network bandwidth and
processing power are limited. A client API key provides restricted access
to flag state for one target only. Whereas a server SDK will download rule
information and process evaluations locally for many targets. A server
SDK API key should only be used in secure environments. See
[.NET Server SDK](https://github.com/harness/ff-dotnet-server-sdk)


## Requirements
The library is packaged as multi-target supporting  `net461`,`netstandard2.0`,
`net5.0`, `net6.0`, `net7.0` and `net8.0`.

https://developer.harness.io/docs/feature-flags/get-started/java-quickstart#create-an-sdk-key

## Build Requirements
If building from source you will need [.Net 7.0.404](https://dotnet.microsoft.com/en-us/download/dotnet/7.0) or newer (dotnet --version)<br>

## Quickstart
To follow along with our test code sample, make sure you’ve:

- [Created a Feature Flag on the Harness Platform](https://ngdocs.harness.io/article/1j7pdkqh7j-create-a-feature-flag) called `test`
- [Created a client SDK key and made a copy of it](https://developer.harness.io/docs/feature-flags/get-started/java-quickstart#create-an-sdk-key)

### Install the SDK
Add the sdk using dotnet
```bash
dotnet add package ff-dotnet-client-sdk
```

## Examples

### Getting started
See [getting_started](getting_started)

This is a generic .NET application that targets `.net8.0`

### MAUI
See [mauiapp_basic_example](mauiapp_basic_example).

This is an example that shows you the client SDK working inside a
[MAUI](https://dotnet.microsoft.com/en-us/apps/maui) app. MAUI allows
you to deploy and target multiple platforms (such as Android, iOS, macOS)
while keeping a single portable codebase.

### Code Sample
The following is a complete code example that you can use to test the `harnessappdemodarkmode` Flag you created on the Harness Platform. When you run the code it will:
- Connect to the FF service.
- Report the value of the Flag every second until the connection is closed. Every time the `harnessappdemodarkmode` Flag is toggled on or off on the Harness Platform, the updated value is reported.
- Close the SDK with the `using` statement.


```
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
        var apiKey = Environment.GetEnvironmentVariable("FF_API_KEY");
        var flagName = Environment.GetEnvironmentVariable("FF_FLAG_NAME") ?? "harnessappdemodarkmode";

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(flagName)) throw new Exception("Please set FF_API_KEY and FF_FLAG_NAME");

        var loggerFactory = new SerilogLoggerFactory(
            new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console()
                .CreateLogger());

        var config = FfConfig.Builder().LoggerFactory(loggerFactory).Debug(true).Build();

        /*
         * Define your target (and attributes for any server side rules you may have)
         */
        FfTarget target = new FfTarget("dotnetclientsdk", ".NET Client SDK",
            new Dictionary<string, string> { { "email", "person@myorg.com" }});

        /*
         * Set up the SDK. It implements `IDisposable`, 'using' will free it when it goes out of scope
         */
        using var client = new FfClient();
        client.Initialize(apiKey, config, target);

        if (!client.WaitForInitialization(60_000))
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
```

### Running the example

```bash
export FF_API_KEY=<your key here>
dotnet run --project examples/getting_started/
```

### Running the example with Docker
If you don't have the right version of dotnet installed locally, or don't want to install the dependencies you can
use docker to quicky get started

```bash
docker run -v $(pwd):/app -w /app -e FF_API_KEY=$FF_API_KEY mcr.microsoft.com/dotnet/sdk:8.0 dotnet run --framework net8.0 --project examples/getting_started/
```