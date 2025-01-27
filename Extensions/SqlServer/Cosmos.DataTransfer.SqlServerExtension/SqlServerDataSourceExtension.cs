using System.ComponentModel.Composition;
using System.IO;
using System.Runtime.CompilerServices;
using Cosmos.DataTransfer.Interfaces;
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
            await foreach (var item in this.ReadAsync(config, logger, (string connectionString) => new ValueTask<System.Data.Common.DbConnection>(new SqlConnection(connectionString)), cancellationToken)) {
                yield return item;
            }
        }

        public async IAsyncEnumerable<IDataItem> ReadAsync(
            IConfiguration config, 
            ILogger logger, 
            Func<string,ValueTask<System.Data.Common.DbConnection>> connectionFactory,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var settings = config.Get<SqlServerSourceSettings>();
            settings.Validate();

            string queryText = settings!.QueryText!;
            if (settings.FilePath != null) {
                queryText = File.ReadAllText(settings.FilePath);
            }
            
            await using var connection = connectionFactory(settings.ConnectionString!).Result;
            await connection.OpenAsync(cancellationToken);
            var command = connection.CreateCommand();
            command.CommandText = queryText;
            //await using SqlCommand command = new SqlCommand(queryText, connection);
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
        }

        public IEnumerable<IDataExtensionSettings> GetSettings()
        {
            yield return new SqlServerSourceSettings();
        }
    }
}