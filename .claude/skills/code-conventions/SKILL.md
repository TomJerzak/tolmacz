---
name: code-conventions
description: Clean code and design rules for Tolmacz. Use when writing, changing, reviewing or refactoring any C# or XAML code in this repository.
---

# Code conventions

## General principles
- **Single responsibility.** A class or method does one thing. Split it when a name needs "and".
- **Small, readable methods.** Prefer clear names and early returns over comments that explain
  tangled code.
- **No duplication.** Reuse an existing helper before writing a new one. Search first.
- **Simple over clever.** Add an abstraction (interface, base class, pattern) only when there is
  a second implementation or a test needs to replace it. No layers "for later".
- **Depend on abstractions at boundaries.** UI and services meet through interfaces
  (`ITranslator`, `ITextClipboard`, `ITextSizeStore`), so logic can be tested without MAUI.
- **Open for extension where it is real.** For example, a new cleanup rule is a new method in
  `OutputCleanup`, not a new branch in an unrelated method.
- **Fail clearly.** Throw a specific exception with an actionable message
  (`TranslationModelException`); do not swallow exceptions.
- **Immutability.** Prefer records, `readonly` fields and local values over shared mutable state.
- **Keep changes narrow.** Change only what the task needs, and do not reformat unrelated code.

## Project boundaries
| Project | May depend on | Must not depend on |
| --- | --- | --- |
| `src/Tolmacz` | ONNX Runtime, Microsoft.ML.Tokenizers | MAUI, Presentation |
| `src/Tolmacz.Presentation` | `src/Tolmacz` | MAUI |
| `src/Tolmacz.App` | both | — |

- Screen logic belongs in `MainViewModel`, not in XAML code-behind.
- Platform code goes in `Platforms/` or under `#if WINDOWS` / `#if MACCATALYST`, so a fix for one
  platform cannot change the other.

## Public API
- The library's public API is only `ITranslator`, `MarianOnnxTranslator`,
  `TranslationDirection`, `TranslationResult`, `TranslationModel` and
  `TranslationModelException`. Everything else is `internal`; tests use `InternalsVisibleTo`.
- Making a type public, or changing a public signature, needs the user's approval, because it
  affects the future NuGet package.

## Comments and documentation
- Every public and internal member in `src/Tolmacz` and `src/Tolmacz.Presentation` has an XML
  doc comment. Missing docs are build errors.
- Comments explain **why**, with a real example of the model behaviour being handled, as in
  `// The model repeats itself on short phrases, such as "Warszawa".`
- Do not leave debug output, temporary files or commented-out code.

## Tests
- Every new rule gets unit tests for what it changes **and** for what it must leave unchanged.
- Test names describe behaviour in words: `Removes_the_category_label_only_when_...`.
- Model behaviour is tested in `ModelTests` with the real model; pure text rules are tested
  without the model.
- Changes that can alter translations also follow the `translation-quality` skill.

## Build quality
- 0 warnings. Analyzers run at `latest-recommended`; suppress a rule only in `.editorconfig`,
  for a stated reason and the narrowest scope.
- Use `[GeneratedRegex]` for regular expressions and pass `StringComparison` explicitly.
