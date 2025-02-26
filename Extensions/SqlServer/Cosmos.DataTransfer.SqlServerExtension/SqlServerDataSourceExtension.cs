using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using Cosmos.DataTransfer.Interfaces;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.SqlServerExtension
{
    [Export(typeof(IDataSourceExtension))]
    public class SqlServerDataSourceExtension : IDataSourceExtensionWithSettings
    {
        public string DisplayName => "SqlServer";

        public async IAsyncEnumerable<IDataItem> ReadAsync(IConfiguration config, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var settings = config.Get<SqlServerSourceSettings>();
            settings.Validate();

            var providerFactory = SqlClientFactory.Instance;
            var connection = providerFactory.CreateConnection()!;
            connection.ConnectionString = settings!.ConnectionString;

            var iterable = this.ReadAsync(config, logger, settings.GetQueryText(), 
                settings.GetDbParameters(providerFactory), connection, 
                providerFactory, cancellationToken);
            
            await foreach (var item in iterable) {
                yield return item;
            }
        }

        public async IAsyncEnumerable<IDataItem> ReadAsync(
            IConfiguration config, 
            ILogger logger, 
            string queryText,
            DbParameter[] parameters,
            DbConnection connection,
            DbProviderFactory dbProviderFactory,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            try {
                await connection.OpenAsync(cancellationToken);
                var command = connection.CreateCommand();
                command.CommandText = queryText;
                command.Parameters.AddRange(parameters);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var columns = await reader.GetColumnSchemaAsync(cancellationToken);
                    Dictionary<string, object?> fields = new Dictionary<string, object?>();
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
            } finally {
                await connection.CloseAsync();
            }
        }

        public IEnumerable<IDataExtensionSettings> GetSettings()
        {
            yield return new SqlServerSourceSettings();
        }
    }
}