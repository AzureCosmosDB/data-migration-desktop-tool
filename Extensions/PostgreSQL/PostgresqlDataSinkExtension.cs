using System.ComponentModel.Composition;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Cosmos.DataTransfer.PostgresqlExtension
{
    [Export(typeof(IDataSinkExtension))]
    public class PostgresqlDataSinkExtension : IDataSinkExtensionWithSettings
    {
        public string DisplayName => "PostgreSQL";        

        public IEnumerable<IDataExtensionSettings> GetSettings()
        {
            throw new NotImplementedException();
        }

        public async Task WriteAsync(IAsyncEnumerable<IDataItem> dataItems, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken = default)
        {
            await MapDataTypes(dataItems);
            /*
            NpgsqlConnection con = new NpgsqlConnection("");
            using (var writer = con.BeginBinaryImport("COPY teachers (first_name, last_name, subject, salary) FROM STDIN (FORMAT BINARY)"))
            {
                await foreach (var item in dataItems)
                {
                    await writer.StartRowAsync().ConfigureAwait(false);
                    await writer.WriteAsync("Firstname", NpgsqlTypes.NpgsqlDbType.Varchar).ConfigureAwait(false);                    
                  
                    

                    await writer.WriteAsync(teacher.LastName, NpgsqlTypes.NpgsqlDbType.Varchar).ConfigureAwait(false);
                    await writer.WriteAsync(teacher.Subject, NpgsqlTypes.NpgsqlDbType.Varchar).ConfigureAwait(false);
                    await writer.WriteAsync(teacher.Salary, NpgsqlTypes.NpgsqlDbType.Integer).ConfigureAwait(false);
                }
                await writer.CompleteAsync().ConfigureAwait(false);
            }
            throw new NotImplementedException();*/
        }

        public async Task MapDataTypes(IAsyncEnumerable<IDataItem> dataItems, CancellationToken cancellationToken = default)
        {
            List<PostgreDataCol> postgreDataCols = new List<PostgreDataCol>();
            await foreach (var item in dataItems)
            {
                var fieldNames = item.GetFieldNames();
                long row = 0;
                foreach (var col in fieldNames)
                {
                    var current = postgreDataCols.FirstOrDefault(c => c.ColumnName == col);
                    var colval = item.GetValue(col);
                    var coltype = Type.Missing.GetType();
                    if (colval != null)
                    {
                        coltype = colval.GetType();
                    }
                    if (current == null)
                    {
                        var newcol = new PostgreDataCol(col, coltype);                       
                        newcol.AddColumnValue(row, colval);
                        postgreDataCols.Add(newcol);
                        row++;
                    }
                }
            }
        }

        
        

        
    }
}
