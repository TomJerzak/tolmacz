
namespace Tolmacz.Tests;

public class TextSegmenterTests
{
    private static string[] Sentences(string text) =>
        TextSegmenter.Split(text).Where(s => s.IsTranslatable).Select(s => s.Text).ToArray();

    [Theory]
    [InlineData("")]
    [InlineData("   \n\t ")]
    [InlineData("  One. Two!\r\n\r\n  Three?  ")]
    [InlineData("— ... —")]
    public void Segments_always_rebuild_the_original_text(string text) =>
        Assert.Equal(text, string.Concat(TextSegmenter.Split(text).Select(s => s.Text)));

    [Fact]
    public void Splits_on_sentence_endings_and_line_breaks() =>
        Assert.Equal(
            ["Hello there.", "How are you?", "Fine"],
            Sentences("Hello there. How are you?\nFine"));

    [Theory]
    [InlineData("Dr. Smith is here.")]
    [InlineData("Kupiłem m.in. Chleb i mleko.")]
    [InlineData("Spotkałem J. Kowalskiego wczoraj.")]
    [InlineData("Mieszkam przy ul. Długiej 5.")]
    [InlineData("U.S. President George W. Bush arrived.")]
    public void Does_not_split_after_abbreviations(string text) =>
        Assert.Equal([text], Sentences(text));

    [Theory]
    [InlineData("No.", "I don't think so.")]
    [InlineData("I told him no.", "He left anyway.")]
    [InlineData("Ok.", "Idziemy do domu.")]
    [InlineData("The car is red.", "It was very expensive.")]
    [InlineData("Dałem im.", "Potem wyszedłem.")]
    [InlineData("So do I.", "Then we left.")]
    public void Splits_after_ordinary_words_that_resemble_abbreviations(string first, string second) =>
        Assert.Equal([first, second], Sentences($"{first} {second}"));

    [Theory]
    [InlineData("See No. 5 for details.")]
    [InlineData("Mam ok. 20 książek.")]
    public void Number_abbreviations_stay_together_before_a_number(string text) =>
        Assert.Equal([text], Sentences(text));

    [Fact]
    public void Splits_near_the_middle_and_keeps_every_character()
    {
        const string text = "one two, three four";

        Assert.True(TextSegmenter.TrySplitNearMiddle(text, out var left, out var separator, out var right));
        Assert.Equal("one two,", left);
        Assert.Equal(text, left + separator + right);
    }

    [Theory]
    [InlineData("single")]
    [InlineData("trailing ")]
    public void Cannot_split_text_without_a_space_between_words(string text) =>
        Assert.False(TextSegmenter.TrySplitNearMiddle(text, out _, out _, out _));

    [Theory]
    [InlineData("123")]
    [InlineData("12:30")]
    [InlineData("50%")]
    [InlineData("https://example.com/a/b")]
    [InlineData("www.example.com")]
    [InlineData("name@example.com")]
    public void Numbers_and_addresses_are_copied(string text) =>
        Assert.Empty(Sentences(text));

    [Fact]
    public void List_markers_are_copied_and_their_text_is_translated() =>
        Assert.Equal(["Buy milk"], Sentences("1. Buy milk"));

    [Fact]
    public void Long_sentence_is_not_cut_at_an_early_comma()
    {
        var text = "Tak, " + string.Join(' ', Enumerable.Repeat("słowo", 170));

        Assert.True(Sentences(text)[0].Length >= TextSegmenter.MaxSegmentLength / 2);
    }

    [Theory]
    [InlineData("Imię\tNazwisko\tWiek", new[] { "Imię", "Nazwisko", "Wiek" })]
    [InlineData("Kot\tśpi na kanapie.", new[] { "Kot", "śpi na kanapie." })]
    [InlineData("\tDzień dobry.", new[] { "Dzień dobry." })]
    [InlineData("Imię\t\tWiek\t", new[] { "Imię", "Wiek" })]
    public void Tabs_separate_text_and_are_kept(string text, string[] expected)
    {
        Assert.Equal(expected, Sentences(text));
        Assert.Equal(text, string.Concat(TextSegmenter.Split(text).Select(s => s.Text)));
    }

    [Fact]
    public void Does_not_split_before_a_lowercase_word() =>
        Assert.Single(Sentences("Version 2. is what we meant."));

    [Fact]
    public void Keeps_closing_quote_with_its_sentence() =>
        Assert.Equal(["\"Stop!\"", "He ran."], Sentences("\"Stop!\" He ran."));

    [Fact]
    public void Punctuation_only_segments_are_not_translated() =>
        Assert.Empty(Sentences("— ... —"));

    [Theory]
    [InlineData("word ")]
    [InlineData("x")]
    [InlineData("słowo, ")]
    public void Long_text_is_split_within_the_limit_without_losing_characters(string unit)
    {
        var text = string.Concat(Enumerable.Repeat(unit, 3000));
        var segments = TextSegmenter.Split(text);

        Assert.Equal(text, string.Concat(segments.Select(s => s.Text)));
        Assert.All(segments, s => Assert.True(s.Text.Length <= TextSegmenter.MaxSegmentLength));
    }
}
