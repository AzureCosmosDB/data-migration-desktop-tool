using System.ComponentModel.Composition;
using System.Data;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.PostgresqlExtension.Settings;
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
            yield return new PostgreSinkSettings();
        }

        public async Task WriteAsync(IAsyncEnumerable<IDataItem> dataItems, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken = default)
        {
            var settings = config.Get<PostgreSinkSettings>();
            settings.Validate();
            
            var cols = await FindPostgreDataTypes(dataItems, cancellationToken);
            NpgsqlConnection con = new(settings.ConnectionString);

            if (settings.AppendDataToTable == true && !string.IsNullOrEmpty(settings.TableName))
            {
                var destcols = LoadTableSchema(con, settings.TableName);                
                cols = MapDataTypes(destcols, cols);
            }
            else if (settings.DropAndCreateTable == true)
            {
                DropTable(con, settings.TableName);
                CreateTable(con, settings.TableName, cols);
            }
            con.Open();
            using (var writer = con.BeginBinaryImport(GenerateInsertCommand(settings.TableName, cols)))                
            {
                await foreach (var row in dataItems)
                {                    
                    await writer.StartRowAsync(cancellationToken).ConfigureAwait(false);
                    foreach (var item in cols)
                    {
                        try
                        {                            
                            await writer.WriteAsync(row.GetValue(item.ColumnName), item.PostgreType, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error writing to database");
                        }
                    }
                }
                await writer.CompleteAsync(cancellationToken).ConfigureAwait(false);
            }
            con.Close();
        }

        private async Task<List<PostgreDataCol>> FindPostgreDataTypes(IAsyncEnumerable<IDataItem> dataItems, CancellationToken cancellationToken = default)
        {
            List<PostgreDataCol> postgreDataCols = new();
            await foreach (var item in dataItems)
            {
                var fieldNames = item.GetFieldNames();
                int row = 0;
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

                    }
                    else
                    {
                        if (current.PostgreType == NpgsqlTypes.NpgsqlDbType.Unknown && coltype?.Name != "Missing")
                        {
                            var newcol = new PostgreDataCol(col, coltype);
                            postgreDataCols[row] = newcol;
                        }
                    }
                    row++;
                }
            }
            return postgreDataCols;
        }

        private List<PostgreDataCol> MapDataTypes(List<PostgreDataCol> dest, List<PostgreDataCol> source)
        {
            var temp = new List<PostgreDataCol>();
            foreach (var item in dest)
            {
                bool found = false;
                foreach (var col in source)
                {
                    if (item.ColumnName.ToLower() == col.ColumnName.ToLower())
                    {
                        temp.Add(new PostgreDataCol()
                        {
                            ColumnName = col.ColumnName,
                            ColumnType = item.ColumnType,
                            PostgreType = item.PostgreType
                        });
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    throw new Exception($"Column '{item.ColumnName}' does not exist in the source.");
                }
            }
            return temp;
        }

        private static void CreateTable(NpgsqlConnection con, string tableName, List<PostgreDataCol> cols)
        {
            //NpgsqlConnection con = new(connectionString);
            var createtxt = $"CREATE TABLE {tableName}(";
            foreach (var item in cols)
            {
                createtxt += $"{item.ColumnName} {item.PostgreType},";
                if (cols.Last() == item)
                {
                    createtxt = createtxt.TrimEnd(',');
                }
            }
            createtxt += ")";
            con.Open();
            using (var cmd = new NpgsqlCommand(createtxt, con))
            {
                cmd.ExecuteNonQuery();
            }
            con.Close();
        }

        private static void DropTable(NpgsqlConnection con, string tableName)
        {            
            con.Open();
            using (var cmd = new NpgsqlCommand($"DROP TABLE IF EXISTS {tableName}", con))
            {
                cmd.ExecuteNonQuery();
            }
            con.Close();
        }

        private static List<PostgreDataCol> LoadTableSchema(NpgsqlConnection con, string tableName)
        {
            var temp = new List<PostgreDataCol>();            
            con.Open();
            var dt = new DataTable();
            using (var cmd = new NpgsqlCommand($"SELECT column_name, udt_name FROM information_schema.columns WHERE table_name = '{tableName}'", con))
            using (var reader = cmd.ExecuteReader())
            {
                dt.Load(reader);
            }
            foreach (DataRow row in dt.Rows)
            {
                if (row != null)
                {
                    var newcol = new PostgreDataCol(row["column_name"]?.ToString(), row["udt_name"]?.ToString());
                    temp.Add(newcol);
                }
            }

            con.Close();
            return temp;
        }

        private static string GenerateInsertCommand(string tablename, List<PostgreDataCol> cols)
        {
            var colstxt = "";
            foreach (var item in cols)
            {
                colstxt += $"{item.ColumnName},";
                if (cols.Last() == item)
                {
                    colstxt = colstxt.TrimEnd(',');
                }
            }
            return $"COPY {tablename}({colstxt}) FROM STDIN(FORMAT BINARY)";
        }


    }
}
