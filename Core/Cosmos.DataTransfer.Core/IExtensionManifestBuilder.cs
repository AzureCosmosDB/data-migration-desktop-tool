using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Interfaces.Manifest;
using System;

namespace Cosmos.DataTransfer.Core
{
    public interface IExtensionManifestBuilder
    {
        ExtensionManifest BuildManifest(ExtensionDirection direction);
        List<ExtensionSettingProperty> GetExtensionSettings(IExtensionWithSettings? extension);
        List<IDataSinkExtension> GetSinks();
        List<IDataSourceExtension> GetSources();
    }
}