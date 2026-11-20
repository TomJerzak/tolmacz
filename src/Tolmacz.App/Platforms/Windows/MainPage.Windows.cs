using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace Tolmacz.App;

public partial class MainPage
{
    private UIElement? _shortcutRoot;

    partial void ConnectTranslateShortcut()
    {
        Loaded += (_, _) => AttachTranslateShortcut();
        Unloaded += (_, _) => DetachTranslateShortcut();
    }

    private void AttachTranslateShortcut()
    {
        DetachTranslateShortcut();
        _shortcutRoot = (Window?.Handler?.PlatformView as Microsoft.UI.Xaml.Window)?.Content;
        if (_shortcutRoot is not null)
        {
            _shortcutRoot.PreviewKeyDown += OnPreviewKeyDown;
        }
    }

    private void DetachTranslateShortcut()
    {
        if (_shortcutRoot is not null)
        {
            _shortcutRoot.PreviewKeyDown -= OnPreviewKeyDown;
            _shortcutRoot = null;
        }
    }

    // A preview handler runs before the text box, which would otherwise insert a line break.
    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // AltGr is reported as Ctrl+Alt, so a pressed Alt means this is not the shortcut.
        if (e.Key != VirtualKey.Enter || !IsKeyDown(VirtualKey.Control) || IsKeyDown(VirtualKey.Menu))
        {
            return;
        }

        e.Handled = true;

        // Holding the keys must not switch between Translate and Cancel.
        if (!e.KeyStatus.WasKeyDown && _viewModel.TranslateCommand.CanExecute())
        {
            _viewModel.TranslateCommand.Execute();
        }
    }

    private static bool IsKeyDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);
}
