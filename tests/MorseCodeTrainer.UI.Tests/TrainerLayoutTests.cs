// Copyright © 2026 myNOC LLC. All rights reserved.
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using MorseCodeTrainer.UI.Services;
using NSubstitute;

namespace MorseCodeTrainer.UI.Tests;

[TestClass]
public sealed class TrainerLayoutTests
{
	[TestMethod]
	[DataRow("Practice", "Find the rhythm.")]
	[DataRow("Translate", "Give your message a voice.")]
	[DataRow("Receive", "message in the noise.")]
	[DataRow("Reference", "Every character has a rhythm.")]
	[DataRow("About", "myNOC LLC")]
	public async Task NativeTabsRenderTheirSectionWithoutDuplicateWebNavigation(string section, string content)
	{
		var html = await RenderAsync(new() { [nameof(Trainer.InitialTab)] = section, [nameof(Trainer.UseNativeNavigation)] = true });
		StringAssert.Contains(html, content);
		StringAssert.Contains(html, "native-navigation");
		Assert.IsFalse(html.Contains("Main navigation", StringComparison.Ordinal));
		Assert.IsFalse(html.Contains("theme-toggle", StringComparison.Ordinal));
	}

	[TestMethod]
	public async Task BrowserKeepsNavigationAndUsesDefaultSignalSettings()
	{
		var html = await RenderAsync(new());
		StringAssert.Contains(html, "Main navigation");
		StringAssert.Contains(html, "15 WPM");
		StringAssert.Contains(html, "12 WPM");
		StringAssert.Contains(html, "600 Hz");
		StringAssert.Contains(html, "20%");
		Assert.IsFalse(html.Contains("theme-toggle", StringComparison.Ordinal));
	}

	[TestMethod]
	public async Task TabsUseTheSameSignalSettingsAfterAnotherTabChangesThem()
	{
		var settings = new SignalSettings { WordsPerMinute = 24, EffectiveWordsPerMinute = 18, Frequency = 750, Volume = 0.4, Sound = false, Light = true };
		foreach (var section in new[] { "Practice", "Translate", "Receive", "Reference" })
		{
			var html = await RenderAsync(new() { [nameof(Trainer.InitialTab)] = section, [nameof(Trainer.Settings)] = settings });
			StringAssert.Contains(html, "24 WPM");
			StringAssert.Contains(html, "18 WPM");
			StringAssert.Contains(html, "750 Hz");
			StringAssert.Contains(html, "40%");
			StringAssert.Contains(html, "checked /> Screen light");
			Assert.IsFalse(html.Contains("checked /> Sound", StringComparison.Ordinal));
		}
	}

	[TestMethod]
	[DataRow(12, 20, 12)]
	[DataRow(20, 12, 12)]
	[DataRow(20, 20, 20)]
	public void ChangingCharacterSpeedKeepsEffectiveSpeedWithinIt(int speed, int effective, int expected)
	{
		var settings = new SignalSettings { WordsPerMinute = speed, EffectiveWordsPerMinute = effective };
		settings.ClampEffectiveSpeed();
		Assert.AreEqual(expected, settings.EffectiveWordsPerMinute);
		Assert.AreEqual(speed, settings.WordsPerMinute);
	}

	[TestMethod]
	public async Task ReturningToAnExistingTabRefreshesItsSharedSignalControls()
	{
		var settings = new SignalSettings();
		settings.NotifyChanged(); // First appearance can precede WebView initialization.
		await using var provider = CreateServices();
		await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
		await renderer.Dispatcher.InvokeAsync(async () =>
		{
			var output = await renderer.RenderComponentAsync<Trainer>(ParameterView.FromDictionary(new Dictionary<string, object?>
			{
				[nameof(Trainer.InitialTab)] = "Translate",
				[nameof(Trainer.Settings)] = settings
			}));
			StringAssert.Contains(output.ToHtmlString(), "15 WPM");
			settings.WordsPerMinute = 24;
			settings.Frequency = 750;
			settings.NotifyChanged();
			var refreshed = output.ToHtmlString();
			StringAssert.Contains(refreshed, "24 WPM");
			StringAssert.Contains(refreshed, "750 Hz");
			StringAssert.Contains(refreshed, "Give your message a voice.");
		});
	}

	private static async Task<string> RenderAsync(Dictionary<string, object?> parameters)
	{
		await using var provider = CreateServices();
		await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
		return await renderer.Dispatcher.InvokeAsync(async () =>
		{
			var output = await renderer.RenderComponentAsync<Trainer>(ParameterView.FromDictionary(parameters));
			return output.ToHtmlString();
		});
	}

	private static ServiceProvider CreateServices()
	{
		var services = new ServiceCollection();
		services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
		services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
		services.AddSingleton(Substitute.For<IJSRuntime>());
		services.AddMorseTrainer();
		return services.BuildServiceProvider();
	}
}
