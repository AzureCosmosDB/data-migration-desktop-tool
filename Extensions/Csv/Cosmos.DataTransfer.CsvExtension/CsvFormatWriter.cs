using System.Globalization;
using System.Runtime.CompilerServices;
using Cosmos.DataTransfer.CsvExtension.Settings;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Common;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cosmos.DataTransfer.CsvExtension;

public class CsvFormatWriter : IFormattedDataWriter, IProgressAwareFormattedDataWriter
{
    public IEnumerable<IDataExtensionSettings> GetSettings()
    {
        yield return new CsvWriterSettings();
    }

    public async Task FormatDataAsync(IAsyncEnumerable<IDataItem> dataItems, Stream target, IConfiguration config, ILogger logger, CancellationToken cancellationToken = default)
    {
        // Call the progress-aware version with null progress
        await FormatDataAsync(dataItems, target, config, logger, null, cancellationToken);
    }

    public async Task FormatDataAsync(IAsyncEnumerable<IDataItem> dataItems, Stream target, IConfiguration config, ILogger logger, IProgress<DataTransferProgress>? progress, CancellationToken cancellationToken = default)
    {
        var settings = config.Get<CsvWriterSettings>() ?? new CsvWriterSettings();
        settings.Validate();

        // Track progress locally
        int itemCount = 0;

        await using var textWriter = new StreamWriter(target, leaveOpen: true);
        await using var writer = new CsvWriter(textWriter, new CsvConfiguration(settings.GetCultureInfo())
        {
            Delimiter = settings.Delimiter ?? ",",
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
                writer.WriteField(item.GetValue(field));
            }

            firstRecord = false;
            itemCount++;
            
            // Report progress if progress reporter is available
            if (progress != null && itemCount % settings.ItemProgressFrequency == 0)
            {
                progress.Report(new DataTransferProgress(itemCount, 0, $"Formatted {itemCount} items for transfer"));
            }
        }

        await writer.FlushAsync();
        
        // Report final count
        if (progress != null && itemCount > 0)
        {
            progress.Report(new DataTransferProgress(itemCount, 0, null));
        }
    }
}
