// Copyright © 2026 myNOC LLC. All rights reserved.
using MorseCodeTrainer.UI.Services;

namespace MorseCodeTrainer.Maui;

public sealed class NativeCapturePermission : ICapturePermission
{
	public async Task EnsureGrantedAsync(CaptureKind kind)
	{
#if ANDROID || IOS
		var granted = await MainThread.InvokeOnMainThreadAsync(async () => kind == CaptureKind.Camera
			? await Permissions.RequestAsync<Permissions.Camera>()
			: await Permissions.RequestAsync<Permissions.Microphone>());
		if (granted != PermissionStatus.Granted)
			throw new UnauthorizedAccessException($"Allow {kind.ToString().ToLowerInvariant()} access in your device settings to receive Morse.");
#else
		// Windows uses the WebView2 permission prompt and Windows privacy settings.
		await Task.CompletedTask;
#endif
	}
}
