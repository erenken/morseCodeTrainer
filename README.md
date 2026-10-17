# Morse Code Trainer

Copyright © 2026 myNOC LLC. All rights reserved.

A .NET 11 Morse trainer for Windows, Android, iOS, and the browser. A shared Razor UI runs inside MAUI Blazor Hybrid and a standalone Blazor WebAssembly SPA. The browser app needs only static hosting; it does not require an ASP.NET server or account.

## What is implemented

- **Practice:** random character groups, starter letters, full alphabet, numbers, sound/light replay, answer checking, and session scoring.
- **Translate:** International Morse letters, numbers, and common punctuation, in both directions. Spaces separate letters; `/` separates words. Unsupported text is reported instead of silently dropped.
- **Transmit:** sine-wave tones, screen flashes, or both; adjustable character speed, Farnsworth effective speed, pitch, and volume. Stop works during playback and pending audio startup.
- **Receive:** live microphone tone-band measurements and camera center-brightness measurements feed the same C# streaming decoder. Adjustable detection threshold with hysteresis, pulse debounce, and bounded transcript. Unknown sequences display `�`.
- **Reference:** the full supported alphabet with individual character playback.
- **About:** shared across every app, with information about myNOC LLC, the 2026 copyright, and a link to [mynoc.com](https://mynoc.com/).

Error alerts show plain-language validation and recovery guidance. Exception parameter names, runtime resource keys, native error codes, and stack traces are kept in diagnostic logging rather than displayed in the app.

Everything is processed on the device. No recordings, video, or transcripts are uploaded. Session results are in memory and reset when the app closes. No database is needed at present. If durable database storage is introduced, use Entity Framework Core behind a shared interface (native SQLite or a server API for the SPA); never put database credentials in the browser.

## Solution

Open `MorseCodeTrainer.slnx`. Application source is under `src/`, tests under `tests/`.

| Project | Responsibility |
| --- | --- |
| `MorseCodeTrainer.Core` | UI-independent alphabet, codec, timing, transmission, streaming decoding, tone measurement, and training logic |
| `MorseCodeTrainer.UI` | Shared Razor components, CSS, media-session orchestration, browser audio/video adapters |
| `MorseCodeTrainer.Web` | Standalone WebAssembly SPA host |
| `MorseCodeTrainer.Maui` | Windows/Android/iOS host, device permission declarations, Android web-view permissions, native iOS AVFoundation capture |
| `MorseCodeTrainer.Core.Tests` | Alphabet, codec, timing, transmission, decoding, tone measurement, and training tests |
| `MorseCodeTrainer.UI.Tests` | Media orchestration, interop, permission, and native-capture interface tests |
| `MorseCodeTrainer.Web.Tests` | SPA root-component registration, browser services, and session lifetimes |
| `MorseCodeTrainer.Maui.Tests` | Windows MAUI host composition and native permission delegation |

Every source assembly has its own MSTest project referencing that assembly. Tests use NSubstitute at dependency boundaries. The root solution contains all eight projects; `tests/Portable.Tests.slnx` contains the Core/UI/Web projects and their tests for systems without MAUI workloads. The MAUI test project targets Windows and exercises the actual Windows MAUI assembly.

JavaScript media lifecycle tests use Node's built-in test runner. Python tests exercise the coverage gate. Neither requires third-party packages.

## Prerequisites

- .NET **11 RC2**, SDK `11.0.100-rc.2.26504.105`, pinned in `global.json`. This is a prerelease target, not .NET 11 GA.
- MAUI workload set `11.0.100-rc.2.26509.2` when developing native apps. Visual Studio-managed workloads can also supply the corresponding RC2 packs.
- Windows: Windows SDK and WebView2 runtime; Android: JDK 21 and Android SDK API 37 supplied by the MAUI tooling.
- iOS: a Mac with Xcode **27.0**, macOS 26.6+, and the matching iOS workload. Windows can compile the managed iOS code, but cannot validate a complete native app without a Mac.
- Node 24+ and Python 3.11+ for the media tests and coverage gate.

```powershell
dotnet workload install maui --version 11.0.100-rc.2.26509.2
```

Use only the individual workload (`maui-windows`, `maui-android`, or `maui-ios`) if you develop one native target.

## Supported operating systems

Native apps support the current stable OS release and the two preceding major releases. Windows uses annual H2 feature releases; previews and the hardware-specific 26H1 release do not advance that annual baseline. Reviewed October 17, 2026:

| App | Current stable generation | Minimum OS | Build setting |
| --- | --- | --- | --- |
| Windows | Windows 11 26H2 | **Windows 11 24H2** | `10.0.26100.0` |
| Android | Android 17 | **Android 15** | API level `35` |
| iOS / iPadOS | iOS / iPadOS 27 | **iOS / iPadOS 18** | `18.0` |

The Apple release sequence is **18 → 26 → 27**, so two releases behind 27 is 18, not 25. Android API levels are different from Android marketing versions. The Windows target framework still uses `windows10.0` because Windows 11 reports NT version 10.0; build 26100 identifies the 24H2 baseline.

Release references: [Microsoft Windows release information](https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information), [Apple release history](https://support.apple.com/en-us/100100), [Android 17 release announcement](https://android-developers.googleblog.com/2026/06/Android-17.html), and [Android version/API mapping](https://developer.android.com/tools/releases/platforms).

Review this policy before each release and raise the pinned minimums when a new stable major OS generation ships. Keep the MAUI project, Windows test project, Windows package manifest, CI target framework, and build examples aligned. Minimums are explicit build settings; they do not silently change with the calendar or SDK updates. The standalone SPA depends on a current browser with WebAssembly and media support rather than a native OS installation requirement.

## Run

Web (localhost supports camera/microphone access; deployed sites must use HTTPS):

```powershell
dotnet run --project src/MorseCodeTrainer.Web
```

Open `http://localhost:5042`. For a phone browser, host the published files on HTTPS; a LAN HTTP address is not a secure context.

Windows:

```powershell
dotnet build src/MorseCodeTrainer.Maui -t:Run -f net11.0-windows10.0.26100.0 -p:MauiTargetFramework=net11.0-windows10.0.26100.0
```

Android (start an emulator or connect a development device first):

```powershell
dotnet build src/MorseCodeTrainer.Maui -t:Run -f net11.0-android -p:MauiTargetFramework=net11.0-android
```

iOS on an Apple Silicon Mac:

```sh
dotnet build src/MorseCodeTrainer.Maui -f net11.0-ios -p:MauiTargetFramework=net11.0-ios -p:RuntimeIdentifier=iossimulator-arm64 -p:EnableCodeSigning=false
```

The `MauiTargetFramework` override selects only the native project; it intentionally does not change the framework of the portable libraries. For example, build the complete SLNX on Windows with:

```powershell
dotnet build MorseCodeTrainer.slnx -p:MauiTargetFramework=net11.0-windows10.0.26100.0
```

## Receiving real signals

1. Select **Receive**, then microphone or camera. Match character and effective speed to the sender; set them equal for normal Morse spacing.
2. For audio, select the sender's pitch (600 Hz is the default). Avoid speech, multiple simultaneous transmitters, and loud background noise.
3. For camera, center a single light source. Begin at 5–12 WPM. The web, Android, and Windows versions show a center-region video preview. The native iOS adapter currently uses the rear camera and signal meter without a video preview.
4. Start receiving and grant the requested permission. Set the threshold between idle and signal brightness/strength. Stop before changing settings.
5. Wait for the letter/word gap. The transcript updates without waiting for another tone. Stop preserves the final letter.

The receiver uses a manually selected speed, pitch, and threshold. It does not identify arbitrary speeds automatically, transcribe speech, or read printed Morse symbols. Camera exposure, frame rate, ambient light, input processing, and background throttling affect accuracy. Capture stops when hidden or interrupted. Screen light means the display, not the camera torch. Keep the app foregrounded while transmitting. The flash panel floats above the controls on small screens while transmitting light.

## Tests and coverage

Run from the repository root. Use a fresh results directory per run so old coverage cannot conceal a regression:

```powershell
dotnet test tests/Portable.Tests.slnx -c Release --warnaserror --collect:"XPlat Code Coverage" --settings coverage.runsettings --results-directory artifacts/test-results --logger trx
node --test --experimental-test-coverage --test-coverage-include='src/**/*.js' --test-coverage-lines=90 --test-reporter=spec --test-reporter-destination=stdout --test-reporter=lcov --test-reporter-destination=artifacts/test-results/media.lcov tests/browser/*.test.mjs
python -m unittest discover -s tests/ci
python scripts/check_coverage.py --report 'artifacts/test-results/**/coverage.cobertura.xml' --lcov artifacts/test-results/media.lcov
```

Run the MAUI tests on Windows with the pinned `maui-windows` workload:

```powershell
dotnet test tests/MorseCodeTrainer.Maui.Tests -c Release -p:MauiTargetFramework=net11.0-windows10.0.26100.0 --warnaserror --logger trx --results-directory artifacts/test-results/Maui
```

To run all four C# test projects together on Windows, use `dotnet test MorseCodeTrainer.slnx -c Release -p:MauiTargetFramework=net11.0-windows10.0.26100.0`.

CI has a clean checkout, runs Core/UI/Web tests in the portable test job and MAUI host tests in the Windows native job, builds/publishes the SPA, and builds every native target. The required check depends on all of these jobs. **90% of total and changed shared-code lines must be covered**, with no minimum patch-size exemption. PR comparison uses the merge base with the target branch. Coverage reports missing changed source files cause failure. A documentation-only change still runs tests and enforces total coverage.

Coverage includes the shared C# engine, media orchestration, and JavaScript adapter. Razor markup, CSS, generated code, and native platform glue are excluded from the line-coverage metric. Native builds and the device acceptance checklist complement these tests. Mock media tests do not establish real microphone/camera accuracy.

## GitHub and releases

The root directory is the repository root. GitVersion 6.8.2 and `GitVersion.yml` use WeatherLink's `main` / `work/*` conventions. EditorConfig starts from WeatherLink's tab indentation, constant naming, and private-field conventions, with spaces for JSON/YAML/Python. Dependency versions are centralized in `Directory.Packages.props`.

```powershell
dotnet tool restore
dotnet gitversion
```

GitVersion requires an initialized repository with at least one commit. No remote repository, commits, or releases are created by this scaffold.

- **PR:** test results, total/patch coverage, SPA publishing, and Windows/Android/iOS compilation; a final `validate / Required checks` job fails if any dependency fails or is skipped.
- **Release:** on `main`, GitVersion determines the version; all validation reruns before creating a GitHub prerelease with the SPA and native build archives. Re-running an existing version keeps its assets unchanged.
- Releases are marked prerelease while .NET 11 is RC. Windows is an unpackaged build; Android output is a development APK; iOS output is an unsigned simulator build. Store-ready MSIX/AAB/IPA distribution needs signing identities, provisioning, and store configuration. No signing credentials are embedded.
- The iOS job expects `/Applications/Xcode_27.0.app` on `macos-26`. If runner images change, select the Xcode version required by the pinned workload; do not suppress the SDK compatibility checks.

**Required GitHub setting:** workflow YAML cannot enforce merge restrictions on its own. After creating the repository and running the PR workflow, import `.github/branch-ruleset.json` under **Settings → Rules → Rulesets**, or create an equivalent rule requiring pull requests and the `validate / Required checks` status on `main`. Confirm the exact check name from the first run. The ruleset is prepared locally and is not activated automatically.

To deploy the SPA, publish and serve `artifacts/web/wwwroot` over HTTPS:

```powershell
dotnet publish src/MorseCodeTrainer.Web -c Release -o artifacts/web
```

The SPA uses one page with in-app tabs; no server-side routes or API are required. Change the HTML base path if hosting below a subdirectory.

## Device acceptance

Before publishing to users, run [the device checklist](docs/device-validation.md) on physical devices.

## Design references

- [Microsoft: share Razor components across MAUI and web](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/tutorials/maui-blazor-web-app?view=aspnetcore-10.0)
- [Microsoft: .NET 11 downloads](https://dotnet.microsoft.com/en-us/download/dotnet/11.0)
- [Microsoft: iOS workload and Xcode release requirements](https://github.com/dotnet/macios/releases)
- [Microsoft: WebView permissions](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/webview)
