namespace Cosmos.DataTransfer.AzureTableAPIExtension.Settings
{
    public class AzureTableAPIDataSinkSettings : AzureTableAPISettingsBase
    {
        /// <summary>
        /// The Maximum number of concurrent entity writes to the Azure Table API.
        /// This setting is used to control the number of concurrent writes to the Azure Table API.
        /// </summary>
        public int? MaxConcurrentEntityWrites { get; set; }
        
        /// <summary>
        /// Specifies the behavior when writing entities to the table.
        /// Create: Adds new entities only, fails if entity already exists (default).
        /// Replace: Upserts entities, completely replacing existing ones.
        /// Merge: Upserts entities, merging properties with existing ones.
        /// </summary>
        public EntityWriteMode? WriteMode { get; set; } = EntityWriteMode.Create;
    }
}