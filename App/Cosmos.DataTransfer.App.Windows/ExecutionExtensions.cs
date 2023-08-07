using CommunityToolkit.Mvvm.Messaging;
using Cosmos.DataTransfer.Ui.Common;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Cosmos.DataTransfer.App.Windows;

public static class ExecutionExtensions
{
    public static void Log(this IMessenger messenger, LogMessage message)
    {
        messenger.Send(message);
    }
}