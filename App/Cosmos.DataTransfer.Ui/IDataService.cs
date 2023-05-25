using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cosmos.DataTransfer.Interfaces.Manifest;

namespace Cosmos.DataTransfer.Ui
{
    public interface IDataService
    {
        Task<AppExtensions> GetExtensionsAsync();
        Task<ExtensionSettings> GetSettingsAsync(string name, ExtensionDirection direction);
    }
}
