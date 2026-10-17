# Initial validation — October 17, 2026

Copyright © 2026 myNOC LLC. All rights reserved.

- 130 MSTest cases passed across four projects: Core (106), UI (19), Web (2), and MAUI (3), using NSubstitute at dependency boundaries. Each test project references its matching source assembly.
- Friendly-error regression tests verify Morse validation guidance and input-setting hints, while keeping runtime resource keys, parameter diagnostics, JavaScript stacks, and native error codes out of alerts.
- All four projects passed together through the root SLNX in Release with warnings treated as errors and the Windows MAUI target. The portable solution also passed independently with coverage collection.
- Web tests verify root-component registration and service lifetimes. MAUI tests verify Windows service registration and permission delegation without launching a UI; Android/iOS device behavior still requires the device checklist.
- 11 Node media lifecycle tests passed.
- 7 Python coverage-gate tests passed, including failures for uncovered changes and missing reports.
- Combined shared C#/JavaScript line coverage: **99.41% (504/507)** after the friendly-error changes. Razor/CSS/native glue are outside this metric; see the README for the exact scope.
- Complete SLNX Release build passed with the Windows MAUI target and warnings treated as errors.
- After raising the OS baselines, all 120 MSTest cases passed with the `net11.0-windows10.0.26100.0` target. The generated Windows manifest declares minimum build 26100 (Windows 11 24H2).
- Android Release build passed.
- The Android Release APK rebuilt successfully with minimum API 35 (Android 15), confirmed in the generated Android manifest; target API remains 37.
- iOS managed code compiled cleanly on Windows using the installed iOS reference packs. This does not validate native linking, Xcode packaging, simulator execution, or signing.
- The iOS managed build also passed with minimum iOS 18.0, confirmed in the generated assembly platform metadata.
- Standalone SPA Release publish passed. Its static output was served separately and checked in the browser: text/Morse conversion in both directions, audio scheduling at zero volume, Stop, challenge reveal, and answer scoring.
- Workflow YAML and shared JavaScript/CSS formatting passed Prettier validation.

The test results are local. GitHub Actions have not run because the repository has no remote yet. Branch protection is prepared but has not been activated. Real camera/microphone accuracy, full iOS packaging, and signed distribution still require the device and account setup described in the README.
