// Copyright © 2026 myNOC LLC. All rights reserved.
using Android.App;
using Android.Content.PM;
using Android.OS;

namespace MorseCodeTrainer.Maui;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
