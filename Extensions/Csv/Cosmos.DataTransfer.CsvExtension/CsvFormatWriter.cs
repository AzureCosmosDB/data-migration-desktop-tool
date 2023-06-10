using System.Globalization;
using Cosmos.DataTransfer.Interfaces;
using CsvHelper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.CsvExtension;

public class CsvFormatWriter : IFormattedDataWriter
{
    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield return new CsvWriterSettings();
    }

    public async Task FormatDataAsync(IAsyncEnumerable<IDataItem> dataItems, Stream target, IConfiguration config, ILogger logger, CancellationToken cancellationToken = default)
    {
        var settings = config.Get<CsvWriterSettings>();
        settings.Validate();

        await using var textWriter = new StreamWriter(target, leaveOpen: true);
        await using var writer = new CsvWriter(textWriter, CultureInfo.InvariantCulture);

        await foreach (var item in dataItems.WithCancellation(cancellationToken))
        {
            foreach (string field in item.GetFieldNames())
            {
                writer.WriteField(item.GetValue(field)?.ToString());
            }
            await writer.NextRecordAsync();
        }

        await writer.FlushAsync();
    }
}
