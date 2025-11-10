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
    "InitClientEncryption": false,
    "WebProxy":"http://yourproxy.server.com/"
}
```

#### Certificate Configuration Examples

For emulator development with SSL validation disabled:

```json
{
    "ConnectionString": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDj...",
    "Database":"myDb",
    "Container":"myContainer",
    "DisableSslValidation": true
}
```

For custom certificate validation:

```json
{
    "ConnectionString": "AccountEndpoint=https://my-custom-cosmos.domain.com:8081/;AccountKey=...",
    "Database":"myDb",
    "Container":"myContainer",
    "CertificatePath": "C:\\certs\\cosmos-custom.cer"
}
```

For enterprise PFX certificate with password:

```json
{
    "ConnectionString": "AccountEndpoint=https://enterprise-cosmos.company.com:8081/;AccountKey=...",
    "Database":"EnterpriseDB",
    "Container":"SecureContainer",
    "CertificatePath": "C:\\enterprise-certs\\cosmos-client.pfx",
    "CertificatePassword": "SecureP@ssw0rd!"
}
```

For enterprise PFX certificate without password:

```json
{
    "UseRbacAuth": true,
    "AccountEndpoint": "https://enterprise-cosmos.company.com:443/",
    "Database":"EnterpriseDB",
    "Container":"SecureContainer",
    "CertificatePath": "C:\\enterprise-certs\\cosmos-client.p12"
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

- **`LimitToEndpoint`**: Optional, defaults to `false`. When the value of this property is false, the Cosmos DB SDK will automatically discover
  write and read regions, and use them when the configured application region is not available.
  When set to `true`, availability is limited to the endpoint specified.
  - **Note**: [CosmosClientOptions.LimitToEndpoint Property](https://learn.microsoft.com/dotnet/api/microsoft.azure.cosmos.cosmosclientoptions.limittoendpoint?view=azure-dotnet). When using the Cosmos DB Emulator Container for Linux it's been observed
    setting the value to `true` enables import and export of data.

#### **SSL/Certificate Settings**

- **`CertificatePath`**: Optional. Path to a certificate file for SSL validation and client authentication. Supports multiple formats:
  - `.cer`, `.crt`, `.pem` files for basic SSL validation
  - `.pfx`, `.p12` files for client authentication (enterprise scenarios)
  For PFX/P12 files, use `CertificatePassword` if the file is password-protected.
- **`CertificatePassword`**: Optional. Password for PFX/P12 certificate files when they are password-protected. Only used when `CertificatePath` points to a `.pfx` or `.p12` file. ⚠️ Store securely and avoid hardcoding in configuration files.
- **`DisableSslValidation`**: Optional, defaults to `false`. Disables SSL certificate validation entirely.
  - **⚠️ WARNING**: Only use this for development with the emulator. Never use in production environments as it makes connections vulnerable to man-in-the-middle attacks.

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
