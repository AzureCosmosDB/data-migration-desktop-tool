using System;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.DataTransfer.Ui.Common;

namespace Cosmos.DataTransfer.App.Windows.Actions;

public class ExportSettingsAction : CommandAction
{
    public ExportSettingsAction(MainViewModel host) : base(host)
    {
    }

    protected override async Task Execute(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var output = await DataService.BuildSettingsAsync(SelectedSource?.DisplayName ?? throw new InvalidOperationException("No Source selected"),
            SelectedSink?.DisplayName ?? throw new InvalidOperationException("No Sink selected"),
            SourceSettings?.Settings,
            SinkSettings?.Settings);

        Messenger.Log(LogMessage.Data(output));
    }
}