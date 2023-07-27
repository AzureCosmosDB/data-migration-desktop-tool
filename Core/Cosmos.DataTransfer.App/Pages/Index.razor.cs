using Microsoft.AspNetCore.Components;
using Cosmos.DataTransfer.Ui;
using Cosmos.DataTransfer.App.Features.Settings;

namespace Cosmos.DataTransfer.App.Pages
{
    public partial class Index
    {
        [Inject]
        public IAppDataService DataService { get; set; } = null!;

        public SettingsState SettingsState => GetState<SettingsState>();

        protected override async Task OnParametersSetAsync()
        {
            await Mediator.Send(new SettingsState.LoadExtensionsAction());
        }

        private async Task SourceSelectionChanged(string name)
        {
            await Mediator.Send(new SettingsState.SelectSourceAction(name));
        }

        private async Task SinkSelectionChanged(string name)
        {
            await Mediator.Send(new SettingsState.SelectSinkAction(name));
        }
    }
}