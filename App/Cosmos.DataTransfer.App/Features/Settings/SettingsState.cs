using BlazorState;
using Cosmos.DataTransfer.Ui;
using Cosmos.DataTransfer.Ui.Common;

namespace Cosmos.DataTransfer.App.Features.Settings;

public partial class SettingsState : State<SettingsState>
{
    public AppExtensions? AvailableExtensions { get; private set; }
    public string? SelectedSource { get; private set; }
    public string? SelectedSink { get; private set; }
    public ExtensionSettings? SourceSettings { get; private set; }
    public ExtensionSettings? SinkSettings { get; private set; }

    public override void Initialize()
    {
        AvailableExtensions = null;
        SelectedSource = null;
        SelectedSink = null;
        SourceSettings = null;
        SinkSettings = null;
    }
}
