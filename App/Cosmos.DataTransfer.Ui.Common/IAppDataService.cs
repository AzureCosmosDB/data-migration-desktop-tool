namespace Cosmos.DataTransfer.Ui.Common;

public interface IAppDataService : IDataService
{
    Task<string> BuildCommandAsync(string selectedSource, string selectedSink, IEnumerable<ExtensionSetting>? source, IEnumerable<ExtensionSetting>? sink);
    Task<string> BuildSettingsAsync(string selectedSource, string selectedSink, IEnumerable<ExtensionSetting>? source, IEnumerable<ExtensionSetting>? sink);
    Task<bool> ExecuteWithSettingsAsync(string selectedSource, string selectedSink, IEnumerable<ExtensionSetting>? source, IEnumerable<ExtensionSetting>? sink, Func<LogMessage, Task> sendLogMessage, CancellationToken cancellationToken);
}
