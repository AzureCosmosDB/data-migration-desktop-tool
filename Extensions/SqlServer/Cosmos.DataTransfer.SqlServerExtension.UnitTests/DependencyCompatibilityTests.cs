using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.Sqlite;

namespace Cosmos.DataTransfer.SqlServerExtension.UnitTests;

[TestClass]
public class DependencyCompatibilityTests
{
    [TestMethod]
    public void SqliteProvider_LoadsPatchedNativeEngine()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sqlite_version()";
        var engineVersion = (string)command.ExecuteScalar()!;
        Assert.IsTrue(Version.Parse(engineVersion) >= new Version(3, 53, 4),
            $"Expected the serviced SQLite bundle's engine (at least 3.53.4), loaded {engineVersion}.");
    }

    [TestMethod]
    public void CmsEncryption_RoundTripsWithServicedPkcs()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=DMT dependency compatibility", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        var expected = new byte[] { 1, 2, 3, 4, 5 };
        var envelope = new EnvelopedCms(new ContentInfo(expected));
        envelope.Encrypt(new CmsRecipient(certificate));
        var decoded = new EnvelopedCms();
        decoded.Decode(envelope.Encode());
        decoded.Decrypt(decoded.RecipientInfos[0], rsa);
        CollectionAssert.AreEqual(expected, decoded.ContentInfo.Content);
    }
}
