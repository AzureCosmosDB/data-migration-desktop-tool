namespace Cosmos.DataTransfer.Interfaces;

/// <summary>
/// A transfer failure with a safe public message. The original exception is retained
/// for programmatic inspection, but must not be sent to a logging provider.
/// </summary>
public sealed class DataTransferException : Exception
{
    public DataTransferException(string safeMessage, Exception innerException)
        : base(safeMessage, innerException)
    {
    }

    public override string ToString() => $"{GetType().FullName}: {Message}";
}
