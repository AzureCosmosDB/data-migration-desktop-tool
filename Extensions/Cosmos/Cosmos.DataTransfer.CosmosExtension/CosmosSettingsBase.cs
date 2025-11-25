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
        /// Path to a certificate file for SSL validation and client authentication.
        /// Supports multiple formats:
        /// - .cer, .crt, .pem files for basic SSL validation
        /// - .pfx, .p12 files for client authentication (enterprise scenarios)
        /// For PFX/P12 files, use CertificatePassword if the file is password-protected.
        /// </summary>
        public string? CertificatePath { get; set; }

        /// <summary>
        /// Password for PFX/P12 certificate files when they are password-protected.
        /// Only used when CertificatePath points to a .pfx or .p12 file.
        /// WARNING: Store this securely and avoid hardcoding in configuration files.
        /// </summary>
        public string? CertificatePassword { get; set; }

        /// <summary>
        /// Disable SSL certificate validation.
        /// WARNING: Only use this for development with the emulator. Never use in production.
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
            if (!string.IsNullOrEmpty(CertificatePath) && !File.Exists(CertificatePath))
            {
                yield return new ValidationResult($"CertificatePath file does not exist: {CertificatePath}", new[] { nameof(CertificatePath) });
            }
            if (!string.IsNullOrEmpty(CertificatePassword) && string.IsNullOrEmpty(CertificatePath))
            {
                yield return new ValidationResult("CertificatePassword is specified but CertificatePath is missing", new[] { nameof(CertificatePassword) });
            }
        }
    }
}