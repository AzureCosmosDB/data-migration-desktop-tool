using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.Composition;
using Cosmos.DataTransfer.AwsS3Storage;

namespace Cosmos.DataTransfer.JsonExtension;

[Export(typeof(IDataSourceExtension))]
public class JsonAwsS3Source : CompositeSourceExtension<AwsS3DataSource, JsonFormatReader>
{
    public override string DisplayName => "JSON-AWSS3";
}