using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Cosmos.DataTransfer.Interfaces.Manifest;

namespace Cosmos.DataTransfer.Ui.Common
{
    public static class ExtensionManifestUtility
    {
        public static JsonSerializerOptions JsonOptions => new()
        {
            Converters = { new JsonStringEnumConverter() },
            WriteIndented = true
        };

        public static ExtensionSettings GetExtensionSettings(this ExtensionManifest manifest, string name)
        {
            var extension = manifest.Extensions.FirstOrDefault(e => e.Name == name);

            var settings = extension?.Settings
                .OrderByDescending(p => p.IsRequired)
                .Select(d => new ExtensionSetting(d))
                .ToList() ?? new List<ExtensionSetting>();

            if (!settings.Any())
            {
                settings.Add(new ExtensionSetting(ExtensionSettingProperty.Empty));
            }

            return new ExtensionSettings(new ExtensionDefinition(name), settings);
        }

        public static Dictionary<string, object?> GetSettingValues(this IEnumerable<ExtensionSetting>? settings)
        {
            var settingValues = new Dictionary<string, object?>();
            if (settings != null)
            {
                foreach (ExtensionSetting extensionSetting in settings)
                {
                    bool isValid;
                    try
                    {
                        isValid = Validate(extensionSetting);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Invalid setting: {ex.Message}");
                    }
                    if (!isValid)
                    {
                        throw new Exception($"Invalid value for setting '{extensionSetting.Definition.Name}'");
                    }

                    if (extensionSetting.Definition.Type == PropertyType.Undeclared)
                    {
                        var properties = ParseUndeclaredProperties(extensionSetting);
                        if (properties != null)
                        {
                            foreach (var item in properties)
                            {
                                settingValues.Add(item.Key, item.Value);
                            }
                        }
                    }
                    else if (extensionSetting.Definition.Type == PropertyType.Array)
                    {
                        settingValues.Add(extensionSetting.Definition.Name, ParseGenericArrayProperty(extensionSetting));
                    }
                    else if (extensionSetting.Value != null)
                    {
                        settingValues.Add(extensionSetting.Definition.Name, extensionSetting.Value);
                    }
                }
            }

            return settingValues;
        }

        public static bool Validate(this ExtensionSetting extensionSetting)
        {
            if (extensionSetting.Definition.IsRequired)
            {
                if (extensionSetting.Value == null)
                    return false;

                if (extensionSetting.Definition.Type == PropertyType.String && string.IsNullOrWhiteSpace(extensionSetting.Value.ToString()))
                    return false;
            }

            if (extensionSetting.Definition.ValidValues.Any())
            {
                string? textValue = extensionSetting.Value?.ToString();
                if (textValue == null)
                    return false;

                if (!extensionSetting.Definition.ValidValues.Contains(textValue))
                    return false;
            }

            if (extensionSetting.Definition.Type == PropertyType.Undeclared)
            {
                var properties = ParseUndeclaredProperties(extensionSetting, true);
                if (properties == null)
                    return false;
            }

            return true;
        }

        private static Dictionary<string, object?>? ParseUndeclaredProperties(ExtensionSetting extensionSetting, bool throwOnError = false)
        {
            Dictionary<string, object?>? properties = null;
            try
            {
                string json = "{" + extensionSetting.Value?.ToString()?.Trim('{', '}', '[', ']') + "}";
                properties = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, new JsonSerializerOptions
                {
                    AllowTrailingCommas = true,
                });
            }
            catch (Exception ex)
            {
                if (throwOnError)
                    throw;
            }

            return properties;
        }

        private static JsonNode? ParseGenericArrayProperty(ExtensionSetting extensionSetting, bool throwOnError = false)
        {
            try
            {
                string json = "[" + extensionSetting.Value?.ToString()?.Trim('[', ']') + "]";
                var node = JsonNode.Parse(json);
                return node;
            }
            catch (Exception ex)
            {
                if (throwOnError)
                    throw;
            }

            return null;
        }

        public static string CreateMigrationSettingsJson(string selectedSource, string selectedSink, IEnumerable<ExtensionSetting>? source, IEnumerable<ExtensionSetting>? sink)
        {
            Dictionary<string, object?> sourceSettings;
            try
            {
                sourceSettings = source.GetSettingValues();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Source settings error: {ex.Message}", ex);
            }

            Dictionary<string, object?> sinkSettings;
            try
            {
                sinkSettings = sink.GetSettingValues();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Sink settings error: {ex.Message}", ex);
            }
            var migrationSettings = new MigrationSettings
            {
                Source = selectedSource,
                Sink = selectedSink,
                SourceSettings = sourceSettings,
                SinkSettings = sinkSettings,
            };

            var json = JsonSerializer.Serialize(migrationSettings, JsonOptions);
            return json;
        }
        
        public static string CreateRunCommandJson(string selectedSource, string selectedSink, IEnumerable<ExtensionSetting>? source, IEnumerable<ExtensionSetting>? sink)
        {
            Dictionary<string, object?> sourceSettings;
            try
            {
                sourceSettings = source.GetSettingValues();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Source settings error: {ex.Message}", ex);
            }

            Dictionary<string, object?> sinkSettings;
            try
            {
                sinkSettings = sink.GetSettingValues();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Sink settings error: {ex.Message}", ex);
            }

            var settingParams = GetSettingParams(sourceSettings, "SourceSettings");
            settingParams.AddRange(GetSettingParams(sinkSettings, "SinkSettings"));

            var command = $"dmt run --source {selectedSource} --sink {selectedSink}";
            if (settingParams.Any())
            {
                command = $"{command} {string.Join(" ", settingParams)}";
            }

            return command;
        }

        private static List<string> GetSettingParams(Dictionary<string, object?> sourceSettings, string settingContainer)
        {
            var sourceSettingParams = new List<string>();
            foreach (var setting in sourceSettings)
            {
                sourceSettingParams.Add($"--{settingContainer}:{setting.Key}={setting.Value}");
            }

            return sourceSettingParams;
        }

        public static AppExtensions CombineManifestExtensions(ExtensionManifest sourceManifest, ExtensionManifest sinkManifest)
        {
            var sources = sourceManifest.Extensions.Select(e => new ExtensionDefinition(e.Name));
            var sinks = sinkManifest.Extensions.Select(e => new ExtensionDefinition(e.Name));

            return new AppExtensions(sources, sinks);
        }
    }
}
