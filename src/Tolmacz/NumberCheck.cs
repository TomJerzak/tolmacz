using System.Text.RegularExpressions;

namespace Tolmacz;

/// <summary>Detects number-heavy text whose numbers changed during translation.</summary>
internal static partial class NumberCheck
{
    private const int MinNumbersToCheck = 6;

    /// <summary>
    /// Returns whether a source with many numbers has numbers missing from its translation or
    /// repeated more often than in the source. Numbers are compared as groups of digits, so
    /// "1,250" and "1 250" count as equal.
    /// </summary>
    public static bool ChangedNumbers(string source, string translation)
    {
        var sourceNumbers = Count(source);
        if (sourceNumbers.Values.Sum() < MinNumbersToCheck)
        {
            return false;
        }

        var translatedNumbers = Count(translation);
        return sourceNumbers.Any(number => translatedNumbers.GetValueOrDefault(number.Key) < number.Value)
            || translatedNumbers.Any(number => sourceNumbers.TryGetValue(number.Key, out var count) && number.Value > count);
    }

    private static Dictionary<string, int> Count(string text) =>
        DigitGroups().Matches(text)
            .GroupBy(match => match.Value)
            .ToDictionary(group => group.Key, group => group.Count());

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitGroups();
}
