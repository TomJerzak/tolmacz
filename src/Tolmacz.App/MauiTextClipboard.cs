using Tolmacz.Presentation;

namespace Tolmacz.App;

/// <summary>Uses the clipboard of the current platform.</summary>
public sealed class MauiTextClipboard : ITextClipboard
{
    /// <inheritdoc />
    public Task SetTextAsync(string text) => Clipboard.Default.SetTextAsync(text);
}
