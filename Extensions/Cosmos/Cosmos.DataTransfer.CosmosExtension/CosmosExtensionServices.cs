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
using System.Diagnostics.Tracing;
using System.Collections.Concurrent;

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

        // The SDK configures and disposes each HttpClient. Only handlers/pools are process-owned.
        private static readonly ConcurrentDictionary<HttpTransportSettings, Lazy<SocketsHttpHandler>> _httpHandlers = new();

        private sealed record HttpTransportSettings(string? WebProxy, bool UseDefaultProxyCredentials,
            bool PreAuthenticate, bool DisableSslValidation);

        internal static SocketsHttpHandler GetHttpHandler(CosmosSettingsBase settings)
        {
            var transportSettings = new HttpTransportSettings(
                string.IsNullOrEmpty(settings.WebProxy) ? null : settings.WebProxy,
                settings.UseDefaultProxyCredentials, settings.PreAuthenticate, settings.DisableSslValidation);
            return _httpHandlers.GetOrAdd(transportSettings, key => new Lazy<SocketsHttpHandler>(() =>
            {
                var handler = new SocketsHttpHandler
                {
                    Credentials = CredentialCache.DefaultNetworkCredentials,
                    PreAuthenticate = key.PreAuthenticate,
                    // Refresh DNS for long-running migrations without giving up connection pooling.
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                    UseCookies = false,
                };
                if (key.WebProxy is not null)
                {
                    handler.Proxy = new WebProxy(key.WebProxy)
                    {
                        UseDefaultCredentials = key.UseDefaultProxyCredentials,
                    };
                }
                if (key.DisableSslValidation)
                {
                    handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
                }
                return handler;
            })).Value;
        }

        private static readonly object _httpEventListenerLock = new();
        private static EventListener? _httpEventListener;

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

        internal static DefaultAzureCredentialOptions CreateDefaultAzureCredentialOptions(CosmosSettingsBase settings)
        {
            var options = new DefaultAzureCredentialOptions
            {
                ExcludeInteractiveBrowserCredential = !settings.EnableInteractiveCredentials,
            };

            if (!string.IsNullOrWhiteSpace(settings.ManagedIdentityClientId))
            {
                options.ManagedIdentityClientId = settings.ManagedIdentityClientId;
            }

            return options;
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

                        var certificate = X509Certificate2.GetCertContentType(settings.ClientCertificatePath!) == X509ContentType.Pkcs12
                            ? X509CertificateLoader.LoadPkcs12FromFile(
                                settings.ClientCertificatePath!, certificatePassword, X509KeyStorageFlags.EphemeralKeySet)
                            : X509CertificateLoader.LoadCertificateFromFile(settings.ClientCertificatePath!);

                        if (!certificate.HasPrivateKey)
                        {
                            certificate.Dispose();
                            throw new CryptographicException("Client certificate must contain a private key.");
                        }

                        return new ClientCertificateCredential(settings.TenantId!, settings.ClientId!, certificate);

                    default:
                        return new DefaultAzureCredential(CreateDefaultAzureCredentialOptions(settings));
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
                EnableContentResponseOnWrite = settings.EnableContentResponseOnWrite,
                Serializer = cosmosSerializer,
                LimitToEndpoint = settings.LimitToEndpoint,
            };

            if (settings.UseDefaultCredentials)
            {
                var handler = GetHttpHandler(settings);
                clientOptions.HttpClientFactory = () => new HttpClient(handler, disposeHandler: false);
            }
            else if (!string.IsNullOrEmpty(settings.WebProxy))
            {
                var webProxy = new WebProxy(settings.WebProxy);
                if (settings.UseDefaultProxyCredentials)
                {
                    webProxy.UseDefaultCredentials = true;
                }
                clientOptions.WebProxy = webProxy;
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
            return CreateClient(settings, displayName, logger, sourceDisplayName, CreateRbacTokenCredential);
        }

        internal static CosmosClient CreateClient(CosmosSettingsBase settings, string displayName, ILogger logger,
            string? sourceDisplayName, Func<CosmosSettingsBase, ILogger, TokenCredential> credentialFactory)
        {
            string userAgentString = CreateUserAgentString(displayName, sourceDisplayName);
            var clientOptions = CreateClientOptions(settings, userAgentString, logger);

            if (settings.EnableNetHttpLogging)
            {
                logger.LogWarning(".NET HTTP diagnostic logging is ENABLED for Cosmos connection troubleshooting. Logs are emitted at Debug level and may include endpoint/request details.");
                EnsureHttpEventLoggingEnabled(logger);
            }
            
            CosmosClient? cosmosClient;
            if (settings.UseRbacAuth)
            {
                var tokenCredential = credentialFactory(settings, logger);

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

        private static void EnsureHttpEventLoggingEnabled(ILogger logger)
        {
            lock (_httpEventListenerLock)
            {
                _httpEventListener ??= new NetHttpEventListener(logger);
            }
        }

        private sealed class NetHttpEventListener(ILogger logger) : EventListener
        {
            private readonly ILogger _logger = logger;

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (eventSource.Name == "System.Net.Http" || eventSource.Name == "System.Net.Security")
                {
                    EnableEvents(eventSource, EventLevel.Informational, EventKeywords.All);
                }
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                if (!_logger.IsEnabled(LogLevel.Debug))
                {
                    return;
                }

                if (eventData.EventSource.Name != "System.Net.Http" && eventData.EventSource.Name != "System.Net.Security")
                {
                    return;
                }

                if (string.Equals(eventData.EventName, "EventCounters", StringComparison.Ordinal))
                {
                    return;
                }

                var payload = FormatPayload(eventData);

                _logger.LogDebug(".NET HTTP event {Source}:{EventName} ({EventId}) {Payload}",
                    eventData.EventSource.Name,
                    eventData.EventName ?? "unknown",
                    eventData.EventId,
                    payload);
            }

            private static string FormatPayload(EventWrittenEventArgs eventData)
            {
                if (eventData.Payload is null || eventData.Payload.Count == 0)
                {
                    return string.Empty;
                }

                return string.Join(", ", eventData.Payload.Select((value, index) =>
                {
                    var key = eventData.PayloadNames is not null && index < eventData.PayloadNames.Count
                        ? eventData.PayloadNames[index]
                        : $"arg{index}";
                    return $"{key}={FormatPayloadValue(value)}";
                }));
            }

            private static string FormatPayloadValue(object? value)
            {
                if (value is null)
                {
                    return "null";
                }

                if (value is string text)
                {
                    return text;
                }

                if (value is IEnumerable<KeyValuePair<string, object>> keyValuePairs)
                {
                    return string.Join(";", keyValuePairs.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                }

                return value.ToString() ?? string.Empty;
            }
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