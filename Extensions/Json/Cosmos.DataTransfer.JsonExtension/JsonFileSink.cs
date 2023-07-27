using Cosmos.DataTransfer.Common;
using Cosmos.DataTransfer.Interfaces;
using System.ComponentModel.Composition;

namespace Cosmos.DataTransfer.JsonExtension;

[Export(typeof(IDataSinkExtension))]
public class JsonFileSink : CompositeSinkExtension<FileDataSink, JsonFormatWriter>, IAliasedDataTransferExtension
{
    public override string DisplayName => "JSON";
    public IEnumerable<string> Aliases => new[] { "JSON-File" };
}

