using Cosmos.DataTransfer.AwsS3Storage;
using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.Composition;

namespace Cosmos.DataTransfer.ParquetExtension
{
    [Export(typeof(IDataSinkExtension))]
    public class ParquetAwsS3DataSinkExtension : CompositeSinkExtension<AwsS3DataSink, ParquetFormatWriter>
    {
        public override string DisplayName => "Parquet-AWSS3(beta)";
    }
}
