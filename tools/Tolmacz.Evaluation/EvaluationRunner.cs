using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Tolmacz.Evaluation;

/// <summary>Translates evaluation cases and writes their results and summary.</summary>
internal static class EvaluationRunner
{
    private static readonly TimeSpan CaseTimeout = TimeSpan.FromMinutes(3);

    public static async Task RunAsync(string modelDirectory, IReadOnlyList<EvaluationCase> cases, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        using var translator = new MarianOnnxTranslator(modelDirectory);
        await translator.LoadAsync();

        var results = new List<EvaluationResult>(cases.Count);
        await using var writer = new StreamWriter(Path.Combine(outputDirectory, EvaluationStorage.ResultsFile), false, new UTF8Encoding(false));
        foreach (var evaluationCase in cases)
        {
            var result = await TranslateAsync(translator, evaluationCase);
            results.Add(result);
            await writer.WriteLineAsync(EvaluationStorage.ToLine(result));
            if (results.Count % 100 == 0)
            {
                await writer.FlushAsync();
                Console.WriteLine($"{results.Count}/{cases.Count}");
            }
        }

        var summary = CreateSummary(results);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "summary.md"), summary);
        Console.WriteLine(summary);
    }

    internal static string CreateSummary(IReadOnlyList<EvaluationResult> results)
    {
        var text = new StringBuilder("# Evaluation summary\n\n");
        Append(text, $"Translations: {results.Count}\n\n");
        Append(text, $"Average time: {(results.Count == 0 ? 0 : results.Average(result => result.Milliseconds)):F0} ms\n\n");
        text.Append("| Category | Translations | With problems |\n| --- | ---: | ---: |\n");
        foreach (var group in results.GroupBy(result => result.Category).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            Append(text, $"| {group.Key} | {group.Count()} | {group.Count(result => result.Violations.Count > 0)} |\n");
        }

        text.Append("\n| Problem | Count |\n| --- | ---: |\n");
        foreach (var group in results.SelectMany(result => result.Violations).GroupBy(name => name)
            .OrderByDescending(group => group.Count()).ThenBy(group => group.Key, StringComparer.Ordinal))
        {
            Append(text, $"| {group.Key} | {group.Count()} |\n");
        }

        return text.ToString();
    }

    private static async Task<EvaluationResult> TranslateAsync(MarianOnnxTranslator translator, EvaluationCase evaluationCase)
    {
        var stopwatch = Stopwatch.StartNew();
        using var timeout = new CancellationTokenSource(CaseTimeout);
        try
        {
            var translation = await translator.TranslateAsync(
                evaluationCase.Source,
                evaluationCase.Direction,
                cancellationToken: timeout.Token);
            return CreateResult(
                evaluationCase,
                translation.Text,
                translation.IsComplete,
                stopwatch.ElapsedMilliseconds,
                TranslationChecks.Find(evaluationCase.Source, translation.Text, translation.IsComplete));
        }
        catch (OperationCanceledException)
        {
            return CreateResult(evaluationCase, string.Empty, false, stopwatch.ElapsedMilliseconds, ["timeout"]);
        }
        catch (Exception exception)
        {
            return CreateResult(evaluationCase, exception.Message, false, stopwatch.ElapsedMilliseconds, ["translation_error"]);
        }
    }

    private static EvaluationResult CreateResult(
        EvaluationCase evaluationCase,
        string translation,
        bool isComplete,
        long milliseconds,
        IReadOnlyList<string> violations) =>
        new(
            evaluationCase.Id,
            evaluationCase.Category,
            evaluationCase.Direction,
            evaluationCase.Source,
            evaluationCase.Reference,
            translation,
            isComplete,
            milliseconds,
            violations);

    private static void Append(StringBuilder text, FormattableString value) =>
        text.Append(value.ToString(CultureInfo.InvariantCulture));
}
