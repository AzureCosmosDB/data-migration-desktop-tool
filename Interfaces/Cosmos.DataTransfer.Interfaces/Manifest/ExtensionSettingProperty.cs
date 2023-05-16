namespace Cosmos.DataTransfer.Interfaces.Manifest;

public record ExtensionSettingProperty(string Name, PropertyType Type, object? DefaultValue = null, bool IsRequired = false, bool IsSensitive = false)
{
    public static ExtensionSettingProperty Empty = new("--", PropertyType.Undeclared);

    public List<string> ValidValues { get; init; } = new();
}
