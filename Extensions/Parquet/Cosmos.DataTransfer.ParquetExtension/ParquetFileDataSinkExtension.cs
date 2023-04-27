using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.Composition;
using Cosmos.DataTransfer.Common;

namespace Cosmos.DataTransfer.ParquetExtension
{
    [Export(typeof(IDataSinkExtension))]
    public class ParquetFileDataSinkExtension : CompositeSinkExtension<FileDataSink, ParquetFormatWriter>
    {
        public override string DisplayName => "Parquet";
    }
}
