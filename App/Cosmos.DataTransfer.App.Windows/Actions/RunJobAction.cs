using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cosmos.DataTransfer.App.Windows.Actions;

public class RunJobAction : CommandAction
{
    public RunJobAction(MainViewModel host) : base(host)
    {
    }

    protected override async Task Execute(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool completed = await DataService.ExecuteWithSettingsAsync(SelectedSource?.DisplayName ?? throw new InvalidOperationException("No Source selected"),
            SelectedSink?.DisplayName ?? throw new InvalidOperationException("No Sink selected"),
            SourceSettings?.Settings,
            SinkSettings?.Settings,
            async m => Messenger.Log(m),
            cancellationToken);
    }
}