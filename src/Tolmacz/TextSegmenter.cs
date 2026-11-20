using System.Text.RegularExpressions;

namespace Tolmacz;

/// <summary>A piece of text, either to translate or to copy unchanged.</summary>
/// <param name="Text">The original characters.</param>
/// <param name="IsTranslatable">False for whitespace, numbers, symbols and addresses.</param>
internal readonly record struct TextSegment(string Text, bool IsTranslatable);

/// <summary>
/// Splits text into sentences, because the model is trained on single sentences.
/// Concatenating every segment always reproduces the original text exactly.
/// </summary>
internal static partial class TextSegmenter
{
    /// <summary>The longest piece, in characters, produced by <see cref="Split"/>.</summary>
    public const int MaxSegmentLength = 800;

    // Only forms that are not also ordinary words, so real sentence endings are kept.
    private static readonly HashSet<string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        // English
        "mr", "mrs", "ms", "dr", "prof", "st", "vs", "e.g", "i.e",
        // Polish
        "np", "m.in", "tj", "tzn", "tzw", "ul", "al", "pl", "gen", "płk", "inż", "mgr", "hab", "ks", "św",
    };

    // These are abbreviations only before a number, as in "No. 5" or "ok. 20".
    private static readonly HashSet<string> NumberAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "no", "fig", "nr", "ok", "tel", "str", "godz", "pkt",
    };

    /// <summary>Splits text into sentences and the whitespace between them.</summary>
    public static IReadOnlyList<TextSegment> Split(string text)
    {
        var segments = new List<TextSegment>();
        var sentenceStart = -1;
        var position = 0;

        while (position < text.Length)
        {
            var whitespaceEnd = SkipWhitespace(text, position);
            if (whitespaceEnd == position)
            {
                sentenceStart = sentenceStart < 0 ? position : sentenceStart;
                position++;
                continue;
            }

            if (sentenceStart >= 0
                && (whitespaceEnd == text.Length || IsBoundary(text, sentenceStart, position, whitespaceEnd)))
            {
                AddSentence(segments, text[sentenceStart..position]);
                sentenceStart = -1;
            }

            if (sentenceStart < 0)
            {
                segments.Add(new TextSegment(text[position..whitespaceEnd], false));
            }

            position = whitespaceEnd;
        }

        if (sentenceStart >= 0)
        {
            AddSentence(segments, text[sentenceStart..]);
        }

        return segments;
    }

    /// <summary>
    /// Returns whether text needs the model. Numbers, symbols, web addresses and email
    /// addresses are copied unchanged, because the model invents words around them.
    /// </summary>
    public static bool IsTranslatable(string text) =>
        text.Any(char.IsLetter) && !CopiedAddress().IsMatch(text);

    /// <summary>
    /// Splits text at the whitespace nearest its middle, preferring a break after punctuation.
    /// </summary>
    /// <returns>False when the text contains no whitespace between words.</returns>
    public static bool TrySplitNearMiddle(string text, out string left, out string separator, out string right)
    {
        left = separator = right = string.Empty;
        var start = FindBreakNearMiddle(text);
        if (start < 0)
        {
            return false;
        }

        var end = SkipWhitespace(text, start);
        if (end == text.Length)
        {
            return false;
        }

        left = text[..start];
        separator = text[start..end];
        right = text[end..];
        return true;
    }

    private static int FindBreakNearMiddle(string text)
    {
        var middle = text.Length / 2;
        var best = -1;
        var bestCost = int.MaxValue;

        for (var i = 1; i < text.Length - 1; i++)
        {
            if (!char.IsWhiteSpace(text[i]) || char.IsWhiteSpace(text[i - 1]))
            {
                continue;
            }

            var distance = Math.Abs(i - middle);
            var cost = text[i - 1] is ',' or ';' or ':' ? distance / 2 : distance;
            if (cost < bestCost)
            {
                best = i;
                bestCost = cost;
            }
        }

        return best;
    }

    private static int SkipWhitespace(string text, int position)
    {
        while (position < text.Length && char.IsWhiteSpace(text[position]))
        {
            position++;
        }

        return position;
    }

    private static bool IsBoundary(string text, int sentenceStart, int whitespaceStart, int next)
    {
        // Line breaks and tabs show the structure of the text, so each part is translated separately.
        if (text.AsSpan(whitespaceStart, next - whitespaceStart).IndexOfAny('\r', '\n', '\t') >= 0)
        {
            return true;
        }

        var end = whitespaceStart - 1;
        while (end > sentenceStart && IsClosingPunctuation(text[end]))
        {
            end--;
        }

        if (text[end] is not ('.' or '!' or '?' or '…'))
        {
            return false;
        }

        if (!StartsSentence(text, next))
        {
            return false;
        }

        return text[end] != '.' || !EndsWithAbbreviation(text, sentenceStart, end, next);
    }

    private static bool StartsSentence(string text, int index)
    {
        while (index < text.Length && IsOpeningPunctuation(text[index]))
        {
            index++;
        }

        return index < text.Length && (char.IsUpper(text[index]) || char.IsDigit(text[index]));
    }

    private static bool EndsWithAbbreviation(string text, int sentenceStart, int period, int next)
    {
        var wordStart = period;
        while (wordStart > sentenceStart && !char.IsWhiteSpace(text[wordStart - 1]))
        {
            wordStart--;
        }

        var word = text[wordStart..period].TrimStart('(', '[', '"', '\'', '„', '“', '«');
        if (word.Length == 1)
        {
            // A capital letter is an initial, except the English word "I".
            return char.IsUpper(word[0]) && word[0] != 'I';
        }

        // Words with an inner period, such as "U.S" or "e.g", are abbreviations.
        if (word.Contains('.', StringComparison.Ordinal))
        {
            return true;
        }

        return Abbreviations.Contains(word)
            || (NumberAbbreviations.Contains(word) && char.IsDigit(text[next]));
    }

    private static void AddSentence(List<TextSegment> segments, string sentence)
    {
        var start = 0;
        while (sentence.Length - start > MaxSegmentLength)
        {
            var end = FindBreak(sentence, start);
            var whitespaceEnd = SkipWhitespace(sentence, end);
            AddPiece(segments, sentence[start..end]);
            if (whitespaceEnd > end)
            {
                segments.Add(new TextSegment(sentence[end..whitespaceEnd], false));
            }

            start = whitespaceEnd;
        }

        AddPiece(segments, sentence[start..]);
    }

    private static int FindBreak(string sentence, int start)
    {
        var limit = start + MaxSegmentLength;
        var lastSpace = -1;
        for (var i = limit; i > start; i--)
        {
            if (!char.IsWhiteSpace(sentence[i]))
            {
                continue;
            }

            // A break near the start would leave a fragment without context.
            if (sentence[i - 1] is ',' or ';' or ':' && i - start >= MaxSegmentLength / 2)
            {
                return i;
            }

            if (lastSpace < 0)
            {
                lastSpace = i;
            }
        }

        if (lastSpace < 0)
        {
            return char.IsLowSurrogate(sentence[limit]) ? limit - 1 : limit;
        }

        while (lastSpace > start + 1 && char.IsWhiteSpace(sentence[lastSpace - 1]))
        {
            lastSpace--;
        }

        return lastSpace;
    }

    private static void AddPiece(List<TextSegment> segments, string piece)
    {
        if (piece.Length > 0)
        {
            segments.Add(new TextSegment(piece, IsTranslatable(piece)));
        }
    }

    [GeneratedRegex(@"^(?:(?:[a-z][a-z0-9+.-]*://|www\.)\S+|[^\s@]+@[^\s@]+\.[^\s@]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex CopiedAddress();

    private static bool IsClosingPunctuation(char c) =>
        c is '"' or '\'' or ')' or ']' or '”' or '’' or '»';

    private static bool IsOpeningPunctuation(char c) =>
        c is '"' or '\'' or '(' or '[' or '„' or '“' or '‘' or '«' or '-' or '–' or '—';
}
