# Device acceptance checklist

Copyright © 2026 myNOC LLC. All rights reserved.

Automated tests cover code behavior, synthetic pulse timing, and mocked device lifecycle. These checks require real devices and should be recorded for each release.

Validate on both the minimum supported and current stable OS generations: Windows 11 24H2 and 26H2, Android 15 and 17, and iOS/iPadOS 18 and 27 (baseline reviewed October 17, 2026). Review the README support table for each release. Inspect generated package metadata to confirm minimum Windows build 26100, Android API 35, and iOS 18.0; build success alone does not prove device compatibility.

| Target | Build validation performed during initial setup | Device validation |
| --- | --- | --- |
| Browser SPA | Published release inspected: encode/decode, audio scheduling at zero volume, Stop, challenge reveal, answer scoring | Real camera/microphone not tested |
| Windows | Release native build | Real camera/microphone not tested |
| Android | Release APK build | Emulator/physical-device interaction not tested |
| iOS | Managed code compiled on Windows against iOS RC2 packs | Full Mac/Xcode build, signing, and physical-device validation required |

1. **Text and timing:** encode/decode `SOS CQ TEST 123, OK?`; verify a dash is three dots and normal letter/word gaps are three/seven dots. Check 5, 12, 20 WPM and Farnsworth spacing.
2. **Playback:** sound, light, both, zero volume, stop mid-dot, stop during audio startup, long message, app backgrounding. On a phone the flash panel must remain visible. Use a separate receiving device to check output timing.
3. **Microphone:** decode an external 600 Hz transmitter, change pitch, vary volume/noise, deny permission, grant after denial, unplug the device, and interrupt with a phone call. Verify devices are released after Stop and backgrounding.
4. **Camera:** point at a single flashing source; adjust the threshold above idle and below peak. Test 5–12 WPM in different light levels. On iOS, align using the level meter; no video preview is implemented yet.
5. **Training:** generate groups from each set, replay, reveal, submit partial/extra/wrong answers, press Enter, generate a new challenge, and verify scores reset when the app restarts.
6. **Platform UX:** portrait/landscape, narrow browser, keyboard focus, screen reader labels, large text, safe areas, and microphone/camera privacy indicators. Change system/browser appearance while open, backgrounded, and after restart; old saved overrides must not take effect, and no theme button should appear. Verify readable controls, alerts, and signal previews in both modes. On iOS, check Dynamic Island/notch clearance in both orientations, the home indicator, keyboard avoidance, and native tab appearance (Liquid Glass where supported, including Reduce Transparency). On Android, check native Material tabs, cutouts, and gesture/three-button navigation. Switch tabs during playback, capture, and a pending permission prompt: media must stop, the tab's text/answers must survive, and signal settings must carry across tabs.
7. **Foldable/multitasking:** fold/unfold, resize into split-screen, rotate, and open the keyboard without losing entered text, transcript, answers, or settings. Check compact and expanded widths with large text. Where the browser/WebView reports viewport segments, keep all controls and floating signal/error panels outside the physical hinge in book and tabletop orientations. In Xcode Device Hub/iPhone Duo Simulator and on hardware, verify system-managed vertical bars on the outer display and inner landscape, horizontal bars where the OS chooses them, and Split View with controls on either outer edge. On iOS 27.1+, activate/deactivate the inner camera and fold region: the same WebView must resize into the largest unobstructed region and restore the expanded layout without losing state. Verify Reduce Transparency and large text. Native bar placement and reserved-region notifications still need actual simulator/hardware verification; desktop previews and geometry unit tests do not establish device compatibility. Reference: [Apple iPhone Duo guidance](https://developer.apple.com/design/human-interface-guidelines/designing-for-iphone-duo).
8. **Distribution:** install the signed candidate on clean devices; test the deployed SPA over HTTPS and under the intended base path. Validate permissions again on the release build because trimming and signing can differ from development builds.

Record device model, OS/browser versions, sender speed/pitch, environment, observed accuracy, and release commit. Do not infer hardware accuracy from test coverage percentages.
