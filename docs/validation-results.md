# Initial validation — October 17, 2026

Copyright © 2026 myNOC LLC. All rights reserved.

- 130 MSTest cases passed across four projects: Core (106), UI (19), Web (2), and MAUI (3), using NSubstitute at dependency boundaries. Each test project references its matching source assembly.
- Friendly-error regression tests verify Morse validation guidance and input-setting hints, while keeping runtime resource keys, parameter diagnostics, JavaScript stacks, and native error codes out of alerts.
- All four projects passed together through the root SLNX in Release with warnings treated as errors and the Windows MAUI target. The portable solution also passed independently with coverage collection.
- Web tests verify root-component registration and service lifetimes. MAUI tests verify Windows service registration and permission delegation without launching a UI; Android/iOS device behavior still requires the device checklist.
- 11 Node media lifecycle tests passed.
- 35 Pester 5.7.1 tests passed for the PowerShell provisioning and coverage scripts, including secure stdin transfer, failure-output redaction, preserved GitHub protections, missing reports, the 90% boundary, and real UTF-8 Git diffs with spaces in source paths. The PowerShell coverage gate reproduced 99.41% (504/507) from the existing application coverage reports. Azure preview mode completed against the provisioned environment without changing resources or GitHub settings.
- Combined shared C#/JavaScript line coverage: **99.41% (504/507)** after the friendly-error changes. Razor/CSS/native glue are outside this metric; see the README for the exact scope.
- Complete SLNX Release build passed with the Windows MAUI target and warnings treated as errors.
- After raising the OS baselines, all 120 MSTest cases passed with the `net11.0-windows10.0.26100.0` target. The generated Windows manifest declares minimum build 26100 (Windows 11 24H2).
- Android Release build passed.
- The Android Release APK rebuilt successfully with minimum API 35 (Android 15), confirmed in the generated Android manifest; target API remains 37.
- iOS managed code compiled cleanly on Windows using the installed iOS reference packs. This does not validate native linking, Xcode packaging, simulator execution, or signing.
- The iOS managed build also passed with minimum iOS 18.0, confirmed in the generated assembly platform metadata.
- Standalone SPA Release publish passed. Its static output was served separately and checked in the browser: text/Morse conversion in both directions, audio scheduling at zero volume, Stop, challenge reveal, and answer scoring.
- Workflow YAML and shared JavaScript/CSS formatting passed Prettier validation.

The results above are local. The initial application has been pushed to `work/initial-setup` in [erenken/morseCodeTrainer](https://github.com/erenken/morseCodeTrainer), and `main` protection requires pull requests and passing validation. The initial GitHub iOS job exposed a runner mismatch; the workflow now selects the `xcode-27` runner required for Xcode 27.0. Check [GitHub Actions](https://github.com/erenken/morseCodeTrainer/actions) for current CI results. Real camera/microphone accuracy, full iOS packaging, and signed distribution still require the device and account setup described in the README.
