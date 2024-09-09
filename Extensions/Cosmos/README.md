# Cosmos Extension

The Cosmos data transfer extension provides source and sink capabilities for reading from and writing to containers in Cosmos DB using the Core (SQL) API. Source and sink both support string, number, and boolean property values, arrays, and hierarchical nested object structures.

> **Note**: When specifying the JSON extension as the Source or Sink property in configuration, utilize the name **Cosmos-nosql**.

## Settings

Source and sink require settings used to locate and access the Cosmos DB account. This can be done in one of two ways:

- Using a `ConnectionString` that includes an AccountEndpoint and AccountKey
- Using RBAC (Role Based Access Control) by setting `UseRbacAuth` to true and specifying `AccountEndpoint` and optionally `EnableInteractiveCredentials` to prompt the user to log in to Azure if default credentials are not available. See ([migrate-passwordless](https://learn.microsoft.com/azure/cosmos-db/nosql/migrate-passwordless?tabs=sign-in-azure-cli%2Cdotnet%2Cazure-portal-create%2Cazure-portal-associate%2Capp-service-identity) for how to configure Cosmos DB for passwordless access.

Source and sink settings also both require parameters to specify the data location within a Cosmos DB account:

- `Database`
- `Container`

Source supports an optional `IncludeMetadataFields` parameter (`false` by default) to enable inclusion of built-in Cosmos fields prefixed with `"_"`, for example `"_etag"` and `"_ts"`. An optional PartitionKeyValue setting allows for filtering to a single partition. The optional Query setting allows further filtering using a Cosmos SQL statement. An optional `WebProxy` parameter (`null` by default) enables connections through a proxy.

### Always Encrypted

Source and Sink support Always Encrypted as an optional parameter. When `InitClientEncryption` is set to `true`, the extension will initialize the Cosmos client with the Always Encrypted feature enabled. This allows for the use of encrypted fields in the Cosmos DB container. The extension will automatically decrypt the fields when reading from the source and encrypt the fields when writing to the sink. 
</br>
The extension will also automatically handle the encryption keys and encryption policy for the client, but it requires `UseRbacAuth` to be set to `true` and the user to have the necessary permissions to access the key vault.
</br>
> **Note**: To use Always Encrypted, Cosmos DB container must be pre-configured with the necessary encryption policy and the user must have the necessary permissions to access the key vault.

### Source

```json
{
    "ConnectionString": "AccountEndpoint=https://...",
    "Database":"myDb",
    "Container":"myContainer",
    "IncludeMetadataFields": false,
    "PartitionKeyValue":"123",
    "Query":"SELECT * FROM c WHERE c.category='event'",
    "WebProxy":"http://yourproxy.server.com/"
}
```

Or with RBAC:

```json
{
    "UseRbacAuth": true,
    "AccountEndpoint": "https://...",
    "EnableInteractiveCredentials": true,
    "Database":"myDb",
    "Container":"myContainer",
    "IncludeMetadataFields": false,
    "PartitionKeyValue":"123",
    "Query":"SELECT * FROM c WHERE c.category='event'",
    "InitClientEncryption": false
    "WebProxy":"http://yourproxy.server.com/"
}
```

Sink requires an additional `PartitionKeyPath` parameter which is used when creating the container if it does not exist. To use hierarchical partition keys, instead use the `PartitionKeyPaths` setting to supply an array of up to 3 paths. It also supports an optional `RecreateContainer` parameter (`false` by default) to delete and then recreate the container to ensure only newly imported data is present. The optional `BatchSize` parameter (100 by default) sets the number of items to accumulate before inserting. `ConnectionMode` can be set to either `Gateway` (default) or `Direct` to control how the client connects to the CosmosDB service. For situations where a container is created as part of the transfer operation `CreatedContainerMaxThroughput` (in RUs) and `UseAutoscaleForCreatedContainer` provide the initial throughput settings which will be in effect when executing the transfer. To instead use shared throughput that has been provisioned at the database level, set the `UseSharedThroughput` parameter to `true`. The optional `WriteMode` parameter specifies the type of data write to use: `InsertStream`, `Insert`, `UpsertStream`, or `Upsert`. The `IsServerlessAccount` parameter specifies whether the target account uses Serverless instead of Provisioned throughput, which affects the way containers are created. Additional parameters allow changing the behavior of the Cosmos client appropriate to your environment. The `PreserveMixedCaseIds` parameter (`false` by default) ignores differently cased `id` fields and writes them through without modification, while generating a separate lowercased `id` field as required by Cosmos. The `IgnoreNullValues` parameter allows for excluding fields with null values when writing to Cosmos DB.

### Sink

```json
{
    "ConnectionString": "AccountEndpoint=https://...",
    "Database":"myDb",
    "Container":"myContainer",
    "PartitionKeyPath":"/id",
    "RecreateContainer": false,
    "BatchSize": 100,
    "ConnectionMode": "Gateway",
    "MaxRetryCount": 5,
    "InitialRetryDurationMs": 200,
    "CreatedContainerMaxThroughput": 1000,
    "UseAutoscaleForCreatedContainer": true,
    "WriteMode": "InsertStream",
    "PreserveMixedCaseIds": false,
    "IgnoreNullValues": false,
    "IsServerlessAccount": false,
    "UseSharedThroughput": false,
    "InitClientEncryption": false
}
```
