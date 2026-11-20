using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace Tolmacz.Tests;

public sealed class ModelFixture : IDisposable
{
    public ModelFixture()
    {
        var directory = new DirectoryInfo(GetSourceDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tolmacz.sln")))
        {
            directory = directory.Parent;
        }

        ModelDirectory = Path.Combine(directory!.FullName, "models", "bidi-eng-pol");
        Tolmacz = new MarianOnnxTranslator(ModelDirectory);
    }

    // The source location still works when build output is outside the repository.
    private static string GetSourceDirectory([CallerFilePath] string sourceFile = "") =>
        Path.GetDirectoryName(sourceFile)!;

    public string ModelDirectory { get; }
    public MarianOnnxTranslator Tolmacz { get; }

    public void Dispose() => Tolmacz.Dispose();
}

public class ModelTests(ModelFixture model, ITestOutputHelper output) : IClassFixture<ModelFixture>
{
    // Expected ids were produced by the original Hugging Face MarianTokenizer.
    public static TheoryData<TranslationDirection, string, long[]> TokenizerCases => new()
    {
        {
            TranslationDirection.EnglishToPolish,
            "The weather is beautiful today, so we are going for a walk.",
            [5, 42, 385, 28, 1400, 941, 6, 124, 77, 54, 525, 27, 16, 1752, 7, 2]
        },
        {
            TranslationDirection.PolishToEnglish,
            "Zażółć gęślą jaźń, proszę — to ważne!",
            [4, 276, 18935, 111, 718, 102, 699, 11010, 463, 2444, 1077, 6, 2430, 267, 10, 2113, 50, 2]
        },
        {
            TranslationDirection.EnglishToPolish,
            "Dr. Smith paid $1,250.50 in 2024.",
            [5, 1201, 7, 8876, 4311, 16313, 6, 10304, 7, 1465, 14, 394, 1460, 7, 2]
        },
    };

    private async Task<TranslationResult> TranslateAsync(string text, TranslationDirection direction)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await model.Tolmacz.TranslateAsync(text, direction);
        output.WriteLine($"{stopwatch.ElapsedMilliseconds} ms: {result.Text}");
        return result;
    }

    [Theory]
    [MemberData(nameof(TokenizerCases))]
    public void Tokenizer_matches_the_reference_implementation(
        TranslationDirection direction, string text, long[] expected) =>
        Assert.Equal(expected, new MarianTokenizer(model.ModelDirectory).Encode(text, direction));

