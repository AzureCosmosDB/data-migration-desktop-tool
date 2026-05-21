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
        public bool UseDefaultProxyCredentials { get; set; } = false;
        public bool UseDefaultCredentials { get; set; } = false;
        public bool PreAuthenticate { get; set; } = false;
        public bool UseRbacAuth { get; set; }
        public string? AccountEndpoint { get; set; }
        public bool EnableInteractiveCredentials { get; set; }
        public bool InitClientEncryption { get; set; } = false;

        public string? TenantId { get; set; }
        public string? ClientId { get; set; }

        public string? ClientSecret { get; set; }

        public string? ClientCertificatePath { get; set; }
        public string? ClientCertificatePassword { get; set; }

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

        /// <summary>
        /// Enables bulk execution for Cosmos DB operations.
        /// When set to <c>true</c>, operations such as bulk inserts and updates are optimized for performance.
        /// <para>
        /// <b>Default:</b> <c>false</c>
        /// </para>
        /// <para>
        /// <b>Warning:</b> Use with caution, as enabling bulk execution may affect consistency and error handling. 
        /// Review Cosmos DB documentation for bulk operation caveats before enabling in production environments.
        /// </para>
        /// </summary>
        public bool AllowBulkExecution { get; set; } = false;

        public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!UseRbacAuth && string.IsNullOrEmpty(ConnectionString))
            {
                yield return new ValidationResult("ConnectionString must be specified unless UseRbacAuth is true", [nameof(ConnectionString)]);
            }
            if (UseRbacAuth && string.IsNullOrEmpty(AccountEndpoint))
            {
                yield return new ValidationResult("AccountEndpoint must be specified when UseRbacAuth is true", [nameof(AccountEndpoint)]);
            }
            if (!UseRbacAuth && InitClientEncryption)
            {
                yield return new ValidationResult("InitClientEncryption can only be used when UseRbacAuth is true", [nameof(InitClientEncryption)]);
            }
            var tenantIdSet = !string.IsNullOrEmpty(TenantId);
            var clientIdSet = !string.IsNullOrEmpty(ClientId);
            var servicePrincipalSet = tenantIdSet && clientIdSet;
            if (UseRbacAuth && tenantIdSet != clientIdSet)
            {
                yield return new ValidationResult("Both TenantId and ClientId must be specified when UseRbacAuth is used with service principal",
                    [nameof(TenantId), nameof(ClientId)]);
            }
            var clientSecretSet = !string.IsNullOrEmpty(ClientSecret);
            var clientCertificateSet = !string.IsNullOrEmpty(ClientCertificatePath);
            if (UseRbacAuth && servicePrincipalSet && !clientSecretSet && !clientCertificateSet)
            {
                yield return new ValidationResult("Either ClientSecret or ClientCertificatePath must be specified when UseRbacAuth is used with service principal",
                    [nameof(ClientSecret), nameof(ClientCertificatePath)]);
            }
            if (UseRbacAuth && servicePrincipalSet && clientSecretSet && clientCertificateSet)
            {
                yield return new ValidationResult("Specify either ClientSecret or ClientCertificatePath, not both.",
                    [nameof(ClientSecret), nameof(ClientCertificatePath)]);
            }
            if (!UseRbacAuth && (tenantIdSet || clientIdSet || clientSecretSet || clientCertificateSet))
            {
                yield return new ValidationResult("Service principal settings require UseRbacAuth to be set to true.", [nameof(UseRbacAuth)]);
            }
        }
    }
}