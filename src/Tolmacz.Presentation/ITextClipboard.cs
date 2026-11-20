namespace Tolmacz.Presentation;

/// <summary>Copies text to the system clipboard.</summary>
public interface ITextClipboard
{
    /// <summary>Replaces the clipboard contents with the given text.</summary>
    Task SetTextAsync(string text);
}
