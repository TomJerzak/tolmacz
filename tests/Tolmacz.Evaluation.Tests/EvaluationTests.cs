namespace Tolmacz.Evaluation.Tests;

public class EvaluationTests
{
    private static EvaluationResult Result(string id, string translation, params string[] violations) =>
        new(id, "test", TranslationDirection.PolishToEnglish, "Numer 123", null, translation, true, 1, violations);

    [Theory]
    [InlineData("Faktura 123.", "Invoice.", "number_missing")]
    [InlineData("Faktura.", "Invoice 123.", "number_added")]
    [InlineData("Numer 1, numer 2.", "Number 1, number 1, number 2.", "number_duplicated")]
    [InlineData("Zobacz https://example.com/a.", "See the page.", "address_changed")]
    [InlineData("Kolega 李明 przyszedł.", "A colleague came.", "protected_text_lost")]
    [InlineData("Imię\tWiek", "Name Age", "layout_changed")]
    [InlineData("Tak", "yes yes yes yes", "repetition_added")]
    [InlineData("To jest dość długie zdanie do przetłumaczenia.", "Short.", "suspicious_length")]
    [InlineData("Dom", "House (ang.)", "parenthetical_added")]
    [InlineData("This sentence stayed in English.", "This sentence stayed in English.", "text_not_translated")]
    [InlineData("Tekst", "", "empty_output")]
    public void Finds_translation_problems(string source, string translation, string expected) =>
        Assert.Contains(expected, TranslationChecks.Find(source, translation, true));

    [Fact]
    public void Accepts_a_faithful_translation() =>
        Assert.Empty(TranslationChecks.Find(
            "Faktura 123 jest na stronie https://example.com/a.\nDziękuję.",
            "Invoice 123 is at https://example.com/a.\nThank you.",
            true));

    [Fact]
    public void Number_format_changes_are_allowed() =>
        Assert.DoesNotContain("number_missing", TranslationChecks.Find("Cena 1,250", "Price 1 250", true));

    [Fact]
    public void Reports_incomplete_translation() =>
        Assert.Contains("reported_incomplete", TranslationChecks.Find("Tak", "Yes", false));

    [Fact]
    public void Generated_cases_are_repeatable_and_unique()
    {
        var first = EdgeCaseGenerator.Create();
        var second = EdgeCaseGenerator.Create();

        Assert.Equal(first, second);
        Assert.Equal(first.Count, first.Select(item => item.Id).Distinct().Count());
        Assert.Contains(first, item => item.Direction == TranslationDirection.PolishToEnglish);
        Assert.Contains(first, item => item.Direction == TranslationDirection.EnglishToPolish);
    }

    [Fact]
    public void Comparison_reports_changes_and_problems()
    {
        var comparison = EvaluationComparer.Compare(
            [Result("a", "Number"), Result("b", "Number 123"), Result("c", "Number 123")],
            [Result("a", "Number\t123"), Result("b", "Number 123")]);

        Assert.Equal(1, comparison.ChangedTranslations);
        Assert.Equal("layout_changed", Assert.Single(comparison.NewViolations).Violation);
        Assert.Equal("number_missing", Assert.Single(comparison.FixedViolations).Violation);
        Assert.Equal("c", Assert.Single(comparison.MissingCases));
    }

    [Fact]
    public void Results_survive_a_save_and_load()
    {
        var path = Path.GetTempFileName();
        try
        {
            var result = Result("a", "Zażółć 😀", "layout_changed");
            File.WriteAllText(path, EvaluationStorage.ToLine(result) + "\n");

            var loaded = Assert.Single(EvaluationStorage.ReadLines<EvaluationResult>(path));

            Assert.Equal(result.Translation, loaded.Translation);
            Assert.Equal(result.Violations, loaded.Violations);
            Assert.Equal(result.Direction, loaded.Direction);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
