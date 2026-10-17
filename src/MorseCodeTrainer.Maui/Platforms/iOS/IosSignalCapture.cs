// Copyright © 2026 myNOC LLC. All rights reserved.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using AVFoundation;
using CoreFoundation;
using CoreMedia;
using CoreVideo;
using Foundation;
using MorseCodeTrainer.Core;
using MorseCodeTrainer.UI.Services;
using UIKit;

namespace MorseCodeTrainer.Maui;

/// <summary>Native capture avoids WKWebView restrictions on media capture from app:// URLs.</summary>
[SupportedOSPlatform("ios")]
public sealed class IosSignalCapture : INativeSignalCapture
{
	private AVAudioEngine? _engine;
	private AVCaptureSession? _camera;
	private AVCaptureDeviceInput? _input;
	private AVCaptureVideoDataOutput? _output;
	private CameraDelegate? _cameraDelegate;
	private DispatchQueue? _queue;
	private NSObject? _backgroundObserver, _interruptionObserver;
	private readonly Stopwatch _clock = new();
	private int _generation;
	private bool _tapInstalled;

	public async Task StartAsync(CaptureKind kind, int frequency, Action<double[]> samples, Action<string> failed)
	{
		await StopAsync();
		var generation = _generation;
		_clock.Restart();
		void Emit(double level)
		{
			var timestamp = _clock.Elapsed.TotalMilliseconds;
			MainThread.BeginInvokeOnMainThread(() => { if (generation == _generation) samples([timestamp, level]); });
		}
		void Fail(string message) => MainThread.BeginInvokeOnMainThread(async () =>
		{
			if (generation != _generation) return;
			await StopAsync();
			failed(message);
		});
		try
		{
			_backgroundObserver = NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.DidEnterBackgroundNotification,
				_ => Fail("Capture stopped while the app was in the background. Start receiving to continue."));
			if (kind == CaptureKind.Microphone)
			{
				var session = AVAudioSession.SharedInstance();
				session.SetCategory(AVAudioSessionCategory.PlayAndRecord, AVAudioSessionCategoryOptions.DefaultToSpeaker, out var categoryError);
				if (categoryError is not null) throw new InvalidOperationException(categoryError.LocalizedDescription);
				session.SetActive(true, out var activeError);
				if (activeError is not null) throw new InvalidOperationException(activeError.LocalizedDescription);
				_interruptionObserver = NSNotificationCenter.DefaultCenter.AddObserver(AVAudioSession.InterruptionNotification,
					_ => Fail("Microphone capture was interrupted. Start receiving to continue."));
				_engine = new AVAudioEngine();
				var input = _engine.InputNode;
				var format = input.GetBusOutputFormat(0);
				if (format.SampleRate <= 0 || format.ChannelCount == 0) throw new InvalidOperationException("No microphone input is available.");
				AVAudioNodeTapBlock tap = (buffer, _) =>
				{
					try
					{
						var data = new float[buffer.FrameLength];
						Marshal.Copy(Marshal.ReadIntPtr(buffer.FloatChannelData), data, 0, data.Length);
						Emit(ToneDetector.Measure(data, format.SampleRate, frequency));
					}
					catch { Fail("Unable to read the microphone signal."); }
				};
				if (OperatingSystem.IsIOSVersionAtLeast(27))
				{
					if (!input.InstallTapOnBus(0, 512, format, out var tapError, tap))
						throw new InvalidOperationException(tapError?.LocalizedDescription ?? "Unable to start microphone sampling.");
				}
				else input.InstallTapOnBus(0, 512, format, tap);
				_tapInstalled = true;
				_engine.Prepare();
				if (!_engine.StartAndReturnError(out var error)) throw new InvalidOperationException(error?.LocalizedDescription ?? "Microphone unavailable.");
			}
			else
			{
				_camera = new AVCaptureSession { SessionPreset = AVCaptureSession.Preset640x480 };
				using var device = AVCaptureDevice.GetDefaultDevice(AVMediaTypes.Video) ?? throw new InvalidOperationException("No camera found.");
				_input = AVCaptureDeviceInput.FromDevice(device, out var error);
				if (_input is null || error is not null) throw new InvalidOperationException(error?.LocalizedDescription ?? "Camera unavailable.");
				if (!_camera.CanAddInput(_input)) throw new InvalidOperationException("Camera input is unavailable.");
				_camera.AddInput(_input);
				_output = new AVCaptureVideoDataOutput
				{
					AlwaysDiscardsLateVideoFrames = true,
					UncompressedVideoSetting = new AVVideoSettingsUncompressed(new CVPixelBufferAttributes { PixelFormatType = CVPixelFormatType.CV32BGRA }.Dictionary)
				};
				_cameraDelegate = new CameraDelegate(Emit, Fail);
				_queue = new DispatchQueue("com.mynoc.morse.camera");
				_output.SetSampleBufferDelegate(_cameraDelegate, _queue);
				if (!_camera.CanAddOutput(_output)) throw new InvalidOperationException("Camera output is unavailable.");
				_camera.AddOutput(_output);
				_interruptionObserver = NSNotificationCenter.DefaultCenter.AddObserver(AVCaptureSession.RuntimeErrorNotification,
					_ => Fail("Camera capture was interrupted. Start receiving to continue."), _camera);
				await Task.Run(_camera.StartRunning);
			}
		}
		catch { await StopAsync(); throw; }
	}

	public async Task StopAsync()
	{
		_generation++;
		RemoveObserver(ref _backgroundObserver);
		RemoveObserver(ref _interruptionObserver);
		if (_engine is not null)
		{
			if (_tapInstalled) _engine.InputNode.RemoveTapOnBus(0);
			_tapInstalled = false;
			_engine.Stop(); _engine.Dispose(); _engine = null;
			AVAudioSession.SharedInstance().SetActive(false, out _);
		}
		if (_camera is not null) { await Task.Run(_camera.StopRunning); _camera.Dispose(); _camera = null; }
		_output?.Dispose(); _output = null;
		_input?.Dispose(); _input = null;
		_cameraDelegate?.Dispose(); _cameraDelegate = null;
		_queue?.Dispose(); _queue = null;
		_clock.Stop();
	}

	private static void RemoveObserver(ref NSObject? observer)
	{
		if (observer is null) return;
		NSNotificationCenter.DefaultCenter.RemoveObserver(observer);
		observer.Dispose(); observer = null;
	}
	public async ValueTask DisposeAsync() => await StopAsync();

	private sealed class CameraDelegate(Action<double> emit, Action<string> failed) : AVCaptureVideoDataOutputSampleBufferDelegate
	{
		public override void DidOutputSampleBuffer(AVCaptureOutput captureOutput, CMSampleBuffer sampleBuffer, AVCaptureConnection connection)
		{
			try
			{
				using var pixels = sampleBuffer.GetImageBuffer() as CVPixelBuffer;
				if (pixels is null) return;
				pixels.Lock(CVPixelBufferLock.ReadOnly);
				try
				{
					double total = 0; var count = 0;
					var width = (int)pixels.Width; var height = (int)pixels.Height; var stride = (int)pixels.BytesPerRow;
					for (var y = height * 2 / 5; y < height * 3 / 5; y += 4)
					for (var x = width * 2 / 5; x < width * 3 / 5; x += 4)
					{
						var offset = y * stride + x * 4;
						total += 0.0722 * Marshal.ReadByte(pixels.BaseAddress, offset) + 0.7152 * Marshal.ReadByte(pixels.BaseAddress, offset + 1) + 0.2126 * Marshal.ReadByte(pixels.BaseAddress, offset + 2);
						count++;
					}
					emit(count == 0 ? 0 : total / (count * 255));
				}
				finally { pixels.Unlock(CVPixelBufferLock.ReadOnly); }
			}
			catch { failed("Unable to read the camera signal."); }
			finally { sampleBuffer.Dispose(); }
		}
	}
}
