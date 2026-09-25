using Microsoft.Extensions.Logging;
using PiRadar.App.Services;
using PiRadar.Core.Abstractions;
using PiRadar.Data;
using Plugin.Maui.OCR;

namespace PiRadar.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseOcr()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

		builder.Services.AddSingleton<IClock, SystemClock>();
		builder.Services.AddSingleton(SecureStorage.Default);
		builder.Services.AddSingleton<IDatabaseKeyProvider, SecureStorageDatabaseKeyProvider>();
		builder.Services.AddSingleton(sp =>
			new DatabaseHost(sp.GetRequiredService<IDatabaseKeyProvider>(), DatabaseLocation.GetDatabasePath()));
		builder.Services.AddPiRadarData();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
