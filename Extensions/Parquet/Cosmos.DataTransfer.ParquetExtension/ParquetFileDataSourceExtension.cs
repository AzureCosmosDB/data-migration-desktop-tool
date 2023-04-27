using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.Composition;

namespace Cosmos.DataTransfer.ParquetExtension
{
    [Export(typeof(IDataSourceExtension))]
    public class ParquetFileDataSourceExtension : CompositeSourceExtension<FileDataSource, ParquetFormatReader>
    {
        public override string DisplayName => "Parquet";
    }
}