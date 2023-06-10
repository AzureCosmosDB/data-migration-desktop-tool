using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.Composition;

namespace Cosmos.DataTransfer.CsvExtension;

[Export(typeof(IDataSinkExtension))]
public class CsvFileSinkExtension : CompositeSinkExtension<FileDataSink, CsvFormatWriter>
{
    public override string DisplayName => "CSV-File";
}
