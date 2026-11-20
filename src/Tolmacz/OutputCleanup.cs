using System.Text.RegularExpressions;

namespace Tolmacz;

/// <summary>Repairs small, predictable changes the model makes to its output.</summary>
internal static partial class OutputCleanup
{
    /// <summary>
    /// Removes the dictionary marker "(ang.)" that the model adds after names in Polish output,
    /// as in "Marta Suplicy (ang.)", unless the source contains it.
    /// </summary>
    public static string RemoveEnglishMarker(string source, string translation) =>
        source.Contains("(ang", StringComparison.OrdinalIgnoreCase)
            ? translation
            : EnglishMarker().Replace(MarkerBetweenPeriods().Replace(translation, string.Empty), string.Empty);

    /// <summary>
    /// Removes the Wikipedia label "Kategoria:" that the model can put before a short Polish
    /// translation, as in "Kategoria: Wiek" for "Age", unless the source mentions a category.
    /// </summary>
    public static string RemoveCategoryLabel(string source, string translation)
    {
        var match = CategoryLabel().Match(translation);
        return match.Success
            && match.Length < translation.Length
            && !source.Contains("categor", StringComparison.OrdinalIgnoreCase)
                ? translation[match.Length..]
                : translation;
    }

    /// <summary>
    /// Removes a dialogue dash that the model can put before a translation, as in "- Ser" for
    /// "Cheese", unless the source starts with a dash too. A dash that opens quoted speech is kept
    /// when a second dash follows, as in "- Teraz ważne jest… - powiedział rzecznik", because
    /// removing only the first would leave the second without its pair.
    /// </summary>
    public static string RemoveLeadingDash(string source, string translation)
    {
        var match = LeadingDash().Match(translation);
        if (!match.Success || match.Length == translation.Length || LeadingDash().IsMatch(source.TrimStart()))
        {
            return translation;
        }

        var rest = translation[match.Length..];
        return DialogueDash().IsMatch(rest) ? translation : rest;
    }

    /// <summary>
    /// Restores unit exponents the model writes as plain digits, as in "km2" for "km²",
    /// but only for units that appear with an exponent in the source.
    /// </summary>
    public static string RestoreUnitExponents(string source, string translation)
    {
        foreach (var unit in UnitWithExponent().Matches(source).Select(match => match.Value).Distinct(StringComparer.Ordinal))
        {
            var plain = unit[..^1] + (unit[^1] == '²' ? "2" : "3");
            translation = Regex.Replace(translation, $@"\b{Regex.Escape(plain)}\b", unit);
        }

        return translation;
    }

    [GeneratedRegex(@"\s*\(ang\.\)")]
    private static partial Regex EnglishMarker();

    // In "U.S.A. (ang.)." removing only the marker would leave "U.S.A..".
    [GeneratedRegex(@"(?<=\.)\s*\(ang\.\)\.")]
    private static partial Regex MarkerBetweenPeriods();

    // A dash followed by a space, so negative numbers such as "-5" are kept.
    [GeneratedRegex(@"^[-–—]\s+")]
    private static partial Regex LeadingDash();

    // A dash between spaces, as in "w szoku — stwierdziła", not a hyphen inside a word.
    [GeneratedRegex(@"\s[-–—]\s")]
    private static partial Regex DialogueDash();

    [GeneratedRegex(@"^Kategoria:\s*")]
    private static partial Regex CategoryLabel();

    [GeneratedRegex(@"\b[A-Za-z]{1,3}[²³]")]
    private static partial Regex UnitWithExponent();
}
