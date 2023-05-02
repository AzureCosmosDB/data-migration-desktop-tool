using Cosmos.DataTransfer.AzureBlobStorage;
using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.Composition;

namespace Cosmos.DataTransfer.ParquetExtension
{
    [Export(typeof(IDataSinkExtension))]
    public class ParquetAzureBlobDataSinkExtension : CompositeSinkExtension<AzureBlobDataSink, ParquetFormatWriter>
    {
        public override string DisplayName => "Parquet-AzureBlob(beta)";
    }
}
