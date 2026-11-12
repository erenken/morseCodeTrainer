// Copyright © 2026 myNOC LLC. All rights reserved.
// Run before the stylesheet and Blazor to follow the browser/WebView appearance before paint.
(() => {
	const systemTheme = window.matchMedia("(prefers-color-scheme: dark)");
	function applyTheme() {
		const dark = systemTheme.matches;
		document.documentElement.dataset.theme = dark ? "dark" : "light";
		const browserColor = document.querySelector('meta[name="theme-color"]');
		if (browserColor) browserColor.content = dark ? "#080d18" : "#e8eef7";
	}
	applyTheme();
	systemTheme.addEventListener("change", applyTheme);
	// Recheck after returning from the background or the browser's back/forward cache.
	window.addEventListener("pageshow", applyTheme);
	document.addEventListener("visibilitychange", applyTheme);
})();
