using System.Windows.Input;

namespace Tolmacz.Presentation;

/// <summary>A UI command backed by an asynchronous action that handles its own errors.</summary>
/// <param name="execute">The action to run. It must handle its own errors.</param>
/// <param name="canExecute">Returns whether the command is available.</param>
public sealed class RelayCommand(Func<Task> execute, Func<bool> canExecute) : ICommand
{
    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter = null) => canExecute();

    /// <inheritdoc />
    public async void Execute(object? parameter = null) => await execute();

    /// <summary>Runs the command and lets the caller wait for it.</summary>
    public Task ExecuteAsync() => execute();

    /// <summary>Tells the UI to check again whether the command is available.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
