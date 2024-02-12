using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.PostgresqlExtension.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;

namespace Cosmos.DataTransfer.PostgresqlExtension;
[Export(typeof(IDataSourceExtension))]

internal class PostgresqlDataSourceExtension : IDataSourceExtensionWithSettings
{
    public string DisplayName => "PostgreSQL";

    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield return new PostgreSourceSettings();
    }

    public async IAsyncEnumerable<IDataItem> ReadAsync(IConfiguration config, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var settings = config.Get<PostgreSourceSettings>();
        settings.Validate();

        await using var connection = new NpgsqlConnection(settings.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(settings.QueryText, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var columns = await reader.GetColumnSchemaAsync(cancellationToken);
            Dictionary<string, object?> fields = new();
            foreach (var column in columns)
            {
                var value = column.ColumnOrdinal.HasValue ? reader[column.ColumnOrdinal.Value] : reader[column.ColumnName];
                if (value == DBNull.Value)
                {
                    value = null;
                }
                fields[column.ColumnName] = value;
            }
            yield return new DictionaryDataItem(fields);
        }
    }
}

