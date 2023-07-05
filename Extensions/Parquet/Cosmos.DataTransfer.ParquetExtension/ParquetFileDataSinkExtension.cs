using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.Composition;
using Cosmos.DataTransfer.Common;

namespace Cosmos.DataTransfer.ParquetExtension
{
    [Export(typeof(IDataSinkExtension))]
    public class ParquetFileDataSinkExtension : CompositeSinkExtension<FileDataSink, ParquetFormatWriter>, IAliasedDataTransferExtension
    {
        public override string DisplayName => "Parquet";
        public IEnumerable<string> Aliases => new[] { "Parquet-File" };
    }
}
