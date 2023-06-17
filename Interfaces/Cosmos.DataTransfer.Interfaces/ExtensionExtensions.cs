namespace Cosmos.DataTransfer.Interfaces;

public static class ExtensionExtensions
{
    public const string BetaExtensionTag = "(beta)";

    public static bool MatchesExtensionSelection<T>(this T extension, string selectionName)
        where T : class, IDataTransferExtension
    {
        var validNames = new List<string> { extension.DisplayName, $"{extension.DisplayName}{BetaExtensionTag}" };
        if (extension is IAliasedDataTransferExtension aliased)
        {
            validNames.AddRange(aliased.Aliases);
            validNames.AddRange(aliased.Aliases.Select(a => $"{a}{BetaExtensionTag}"));
        }

        return validNames.Any(n => selectionName.Equals(n, StringComparison.OrdinalIgnoreCase));
    }
}
