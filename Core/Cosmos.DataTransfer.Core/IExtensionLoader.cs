using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.Composition.Hosting;

namespace Cosmos.DataTransfer.Core
{
    public interface IExtensionLoader
    {
        CompositionContainer BuildExtensionCatalog(string extensionsPath);
        string GetExtensionFolderPath();
        List<T> LoadExtensions<T>(CompositionContainer container) where T : class, IDataTransferExtension;
    }
}