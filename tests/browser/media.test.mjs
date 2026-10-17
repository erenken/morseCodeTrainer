// Copyright © 2026 myNOC LLC. All rights reserved.
import { test, beforeEach, afterEach } from "node:test";
import assert from "node:assert/strict";

let now = 0,
	sequence = 0,
	timers,
	frames,
	events,
	tracks,
	calls,
	nodes,
	video,
	lamp,
	denied,
	resumeGate,
	streamGate;
const documentEvents = {};
const original = {
	setTimeout,
	clearTimeout,
	performance,
	navigator: globalThis.navigator,
};
globalThis.document = {
	addEventListener: (name, fn) => {
		documentEvents[name] = fn;
	},
};
const media = await import("../../src/MorseCodeTrainer.UI/wwwroot/media.js");

beforeEach(() => {
	now = 0;
	sequence = 0;
	timers = new Map();
	frames = new Map();
	events = {};
	tracks = [];
	calls = [];
	nodes = [];
	denied = false;
	resumeGate = null;
	streamGate = null;
	globalThis.setTimeout = (fn, delay) => {
		const id = ++sequence;
		timers.set(id, { at: now + delay, fn });
		return id;
	};
	globalThis.clearTimeout = (id) => timers.delete(id);
	globalThis.requestAnimationFrame = (fn) => {
		const id = ++sequence;
		frames.set(id, fn);
		return id;
	};
	globalThis.cancelAnimationFrame = (id) => frames.delete(id);
	Object.defineProperty(globalThis, "performance", {
		configurable: true,
		value: { now: () => now },
	});
	lamp = {
		lit: false,
		classList: {
			add() {},
			toggle: (_, on) => (lamp.lit = on),
			remove: () => (lamp.lit = false),
		},
	};
	video = {
		videoWidth: 640,
		videoHeight: 480,
		srcObject: null,
		play: async () => {},
		pause: () => {},
	};
	document.hidden = false;
	document.getElementById = (id) => (id === "signal-lamp" ? lamp : video);
	document.createElement = () => ({
		getContext: () => ({
			drawImage() {},
			getImageData: () => ({
				data: new Uint8ClampedArray(24 * 24 * 4).fill(255),
			}),
		}),
	});
	const track = {
		stopped: false,
		stop() {
			this.stopped = true;
		},
		addEventListener: (name, fn) => {
			events[name] = fn;
		},
	};
	tracks.push(track);
	Object.defineProperty(globalThis, "navigator", {
		configurable: true,
		value: {
			mediaDevices: {
				getUserMedia: async () => {
					if (denied) throw new Error("Permission denied");
					return streamGate ?? { getTracks: () => tracks };
				},
			},
		},
	});
	globalThis.AudioContext = class {
		get currentTime() {
			return now / 1000;
		}
		sampleRate = 48000;
		destination = {};
		async resume() {
			if (resumeGate) await resumeGate;
		}
		async close() {}
		createGain() {
			return {
				gain: {
					setValueAtTime: (...args) => calls.push(args),
					linearRampToValueAtTime: (...args) => calls.push(args),
				},
				connect() {},
				disconnect() {},
			};
		}
		createOscillator() {
			const node = {
				frequency: {},
				connect() {},
				disconnect() {},
				start() {},
				stop() {
					this.stopped = true;
				},
			};
			nodes.push(node);
			return node;
		}
		createMediaStreamSource() {
			return { connect() {}, disconnect() {} };
		}
		createAnalyser() {
			return {
				frequencyBinCount: 512,
				getFloatFrequencyData: (bins) => {
					bins.fill(-100);
					bins[13] = -20;
				},
				disconnect() {},
			};
		}
	};
});

afterEach(async () => {
	await media.dispose();
	globalThis.setTimeout = original.setTimeout;
	globalThis.clearTimeout = original.clearTimeout;
	Object.defineProperty(globalThis, "performance", {
		configurable: true,
		value: original.performance,
	});
	Object.defineProperty(globalThis, "navigator", {
		configurable: true,
		value: original.navigator,
	});
});

const settle = async () => {
	for (let i = 0; i < 12; i++) await Promise.resolve();
};
async function advance(milliseconds) {
	const end = now + milliseconds;
	while (true) {
		const next = [...timers]
			.filter(([, t]) => t.at <= end)
			.sort((a, b) => a[1].at - b[1].at)[0];
		if (!next) break;
		now = next[1].at;
		timers.delete(next[0]);
		await next[1].fn();
	}
	now = end;
	const pending = [...frames.values()];
	frames.clear();
	pending.forEach((fn) => fn());
	await settle();
}
const callback = () => ({
	samples: [],
	failures: [],
	async invokeMethodAsync(method, data) {
		this[method === "ReceiveSamples" ? "samples" : "failures"].push(data);
	},
});

