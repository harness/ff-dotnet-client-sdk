using io.harness.ff_dotnet_client_sdk.client;
using io.harness.ff_dotnet_client_sdk.client.dto;

using Microsoft.Extensions.Logging;
//using NLog;
//using NLog.Extensions.Logging;

using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace MauiApp_basic;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});


		builder.Logging.AddDebug();

		builder.Logging.AddConsole();


		builder.Services.AddSingleton<IFeatureFlagsContext>(sp => new FeatureFlagsContext(sp.GetRequiredService<ILoggerFactory>()));
		//builder.Services.AddSingleton<FeatureFlagsContext>();
		//builder.Services.AddSingleton<IFeatureFlagsContext, FeatureFlagsContext>();
		var app = builder.Build();
		ServiceHelper.Initialize(app.Services);
		return app;
	}
}
