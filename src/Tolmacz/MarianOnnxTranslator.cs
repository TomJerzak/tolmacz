using System.Text;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;

namespace Tolmacz;

/// <summary>Runs a local Marian model on the CPU with ONNX Runtime. No network access.</summary>
public sealed partial class MarianOnnxTranslator : ITranslator, IDisposable
{
    private const int BeamSize = 4;
    private const int MaxSourceTokens = 256;
    private const int MaxOutputTokens = 900;
    private const int MaxShortPhraseWords = 3;
    private const int MaxWordLength = 64;
    private const int MinListItems = 3;

    private readonly string _modelDirectory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private MarianTokenizer? _tokenizer;
    private InferenceSession? _encoder;
    private InferenceSession? _decoder;
    private bool[] _forbiddenTokens = [];

    /// <summary>Creates a translator. The model is loaded on first use.</summary>
    /// <param name="modelDirectory">The folder containing the extracted model release.</param>
    public MarianOnnxTranslator(string modelDirectory) => _modelDirectory = modelDirectory;

    /// <inheritdoc />
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_decoder is null)
            {
                await Task.Run(Load, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<TranslationResult> TranslateAsync(
        string text,
        TranslationDirection direction,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await LoadAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                () => TranslateText(text, direction, progress, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _encoder?.Dispose();
        _decoder?.Dispose();
        _gate.Dispose();
    }

    private void Load()
    {
        TranslationModel.Validate(_modelDirectory);

        InferenceSession? encoder = null;
        InferenceSession? decoder = null;
        try
        {
            var tokenizer = new MarianTokenizer(_modelDirectory);
            using var options = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            };

            // On x64 CPUs without VNNI, the fast int8 multiplication can overflow and change
            // the translation. This selects the exact routine. Other CPUs ignore the setting.
            options.AddSessionConfigEntry("session.x64quantprecision", "1");

            encoder = new InferenceSession(Path.Combine(_modelDirectory, TranslationModel.EncoderFile), options);
            decoder = new InferenceSession(Path.Combine(_modelDirectory, TranslationModel.DecoderFile), options);

            _forbiddenTokens = CreateForbiddenTokens(tokenizer);
            _tokenizer = tokenizer;
            _encoder = encoder;
            _decoder = decoder;
        }
        catch (Exception exception)
        {
            encoder?.Dispose();
            decoder?.Dispose();
            throw new TranslationModelException(
                _modelDirectory,
                ["the model files could not be loaded"],
                exception);
        }
    }

    private TranslationResult TranslateText(
        string text,
        TranslationDirection direction,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var segments = TextSegmenter.Split(text);
        var totalLength = Math.Max(1, segments.Where(s => s.IsTranslatable).Sum(s => s.Text.Length));
        var completedLength = 0;
        var isComplete = true;
        var output = new StringBuilder(text.Length);

        progress?.Report(0);
        foreach (var segment in segments)
        {
            if (!segment.IsTranslatable)
            {
                output.Append(segment.Text);
                continue;
            }

            var result = TranslateSegment(segment.Text, direction, cancellationToken);
            output.Append(result.Text);
            isComplete &= result.IsComplete && !RepetitionCleaner.HasLongWordRun(segment.Text);
            completedLength += segment.Text.Length;
            progress?.Report((double)completedLength / totalLength);
        }

        progress?.Report(1);
        return new TranslationResult(output.ToString(), isComplete);
    }

    private TranslationResult TranslateSegment(
        string segment,
        TranslationDirection direction,
        CancellationToken cancellationToken)
    {
        if (!TextSegmenter.IsTranslatable(segment))
        {
            return new TranslationResult(segment, true);
        }

        return TranslateAroundProtectedWords(segment, direction, cancellationToken)
            ?? TranslateWithModel(segment, direction, cancellationToken);
    }

    private TranslationResult TranslateWithModel(
        string segment,
        TranslationDirection direction,
        CancellationToken cancellationToken)
    {
        var tokenizer = _tokenizer!;
        var usedPeriod = IsShortPhrase(segment);
        var sourceIds = tokenizer.Encode(usedPeriod ? segment + "." : segment, direction);
        if (sourceIds.Length > MaxSourceTokens
            && TryTranslateInParts(segment, direction, cancellationToken) is { } parts)
        {
            return parts;
        }

        var search = Search(sourceIds, cancellationToken);
        var decoded = tokenizer.Decode(search.Tokens);
        var translation = CleanOutput(segment, RepetitionCleaner.RemoveEchoedParenthesis(segment, RepetitionCleaner.Clean(segment, decoded)), direction);
        var numbersChanged = NumberCheck.ChangedNumbers(segment, translation);
        var isComplete = !search.ReachedLimit && !numbersChanged;
        var listRetry = numbersChanged ? TryTranslateListItems(segment, direction, cancellationToken) : null;
        if (!isComplete
            && (listRetry ?? TryTranslateInParts(segment, direction, cancellationToken)) is { } retried)
        {
            // The retried parts are too small for the number check, so check the joined result.
            return retried with
            {
                IsComplete = retried.IsComplete && !NumberCheck.ChangedNumbers(segment, retried.Text),
            };
        }

        var text = usedPeriod ? RemoveTemporaryEnding(translation, direction) : translation;
        if (isComplete && usedPeriod)
        {
            text = direction == TranslationDirection.PolishToEnglish
                ? PreferPlainShortTranslation(segment, text, cancellationToken)
                : PreferPlainPolishTranslation(segment, text, cancellationToken);
        }

        return new TranslationResult(text, isComplete);
    }

    private static string CleanOutput(string segment, string translation, TranslationDirection direction)
    {
        var text = OutputCleanup.RemoveLeadingDash(segment, OutputCleanup.RestoreUnitExponents(segment, translation));
        return direction == TranslationDirection.EnglishToPolish
            ? OutputCleanup.RemoveCategoryLabel(segment, OutputCleanup.RemoveEnglishMarker(segment, text))
            : text;
    }

    // The temporary period turns commands into sentences, as in "Zapisz" -> "Write it down".
    // Without the period the model often gives the shorter "Save", but it can also repeat
    // itself, cut a phrase short or drop a meaningful word, as in "Name" for "Last name".
    // That version is used only when it is clean and the period only added articles or a pronoun.
    private string PreferPlainShortTranslation(string segment, string periodTranslation, CancellationToken cancellationToken)
    {
        var tokenizer = _tokenizer!;
        var search = Search(tokenizer.Encode(segment, TranslationDirection.PolishToEnglish), cancellationToken);
        if (search.ReachedLimit)
        {
            return periodTranslation;
        }

        var plain = RepetitionCleaner.RemoveEchoedParenthesis(
            segment,
            RepetitionCleaner.Clean(segment, tokenizer.Decode(search.Tokens)));
        var isBetter = !RepetitionCleaner.LooksDamaged(segment, plain)
            && !RepetitionCleaner.LostSourceRepeat(segment, plain)
            && !RepetitionCleaner.IsCutShort(plain, periodTranslation)
            && RepetitionCleaner.OnlyAddsFillerOrPronoun(plain, periodTranslation)
            && Words().Count(plain) < Words().Count(periodTranslation);
        if (!isBetter)
        {
            return periodTranslation;
        }

        return char.IsUpper(segment[0]) && char.IsLower(plain[0])
            ? char.ToUpperInvariant(plain[0]) + plain[1..]
            : plain;
    }

    // In English short phrases the temporary period fixes nouns and names, as in "House" ->
    // "Dom" instead of "Domek letniskowy", but changes commands, as in "Save" -> "Oszczędzaj".
    // The plain translation is kept when it looks like a clean word-for-word answer. A raw
    // repeat, as in "Warszawie Warszawie" for "Warsaw", means the model is unsure without the
    // period. A damaged period version, as in "Nie, nie, nie" for "the", means the period does
    // not help, so the plain translation stays as before.
    private string PreferPlainPolishTranslation(string segment, string periodTranslation, CancellationToken cancellationToken)
    {
        var tokenizer = _tokenizer!;
        var search = Search(tokenizer.Encode(segment, TranslationDirection.EnglishToPolish), cancellationToken);
        var raw = tokenizer.Decode(search.Tokens);
        if (search.ReachedLimit || RepetitionCleaner.LooksRepetitive(raw) || RepetitionCleaner.LostSourceRepeat(segment, raw))
        {
            return periodTranslation;
        }

        var plain = CleanOutput(
            segment,
            RepetitionCleaner.RemoveEchoedParenthesis(segment, RepetitionCleaner.Clean(segment, raw)),
            TranslationDirection.EnglishToPolish);
        if (RepetitionCleaner.LooksDamaged(segment, periodTranslation))
        {
            return plain;
        }

        // The dash check uses the raw output, because cleanup removes the dash that marks a guess.
        var isClean = !RepetitionCleaner.LooksDamaged(segment, plain)
            && !RepetitionCleaner.LostSourceRepeat(segment, plain)
            && !RepetitionCleaner.RepeatsWordStem(plain)
            && !plain.Contains('(', StringComparison.Ordinal)
            && !raw.TrimStart().StartsWith('-')
            && RepetitionCleaner.CountWords(plain) == RepetitionCleaner.CountWordsWithoutArticles(segment)
            && char.IsUpper(plain[0]) == char.IsUpper(segment[0]);
        return isClean ? plain : periodTranslation;
    }

    // Used only after numbers changed: the model can repeat or drop items of a long comma list,
    // while each item translated alone keeps its number.
    private TranslationResult? TryTranslateListItems(
        string segment,
        TranslationDirection direction,
        CancellationToken cancellationToken)
    {
        var parts = ListSeparator().Split(segment);
        if (parts.Length < MinListItems * 2 - 1)
        {
            return null;
        }

        var output = new StringBuilder(segment.Length);
        var isComplete = true;
        for (var index = 0; index < parts.Length; index++)
        {
            if (index % 2 == 1)
            {
                output.Append(parts[index]);
                continue;
            }

            var result = TranslateListItem(parts[index], direction, cancellationToken);
            output.Append(result.Text);
            isComplete &= result.IsComplete;
        }

        return new TranslationResult(output.ToString(), isComplete);
    }

    // A list item alone is a fragment, so a temporary period makes the model treat it as a sentence.
    private TranslationResult TranslateListItem(
        string item,
        TranslationDirection direction,
        CancellationToken cancellationToken)
    {
        if (item.Length == 0 || !char.IsLetterOrDigit(item[^1]))
        {
            return TranslateSegment(item, direction, cancellationToken);
        }

        var result = TranslateSegment(item + ".", direction, cancellationToken);
        return result with { Text = RemoveTemporaryEnding(result.Text, direction) };
    }

    // Words the model cannot represent are copied, so they are neither lost nor replaced.
    private TranslationResult? TranslateAroundProtectedWords(
        string segment,
        TranslationDirection direction,
        CancellationToken cancellationToken)
    {
        var tokenizer = _tokenizer!;
        var words = Words().Matches(segment);
        if (!tokenizer.ContainsUnknown(segment) && words.All(word => word.Length <= MaxWordLength))
        {
            return null;
        }

        var isProtected = words
            .Select(word => word.Length > MaxWordLength || tokenizer.ContainsUnknown(word.Value))
            .ToArray();
        if (!isProtected.Contains(true))
        {
            return null;
        }

        var output = new StringBuilder(segment.Length);
        var isComplete = true;
        var position = 0;
        for (var first = 0; first < words.Count; first++)
        {
            var last = first;
            while (!isProtected[first] && last + 1 < words.Count && !isProtected[last + 1])
            {
                last++;
            }

            var end = words[last].Index + words[last].Length;
            var part = segment[words[first].Index..end];
            output.Append(segment, position, words[first].Index - position);
            if (isProtected[first])
            {
                output.Append(part);
            }
            else
            {
                var result = TranslateSegment(part, direction, cancellationToken);
                output.Append(result.Text);
                isComplete &= result.IsComplete;
            }

            position = end;
            first = last;
        }

        output.Append(segment, position, segment.Length - position);
        return new TranslationResult(output.ToString(), isComplete);
    }

    // The model repeats itself on short phrases without final punctuation, such as
    // "Warszawa". A period makes it treat them as complete sentences.
    private static bool IsShortPhrase(string segment) =>
        char.IsLetterOrDigit(segment[^1]) && Words().Count(segment) <= MaxShortPhraseWords;

    private static string RemoveTemporaryEnding(string translation, TranslationDirection direction)
    {
        var text = (direction == TranslationDirection.EnglishToPolish
            ? EncyclopediaEnding().Replace(translation, string.Empty)
            : translation).TrimEnd();
        if (!text.EndsWith('.'))
        {
            return text;
        }

        // The model sometimes repeats the period, as in "Warszawa. .", so remove all of them.
        var withoutPeriod = text.TrimEnd('.', ' ');
        var lastWord = withoutPeriod[(withoutPeriod.LastIndexOf(' ') + 1)..];
        var isAbbreviation = lastWord.Contains('.') || lastWord.Equals("etc", StringComparison.OrdinalIgnoreCase);
        return isAbbreviation ? withoutPeriod + "." : withoutPeriod;
    }

    private TranslationResult? TryTranslateInParts(
        string segment,
        TranslationDirection direction,
        CancellationToken cancellationToken)
    {
        if (!TextSegmenter.TrySplitNearMiddle(segment, out var left, out var separator, out var right))
        {
            return null;
        }

        var first = TranslateSegment(left, direction, cancellationToken);
        var second = TranslateSegment(right, direction, cancellationToken);
        return new TranslationResult(
            first.Text + separator + second.Text,
            first.IsComplete && second.IsComplete);
    }

    private BeamSearchResult Search(long[] sourceIds, CancellationToken cancellationToken)
    {
        var tokenizer = _tokenizer!;
        var attentionMask = new long[sourceIds.Length];
        Array.Fill(attentionMask, 1L);

        var decoder = new OnnxBeamDecoder(_decoder!, attentionMask, Encode(sourceIds, attentionMask));
        var options = new BeamSearchOptions(
            BeamSize,
            Math.Min(MaxOutputTokens, sourceIds.Length * 3 + 16),
            tokenizer.PadId,
            tokenizer.EndOfSentenceId,
            _forbiddenTokens);
        return BeamSearch.Run(decoder, options, cancellationToken);
    }

    private float[] Encode(long[] sourceIds, long[] attentionMask)
    {
        long[] shape = [1, sourceIds.Length];
        using var inputIds = OrtValue.CreateTensorValueFromMemory(OrtMemoryInfo.DefaultInstance, sourceIds.AsMemory(), shape);
        using var mask = OrtValue.CreateTensorValueFromMemory(OrtMemoryInfo.DefaultInstance, attentionMask.AsMemory(), shape);
        using var runOptions = new RunOptions();
        using var outputs = _encoder!.Run(
            runOptions,
            ["input_ids", "attention_mask"],
            [inputIds, mask],
            ["last_hidden_state"]);
        return outputs[0].GetTensorDataAsSpan<float>().ToArray();
    }

    [GeneratedRegex(@"\S+")]
    private static partial Regex Words();

    // A comma followed by whitespace; the capture keeps the separator in the split result.
    // Decimal commas such as "5,50" have no whitespace and are not split.
    [GeneratedRegex(@"(,\s+)")]
    private static partial Regex ListSeparator();

    // Dictionary-style endings the model adds to bare names, such as "(ang.)".
    [GeneratedRegex(@"(?:\s*\(ang\.\)|,\s*właśc\.?)\.?$")]
    private static partial Regex EncyclopediaEnding();

    private static bool[] CreateForbiddenTokens(MarianTokenizer tokenizer)
    {
        var forbidden = new bool[tokenizer.VocabularySize];
        forbidden[0] = true;
        forbidden[tokenizer.PadId] = true;
        forbidden[tokenizer.UnknownId] = true;
        forbidden[tokenizer.EnglishId] = true;
        forbidden[tokenizer.PolishId] = true;
        return forbidden;
    }
}
