namespace Tolmacz.Evaluation;

/// <summary>One text to translate during an evaluation.</summary>
internal sealed record EvaluationCase(
    string Id,
    string Category,
    TranslationDirection Direction,
    string Source,
    string? Reference = null);

/// <summary>The translation and detected problems for one evaluation case.</summary>
internal sealed record EvaluationResult(
    string Id,
    string Category,
    TranslationDirection Direction,
    string Source,
    string? Reference,
    string Translation,
    bool IsComplete,
    long Milliseconds,
    IReadOnlyList<string> Violations);
