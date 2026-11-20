using System.Text.RegularExpressions;

namespace Tolmacz;

/// <summary>
/// Removes the repeats the model tends to add to very short sentences, such as
/// "Good morning. Good morning." for "Dzień dobry."
/// </summary>
internal static partial class RepetitionCleaner
{
    private const int MaxSourceWords = 6;
    private const int LongWordRun = 8;
    private const int StemLength = 4;
    private const string SentenceEndings = ".!?…";

    private static readonly char[] TrailingPunctuation = ['.', ',', ';', ':', '!', '?', '…'];
    private static readonly HashSet<string> ObjectPronouns = new(StringComparer.OrdinalIgnoreCase) { "it", "them", "this", "that" };
    private static readonly HashSet<string> Articles = new(StringComparer.Ordinal) { "THE", "A", "AN" };
    private static readonly HashSet<string> Pronouns = new(StringComparer.Ordinal)
    {
        "IT", "IT'S", "I", "I'LL", "I'M", "YOU", "YOU'RE", "YOU'LL", "HE", "SHE", "WE", "THEY", "ME",
    };
    private static readonly char[] SourceWordSeparators = [' ', '\t', '\r', '\n', '-', '–', '—'];

    /// <summary>Limits a short translation to the number of repeats found in its source.</summary>
    public static string Clean(string source, string translation)
    {
        var words = SplitWords(translation);
        var period = FindRepeatPeriod(words);
        var sourceWords = SplitSourceWords(source);
        if (period == 0 || sourceWords.Length > MaxSourceWords)
        {
            return translation;
        }

        var sourcePeriod = FindRepeatPeriod(sourceWords);
        var allowedRepeats = sourcePeriod == 0 ? 1 : sourceWords.Length / sourcePeriod;
        var isSeparated = TrailingPart(words[period - 1]).Length > 0;
        var isNaturalDoubling = !isSeparated && words.Length <= sourceWords.Length;
        if (words.Length / period <= allowedRepeats || isNaturalDoubling)
        {
            return translation;
        }

        var kept = words[..(period * allowedRepeats)];
        var ending = TrailingPart(words[^1]);
        if (ending.Length == 0 && TrailingPart(kept[^1]) is { Length: > 0 } own && SentenceEndings.Contains(own[^1]))
        {
            ending = own;
        }

        kept[^1] = kept[^1].TrimEnd(TrailingPunctuation) + ending;
        return string.Join(' ', kept);
    }

    /// <summary>
    /// Removes a bracket that only repeats the translation, as in "Chleb (chleb)", when the
    /// source has no brackets. Brackets with any other content are kept.
    /// </summary>
    public static string RemoveEchoedParenthesis(string source, string translation)
    {
        var match = EchoedParenthesis().Match(translation);
        var isEcho = match.Success
            && !source.Contains('(', StringComparison.Ordinal)
            && string.Equals(match.Groups["text"].Value.Trim(), match.Groups["echo"].Value.Trim(), StringComparison.OrdinalIgnoreCase);
        return isEcho ? match.Groups["text"].Value.TrimEnd() + match.Groups["end"].Value : translation;
    }

