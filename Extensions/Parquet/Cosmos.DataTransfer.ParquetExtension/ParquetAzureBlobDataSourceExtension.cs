using Cosmos.DataTransfer.AzureBlobStorage;
using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.Composition;

namespace Cosmos.DataTransfer.ParquetExtension;

[Export(typeof(IDataSourceExtension))]
public class ParquetAzureBlobDataSourceExtension : CompositeSourceExtension<AzureBlobDataSource, ParquetFormatReader>
{
    public override string DisplayName => "Parquet-AzureBlob";
}