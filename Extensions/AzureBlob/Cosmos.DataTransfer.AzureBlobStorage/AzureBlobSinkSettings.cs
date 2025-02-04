using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Interfaces.Manifest;
using System.ComponentModel.DataAnnotations;

namespace Cosmos.DataTransfer.AzureBlobStorage
{
    public class AzureBlobSinkSettings : IDataExtensionSettings, IValidatableObject
    {
        [SensitiveValue]
        public string? ConnectionString { get; set; } = null!;

        public string? AccountEndpoint { get; set; } = null!;

        [Required]
        public string ContainerName { get; set; } = null!;

        [Required]
        public string BlobName { get; set; } = null!;

        public int? MaxBlockSizeinKB { get; set; }

        public bool UseRbacAuth { get; set; }

        public bool EnableInteractiveCredentials { get; set; }

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