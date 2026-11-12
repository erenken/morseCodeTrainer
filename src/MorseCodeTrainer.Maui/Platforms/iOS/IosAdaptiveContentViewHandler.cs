// Copyright © 2026 myNOC LLC. All rights reserved.
using System.Runtime.Versioning;
using CoreGraphics;
using Microsoft.Maui.Handlers;
using MorseCodeTrainer.UI.Services;
using UIKit;
using PlatformContentView = Microsoft.Maui.Platform.ContentView;

namespace MorseCodeTrainer.Maui;

[SupportedOSPlatform("ios27.1")]
public sealed class IosAdaptiveContentViewHandler : ContentViewHandler
{
	protected override PlatformContentView CreatePlatformView() => new RegionAwareView
	{
		CrossPlatformLayout = VirtualView,
		ApplyRegion = (region, width, height) =>
		{
			if (VirtualView is not AdaptiveContentView view) return;
			var padding = new Thickness(region.X, region.Y, width - region.Right, height - region.Bottom);
			if (view.Padding != padding) view.Padding = padding;
		}
	};

	protected override void DisconnectHandler(PlatformContentView platformView)
	{
		((RegionAwareView)platformView).ApplyRegion = null;
		base.DisconnectHandler(platformView);
	}

	private sealed class RegionAwareView : PlatformContentView
	{
		private CGSize _lastSize;
		public Action<LayoutRegion, double, double>? ApplyRegion { get; set; }

		public override void LayoutSubviews()
		{
			base.LayoutSubviews();
			if (_lastSize == Bounds.Size) return;
			_lastSize = Bounds.Size;
			SetNeedsUpdateConstraints();
		}

		public override void UpdateConstraints()
		{
			base.UpdateConstraints();
			if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
			using var occlusion = UIViewReservedRegionKind.CreateOcclusion();
			using var division = UIViewReservedRegionKind.CreateDivision();
			// UIKit tracks queries made here and invalidates constraints when regions change.
			// Frames are local points and already include the system's clearance margins.
			var regions = GetReservedRegions(occlusion).Concat(GetReservedRegions(division))
				.Where(r => r.Active)
				.Select(r => new LayoutRegion(r.Frame.X, r.Frame.Y, r.Frame.Width, r.Frame.Height));
			var width = (double)Bounds.Width;
			var height = (double)Bounds.Height;
			ApplyRegion?.Invoke(AdaptiveLayout.FindUsableRegion(width, height, regions), width, height);
		}
	}
}
