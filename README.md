# Tolmacz

Offline translation between Polish and English, in two parts:

| Part | For | What it is |
| --- | --- | --- |
| **Tolmacz library** | Developers | A .NET 10 library that you can use in your own applications on macOS and Windows |
| **Tolmacz app** | Everyone | A desktop app for macOS and Windows, built on the library |

Both use the same [Tolmacz model](https://github.com/TomJerzak/tolmacz-model), which is published
in a separate repository. Translation runs on your computer: no internet, cloud service, Docker or
Python is needed, and your text never leaves the computer.

## What Tolmacz translates

- Polish to English and English to Polish, with one model for both directions.
- Keeps the layout: paragraphs, lines, lists and tab-separated tables.
- Keeps numbers, addresses, e-mail addresses, web links and words the model cannot translate.
- Reports when a translation may be incomplete.

These features come from the library, so your own applications get them too.

## Tolmacz app

### Download

Download the installer for your system from the [latest release](../../releases/latest). The
model is included.

| System | File | Requirements |
| --- | --- | --- |
| macOS | `Tolmacz-macOS.dmg` | macOS 14 or later, Apple Silicon or Intel |
| Windows | `Tolmacz-Setup.exe` | 64-bit Windows 10 version 1809 or later |

The installers are not signed with a paid certificate, so the system warns you the first time:

- **macOS:** open the DMG, drag Tolmacz to Applications, then right-click Tolmacz and choose
  **Open**. You can also allow it in System Settings → Privacy & Security.
- **Windows:** if SmartScreen says it protected your PC, choose **More info** → **Run anyway**.

### Using the app

- Translate with the button, or Cmd+Enter on macOS and Ctrl+Enter on Windows. A long
  translation can be cancelled.
- Swap the languages, copy the translation, clear both boxes, and change the text size.

## Tolmacz library

### Model

The library does not include or download the model. Download release
[`bidi-eng-pol-v1`](https://github.com/TomJerzak/tolmacz-model/releases/tag/bidi-eng-pol-v1)
from the [Tolmacz model](https://github.com/TomJerzak/tolmacz-model) repository and extract all of
its files into one folder. The model is a converted int8 ONNX version of
[Allegro BiDi-eng-pol](https://huggingface.co/allegro/BiDi-eng-pol) (CC BY 4.0), about 481 MB,
and runs on the CPU through ONNX Runtime. See [NOTICE.md](NOTICE.md) for attribution, which must
stay with any copy you distribute.

### Usage

Add the [Tolmacz NuGet package](https://www.nuget.org/packages/Tolmacz) to your project:

```bash
dotnet add package Tolmacz
```

Then pass the model folder to the translator:

```csharp
using Tolmacz;

using var translator = new MarianOnnxTranslator("/path/to/bidi-eng-pol");
var result = await translator.TranslateAsync("Dzień dobry.", TranslationDirection.PolishToEnglish);
Console.WriteLine(result.Text);
```

`result.IsComplete` is false when part of the translation may be missing. If the folder is missing,
incomplete or for another model version, the translator throws `TranslationModelException` and
explains what is wrong. After downloading a model, call `TranslationModel.VerifyChecksumsAsync` to
detect damaged files.

The library targets .NET 10 and has been tested on macOS (Apple Silicon) and Windows 11 (x64).
In a .NET MAUI app for Mac Catalyst, set `<UseInterpreter>false</UseInterpreter>` for the
Mac Catalyst build, because the Mono interpreter crashes while translating longer sentences.

## Tested platforms

| Platform | Library | App |
| --- | --- | --- |
| macOS on Apple Silicon | Tested | Built, tested and run |
| Windows 11 x64 | Tested | Built, tested, installed and uninstalled |
| macOS on Intel | Not tested | Included in the Mac build, not tested |
| Windows 10 version 1809 or later | Not tested | Supported by the build settings, not tested |

## Translation quality

Measured in November 2026 on the final code.

**FLORES-101** (2,009 sentence pairs, both directions), scored with
[COMET](https://github.com/Unbabel/COMET) `wmt22-comet-da`. Higher is better; scores in the
mid-80s mean good, fluent translations with occasional errors.

| Direction | Part | macOS | Windows |
| --- | --- | ---: | ---: |
| English → Polish | dev | 86.01 | 86.00 |
| English → Polish | devtest | 86.07 | 86.17 |
| Polish → English | dev | 85.00 | 85.06 |
| Polish → English | devtest | 84.79 | 84.74 |

Results differ slightly between macOS and Windows because the 8-bit model can pick a different
word when two words score almost the same on Apple and x64 processors. Neither platform is better.

**Difficult cases.** The evaluation tool also runs 666 generated cases: numbers, dates,
addresses, links, names, abbreviations, lists, tables, layout and mixed text. Together with
FLORES that is 4,684 translations, and the automatic checks found no lost or changed numbers,
broken layout, added repetition or other detected problems on either platform.

**Single words and short phrases** are the hardest, because the model was trained on whole
sentences. On word lists written in advance and checked by hand:

| Direction | Correct |
| --- | ---: |
| Polish → English | 30 of 40 |
| English → Polish | 23 of 40 |

**Speed.** Average time per translation in the evaluation, mostly single sentences, on the CPU:

| System | Processor | Average |
| --- | --- | ---: |
| macOS | Apple M1, 8 cores, 16 GB RAM | 0.31 s |
| Windows 11 | AMD Ryzen 7 7735HS, 8 cores, 16 GB RAM | 0.38 s |

Speed depends on the processor, so newer or faster ones, such as Apple M4, will be faster.

## Known limitations

- Single English words can come out in the wrong Polish form, such as "Bus" → "Autobusem",
  and one-word commands can become an infinitive, such as "Send" → "Wysłać". On Windows this
  also affects "Close" → "Zamknąć się" and "Cancel" → "Odwołać".
- Single words with several meanings get one of them without context, such as "Pomniejsz" →
  "Less" instead of "Zoom out".
- The model sometimes gets rare words wrong, as in "water/marsh areas" → "obszarami
  wodnymi/marszowymi", or drops a detail, as in "1:15 a.m." losing "w nocy" (at night).
- The original model authors warn that English to Polish output may show gender bias.
- Review important translations.

## Project structure

| Project | Purpose |
| --- | --- |
| `src/Tolmacz` | Independent translation library |
| `src/Tolmacz.Presentation` | Screen state and commands, with no MAUI dependency |
| `src/Tolmacz.App` | .NET MAUI desktop application |
| `tools/Tolmacz.Evaluation` | Translation quality evaluation tool |
| `tools/Tolmacz.IconMaker` | Makes the Windows icon file, with every standard size, from its SVG |
| `tests/Tolmacz.Tests` | Translation library tests |
| `tests/Tolmacz.Presentation.Tests` | Screen logic tests |
| `tests/Tolmacz.Evaluation.Tests` | Evaluation tool tests |

## Requirements for building

- .NET 10 SDK
- macOS: Xcode and `dotnet workload install maui-maccatalyst`
- Windows: `dotnet workload install maui-windows` and [Inno Setup 7](https://jrsoftware.org/isdl.php)

Before building the app from a fresh clone, download
[`bidi-eng-pol-v1`](https://github.com/TomJerzak/tolmacz-model/releases/tag/bidi-eng-pol-v1)
and extract all of its files into `models/bidi-eng-pol`. To recreate the model instead, follow the
instructions at the top of `scripts/prepare_model.py`.

## Run and test

```bash
dotnet test tests/Tolmacz.Tests
dotnet test tests/Tolmacz.Presentation.Tests
dotnet test tests/Tolmacz.Evaluation.Tests
dotnet build src/Tolmacz.App -t:Run -f net10.0-maccatalyst -p:RuntimeIdentifier=maccatalyst-arm64
```

On Windows, use `-f net10.0-windows10.0.19041.0` and omit the runtime identifier.

## Check translation quality

The evaluation tool runs the generated difficult cases and optional FLORES sentences, then
records problems such as lost numbers, changed addresses, damaged layout and added repetition.

```bash
python3 scripts/prepare_evaluation_data.py --output .evaluation/data/flores.jsonl
dotnet run --project tools/Tolmacz.Evaluation -c Release -- run --data .evaluation/data/flores.jsonl --output .evaluation/runs/baseline
dotnet run --project tools/Tolmacz.Evaluation -c Release -- compare --before .evaluation/runs/baseline --after .evaluation/runs/new --output .evaluation/compare
```

The `compare` command exits with an error when a change adds problems or removes cases. Review
`changed.jsonl` as well, because automatic checks cannot judge every translation.

To add COMET scores, which need a separate Python environment, follow the instructions at the
top of `scripts/score_comet.py`.

## Create the installers

| Platform | Command | Result |
| --- | --- | --- |
| macOS | `scripts/package-macos.sh` | `dist/Tolmacz-macOS.dmg` |
| Windows | `scripts\package-windows.ps1` | `dist\Tolmacz-Setup.exe` |

Each installer must be built on its own platform.

The Windows app and installer use `src/Tolmacz.App/Resources/AppIcon/Windows/appicon.ico`, made
from the SVG next to it. After changing that SVG, make the icon again:

```bash
dotnet run --project tools/Tolmacz.IconMaker -- src/Tolmacz.App/Resources/AppIcon/Windows/appicon.svg src/Tolmacz.App/Resources/AppIcon/Windows/appicon.ico
```

To create the library's NuGet package and its symbols package in `dist/nuget`:

```bash
dotnet pack src/Tolmacz -c Release -o dist/nuget -p:ContinuousIntegrationBuild=true
```

macOS blocks apps that are not signed with an Apple Developer ID. To give the app to other
people without a warning, set `CODESIGN_KEY` before packaging and notarize the DMG.

## License

The Tolmacz source code is licensed under the [MIT License](LICENSE). The translation model and
third-party components have their own licenses, listed in [NOTICE.md](NOTICE.md).
