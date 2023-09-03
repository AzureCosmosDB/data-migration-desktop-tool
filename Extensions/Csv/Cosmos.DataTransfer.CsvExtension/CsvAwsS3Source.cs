using Cosmos.DataTransfer.AwsS3Storage;
using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.Composition;

namespace Cosmos.DataTransfer.CsvExtension;

[Export(typeof(IDataSourceExtension))]
public class CsvAwsS3Source : CompositeSourceExtension<AwsS3DataSource, CsvFormatReader>
{
    public override string DisplayName => "CSV-AWSS3";
}
