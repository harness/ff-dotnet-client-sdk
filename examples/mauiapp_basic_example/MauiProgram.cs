
using Microsoft.Extensions.Logging;

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
		var app = builder.Build();
		ServiceHelper.Initialize(app.Services);
		return app;
	}
}
