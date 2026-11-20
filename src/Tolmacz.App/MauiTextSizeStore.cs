using Tolmacz.Presentation;

namespace Tolmacz.App;

/// <summary>Keeps the text size in the application preferences.</summary>
public sealed class MauiTextSizeStore : ITextSizeStore
{
    private const string PreferenceKey = "TextSizePercent";

    /// <inheritdoc />
    public int? Load() =>
        Preferences.Default.ContainsKey(PreferenceKey)
            ? Preferences.Default.Get(PreferenceKey, 0)
            : null;

    /// <inheritdoc />
    public void Save(int percent) => Preferences.Default.Set(PreferenceKey, percent);
}
