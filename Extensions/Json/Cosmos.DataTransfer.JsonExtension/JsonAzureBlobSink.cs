using System.ComponentModel.Composition;
using Cosmos.DataTransfer.AzureBlobStorage;
using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.JsonExtension;

[Export(typeof(IDataSinkExtension))]
public class JsonAzureBlobSink : CompositeSinkExtension<AzureBlobDataSink, JsonFormatWriter>
{
    public override string DisplayName => "JSON-AzureBlob";
}