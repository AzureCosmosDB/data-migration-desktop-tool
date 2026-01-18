using System.ComponentModel.Composition;
using System.Data;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Common;
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

            if (settings!.WriteMode == SqlWriteMode.Upsert)
            {
                await WriteUpsertAsync(dataItems, settings, logger, cancellationToken);
            }
            else
            {
                await WriteInsertAsync(dataItems, settings, logger, cancellationToken);
            }
        }

        private async Task WriteInsertAsync(IAsyncEnumerable<IDataItem> dataItems, SqlServerSinkSettings settings, ILogger logger, CancellationToken cancellationToken)
        {
            string tableName = settings!.TableName!;
            
            // Validate table name to prevent SQL injection
            ValidateSqlIdentifier(tableName, nameof(settings.TableName));
            
            // Validate column names to prevent SQL injection
            foreach (var column in settings.ColumnMappings)
            {
                ValidateSqlIdentifier(column.ColumnName!, nameof(column.ColumnName));
            }

            await using var connection = new SqlConnection(settings.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = connection.BeginTransaction();
            
            try
            {
                using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.KeepIdentity, transaction);
                bulkCopy.DestinationTableName = tableName;

                var dataColumns = new Dictionary<ColumnMapping, DataColumn>();
                foreach (ColumnMapping columnMapping in settings.ColumnMappings)
                {
                    Type type = Type.GetType(columnMapping.DataType ?? "System.String")!;
                    DataColumn dbColumn = new DataColumn(columnMapping.ColumnName, type);
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
                logger.LogError(ex, "Error copying data to table {TableName}", tableName);
                try
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                catch (Exception rollbackEx)
                {
                    logger.LogError(rollbackEx, "Error rolling back transaction for table {TableName}", tableName);
                }
                throw;
            }

            await connection.CloseAsync();
        }

        private async Task WriteUpsertAsync(IAsyncEnumerable<IDataItem> dataItems, SqlServerSinkSettings settings, ILogger logger, CancellationToken cancellationToken)
        {
            string tableName = settings!.TableName!;
            
            // Validate table name to prevent SQL injection
            ValidateSqlIdentifier(tableName, nameof(settings.TableName));
            
            // Validate column names to prevent SQL injection
            foreach (var column in settings.ColumnMappings)
            {
                ValidateSqlIdentifier(column.ColumnName!, nameof(column.ColumnName));
            }
            foreach (var pkColumn in settings.PrimaryKeyColumns)
            {
                ValidateSqlIdentifier(pkColumn, nameof(settings.PrimaryKeyColumns));
            }
            
            string stagingTableName = $"#Staging_{Guid.NewGuid():N}";

            await using var connection = new SqlConnection(settings.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            try
            {
                // Create staging table with same structure as target table
                var createStagingTableSql = $@"
                    SELECT TOP 0 * 
                    INTO {stagingTableName}
                    FROM {tableName}";

                await using (var createCommand = new SqlCommand(createStagingTableSql, connection))
                {
                    await createCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                // Bulk insert into staging table
                using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.KeepIdentity, null))
                {
                    bulkCopy.DestinationTableName = stagingTableName;

                    var dataColumns = new Dictionary<ColumnMapping, DataColumn>();
                    foreach (ColumnMapping columnMapping in settings.ColumnMappings)
                    {
                        Type type = Type.GetType(columnMapping.DataType ?? "System.String")!;
                        DataColumn dbColumn = new DataColumn(columnMapping.ColumnName, type);
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
                }

                // Build and execute MERGE statement
                var mergeStatement = BuildMergeStatement(tableName, stagingTableName, settings);
                logger.LogInformation("Executing MERGE statement for upsert operation");
                
                await using (var mergeCommand = new SqlCommand(mergeStatement, connection))
                {
                    mergeCommand.CommandTimeout = 300; // 5 minutes timeout for large merges
                    var rowsAffected = await mergeCommand.ExecuteNonQueryAsync(cancellationToken);
                    logger.LogInformation("MERGE completed. Rows affected: {RowsAffected}", rowsAffected);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during upsert operation to table {TableName}", tableName);
                throw;
            }
            finally
            {
                // Clean up staging table (temp tables are automatically dropped on connection close)
                await connection.CloseAsync();
            }
        }

        /// <summary>
        /// Validates that a SQL identifier contains only allowed characters to prevent SQL injection.
        /// Allows alphanumeric characters, underscores, dots (for schema.table), and spaces (for quoted identifiers).
        /// </summary>
        private static void ValidateSqlIdentifier(string identifier, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException("SQL identifier cannot be null or empty.", parameterName);
            }

            // Allow alphanumeric, underscore, dot (for schema.table), space (for quoted identifiers), and brackets
            if (!System.Text.RegularExpressions.Regex.IsMatch(identifier, @"^[\w\.\s\[\]]+$"))
            {
                throw new ArgumentException(
                    $"Invalid SQL identifier '{identifier}'. Identifiers can only contain alphanumeric characters, underscores, dots, spaces, and brackets.",
                    parameterName);
            }
        }

        private string BuildMergeStatement(string targetTable, string stagingTable, SqlServerSinkSettings settings)
        {
            var allColumns = settings.ColumnMappings.Select(m => m.ColumnName).ToList();
            var primaryKeys = settings.PrimaryKeyColumns;
            var nonKeyColumns = allColumns.Except(primaryKeys).ToList();

            // Build ON clause for matching
            var onClause = string.Join(" AND ", 
                primaryKeys.Select(pk => $"target.[{pk}] = source.[{pk}]"));

            // Build INSERT columns and values
            var insertColumns = string.Join(", ", allColumns.Select(col => $"[{col}]"));
            var insertValues = string.Join(", ", allColumns.Select(col => $"source.[{col}]"));

            // Build the MERGE statement
            var mergeStatement = $@"
                MERGE {targetTable} AS target
                USING {stagingTable} AS source
                ON ({onClause})";

            // Only add UPDATE clause if there are non-key columns to update
            if (nonKeyColumns.Count > 0)
            {
                var updateSet = string.Join(", ", 
                    nonKeyColumns.Select(col => $"target.[{col}] = source.[{col}]"));
                
                mergeStatement += $@"
                WHEN MATCHED THEN
                    UPDATE SET {updateSet}";
            }

            mergeStatement += $@"
                WHEN NOT MATCHED BY TARGET THEN
                    INSERT ({insertColumns})
                    VALUES ({insertValues})";

            // Add DELETE clause if requested for full table synchronization
            if (settings.DeleteNotMatchedBySource)
            {
                mergeStatement += $@"
                WHEN NOT MATCHED BY SOURCE THEN
                    DELETE";
            }

            mergeStatement += ";";

            return mergeStatement;
        }

        public IEnumerable<IDataExtensionSettings> GetSettings()
        {
            yield return new SqlServerSinkSettings();
        }
    }
}