    [Theory]
    [InlineData(TranslationDirection.EnglishToPolish,
        "The weather is beautiful today, so we are going for a walk.",
        "Pogoda jest dziś piękna, więc idziemy na spacer.")]
    [InlineData(TranslationDirection.EnglishToPolish,
        "Dr. Smith paid $1,250.50 in 2024.",
        "Dr Smith zapłacił $1,250.50 w 2024.")]
    public async Task Translation_matches_the_reference_model(
        TranslationDirection direction, string source, string expected)
    {
        var result = await TranslateAsync(source, direction);
        Assert.Equal(expected, result.Text);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task Polish_is_translated_to_english_and_layout_is_kept()
    {
        var progress = new List<double>();
        var result = await model.Tolmacz.TranslateAsync(
            "Dzień dobry.\n\nGdzie jest najbliższy dworzec kolejowy?",
            TranslationDirection.PolishToEnglish,
            new SynchronousProgress(progress.Add));

        output.WriteLine(result.Text);
        var lines = result.Text.Split("\n\n");
        Assert.Equal(2, lines.Length);
        Assert.Equal("Good morning.", lines[0]);
        Assert.Contains("station", lines[1], StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, progress[^1]);
    }

    [Theory]
    [InlineData("Baden-Baden", "Baden-Baden")]
    [InlineData("Bora-Bora", "Bora Bora")]
    [InlineData("Sing-Sing", "Sing-Sing")]
    [InlineData("Warszawa", "Warsaw")]
    [InlineData("Dzień dobry", "Good morning")]
    [InlineData("Jan Kowalski", "Jan Kowalski")]
    [InlineData("Zapisz", "Save")]
    [InlineData("Anuluj", "Cancel")]
    [InlineData("Usuń", "Delete")]
    [InlineData("Zamknij", "Close")]
    [InlineData("Nazwisko", "Last name")]
    public async Task Short_polish_phrases_are_translated_cleanly(string source, string expected)
    {
        var result = await TranslateAsync(source, TranslationDirection.PolishToEnglish);
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public async Task Search_keeps_words_that_an_early_stop_lost()
    {
        var result = await TranslateAsync(
            "She told me that she would come back as soon as she finished her work at the hospital.",
            TranslationDirection.EnglishToPolish);

        Assert.Contains("Powiedziała mi", result.Text);
    }

    [Fact]
    public async Task Every_sentence_is_translated_to_its_end()
    {
        var result = await TranslateAsync(
            "The car is red. It was very expensive.",
            TranslationDirection.EnglishToPolish);

        Assert.EndsWith(".", result.Text);
        Assert.Contains("czerwony.", result.Text);
    }

    [Theory]
    [InlineData(TranslationDirection.PolishToEnglish, "Imię\tNazwisko\tWiek\n1\tJan\t30")]
    [InlineData(TranslationDirection.EnglishToPolish, "Name\tSurname\tAge\n1\tJohn\t30")]
    public async Task Tables_keep_their_columns_and_rows(TranslationDirection direction, string source)
    {
        var result = await TranslateAsync(source, direction);

        Assert.Equal(source.Count(c => c == '\t'), result.Text.Count(c => c == '\t'));
        Assert.Equal(source.Count(c => c == '\n'), result.Text.Count(c => c == '\n'));
        Assert.All(result.Text.Split('\n'), row => Assert.Equal(3, row.Split('\t').Length));
        Assert.All(result.Text.Split('\n', '\t'), cell => Assert.NotEmpty(cell));
    }

    [Fact]
    public async Task Long_number_lists_are_kept_exactly()
    {
        var numbers = string.Join(", ", Enumerable.Range(1, 240));

        var result = await TranslateAsync(numbers, TranslationDirection.EnglishToPolish);

        Assert.True(result.IsComplete);
        Assert.Equal(numbers, result.Text);
    }

    [Fact]
    public async Task Numbered_text_keeps_its_numbers_or_reports_a_problem()
    {
        var source = string.Join(", ", Enumerable.Range(1, 40).Select(number => $"pan Kowalski numer {number}"));

        var result = await TranslateAsync(source, TranslationDirection.PolishToEnglish);

        var numbers = Regex.Matches(result.Text, @"\d+")
            .Select(match => int.Parse(match.Value, CultureInfo.InvariantCulture))
            .ToHashSet();
        Assert.True(!result.IsComplete || Enumerable.Range(1, 40).All(numbers.Contains));
    }

    [Theory]
    [InlineData(TranslationDirection.PolishToEnglish, 8)]
    [InlineData(TranslationDirection.PolishToEnglish, 16)]
    [InlineData(TranslationDirection.EnglishToPolish, 16)]
    public async Task Numbered_lists_keep_each_number_once(TranslationDirection direction, int size)
    {
        var item = direction == TranslationDirection.PolishToEnglish ? "pan Kowalski numer" : "customer number";
        var source = string.Join(", ", Enumerable.Range(1, size).Select(number => $"{item} {number}"));

        var result = await TranslateAsync(source, direction);

        var numbers = Regex.Matches(result.Text, @"\d+").Select(match => match.Value).ToArray();
        Assert.Equal(Enumerable.Range(1, size).Select(number => number.ToString(CultureInfo.InvariantCulture)), numbers);
        Assert.True(result.IsComplete);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("12:30")]
    [InlineData("50%")]
    [InlineData("https://example.com/a/b")]
    public async Task Numbers_and_addresses_are_not_changed(string text)
    {
        var result = await TranslateAsync(text, TranslationDirection.EnglishToPolish);
        Assert.Equal(text, result.Text);
    }

    [Fact]
    public async Task List_markers_get_no_invented_text()
    {
        var result = await TranslateAsync("1. Buy milk", TranslationDirection.EnglishToPolish);

        Assert.StartsWith("1. ", result.Text);
        Assert.DoesNotContain("przedmiocie", result.Text);
    }

    [Theory]
    [InlineData(TranslationDirection.PolishToEnglish, "Mój kolega 李明 przyszedł.", "李明")]
    [InlineData(TranslationDirection.EnglishToPolish, "I love 😀 a lot.", "😀")]
    public async Task Words_outside_the_vocabulary_are_kept(
        TranslationDirection direction, string source, string kept)
    {
        var result = await TranslateAsync(source, direction);
        Assert.Contains(kept, result.Text);
    }

    [Fact]
    public async Task Long_unbroken_text_is_copied()
    {
        var code = string.Concat(Enumerable.Repeat("a1b2c3d4", 40));

        var result = await TranslateAsync($"The code is {code} today.", TranslationDirection.EnglishToPolish);

        Assert.Contains(code, result.Text);
        Assert.DoesNotContain("ustęp", result.Text);
    }

    // Only phrases with the same result on Apple and x64 processors. The int8 model can choose a
    // different word at a near-tie, as in "Close": "Zamknij" on a Mac but "Zamknąć się" on Windows.
    [Theory]
    [InlineData("Hush", "Cicho")]
    [InlineData("Thank you", "Dziękuję")]
    [InlineData("Good morning", "Dzień dobry")]
    [InlineData("Save", "Zapisz")]
    [InlineData("Baden-Baden", "Baden-Baden")]
    [InlineData("Warszawa", "Warszawa")]
    [InlineData("Warsaw", "Warszawa")]
    [InlineData("Yes", "Tak")]
    [InlineData("House", "Dom")]
    [InlineData("Car", "Samochód")]
    [InlineData("Window", "Okno")]
    [InlineData("Copy", "Kopiuj")]
    [InlineData("The cat", "Kot")]
    public async Task Short_english_phrases_are_translated_cleanly(string source, string expected)
    {
        var result = await TranslateAsync(source, TranslationDirection.EnglishToPolish);
        Assert.Equal(expected, result.Text);
    }

    [Theory]
    [InlineData(TranslationDirection.PolishToEnglish, "Spis treści . . . . . . . . 5")]
    [InlineData(TranslationDirection.EnglishToPolish, "Table of contents . . . . . . . . 5")]
    public async Task Dotted_contents_lines_are_not_reported_incomplete(TranslationDirection direction, string source)
    {
        var result = await TranslateAsync(source, direction);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task Inner_period_abbreviations_do_not_split_the_sentence()
    {
        var result = await TranslateAsync("U.S. President George W. Bush welcomed the announcement.", TranslationDirection.EnglishToPolish);

        Assert.DoesNotContain("(ang.)", result.Text);
        Assert.Contains("Bush", result.Text);
    }

    [Theory]
    [InlineData(TranslationDirection.EnglishToPolish, "The park covers 19,500 km².")]
    [InlineData(TranslationDirection.PolishToEnglish, "Park ma powierzchnię 19 500 km².")]
    public async Task Unit_exponents_are_kept(TranslationDirection direction, string source)
    {
        var result = await TranslateAsync(source, direction);
        Assert.Contains("km²", result.Text);
    }

    [Fact]
    public async Task Short_phrases_keep_abbreviations()
    {
        var result = await TranslateAsync("np. kot", TranslationDirection.PolishToEnglish);
        Assert.Contains("e.g.", result.Text);
    }

    [Fact]
    public async Task Translation_does_not_repeat_itself_in_brackets()
    {
        var result = await TranslateAsync("Bread", TranslationDirection.EnglishToPolish);
        Assert.DoesNotContain("(", result.Text);
    }

    [Fact]
    public async Task Temporary_period_does_not_remove_an_abbreviation_period()
    {
        var result = await TranslateAsync("itd", TranslationDirection.PolishToEnglish);
        Assert.Equal("etc.", result.Text);
    }

    [Fact]
    public async Task Repeated_words_are_not_reported_complete_when_cut_short()
    {
        var source = string.Join(' ', Enumerable.Repeat("bardzo", 100)) + " dobrze";

        var result = await TranslateAsync(source, TranslationDirection.PolishToEnglish);

        Assert.False(result.IsComplete);
    }

    [Fact]
    public async Task Cancellation_stops_translation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            model.Tolmacz.TranslateAsync("Hello.", TranslationDirection.EnglishToPolish, null, cancellation.Token));
    }

    private sealed class SynchronousProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
