using Tolmacz.Presentation;

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
#endif

        return builder.Build();
    }

#if WINDOWS
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
