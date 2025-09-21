using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.ParquetExtension.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Parquet;
using Parquet.Schema;
using System.Data;

namespace Cosmos.DataTransfer.ParquetExtension
{
    public class ParquetFormatWriter : IFormattedDataWriter, IProgressAwareFormattedDataWriter
    {
        private List<ParquetDataCol> parquetDataCols = new List<ParquetDataCol>();

        public async Task FormatDataAsync(IAsyncEnumerable<IDataItem> dataItems, Stream target, IConfiguration config, ILogger logger, CancellationToken cancellationToken = default)
        {
            // Call the progress-aware version with null progress
            await FormatDataAsync(dataItems, target, config, logger, null, cancellationToken);
        }

        public async Task FormatDataAsync(IAsyncEnumerable<IDataItem> dataItems, Stream target, IConfiguration config, ILogger logger, IProgress<DataTransferProgress>? progress, CancellationToken cancellationToken = default)
        {
            var settings = config.Get<ParquetSinkSettings>() ?? new ParquetSinkSettings();
            settings.Validate();

            logger.LogInformation("Writing parquet format");
            long row = 0;
            int itemCount = 0;
            await foreach (var item in dataItems.WithCancellation(cancellationToken))
            {
                ProcessColumns(item, row);
                row++;
                itemCount++;
                
                // Report progress if progress reporter is available
                if (progress != null && itemCount % settings.ItemProgressFrequency == 0)
                {
                    progress.Report(new DataTransferProgress(itemCount, 0, $"Formatted {itemCount} items for transfer"));
                }
            }

            var schema = CreateSchema();
            CreateParquetColumns();
            await SaveFile(schema, target, cancellationToken);
            
            // Report final count
            if (progress != null && itemCount > 0)
            {
                progress.Report(new DataTransferProgress(itemCount, 0, null));
            }
        }

        private void ProcessColumns(IDataItem item, long row)
        {
            var itemcolumns = item.GetFieldNames();
            foreach (var col in itemcolumns)
            {
                var current = parquetDataCols.FirstOrDefault(c => c.ColumnName == col);
                var colval = item.GetValue(col);
                var coltype = Type.Missing.GetType();
                if (colval != null)
                {
                    coltype = colval.GetType();
                }
                if (current == null)
                {
                    var newcol = new ParquetDataCol(col, coltype);
                    newcol.AddColumnValue(row, colval);
                    parquetDataCols.Add(newcol);
                }
                else if (coltype != Type.Missing.GetType() && current.ColumnType != coltype)
                {
                    if (current != null)
                    {
                        current.ColumnType = coltype;
                        if (coltype != null)
                        {
                            current.ParquetDataType = new DataField(col, coltype, true);
                        }
                    }
                }
                if (current != null)
                {
                    current.AddColumnValue(row, colval);
                }
            }
        }

        private void CreateParquetColumns()
        {
            for (var i = 0; i < parquetDataCols.Count; i++)
            {

                var current = parquetDataCols[i];
                switch (current.ParquetDataType.ClrType.Name)
                {
                    case "String":
                        current.ParquetDataColumn = new Parquet.Data.DataColumn(current.ParquetDataType, current.ColumnData.Cast<string?>().ToArray());
                        break;
                    case "Int32":
                        current.ParquetDataColumn = new Parquet.Data.DataColumn(current.ParquetDataType, current.ColumnData.Cast<int?>().ToArray());
                        break;
                    case "Int16":
                        current.ParquetDataColumn = new Parquet.Data.DataColumn(current.ParquetDataType, current.ColumnData.Cast<short?>().ToArray());
                        break;
                    case "Int64":
                        current.ParquetDataColumn = new Parquet.Data.DataColumn(current.ParquetDataType, current.ColumnData.Cast<long?>().ToArray());
                        break;
                    case "DateTime":
                        current.ParquetDataColumn = new Parquet.Data.DataColumn(current.ParquetDataType, current.ColumnData.Cast<DateTime?>().ToArray());
                        break;
                    case "Boolean":
                        current.ParquetDataColumn = new Parquet.Data.DataColumn(current.ParquetDataType, current.ColumnData.Cast<bool?>().ToArray());
                        break;
                    case "Float":
                        current.ParquetDataColumn = new Parquet.Data.DataColumn(current.ParquetDataType, current.ColumnData.Cast<float?>().ToArray());
                        break;
                    case "Double":
                        current.ParquetDataColumn = new Parquet.Data.DataColumn(current.ParquetDataType, current.ColumnData.Cast<double?>().ToArray());
                        break;
                }
            }
        }

        private ParquetSchema CreateSchema()
        {
            var arr = new List<Field>();
            for (var i = 0; i < parquetDataCols.Count; i++)
            {
                arr.Add(parquetDataCols[i].ParquetDataType);
            }
            return new ParquetSchema(arr);
        }

        private async Task SaveFile(ParquetSchema schema, Stream stream, CancellationToken cancellationToken)
        {
            using ParquetWriter writer = await ParquetWriter.CreateAsync(schema, stream, cancellationToken: cancellationToken);
            using ParquetRowGroupWriter groupWriter = writer.CreateRowGroup();
            foreach (var col in parquetDataCols)
            {
                if (col.ParquetDataColumn != null)
                {
                    await groupWriter.WriteColumnAsync(col.ParquetDataColumn, cancellationToken);
                }
            }
        }

        public IEnumerable<IDataExtensionSettings> GetSettings()
        {
            yield return new ParquetSinkSettings();
        }
    }
}
