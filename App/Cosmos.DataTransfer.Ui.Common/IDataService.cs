using Cosmos.DataTransfer.Interfaces.Manifest;

namespace Cosmos.DataTransfer.Ui.Common
{
    public interface IDataService
    {
        Task<AppExtensions> GetExtensionsAsync();
        Task<ExtensionSettings> GetSettingsAsync(string name, ExtensionDirection direction);
    }
}
