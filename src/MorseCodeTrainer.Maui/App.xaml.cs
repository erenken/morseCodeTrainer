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
		return new Window(new MainPage()) { Title = "Morse Code Trainer" };
	}
}
