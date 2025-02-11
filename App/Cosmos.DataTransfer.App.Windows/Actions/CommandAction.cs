using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Cosmos.DataTransfer.Ui.Common;

namespace Cosmos.DataTransfer.App.Windows.Actions;

public abstract class CommandAction
{
    public MainViewModel Host { get; set; }
    protected IMessenger Messenger => Host.GetMessenger();

    protected ExtensionDefinition? SelectedSource => Host.SelectedSource;
    protected ExtensionDefinition? SelectedSink => Host.SelectedSink;
    protected ExtensionSettings? SourceSettings => Host.SourceSettings;
    protected ExtensionSettings? SinkSettings => Host.SinkSettings;
    protected IAppDataService DataService => Host.DataService;

    protected CommandAction(MainViewModel host)
    {
        Host = host;
    }

    protected bool SettingsSelected()
    {
        if (SelectedSource == null || SelectedSink == null)
        {
            Messenger.Log(LogMessage.Warn("Choose Source and Sink to generate settings."));
            return false;
        }

        return true;
    }

    protected abstract Task Execute(CancellationToken cancellationToken);

    public async Task Execute()
    {
        if (!SettingsSelected())
            return;

        Host.CurrentExecutionAction = new CancellationTokenSource();
        Host.IsExecuting = true;

        Task task = Execute(Host.CurrentExecutionAction.Token);
        ThenReset(task);
        try
        {
            await task;
        }
        catch (TaskCanceledException)
        {
            Messenger.Log(LogMessage.Warn("Operation Canceled"));
        }
        catch (Exception ex)
        {
            Messenger.Log(LogMessage.Error(ex.Message));
        }
    }

    public void ThenReset(Task task)
    {
        task.ContinueWith(t =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Host.CancelExecution(true);
            });
        });
    }
}
