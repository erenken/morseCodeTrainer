// Copyright © 2026 myNOC LLC. All rights reserved.
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MorseCodeTrainer.Core;

namespace MorseCodeTrainer.UI.Services;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddMorseTrainer(this IServiceCollection services)
	{
		services.TryAddSingleton<IMorseCodec, MorseCodec>();
		services.TryAddSingleton<IRandomSource, RandomSource>();
		services.TryAddScoped<ICapturePermission, BrowserCapturePermission>();
		services.AddScoped<MorseTransmission>();
		services.AddScoped<TrainingSession>();
		services.AddScoped<MediaSession>();
		return services;
	}
}
