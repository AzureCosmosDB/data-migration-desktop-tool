﻿using Cosmos.DataTransfer.Interfaces.Manifest;

namespace Cosmos.DataTransfer.MongoExtension.Settings;
public class MongoSourceSettings : MongoBaseSettings
{
    public string? Collection { get; set; }

    /// <summary>
    /// MongoDB query filter to apply during data migration. Can be specified as:
    /// - Direct JSON query string (e.g., "{\"field\":{\"$gte\":\"value\"}}")
    /// - Path to a JSON file containing the query
    /// </summary>
    public string? Query { get; set; }

    [SensitiveValue]
    public Dictionary<string, IReadOnlyDictionary<string, object>>? KMSProviders { get; set; }

    public string? KeyVaultNamespace { get; set; }
}
