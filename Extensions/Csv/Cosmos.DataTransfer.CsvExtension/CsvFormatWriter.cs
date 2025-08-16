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
        var settings = config.Get<CsvWriterSettings>() ?? new CsvWriterSettings();
        settings.Validate();

        await using var textWriter = new StreamWriter(target, leaveOpen: true);
        await using var writer = new CsvWriter(textWriter, new CsvConfiguration(settings.GetCultureInfo())
        {
            Delimiter = settings.Delimiter ?? ",",
            HasHeaderRecord = settings.IncludeHeader,
        });

        var headerWritten = false;
        var firstRecord = true;
        int documentCount = 0;
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
            documentCount++;
            
            // Log progress periodically based on DocumentProgressFrequency setting
            if (documentCount % settings.DocumentProgressFrequency == 0)
            {
                logger.LogInformation("Processed {DocumentCount} documents for transfer to Azure Blob", documentCount);
            }
        }

        await writer.FlushAsync();
        
        // Log final document count
        if (documentCount > 0)
            logger.LogInformation("Completed processing {DocumentCount} total documents for transfer to Azure Blob", documentCount);
        else
            logger.LogWarning("No documents were processed for transfer to Azure Blob");
    }
}
