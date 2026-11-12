// Copyright © 2026 myNOC LLC. All rights reserved.
namespace MorseCodeTrainer.UI.Services;

public readonly record struct LayoutRegion(double X, double Y, double Width, double Height)
{
	public double Right => X + Width;
	public double Bottom => Y + Height;
}

public static class AdaptiveLayout
{
	// Keep one continuous WebView in the largest usable rectangle. It can reflow without
	// reloading, losing input, or placing controls beneath a fold or an active camera.
	public static LayoutRegion FindUsableRegion(double width, double height, IEnumerable<LayoutRegion> reserved)
	{
		if (!double.IsFinite(width) || width < 0) throw new ArgumentOutOfRangeException(nameof(width));
		if (!double.IsFinite(height) || height < 0) throw new ArgumentOutOfRangeException(nameof(height));
		var candidates = new List<LayoutRegion> { new(0, 0, width, height) };
		foreach (var region in reserved)
		{
			if (region.Width <= 0 || region.Height <= 0) continue;
			var next = new List<LayoutRegion>();
			foreach (var candidate in candidates)
			{
				var left = Math.Max(candidate.X, region.X);
				var top = Math.Max(candidate.Y, region.Y);
				var right = Math.Min(candidate.Right, region.Right);
				var bottom = Math.Min(candidate.Bottom, region.Bottom);
				if (left >= right || top >= bottom) { next.Add(candidate); continue; }
				Add(new(candidate.X, candidate.Y, candidate.Width, top - candidate.Y));
				Add(new(candidate.X, bottom, candidate.Width, candidate.Bottom - bottom));
				Add(new(candidate.X, candidate.Y, left - candidate.X, candidate.Height));
				Add(new(right, candidate.Y, candidate.Right - right, candidate.Height));
				void Add(LayoutRegion available) { if (available.Width > 0 && available.Height > 0) next.Add(available); }
			}
			candidates = next;
		}
		// A stable top/leading preference prevents equal-sized regions from alternating.
		return candidates.OrderByDescending(r => r.Width * r.Height).ThenBy(r => r.Y).ThenBy(r => r.X).FirstOrDefault();
	}
}
