using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.ParqExtension.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Parquet.Schema;
using Parquet;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using Parquet.Data;

namespace Cosmos.DataTransfer.ParquetExtension
{
    [Export(typeof(IDataSourceExtension))]
    public class ParquetDataSourceExtension : IDataSourceExtension
    {
        public string DisplayName => "Parquet";

        public async IAsyncEnumerable<IDataItem> ReadAsync(IConfiguration config, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var coldata = new List<DataColumn>();
            var settings = config.Get<ParquetSinkSettings>();
            settings.Validate();
            if (settings.FilePath != null)
            {
                logger.LogInformation("Reading file '{FilePath}", settings.FilePath);

                using Stream fs = System.IO.File.OpenRead(settings.FilePath);
                using ParquetReader reader = await ParquetReader.CreateAsync(fs);
                var numberofrows = 0;
                //check if number of rows are same for each column.
                for (int i = 0; i < reader.RowGroupCount; i++)
                {
                    using ParquetRowGroupReader rowGroupReader = reader.OpenRowGroupReader(i);
                    foreach (DataField df in reader.Schema.GetDataFields())
                    {
                        var temp = await rowGroupReader.ReadColumnAsync(df);
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
                logger.LogInformation("Completed reading '{FilePath}'", settings.FilePath);
            }
        }

    }
}