namespace Tolmacz.Presentation;

/// <summary>Remembers the chosen text size between application runs.</summary>
public interface ITextSizeStore
{
    /// <summary>Returns the saved size in percent, or null when none was saved.</summary>
    int? Load();

    /// <summary>Saves the size in percent.</summary>
    void Save(int percent);
}
