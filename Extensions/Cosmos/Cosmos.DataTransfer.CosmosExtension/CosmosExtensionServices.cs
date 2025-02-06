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
            };

            if (!string.IsNullOrEmpty(settings.WebProxy)){
                clientOptions.WebProxy = new WebProxy(settings.WebProxy);
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