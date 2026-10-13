using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Encryption;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace Cosmos.DataTransfer.CosmosExtension
{
    public static class CosmosExtensionServices
    {
        internal enum TokenCredentialSelection
        {
            DefaultAzureCredential,
            ClientSecretCredential,
            ClientCertificateCredential,
        }

        // Static HttpClient instances with different configurations for reuse across connections
        // This avoids connection exhaustion and properly handles credentials
        private static readonly Lazy<HttpClient> _httpClientWithDefaultCredentials = new Lazy<HttpClient>(() =>
        {
            var handler = new HttpClientHandler
            {
                Credentials = CredentialCache.DefaultNetworkCredentials,
                PreAuthenticate = false
            };
            return new HttpClient(handler);
        });

        private static readonly Lazy<HttpClient> _httpClientWithDefaultCredentialsAndPreAuth = new Lazy<HttpClient>(() =>
        {
            var handler = new HttpClientHandler
            {
                Credentials = CredentialCache.DefaultNetworkCredentials,
                PreAuthenticate = true
            };
            return new HttpClient(handler);
        });

        // NOTE: Kept as a separate helper so auth-path behavior can be tested directly.
        internal static TokenCredentialSelection GetTokenCredentialSelection(CosmosSettingsBase settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.TenantId) && !string.IsNullOrWhiteSpace(settings.ClientId))
            {
                if (!string.IsNullOrWhiteSpace(settings.ClientSecret))
                {
                    return TokenCredentialSelection.ClientSecretCredential;
                }

                if (!string.IsNullOrWhiteSpace(settings.ClientCertificatePath))
                {
                    return TokenCredentialSelection.ClientCertificateCredential;
                }
            }

            return TokenCredentialSelection.DefaultAzureCredential;
        }

        // NOTE: Added explicit exception wrapping to surface actionable auth configuration failures.
        internal static TokenCredential CreateRbacTokenCredential(CosmosSettingsBase settings, ILogger logger)
        {
            var section = settings is CosmosSinkSettings ? "SinkSettings" : "SourceSettings";

            try
            {
                var selection = GetTokenCredentialSelection(settings);
                switch (selection)
                {
                    case TokenCredentialSelection.ClientSecretCredential:
                    {
                        logger.LogWarning(
                            "ClientSecret is configured in settings. Ensure this configuration file is not committed to source control. Consider injecting via environment variables, command-line args (--{Section}:ClientSecret=...), or User Secrets instead.",
                            section);
                        return new ClientSecretCredential(settings.TenantId!, settings.ClientId!, settings.ClientSecret!);
                    }

                    case TokenCredentialSelection.ClientCertificateCredential:
                        if (!File.Exists(settings.ClientCertificatePath))
                        {
                            throw new FileNotFoundException(
                                "Client certificate file was not found.",
                                settings.ClientCertificatePath);
                        }
                        var certificatePassword = string.IsNullOrWhiteSpace(settings.ClientCertificatePassword)
                            ? null
                            : settings.ClientCertificatePassword;

                        if (certificatePassword is not null)
                        {
                            logger.LogWarning(
                                "ClientCertificatePassword is configured in settings. Ensure this configuration file is not committed to source control. Consider injecting via environment variables, command-line args (--{Section}:ClientCertificatePassword=...), or User Secrets instead.",
                                section);
                        }

                        var certificate = new X509Certificate2(
                            settings.ClientCertificatePath!,
                            certificatePassword,
                            X509KeyStorageFlags.EphemeralKeySet);

                        if (!certificate.HasPrivateKey)
                        {
                            throw new CryptographicException("Client certificate must contain a private key.");
                        }

                        return new ClientCertificateCredential(settings.TenantId!, settings.ClientId!, certificate);

                    default:
                        return new DefaultAzureCredential(includeInteractiveCredentials: settings.EnableInteractiveCredentials);
                }
            }
            catch (Exception ex) when (
                ex is CryptographicException ||
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is ArgumentException)
            {
                throw new InvalidOperationException(
                    $"Failed to configure RBAC credentials from {section}. Validate TenantId/ClientId and service principal secret/certificate settings.",
                    ex);
            }
        }

        internal static CosmosClientOptions CreateClientOptions(CosmosSettingsBase settings, string userAgentString, ILogger logger)
        {
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
                AllowBulkExecution = settings.AllowBulkExecution,
                EnableContentResponseOnWrite = false,
                Serializer = cosmosSerializer,
                LimitToEndpoint = settings.LimitToEndpoint,
            };

            if (!string.IsNullOrEmpty(settings.WebProxy))
            {
                var webProxy = new WebProxy(settings.WebProxy);
                if (settings.UseDefaultProxyCredentials)
                {
                    webProxy.UseDefaultCredentials = true;
                }
                clientOptions.WebProxy = webProxy;
            }

            // Configure the HttpClient with default credentials if requested
            // This enables authenticated proxy support for the underlying HTTP connections
            if (settings.UseDefaultCredentials)
            {
                clientOptions.HttpClientFactory = settings.PreAuthenticate
                    ? () => _httpClientWithDefaultCredentialsAndPreAuth.Value
                    : () => _httpClientWithDefaultCredentials.Value;
            }

            // Disable SSL certificate validation for development scenarios
            if (settings.DisableSslValidation)
            {
                logger.LogWarning("SSL certificate validation is DISABLED. This should ONLY be used for development scenarios. Never use in production.");
                clientOptions.ServerCertificateCustomValidationCallback = (cert, chain, errors) => true;
            }

            return clientOptions;
        }

        public static CosmosClient CreateClient(CosmosSettingsBase settings, string displayName, ILogger logger, string? sourceDisplayName = null)
        {
            string userAgentString = CreateUserAgentString(displayName, sourceDisplayName);
            var clientOptions = CreateClientOptions(settings, userAgentString, logger);
            
            CosmosClient? cosmosClient;
            if (settings.UseRbacAuth)
            {
                var tokenCredential = CreateRbacTokenCredential(settings, logger);

                cosmosClient = settings.InitClientEncryption
                    ? new CosmosClient(settings.AccountEndpoint, tokenCredential, clientOptions)
                        .WithEncryption(new KeyResolver(tokenCredential), KeyEncryptionKeyResolverName.AzureKeyVault)
                    : new CosmosClient(settings.AccountEndpoint, tokenCredential, clientOptions);
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
    }
}