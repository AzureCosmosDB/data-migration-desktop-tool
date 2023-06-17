using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.Composition;
using Cosmos.DataTransfer.AwsS3Storage;
using Cosmos.DataTransfer.AzureBlobStorage;

namespace Cosmos.DataTransfer.CsvExtension;

[Export(typeof(IDataSinkExtension))]
public class CsvFileSink : CompositeSinkExtension<FileDataSink, CsvFormatWriter>
{
    public override string DisplayName => "CSV-File";
}
