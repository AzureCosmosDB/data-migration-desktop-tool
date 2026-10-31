using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Authentication;
using MongoDB.Driver.Encryption;

namespace Cosmos.DataTransfer.MongoExtension.UnitTests;

[TestClass]
public class DriverCompatibilityTests
{
    [TestMethod]
    public void GuidMapping_PreservesLegacyBytesAndNestedValues()
    {
        var guid = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var document = MongoBsonMapper.ToDocument(new Dictionary<string, object?>
        {
            ["_id"] = guid,
            ["nested"] = new Dictionary<string, object?> { ["ids"] = new[] { guid } },
            ["binary"] = new byte[] { 1, 2, 3 },
            ["standard"] = new BsonBinaryData(guid, GuidRepresentation.Standard),
            ["null"] = null
        });

        Assert.AreEqual(BsonBinarySubType.UuidLegacy, document["_id"].AsBsonBinaryData.SubType);
        CollectionAssert.AreEqual(guid.ToByteArray(), document["_id"].AsBsonBinaryData.Bytes);
        Assert.AreEqual(document["_id"], document["nested"]["ids"][0]);
        Assert.AreEqual(BsonBinarySubType.Binary, document["binary"].AsBsonBinaryData.SubType);
        Assert.IsTrue(document["null"].IsBsonNull);
        var item = new MongoDataItem(document);
        Assert.AreEqual(guid, item.GetValue("_id"));
        Assert.AreEqual(guid, item.GetValue("standard"));
        var nested = (Dictionary<string, object?>)item.GetValue("nested")!;
        CollectionAssert.AreEqual(new object[] { guid }, (List<object?>)nested["ids"]!);
    }

    [TestMethod]
    public void AwsAuthentication_IsRegisteredWithoutFetchingCredentials()
    {
        RuntimeHelpers.RunClassConstructor(typeof(Context).TypeHandle);
        var created = MongoClientSettings.Extensions.SaslMechanisms.TryCreate(new SaslContext
        {
            Mechanism = "MONGODB-AWS",
            Identity = new MongoExternalIdentity("synthetic-access-key"),
            IdentityEvidence = new PasswordEvidence("synthetic-secret"),
            EndPoint = new DnsEndPoint("localhost", 1),
            ClusterEndPoints = new[] { new DnsEndPoint("localhost", 1) },
            MechanismProperties = Array.Empty<KeyValuePair<string, object>>()
        }, out var mechanism);
        Assert.IsTrue(created);
        Assert.AreEqual("MongoDB.Driver.Authentication.AWS", mechanism.GetType().Assembly.GetName().Name);
    }

    [TestMethod]
    public void AutoDecryption_IsRegisteredAndLoadsNativeLibrary()
    {
        RuntimeHelpers.RunClassConstructor(typeof(Context).TypeHandle);
        var options = new AutoEncryptionOptions(
            new CollectionNamespace("offline", "keys"),
            new Dictionary<string, IReadOnlyDictionary<string, object>>
            {
                ["local"] = new Dictionary<string, object> { ["key"] = new byte[96] }
            },
            bypassAutoEncryption: true);

        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:1/?directConnection=true");
        settings.AutoEncryptionOptions = options;
        if (OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            // Upstream ships no Windows ARM64 libmongocrypt, just as with the previous driver.
            Assert.IsFalse(NativeLibrary.TryLoad("mongocrypt", typeof(ClientEncryption).Assembly, null, out _),
                "Windows ARM64 native encryption support has changed; require the initialization check on this platform.");
            return;
        }

        using var client = new MongoClient(settings);
        Assert.IsTrue(client.Settings.AutoEncryptionOptions.BypassAutoEncryption);
    }
}
