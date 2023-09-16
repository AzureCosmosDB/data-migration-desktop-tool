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

                if (source != null)
                {
                    var seekableStream = source;
                    using var tempStream = new MemoryStream();
                    if (source.CanSeek == false || !source.TryGetSize(out _))
                    {
                        logger.LogInformation("Source stream is not seekable or size is not known. Copying to temporary stream.");
                        await seekableStream.CopyToAsync(tempStream, cancellationToken);
                        tempStream.Position = 0;
                        seekableStream = tempStream;
                    }

                    using ParquetReader reader = await ParquetReader.CreateAsync(seekableStream, cancellationToken: cancellationToken);
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
                            temp.Add(x.Field.Name, x.Data.Cast<object?>().ElementAtOrDefault(i));
                        }
                        yield return new ParquetDictionaryDataItem(temp);
                    }
                }

                logger.LogInformation("Completed reading source file");
            }
        }

        public IEnumerable<IDataExtensionSettings> GetSettings()
        {
            yield return new ParquetSourceSettings();
        }
    }
}