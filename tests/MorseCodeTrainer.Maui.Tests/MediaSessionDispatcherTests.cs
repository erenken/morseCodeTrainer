// Copyright © 2026 myNOC LLC. All rights reserved.
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MorseCodeTrainer.UI.Services;
using NSubstitute;

namespace MorseCodeTrainer.Maui.Tests;

[TestClass]
public sealed class MediaSessionDispatcherTests
{
	[TestMethod]
	public async Task NavigationWaitsForMediaCleanupAfterDispatchReturns()
	{
		var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var capture = Substitute.For<INativeSignalCapture>();
		capture.StopAsync().Returns(stopped.Task);
		await using var provider = CreateServices(capture);

		var suspension = MediaSessionDispatcher.SuspendAsync(action =>
		{
			action(provider);
			return Task.FromResult(true);
		});

		Assert.IsFalse(suspension.IsCompleted);
		stopped.SetResult();
		await suspension;
		await capture.Received(1).StopAsync();
	}

	[TestMethod]
	public async Task DelayedCleanupFailureReachesTheNavigationErrorHandler()
	{
		var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var capture = Substitute.For<INativeSignalCapture>();
		capture.StopAsync().Returns(stopped.Task);
		await using var provider = CreateServices(capture);
		var failure = new InvalidOperationException("Device cleanup failed.");

		var suspension = MediaSessionDispatcher.SuspendAsync(action =>
		{
			action(provider);
			return Task.FromResult(true);
		});
		stopped.SetException(failure);

		var observed = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => suspension);
		Assert.AreSame(failure, observed);
	}

	[TestMethod]
	public async Task UninitializedWebViewDoesNotBlockNavigation()
	{
		await MediaSessionDispatcher.SuspendAsync(_ => Task.FromResult(false));
	}

	private static ServiceProvider CreateServices(INativeSignalCapture capture) => new ServiceCollection()
		.AddSingleton(new MediaSession(Substitute.For<IJSRuntime>(), new BrowserCapturePermission(), capture))
		.BuildServiceProvider();
}
