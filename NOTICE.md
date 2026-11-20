# Third-party notices

## Translation model

This application includes a modified version of **BiDi-eng-pol** by Allegro
(<https://huggingface.co/allegro/BiDi-eng-pol>), licensed under
[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).

Creators: Allegro Machine Learning Research (Artur Kot, Mikołaj Koszowski, Wojciech Chojnowski,
Mieszko Rutkowski) and Laniqo.com (Artur Nowakowski, Kamil Guttmann, Mikołaj Pokrywka).

Changes: the model was converted to ONNX, its weights were quantized to 8-bit integers, unused
tokenizer and configuration files were removed, and `model.json` was added.

The original creators do not endorse this application. The model is provided "as is", without
warranties of any kind, as described in the license.

## Libraries

These libraries are used under the MIT License below:

- ONNX Runtime, Copyright (c) Microsoft Corporation, <https://github.com/microsoft/onnxruntime>
- Microsoft.ML.Tokenizers, Copyright (c) .NET Foundation and Contributors,
  <https://github.com/dotnet/machinelearning>
- .NET MAUI, Copyright (c) .NET Foundation and Contributors, <https://github.com/dotnet/maui>

The notices for components included in ONNX Runtime and Microsoft.ML.Tokenizers are in
`ThirdPartyNotices.txt` and `THIRD-PARTY-NOTICES.TXT` in the application's `Licenses` folder.

## MIT License

Permission is hereby granted, free of charge, to any person obtaining a copy of this software
and associated documentation files (the "Software"), to deal in the Software without
restriction, including without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the
Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
