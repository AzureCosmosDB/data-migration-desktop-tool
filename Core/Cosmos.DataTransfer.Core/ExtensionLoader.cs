using System.ComponentModel.Composition.Hosting;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.Core
{
    public class ExtensionLoader : IExtensionLoader
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;

        public ExtensionLoader(IConfiguration configuration, ILogger<ExtensionLoader> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public string GetExtensionFolderPath()
        {
            return GetExtensionFolderPath(_configuration, _logger);
        }

        public static string GetExtensionFolderPath(IConfiguration configuration, ILogger logger)
        {
            var configPath = configuration.GetValue<string>("ExtensionsPath");
            if (!string.IsNullOrWhiteSpace(configPath))
            {
                try
                {
                    var fullPath = Path.GetFullPath(configPath);
                    if (!Directory.Exists(fullPath))
                    {
                        Directory.CreateDirectory(fullPath);
                    }

                    return fullPath;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Configured path {ExtensionsPath} is invalid. Using default instead.", configPath);
                }
            }

            var exeFolder = AppContext.BaseDirectory;
            var path = Path.Combine(exeFolder, "Extensions");
            var di = new DirectoryInfo(path);
            if (!di.Exists)
            {
                di.Create();
            }
            return di.FullName;
        }

        public CompositionContainer BuildExtensionCatalog(string extensionsPath)
        {
            var catalog = new AggregateCatalog();
            _logger.LogInformation("Loading extensions from {ExtensionsPath}", extensionsPath);
            catalog.Catalogs.Add(new DirectoryCatalog(extensionsPath, "*Extension.dll"));
            return new CompositionContainer(catalog);
        }

        public List<T> LoadExtensions<T>(CompositionContainer container)
            where T : class, IDataTransferExtension
        {
            var sources = new List<T>();

            foreach (var exportedExtension in container.GetExports<T>())
            {
                _logger.LogDebug("Loaded extension {ExtensionName} as {ExtensionType}", exportedExtension.Value.DisplayName, typeof(T).Name);
                sources.Add(exportedExtension.Value);
            }

            _logger.LogInformation("{ExtensionCount} Extensions Loaded for type {ExtensionType}", sources.Count, typeof(T).Name);

            return sources;
        }
    }
}