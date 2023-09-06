using System.ComponentModel.Composition;
using Cosmos.DataTransfer.AzureBlobStorage;
using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.JsonExtension;

[Export(typeof(IDataSourceExtension))]
public class JsonAzureBlobSource : CompositeSourceExtension<AzureBlobDataSource, JsonFormatReader>
{
    public override string DisplayName => "JSON-AzureBlob";
}