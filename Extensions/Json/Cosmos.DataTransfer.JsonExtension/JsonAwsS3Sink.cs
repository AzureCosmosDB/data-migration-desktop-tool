using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.Composition;
using Cosmos.DataTransfer.AwsS3Storage;

namespace Cosmos.DataTransfer.JsonExtension;

[Export(typeof(IDataSinkExtension))]
public class JsonAwsS3Sink : CompositeSinkExtension<AwsS3DataSink, JsonFormatWriter>
{
    public override string DisplayName => "JSON-AWSS3";
}