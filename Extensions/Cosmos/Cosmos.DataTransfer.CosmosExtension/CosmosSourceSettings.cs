using System.ComponentModel.DataAnnotations;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Interfaces.Manifest;
using Microsoft.Azure.Cosmos;

namespace Cosmos.DataTransfer.CosmosExtension
{
    public class CosmosSourceSettings : IDataExtensionSettings
    {
        [Required]
        [SensitiveValue]
        public string? ConnectionString { get; set; }
        [Required]
        public string? Database { get; set; }
        [Required]
        public string? Container { get; set; }

        public string? PartitionKeyValue { get; set; }

        public string? Query { get; set; }

        public bool IncludeMetadataFields { get; set; }
        public ConnectionMode ConnectionMode { get; set; } = ConnectionMode.Gateway;
    }
}