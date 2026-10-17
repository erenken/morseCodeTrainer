// Copyright © 2026 myNOC LLC. All rights reserved.
using Android.App;
using Android.Runtime;

namespace MorseCodeTrainer.Maui;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
