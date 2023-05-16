using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.Common
{
    public static class ExtensionHelpers
    {
        public static IEnumerable<IDataExtensionSettings> GetCompositeSettings<TFormatter, TStorage>()
            where TFormatter : class, IExtensionWithSettings, new()
            where TStorage : class, IExtensionWithSettings, new()
        {
            var formatter = new TFormatter();
            var source = new TStorage();
            foreach (var settings in formatter.GetSettings().Concat(source.GetSettings()))
            {
                yield return settings;
            }
        }

    }
}