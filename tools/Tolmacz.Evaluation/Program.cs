using Tolmacz;
using Tolmacz.Evaluation;

const string Usage = """
    Usage:
      run --output <folder> [--model <folder>] [--data <file.jsonl>] [--limit <count>]
      compare --before <folder> --after <folder> [--output <folder>]
    """;

try
{
    return args.FirstOrDefault() switch
    {
        "run" => await RunAsync(ParseOptions(args)),
        "compare" => Compare(ParseOptions(args)),
        _ => ShowUsage(),
    };
}
catch (Exception exception) when (exception is ArgumentException or IOException or InvalidDataException or TranslationModelException)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}

int ShowUsage()
{
    Console.Error.WriteLine(Usage);
    return 2;
}

static Dictionary<string, string> ParseOptions(string[] arguments)
{
    if (arguments.Length % 2 == 0)
    {
        throw new ArgumentException("Every option needs a value.");
    }

    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var index = 1; index < arguments.Length; index += 2)
    {
        options[arguments[index]] = arguments[index + 1];
    }

    return options;
}

static string Required(Dictionary<string, string> options, string name) =>
    options.TryGetValue(name, out var value) ? value : throw new ArgumentException($"Missing option {name}.");

static async Task<int> RunAsync(Dictionary<string, string> options)
{
    var cases = new List<EvaluationCase>(EdgeCaseGenerator.Create());
    if (options.TryGetValue("--data", out var dataFile))
    {
        cases.AddRange(EvaluationStorage.ReadLines<EvaluationCase>(dataFile));
    }

    var selected = options.TryGetValue("--limit", out var limit)
        ? cases.Where((_, index) => index % Math.Max(1, cases.Count / int.Parse(limit, System.Globalization.CultureInfo.InvariantCulture)) == 0).ToList()
        : cases;
    Console.WriteLine($"Evaluating {selected.Count} translations.");
    await EvaluationRunner.RunAsync(
        options.GetValueOrDefault("--model", Path.Combine("models", "bidi-eng-pol")),
        selected,
        Required(options, "--output"));
    return 0;
}

static int Compare(Dictionary<string, string> options)
{
    var before = EvaluationStorage.ReadLines<EvaluationResult>(Path.Combine(Required(options, "--before"), EvaluationStorage.ResultsFile));
    var after = EvaluationStorage.ReadLines<EvaluationResult>(Path.Combine(Required(options, "--after"), EvaluationStorage.ResultsFile));
    var comparison = EvaluationComparer.Compare(before, after);
    var report = EvaluationComparer.CreateReport(comparison);
    Console.WriteLine(report);

    if (options.TryGetValue("--output", out var output))
    {
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "comparison.md"), report);
        File.WriteAllLines(Path.Combine(output, "changed.jsonl"), comparison.Changes.Select(EvaluationStorage.ToLine));
    }

    return comparison.NewViolations.Count > 0 || comparison.MissingCases.Count > 0 ? 1 : 0;
}
