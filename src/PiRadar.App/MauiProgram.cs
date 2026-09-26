using System.Globalization;
using Microsoft.Extensions.Logging;
using MudBlazor;
using MudBlazor.Services;
using PiRadar.App.Services;
using PiRadar.Core.Abstractions;
using PiRadar.Data;
using Plugin.Maui.OCR;

namespace PiRadar.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		// Textos, fechas y números de la UI en español de Colombia, sin importar el idioma del teléfono.
		var colombia = CultureInfo.GetCultureInfo("es-CO");
		CultureInfo.DefaultThreadCurrentCulture = colombia;
		CultureInfo.DefaultThreadCurrentUICulture = colombia;

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseOcr()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();
		builder.Services.AddMudServices(config =>
		{
			config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomCenter;
			config.SnackbarConfiguration.VisibleStateDuration = 3000;
			config.SnackbarConfiguration.ShowCloseIcon = false;
		});
		builder.Services.AddTransient<MudLocalizer, SpanishMudLocalizer>();
		builder.Services.AddSingleton<ThemeState>();
		builder.Services.AddSingleton<TransactionsViewState>();
		builder.Services.AddSingleton<AnalyticsViewState>();
		builder.Services.AddSingleton<ScanState>();

		builder.Services.AddSingleton<IClock, SystemClock>();
		builder.Services.AddSingleton(SecureStorage.Default);
		builder.Services.AddSingleton<IDatabaseKeyProvider, SecureStorageDatabaseKeyProvider>();
		builder.Services.AddSingleton(sp =>
			new DatabaseHost(sp.GetRequiredService<IDatabaseKeyProvider>(), DatabaseLocation.GetDatabasePath()));
		builder.Services.AddPiRadarData();
		builder.Services.AddSingleton<PiRadar.Core.Abstractions.IOcrService>(sp => new PluginOcrService(sp.GetRequiredService<Plugin.Maui.OCR.IOcrService>()));

#if IOS
		// El CSS ya respeta el notch y la barra de gestos con env(safe-area-inset-*); si el scroll de WKWebView
		// también los suma, la barra inferior queda flotando y la altura visible se encoge.
		Microsoft.AspNetCore.Components.WebView.Maui.BlazorWebViewHandler.BlazorWebViewMapper.AppendToMapping(
			"NoSafeAreaInsets",
			(handler, _) => handler.PlatformView.ScrollView.ContentInsetAdjustmentBehavior =
				UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never);
#endif

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
		builder.Logging.AddProvider(new FileLoggerProvider());
#endif

		return builder.Build();
	}
}
