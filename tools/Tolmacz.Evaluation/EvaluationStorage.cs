using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tolmacz.Evaluation;

/// <summary>Reads and writes evaluation data as one JSON object per line.</summary>
internal static class EvaluationStorage
{
    public const string ResultsFile = "results.jsonl";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public static IReadOnlyList<T> ReadLines<T>(string path) =>
        File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<T>(line, Options)
                ?? throw new InvalidDataException($"Invalid line in {path}."))
            .ToList();

    public static string ToLine<T>(T value) => JsonSerializer.Serialize(value, Options);
}
