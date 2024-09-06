using Microsoft.Azure.Cosmos;
using System.ComponentModel.DataAnnotations;

namespace Cosmos.DataTransfer.CosmosExtension
{
    public abstract class CosmosSettingsBase : IValidatableObject
    {
        public string? ConnectionString { get; set; }
        [Required]
        public string? Database { get; set; }
        [Required]
        public string? Container { get; set; }
        public ConnectionMode ConnectionMode { get; set; } = ConnectionMode.Gateway;

        public bool UseRbacAuth { get; set; }
        public string? AccountEndpoint { get; set; }
        public bool EnableInteractiveCredentials { get; set; }

        public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!UseRbacAuth && string.IsNullOrEmpty(ConnectionString))
            {
                yield return new ValidationResult("ConnectionString must be specified unless UseRbacAuth is true", new[] { nameof(ConnectionString) });
            }
            if (UseRbacAuth && string.IsNullOrEmpty(AccountEndpoint))
            {
                yield return new ValidationResult("AccountEndpoint must be specified when UseRbacAuth is true", new[] { nameof(AccountEndpoint) });
            }
        }
    }
}