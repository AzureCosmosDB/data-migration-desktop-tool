using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Interfaces.Manifest;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel.Composition.Hosting;
using System.Reflection;

namespace Cosmos.DataTransfer.Core
{
    public class ExtensionManifestBuilder : IExtensionManifestBuilder
    {
        private readonly ILogger _logger;
        private readonly IExtensionLoader _extensionLoader;
        private static string? _appVersion;

        public ExtensionManifestBuilder(IExtensionLoader extensionLoader, ILogger<ExtensionManifestBuilder> logger)
        {
            _extensionLoader = extensionLoader;
            _logger = logger;
        }

        public List<IDataSourceExtension> GetSources()
        {
            string extensionsPath = _extensionLoader.GetExtensionFolderPath();
            CompositionContainer container = _extensionLoader.BuildExtensionCatalog(extensionsPath);

            return _extensionLoader.LoadExtensions<IDataSourceExtension>(container);
        }

        public List<IDataSinkExtension> GetSinks()
        {
            string extensionsPath = _extensionLoader.GetExtensionFolderPath();
            CompositionContainer container = _extensionLoader.BuildExtensionCatalog(extensionsPath);

            return _extensionLoader.LoadExtensions<IDataSinkExtension>(container);
        }

        public ExtensionManifest BuildManifest(ExtensionDirection direction)
        {
            var extensions = new List<IDataTransferExtension>();
            if (direction == ExtensionDirection.Source)
            {
                extensions.AddRange(GetSources());
            }
            else
            {
                extensions.AddRange(GetSinks());
            }
            var manifest = new ExtensionManifest(AppVersion, extensions
                .Select(e =>
                {
                    var assembly = Assembly.GetAssembly(e.GetType());
                    var version = assembly != null ? GetAssemblyVersion(assembly) : null;
                    var assemblyName = assembly?.GetName().Name;

                    return new ExtensionManifestItem(e.DisplayName,
                        direction,
                        version,
                        assemblyName,
                        GetExtensionSettings(e as IExtensionWithSettings));
                }).ToList());
            return manifest;
        }

        public List<ExtensionSettingProperty> GetExtensionSettings(IExtensionWithSettings? extension)
        {
            var allProperties = new List<ExtensionSettingProperty>();
            if (extension != null)
            {
                var allSettings = extension.GetSettings();
                foreach (IDataExtensionSettings settings in allSettings)
                {
                    var settingsType = settings.GetType();

                    var props = settingsType.GetProperties();
                    foreach (PropertyInfo propertyInfo in props)
                    {
                        var defaultValue = propertyInfo.GetValue(settings);
                        var settingProperty = new ExtensionSettingProperty(propertyInfo.Name, GetPropertyType(propertyInfo.PropertyType))
                        {
                            IsRequired = propertyInfo.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>() is not null,
                            DefaultValue = defaultValue,
                            IsSensitive = propertyInfo.GetCustomAttribute<SensitiveValueAttribute>() is not null
                        };
                        if (settingProperty.Type == PropertyType.Enum)
                        {
                            settingProperty.ValidValues.AddRange(GetPropertyEnumValues(propertyInfo, settings));
                        }
                        allProperties.Add(settingProperty);
                    }
                }
            }

            return allProperties;
        }

        private IEnumerable<string> GetPropertyEnumValues(PropertyInfo propertyInfo, IDataExtensionSettings settings)
        {
            if (propertyInfo.PropertyType.IsEnum)
            {
                return Enum.GetNames(propertyInfo.PropertyType);
            }

            return Enumerable.Empty<string>();
        }

        private static PropertyType GetPropertyType(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var genericType = type.GetGenericArguments().FirstOrDefault();
                if (genericType != null)
                    type = genericType;
            }

            if (type == typeof(byte) || type == typeof(short) || type == typeof(ushort) ||
                type == typeof(int) || type == typeof(uint) ||
                type == typeof(long) || type == typeof(ulong))
            {
                return PropertyType.Int;
            }

            if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
            {
                return PropertyType.Float;
            }

            if (type == typeof(bool))
            {
                return PropertyType.Boolean;
            }

            if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            {
                return PropertyType.DateTime;
            }

            if (type.IsEnum)
            {
                return PropertyType.Enum;
            }

            if (type == typeof(string))
            {
                return PropertyType.String;
            }

            if (type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                return PropertyType.Array;
            }

            return PropertyType.String;
        }

        private static string AppVersion => _appVersion ??= GetAppVersion();

        private static string GetAppVersion()
        {
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

            return GetAssemblyVersion(assembly);
        }

        private static string GetAssemblyVersion(Assembly assembly)
        {
            var assemblyVersionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

            if (assemblyVersionAttribute is null)
            {
                return assembly.GetName().Version?.ToString() ?? "";
            }
            else
            {
                return assemblyVersionAttribute.InformationalVersion;
            }
        }
    }
}