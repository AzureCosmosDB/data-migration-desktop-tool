using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using System.Data.Common;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Common.UnitTests;
using Moq;
using Microsoft.Extensions.Configuration;

namespace Cosmos.DataTransfer.SqlServerExtension.UnitTests;

[TestClass]
public class SqlServerDataSourceExtensionTests
{

    private static async Task<Tuple<SqliteFactory,DbConnection>> connectionFactory(CancellationToken cancellationToken = default(CancellationToken)) {
        var provider = SqliteFactory.Instance;
        var connection = provider.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"CREATE TABLE foobar (
            id INTEGER NOT NULL,
            name TEXT
        );";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        cmd.CommandText = @"INSERT INTO foobar (id, name) 
        VALUES (1, 'zoo');";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        cmd.CommandText = @"INSERT INTO foobar (id, name) 
        VALUES (2, NULL);";
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        return Tuple.Create(provider, connection);
    }

    [TestMethod]
    public async Task TestReadAsync() {
        var config = new Mock<IConfiguration>();
        var cancellationToken = new CancellationTokenSource(500);
        var (providerFactory, connection) = await connectionFactory(cancellationToken.Token);

        var extension = new SqlServerDataSourceExtension();
        Assert.AreEqual("SqlServer", extension.DisplayName);

        var result = await extension.ReadAsync(config.Object, NullLogger.Instance,
          "SELECT * FROM foobar", Array.Empty<DbParameter>(), connection, providerFactory, cancellationToken.Token).ToListAsync();
        var expected = new List<DictionaryDataItem> {
            new DictionaryDataItem(new Dictionary<string, object?> { { "id", (long)1 }, { "name", "zoo" } }),
            new DictionaryDataItem(new Dictionary<string, object?> { { "id", (long)2 }, { "name", null }  })
        };
        CollectionAssert.That.AreEqual(expected, result, new DataItemComparer());
    }

    [TestMethod]
    public async Task TestReadAsyncWithParameters() {
        var config = new Mock<IConfiguration>();
        var cancellationToken = new CancellationTokenSource();
        var (providerFactory, connection) = await connectionFactory(cancellationToken.Token);

        var extension = new SqlServerDataSourceExtension();
        Assert.AreEqual("SqlServer", extension.DisplayName);

        var parameter = providerFactory.CreateParameter();
        parameter.ParameterName = "@x";
        parameter.DbType = System.Data.DbType.Int32;
        parameter.Value = 2;

        var result = await extension.ReadAsync(config.Object, NullLogger.Instance,
          "SELECT * FROM foobar WHERE id = @x", 
          new DbParameter[] { parameter }, connection, providerFactory, cancellationToken.Token).FirstAsync();
        Assert.That.AreEqual(result,
            new DictionaryDataItem(new Dictionary<string, object?> { { "id", (long)2 }, { "name", null } }),
            new DataItemComparer());
    }

    // Allows for testing against an actual SQL Server by specifying a 
    // connectionstring in either a .runsettings-file or environment variable.
    // Example: Using sql.runsettings
    // <?xml version="1.0" encoding="utf-8"?>
    // <RunSettings>
    //   <TestRunParameters>
    //     <Parameter name="TestReadAsync_LiveSqlServer_ConnectionString" value="Your connection string" />
    //   </TestRunParameters>
    // </RunSettings>
    // run test with
    // dotnet test --settings sql.runsettings 
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public TestContext TestContext { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    [TestMethod]
    [Timeout(1000)]
    public async Task TestReadAsync_LiveSqlServer() {
        var connectionString = (string?)TestContext.Properties["TestReadAsync_LiveSqlServer_ConnectionString"];
        connectionString ??= Environment.GetEnvironmentVariable("TestReadAsync_LiveSqlServer_ConnectionString");
        if (connectionString is null) {
            Assert.Inconclusive("Could not run, as no connection string to live SQL Server was provided.");
        }

        var extension = new SqlServerDataSourceExtension();
        var config = TestHelpers.CreateConfig(new Dictionary<string, string> {
            { "ConnectionString", connectionString! },
            { "QueryText", "SELECT 1, 'foo' as bar, NULL as zoo;" }
        });

        var result = await extension.ReadAsync(config, NullLogger.Instance).FirstAsync();

        Assert.IsTrue(new DataItemComparer().Equals(result, 
            new DictionaryDataItem(new Dictionary<string, object?> {
                { "", 1 },
                { "bar", "foo" },
                { "zoo", null }
        })));
    }
}
