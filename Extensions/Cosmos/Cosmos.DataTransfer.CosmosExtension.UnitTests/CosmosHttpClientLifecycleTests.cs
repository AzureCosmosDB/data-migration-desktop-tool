using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Encryption;
using Azure.Security.KeyVault.Keys.Cryptography;
using Moq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Cosmos.DataTransfer.CosmosExtension.UnitTests;

[TestClass]
public class CosmosHttpClientLifecycleTests
{
    [DataTestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, false)]
    [DataRow(false, true, true)]
    [DataRow(true, false, false)]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    [DataRow(true, true, true)]
    public async Task CreateClient_AfterTransportUsed_CreatesLaterSourceAndSink(
        bool useRbacAuth, bool preAuthenticate, bool differentAccount)
    {
        var source = CreateSettings<CosmosSourceSettings>(useRbacAuth, preAuthenticate);
        var sink = CreateSettings<CosmosSinkSettings>(useRbacAuth, preAuthenticate,
            differentAccount ? 2 : 1);
        using var first = CreateOfflineClient(source, "source");
        using var transport = first.ClientOptions.HttpClientFactory();
        await StartTransportAsync(transport);

        // Construct through DMT, not by manually assigning HttpClient.Timeout.
        using var second = CreateOfflineClient(sink, "sink", "source");
        first.Dispose();
        using var secondTransport = second.ClientOptions.HttpClientFactory();
        await StartTransportAsync(secondTransport);
        using var third = CreateOfflineClient(source, "next-operation");
        using var thirdTransport = third.ClientOptions.HttpClientFactory();
        await StartTransportAsync(thirdTransport);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CreateClient_WithoutDefaultCredentials_LeavesTransportToSdk(bool useRbacAuth)
    {
        var settings = CreateSettings<CosmosSourceSettings>(useRbacAuth, false);
        settings.UseDefaultCredentials = false;
        using var first = CreateOfflineClient(settings, "source");
        using var second = CreateOfflineClient(settings, "sink");
        Assert.IsNull(first.ClientOptions.HttpClientFactory);
        Assert.IsNull(second.ClientOptions.HttpClientFactory);
    }

    [TestMethod]
    public async Task Factory_ReturnsIndependentWrappers_AndSnapshotsSettings()
    {
        var settings = CreateSettings<CosmosSourceSettings>(false, false);
        var options = CosmosExtensionServices.CreateClientOptions(settings, "source-agent", NullLogger.Instance);
        settings.PreAuthenticate = true;
        settings.WebProxy = "http://127.0.0.1:1/";
        settings.DisableSslValidation = true;
        using var first = options.HttpClientFactory();
        using var second = options.HttpClientFactory();
        Assert.AreNotSame(first, second);
        first.DefaultRequestHeaders.Add("x-test-source", "source");
        first.Timeout = TimeSpan.FromSeconds(17);
        Assert.IsFalse(second.DefaultRequestHeaders.Contains("x-test-source"));
        Assert.AreNotEqual(first.Timeout, second.Timeout);
        await StartTransportAsync(first);
        first.Dispose();
        await StartTransportAsync(second);
        using var later = options.HttpClientFactory();
        await StartTransportAsync(later);
        Assert.AreEqual("source-agent", options.ApplicationName);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SdkDisposal_DisposesWrapper_NotSharedHandler(bool encryption)
    {
        var settings = CreateSettings<CosmosSourceSettings>(true, false);
        var options = CosmosExtensionServices.CreateClientOptions(settings, "test", NullLogger.Instance);
        var factory = options.HttpClientFactory;
        HttpClient? ownedTransport = null;
        options.HttpClientFactory = () => ownedTransport = factory();
        var credential = new OfflineTokenCredential();
        using var client = new CosmosClient(settings.AccountEndpoint, credential, options);
        Assert.IsNotNull(ownedTransport);
        await StartTransportAsync(ownedTransport);

        using var owner = encryption
            ? client.WithEncryption(new KeyResolver(credential), KeyEncryptionKeyResolverName.AzureKeyVault)
            : client;
        owner.Dispose();
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() =>
            ownedTransport.GetAsync("http://127.0.0.1:1/", new CancellationToken(true)));

        using var survivingTransport = factory();
        await StartTransportAsync(survivingTransport);
        using var next = CreateOfflineClient(settings, "next");
    }

    [TestMethod]
    public async Task CreateClient_ConcurrentCreationAndUse_DoesNotShareWrapperState()
    {
        await Task.WhenAll(Enumerable.Range(0, 24).Select(i => Task.Run(async () =>
        {
            var settings = CreateSettings<CosmosSinkSettings>(i % 2 == 0, i % 3 == 0, 1 + i % 2);
            using var client = CreateOfflineClient(settings, $"sink-{i}");
            using var transport = client.ClientOptions.HttpClientFactory();
            await StartTransportAsync(transport);
            using var next = CreateOfflineClient(settings, $"next-{i}");
        })));
    }

    [TestMethod]
    public void Handler_PoolsOnlyCompatibleSettings_AndAppliesCredentialsAndTls()
    {
        var settings = CreateSettings<CosmosSourceSettings>(false, false);
        var handler = CosmosExtensionServices.GetHttpHandler(settings);
        Assert.AreSame(handler, CosmosExtensionServices.GetHttpHandler(
            CreateSettings<CosmosSinkSettings>(true, false, 2)));
        Assert.AreSame(CredentialCache.DefaultNetworkCredentials, handler.Credentials);
        Assert.IsNull(handler.DefaultProxyCredentials);
        Assert.IsNull(handler.Proxy);
        Assert.IsFalse(handler.PreAuthenticate);
        Assert.IsFalse(handler.UseCookies);
        Assert.IsNull(handler.SslOptions.RemoteCertificateValidationCallback);
        Assert.AreEqual(TimeSpan.FromMinutes(5), handler.PooledConnectionLifetime);

        settings.PreAuthenticate = true;
        var preAuth = CosmosExtensionServices.GetHttpHandler(settings);
        Assert.AreNotSame(handler, preAuth);
        Assert.IsTrue(preAuth.PreAuthenticate);
        settings.DisableSslValidation = true;
        var insecure = CosmosExtensionServices.GetHttpHandler(settings);
        Assert.AreNotSame(preAuth, insecure);
        Assert.IsNotNull(insecure.SslOptions.RemoteCertificateValidationCallback);
        settings.WebProxy = "http://127.0.0.1:12345/";
        var proxy = CosmosExtensionServices.GetHttpHandler(settings);
        Assert.AreNotSame(insecure, proxy);
        Assert.IsInstanceOfType<WebProxy>(proxy.Proxy);
        Assert.AreEqual(new Uri(settings.WebProxy), ((WebProxy)proxy.Proxy!).Address);
        Assert.IsNull(proxy.Proxy.Credentials);
        settings.UseDefaultProxyCredentials = true;
        var authenticatedProxy = CosmosExtensionServices.GetHttpHandler(settings);
        Assert.AreNotSame(proxy, authenticatedProxy);
        Assert.AreSame(CredentialCache.DefaultNetworkCredentials, authenticatedProxy.Proxy!.Credentials);
        Assert.IsNull(proxy.Proxy.Credentials);
        settings.WebProxy = "http://127.0.0.1:12346/";
        Assert.AreNotSame(authenticatedProxy, CosmosExtensionServices.GetHttpHandler(settings));
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ClientOptions_PreservesProxyTlsAndCosmosSettings(bool customFactory)
    {
        var settings = CreateSettings<CosmosSinkSettings>(false, true);
        settings.UseDefaultCredentials = customFactory;
        settings.UseDefaultProxyCredentials = true;
        settings.WebProxy = "http://127.0.0.1:12345/";
        settings.DisableSslValidation = true;
        settings.ConnectionMode = ConnectionMode.Direct;
        settings.LimitToEndpoint = true;
        settings.AllowBulkExecution = true;
        settings.EnableContentResponseOnWrite = false;
        var logger = new Mock<ILogger>();
        var options = CosmosExtensionServices.CreateClientOptions(settings, "test-agent", logger.Object);
        Assert.AreEqual(ConnectionMode.Direct, options.ConnectionMode);
        Assert.IsTrue(options.LimitToEndpoint);
        Assert.IsTrue(options.AllowBulkExecution);
        Assert.IsFalse(options.EnableContentResponseOnWrite);
        Assert.AreEqual("test-agent", options.ApplicationName);
        Assert.IsInstanceOfType<RawJsonCosmosSerializer>(options.Serializer);
        Assert.IsNotNull(options.ServerCertificateCustomValidationCallback);
        if (customFactory)
        {
            Assert.IsNotNull(options.HttpClientFactory);
            Assert.IsNull(options.WebProxy); // The SDK rejects WebProxy together with HttpClientFactory.
        }
        else
        {
            Assert.IsNull(options.HttpClientFactory);
            Assert.IsInstanceOfType<WebProxy>(options.WebProxy);
            Assert.AreSame(CredentialCache.DefaultNetworkCredentials, options.WebProxy.Credentials);
        }
        logger.Verify(x => x.Log(LogLevel.Warning, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("SSL certificate validation is DISABLED")),
            It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [TestMethod]
    public async Task Factory_ConcurrentRequestsAndDisposal_KeepOtherClientsUsable()
    {
        using var server = new LoopbackServer();
        var responses = Enumerable.Range(0, 12).Select(_ => server.ServeAsync(1)).ToArray();
        var clients = Task.WhenAll(Enumerable.Range(0, 12).Select(i => Task.Run(async () =>
        {
            var settings = CreateSettings<CosmosSinkSettings>(i % 2 == 0, false);
            settings.WebProxy = server.Address.ToString();
            var options = CosmosExtensionServices.CreateClientOptions(settings, $"sink-{i}", NullLogger.Instance);
            using var transport = options.HttpClientFactory();
            using var response = await transport.GetAsync("http://cosmos.invalid/dbs", server.Token);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        })));
        await Task.WhenAll(responses.Cast<Task>().Append(clients));
    }

    [TestMethod]
    public async Task Factory_UsesConfiguredProxy_AndReusesConnectionAfterWrapperDisposal()
    {
        using var server = new LoopbackServer();
        var requests = server.ServeAsync(requestCount: 2);
        var settings = CreateSettings<CosmosSourceSettings>(false, true);
        settings.WebProxy = server.Address.ToString();
        settings.UseDefaultProxyCredentials = true;
        var options = CosmosExtensionServices.CreateClientOptions(settings, "test", NullLogger.Instance);
        // The closure must not read subsequent mutations to the settings object.
        settings.WebProxy = "http://127.0.0.1:1/";
        for (var i = 0; i < 2; i++)
        {
            using var transport = options.HttpClientFactory();
            using var response = await transport.GetAsync("http://cosmos.invalid/dbs", server.Token);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }
        var received = await requests;
        Assert.AreEqual(2, received.Count);
        Assert.IsTrue(received.All(r => r.StartsWith("GET http://cosmos.invalid/dbs HTTP/1.1")));
        Assert.IsTrue(received.All(r => !r.Contains("Authorization:", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(received[1].Contains("Cookie:", StringComparison.OrdinalIgnoreCase));
        // ServeAsync accepts only one socket: both requests must reuse that connection.
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Factory_SelfSignedTls_RequiresExplicitOptOut(bool disableSslValidation)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        // Import the key for Windows Schannel as well as OpenSSL.
        using var serverCertificate = new X509Certificate2(certificate.Export(X509ContentType.Pfx));
        using var server = new LoopbackServer();
        var served = server.ServeAsync(1, serverCertificate, expectTlsRejection: !disableSslValidation);
        var settings = CreateSettings<CosmosSourceSettings>(false, false);
        settings.DisableSslValidation = disableSslValidation;
        var options = CosmosExtensionServices.CreateClientOptions(settings, "test", NullLogger.Instance);
        using var transport = options.HttpClientFactory();
        var uri = new UriBuilder(server.Address) { Scheme = "https" }.Uri;
        try
        {
            if (disableSslValidation)
            {
                using var response = await transport.GetAsync(uri, server.Token);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }
            else
            {
                var error = await Assert.ThrowsExceptionAsync<HttpRequestException>(() => transport.GetAsync(uri, server.Token));
                Assert.IsInstanceOfType<AuthenticationException>(error.InnerException);
            }
        }
        finally
        {
            await served;
        }
    }

    private sealed class LoopbackServer : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
        public Uri Address { get; }
        public CancellationToken Token => timeout.Token;

        public LoopbackServer()
        {
            listener.Start();
            Address = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/");
        }

        public async Task<List<string>> ServeAsync(int requestCount, X509Certificate2? certificate = null,
            bool expectTlsRejection = false)
        {
            using var socket = await listener.AcceptTcpClientAsync(Token);
            using var network = socket.GetStream();
            using var tls = certificate is null ? null : new SslStream(network, leaveInnerStreamOpen: true);
            if (tls is not null)
            {
                try
                {
                    await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                    {
                        ServerCertificate = certificate,
                    }, Token);
                }
                catch (AuthenticationException) when (expectTlsRejection)
                {
                    return [];
                }
                catch (IOException) when (expectTlsRejection)
                {
                    return [];
                }
                if (expectTlsRejection)
                {
                    return [];
                }
            }
            Stream stream = tls is null ? network : tls;
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var requests = new List<string>();
            for (var i = 0; i < requestCount; i++)
            {
                var headers = new StringBuilder();
                while (true)
                {
                    var line = await reader.ReadLineAsync(Token);
                    Assert.IsNotNull(line, "Client closed the pooled connection.");
                    if (line.Length == 0)
                    {
                        break;
                    }
                    headers.AppendLine(line);
                }
                requests.Add(headers.ToString());
                var closeHeader = i == requestCount - 1 ? "Connection: close\r\n" : "";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 200 OK\r\nContent-Length: 0\r\n{closeHeader}Set-Cookie: test=not-shared\r\n\r\n"), Token);
                await stream.FlushAsync(Token);
            }
            return requests;
        }

        public void Dispose()
        {
            timeout.Cancel();
            listener.Stop();
            timeout.Dispose();
        }
    }

    [TestMethod]
    public async Task CreateClient_RbacConstructor_UsesOnlyOfflineCredentialForEagerTokenAcquisition()
    {
        var credential = new OfflineTokenCredential();
        var settings = CreateSettings<CosmosSourceSettings>(true, false);
        using var client = CosmosExtensionServices.CreateClient(settings, "test", NullLogger.Instance, null,
            (_, _) => credential);
        var scopes = await credential.RequestedScopes.Task.WaitAsync(TimeSpan.FromSeconds(5));
        CollectionAssert.AreEqual(new[] { "https://127.0.0.1/.default" }, scopes);
    }

    private static CosmosClient CreateOfflineClient(CosmosSettingsBase settings, string displayName,
        string? sourceDisplayName = null)
    {
        return CosmosExtensionServices.CreateClient(settings, displayName, NullLogger.Instance, sourceDisplayName,
            (_, _) => new OfflineTokenCredential());
    }

    private static T CreateSettings<T>(bool useRbacAuth, bool preAuthenticate, int port = 1)
        where T : CosmosSettingsBase, new()
    {
        var endpoint = $"https://127.0.0.1:{port}/";
        return new T
        {
            UseDefaultCredentials = true,
            PreAuthenticate = preAuthenticate,
            UseRbacAuth = useRbacAuth,
            AccountEndpoint = useRbacAuth ? endpoint : null,
            ConnectionString = useRbacAuth ? null
                : $"AccountEndpoint={endpoint};AccountKey={Convert.ToBase64String(new byte[64])};",
            Database = "db",
            Container = "container",
        };
    }

    private static async Task StartTransportAsync(HttpClient transport)
    {
        // Cancellation marks HttpClient as started without sending credentials or doing network I/O.
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:1/");
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
            await transport.SendAsync(request, new CancellationToken(canceled: true)));
    }
}
