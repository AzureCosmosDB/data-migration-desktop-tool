using System.Windows.Input;
using System;

namespace Cosmos.DataTransfer.App.Windows.Framework;

public class DelegateCommand : ICommand
{
    private readonly Action _command;
    private readonly Func<bool>? _canExecute;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public DelegateCommand(Action command, Func<bool>? canExecute = null)
    {
        _canExecute = canExecute;
        _command = command ?? throw new ArgumentNullException();
    }

    public void Execute(object? parameter)
    {
        _command();
    }

    public bool CanExecute(object? parameter)
    {
        return _canExecute == null || _canExecute();
    }
}