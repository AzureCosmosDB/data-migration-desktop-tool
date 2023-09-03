using Cosmos.DataTransfer.AwsS3Storage;
using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.Composition;

namespace Cosmos.DataTransfer.ParquetExtension;

[Export(typeof(IDataSourceExtension))]
public class ParquetAwsS3DataSourceExtension : CompositeSourceExtension<AwsS3DataSource, ParquetFormatReader>
{
    public override string DisplayName => "Parquet-AWSS3";
}