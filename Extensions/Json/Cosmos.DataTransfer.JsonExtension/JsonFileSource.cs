using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.Composition;

namespace Cosmos.DataTransfer.JsonExtension;

[Export(typeof(IDataSourceExtension))]
public class JsonFileSource : CompositeSourceExtension<FileDataSource, JsonFormatReader>
{
    public override string DisplayName => "JSON-File(beta)";
}
