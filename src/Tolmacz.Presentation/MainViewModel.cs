using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Tolmacz.Presentation;

/// <summary>
/// The state and actions of the translator screen. A visible translation always belongs
/// to the current source text: editing the source removes it.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private const string ReadyStatus = "Ready. Everything stays on this device.";
    private const int DefaultTextSize = 100;
    private const int MinTextSize = 80;
    private const int MaxTextSize = 200;
    private const int TextSizeStep = 10;
    private const double BaseEditorFontSize = 16;

    private readonly ITranslator _translator;
    private readonly ITextClipboard _clipboard;
    private readonly ITextSizeStore _textSizeStore;

    private CancellationTokenSource? _cancellation;
    private TranslationDirection _direction = TranslationDirection.PolishToEnglish;
    private string _sourceText = string.Empty;
    private string _translatedText = string.Empty;
    private string _status = "Loading the translation model…";
    private double _progress;
    private bool _isReady;
    private bool _isTranslating;
    private int _textSizePercent;

    /// <summary>Creates the view model. Call <see cref="InitializeAsync"/> before translating.</summary>
    public MainViewModel(ITranslator translator, ITextClipboard clipboard, ITextSizeStore textSizeStore)
    {
        _translator = translator;
        _clipboard = clipboard;
        _textSizeStore = textSizeStore;
        _textSizePercent = NormalizeTextSize(textSizeStore.Load() ?? DefaultTextSize);
        DecreaseTextSizeCommand = new RelayCommand(
            Run(() => SetTextSize(_textSizePercent - TextSizeStep)),
            () => _textSizePercent > MinTextSize);
        IncreaseTextSizeCommand = new RelayCommand(
            Run(() => SetTextSize(_textSizePercent + TextSizeStep)),
            () => _textSizePercent < MaxTextSize);
        ResetTextSizeCommand = new RelayCommand(
            Run(() => SetTextSize(DefaultTextSize)),
            () => _textSizePercent != DefaultTextSize);
        TranslateCommand = new RelayCommand(TranslateOrCancelAsync, CanTranslateOrCancel);
        SwapCommand = new RelayCommand(Run(Swap), () => !IsTranslating);
        ClearCommand = new RelayCommand(
            Run(Clear),
            () => !IsTranslating && (SourceText.Length > 0 || TranslatedText.Length > 0));
        CopyCommand = new RelayCommand(CopyAsync, () => TranslatedText.Length > 0);
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Starts a translation, or cancels the one in progress.</summary>
    public RelayCommand TranslateCommand { get; }
    /// <summary>Switches languages and moves a current translation to the source.</summary>
    public RelayCommand SwapCommand { get; }
    /// <summary>Clears the source text and the translation.</summary>
    public RelayCommand ClearCommand { get; }
    /// <summary>Copies the translation to the clipboard.</summary>
    public RelayCommand CopyCommand { get; }
    /// <summary>Makes the text in both text boxes one step smaller.</summary>
    public RelayCommand DecreaseTextSizeCommand { get; }
    /// <summary>Makes the text in both text boxes one step larger.</summary>
    public RelayCommand IncreaseTextSizeCommand { get; }
    /// <summary>Returns the text in both text boxes to its default size.</summary>
    public RelayCommand ResetTextSizeCommand { get; }

    /// <summary>The length of the source text as shown to the user.</summary>
    public string CharacterCountLabel => SourceText.Length == 1 ? "1 character" : $"{SourceText.Length} characters";
    /// <summary>The current text size as shown to the user.</summary>
    public string TextSizeLabel => $"{_textSizePercent}%";
    /// <summary>The font size of the source and translation text boxes.</summary>
    public double EditorFontSize => BaseEditorFontSize * _textSizePercent / DefaultTextSize;

    /// <summary>The display name of the source language.</summary>
    public string SourceLanguage => _direction == TranslationDirection.PolishToEnglish ? "Polish" : "English";
    /// <summary>The display name of the target language.</summary>
    public string TargetLanguage => _direction == TranslationDirection.PolishToEnglish ? "English" : "Polish";
    /// <summary>The hint shown while the source text is empty.</summary>
    public string SourcePlaceholder => _direction == TranslationDirection.PolishToEnglish
        ? "Wpisz lub wklej tekst…"
        : "Type or paste text…";
    /// <summary>The hint shown while there is no translation, in the target language.</summary>
    public string TargetPlaceholder => _direction == TranslationDirection.PolishToEnglish
        ? "Translation appears here"
        : "Tu pojawi się tłumaczenie";
    /// <summary>The label of the main action button.</summary>
    public string TranslateButtonText => IsTranslating ? "Cancel" : "Translate";

    /// <summary>The text to translate. Changing it removes the current translation.</summary>
    public string SourceText
    {
        get => _sourceText;
        set
        {
            if (!Set(ref _sourceText, value ?? string.Empty))
            {
                return;
            }

            OnPropertyChanged(nameof(CharacterCountLabel));

            if (!IsTranslating)
            {
                TranslatedText = string.Empty;
                if (_isReady)
                {
                    Status = ReadyStatus;
                }
            }

            RefreshCommands();
        }
    }

    /// <summary>The translation of the current source text.</summary>
    public string TranslatedText
    {
        get => _translatedText;
        private set
        {
            if (Set(ref _translatedText, value))
            {
                RefreshCommands();
            }
        }
    }

    /// <summary>A message describing the latest state or result.</summary>
    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    /// <summary>The completed fraction of the current translation, from 0 to 1.</summary>
    public double Progress
    {
        get => _progress;
        private set => Set(ref _progress, value);
    }

    /// <summary>Whether a translation is in progress.</summary>
    public bool IsTranslating
    {
        get => _isTranslating;
        private set
        {
            if (Set(ref _isTranslating, value))
            {
                OnPropertyChanged(nameof(TranslateButtonText));
                RefreshCommands();
            }
        }
    }

    /// <summary>Loads the model and reports the outcome in <see cref="Status"/>.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            await _translator.LoadAsync();
            _isReady = true;
            Status = ReadyStatus;
        }
        catch (Exception exception)
        {
            Status = $"The translation model could not be loaded: {exception.Message}";
        }

        RefreshCommands();
    }

    private static Func<Task> Run(Action action) => () =>
    {
        action();
        return Task.CompletedTask;
    };

    private bool CanTranslateOrCancel() =>
        IsTranslating || (_isReady && !string.IsNullOrWhiteSpace(SourceText));

    private async Task TranslateOrCancelAsync()
    {
        if (IsTranslating)
        {
            _cancellation?.Cancel();
            Status = "Cancelling…";
            return;
        }

        await TranslateAsync();
    }

    private async Task TranslateAsync()
    {
        using var cancellation = new CancellationTokenSource();
        var requestedText = SourceText;
        _cancellation = cancellation;
        TranslatedText = string.Empty;
        IsTranslating = true;
        Progress = 0;
        Status = "Translating…";

        try
        {
            var result = await _translator.TranslateAsync(
                requestedText,
                _direction,
                new Progress<double>(value => Progress = value),
                cancellation.Token);

            ShowResult(requestedText, result);
        }
        catch (OperationCanceledException)
        {
            Status = "Cancelled.";
        }
        catch (Exception exception)
        {
            Status = $"Translation failed: {exception.Message}";
        }
        finally
        {
            _cancellation = null;
            IsTranslating = false;
            Progress = 0;
        }
    }

    private void ShowResult(string requestedText, TranslationResult result)
    {
        if (SourceText != requestedText)
        {
            Status = "The text changed, so the translation was discarded.";
            return;
        }

        TranslatedText = result.Text;
        Status = result.IsComplete ? "Done." : "Done, but part of the translation may be missing.";
    }

    private void Swap()
    {
        var translation = TranslatedText;
        _direction = _direction == TranslationDirection.PolishToEnglish
            ? TranslationDirection.EnglishToPolish
            : TranslationDirection.PolishToEnglish;

        if (translation.Length > 0)
        {
            SourceText = translation;
            TranslatedText = string.Empty;
        }

        OnPropertyChanged(nameof(SourceLanguage));
        OnPropertyChanged(nameof(TargetLanguage));
        OnPropertyChanged(nameof(SourcePlaceholder));
        OnPropertyChanged(nameof(TargetPlaceholder));
    }

    private void Clear()
    {
        SourceText = string.Empty;
        TranslatedText = string.Empty;
    }

    private async Task CopyAsync()
    {
        try
        {
            await _clipboard.SetTextAsync(TranslatedText);
            Status = "Translation copied.";
        }
        catch (Exception exception)
        {
            Status = $"The translation could not be copied: {exception.Message}";
        }
    }

    private static int NormalizeTextSize(int percent)
    {
        var limited = Math.Clamp(percent, MinTextSize, MaxTextSize);
        return (int)Math.Round(limited / (double)TextSizeStep) * TextSizeStep;
    }

    private void SetTextSize(int percent)
    {
        _textSizePercent = NormalizeTextSize(percent);
        OnPropertyChanged(nameof(TextSizeLabel));
        OnPropertyChanged(nameof(EditorFontSize));
        DecreaseTextSizeCommand.RaiseCanExecuteChanged();
        IncreaseTextSizeCommand.RaiseCanExecuteChanged();
        ResetTextSizeCommand.RaiseCanExecuteChanged();

        try
        {
            _textSizeStore.Save(_textSizePercent);
        }
        catch (Exception exception)
        {
            Status = $"The text size could not be saved: {exception.Message}";
        }
    }

    private void RefreshCommands()
    {
        TranslateCommand.RaiseCanExecuteChanged();
        SwapCommand.RaiseCanExecuteChanged();
        ClearCommand.RaiseCanExecuteChanged();
        CopyCommand.RaiseCanExecuteChanged();
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
