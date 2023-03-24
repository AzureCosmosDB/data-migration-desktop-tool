namespace Cosmos.DataTransfer.CognitiveSearchExtension.Settings
{
    public class CognitiveSearchDataSourceSettings : CognitiveSearchSettingsBase
    {
        /// <summary>
        /// OData $filter syntax in Azure Cognitive Search
        /// https://learn.microsoft.com/en-us/azure/search/search-query-odata-filter
        /// </summary>
        public string? ODataFilter { get; set; }
    }
}