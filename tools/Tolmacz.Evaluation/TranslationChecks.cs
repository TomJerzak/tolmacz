using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Tolmacz.Evaluation;

/// <summary>Finds translation problems that can be detected without a reference translation.</summary>
internal static partial class TranslationChecks
{
    private const int MinCharactersForLengthCheck = 30;
    private const int MinLettersForCopyCheck = 20;
    private const double MinLengthRatio = 0.4;
    private const double MaxLengthRatio = 2.5;
    private const int MaxRepeatedPhraseWords = 5;

    private static readonly char[] AddressEndings = ['.', ',', ';', ':', '!', '?', ')', ']', '"', '\''];

    public static IReadOnlyList<string> Find(string source, string translation, bool isComplete)
    {
        var violations = new List<string>();
        void AddIf(bool condition, string name)
        {
            if (condition)
            {
                violations.Add(name);
            }
        }

        var hasOutput = !string.IsNullOrWhiteSpace(translation);
        AddIf(!hasOutput, "empty_output");
        AddIf(!isComplete, "reported_incomplete");
        AddIf(HasMissingNumber(source, translation), "number_missing");
        AddIf(HasAddedNumber(source, translation), "number_added");
        AddIf(HasDuplicatedNumber(source, translation), "number_duplicated");
        AddIf(HasChangedAddress(source, translation), "address_changed");
        AddIf(HasLostProtectedCharacter(source, translation), "protected_text_lost");
        AddIf(HasChangedLayout(source, translation), "layout_changed");
        AddIf(HasAddedRepetition(source, translation), "repetition_added");
        AddIf(HasSuspiciousLength(source, translation), "suspicious_length");
        AddIf(Parenthetical().Count(translation) > Parenthetical().Count(source), "parenthetical_added");
        AddIf(LooksUntranslated(source, translation), "text_not_translated");
        return violations;
    }

    private static bool HasMissingNumber(string source, string translation)
    {
        var available = CountNumbers(translation);
        foreach (Match number in DigitGroups().Matches(source))
        {
            if (available.GetValueOrDefault(number.Value) == 0)
            {
                return true;
            }

            available[number.Value]--;
        }

        return false;
    }

    private static bool HasAddedNumber(string source, string translation)
    {
        var known = CountNumbers(source);
        return DigitGroups().Matches(translation).Any(number => !known.ContainsKey(number.Value));
    }

    private static bool HasDuplicatedNumber(string source, string translation)
    {
        var known = CountNumbers(source);
        return CountNumbers(translation).Any(number => known.TryGetValue(number.Key, out var count) && number.Value > count);
    }

    private static Dictionary<string, int> CountNumbers(string text) =>
        DigitGroups().Matches(text)
            .GroupBy(number => number.Value)
            .ToDictionary(group => group.Key, group => group.Count());

    private static bool HasChangedAddress(string source, string translation) =>
        Addresses().Matches(source)
            .Select(address => address.Value.TrimEnd(AddressEndings))
            .Any(address => !translation.Contains(address, StringComparison.Ordinal));

    private static bool HasLostProtectedCharacter(string source, string translation)
    {
        var output = translation.EnumerateRunes().ToHashSet();
        return source.EnumerateRunes().Any(rune => IsProtected(rune) && !output.Contains(rune));
    }

    private static bool IsProtected(Rune rune)
    {
        var isSymbol = Rune.GetUnicodeCategory(rune) == UnicodeCategory.OtherSymbol;
        var isOtherScript = Rune.IsLetter(rune)
            && rune.Value >= 0x0370
            && rune.Value is not (>= 0x1E00 and <= 0x1EFF);
        return isSymbol || isOtherScript;
    }

    private static bool HasChangedLayout(string source, string translation)
    {
        var normalizedSource = source.ReplaceLineEndings("\n");
        var normalizedOutput = translation.ReplaceLineEndings("\n");
        return normalizedSource.Count(c => c == '\n') != normalizedOutput.Count(c => c == '\n')
            || normalizedSource.Count(c => c == '\t') != normalizedOutput.Count(c => c == '\t')
            || LeadingWhitespace(normalizedSource) != LeadingWhitespace(normalizedOutput)
            || TrailingWhitespace(normalizedSource) != TrailingWhitespace(normalizedOutput);
    }

    private static string LeadingWhitespace(string text) => text[..(text.Length - text.TrimStart().Length)];

    private static string TrailingWhitespace(string text) => text[text.TrimEnd().Length..];

    private static bool HasAddedRepetition(string source, string translation) =>
        !HasRepetition(source) && HasRepetition(translation);

    private static bool HasRepetition(string text)
    {
        var words = Words().Matches(text).Select(word => word.Value.ToUpperInvariant()).ToArray();
        for (var size = 1; size <= MaxRepeatedPhraseWords; size++)
        {
            var requiredRepeats = size == 1 ? 4 : 3;
            for (var start = 0; start + size * requiredRepeats <= words.Length; start++)
            {
                var repeats = 1;
                while (start + size * (repeats + 1) <= words.Length && SamePhrase(words, start, start + size * repeats, size))
                {
                    repeats++;
                }

                if (repeats >= requiredRepeats)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SamePhrase(string[] words, int first, int second, int size) =>
        Enumerable.Range(0, size).All(offset => words[first + offset] == words[second + offset]);

    private static bool HasSuspiciousLength(string source, string translation)
    {
        var sourceLength = source.Trim().Length;
        if (sourceLength < MinCharactersForLengthCheck)
        {
            return false;
        }

        var ratio = translation.Trim().Length / (double)sourceLength;
        return ratio is < MinLengthRatio or > MaxLengthRatio;
    }

    private static bool LooksUntranslated(string source, string translation) =>
        source.Count(char.IsLetter) >= MinLettersForCopyCheck
        && string.Equals(source.Trim(), translation.Trim(), StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitGroups();

    [GeneratedRegex(@"\b(?:https?://|www\.)\S+|[\w.+-]+@[\w-]+(?:\.[\w-]+)+", RegexOptions.IgnoreCase)]
    private static partial Regex Addresses();

    [GeneratedRegex(@"\([^()]*\)")]
    private static partial Regex Parenthetical();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Words();
}
