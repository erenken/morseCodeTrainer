// Copyright © 2026 myNOC LLC. All rights reserved.
let audio;
let playback;
let capture;
let playbackGeneration = 0;

async function audioContext() {
	audio ??= new AudioContext();
	await audio.resume();
	return audio;
}

export function stopPlayback() {
	playbackGeneration++;
	if (!playback) return;
	const current = playback;
	playback = null;
	cancelAnimationFrame(current.frame);
	clearTimeout(current.timeout);
	for (const node of current.nodes) {
		try {
			node.stop();
		} catch {
			/* An oscillator may have already ended. */
		}
		node.disconnect();
	}
	current.gain?.disconnect();
	document
		.getElementById("signal-lamp")
		?.classList.remove("lit", "transmitting");
	current.resolve();
}

export async function play(segments, sound, light, frequency, volume) {
	stopPlayback();
	const generation = playbackGeneration;
	const context = sound ? await audioContext() : null;
	if (generation !== playbackGeneration) return;
	const start = context
		? context.currentTime + 0.08
		: performance.now() / 1000 + 0.08;
	const clock = () =>
		context ? context.currentTime : performance.now() / 1000;
	const state = {
		nodes: [],
		frame: 0,
		timeout: 0,
		resolve: () => {},
		gain: null,
	};
	playback = state;
	if (light)
		document.getElementById("signal-lamp")?.classList.add("transmitting");
	let offset = 0;
	const timeline = segments.map((segment) => {
		const item = {
			on: segment.isOn,
			start: offset,
			end: offset + segment.durationMilliseconds / 1000,
		};
		offset = item.end;
		return item;
	});
	if (sound) {
		const gain = context.createGain();
		state.gain = gain;
		gain.gain.value = 0;
		gain.connect(context.destination);
		const oscillator = context.createOscillator();
		oscillator.frequency.value = frequency;
		oscillator.connect(gain);
		state.nodes.push(oscillator);
		// Use the audio clock for tones; animation/timer jitter cannot stretch a dot.
		for (const segment of timeline.filter((s) => s.on)) {
			const on = start + segment.start;
			const off = start + segment.end;
			gain.gain.setValueAtTime(0, on);
			gain.gain.linearRampToValueAtTime(volume, on + 0.004);
			gain.gain.setValueAtTime(volume, Math.max(on + 0.004, off - 0.004));
			gain.gain.linearRampToValueAtTime(0, off);
		}
		oscillator.start(start);
		oscillator.stop(start + offset + 0.01);
	}
	await new Promise((resolve) => {
		state.resolve = resolve;
		const lamp = document.getElementById("signal-lamp");
		let index = 0;
		const animate = () => {
			if (playback !== state) return;
			const elapsed = clock() - start;
			while (index < timeline.length && elapsed >= timeline[index].end)
				index++;
			lamp?.classList.toggle(
				"lit",
				Boolean(light && elapsed >= 0 && timeline[index]?.on),
			);
			state.frame = requestAnimationFrame(animate);
		};
		state.frame = requestAnimationFrame(animate);
		state.timeout = setTimeout(stopPlayback, (offset + 0.12) * 1000);
	});
}

export async function stopCapture() {
	const current = capture;
	capture = null;
	if (!current) return;
	clearTimeout(current.timer);
	current.stream?.getTracks().forEach((track) => track.stop());
	current.source?.disconnect();
	current.analyser?.disconnect();
	if (current.video) {
		current.video.pause();
		current.video.srcObject = null;
	}
	// Wait for the last callback so a restarted session cannot receive old samples.
	await current.pending?.catch(() => {});
}

