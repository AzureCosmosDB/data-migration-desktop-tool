using Azure.Identity;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Reflection;
using Azure.Core;
using System.Text.RegularExpressions;
using Microsoft.Azure.Cosmos.Encryption;
using Azure.Security.KeyVault.Keys.Cryptography;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Cosmos.DataTransfer.CosmosExtension
{
    public static class CosmosExtensionServices
    {
        public static CosmosClient CreateClient(CosmosSettingsBase settings, string displayName, string? sourceDisplayName = null)
        {
            string userAgentString = CreateUserAgentString(displayName, sourceDisplayName);

            var cosmosSerializer = new RawJsonCosmosSerializer();
            if (settings is CosmosSinkSettings sinkSettings)
            {
                cosmosSerializer.SerializerSettings.NullValueHandling = sinkSettings.IgnoreNullValues
                    ? Newtonsoft.Json.NullValueHandling.Ignore
                    : Newtonsoft.Json.NullValueHandling.Include;
            }

            var clientOptions = new CosmosClientOptions
            {
                ConnectionMode = settings.ConnectionMode,
                ApplicationName = userAgentString,
                AllowBulkExecution = true,
                EnableContentResponseOnWrite = false,
                Serializer = cosmosSerializer,
                LimitToEndpoint = settings.LimitToEndpoint,
            };

            if (!string.IsNullOrEmpty(settings.WebProxy)){
                clientOptions.WebProxy = new WebProxy(settings.WebProxy);
            }

            // Configure custom certificate validation
            if (settings.DisableSslValidation || !string.IsNullOrEmpty(settings.CertificatePath))
            {
                clientOptions.ServerCertificateCustomValidationCallback = CreateCertificateValidationCallback(settings);
            }
            
            CosmosClient? cosmosClient;
            if (settings.UseRbacAuth)
            {
                TokenCredential tokenCredential = new DefaultAzureCredential(includeInteractiveCredentials: settings.EnableInteractiveCredentials);

                if(settings.InitClientEncryption)
                {
                    var keyResolver = new KeyResolver(tokenCredential);
                    cosmosClient = new CosmosClient(settings.AccountEndpoint, tokenCredential, clientOptions)
                        .WithEncryption(keyResolver, KeyEncryptionKeyResolverName.AzureKeyVault);
                }
                else
                {
                    cosmosClient = new CosmosClient(settings.AccountEndpoint, tokenCredential, clientOptions);
                }
            }
            else
            {
                cosmosClient = new CosmosClient(settings.ConnectionString, clientOptions);
            }

            return cosmosClient;
        }

        private static string CreateUserAgentString(string displayName, string? sourceDisplayName)
        {
            // based on:
            //UserAgentSuffix = String.Format(CultureInfo.InvariantCulture, Resources.CustomUserAgentSuffixFormat,
            //    entryAssembly == null ? Resources.UnknownEntryAssembly : entryAssembly.GetName().Name,
            //    Assembly.GetExecutingAssembly().GetName().Version,
            //    context.SourceName, context.SinkName,
            //    isShardedImport ? Resources.ShardedImportDesignator : String.Empty)
            string sourceName = StripSpecialChars(sourceDisplayName ?? "");
            string sinkName = StripSpecialChars(displayName);

            var entryAssembly = Assembly.GetEntryAssembly();
            bool isShardedImport = false;
            string userAgentString = string.Format(CultureInfo.InvariantCulture, "{0}-{1}-{2}-{3}{4}",
                entryAssembly == null ? "dtr" : entryAssembly.GetName().Name,
                Assembly.GetExecutingAssembly().GetName().Version,
                sourceName, sinkName,
                isShardedImport ? "-Sharded" : string.Empty);
            return userAgentString;
        }
        private static string StripSpecialChars(string displayName)
        {
            return Regex.Replace(displayName, "[^\\w]", "", RegexOptions.Compiled);
        }

        public static async Task VerifyContainerAccess(Container? container, string? name, ILogger logger, CancellationToken cancellationToken)
        {
            if (container == null)
            {
                logger.LogError("Failed to initialize Container {Container}", name);
                throw new Exception("Cosmos container unavailable for write");
            }

            try
            {
                var props = await container.ReadContainerAsync(cancellationToken: cancellationToken);
                if (props.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    throw new Exception("Unable to read Container properties");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to connect to CosmosDB. Please check your connection settings and try again.");
                throw new InvalidOperationException("Failed to create CosmosClient");
            }
        }

        private static Func<X509Certificate2, X509Chain, SslPolicyErrors, bool> CreateCertificateValidationCallback(CosmosSettingsBase settings)
        {
            return (cert, chain, errors) =>
            {
                // If SSL validation is disabled (development/emulator only)
                if (settings.DisableSslValidation)
                {
                    return true;
                }

                // If a certificate path is specified
                if (!string.IsNullOrEmpty(settings.CertificatePath))
                {
                    try
                    {
                        bool isPfxCertificate = IsPfxCertificate(settings.CertificatePath);
                        
                        // Load certificate based on type and password availability
                        using (X509Certificate2 trustedCert = isPfxCertificate
                            ? (string.IsNullOrEmpty(settings.CertificatePassword)
                                ? new X509Certificate2(settings.CertificatePath)
                                : new X509Certificate2(settings.CertificatePath, settings.CertificatePassword))
                            : new X509Certificate2(settings.CertificatePath))
                        {
                            // Compare certificate thumbprints (most reliable method)
                            bool thumbprintMatch = cert.Thumbprint.Equals(trustedCert.Thumbprint, StringComparison.OrdinalIgnoreCase);
                            
                            if (!thumbprintMatch)
                            {
                                // For PFX certificates, also check if the server cert was issued by our trusted cert
                                // This handles cases where the PFX is a CA certificate
                                if (isPfxCertificate)
                                {
                                    try
                                    {
                                        using (var certChain = new X509Chain())
                                        {
                                            certChain.ChainPolicy.ExtraStore.Add(trustedCert);
                                            certChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                                            certChain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                                            
                                            bool chainIsValid = certChain.Build(cert);
                                            return chainIsValid && certChain.ChainElements
                                                .Cast<X509ChainElement>()
                                                .Any(element => element.Certificate.Thumbprint.Equals(trustedCert.Thumbprint, StringComparison.OrdinalIgnoreCase));
                                        }
                                    }
                                    catch
                                    {
                                        // Fallback to subject and issuer comparison
                                        bool subjectMatch = cert.Subject.Equals(trustedCert.Subject, StringComparison.OrdinalIgnoreCase);
                                        bool issuerMatch = cert.Issuer.Equals(trustedCert.Issuer, StringComparison.OrdinalIgnoreCase);
                                        return subjectMatch && issuerMatch;
                                    }
                                }
                                else
                                {
                                    // For standard certificates, try comparing by subject and issuer as fallback
                                    bool subjectMatch = cert.Subject.Equals(trustedCert.Subject, StringComparison.OrdinalIgnoreCase);
                                    bool issuerMatch = cert.Issuer.Equals(trustedCert.Issuer, StringComparison.OrdinalIgnoreCase);
                                    return subjectMatch && issuerMatch;
                                }
                            }
                            
                            return thumbprintMatch;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the exception details to help diagnose certificate loading issues
                        Console.Error.WriteLine($"Certificate loading failed: {ex.Message}\n{ex.StackTrace}");
                        // If we can't load the certificate, fail validation
                        return false;
                    }
                }

                // Default validation - accept only if no SSL policy errors
                return errors == SslPolicyErrors.None;
            };
        }

        private static bool IsPfxCertificate(string certificatePath)
        {
            var extension = Path.GetExtension(certificatePath).ToLowerInvariant();
            return extension == ".pfx" || extension == ".p12";
        }
    }
}