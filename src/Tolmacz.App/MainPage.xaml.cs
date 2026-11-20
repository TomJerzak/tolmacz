using Tolmacz.Presentation;

namespace Tolmacz.App;

/// <summary>The translator screen.</summary>
public partial class MainPage : ContentPage
{
#if MACCATALYST
    private const string ShortcutName = "Cmd+Enter";
#else
    private const string ShortcutName = "Ctrl+Enter";
#endif

    private readonly MainViewModel _viewModel;
    private bool _initialized;

    /// <summary>Creates the screen and connects it to its view model.</summary>
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        AddTranslateShortcut();
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_initialized)
        {
            _initialized = true;
            await _viewModel.InitializeAsync();
        }
    }

    private void AddTranslateShortcut()
    {
        ToolTipProperties.SetText(TranslateButton, $"Translate ({ShortcutName})");

        // Windows implements this in MainPage.Windows.cs. Mac uses the menu in AppDelegate.
        ConnectTranslateShortcut();
    }

    partial void ConnectTranslateShortcut();
}
