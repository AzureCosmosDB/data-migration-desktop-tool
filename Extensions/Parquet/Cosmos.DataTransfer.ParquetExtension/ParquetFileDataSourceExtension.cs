using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.Composition;

namespace Cosmos.DataTransfer.ParquetExtension
{
    [Export(typeof(IDataSourceExtension))]
    public class ParquetFileDataSourceExtension : CompositeSourceExtension<FileDataSource, ParquetFormatReader>, IAliasedDataTransferExtension
    {
        public override string DisplayName => "Parquet";
        public IEnumerable<string> Aliases => new[] { "Parquet-File" };
    }
}