// Copyright © 2026 myNOC LLC. All rights reserved.
using MorseCodeTrainer.UI.Services;

namespace MorseCodeTrainer.Maui;

public static class MediaSessionDispatcher
{
	public static async Task SuspendAsync(Func<Action<IServiceProvider>, Task<bool>> dispatch)
	{
		// BlazorWebView dispatch accepts an Action, so await the media task separately.
		// A WebView that has not initialized has no media session to stop.
		var suspension = Task.CompletedTask;
		await dispatch(services => { suspension = services.GetRequiredService<MediaSession>().SuspendAsync(); });
		await suspension;
	}
}
