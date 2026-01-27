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

        /// <summary>
        /// Gets or sets whether to only return headers and status code in the Cosmos DB response 
        /// for write item operations like Create, Upsert, Patch and Replace.
        /// Setting to <c>false</c> will cause the response to have a null resource, 
        /// reducing networking and CPU load by not sending the resource back over the network 
        /// and serializing it on the client.
        /// <para>
        /// This setting applies to sink (write) operations.
        /// </para>
        /// <para>
        /// <b>Default:</b> <c>true</c>
        /// </para>
        /// <para>
        /// For more information, see 
        /// <see href="https://learn.microsoft.com/dotnet/api/microsoft.azure.cosmos.cosmosclientoptions.enablecontentresponseonwrite">
        /// CosmosClientOptions.EnableContentResponseOnWrite Property
        /// </see>
        /// </para>
        /// </summary>
        public bool EnableContentResponseOnWrite { get; set; } = true;

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