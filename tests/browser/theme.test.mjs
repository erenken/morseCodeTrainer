// Copyright © 2026 myNOC LLC. All rights reserved.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { Script } from "node:vm";

const sourcePath = fileURLToPath(
	new URL("../../src/MorseCodeTrainer.UI/wwwroot/theme.js", import.meta.url),
);
const script = new Script(readFileSync(sourcePath, "utf8"), {
	filename: sourcePath,
});
const storageKey = "morse-code-trainer.theme";

function createPage({
	dark = false,
	storage = new Map(),
	failRead = false,
	failWrite = false,
	withMeta = true,
} = {}) {
	const events = new Map();
	const systemEvents = new Map();
	const root = { dataset: {} };
	const meta = withMeta ? { content: "#080d18" } : null;
	const warnings = [];
	const system = {
		matches: dark,
		addEventListener: (event, handler) => systemEvents.set(event, handler),
	};
	script.runInNewContext({
		document: {
			documentElement: root,
			querySelector: () => meta,
			addEventListener: (event, handler) => events.set(event, handler),
		},
		window: {
			matchMedia: (query) => {
				assert.equal(query, "(prefers-color-scheme: dark)");
				return system;
			},
			localStorage: {
				getItem: (key) => {
					if (failRead) throw new Error("Storage access denied");
					return storage.get(key) ?? null;
				},
				setItem: (key, value) => {
					if (failWrite) throw new Error("Storage quota exceeded");
					storage.set(key, value);
				},
			},
		},
		console: { warn: (...args) => warnings.push(args) },
	});
	return {
		root,
		meta,
		storage,
		warnings,
		click: (target) => events.get("click")({ target }),
		toggle: () => events.get("click")({
			target: {
				closest: (selector) => {
					assert.equal(selector, "[data-theme-toggle]");
					return {}; // A new button or a child icon after a Blazor render.
				},
			},
		}),
		changeSystem: (darkMode) => {
			system.matches = darkMode;
			systemEvents.get("change")();
		},
	};
}

for (const dark of [false, true]) {
	test(`uses the ${dark ? "dark" : "light"} system theme before rendering`, () => {
		const page = createPage({ dark });
		assert.equal(page.root.dataset.theme, dark ? "dark" : "light");
		assert.equal(page.meta.content, dark ? "#080d18" : "#e8eef7");
		assert.equal(page.storage.size, 0);
	});
}

for (const saved of ["dark", "light"]) {
	test(`restores saved ${saved} mode instead of the system preference`, () => {
		const page = createPage({
			dark: saved === "light",
			storage: new Map([[storageKey, saved]]),
		});
		assert.equal(page.root.dataset.theme, saved);
		page.changeSystem(saved === "light");
		assert.equal(page.root.dataset.theme, saved);
	});
}

test("ignores an invalid stored value and follows system changes", () => {
	const page = createPage({ storage: new Map([[storageKey, "invalid"]]) });
	assert.equal(page.root.dataset.theme, "light");
	page.changeSystem(true);
	assert.equal(page.root.dataset.theme, "dark");
	page.changeSystem(false);
	assert.equal(page.root.dataset.theme, "light");
});

test("toggles in both directions, persists across reloads, and keeps an explicit choice", () => {
	const page = createPage();
	page.toggle();
	assert.equal(page.root.dataset.theme, "dark");
	assert.equal(page.storage.get(storageKey), "dark");
	assert.equal(createPage({ storage: page.storage }).root.dataset.theme, "dark");
	page.changeSystem(false);
	assert.equal(page.root.dataset.theme, "dark");
	page.toggle();
	assert.equal(page.root.dataset.theme, "light");
	assert.equal(page.meta.content, "#e8eef7");
	assert.equal(page.storage.get(storageKey), "light");
	assert.equal(createPage({ dark: true, storage: page.storage }).root.dataset.theme, "light");
});

test("unrelated clicks and non-element event targets leave the theme alone", () => {
	const page = createPage({ dark: true });
	for (const target of [null, {}, { closest: () => null }]) page.click(target);
	assert.equal(page.root.dataset.theme, "dark");
	assert.equal(page.storage.size, 0);
});

test("blocked storage reads still allow system defaults and theme selection", () => {
	const page = createPage({ dark: true, failRead: true });
	assert.equal(page.root.dataset.theme, "dark");
	page.toggle();
	assert.equal(page.root.dataset.theme, "light");
	assert.equal(page.warnings.length, 1);
	assert.match(page.warnings[0][0], /read the saved color theme/);
});

test("blocked storage writes keep the chosen theme for the current session", () => {
	const page = createPage({ failWrite: true });
	page.toggle();
	page.changeSystem(false);
	assert.equal(page.root.dataset.theme, "dark");
	assert.equal(page.storage.size, 0);
	assert.equal(page.warnings.length, 1);
	assert.match(page.warnings[0][0], /save the color theme/);
});

test("a missing browser-color tag does not prevent changing themes", () => {
	const page = createPage({ withMeta: false });
	page.toggle();
	assert.equal(page.root.dataset.theme, "dark");
});