    /// <summary>Returns whether text repeats itself, as in "Cat cat" or "Quiet and quiet".</summary>
    public static bool LooksRepetitive(string translation)
    {
        var words = SplitWords(translation);
        return FindRepeatPeriod(words) != 0
            || (words.Length == 3
                && words[1] is "i" or "and"
                && string.Equals(Normalize(words[0]), Normalize(words[2]), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns whether a short translation looks damaged: it repeats itself, repeats a word next
    /// to itself, as in "Good Night Night", or cuts a name short, as in "Jan Ko" for "Jan Kowalski".
    /// </summary>
    public static bool LooksDamaged(string source, string translation)
    {
        var words = SplitWords(translation).Select(Normalize).Where(word => word.Length > 0).ToArray();
        var names = SplitSourceWords(source).Select(Normalize).Where(word => word.Length > 0 && char.IsUpper(word[0]));
        var hasAdjacentRepeat = words.Zip(words.Skip(1), (left, right) => left.Equals(right, StringComparison.OrdinalIgnoreCase)).Any(same => same);
        var cutsName = words.Any(word => names.Any(name =>
            name.Length > word.Length && name.StartsWith(word, StringComparison.OrdinalIgnoreCase)));
        return words.Length == 0 || LooksRepetitive(translation) || hasAdjacentRepeat || cutsName;
    }

    /// <summary>
    /// Returns whether a shorter translation is just the beginning of a longer one, as in
    /// "Thank you very" for "Thank you very much" or "App" for "Approve it". An added object
    /// pronoun does not count, so "Cancel" is not a cut version of "Cancel it".
    /// </summary>
    public static bool IsCutShort(string shorter, string longer)
    {
        var shortWords = SplitWords(shorter).Select(Normalize).Where(word => word.Length > 0).ToArray();
        var longWords = SplitWords(longer).Select(Normalize).Where(word => word.Length > 0).ToArray();
        if (shortWords.Length == 0 || shortWords.Length > longWords.Length)
        {
            return false;
        }

        for (var i = 0; i < shortWords.Length - 1; i++)
        {
            if (!shortWords[i].Equals(longWords[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var last = shortWords[^1];
        var matchingWord = longWords[shortWords.Length - 1];
        if (!matchingWord.StartsWith(last, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return matchingWord.Length > last.Length
            || longWords.Skip(shortWords.Length).Any(word => !ObjectPronouns.Contains(word));
    }

    /// <summary>
    /// Returns whether a longer translation only adds articles to a shorter one, as in "The cat"
    /// for "Cat", or turns it into a sentence with a pronoun, as in "Write it down" for "Save".
    /// Other added words, as in "Last name" for "Name", may carry meaning.
    /// </summary>
    public static bool OnlyAddsFillerOrPronoun(string shorter, string longer)
    {
        var shortWords = SplitWords(shorter).Select(word => Normalize(word).ToUpperInvariant()).Where(word => word.Length > 0).ToArray();
        var longWords = SplitWords(longer).Select(word => Normalize(word).ToUpperInvariant()).Where(word => word.Length > 0).ToArray();
        var addsPronoun = longWords.Any(Pronouns.Contains);
        var sameWithoutArticles = longWords.Where(word => !Articles.Contains(word))
            .SequenceEqual(shortWords.Where(word => !Articles.Contains(word)));
        return addsPronoun || sameWithoutArticles;
    }

    /// <summary>
    /// Returns whether two neighbouring words start the same way, as in "Samochód samochodowy"
    /// for "Car" or "Deszcz deszczowy" for "Rain".
    /// </summary>
    public static bool RepeatsWordStem(string translation)
    {
        var words = SplitWords(translation).Select(Normalize).ToArray();
        return words.Zip(words.Skip(1)).Any(pair =>
            pair.First.Length >= StemLength
            && pair.Second.Length >= StemLength
            && pair.First.AsSpan(0, StemLength).Equals(pair.Second.AsSpan(0, StemLength), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Counts the words of English text without the articles "the", "a" and "an", which Polish
    /// does not have, so "The cat" counts as one word, like its translation "Kot".
    /// </summary>
    public static int CountWordsWithoutArticles(string text) =>
        SplitWords(text).Select(Normalize).Count(word => word.Length > 0 && !Articles.Contains(word.ToUpperInvariant()));

    /// <summary>Counts the words of text the same way as <see cref="CountWordsWithoutArticles"/>, keeping every word.</summary>
    public static int CountWords(string text) =>
        SplitWords(text).Select(Normalize).Count(word => word.Length > 0);

    /// <summary>Returns whether the source repeats itself, as in "Baden-Baden", but the translation does not.</summary>
    public static bool LostSourceRepeat(string source, string translation) =>
        FindRepeatPeriod(SplitSourceWords(source)) != 0
        && FindRepeatPeriod(SplitSourceWords(translation)) == 0;

    /// <summary>Returns whether text repeats one word so often that the model may lose content.</summary>
    public static bool HasLongWordRun(string text)
    {
        // Punctuation such as the dots in a table of contents is not a repeated word.
        var words = SplitWords(text).Select(Normalize).Where(word => word.Length > 0).ToArray();
        var runLength = 1;
        for (var i = 1; i < words.Length; i++)
        {
            var isSameWord = string.Equals(words[i], words[i - 1], StringComparison.OrdinalIgnoreCase);
            runLength = isSameWord ? runLength + 1 : 1;
            if (runLength >= LongWordRun)
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"^(?<text>[^()]+?)\s*\((?<echo>[^()]+)\)(?<end>[.!?…]*)$")]
    private static partial Regex EchoedParenthesis();

    private static string[] SplitWords(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private static string[] SplitSourceWords(string text) =>
        text.Split(SourceWordSeparators, StringSplitOptions.RemoveEmptyEntries);

    private static int FindRepeatPeriod(string[] words)
    {
        for (var period = 1; period <= words.Length / 2; period++)
        {
            if (words.Length % period != 0)
            {
                continue;
            }

            var repeats = true;
            for (var i = period; i < words.Length && repeats; i++)
            {
                repeats = string.Equals(
                    Normalize(words[i]),
                    Normalize(words[i - period]),
                    StringComparison.OrdinalIgnoreCase);
            }

            if (repeats)
            {
                return period;
            }
        }

        return 0;
    }

    private static string Normalize(string word) => word.Trim(TrailingPunctuation);

    private static string TrailingPart(string word) => word[word.TrimEnd(TrailingPunctuation).Length..];
}
