using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.JsonExtension.Settings
{
    public class JsonFormatWriterSettings : IDataExtensionSettings
    {
        public bool IncludeNullFields { get; set; }
        public bool Indented { get; set; }
        public int BufferSizeMB { get; set; } = 200;
    }
}