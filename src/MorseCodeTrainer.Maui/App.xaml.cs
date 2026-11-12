// Copyright © 2026 myNOC LLC. All rights reserved.
namespace MorseCodeTrainer.Maui;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		#if IOS || ANDROID
		return new Window(new MobileShell()) { Title = "Morse Code Trainer" };
#else
		return new Window(new MainPage()) { Title = "Morse Code Trainer" };
#endif
	}
}
