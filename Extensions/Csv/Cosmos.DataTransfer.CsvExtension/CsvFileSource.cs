using System.ComponentModel.Composition;
using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.CsvExtension;

[Export(typeof(IDataSourceExtension))]
public class CsvFileSource : CompositeSourceExtension<FileDataSource, CsvFormatReader>, IAliasedDataTransferExtension
{
    public override string DisplayName => "CSV";
    public IEnumerable<string> Aliases => new[] { "CSV-File" };
}
