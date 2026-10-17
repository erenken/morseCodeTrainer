// Copyright © 2026 myNOC LLC. All rights reserved.
using Microsoft.JSInterop;
using MorseCodeTrainer.Core;

namespace MorseCodeTrainer.UI.Services;

// Interop failures propagate to the UI's operation boundary, which logs diagnostics and shows friendly guidance.
#pragma warning disable BL0016

public enum CaptureKind { Microphone, Camera }

public interface ICapturePermission
{
	Task EnsureGrantedAsync(CaptureKind kind);
}

// Browsers ask for permission as part of getUserMedia; native hosts also need OS permission.
public sealed class BrowserCapturePermission : ICapturePermission
{
	public Task EnsureGrantedAsync(CaptureKind kind) => Task.CompletedTask;
}

public interface INativeSignalCapture : IAsyncDisposable
{
	// The failure callback accepts deliberate user-facing guidance, never an exception's Message.
	Task StartAsync(CaptureKind kind, int frequency, Action<double[]> samples, Action<string> failed);
	Task StopAsync();
}

public sealed class MediaSession(IJSRuntime js, ICapturePermission permission, INativeSignalCapture? nativeCapture = null) : IAsyncDisposable
{
	private IJSObjectReference? _module;
	private DotNetObjectReference<MediaSession>? _callback;
	private StreamingMorseDecoder? _decoder;
	private SignalThreshold? _threshold;
	private double _lastTimestamp;
	private bool _disposed;
	private int _playbackGeneration;
	public string Transcript => _decoder?.Text ?? "";
	public string PendingSymbol => _decoder?.PendingSymbol ?? "";
	public double Level { get; private set; }
	public string? CaptureError { get; private set; }
	public bool IsCapturing { get; private set; }
	public bool UsesNativeCapture => nativeCapture is not null;
	public event Action? Changed;

	private async ValueTask<IJSObjectReference> ModuleAsync()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./_content/MorseCodeTrainer.UI/media.js");
	}

	public async Task PlayAsync(IReadOnlyList<SignalSegment> segments, bool sound, bool light, int frequency, double volume)
	{
		if (!sound && !light) throw new InputValidationException("Choose sound, light, or both.");
		if (frequency is < 200 or > 1500) throw new ArgumentOutOfRangeException(nameof(frequency));
		if (!double.IsFinite(volume) || volume is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(volume));
		var generation = ++_playbackGeneration;
		var module = await ModuleAsync();
		if (generation != _playbackGeneration) return;
		// A long message can exceed the default interop timeout; Stop/disposal ends its promise.
		await module.InvokeVoidAsync("play", CancellationToken.None, segments, sound, light, frequency, volume);
	}

	public async Task StopPlaybackAsync()
	{
		_playbackGeneration++;
		if (_module is not null) await _module.InvokeVoidAsync("stopPlayback");
	}

	public async Task StartCaptureAsync(CaptureKind kind, MorseTiming timing, double threshold, int frequency)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		if (IsCapturing) throw new InvalidOperationException("Stop the current receiver first.");
		if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
		if (frequency is < 200 or > 1500) throw new ArgumentOutOfRangeException(nameof(frequency));
		var gate = new SignalThreshold(threshold * 0.7, threshold);
		await permission.EnsureGrantedAsync(kind);
		ObjectDisposedException.ThrowIf(_disposed, this);
		_decoder = new(timing);
		_threshold = gate;
		_lastTimestamp = 0;
		CaptureError = null;
		_callback ??= DotNetObjectReference.Create(this);
		IsCapturing = true;
		try
		{
			if (nativeCapture is not null)
				await nativeCapture.StartAsync(kind, frequency, ReceiveSamples, CaptureFailed);
			else
				await (await ModuleAsync()).InvokeVoidAsync("startCapture", kind == CaptureKind.Camera ? "camera" : "microphone", _callback, frequency);
		}
		catch
		{
			IsCapturing = false;
			throw;
		}
	}

	[JSInvokable]
	public void ReceiveSamples(double[] samples)
	{
		if (!IsCapturing || _decoder is null || _threshold is null) return;
		if (samples.Length % 2 != 0) throw new ArgumentException("Expected timestamp/level pairs.", nameof(samples));
		for (var i = 0; i < samples.Length; i += 2)
		{
			Level = samples[i + 1];
			_decoder.AddSample(_threshold.Process(Level), samples[i]);
			_lastTimestamp = samples[i];
		}
		Changed?.Invoke();
	}

	[JSInvokable]
	public void CaptureFailed(string message)
	{
		CaptureError = message;
		IsCapturing = false;
		Level = 0;
		_decoder?.Finish(_lastTimestamp);
		Changed?.Invoke();
	}

	public async Task StopCaptureAsync()
	{
		if (nativeCapture is not null) await nativeCapture.StopAsync();
		if (_module is not null) await _module.InvokeVoidAsync("stopCapture");
		IsCapturing = false;
		_decoder?.Finish(_lastTimestamp);
		Level = 0;
		Changed?.Invoke();
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed) return;
		_disposed = true;
		_playbackGeneration++;
		IsCapturing = false;
		try
		{
			if (_module is not null)
			{
				await _module.InvokeVoidAsync("dispose");
				await _module.DisposeAsync();
			}
		}
		catch (JSDisconnectedException) { /* The web view has already closed. */ }
		finally
		{
			_callback?.Dispose();
			if (nativeCapture is not null) await nativeCapture.DisposeAsync();
		}
	}
}
