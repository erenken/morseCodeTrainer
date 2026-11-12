// Copyright © 2026 myNOC LLC. All rights reserved.
using MorseCodeTrainer.UI.Services;

namespace MorseCodeTrainer.UI.Tests;

[TestClass]
public sealed class AdaptiveLayoutTests
{
	[TestMethod]
	public void FullyOpenDisplayUsesAllAvailableSpace()
	{
		Assert.AreEqual(new LayoutRegion(0, 0, 900, 700), AdaptiveLayout.FindUsableRegion(900, 700, []));
	}

	[TestMethod]
	public void BookPoseKeepsControlsBesideTheFoldAndRestoresTheExpandedLayout()
	{
		var fold = new LayoutRegion(440, 0, 20, 700);
		Assert.AreEqual(new LayoutRegion(0, 0, 440, 700), AdaptiveLayout.FindUsableRegion(900, 700, [fold]));
		Assert.AreEqual(new LayoutRegion(0, 0, 900, 700), AdaptiveLayout.FindUsableRegion(900, 700, []));
	}

	[TestMethod]
	public void TabletopPoseUsesTheTopRegionWhenBothSidesAreEqual()
	{
		Assert.AreEqual(new LayoutRegion(0, 0, 700, 440), AdaptiveLayout.FindUsableRegion(700, 900, [new(0, 440, 700, 20)]));
	}

	[TestMethod]
	public void ActiveCameraAndFoldAreBothAvoided()
	{
		var usable = AdaptiveLayout.FindUsableRegion(900, 700, [new(440, 0, 20, 700), new(700, -10, 70, 50)]);
		Assert.AreEqual(new LayoutRegion(0, 0, 440, 700), usable);
	}

	[TestMethod]
	public void CameraPartiallyOutsideTheViewUsesLocalClippedCoordinates()
	{
		Assert.AreEqual(new LayoutRegion(0, 40, 400, 760), AdaptiveLayout.FindUsableRegion(400, 800, [new(150, -10, 100, 50)]));
	}

	[TestMethod]
	public void LargerUsableSideIsChosenAndOffscreenRegionsDoNotShrinkIt()
	{
		Assert.AreEqual(new LayoutRegion(320, 0, 580, 700), AdaptiveLayout.FindUsableRegion(900, 700, [new(300, 0, 20, 700), new(-50, -50, 10, 10), new(0, 0, 0, 700)]));
	}

	[TestMethod]
	public void ZeroSizeAndEntirelyCoveredViewsHaveNoInteractiveArea()
	{
		Assert.AreEqual(default(LayoutRegion), AdaptiveLayout.FindUsableRegion(0, 0, []));
		Assert.AreEqual(default(LayoutRegion), AdaptiveLayout.FindUsableRegion(100, 100, [new(-1, -1, 102, 102)]));
	}

	[TestMethod]
	public void InvalidViewportDimensionsAreRejected()
	{
		foreach (var size in new[] { -1.0, double.NaN, double.PositiveInfinity })
		{
			Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => AdaptiveLayout.FindUsableRegion(size, 100, []));
			Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => AdaptiveLayout.FindUsableRegion(100, size, []));
		}
	}
}
