using Microsoft.AspNetCore.Components;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Interfaces.Manifest;
using Cosmos.DataTransfer.Ui;
using Cosmos.DataTransfer.Ui.Common;
using Cosmos.DataTransfer.Ui.MessageOutput;

namespace Cosmos.DataTransfer.Web.Pages
{
    public partial class Index
    {
        [Inject]
        public IClientDataService DataService { get; set; } = null!;

        public AppExtensions? AllExtensions { get; set; }
        public string? SelectedSource { get; set; }
        public string? SelectedSink { get; set; }

        public ExtensionSettings? SourceSettings { get; set; }
        public ExtensionSettings? SinkSettings { get; set; }

        public List<LogMessage> Logs { get; } = new();

        protected override async Task OnParametersSetAsync()
        {
            AppExtensions extensions = await DataService.GetExtensionsAsync();
            AllExtensions = extensions;
        }

        private async Task SourceSelectionChanged(string name)
        {
            SelectedSource = name;
            var settings = await DataService.GetSettingsAsync(name, ExtensionDirection.Source);
            SourceSettings = settings;
        }

        private async Task SinkSelectionChanged(string name)
        {
            SelectedSink = name;
            var settings = await DataService.GetSettingsAsync(name, ExtensionDirection.Sink);
            SinkSettings = settings;
        }

        private async Task RunTransfer()
        {
            if (SelectedSource == null || SelectedSink == null)
            {
                Logs.Add(LogMessage.Warn("Choose Source and Sink to generate settings."));
                return;
            }

            try
            {
                var output = await DataService.GenerateMigrationFileAsync(SelectedSource, SelectedSink, SourceSettings?.Settings, SinkSettings?.Settings);
                Logs.Add(LogMessage.Data(output));
            }
            catch (Exception ex)
            {
                Logs.Add(LogMessage.Error(ex.Message));
            }
        }
    }
}