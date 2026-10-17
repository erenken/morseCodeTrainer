# Repository conventions

- Application source belongs in `src/`; tests belong in `tests/`. Keep `MorseCodeTrainer.slnx` current.
- Target .NET 11 using the pinned SDK/workload versions. Keep platform dependencies out of `MorseCodeTrainer.Core`.
- Native minimum OS versions track the current stable release and the two preceding major releases; use annual H2 releases for Windows and actual release generations for Apple's numbering jumps. Review the README support table before a release and keep app/test targets, manifests, CI, and commands aligned.
- Share business logic in Core, UI in the Razor class library, and device integration through interfaces.
- Alerts must use deliberate validation guidance or an operation-specific friendly fallback. Log exception diagnostics; never display raw exception messages, parameter names, stack traces, or runtime resource keys in the UI.
- Use MSTest and NSubstitute for C# tests. Test meaningful behavior, including timing boundaries and cancellation.
- Prefer PowerShell 7.4+ (`pwsh`) over Python for repository automation, setup, build, deployment, and CI helper scripts. Use Pester for PowerShell tests, and keep workflow commands and documentation aligned with script changes.
- Every source assembly must have its own `tests/<AssemblyName>.Tests` project with a project reference to that assembly. Keep all test projects in the root SLNX and run them in CI; portable tests run in `tests/Portable.Tests.slnx`, and MAUI host tests run on Windows.
- If database access becomes necessary, use Entity Framework Core; never connect the SPA directly to a remote database.
- Maintain copyright © 2026 myNOC LLC and follow `.editorconfig` and `GitVersion.yml`.
- Run relevant tests and coverage gates. Do not disable a failing platform or lower the 90% shared-code coverage thresholds to make CI pass.
- Mark device-dependent behavior as unverified until tested on actual devices. Keep signing keys and recordings out of the repository.
