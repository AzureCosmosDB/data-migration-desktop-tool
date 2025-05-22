namespace Cosmos.DataTransfer.AzureTableAPIExtension.Settings
{
    public class AzureTableAPIDataSinkSettings : AzureTableAPISettingsBase
    {
        /// <summary>
        /// The Maximum number of concurrent entity writes to the Azure Table API.
        /// This setting is used to control the number of concurrent writes to the Azure Table API.
        /// </summary>
        public int? MaxConcurrentEntityWrites { get; set; }
    }
}