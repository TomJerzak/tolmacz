using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Tolmacz.Tests;

public sealed class TranslationModelTests(ModelFixture model) : IClassFixture<ModelFixture>, IDisposable
{
    private static readonly string[] FileNames =
    [
        "encoder_model.int8.onnx",
        "decoder_model_merged.int8.onnx",
        "source.spm",
        "vocab.json",
    ];

    private readonly string _directory = Directory.CreateTempSubdirectory("tolmacz-model-test-").FullName;

    public void Dispose() => Directory.Delete(_directory, true);

    private string PathOf(string name) => Path.Combine(_directory, name);

    private void WriteModel(string id = TranslationModel.Id, string version = TranslationModel.Version)
    {
        var files = new Dictionary<string, object>();
        foreach (var name in FileNames)
        {
            var content = Encoding.UTF8.GetBytes("content of " + name);
            File.WriteAllBytes(PathOf(name), content);
            files[name] = new { size = content.Length, sha256 = Convert.ToHexString(SHA256.HashData(content)) };
        }

        File.WriteAllText(
            PathOf(TranslationModel.ManifestFileName),
            JsonSerializer.Serialize(new { id, version, files }));
    }

    private TranslationModelException ValidationError() =>
        Assert.Throws<TranslationModelException>(() => TranslationModel.Validate(_directory));

    [Fact]
    public void Complete_model_is_valid()
    {
        WriteModel();

        TranslationModel.Validate(_directory);
    }

    [Fact]
    public void Error_explains_which_release_to_download()
    {
        var error = ValidationError();

        Assert.Equal(_directory, error.ModelDirectory);
        Assert.Contains("model.json is missing", error.Problems);
        Assert.Contains(TranslationModel.ReleaseName, error.Message);
        Assert.Contains(TranslationModel.ReleaseUrl, error.Message);
        Assert.Contains(_directory, error.Message);
    }

    [Fact]
    public void Missing_file_is_reported()
    {
        WriteModel();
        File.Delete(PathOf("vocab.json"));

        Assert.Contains("vocab.json is missing", ValidationError().Problems);
    }

    [Fact]
    public void Wrong_file_size_is_reported()
    {
        WriteModel();
        File.AppendAllText(PathOf("source.spm"), "extra");

        Assert.Contains("source.spm has the wrong size", ValidationError().Problems);
    }

    [Theory]
    [InlineData("other-model", TranslationModel.Version)]
    [InlineData(TranslationModel.Id, "2")]
    public void Incompatible_model_is_rejected(string id, string version)
    {
        WriteModel(id, version);

        Assert.Contains("is required", ValidationError().Message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"id":"bidi-eng-pol","version":"1","files":{"source.spm":{}}}""")]
    public void Invalid_manifest_is_reported(string manifest)
    {
        File.WriteAllText(PathOf(TranslationModel.ManifestFileName), manifest);

        Assert.Contains("model.json is invalid", ValidationError().Problems);
    }

    [Fact]
    public async Task Damaged_file_with_the_right_size_is_detected()
    {
        WriteModel();
        var content = File.ReadAllBytes(PathOf("vocab.json"));
        content[0] ^= 1;
        File.WriteAllBytes(PathOf("vocab.json"), content);

        TranslationModel.Validate(_directory);
        var error = await Assert.ThrowsAsync<TranslationModelException>(
            () => TranslationModel.VerifyChecksumsAsync(_directory));

        Assert.Contains("vocab.json is damaged", error.Problems);
    }

    [Fact]
    public async Task Translator_reports_a_missing_model()
    {
        using var translator = new MarianOnnxTranslator(_directory);

        await Assert.ThrowsAsync<TranslationModelException>(() => translator.LoadAsync());
    }

    [Fact]
    public async Task Translator_reports_unreadable_model_files()
    {
        WriteModel();
        using var translator = new MarianOnnxTranslator(_directory);

        var error = await Assert.ThrowsAsync<TranslationModelException>(() => translator.LoadAsync());

        Assert.NotNull(error.InnerException);
    }

    [Fact]
    public async Task Bundled_model_matches_its_manifest() =>
        await TranslationModel.VerifyChecksumsAsync(model.ModelDirectory);
}