export async function startCapture(kind, callback, frequency) {
	await stopCapture();
	if (!navigator.mediaDevices?.getUserMedia)
		throw new Error(
			"Capture requires HTTPS (or localhost) and a supported browser/web view.",
		);
	const state = {
		stream: null,
		source: null,
		analyser: null,
		video: null,
		timer: 0,
		pending: null,
		callback,
	};
	capture = state;
	try {
		const isCamera = kind === "camera";
		state.stream = await navigator.mediaDevices.getUserMedia(
			isCamera
				? {
						video: {
							facingMode: "environment",
							frameRate: { ideal: 60 },
						},
						audio: false,
					}
				: {
						audio: {
							echoCancellation: false,
							noiseSuppression: false,
							autoGainControl: false,
						},
						video: false,
					},
		);
		if (capture !== state) {
			state.stream.getTracks().forEach((track) => track.stop());
			return;
		}
		let readLevel;
		if (isCamera) {
			const video = document.getElementById("camera-preview");
			if (!video) throw new Error("The camera preview is unavailable.");
			state.video = video;
			video.srcObject = state.stream;
			await video.play();
			const canvas = document.createElement("canvas");
			canvas.width = canvas.height = 24;
			const pixels = canvas.getContext("2d", {
				willReadFrequently: true,
			});
			readLevel = () => {
				const size =
					Math.min(video.videoWidth, video.videoHeight) * 0.2;
				if (!size) return 0;
				pixels.drawImage(
					video,
					(video.videoWidth - size) / 2,
					(video.videoHeight - size) / 2,
					size,
					size,
					0,
					0,
					24,
					24,
				);
				const data = pixels.getImageData(0, 0, 24, 24).data;
				let total = 0;
				for (let i = 0; i < data.length; i += 4)
					total +=
						0.2126 * data[i] +
						0.7152 * data[i + 1] +
						0.0722 * data[i + 2];
				return total / (24 * 24 * 255);
			};
		} else {
			const context = await audioContext();
			state.source = context.createMediaStreamSource(state.stream);
			const analyser = context.createAnalyser();
			state.analyser = analyser;
			analyser.fftSize = 1024;
			analyser.smoothingTimeConstant = 0;
			state.source.connect(analyser);
			const bins = new Float32Array(analyser.frequencyBinCount);
			const center = Math.round(
				(frequency * analyser.fftSize) / context.sampleRate,
			);
			readLevel = () => {
				analyser.getFloatFrequencyData(bins);
				let peak = -100;
				for (
					let i = Math.max(0, center - 1);
					i <= Math.min(bins.length - 1, center + 1);
					i++
				)
					peak = Math.max(peak, bins[i]);
				return Math.min(1, Math.max(0, (peak + 90) / 70));
			};
		}
		const epoch = performance.now();
		let batch = [];
		let lastSent = 0;
		const sample = async () => {
			if (capture !== state) return;
			try {
				const now = performance.now() - epoch;
				batch.push(now, readLevel());
				if (now - lastSent >= 60) {
					const send = batch;
					batch = [];
					lastSent = now;
					state.pending = callback.invokeMethodAsync(
						"ReceiveSamples",
						send,
					);
					await state.pending;
				}
				if (capture === state)
					state.timer = setTimeout(sample, isCamera ? 16 : 8);
			} catch {
				if (capture === state) {
					await stopCapture();
					await callback.invokeMethodAsync(
						"CaptureFailed",
						"Capture stopped. Check your device and restart the receiver.",
					);
				}
			}
		};
		for (const track of state.stream.getTracks())
			track.addEventListener("ended", async () => {
				if (capture !== state) return;
				await stopCapture();
				await callback.invokeMethodAsync(
					"CaptureFailed",
					"The input device was disconnected or its permission was revoked.",
				);
			});
		await sample();
	} catch (error) {
		await stopCapture();
		throw error;
	}
}

export async function dispose() {
	stopPlayback();
	await stopCapture();
	if (audio) {
		await audio.close();
		audio = null;
	}
}

// Browsers throttle background tabs, which makes reliable flash/pulse timing impossible.
if (typeof document !== "undefined")
	document.addEventListener("visibilitychange", async () => {
		if (document.hidden) {
			stopPlayback();
			const callback = capture?.callback;
			await stopCapture();
			if (callback)
				await callback.invokeMethodAsync(
					"CaptureFailed",
					"Capture stopped while the app was hidden. Start receiving to continue.",
				);
		}
	});
