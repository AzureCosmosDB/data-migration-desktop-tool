# Cosmos Extension

The Cosmos data transfer extension provides source and sink capabilities for reading from and writing to containers in Cosmos DB using the Core (SQL) API. Source and sink both support string, number, and boolean property values, arrays, and hierarchical nested object structures.

> **Note**: When specifying the JSON extension as the Source or Sink property in configuration, utilize the name **Cosmos-nosql**.

## Settings

Source and sink require settings used to locate and access the Cosmos DB account. This can be done in one of two ways:
- Using a `ConnectionString` that includes an AccountEndpoint and AccountKey
- Using RBAC (Role Based Access Control) by setting `UseRbacAuth` to true and specifying `AccountEndpoint` and optionally `EnableInteractiveCredentials` to prompt the user to log in to Azure if default credentials are not available.

Source and sink settings also both require parameters to specify the data location within a Cosmos DB account: 
- `Database`
- `Container`

Source supports an optional `IncludeMetadataFields` parameter (`false` by default) to enable inclusion of built-in Cosmos fields prefixed with `"_"`, for example `"_etag"` and `"_ts"`. An optional PartitionKeyValue setting allows for filtering to a single partition. The optional Query setting allows further filtering using a Cosmos SQL statement.

### Source

```json
{
    "ConnectionString": "AccountEndpoint=https://...",
    "Database":"myDb",
    "Container":"myContainer",
    "IncludeMetadataFields": false,
    "PartitionKeyValue":"123",
    "Query":"SELECT * FROM c WHERE c.category='event'"
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
    "Query":"SELECT * FROM c WHERE c.category='event'"
}
```

Sink requires an additional `PartitionKeyPath` parameter which is used when creating the container if it does not exist. To use hierarchical partition keys, instead use the `PartitionKeyPaths` setting to supply an array of up to 3 paths. It also supports an optional `RecreateContainer` parameter (`false` by default) to delete and then recreate the container to ensure only newly imported data is present. 

The optional `BatchSize` parameter (100 by default) sets the number of items to accumulate before inserting. 

`ConnectionMode` can be set to either `Gateway` (default) or `Direct` to control how the client connects to the CosmosDB service. 

For situations where a container is created as part of the transfer operation `CreatedContainerMaxThroughput` (in RUs) and `UseAutoscaleForCreatedContainer` provide the initial throughput settings which will be in effect when executing the transfer. To instead use shared throughput that has been provisioned at the database level, set the `UseSharedThroughput` parameter to `true`. 

The optional `WriteMode` parameter specifies the type of data write to use: `InsertStream`, `Insert`, `UpsertStream`, `Upsert`, `DeleteStream` or `Delete`. The latter is useful to undo inserts that might have been written to the wrong container by mistake, if you cannot delete the entire container because it might contain other unrelated data.  

The `IsServerlessAccount` parameter specifies whether the target account uses Serverless instead of Provisioned throughput, which affects the way containers are created. Additional parameters allow changing the behavior of the Cosmos client appropriate to your environment.

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
    "IsServerlessAccount": false,
    "UseSharedThroughput": false
}
```
