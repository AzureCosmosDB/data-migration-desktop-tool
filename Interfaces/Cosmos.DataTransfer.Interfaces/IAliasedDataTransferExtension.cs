namespace Cosmos.DataTransfer.Interfaces;
public interface IAliasedDataTransferExtension
{
    IEnumerable<string> Aliases { get; }
}
