// Copyright © 2026 myNOC LLC. All rights reserved.
using Android.Webkit;

namespace MorseCodeTrainer.Maui;

internal sealed class CaptureWebChromeClient : WebChromeClient
{
	public override async void OnPermissionRequest(PermissionRequest? request)
	{
		if (request is null) return;
		try
		{
			// Only bundled content may use already-approved native device permissions.
			if (!Uri.TryCreate(request.Origin?.ToString(), UriKind.Absolute, out var origin) ||
				origin.Scheme != "https" || origin.Host != "0.0.0.0" || !origin.IsDefaultPort) { request.Deny(); return; }
			var approved = new List<string>();
			foreach (var resource in request.GetResources() ?? [])
			{
				if (resource == PermissionRequest.ResourceAudioCapture && await Permissions.CheckStatusAsync<Permissions.Microphone>() == PermissionStatus.Granted)
					approved.Add(resource);
				if (resource == PermissionRequest.ResourceVideoCapture && await Permissions.CheckStatusAsync<Permissions.Camera>() == PermissionStatus.Granted)
					approved.Add(resource);
			}
			if (approved.Count > 0) request.Grant(approved.ToArray()); else request.Deny();
		}
		catch { request.Deny(); }
	}
}
