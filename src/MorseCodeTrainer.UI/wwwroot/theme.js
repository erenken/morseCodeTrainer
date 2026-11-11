// Copyright © 2026 myNOC LLC. All rights reserved.
// Run before the stylesheet and Blazor so the saved theme is applied before paint.
(() => {
	const storageKey = "morse-code-trainer.theme";
	const root = document.documentElement;
	const systemTheme = window.matchMedia("(prefers-color-scheme: dark)");
	let preference = null;

	try {
		const saved = window.localStorage.getItem(storageKey);
		if (saved === "light" || saved === "dark") preference = saved;
	} catch (error) {
		console.warn("Couldn't read the saved color theme.", error);
	}

	function applyTheme() {
		const theme = preference ?? (systemTheme.matches ? "dark" : "light");
		root.dataset.theme = theme;
		const browserColor = document.querySelector('meta[name="theme-color"]');
		if (browserColor) {
			browserColor.content = theme === "dark" ? "#080d18" : "#e8eef7";
		}
	}

	applyTheme();
	systemTheme.addEventListener("change", () => {
		if (preference === null) applyTheme();
	});

	// Delegation survives Blazor rendering the toolbar or changing pages.
	document.addEventListener("click", (event) => {
		if (!event.target?.closest?.("[data-theme-toggle]")) return;
		preference = root.dataset.theme === "dark" ? "light" : "dark";
		applyTheme();
		try {
			window.localStorage.setItem(storageKey, preference);
		} catch (error) {
			// The selection still works for this session when storage is unavailable.
			console.warn("Couldn't save the color theme.", error);
		}
	});
})();
