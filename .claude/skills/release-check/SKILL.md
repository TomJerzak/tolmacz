---
name: release-check
description: Checklist before publishing Tolmacz (GitHub repository, installers, model release or NuGet package). Use when preparing a release, updating README results, or when asked whether the project is ready to publish.
---

# Release checklist

Never publish, push, upload or create a release without the user's explicit approval for that
step. The user checks the code manually before the main repository is published.

## 1. Code
- [ ] All three test projects pass (see `build-and-test`).
- [ ] The Mac app builds with 0 warnings.
- [ ] The latest Windows test passed: tests, app checks (Ctrl+Enter, text size, Copy, Clear,
      Swap, a tab-separated table), install and uninstall. Windows runs on another PC, so ask
      the user for the result and the Windows agent's report.
- [ ] The full evaluation has 0 detected problems on macOS and Windows, and every changed
      translation was reviewed (see `translation-quality`).

## 2. Files
- [ ] No `bin`, `obj`, `dist`, `.evaluation`, `.venv-comet`, `.DS_Store`, `*.Backup.tmp` or
      `.onnx` files are published. `.gitignore` covers them.
- [ ] No personal paths, email addresses, secrets or old `Translator` names.
- [ ] `LICENSE` (MIT) and `NOTICE.md` (model and third-party credits) are present.

## 3. README
- [ ] Tested platforms table matches what was actually tested.
- [ ] Quality numbers (evaluation, short sets, COMET for both platforms) come from the latest
      runs, with dates.
- [ ] Known limitations list the errors that remain.
- [ ] Show the user the proposed README changes before applying them.

## 4. After the main repository is published
- [ ] Update the README of https://github.com/TomJerzak/tolmacz-model so "Tolmacz .NET library"
      links to the main repository, and to the NuGet package once one exists. Ask first.
- [ ] Check that the C# sample in that README still runs against the current library.

## 5. Installers
- [ ] Rebuild `dist/Tolmacz-macOS.dmg` and `dist\Tolmacz-Setup.exe` from the final code.
- [ ] Without `CODESIGN_KEY`, the DMG is only ad-hoc signed and macOS warns other users.
- [ ] Publishing happens from macOS, so the Windows installer is built on Windows from the frozen
      final code and brought back to the Mac with the Windows report. Do not change code between
      creating the Windows package and publishing.
- [ ] Upload both installers as assets of the main repository's GitHub Release, and link them
      from the README. Explain the unsigned-app warnings: macOS Gatekeeper for the DMG and
      Windows SmartScreen for the setup program.

## 6. NuGet (only when the user decides to publish a package)
- [ ] Only `src/Tolmacz` is packaged. The public API is `ITranslator`, `MarianOnnxTranslator`,
      `TranslationDirection`, `TranslationResult`, `TranslationModel` and
      `TranslationModelException`; everything else stays internal.
- [ ] The package does not include the model. The README explains where to download it.
