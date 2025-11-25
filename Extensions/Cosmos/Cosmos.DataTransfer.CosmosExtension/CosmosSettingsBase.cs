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
        public string? WebProxy { get; set; }
        public bool UseRbacAuth { get; set; }
        public string? AccountEndpoint { get; set; }
        public bool EnableInteractiveCredentials { get; set; }
        public bool InitClientEncryption { get; set; } = false;
        
        /// <summary>
        /// <see cref="CosmosClientOptions.LimitToEndpoint"/>
        /// When running the Azure Cosmos DB emulator in a Linux Container on Windows
        /// a value of false results in failure to connect to Cosmos DB emulator. 
        /// </summary>
        public bool LimitToEndpoint { get; set; } = false;

        /// <summary>
        /// Disables SSL certificate validation for the Cosmos DB connection.
        /// This is intended for use with local development environments.
        /// WARNING: Never use this option in production as it disables critical security checks.
        /// </summary>
        public bool DisableSslValidation { get; set; } = false;

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
            if (!UseRbacAuth && InitClientEncryption)
            {
                yield return new ValidationResult("InitClientEncryption can only be used when UseRbacAuth is true", new[] { nameof(InitClientEncryption) });
            }
        }
    }
}