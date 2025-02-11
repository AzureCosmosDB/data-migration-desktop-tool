using BlazorState;
using Cosmos.DataTransfer.Interfaces.Manifest;
using Cosmos.DataTransfer.Ui;
using Cosmos.DataTransfer.Ui.Common;

namespace Cosmos.DataTransfer.App.Features.Settings;

public partial class SettingsState
{
    public record SelectSourceAction(string? Source) : IAction;

    public class SelectSourceHandler : StateActionHandler<SettingsState, SelectSourceAction>
    {
        private readonly IAppDataService _dataService;
        public SelectSourceHandler(IStore aStore, IAppDataService dataService) : base(aStore)
        {
            _dataService = dataService;
        }

        public override async Task Handle(SelectSourceAction action, CancellationToken cancellationToken)
        {
            State.SelectedSource = action.Source;
            State.SourceSettings = null;
            if (!string.IsNullOrEmpty(State.SelectedSource))
            {
                var settings = await _dataService.GetSettingsAsync(State.SelectedSource, ExtensionDirection.Source);
                State.SourceSettings = settings;
            }
        }
    }

}
