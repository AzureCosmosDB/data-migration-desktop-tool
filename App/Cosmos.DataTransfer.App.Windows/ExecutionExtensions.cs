using CommunityToolkit.Mvvm.Messaging;
using Cosmos.DataTransfer.Ui.Common;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Cosmos.DataTransfer.App.Windows;

public static class ExecutionExtensions
{
    public static void ThenReset(this Task task, IMessenger messenger)
    {
        // TODO: cancel execution
        //task.ContinueWith(t =>
        //{
        //    messenger.Send(new ExecutionState.CancelExecutionAction(true));
        //});
    }

    public static void Log(this IMessenger messenger, LogMessage message)
    {
        messenger.Send(message);
    }
}