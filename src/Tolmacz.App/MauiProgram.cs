using Tolmacz.Presentation;
#if WINDOWS
using Microsoft.Maui.LifecycleEvents;
#endif

namespace Tolmacz.App;

/// <summary>Configures the application and its services.</summary>
public static class MauiProgram
{
    /// <summary>Builds the application.</summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<ITranslator>(_ => new MarianOnnxTranslator(GetModelDirectory()));
        builder.Services.AddSingleton<ITextClipboard, MauiTextClipboard>();
        builder.Services.AddSingleton<ITextSizeStore, MauiTextSizeStore>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainPage>();

#if WINDOWS
        // After Swap, WinUI can keep the text box's inner scroll presenter at the height of the
        // swapped-out text, so a longer translation shows only its first lines. Measuring it
        // again after every text change keeps all lines visible.
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping(nameof(IEditor.Text), (handler, _) =>
        {
            var textBox = handler.PlatformView;
            textBox.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                InvalidateMeasureUpTo(textBox, FindDescendant(textBox, child => child is Microsoft.UI.Xaml.Controls.ScrollContentPresenter)));
        });

        // After Swap, an empty text box shows no hint at all, where "Tu pojawi się tłumaczenie"
        // should replace "Translation appears here", until the window is resized. The hint gets
        // the new text, but MAUI does not lay out the text box again, so the hint keeps the old
        // text's size and is not drawn. Laying out the text box again in its place draws it.
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping(nameof(IEditor.Placeholder), (handler, _) =>
        {
            var textBox = handler.PlatformView;
            textBox.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                InvalidateMeasureUpTo(textBox, FindDescendant(textBox, child => child is Microsoft.UI.Xaml.FrameworkElement { Name: "PlaceholderTextContentPresenter" }));
                textBox.Measure(Microsoft.UI.Xaml.Controls.Primitives.LayoutInformation.GetAvailableSize(textBox));
                textBox.Arrange(Microsoft.UI.Xaml.Controls.Primitives.LayoutInformation.GetLayoutSlot(textBox));
            });
        });

        // After the Windows theme changes while Tolmacz runs, "−" and "+" disappear and Clear and
        // Copy keep the gray of the old theme. MAUI updates its colors inside the
        // WM_SETTINGCHANGE message, and two kinds of button colors miss that update:
        // - Win2D draws the "−" and "+" icons, but it cannot make its COM call inside that message
        //   (RPC_E_CANTCALLOUT_ININPUTSYNCCALL). MAUI catches the error and removes the icon.
        //   Loading the icons again after the message draws them in the new color.
        // - Clear and Copy are disabled when the window is built, so MAUI first gives them the
        //   Disabled color of Styles.xaml. Setting the GhostButton color binding then moves that
        //   color to a layer above all styles, where it stays after the button is enabled, and no
        //   binding updates it. Applying the style again while the button shows its Disabled state
        //   moves the Disabled color of the new theme there, as at start, so the buttons look as
        //   they do after a restart in the new theme.
        builder.ConfigureLifecycleEvents(events => events.AddWindows(windows => windows.OnWindowCreated(_ =>
        {
            Application.Current!.RequestedThemeChanged -= OnRequestedThemeChanged;
            Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
        })));
#endif

        return builder.Build();
    }

#if WINDOWS
    private static void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        foreach (var window in Application.Current!.Windows)
        {
            if (window is { Page: { } page, Handler.PlatformView: Microsoft.UI.Xaml.Window platformWindow })
            {
                platformWindow.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    foreach (var button in page.GetVisualTreeDescendants().OfType<Button>())
                    {
                        UpdateThemeColors(button);
                    }
                });
            }
        }
    }

    private static void UpdateThemeColors(Button button)
    {
        if (button.ImageSource is not null)
        {
            button.Handler?.UpdateValue(nameof(Button.ImageSource));
        }

        // A disabled button gets the Disabled color layer, as at start. An enabled button has one
        // when its text color is not the one its style gives in the new theme.
        if (button.Style is not { } style || (button.IsEnabled && button.TextColor == new Button { Style = style }.TextColor))
        {
            return;
        }

        VisualStateManager.GoToState(button, "Disabled");
        button.Style = null;
        button.Style = style;
        VisualStateManager.GoToState(button, button.IsEnabled ? "Normal" : "Disabled");
    }

    private static void InvalidateMeasureUpTo(Microsoft.UI.Xaml.Controls.TextBox textBox, Microsoft.UI.Xaml.DependencyObject? start)
    {
        for (var element = start;
             element is not null;
             element = ReferenceEquals(element, textBox) ? null : Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element))
        {
            (element as Microsoft.UI.Xaml.UIElement)?.InvalidateMeasure();
        }
    }

    private static Microsoft.UI.Xaml.DependencyObject? FindDescendant(
        Microsoft.UI.Xaml.DependencyObject parent,
        Func<Microsoft.UI.Xaml.DependencyObject, bool> isTarget)
    {
        var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (var index = 0; index < count; index++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, index);
            if (isTarget(child))
            {
                return child;
            }

            if (FindDescendant(child, isTarget) is { } found)
            {
                return found;
            }
        }

        return null;
    }
#endif

    private static string GetModelDirectory()
    {
#if MACCATALYST
        var root = Foundation.NSBundle.MainBundle.ResourcePath!;
#else
        var root = AppContext.BaseDirectory;
#endif
        return Path.Combine(root, "Models", "bidi-eng-pol");
    }
}
