using Azure.Search.Documents.Models;

namespace Cosmos.DataTransfer.CognitiveSearchExtension.Settings
{
    public class CognitiveSearchDataSinkSettings : CognitiveSearchSettingsBase
    {
        public int BatchSize { get; set; } = 100;

        public IndexActionType IndexAction { get; set; } = IndexActionType.Upload;
    }
}