using System.ComponentModel.DataAnnotations;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Interfaces.Manifest;

namespace Cosmos.DataTransfer.AzureTableAPIExtension.Settings
{
    public abstract class AzureTableAPISettingsBase : IDataExtensionSettings
    {
        /// <summary>
        /// The Connection String.
        /// </summary>
        [SensitiveValue]
        public string? ConnectionString { get; set; }

        public string? AccountEndpoint { get; set; } = null!;

        public bool UseRbacAuth { get; set; }

        public bool EnableInteractiveCredentials { get; set; }

        /// <summary>
        /// The Table name.
        /// </summary>
        [Required]
        public string? Table { get; set; }

        /// <summary>
        /// The field name to translate the RowKey from Table Entities to. (Optional)
        /// </summary>
        public string? RowKeyFieldName { get; set; }

        /// <summary>
        /// The field name to translate the PartitionKey from Table Entities to. (Optional)
        /// </summary>
        public string? PartitionKeyFieldName { get; set; }

        public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!UseRbacAuth && string.IsNullOrEmpty(ConnectionString))
            {
                yield return new ValidationResult($"{nameof(ConnectionString)} must be specified unless {nameof(UseRbacAuth)} is true", new[] { nameof(ConnectionString) });
            }

            if (UseRbacAuth && string.IsNullOrEmpty(AccountEndpoint))
            {
                yield return new ValidationResult($"{nameof(AccountEndpoint)} must be specified unless {nameof(UseRbacAuth)} is false", new[] { nameof(AccountEndpoint) });
            }
        }
    }
}