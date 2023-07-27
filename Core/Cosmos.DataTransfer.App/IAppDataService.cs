using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Ui.MessageOutput;

namespace Cosmos.DataTransfer.Ui;
public interface IAppDataService : IDataService
{
    Task<string> BuildCommandAsync(string selectedSource, string selectedSink, IEnumerable<ExtensionSetting>? source, IEnumerable<ExtensionSetting>? sink);
    Task<string> BuildSettingsAsync(string selectedSource, string selectedSink, IEnumerable<ExtensionSetting>? source, IEnumerable<ExtensionSetting>? sink);
    Task<bool> ExecuteWithSettingsAsync(string selectedSource, string selectedSink, IEnumerable<ExtensionSetting>? source, IEnumerable<ExtensionSetting>? sink, Func<LogMessage, Task> sendLogMessage, CancellationToken cancellationToken);
}