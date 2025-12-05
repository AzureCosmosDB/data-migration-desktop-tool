using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Encryption;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Cosmos.DataTransfer.CosmosExtension
{
    public static class CosmosExtensionServices
    {
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

        public static CosmosClient CreateClient(CosmosSettingsBase settings, string displayName, ILogger logger, string? sourceDisplayName = null)
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
    }
}