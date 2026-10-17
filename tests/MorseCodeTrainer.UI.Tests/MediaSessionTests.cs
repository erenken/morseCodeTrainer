// Copyright © 2026 myNOC LLC. All rights reserved.
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MorseCodeTrainer.Core;
using MorseCodeTrainer.UI.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace MorseCodeTrainer.UI.Tests;

#pragma warning disable BL0016 // Mock invocation expectations, not UI interop calls.

[TestClass]
public sealed class MediaSessionTests
{
	private readonly IJSRuntime _js = Substitute.For<IJSRuntime>();
	private readonly IJSObjectReference _module = Substitute.For<IJSObjectReference>();
	private readonly ICapturePermission _permission = Substitute.For<ICapturePermission>();
	private MediaSession Create()
	{
		_js.InvokeAsync<IJSObjectReference>("import", Arg.Any<object?[]>()).Returns(_module);
		return new(_js, _permission);
	}

	[TestMethod]
	public async Task PlaybackUsesOneModuleAndCanBeStopped()
	{
		await using var session = Create();
		await session.StopPlaybackAsync();
		await session.PlayAsync([new(true, 80)], true, false, 600, 0.2);
		await session.PlayAsync([], false, true, 600, 0.2);
		await session.StopPlaybackAsync();
		await _js.Received(1).InvokeAsync<IJSObjectReference>("import", Arg.Any<object?[]>());
		Assert.AreEqual(2, _module.ReceivedCalls().Count(call => Equals(call.GetArguments()[0], "play")));
	}

	[TestMethod]
	public async Task StopDuringModuleImportPreventsLatePlayback()
	{
		var import = new TaskCompletionSource<IJSObjectReference>();
		_js.InvokeAsync<IJSObjectReference>("import", Arg.Any<object?[]>()).Returns(new ValueTask<IJSObjectReference>(import.Task));
		await using var session = new MediaSession(_js, _permission);
		var playing = session.PlayAsync([new(true, 80)], true, false, 600, 0.2);
		await session.StopPlaybackAsync();
		import.SetResult(_module);
		await playing;
		Assert.IsEmpty(_module.ReceivedCalls());
	}

	[TestMethod]
	public async Task RejectsInvalidPlaybackSettings()
	{
		await using var session = Create();
		var outputError = await Assert.ThrowsExactlyAsync<InputValidationException>(() => session.PlayAsync([], false, false, 600, 0.2));
		Assert.AreEqual("Choose sound, light, or both.", UserMessages.FromException(outputError, "Couldn't play the signal."));
		await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => session.PlayAsync([], true, false, 0, 0.2));
		foreach (var volume in new[] { -1.0, 2, double.NaN })
			await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => session.PlayAsync([], true, false, 600, volume));
	}

	[TestMethod]
	[DataRow(CaptureKind.Microphone)] [DataRow(CaptureKind.Camera)]
	public async Task CaptureDecodesBatchesAndReleasesResources(CaptureKind kind)
	{
		await using var session = Create();
		Assert.AreEqual("", session.Transcript); Assert.AreEqual("", session.PendingSymbol);
		var updates = 0; session.Changed += () => updates++;
		session.ReceiveSamples([0, 1]);
		await session.StartCaptureAsync(kind, new(20), 0.6, 600);
		await _permission.Received(1).EnsureGrantedAsync(kind);
		session.ReceiveSamples([0, 1, 10, 1, 60, 0, 70, 0, 200, 0]);
		Assert.AreEqual("E", session.Transcript);
		Assert.AreEqual("", session.PendingSymbol);
		Assert.IsTrue(session.IsCapturing);
		Assert.ThrowsExactly<ArgumentException>(() => session.ReceiveSamples([0]));
		await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => session.StartCaptureAsync(kind, new(), 0.6, 600));
		await session.StopCaptureAsync();
		Assert.IsFalse(session.IsCapturing); Assert.AreEqual(0, session.Level); Assert.AreEqual(2, updates);
	}

	[TestMethod]
	public async Task CaptureFailureAndStartupFailureResetState()
	{
		await using var session = Create();
		await session.StopCaptureAsync();
		await session.StartCaptureAsync(CaptureKind.Camera, new(), 0.6, 600);
		session.CaptureFailed("Disconnected");
		Assert.AreEqual("Disconnected", session.CaptureError); Assert.IsFalse(session.IsCapturing);
		await session.StartCaptureAsync(CaptureKind.Microphone, new(), 0.6, 600);
		Assert.IsNull(session.CaptureError);
		await session.StopCaptureAsync();
		_module.When(m => m.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("startCapture", Arg.Any<object?[]>())).Do(_ => throw new JSException("Denied"));
		await Assert.ThrowsExactlyAsync<JSException>(() => session.StartCaptureAsync(CaptureKind.Microphone, new(), 0.6, 600));
		Assert.IsFalse(session.IsCapturing);
	}

	[TestMethod]
	public async Task PermissionFailureDoesNotStartCapture()
	{
		await using var session = Create();
		_permission.EnsureGrantedAsync(CaptureKind.Camera).ThrowsAsync(new UnauthorizedAccessException());
		await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() => session.StartCaptureAsync(CaptureKind.Camera, new(), 0.6, 600));
		Assert.IsFalse(session.IsCapturing);
		Assert.IsEmpty(_js.ReceivedCalls());
		await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => session.StartCaptureAsync((CaptureKind)8, new(), 0.6, 600));
		await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => session.StartCaptureAsync(CaptureKind.Camera, new(), 0.6, 0));
	}

	[TestMethod]
	public async Task DisposalIsIdempotentAndToleratesDisconnectedViews()
	{
		var session = Create();
		await session.PlayAsync([], true, false, 600, 0.2);
		_module.When(m => m.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>("dispose", Arg.Any<object?[]>())).Do(_ => throw new JSDisconnectedException("Gone"));
		await session.DisposeAsync(); await session.DisposeAsync();
		await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => session.PlayAsync([], true, false, 600, 0.2));
		await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => session.StartCaptureAsync(CaptureKind.Camera, new(), 0.6, 600));
		await Create().DisposeAsync();
	}

	[TestMethod]
	public async Task ServiceRegistrationPreservesNativePermissionOverrides()
	{
		var services = new ServiceCollection();
		services.AddSingleton(_js); services.AddSingleton(_permission); services.AddMorseTrainer();
		await using var provider = services.BuildServiceProvider();
		Assert.AreSame(_permission, provider.GetRequiredService<ICapturePermission>());
		Assert.IsNotNull(provider.GetRequiredService<MediaSession>());
		Assert.IsNotNull(provider.GetRequiredService<MorseTransmission>());
		Assert.IsNotNull(provider.GetRequiredService<TrainingSession>());
		await new BrowserCapturePermission().EnsureGrantedAsync(CaptureKind.Camera);
	}
}
