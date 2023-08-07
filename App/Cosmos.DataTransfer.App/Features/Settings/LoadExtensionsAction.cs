using BlazorState;
using Cosmos.DataTransfer.Ui;
using Cosmos.DataTransfer.Ui.Common;

namespace Cosmos.DataTransfer.App.Features.Settings;

public partial class SettingsState
{
    public record LoadExtensionsAction : IAction;

    public class LoadExtensionsHandler : StateActionHandler<SettingsState, LoadExtensionsAction>
    {
        private readonly IAppDataService _dataService;

        public LoadExtensionsHandler(IStore aStore, IAppDataService dataService) : base(aStore)
        {
            _dataService = dataService;
        }

        public override async Task Handle(LoadExtensionsAction aAction, CancellationToken aCancellationToken)
        {
            AppExtensions extensions = await _dataService.GetExtensionsAsync();
            State.AvailableExtensions = extensions;
        }
    }

}
