using System.Security.Cryptography;
using System.Text.Json;

namespace Tolmacz;

/// <summary>Describes and checks the model files required by the translator.</summary>
public static class TranslationModel
{
    /// <summary>The identifier of the supported model.</summary>
    public const string Id = "bidi-eng-pol";

    /// <summary>The supported version of the model files.</summary>
    public const string Version = "1";

    /// <summary>The name of the model release to download.</summary>
    public const string ReleaseName = $"{Id}-v{Version}";

    /// <summary>The web page where the model release can be downloaded.</summary>
    public const string ReleaseUrl = $"https://github.com/TomJerzak/tolmacz-model/releases/tag/{ReleaseName}";

    /// <summary>The file in the model folder that lists the expected model files.</summary>
    public const string ManifestFileName = "model.json";

    internal const string EncoderFile = "encoder_model.int8.onnx";
    internal const string DecoderFile = "decoder_model_merged.int8.onnx";
    internal const string TokenizerFile = "source.spm";
    internal const string VocabularyFile = "vocab.json";

    private static readonly string[] RequiredFiles = [EncoderFile, DecoderFile, TokenizerFile, VocabularyFile];

    /// <summary>Quickly checks the model version and the presence and size of every file.</summary>
    /// <param name="modelDirectory">The folder containing the extracted model release.</param>
    /// <exception cref="TranslationModelException">The model is missing or incompatible.</exception>
    public static void Validate(string modelDirectory)
    {
        var problems = new List<string>();
        foreach (var file in ReadManifest(modelDirectory))
        {
            var path = Path.Combine(modelDirectory, file.Name);
            if (!File.Exists(path))
            {
                problems.Add($"{file.Name} is missing");
            }
            else if (new FileInfo(path).Length != file.Size)
            {
                problems.Add($"{file.Name} has the wrong size");
            }
        }

        ThrowIfAny(modelDirectory, problems);
    }

    /// <summary>
    /// Compares every model file with its SHA-256 checksum. This reads all files, so use it
    /// after downloading a model and not every time the translator starts.
    /// </summary>
    /// <param name="modelDirectory">The folder containing the extracted model release.</param>
    /// <param name="cancellationToken">Stops the check.</param>
    /// <exception cref="TranslationModelException">The model is missing, incompatible or damaged.</exception>
    public static async Task VerifyChecksumsAsync(string modelDirectory, CancellationToken cancellationToken = default)
    {
        Validate(modelDirectory);

        var problems = new List<string>();
        foreach (var file in ReadManifest(modelDirectory))
        {
            await using var stream = File.OpenRead(Path.Combine(modelDirectory, file.Name));
            var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            if (!Convert.ToHexString(hash).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"{file.Name} is damaged");
            }
        }

        ThrowIfAny(modelDirectory, problems);
    }

    private static List<ManifestFile> ReadManifest(string modelDirectory)
    {
        var manifestPath = Path.Combine(modelDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new TranslationModelException(modelDirectory, [$"{ManifestFileName} is missing"]);
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;
            var id = root.GetProperty("id").GetString();
            var version = root.GetProperty("version").GetString();
            if (id != Id || version != Version)
            {
                throw new TranslationModelException(
                    modelDirectory,
                    [$"found model {id} version {version}, but {Id} version {Version} is required"]);
            }

            var files = root.GetProperty("files").EnumerateObject()
                .Select(file => new ManifestFile(
                    file.Name,
                    file.Value.GetProperty("size").GetInt64(),
                    file.Value.GetProperty("sha256").GetString() ?? string.Empty))
                .ToList();
            var unlisted = RequiredFiles.Where(name => files.All(file => file.Name != name));
            ThrowIfAny(modelDirectory, unlisted.Select(name => $"{ManifestFileName} does not list {name}").ToList());
            return files;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new TranslationModelException(modelDirectory, [$"{ManifestFileName} is invalid"], exception);
        }
    }

    private static void ThrowIfAny(string modelDirectory, List<string> problems)
    {
        if (problems.Count > 0)
        {
            throw new TranslationModelException(modelDirectory, problems);
        }
    }

    private sealed record ManifestFile(string Name, long Size, string Sha256);
}
