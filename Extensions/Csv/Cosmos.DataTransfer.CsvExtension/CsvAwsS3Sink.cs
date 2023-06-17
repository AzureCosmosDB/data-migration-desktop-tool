using Cosmos.DataTransfer.AwsS3Storage;
using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.Composition;

namespace Cosmos.DataTransfer.CsvExtension;

[Export(typeof(IDataSinkExtension))]
public class CsvAwsS3Sink : CompositeSinkExtension<AwsS3DataSink, CsvFormatWriter>
{
    public override string DisplayName => "CSV-AWSS3";
}
