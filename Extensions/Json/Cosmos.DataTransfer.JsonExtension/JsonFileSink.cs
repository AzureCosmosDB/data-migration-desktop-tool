using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.Composition;
using Cosmos.DataTransfer.AwsS3Storage;

namespace Cosmos.DataTransfer.JsonExtension;

[Export(typeof(IDataSinkExtension))]
public class JsonFileSink : CompositeSinkExtension<FileDataSink, JsonFormatWriter>
{
    public override string DisplayName => "JSON-File(beta)";
}
