# Cosmos Extension

The Cosmos data transfer extension provides source and sink capabilities for reading from and writing to containers in Cosmos DB using the Core (SQL) API. Source and sink both support string, number, and boolean property values, arrays, and hierarchical nested object structures.

> **Note**: When specifying the JSON extension as the Source or Sink property in configuration, utilize the name **Cosmos-nosql**.

## JSON Metadata Property Preservation

The Cosmos extension preserves all JSON properties during data migration, including properties that start with special characters like `$type`, `$id`, and `$ref`. These properties are commonly used by serialization libraries (such as Newtonsoft.Json) to store type information for polymorphic objects or reference tracking.

**Example**: If your source data contains documents with `$type` properties used for type discrimination:

```json
{
  "id": "1",
  "name": "Dog",
  "myFavouritePet": {
    "$type": "MyProject.Pets.Dog, MyProject",
    "Name": "Foo",
    "OtherName": "OtherFoo"
  }
}
```

These properties will be preserved exactly as they appear in the source when migrating to the destination. This ensures that applications using type information embedded in JSON properties will continue to work correctly after migration.

> **Note**: Prior to version 3.1.0, properties starting with `$` were filtered out during migration. If you need the old behavior, please use an earlier version of the tool.

## Settings


### Main Settings

| Setting                | Description                                                                                       | Default   |
|------------------------|---------------------------------------------------------------------------------------------------|-----------|
| ConnectionString       | Cosmos DB connection string (AccountEndpoint + AccountKey)                                         |           |
| UseRbacAuth            | Use Role Based Access Control for authentication                                                   | false     |
| AccountEndpoint        | Cosmos DB account endpoint (required for RBAC)                                                     |           |
| EnableInteractiveCredentials | Prompt for Azure login if default credentials are unavailable                                 | false     |
| Database               | Cosmos DB database name                                                                           |           |
| Container              | Cosmos DB container name                                                                          |           |
| WebProxy               | Proxy server URL for Cosmos DB connections                                                        |           |
| InitClientEncryption   | Enable Always Encrypted feature                                                                   | false     |
| LimitToEndpoint        | Restrict client to endpoint (see CosmosClientOptions.LimitToEndpoint)                             | false     |
| DisableSslValidation   | Disable SSL certificate validation (for local dev only; not for production)                       | false     |
| AllowBulkExecution     | Enable bulk execution for optimized performance. <br>**Warning:** May affect consistency and error handling. | false     |

Source and sink require settings used to locate and access the Cosmos DB account. This can be done in one of two ways:

