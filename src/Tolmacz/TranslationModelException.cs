namespace Tolmacz;

/// <summary>Reports that the translation model is missing, incompatible or damaged.</summary>
public sealed class TranslationModelException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="modelDirectory">The folder that was checked.</param>
    /// <param name="problems">What is wrong with the model files.</param>
    /// <param name="innerException">The original error, when there is one.</param>
    public TranslationModelException(
        string modelDirectory,
        IReadOnlyList<string> problems,
        Exception? innerException = null)
        : base(CreateMessage(modelDirectory, problems), innerException)
    {
        ModelDirectory = modelDirectory;
        Problems = problems;
    }

    /// <summary>The folder that was checked.</summary>
    public string ModelDirectory { get; }

    /// <summary>What is wrong with the model files.</summary>
    public IReadOnlyList<string> Problems { get; }

    private static string CreateMessage(string modelDirectory, IReadOnlyList<string> problems) =>
        $"The translation model in '{modelDirectory}' cannot be used: {string.Join("; ", problems)}. "
        + $"Download model release {TranslationModel.ReleaseName} from {TranslationModel.ReleaseUrl} "
        + "and extract all of its files into this folder.";
}
