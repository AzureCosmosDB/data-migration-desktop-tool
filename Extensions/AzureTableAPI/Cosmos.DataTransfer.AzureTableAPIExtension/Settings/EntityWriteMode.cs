namespace Cosmos.DataTransfer.AzureTableAPIExtension.Settings
{
    /// <summary>
    /// Defines the behavior when writing entities to Azure Table API.
    /// </summary>
    public enum EntityWriteMode
    {
        /// <summary>
        /// Creates new entities only. Fails if an entity with the same partition key and row key already exists.
        /// Uses AddEntityAsync method.
        /// </summary>
        Create,
        
        /// <summary>
        /// Replaces existing entities or creates new ones. Completely replaces the entity in the table.
        /// Uses UpsertEntityAsync with TableUpdateMode.Replace.
        /// </summary>
        Replace,
        
        /// <summary>
        /// Merges with existing entities or creates new ones. Merges the properties of the supplied entity with the entity in the table.
        /// Uses UpsertEntityAsync with TableUpdateMode.Merge.
        /// </summary>
        Merge
    }
}