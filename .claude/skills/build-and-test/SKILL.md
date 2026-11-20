---
name: build-and-test
description: Build, test, run and package Tolmacz on macOS or Windows. Use when building the app, running tests, starting the app, or creating the DMG, Windows installer or NuGet package.
---

# Build and test Tolmacz

## Before building
- The model must be in `models/bidi-eng-pol`. On a fresh clone, download release
  `bidi-eng-pol-v1` from https://github.com/TomJerzak/tolmacz-model and extract all files there.
- Close a running Tolmacz before building the app, because the app runs from `bin`.

## Tests
Run all three projects. All tests must pass before a change is finished.

```bash
dotnet test tests/Tolmacz.Tests
dotnet test tests/Tolmacz.Presentation.Tests
dotnet test tests/Tolmacz.Evaluation.Tests
```

`Tolmacz.Tests` loads the real model and takes about a minute.

## Build the app
macOS (Apple Silicon):
```bash
dotnet build src/Tolmacz.App -f net10.0-maccatalyst -p:RuntimeIdentifier=maccatalyst-arm64
```
Windows:
```powershell
dotnet build src/Tolmacz.App -f net10.0-windows10.0.19041.0
```
Expect 0 warnings. Analyzers run at `latest-recommended`, and missing XML docs in
`src/Tolmacz` and `src/Tolmacz.Presentation` are errors.

## Run the app
Only when the user asks. Do not run UI or screenshot tests as routine checks.
- macOS: `open src/Tolmacz.App/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/Tolmacz.app`
- Windows: add `-t:Run` to the Windows build command.

## Installers
Each installer must be built on its own platform.

| Platform | Command | Result |
| --- | --- | --- |
| macOS | `scripts/package-macos.sh` | `dist/Tolmacz-macOS.dmg` |
| Windows | `scripts\package-windows.ps1` | `dist\Tolmacz-Setup.exe` (needs Inno Setup 7) |
| Any | `dotnet pack src/Tolmacz -c Release -o dist/nuget -p:ContinuousIntegrationBuild=true` | `dist/nuget/Tolmacz.<version>.nupkg` and `.snupkg` |

## Settings that must stay
- `UseInterpreter=false` on Mac Catalyst avoids a Mono interpreter crash.
- `ValidateXcodeVersion=false` allows newer Xcode versions.
- ONNX Runtime on Mac Catalyst uses the xcframework `NativeReference` with
  `ReferenceNativeSymbol RegisterCustomOps Ignore`.
- Windows sets `session.x64quantprecision=1` for int8 accuracy on x64.
- Platform-specific code goes under `#if WINDOWS` / `#if MACCATALYST` or `Platforms/`, so a fix
  for one platform does not change the other.
