using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.ParquetExtension.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Parquet.Schema;
using Parquet;
using System.Runtime.CompilerServices;
using Parquet.Data;

namespace Cosmos.DataTransfer.ParquetExtension
{
    public class ParquetFormatReader : IFormattedDataReader
    {
        public async IAsyncEnumerable<IDataItem> ParseDataAsync(IComposableDataSource sourceExtension, IConfiguration config, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var settings = config.Get<ParquetSourceSettings>();
            settings.Validate();
            
            var sources = sourceExtension.ReadSourceAsync(config, logger, cancellationToken);
            await foreach (var source in sources.WithCancellation(cancellationToken))
            {
                logger.LogInformation("Reading source file");

                using ParquetReader reader = await ParquetReader.CreateAsync(source, cancellationToken: cancellationToken);
                var coldata = new List<DataColumn>();
                var numberofrows = 0;
                //check if number of rows are same for each column.
                for (int i = 0; i < reader.RowGroupCount; i++)
                {
                    using ParquetRowGroupReader rowGroupReader = reader.OpenRowGroupReader(i);
                    foreach (DataField df in reader.Schema.GetDataFields())
                    {
                        var temp = await rowGroupReader.ReadColumnAsync(df, cancellationToken);
                        if (numberofrows == 0)
                        {
                            numberofrows = temp.Data.Length;
                        }
                        else
                        {
                            if (numberofrows != temp.Data.Length)
                            {
                                logger.LogInformation("Number of rows in '{colname}' column does not match the rest.", temp.Field.Name);
                            }
                        }
                        coldata.Add(temp);
                    }
                }
                for (var i = 0; i < numberofrows; i++)
                {
                    var temp = new Dictionary<string, object?>();
                    foreach (var x in coldata)
                    {
                        temp.Add(x.Field.Name, x.Data.GetValue(i));

                    }
                    yield return new ParquetDictionaryDataItem(temp);
                }
                logger.LogInformation("Completed reading source file");
            }
        }
    }
}