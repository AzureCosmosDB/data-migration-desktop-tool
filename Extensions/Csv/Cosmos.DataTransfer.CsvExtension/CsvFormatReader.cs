using System.Globalization;
using System.Runtime.CompilerServices;
using Cosmos.DataTransfer.CsvExtension.Settings;
using Cosmos.DataTransfer.Interfaces;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.CsvExtension;

public class CsvFormatReader : IFormattedDataReader
{
    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield return new CsvReaderSettings();
    }

    public async IAsyncEnumerable<IDataItem> ParseDataAsync(IComposableDataSource sourceExtension, IConfiguration config, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var settings = config.Get<CsvReaderSettings>();
        settings.Validate();

        var data = sourceExtension.ReadSourceAsync(config, logger, cancellationToken);
        await foreach (var source in data.WithCancellation(cancellationToken))
        {
            if (source == null)
                continue;

            using var textReader = new StreamReader(source, leaveOpen: true);
            using var reader = new CsvReader(textReader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = settings.HasHeader,
                Delimiter = settings.Delimiter,
            });

            if (settings.HasHeader)
            {
                await reader.ReadAsync();
                reader.ReadHeader();
            }

            int rowCount = 0;
            while (await reader.ReadAsync())
            {
                rowCount++;
                var values = new Dictionary<string, object?>();
                for (int i = 0; i < reader.Parser.Count; i++)
                {
                    var value = reader.GetField(i);
                    var columnName = string.Format(settings.ColumnNameFormat ?? "{0}", i);
                    if (settings.HasHeader)
                    {
                        var header = reader.HeaderRecord?.ElementAtOrDefault(i);
                        columnName = header;
                    }
                    if (columnName != null)
                    {
                        values[columnName] = value;
                    }
                    else
                    {
                        logger.LogWarning("Missing column name for Value: {value} in row {rowNumber}", value, rowCount);
                    }
                }
                yield return new DictionaryDataItem(values);
            }
        }
    }
}
