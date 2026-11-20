using Foundation;
using ObjCRuntime;
using Tolmacz.Presentation;
using UIKit;

namespace Tolmacz.App;

/// <summary>Connects the Mac Catalyst application to .NET MAUI.</summary>
[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    private const string TranslateAction = "translateFromShortcut:";

    /// <inheritdoc />
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    /// <summary>Adds the Translate command and its Cmd+Enter shortcut to the Mac menu bar.</summary>
    public override void BuildMenu(IUIMenuBuilder builder)
    {
        base.BuildMenu(builder);
        if (builder.System != UIMenuSystem.MainSystem)
        {
            return;
        }

        var translate = UIKeyCommand.Create(
            "Translate",
            null,
            new Selector(TranslateAction),
            "\r",
            UIKeyModifierFlags.Command,
            null);
        var menu = UIMenu.Create("Translation", [translate]);
        builder.InsertSiblingMenuAfter(menu, UIMenuIdentifier.Edit.GetConstant()!);
    }

    /// <summary>Runs Translate, or Cancel while a translation is in progress.</summary>
    [Export(TranslateAction)]
    public void TranslateFromShortcut(NSObject sender)
    {
        var command = IPlatformApplication.Current?.Services.GetRequiredService<MainViewModel>().TranslateCommand;
        if (command?.CanExecute() == true)
        {
            command.Execute();
        }
    }
}
