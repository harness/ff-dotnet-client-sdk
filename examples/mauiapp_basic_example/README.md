# Running the MAUI example

Before running the MAUI example make sure you have added your FF SDK client key to `ff-dotnet-client-sdk/examples/mauiapp_basic_example/FeatureFlagsContext.cs` under `TestApiKey`

## Android
Use `adb` or Android Device manager in Android studio to start an emulator then run:
```
cd examples/mauiapp_basic_example
dotnet build -t:Run -f net8.0-android
```
More info at the Microsoft MAUI site [Android emulator](https://learn.microsoft.com/en-us/dotnet/maui/android/emulator/?view=net-maui-8.0)

## iOS
Use `open -a Simulator.app` or XCode to start a simulator.
```
cd examples/mauiapp_basic_example
dotnet build -t:Run -f net8.0-ios
```
More info at the Microsoft MAUI site [Build an iOS app with .NET CLI](https://learn.microsoft.com/en-us/dotnet/maui/ios/cli?view=net-maui-8.0)

## MacOS
```
cd examples/mauiapp_basic_example
dotnet build -t:Run -f net8.0-maccatalyst
```
More info at the Microsoft MAUI site [Build a Mac Catalyst app with .NET CLI](https://learn.microsoft.com/en-us/dotnet/maui/mac-catalyst/cli?view=net-maui-8.0)
