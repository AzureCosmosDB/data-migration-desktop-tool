using System.Diagnostics;
using System.Text.Json;
using Cosmos.DataTransfer.Interfaces.Manifest;
using Cosmos.DataTransfer.Ui;
using Cosmos.DataTransfer.Ui.Common;
using Cosmos.DataTransfer.Ui.MessageOutput;

namespace Cosmos.DataTransfer.App.Data;

public class AppDataService : IAppDataService
{
    private readonly AppSettings _appSettings;
    private ExtensionManifest? _sources;
    private ExtensionManifest? _sinks;

    public async Task<ExtensionManifest> GetSourceManifest()
    {
        if (_sources == null || _sources == ExtensionManifest.Empty)
        {
            _sources = await GetExtensionManifest(ExtensionDirection.Source);
        }
        return _sources;
    }

    public async Task<ExtensionManifest> GetSinkManifest()
    {
        if (_sinks == null || _sinks == ExtensionManifest.Empty)
        {
            _sinks = await GetExtensionManifest(ExtensionDirection.Sink);
        }
        return _sinks;
    }

    private async Task<ExtensionManifest> GetExtensionManifest(ExtensionDirection direction)
    {
        string tempFilePath = Path.GetTempFileName();
        await RunCoreAppAsync($"settings --output \"{tempFilePath}\" {(direction == ExtensionDirection.Sink ? "--sink" : "--source")}");
        ExtensionManifest manifest;
        await using (FileStream stream = File.OpenRead(tempFilePath))
        {
            manifest = await JsonSerializer.DeserializeAsync<ExtensionManifest>(stream, ExtensionManifestUtility.JsonOptions) ?? ExtensionManifest.Empty;
        }
        File.Delete(tempFilePath);
        return manifest;
    }

    public AppDataService(AppSettings appSettings)
    {
        _appSettings = appSettings;
    }

    public async Task<AppExtensions> GetExtensionsAsync()
    {
        if (_appSettings.CoreAppPath == null)
        {
            throw new InvalidOperationException();
        }

        var sourceManifest = await GetSourceManifest();
        var sinkManifest = await GetSinkManifest();

        return ExtensionManifestUtility.CombineManifestExtensions(sourceManifest, sinkManifest);
    }

    public async Task<ExtensionSettings> GetSettingsAsync(string name, ExtensionDirection direction)
    {
        if (_appSettings.CoreAppPath == null)
        {
            throw new InvalidOperationException();
        }

        var manifest = direction == ExtensionDirection.Sink ? await GetSinkManifest() : await GetSourceManifest();
        return manifest.GetExtensionSettings(name);
    }

    public async Task<string> BuildSettingsAsync(string selectedSource, string selectedSink, IEnumerable<ExtensionSetting>? source, IEnumerable<ExtensionSetting>? sink)
    {
        string json = ExtensionManifestUtility.CreateMigrationSettingsJson(selectedSource, selectedSink, source, sink);
        return json;
    }

    public async Task<string> BuildCommandAsync(string selectedSource, string selectedSink, IEnumerable<ExtensionSetting>? source, IEnumerable<ExtensionSetting>? sink)
    {
        string command = ExtensionManifestUtility.CreateRunCommandJson(selectedSource, selectedSink, source, sink);
        return command;
    }

    public async Task<bool> ExecuteWithSettingsAsync(string selectedSource, string selectedSink, IEnumerable<ExtensionSetting>? source, IEnumerable<ExtensionSetting>? sink, Func<LogMessage, Task> sendLogMessage, CancellationToken cancellationToken)
    {
        string json = ExtensionManifestUtility.CreateMigrationSettingsJson(selectedSource, selectedSink, source, sink);
        var path = Path.Combine(Path.GetTempPath(), "migrationsettings.json");
        await File.WriteAllTextAsync(path, json, cancellationToken);

        return await RunCoreAppAsync($"run --settings \"{path}\"", sendLogMessage, cancellationToken);
    }


    private Task<bool> RunCoreAppAsync(string arguments)
    {
        return RunCoreAppAsync(arguments, m => Task.CompletedTask, CancellationToken.None);
    }

    private async Task<bool> RunCoreAppAsync(string arguments, Func<LogMessage, Task> sendLogMessage, CancellationToken cancellationToken)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = _appSettings.CoreAppPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        });

        try
        {
            MessageType? activeType = null;
            while (!process!.StandardOutput.EndOfStream)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await process.StandardOutput.ReadLineAsync(cancellationToken);
                if (line != null)
                {
                    if (!line.StartsWith('\t') && !line.StartsWith("  "))
                    {
                        activeType = null;
                    }
                    var message = LogMessage.App(line);
                    if (activeType != null)
                    {
                        message.Type = activeType.Value;
                    }
                    
                    await sendLogMessage(message);

                    activeType = message.Type;
                }
            }

            await process!.WaitForExitAsync(cancellationToken);
            return true;
        }
        catch
        {
            process!.Kill();
            throw;
        }

        return false;
    }
}