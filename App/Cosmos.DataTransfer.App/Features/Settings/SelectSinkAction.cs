using BlazorState;
using Cosmos.DataTransfer.Interfaces.Manifest;
using Cosmos.DataTransfer.Ui;
using Cosmos.DataTransfer.Ui.Common;

namespace Cosmos.DataTransfer.App.Features.Settings;

public partial class SettingsState
{
    public record SelectSinkAction(string? Sink) : IAction;

    public class SelectSinkHandler : StateActionHandler<SettingsState, SelectSinkAction>
    {
        private readonly IAppDataService _dataService;
        public SelectSinkHandler(IStore aStore, IAppDataService dataService) : base(aStore)
        {
            _dataService = dataService;
        }

        public override async Task Handle(SelectSinkAction action, CancellationToken cancellationToken)
        {
            State.SelectedSink = action.Sink;
            State.SinkSettings = null;
            if (!string.IsNullOrEmpty(State.SelectedSink))
            {
                var settings = await _dataService.GetSettingsAsync(State.SelectedSink, ExtensionDirection.Sink);
                State.SinkSettings = settings;
            }
        }
    }

}
