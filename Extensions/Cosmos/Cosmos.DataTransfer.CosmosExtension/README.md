# Cosmos Extension

The Cosmos data transfer extension provides source and sink capabilities for reading from and writing to containers in Cosmos DB using the Core (SQL) API. Source and sink both support string, number, and boolean property values, arrays, and hierarchical nested object structures.

## Settings

Source and sink settings both require multiple parameters to specify and provide access to the data location within a Cosmos DB account: 
- `ConnectionString`
- `Database`
- `Container`

Source supports an optional `IncludeMetadataFields` parameter (`false` by default) to enable inclusion of built-in Cosmos fields prefixed with `"_"`, for example `"_etag"` and `"_ts"`. An optional PartitionKeyValue setting allows for filtering to a single partition. The optional Query setting allows further filtering using a Cosmos SQL statement.

### Source

```json
"CosmosSourceSettings": {
    "ConnectionString": "AccountEndpoint=https://...",
    "Database":"myDb",
    "Container":"myContainer",
    "IncludeMetadataFields": false,
    "PartitionKeyValue":"123",
    "Query":"SELECT * FROM c WHERE c.category='event'"
}
```

Sink requires an additional `PartitionKeyPath` parameter which is used when creating the container if it does not exist. It also supports an optional `RecreateContainer` parameter (`false` by default) to delete and then recreate the container to ensure only newly imported data is present. The optional `BatchSize` parameter (100 by default) sets the number of items to accumulate before inserting.

### Sink

```json
"CosmosSinkSettings": {
    "ConnectionString": "AccountEndpoint=https://...",
    "Database":"myDb",
    "Container":"myContainer",
    "PartitionKeyPath":"/id",
    "RecreateContainer": false,
    "BatchSize": 100
}
```
