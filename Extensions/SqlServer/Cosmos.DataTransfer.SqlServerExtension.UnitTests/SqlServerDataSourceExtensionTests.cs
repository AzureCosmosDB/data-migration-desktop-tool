using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Common.UnitTests;

namespace Cosmos.DataTransfer.SqlServerExtension.UnitTests;

[TestClass]
public class SqlServerDataSourceExtensionTests
{

    private static async Task<Func<string,ValueTask<System.Data.Common.DbConnection>>> connectionFactory(CancellationToken cancellationToken = default(CancellationToken)) {
        var connection  = new SqliteConnection("");
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

        var func = (string connectionString) => {
            return new ValueTask<System.Data.Common.DbConnection>(connection);
        };
            
        return func;
    }

    [TestMethod]
    public async Task TestReadAsync_QueryText() {
        var extension = new SqlServerDataSourceExtension();
        var config = TestHelpers.CreateConfig(new Dictionary<string, string> {
            { "ConnectionString", "Sqlite" },
            { "QueryText", "SELECT * FROM foobar" }
        });
        Assert.AreEqual("SqlServer", extension.DisplayName);
        
        var cancellationToken = new CancellationTokenSource(500);

        var result = await extension.ReadAsync(config, NullLogger.Instance, await connectionFactory(cancellationToken.Token), cancellationToken.Token).ToListAsync();
        var expected = new List<DictionaryDataItem> {
            new DictionaryDataItem(new Dictionary<string, object?> { { "id", (long)1 }, { "name", "zoo" } }),
            new DictionaryDataItem(new Dictionary<string, object?> { { "id", (long)2 }, { "name", null }  })
        };
        CollectionAssert.That.AreEqual(expected, result, new DataItemComparer());
    }

    [TestMethod]
    public async Task TestReadAsync_FromFile() {
        var outputFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(outputFile, "SELECT * FROM foobar;");
        var extension = new SqlServerDataSourceExtension();
        var config = TestHelpers.CreateConfig(new Dictionary<string, string> {
            { "ConnectionString", "Sqlite" },
            { "FilePath", outputFile }
        });

        
        var cancellationToken = new CancellationTokenSource(500);

        var result = await extension.ReadAsync(config, NullLogger.Instance, await connectionFactory(cancellationToken.Token), cancellationToken.Token).ToListAsync();
        var expected = new List<DictionaryDataItem> {
            new DictionaryDataItem(new Dictionary<string, object?> { { "id", (long)1 }, { "name", "zoo" } }),
            new DictionaryDataItem(new Dictionary<string, object?> { { "id", (long)2 }, { "name", null }  })
        };
        CollectionAssert.That.AreEqual(expected, result, new DataItemComparer());
    }

    // Allows for testing against an actual SQL Server by specifying a 
    // connectionstring in either a .runsettings-file or environment variable.
    // Example: Using sql.runsettings
    // <?xml version="1.0" encoding="utf-8"?>
    // <RunSettings>
    //   <TestRunParameters>
    //     <Parameter name="TestReadAsync_LiveSqlServer_ConnectionString" value="<Your connection string>" />
    //   </TestRunParameters>
    // </RunSettings>
    // run test with dotnet test --settings sql.runsettings 
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