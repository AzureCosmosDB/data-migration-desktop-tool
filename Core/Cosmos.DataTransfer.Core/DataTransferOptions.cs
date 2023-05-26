namespace Cosmos.DataTransfer.Core;

public class DataTransferOptions
{
    public string? Source { get; set; }
    private string? _sink;
    public string? Sink { get => _sink; set => _sink = value; }
    public string? Target { get => _sink; set => _sink = value; }
    public string? Destination { get => _sink; set => _sink = value; }
    public string? SettingsPath { get; set; }
}