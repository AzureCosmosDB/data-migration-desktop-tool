using System.ComponentModel.DataAnnotations;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Interfaces.Manifest;
using Microsoft.Azure.Cosmos;

namespace Cosmos.DataTransfer.CosmosExtension
{
    public class CosmosSourceSettings : CosmosSettingsBase, IDataExtensionSettings
    {
        public string? PartitionKeyValue { get; set; }

        public string? Query { get; set; }

        public bool IncludeMetadataFields { get; set; }

        public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            foreach (var item in base.Validate(validationContext))
            {
                yield return item;
            }
        }
    }
}