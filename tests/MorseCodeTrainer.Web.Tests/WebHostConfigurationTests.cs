// Copyright © 2026 myNOC LLC. All rights reserved.
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MorseCodeTrainer.Core;
using MorseCodeTrainer.UI;
using MorseCodeTrainer.UI.Services;
using NSubstitute;

namespace MorseCodeTrainer.Web.Tests;

[TestClass]
public sealed class WebHostConfigurationTests
{
	[TestMethod]
	public void MountsSharedTrainerAtTheSpaEntryPoint()
	{
		var roots = new RootComponentMappingCollection();
		WebHostConfiguration.Configure(roots, new ServiceCollection());
		Assert.HasCount(1, roots);
		Assert.AreEqual(typeof(Trainer), roots[0].ComponentType);
		Assert.AreEqual("#app", roots[0].Selector);
	}

	[TestMethod]
	public async Task ResolvesTrainerWithBrowserCaptureAndScopedSessions()
	{
		var services = new ServiceCollection();
		services.AddSingleton(Substitute.For<IJSRuntime>());
		WebHostConfiguration.Configure(new RootComponentMappingCollection(), services);
		await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
		await using var first = provider.CreateAsyncScope();
		await using var second = provider.CreateAsyncScope();
		Assert.IsInstanceOfType<BrowserCapturePermission>(first.ServiceProvider.GetRequiredService<ICapturePermission>());
		var media = first.ServiceProvider.GetRequiredService<MediaSession>();
		Assert.IsFalse(media.UsesNativeCapture);
		Assert.AreSame(media, first.ServiceProvider.GetRequiredService<MediaSession>());
		Assert.AreNotSame(media, second.ServiceProvider.GetRequiredService<MediaSession>());
		Assert.AreNotSame(first.ServiceProvider.GetRequiredService<TrainingSession>(), second.ServiceProvider.GetRequiredService<TrainingSession>());
		Assert.AreSame(first.ServiceProvider.GetRequiredService<IMorseCodec>(), second.ServiceProvider.GetRequiredService<IMorseCodec>());
		Assert.IsNotNull(first.ServiceProvider.GetRequiredService<MorseTransmission>());
	}
}
