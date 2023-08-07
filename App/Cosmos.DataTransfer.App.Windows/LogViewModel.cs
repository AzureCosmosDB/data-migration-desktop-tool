using CommunityToolkit.Mvvm.Messaging;
using Cosmos.DataTransfer.App.Windows.Framework;
using Cosmos.DataTransfer.Ui.Common;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Threading;

namespace Cosmos.DataTransfer.App.Windows;

public class LogViewModel : ViewModelBase
{
    public LogViewModel()
    {
        BindingOperations.EnableCollectionSynchronization(Messages, new object());
        Messenger.Register<LogMessage>(this, (s, m) =>
        {
            Messages.Add(m);
        });
    }

    public ObservableCollection<LogMessage> Messages { get; } = new();
}
