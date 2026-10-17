// Copyright © 2026 myNOC LLC. All rights reserved.
using Microsoft.AspNetCore.Components.WebView;
namespace MorseCodeTrainer.Maui;

public partial class MainPage : ContentPage
{
	public MainPage() => InitializeComponent();

	private void OnWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e)
	{
#if ANDROID
		e.WebView.SetWebChromeClient(new CaptureWebChromeClient());
#endif
	}
}
