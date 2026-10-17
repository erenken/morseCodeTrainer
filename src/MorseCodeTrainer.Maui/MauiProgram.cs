// Copyright © 2026 myNOC LLC. All rights reserved.
using Microsoft.Extensions.Logging;
using MorseCodeTrainer.UI.Services;

namespace MorseCodeTrainer.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder().UseMauiApp<App>();
		builder.Services.AddMauiBlazorWebView();
		ConfigureServices(builder.Services);
#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif
		return builder.Build();
	}

	public static void ConfigureServices(IServiceCollection services)
	{
		services.AddScoped<ICapturePermission, NativeCapturePermission>();
#if IOS
		services.AddScoped<INativeSignalCapture, IosSignalCapture>();
#endif
		services.AddMorseTrainer();
	}
}
