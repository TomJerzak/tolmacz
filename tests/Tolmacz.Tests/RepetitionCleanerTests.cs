using System.Globalization;

namespace Tolmacz.Tests;

public class RepetitionCleanerTests
{
    [Theory]
    [InlineData("Dzień dobry.", "Good morning. Good morning.", "Good morning.")]
    [InlineData("Dzień dobry.", "Good morning. Good morning", "Good morning.")]
    [InlineData("Tak", "Yes, yes", "Yes")]
    [InlineData("Tak.", "Yes, yes.", "Yes.")]
    [InlineData("Tak.", "Yeah, yeah, yeah, yeah.", "Yeah.")]
    [InlineData("Thank you.", "Dziękuję. Dziękuję.", "Dziękuję.")]
    public void Removes_repeats_the_source_does_not_have(string source, string translation, string expected) =>
        Assert.Equal(expected, RepetitionCleaner.Clean(source, translation));

    [Fact]
    public void Keeps_as_many_repeats_as_the_source_has() =>
        Assert.Equal("Well, well", RepetitionCleaner.Clean("No no", "Well, well, well, well, well, well"));

    [Theory]
    [InlineData("Cat cat", true)]
    [InlineData("Cisza i cisza", true)]
    [InlineData("Otwórz plik", false)]
    public void Detects_repetitive_text(string text, bool expected) =>
        Assert.Equal(expected, RepetitionCleaner.LooksRepetitive(text));

    [Theory]
    [InlineData("The cat", 1)]
    [InlineData("A big house", 2)]
    [InlineData("Save as", 2)]
    [InlineData("the", 0)]
    [InlineData("Baden-Baden", 1)]
    public void Counts_words_without_english_articles(string text, int expected) =>
        Assert.Equal(expected, RepetitionCleaner.CountWordsWithoutArticles(text));

    [Theory]
    [InlineData("Samochód samochodowy", true)]
    [InlineData("Deszcz deszczowy", true)]
    [InlineData("Duży dom", false)]
    [InlineData("Dom", false)]
    [InlineData("Na nas", false)]
    public void Detects_neighbouring_words_with_the_same_stem(string translation, bool expected) =>
        Assert.Equal(expected, RepetitionCleaner.RepeatsWordStem(translation));

    [Theory]
    [InlineData("Baden-Baden", "Badenii-Baden", true)]
    [InlineData("Baden-Baden", "Baden-Baden", false)]
    [InlineData("Bora-Bora", "Bora Bora", false)]
    [InlineData("Open file", "Otwórz plik", false)]
    public void Detects_a_source_repeat_lost_in_translation(string source, string translation, bool expected) =>
        Assert.Equal(expected, RepetitionCleaner.LostSourceRepeat(source, translation));

    [Theory]
    [InlineData("Bread", "Chleb (chleb)", "Chleb")]
    [InlineData("Surname", "Nazwisko (nazwisko).", "Nazwisko.")]
    [InlineData("The cat", "Kot (kot)", "Kot")]
    [InlineData("Warsaw", "Warszawa (stolica)", "Warszawa (stolica)")]
    [InlineData("Bread (bread)", "Chleb (chleb)", "Chleb (chleb)")]
    [InlineData("Book", "Książka (książki)", "Książka (książki)")]
    public void Removes_only_a_bracket_that_repeats_the_translation(string source, string translation, string expected) =>
        Assert.Equal(expected, RepetitionCleaner.RemoveEchoedParenthesis(source, translation));

    [Theory]
    [InlineData("Cat", "The cat", true)]
    [InlineData("Save", "Write it down", true)]
    [InlineData("Until tomorrow", "I'll see you tomorrow", true)]
    [InlineData("Name", "Last name", false)]
    [InlineData("cat", "e.g. cat", false)]
    [InlineData("Refresh", "Freshen up", false)]
    public void Accepts_only_article_or_pronoun_additions(string shorter, string longer, bool expected) =>
        Assert.Equal(expected, RepetitionCleaner.OnlyAddsFillerOrPronoun(shorter, longer));

    [Theory]
    [InlineData("Thank you very", "Thank you very much", true)]
    [InlineData("App", "Approve it", true)]
    [InlineData("Log", "Log out", true)]
    [InlineData("Cancel", "Cancel it", false)]
    [InlineData("Save", "Write it down", false)]
    [InlineData("Cat", "The cat", false)]
    public void Detects_translations_cut_short(string shorter, string longer, bool expected) =>
        Assert.Equal(expected, RepetitionCleaner.IsCutShort(shorter, longer));

    [Theory]
    [InlineData("Dobranoc", "Good Night Night", true)]
    [InlineData("Jan Kowalski", "Jan Ko", true)]
    [InlineData("Teraz", "Now Now", true)]
    [InlineData("Zapisz", "Save", false)]
    [InlineData("Jan Kowalski", "Jan Kowalski", false)]
    [InlineData("Kraków", "Krakow", false)]
    public void Detects_damaged_short_translations(string source, string translation, bool expected) =>
        Assert.Equal(expected, RepetitionCleaner.LooksDamaged(source, translation));

    [Theory]
    [InlineData("very very very very very very very very good", true)]
    [InlineData("very very very good", false)]
    [InlineData("Spis treści . . . . . . . . . . 5", false)]
    [InlineData("Table of contents ... ... ... ... ... ... ... ... 5", false)]
    [InlineData("very . very . very . very . very . very . very . very", true)]
    public void Detects_long_word_runs(string text, bool expected) =>
        Assert.Equal(expected, RepetitionCleaner.HasLongWordRun(text));

    [Fact]
    public void Result_does_not_depend_on_the_computer_language()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // In Turkish, "I" and "i" are different letters.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal("Hi", RepetitionCleaner.Clean("Cześć", "Hi HI"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("Do widzenia", "Bye bye")]
    [InlineData("bardzo bardzo bardzo bardzo bardzo bardzo dobrze", "very very very very")]
    [InlineData("Tak, tak.", "Yes, yes.")]
    [InlineData("Bora-Bora", "Bora Bora")]
    [InlineData("Baden-Baden", "Baden Baden")]
    [InlineData("Nie wiem.", "I don't know.")]
    [InlineData("Pies je, kot je.", "The dog eats, the cat eats.")]
    public void Keeps_other_translations(string source, string translation) =>
        Assert.Equal(translation, RepetitionCleaner.Clean(source, translation));
}
