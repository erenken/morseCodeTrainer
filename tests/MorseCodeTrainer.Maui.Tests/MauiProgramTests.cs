// Copyright © 2026 myNOC LLC. All rights reserved.
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MorseCodeTrainer.Core;
using MorseCodeTrainer.UI.Services;
using NSubstitute;

namespace MorseCodeTrainer.Maui.Tests;

[TestClass]
public sealed class MauiProgramTests
{
	[TestMethod]
	public async Task RegistersNativePermissionsAndSharedTrainerServices()
	{
		var services = new ServiceCollection();
		services.AddSingleton(Substitute.For<IJSRuntime>());
		MauiProgram.ConfigureServices(services);
		await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
		await using var scope = provider.CreateAsyncScope();
		Assert.IsInstanceOfType<NativeCapturePermission>(scope.ServiceProvider.GetRequiredService<ICapturePermission>());
		Assert.IsInstanceOfType<MorseCodec>(scope.ServiceProvider.GetRequiredService<IMorseCodec>());
		Assert.IsNotNull(scope.ServiceProvider.GetRequiredService<MorseTransmission>());
		Assert.IsNotNull(scope.ServiceProvider.GetRequiredService<TrainingSession>());
		Assert.IsFalse(scope.ServiceProvider.GetRequiredService<MediaSession>().UsesNativeCapture);
	}

	[TestMethod]
	[DataRow(CaptureKind.Camera)]
	[DataRow(CaptureKind.Microphone)]
	public async Task WindowsLeavesCapturePermissionPromptsToWebView(CaptureKind kind)
	{
		await new NativeCapturePermission().EnsureGrantedAsync(kind);
	}
}
