namespace Tolmacz;

/// <summary>The source and target languages of a translation.</summary>
public enum TranslationDirection
{
    /// <summary>Translates English text into Polish.</summary>
    EnglishToPolish,
    /// <summary>Translates Polish text into English.</summary>
    PolishToEnglish,
}

/// <summary>The translated text and whether the model finished all of it.</summary>
public sealed record TranslationResult(string Text, bool IsComplete);

/// <summary>Translates text between Polish and English.</summary>
public interface ITranslator
{
    /// <summary>Loads the model. Safe to call repeatedly; later calls return immediately.</summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Translates text, preserving its line breaks and spacing between sentences.</summary>
    /// <param name="text">The text to translate.</param>
    /// <param name="direction">The source and target languages.</param>
    /// <param name="progress">Receives the completed fraction, from 0 to 1.</param>
    /// <param name="cancellationToken">Stops the translation.</param>
    Task<TranslationResult> TranslateAsync(
        string text,
        TranslationDirection direction,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
