namespace Tolmacz.App;

/// <summary>The application object, which creates the main window.</summary>
public partial class App : Application
{
    private readonly MainPage _mainPage;

    /// <summary>Creates the application with its main page.</summary>
    public App(MainPage mainPage)
    {
        InitializeComponent();
        _mainPage = mainPage;
    }

    protected override Window CreateWindow(IActivationState? activationState) => new(_mainPage)
    {
        Title = "Tolmacz",
        Width = 1000,
        Height = 640,
        MinimumWidth = 720,
        MinimumHeight = 480,
    };
}
