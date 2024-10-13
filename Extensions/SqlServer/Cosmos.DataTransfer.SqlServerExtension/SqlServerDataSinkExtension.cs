using System.ComponentModel.Composition;
using System.Data;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.SqlServerExtension
{
    [Export(typeof(IDataSinkExtension))]
    public class SqlServerDataSinkExtension : IDataSinkExtensionWithSettings
    {
        public string DisplayName => "SqlServer";

        public async Task WriteAsync(IAsyncEnumerable<IDataItem> dataItems, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken = default)
        {
            var settings = config.Get<SqlServerSinkSettings>();
            settings.Validate();

            string tableName = settings.TableName!;

            await using var connection = new SqlConnection(settings.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.KeepIdentity, transaction);
                    bulkCopy.DestinationTableName = tableName;

                    var dataColumns = new Dictionary<ColumnMapping, DataColumn>();
                    foreach (ColumnMapping columnMapping in settings.ColumnMappings)
                    {
                        DataColumn dbColumn = new DataColumn(columnMapping.ColumnName, Type.GetType(columnMapping.DataType));
                        dataColumns.Add(columnMapping, dbColumn);
                        bulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(dbColumn.ColumnName, dbColumn.ColumnName));
                    }

                    var dataTable = new DataTable();
                    dataTable.Columns.AddRange(dataColumns.Values.ToArray());

                    var batches = dataItems.Buffer(settings.BatchSize);
                    await foreach (var batch in batches.WithCancellation(cancellationToken))
                    {
                        foreach (var item in batch)
                        {
                            var fieldNames = item.GetFieldNames().ToList();
                            DataRow row = dataTable.NewRow();
                            foreach (var columnMapping in dataColumns)
                            {
                                DataColumn column = columnMapping.Value;
                                ColumnMapping mapping = columnMapping.Key;

                                string? fieldName = mapping.GetFieldName();
                                if (fieldName != null)
                                {
                                    object? value = null;
                                    var sourceField = fieldNames.FirstOrDefault(n => n.Equals(fieldName, StringComparison.CurrentCultureIgnoreCase));
                                    if (sourceField != null)
                                    {
                                        value = item.GetValue(sourceField);
                                    }

                                    if (value != null || mapping.AllowNull)
                                    {
                                        if (value is IDataItem child)
                                        {
                                            value = child.AsJsonString(false, false);
                                        }
                                        row[column.ColumnName] = value;
                                    }
                                    else
                                    {
                                        row[column.ColumnName] = mapping.DefaultValue;
                                    }
                                }
                            }
                            dataTable.Rows.Add(row);
                        }
                        await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
                        dataTable.Clear();
                    }

                    await transaction.CommitAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error copying data to table {tableName}: {ex.Message}");
                    await transaction.RollbackAsync(cancellationToken);
                }
            }

            await connection.CloseAsync();
        }

        public IEnumerable<IDataExtensionSettings> GetSettings()
        {
            yield return new SqlServerSinkSettings();
        }
    }
}
