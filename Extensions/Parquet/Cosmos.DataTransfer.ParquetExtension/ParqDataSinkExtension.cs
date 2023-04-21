using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.ParqExtension.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Parquet.Schema;
using Parquet;
using System.ComponentModel.Composition;
using System.Data;

namespace Cosmos.DataTransfer.ParquetExtension
{
    [Export(typeof(IDataSinkExtension))]
    public class ParqDataSinkExtension : IDataSinkExtension
    {
        public string DisplayName => "Parquet";
        public List<ParquetDataCol> parquetDataCols = new List<ParquetDataCol>();
        public async Task WriteAsync(IAsyncEnumerable<IDataItem> dataItems, IConfiguration config, IDataSourceExtension dataSource, ILogger logger, CancellationToken cancellationToken)
        {
            var settings = config.Get<ParquetSinkSettings>();
            settings.Validate();
            if (settings != null && settings.FilePath != null)
            {
                logger.LogInformation("Writing to file '{FilePath}", settings.FilePath);
                await foreach (var item in dataItems.WithCancellation(cancellationToken))
                {
                    ProcessColumns(item);
                }
                var schema = CreateSchema();
                CreateParquetColumns();
                await SaveFile(schema, settings.FilePath, cancellationToken);
                logger.LogInformation("Completed writing data to file '{FilePath}'", settings.FilePath);
                if (settings.UploadToS3 == true)
                {
                    if (settings.S3Region != null && settings.S3BucketName != null && settings.S3AccessKey !=null && settings.S3SecretKey !=null)
                    {
                        logger.LogInformation("Saving file to AWS S3 Bucket '{BucketName}'", settings.S3BucketName);
                        await SaveToS3(settings, cancellationToken);
                    }
                    else
                    {
                        logger.LogError("S3 Requires S3Region, S3BucketName, S3AccessKey, and S3SecretKey to be set.");
                    }                    
                }                
            }
        }

        private void ProcessColumns(IDataItem item)
        {
            var itemcolumns = item.GetFieldNames();
            foreach (var col in itemcolumns)
            {
                var current = parquetDataCols.FirstOrDefault(c => c.ColumnName == col);
                var colval = item.GetValue(col);
                var coltype = System.Type.Missing.GetType();
                if (colval != null)
                {
                    coltype = colval.GetType();
                }
                if (current == null)
                {
                    var newcol = new ParquetDataCol(col, coltype);
                    newcol.ColumnData.Add(colval);
                    parquetDataCols.Add(newcol);
                }
                else if (coltype != System.Type.Missing.GetType() && current.ColumnType != coltype)
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
                    current.ColumnData.Add(colval);
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
                        current.ParquetDataColumn = new Parquet.Data.DataColumn(current.ParquetDataType, current.ColumnData.Cast<String?>().ToArray());
                        break;
                    case "Int32":
                        current.ParquetDataColumn = new Parquet.Data.DataColumn(current.ParquetDataType, current.ColumnData.Cast<Int32?>().ToArray());
                        break;
                    case "Int16":
                        current.ParquetDataColumn = new Parquet.Data.DataColumn(current.ParquetDataType, current.ColumnData.Cast<Int16?>().ToArray());
                        break;
                    case "Int64":
                        current.ParquetDataColumn = new Parquet.Data.DataColumn(current.ParquetDataType, current.ColumnData.Cast<Int64?>().ToArray());
                        break;
                    case "DateTime":
                        current.ParquetDataColumn = new Parquet.Data.DataColumn(current.ParquetDataType, current.ColumnData.Cast<DateTime?>().ToArray());
                        break;
                    case "Boolean":
                        current.ParquetDataColumn = new Parquet.Data.DataColumn(current.ParquetDataType, current.ColumnData.Cast<Boolean?>().ToArray());
                        break;
                    case "Float":
                        current.ParquetDataColumn = new Parquet.Data.DataColumn(current.ParquetDataType, current.ColumnData.Cast<float?>().ToArray());
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

        private async Task SaveFile(ParquetSchema schema, string filepath, CancellationToken cancellationToken)
        {
            using Stream fs = File.OpenWrite(filepath);
            using ParquetWriter writer = await ParquetWriter.CreateAsync(schema, fs);
            using ParquetRowGroupWriter groupWriter = writer.CreateRowGroup();
            foreach (var col in parquetDataCols)
            {
                if (col.ParquetDataColumn != null)
                {
                    await groupWriter.WriteColumnAsync(col.ParquetDataColumn, cancellationToken);
                }
            }           
        }

        private async Task SaveToS3(ParquetSinkSettings settings, CancellationToken cancellationToken)
        {
            S3Writer.InitializeS3Client(settings.S3AccessKey, settings.S3SecretKey, settings.S3Region);
            await S3Writer.WriteToS3(settings.S3BucketName, settings.FilePath, cancellationToken);
        }
    }
}
