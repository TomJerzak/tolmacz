using System.Globalization;
using System.Text;

namespace Tolmacz.Evaluation;

/// <summary>The differences between two evaluation runs.</summary>
internal sealed record EvaluationComparison(
    int ChangedTranslations,
    IReadOnlyList<ViolationChange> NewViolations,
    IReadOnlyList<ViolationChange> FixedViolations,
    IReadOnlyList<string> MissingCases,
    IReadOnlyList<EvaluationResultChange> Changes);

/// <summary>A problem that appeared or disappeared for one case.</summary>
internal sealed record ViolationChange(string Id, string Category, string Violation, string Source, string Translation);

/// <summary>A translation that differs between two runs.</summary>
internal sealed record EvaluationResultChange(string Id, string Category, string Source, string Before, string After);

/// <summary>Compares a new evaluation with an earlier one.</summary>
internal static class EvaluationComparer
{
    public static EvaluationComparison Compare(IReadOnlyList<EvaluationResult> before, IReadOnlyList<EvaluationResult> after)
    {
        var afterById = after.ToDictionary(result => result.Id, StringComparer.Ordinal);
        var newViolations = new List<ViolationChange>();
        var fixedViolations = new List<ViolationChange>();
        var changes = new List<EvaluationResultChange>();
        var missing = new List<string>();

        foreach (var oldResult in before)
        {
            if (!afterById.TryGetValue(oldResult.Id, out var newResult))
            {
                missing.Add(oldResult.Id);
                continue;
            }

            if (oldResult.Translation != newResult.Translation)
            {
                changes.Add(new EvaluationResultChange(
                    oldResult.Id,
                    oldResult.Category,
                    oldResult.Source,
                    oldResult.Translation,
                    newResult.Translation));
            }

            var oldViolations = CurrentViolations(oldResult);
            var currentViolations = CurrentViolations(newResult);
            newViolations.AddRange(currentViolations.Except(oldViolations)
                .Select(name => new ViolationChange(newResult.Id, newResult.Category, name, newResult.Source, newResult.Translation)));
            fixedViolations.AddRange(oldViolations.Except(currentViolations)
                .Select(name => new ViolationChange(oldResult.Id, oldResult.Category, name, oldResult.Source, oldResult.Translation)));
        }

        return new EvaluationComparison(changes.Count, newViolations, fixedViolations, missing, changes);
    }

    // Both runs are checked with the current rules, so a new check does not look like a regression.
    private static IReadOnlyList<string> CurrentViolations(EvaluationResult result) =>
        result.Violations.Any(name => name is "timeout" or "translation_error")
            ? result.Violations
            : TranslationChecks.Find(result.Source, result.Translation, result.IsComplete);

    public static string CreateReport(EvaluationComparison comparison)
    {
        var text = new StringBuilder("# Evaluation comparison\n\n");
        Append(text, $"Changed translations: {comparison.ChangedTranslations}\n\n");
        Append(text, $"New problems: {comparison.NewViolations.Count}\n\n");
        Append(text, $"Fixed problems: {comparison.FixedViolations.Count}\n\n");
        Append(text, $"Missing cases: {comparison.MissingCases.Count}\n\n");
        AppendViolations(text, "New problems", comparison.NewViolations);
        AppendViolations(text, "Fixed problems", comparison.FixedViolations);
        return text.ToString();
    }

    private static void AppendViolations(StringBuilder text, string title, IReadOnlyList<ViolationChange> violations)
    {
        Append(text, $"## {title}\n\n");
        foreach (var violation in violations)
        {
            Append(text, $"- `{violation.Violation}` in `{violation.Id}`: {OneLine(violation.Source)} => {OneLine(violation.Translation)}\n");
        }

        text.Append('\n');
    }

    private static string OneLine(string text) =>
        text.ReplaceLineEndings(" ").Replace('\t', ' ');

    private static void Append(StringBuilder text, FormattableString value) =>
        text.Append(value.ToString(CultureInfo.InvariantCulture));
}
