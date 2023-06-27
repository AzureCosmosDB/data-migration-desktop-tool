using Cosmos.DataTransfer.AzureBlobStorage;
using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.Composition;

namespace Cosmos.DataTransfer.CsvExtension;

[Export(typeof(IDataSinkExtension))]
public class CsvAzureBlobSink : CompositeSinkExtension<AzureBlobDataSink, CsvFormatWriter>
{
    public override string DisplayName => "CSV-AzureBlob";
}
