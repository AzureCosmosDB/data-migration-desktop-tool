using Cosmos.DataTransfer.AzureBlobStorage;
using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.Composition;

namespace Cosmos.DataTransfer.CsvExtension;

[Export(typeof(IDataSourceExtension))]
public class CsvAzureBlobSource : CompositeSourceExtension<AzureBlobDataSource, CsvFormatReader>
{
    public override string DisplayName => "CSV-AzureBlob";
}
