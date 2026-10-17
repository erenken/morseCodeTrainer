// Copyright © 2026 myNOC LLC. All rights reserved.
using MorseCodeTrainer.UI.Services;
using Microsoft.JSInterop;
using NSubstitute;

namespace MorseCodeTrainer.UI.Tests;

[TestClass]
public sealed class NativeCaptureTests
{
	[TestMethod]
	public async Task NativeCaptureUsesTheSameDecoderAndReleasesTheDevice()
	{
		var native = Substitute.For<INativeSignalCapture>();
		var js = Substitute.For<IJSRuntime>();
		var permission = Substitute.For<ICapturePermission>();
		Action<double[]>? samples = null;
		native.StartAsync(CaptureKind.Camera, 600, Arg.Do<Action<double[]>>(callback => samples = callback), Arg.Any<Action<string>>()).Returns(Task.CompletedTask);
		var session = new MediaSession(js, permission, native);
		Assert.IsTrue(session.UsesNativeCapture);
		await session.StartCaptureAsync(CaptureKind.Camera, new(20), 0.6, 600);
		samples!([0, 1, 10, 1, 60, 0, 70, 0, 200, 0]);
		Assert.AreEqual("E", session.Transcript);
		Assert.IsEmpty(js.ReceivedCalls());
		await session.StopCaptureAsync();
		await native.Received(1).StopAsync();
		await session.DisposeAsync();
		await native.Received(1).DisposeAsync();
	}
}
