using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Ui;
using System.Reflection;
using System.Text.Json;
using Cosmos.DataTransfer.Interfaces.Manifest;
using Cosmos.DataTransfer.Ui.Common;

namespace Cosmos.DataTransfer.Web.Pages
{
    public interface IClientDataService : IDataService
    {
        Task<string> GenerateMigrationFileAsync(string selectedSource, string selectedSink, IEnumerable<ExtensionSetting>? source, IEnumerable<ExtensionSetting>? sink);
    }
    public class ClientDataService : IClientDataService
    {
        public ExtensionManifest Sinks { get; }
        public ExtensionManifest Sources { get; }

        public ClientDataService()
        {
            string sourceFile = GetFileContent("Cosmos.DataTransfer.Web.SourceManifest.json");
            string sinkFile = GetFileContent("Cosmos.DataTransfer.Web.SinkManifest.json");

            Sources = JsonSerializer.Deserialize<ExtensionManifest>(sourceFile, ExtensionManifestUtility.JsonOptions) ?? ExtensionManifest.Empty;
            Sinks = JsonSerializer.Deserialize<ExtensionManifest>(sinkFile, ExtensionManifestUtility.JsonOptions) ?? ExtensionManifest.Empty;
        }

        private static string GetFileContent(string filename)
        {
            using var resource = Assembly
                .GetExecutingAssembly()
                .GetManifestResourceStream(filename);
            var tr = new StreamReader(resource);

            return tr.ReadToEnd();
        }

        public Task<string> GenerateMigrationFileAsync(string selectedSource, string selectedSink, IEnumerable<ExtensionSetting>? source, IEnumerable<ExtensionSetting>? sink)
        {
            string json = ExtensionManifestUtility.CreateMigrationSettingsJson(selectedSource, selectedSink, source, sink);
            return Task.FromResult(json);
        }

        public Task<AppExtensions> GetExtensionsAsync()
        {
            return Task.FromResult(ExtensionManifestUtility.CombineManifestExtensions(Sources, Sinks));
        }

        public Task<ExtensionSettings> GetSettingsAsync(string name, ExtensionDirection direction)
        {
            var manifest = direction == ExtensionDirection.Sink ? Sinks : Sources;
            return Task.FromResult(manifest.GetExtensionSettings(name));
        }
    }
}