- Using a `ConnectionString` that includes an AccountEndpoint and AccountKey
- Using RBAC (Role Based Access Control) by setting `UseRbacAuth` to true and specifying `AccountEndpoint` and optionally `EnableInteractiveCredentials` to prompt the user to log in to Azure if default credentials are not available. See ([migrate-passwordless](https://learn.microsoft.com/azure/cosmos-db/nosql/migrate-passwordless?tabs=sign-in-azure-cli%2Cdotnet%2Cazure-portal-create%2Cazure-portal-associate%2Capp-service-identity) for how to configure Cosmos DB for passwordless access.


### Bulk Execution

The extension supports bulk execution for Cosmos DB operations. When the `AllowBulkExecution` setting is set to `true`, operations such as bulk inserts and updates are optimized for performance. Use with caution, as bulk execution may affect consistency and error handling. Default is `false`.

Example:

```json
{
    "ConnectionString": "AccountEndpoint=https://...",
    "Database": "myDb",
    "Container": "myContainer",
    "AllowBulkExecution": true
}
```

Source and sink settings also both require parameters to specify the data location within a Cosmos DB account:

- `Database`
- `Container`

Source supports the following optional parameters:
- `IncludeMetadataFields` (`false` by default) - Enables inclusion of built-in Cosmos fields prefixed with `"_"`, for example `"_etag"` and `"_ts"`.
- `PartitionKeyValue` - Allows for filtering to a single partition.
- `Query` - Allows further filtering using a Cosmos SQL statement.
- `WebProxy` (`null` by default) - Enables connections through a proxy.
- `UseDefaultProxyCredentials` (`false` by default) - When `true`, includes default credentials in the WebProxy request. Use this when connecting through an authenticated proxy that returns [`407 Proxy Authentication Required`](https://learn.microsoft.com/dotnet/api/system.net.webproxy.credentials?view=net-10.0#remarks).
- `UseDefaultCredentials` (`false` by default) - When `true`, configures the underlying HttpClient with default network credentials. Use this when the connection to CosmosDB requires authentication through a proxy.
- `PreAuthenticate` (`false` by default) - When `true`, enables pre-authentication on the HttpClient, which sends credentials with the initial request rather than waiting for a 401/407 challenge. This can save extra round-trips but should only be used when the endpoint is trusted.

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
    "WebProxy":"http://yourproxy.server.com/",
    "UseDefaultProxyCredentials": true,
    "UseDefaultCredentials": true,
    "PreAuthenticate": true
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
    "InitClientEncryption": false,
    "WebProxy":"http://yourproxy.server.com/",
    "UseDefaultProxyCredentials": true,
    "UseDefaultCredentials": true,
    "PreAuthenticate": true
}
```

#### Disable SSL Validation Configuration Example

For development purposes with SSL validation disabled:

```json
{
    "ConnectionString": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDj...",
    "Database":"myDb",
    "Container":"myContainer",
    "DisableSslValidation": true
}
```

### Sink Settings

#### **Partition Key Settings**

- **`PartitionKeyPath`**: Specifies the partition key path when creating the container (e.g., `/id`) if it does not exist.
- **`PartitionKeyPaths`**: Use this to supply an array of up to 3 paths for hierarchical partition keys.

#### **Database Management**

- **`UseAutoscaleForDatabase`**: Specifies if the database will be created with autoscale enabled or manual. Defaults to `false`. manual.

#### **Container Management**

- **`RecreateContainer`**: Optional, defaults to `false`. Deletes and recreates the container to ensure only newly imported data is present.
- **`CreatedContainerMaxThroughput`**: Specifies the initial throughput (in RUs) for a newly created container.
- **`UseAutoscaleForCreatedContainer`**: Enables autoscale for the newly created container.
- **`UseSharedThroughput`**: Set to `true` to use shared throughput provisioned at the database level.

#### **Batching and Write Behavior**

- **`BatchSize`**: Optional, defaults to `100`. Sets the number of items to accumulate before inserting.
- **`WriteMode`**: Specifies the type of data write to use. Options:
  - `InsertStream`
  - `Insert`
  - `UpsertStream`
  - `Upsert`

#### **Connection Settings**

- **`ConnectionMode`**: Controls how the client connects to the Cosmos DB service. Options:
  - `Gateway` (default)
  - `Direct`

- **`WebProxy`**: Optional. Specifies the proxy server URL to use for connections (e.g., `http://yourproxy.server.com/`).
- **`UseDefaultProxyCredentials`**: Optional, defaults to `false`. When `true`, includes default credentials in the WebProxy request. Use this when connecting through an authenticated proxy that returns [`407 Proxy Authentication Required`](https://learn.microsoft.com/dotnet/api/system.net.webproxy.credentials?view=net-10.0#remarks).
- **`UseDefaultCredentials`**: Optional, defaults to `false`. When `true`, configures the underlying HttpClient with default network credentials. Use this when the connection to CosmosDB requires authentication through a proxy.
- **`PreAuthenticate`**: Optional, defaults to `false`. When `true`, enables pre-authentication on the HttpClient, which sends credentials with the initial request rather than waiting for a 401/407 challenge. This can save extra round-trips but should only be used when the endpoint is trusted.

- **`LimitToEndpoint`**: Optional, defaults to `false`. When the value of this property is false, the Cosmos DB SDK will automatically discover
  write and read regions, and use them when the configured application region is not available.
  When set to `true`, availability is limited to the endpoint specified.
  - **Note**: [CosmosClientOptions.LimitToEndpoint Property](https://learn.microsoft.com/dotnet/api/microsoft.azure.cosmos.cosmosclientoptions.limittoendpoint?view=azure-dotnet). When using the Cosmos DB Emulator Container for Linux it's been observed
    setting the value to `true` enables import and export of data.

#### **SSL/Certificate Settings**

- **`DisableSslValidation`**: Optional, defaults to `false`. Disables SSL certificate validation for development/emulator scenarios.
  - **⚠️ WARNING**: Only use this for development purposes. Never use in production environments as it disables critical security checks and makes connections vulnerable to man-in-the-middle attacks.

#### **Serverless Account**

- **`IsServerlessAccount`**: Specifies whether the target account uses Serverless instead of Provisioned throughput, which affects the way containers are created.
  - **Note**: Serverless accounts cannot have shared throughput. See [Azure Cosmos DB serverless account type](https://learn.microsoft.com/azure/cosmos-db/serverless#use-serverless-resources).

#### **Client Behavior**

- **`PreserveMixedCaseIds`**: Optional, defaults to `false`. Writes `id` fields with their original casing while generating a separate lowercased `id` field as required by Cosmos.
- **`IgnoreNullValues`**: Optional. Excludes fields with null values when writing to Cosmos DB.
- **`InitClientEncryption`**: Optional, defaults to `false`. Uses client-side encryption with the container. Can only be used with `UseRbacAuth` set to `true`

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
    "UseAutoscaleForDatabase": false,
    "UseAutoscaleForCreatedContainer": true,
    "WriteMode": "InsertStream",
    "PreserveMixedCaseIds": false,
    "IgnoreNullValues": false,
    "IsServerlessAccount": false,
    "UseSharedThroughput": false,
    "InitClientEncryption": false,
    "LimitToEndpoint": false
}
```
