// Copyright © 2026 myNOC LLC. All rights reserved.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { Script } from "node:vm";

const sourcePath = fileURLToPath(new URL("../../src/MorseCodeTrainer.UI/wwwroot/theme.js", import.meta.url));
const script = new Script(readFileSync(sourcePath, "utf8"), { filename: sourcePath });

function createPage({ dark = false, withMeta = true } = {}) {
	const events = new Map();
	const root = { dataset: {} };
	const meta = withMeta ? { content: "" } : null;
	const system = {
		matches: dark,
		addEventListener: (event, handler) => events.set(`system:${event}`, handler),
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
			addEventListener: (event, handler) => events.set(event, handler),
			get localStorage() { throw new Error("Theme must never access saved preferences"); },
		},
	});
	return { root, meta, system, notify: (event) => events.get(event)() };
}

for (const dark of [false, true]) {
	test(`uses the ${dark ? "dark" : "light"} system appearance before rendering without reading saved choices`, () => {
		const page = createPage({ dark });
		assert.equal(page.root.dataset.theme, dark ? "dark" : "light");
		assert.equal(page.meta.content, dark ? "#080d18" : "#e8eef7");
	});
}

for (const event of ["system:change", "pageshow", "visibilitychange"]) {
	test(`tracks both theme changes on ${event}`, () => {
		const page = createPage();
		page.system.matches = true;
		page.notify(event);
		assert.equal(page.root.dataset.theme, "dark");
		assert.equal(page.meta.content, "#080d18");
		page.system.matches = false;
		page.notify(event);
		assert.equal(page.root.dataset.theme, "light");
		assert.equal(page.meta.content, "#e8eef7");
	});
}

test("a missing browser-color tag does not prevent system appearance changes", () => {
	const page = createPage({ withMeta: false });
	page.system.matches = true;
	page.notify("system:change");
	assert.equal(page.root.dataset.theme, "dark");
});
