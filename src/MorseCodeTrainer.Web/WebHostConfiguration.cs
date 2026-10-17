// Copyright © 2026 myNOC LLC. All rights reserved.
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MorseCodeTrainer.UI;
using MorseCodeTrainer.UI.Services;

namespace MorseCodeTrainer.Web;

public static class WebHostConfiguration
{
	public static void Configure(RootComponentMappingCollection rootComponents, IServiceCollection services)
	{
		rootComponents.Add<Trainer>("#app");
		services.AddMorseTrainer();
	}
}
