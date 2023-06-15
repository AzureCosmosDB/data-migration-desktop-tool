using System.Globalization;
using System.Runtime.CompilerServices;
using Cosmos.DataTransfer.CsvExtension.Settings;
using Cosmos.DataTransfer.Interfaces;
using CsvHelper;
using CsvHelper.Configuration;
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
        await using var writer = new CsvWriter(textWriter, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = settings.Delimiter,
            HasHeaderRecord = settings.IncludeHeader,
        });

        var headerWritten = false;
        var firstRecord = true;
        await foreach (var item in dataItems.WithCancellation(cancellationToken))
        {
            if (!firstRecord)
            {
                await writer.NextRecordAsync();
            }

            if (settings.IncludeHeader && !headerWritten)
            {
                foreach (string field in item.GetFieldNames())
                {
                    writer.WriteField(field);
                }
                headerWritten = true;
                await writer.NextRecordAsync();
            }

            foreach (string field in item.GetFieldNames())
            {
                writer.WriteField(item.GetValue(field)?.ToString());
            }

            firstRecord = false;
        }

        await writer.FlushAsync();
    }
}