test("schedules tones on the audio clock and flashes only during signal segments", async () => {
	const playing = media.play(
		[
			{ isOn: true, durationMilliseconds: 100 },
			{ isOn: false, durationMilliseconds: 100 },
			{ isOn: true, durationMilliseconds: 300 },
		],
		true,
		true,
		600,
		0.2,
	);
	await settle();
	await advance(100);
	assert.equal(lamp.lit, true);
	await advance(100);
	assert.equal(lamp.lit, false);
	assert.equal(calls.length, 8);
	assert.equal(calls[0][1], 0.08);
	await advance(500);
	await playing;
	assert.equal(lamp.lit, false);
	assert.equal(nodes[0].stopped, true);
});

test("light-only playback and manual cancellation clean up timers", async () => {
	const playing = media.play(
		[{ isOn: true, durationMilliseconds: 1000 }],
		false,
		true,
		600,
		0.2,
	);
	await advance(100);
	assert.equal(lamp.lit, true);
	media.stopPlayback();
	await playing;
	assert.equal(timers.size, 0);
	assert.equal(lamp.lit, false);
	assert.equal(nodes.length, 0);
});

test("stop during audio startup prevents delayed playback", async () => {
	let resume;
	resumeGate = new Promise((resolve) => (resume = resolve));
	const playing = media.play(
		[{ isOn: true, durationMilliseconds: 1000 }],
		true,
		true,
		600,
		0.2,
	);
	media.stopPlayback();
	resume();
	await playing;
	assert.equal(nodes.length, 0);
});

test("microphone batches timestamped tone-band levels and disconnects", async () => {
	const sink = callback();
	await media.startCapture("microphone", sink, 600);
	await advance(140);
	assert.ok(sink.samples.length >= 2);
	assert.equal(sink.samples[0][1], 1);
	await media.stopCapture();
	assert.equal(tracks[0].stopped, true);
	assert.equal(timers.size, 0);
});

test("camera samples center luminance and releases the preview", async () => {
	const sink = callback();
	await media.startCapture("camera", sink, 600);
	await advance(100);
	assert.ok(Math.abs(sink.samples[0][1] - 1) < 1e-12);
	await media.stopCapture();
	assert.equal(video.srcObject, null);
	assert.equal(tracks[0].stopped, true);
});

test("camera waits for dimensions and reports missing preview", async () => {
	video.videoWidth = 0;
	const sink = callback();
	await media.startCapture("camera", sink, 600);
	await advance(100);
	assert.equal(sink.samples[0][1], 0);
	await media.stopCapture();
	video = null;
	await assert.rejects(
		media.startCapture("camera", callback(), 600),
		/preview/,
	);
	assert.equal(tracks[0].stopped, true);
});

test("permission refusal and unsupported contexts return actionable errors", async () => {
	denied = true;
	await assert.rejects(
		media.startCapture("microphone", callback(), 600),
		/denied/,
	);
	navigator.mediaDevices = null;
	await assert.rejects(
		media.startCapture("camera", callback(), 600),
		/HTTPS/,
	);
});

test("device disconnection stops capture and notifies the shared UI", async () => {
	const sink = callback();
	await media.startCapture("microphone", sink, 600);
	await events.ended();
	assert.equal(tracks[0].stopped, true);
	assert.match(sink.failures[0], /disconnected/);
});

test("backgrounding stops playback and capture and updates the receiver state", async () => {
	const sink = callback();
	await media.startCapture("microphone", sink, 600);
	document.hidden = true;
	await documentEvents.visibilitychange();
	assert.match(sink.failures[0], /hidden/);
	assert.equal(tracks[0].stopped, true);
	document.hidden = false;
	await documentEvents.visibilitychange();
});

test("a pending permission request cannot leak a stream after Stop", async () => {
	let resolve;
	streamGate = new Promise((r) => (resolve = r));
	const starting = media.startCapture("camera", callback(), 600);
	await settle();
	await media.stopCapture();
	resolve({ getTracks: () => tracks });
	await starting;
	assert.equal(tracks[0].stopped, true);
	assert.equal(timers.size, 0);
});

test("failed sample delivery shuts down the device", async () => {
	const sink = callback();
	const receive = sink.invokeMethodAsync;
	sink.invokeMethodAsync = async function (method, data) {
		if (method === "ReceiveSamples") throw new Error("disconnected");
		return receive.call(this, method, data);
	};
	await media.startCapture("microphone", sink, 600);
	await advance(100);
	assert.equal(tracks[0].stopped, true);
	assert.match(sink.failures[0], /stopped/);
});
