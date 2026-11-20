# Tolmacz

Offline translation between Polish and English for .NET 10 applications on macOS and Windows.
Translation runs on the computer's CPU with ONNX Runtime: no internet connection or cloud service
is needed, and the text never leaves the computer.

- Polish to English and English to Polish, with one model for both directions.
- Keeps the layout: paragraphs, lines, lists and tab-separated tables.
- Keeps numbers, addresses, e-mail addresses, web links and words the model cannot translate.
- Reports when a translation may be incomplete.

## Install

```bash
dotnet add package Tolmacz
```

## Download the model

The package does not include or download the model, which is about 481 MB. Download release
[`bidi-eng-pol-v1`](https://github.com/TomJerzak/tolmacz-model/releases/tag/bidi-eng-pol-v1)
from the [Tolmacz model](https://github.com/TomJerzak/tolmacz-model) repository and extract all of
its files into one folder.

The model is a converted int8 ONNX version of
[Allegro BiDi-eng-pol](https://huggingface.co/allegro/BiDi-eng-pol), licensed under
[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). If you distribute the model with your
application, include its attribution, as in Tolmacz's
[NOTICE.md](https://github.com/TomJerzak/tolmacz/blob/main/NOTICE.md).

## Usage

```csharp
using Tolmacz;

const string modelDirectory = "models/bidi-eng-pol";

await TranslationModel.VerifyChecksumsAsync(modelDirectory);

using var translator = new MarianOnnxTranslator(modelDirectory);
var result = await translator.TranslateAsync(
    "Dzień dobry.",
    TranslationDirection.PolishToEnglish);

Console.WriteLine(result.Text); // Good morning.
```

- `result.IsComplete` is false when part of the translation may be missing.
- If the model folder is missing, incomplete or for another model version, the translator throws
  `TranslationModelException` and explains what is wrong.
- `VerifyChecksumsAsync` detects damaged model files. Call it after downloading the model.
- Loading the model takes time, so create one translator and reuse it.

## Supported systems

| System | Status |
| --- | --- |
| macOS on Apple Silicon | Tested |
| Windows 11 x64 | Tested |
| macOS on Intel, Windows 10 | Not tested |
| Linux | Not tested |

In a .NET MAUI app for Mac Catalyst, set `<UseInterpreter>false</UseInterpreter>` for the
Mac Catalyst build. The Mono interpreter crashes while translating longer sentences.

## More information

Translation quality results, known limitations and the Tolmacz desktop app are described in the
[Tolmacz repository](https://github.com/TomJerzak/tolmacz).

The Tolmacz library is licensed under the MIT License.
