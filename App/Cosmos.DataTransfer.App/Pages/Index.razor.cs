using Microsoft.AspNetCore.Components;
using Cosmos.DataTransfer.Ui;
using Cosmos.DataTransfer.App.Features.Settings;
using Cosmos.DataTransfer.App.Features.Execution;
using Cosmos.DataTransfer.Ui.Common;
using Cosmos.DataTransfer.Ui.MessageOutput;

namespace Cosmos.DataTransfer.App.Pages
{
    public partial class Index
    {
        [Inject]
        public IAppDataService DataService { get; set; } = null!;
        [Inject]
        public AppSettings Settings { get; set; } = null!;

        public SettingsState SettingsState => GetState<SettingsState>();

        protected override async Task OnParametersSetAsync()
        {
            await Mediator.Send(new SettingsState.LoadExtensionsAction());

            if (File.Exists(Settings.CoreAppPath))
                Mediator.Log(new LogMessage($"Using DMT application at path '{Settings.CoreAppPath}'."));
            else
                Mediator.Log(LogMessage.Error($"DMT application not found. Attempted to use path '{Settings.CoreAppPath}'."));
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