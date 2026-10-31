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

### Emulator integration tests

Set both `Cosmos_Endpoint` and `Cosmos_Key` to run the two integration tests in
`CosmosDataSourceExtensionTests`. They create a temporary database and delete it
after the class finishes. With neither variable set, these tests are skipped so
ordinary local unit tests do not require an emulator. Partial configuration or
failed initialization fails the tests instead of silently skipping them.

The Linux emulator workflow tests the current `latest` image and the preview
`vnext-latest` image over HTTPS, with certificate validation enabled. The preview
uses the documented `/ready` probe on port 8080; the current emulator waits for
its certificate endpoint. The tests use Gateway mode and explicitly disable bulk
execution, which the vNext emulator does not support with the .NET SDK. See the
[Linux vNext emulator documentation](https://learn.microsoft.com/azure/cosmos-db/emulator-linux).
These tests cover JSON round trips, not bulk execution or authentication enforcement.

Both Linux and Windows emulator workflows retain TRX results and require
`Test_Json_RoundTrip_WithLongValues` and `Test_FromJson_WithLongValues` to each
execute and pass. Missing, skipped, duplicate, or failed results fail the job.
The Linux preview job remains advisory; the current emulator and the separate
failure-guard regression job are mandatory. Check each matrix job's outcome,
summary, and artifacts rather than relying on the overall workflow status.

### Main Settings

- `ConnectionString`: Cosmos DB connection string (AccountEndpoint + AccountKey).
- `UseRbacAuth` (default: `false`): Use role-based access control for authentication.
- `AccountEndpoint`: Cosmos DB account endpoint (required for RBAC).
- `EnableInteractiveCredentials` (default: `false`): Prompt for Azure login if default credentials are unavailable.
- `ManagedIdentityClientId`: Client ID of a user-assigned managed identity attached to the host (RBAC mode). Selects the identity for the managed-identity step of `DefaultAzureCredential`; it does not override earlier environment/workload credentials. Cannot be combined with explicit service principal settings. If omitted, the SDK uses `AZURE_CLIENT_ID` when set.
- `TenantId`: Microsoft Entra tenant ID for explicit service principal auth (RBAC mode).
- `ClientId`: Service principal app/client ID for explicit service principal auth (RBAC mode).
- `ClientSecret`: Service principal client secret for explicit service principal auth (RBAC mode).
- `ClientCertificatePath`: Path to a PFX/PKCS#12 service principal certificate file that contains a private key for explicit service principal auth (RBAC mode).
- `ClientCertificatePassword`: Optional password for the service principal certificate (RBAC mode).
- `Database`: Cosmos DB database name.
- `Container`: Cosmos DB container name.
- `WebProxy`: Proxy server URL for Cosmos DB connections.
- `InitClientEncryption` (default: `false`): Enable Always Encrypted feature.
- `LimitToEndpoint` (default: `false`): Restrict client to endpoint. See [CosmosClientOptions.LimitToEndpoint Property](https://learn.microsoft.com/dotnet/api/microsoft.azure.cosmos.cosmosclientoptions.limittoendpoint).
- `DisableSslValidation` (default: `false`): Disable SSL certificate validation (for local dev only; not for production).
- `AllowBulkExecution` (default: `false`): Enable bulk execution for optimized performance. Warning: may affect consistency and error handling.
- `EnableContentResponseOnWrite` (default: `true`): Return the response body for write operations. Set to `false` to return only headers and status code, reducing network IO. Applies to sink operations. See [CosmosClientOptions.EnableContentResponseOnWrite Property](https://learn.microsoft.com/dotnet/api/microsoft.azure.cosmos.cosmosclientoptions.enablecontentresponseonwrite).
- `EnableNetHttpLogging` (default: `false`): Enable .NET HTTP diagnostic events for troubleshooting connectivity issues (Debug log level).

Source and sink require settings used to locate and access the Cosmos DB account. This can be done in one of three ways:

- Using a `ConnectionString` that includes an AccountEndpoint and AccountKey
- Using RBAC (Role Based Access Control) by setting `UseRbacAuth` to true and specifying `AccountEndpoint` and optionally `EnableInteractiveCredentials` to prompt the user to log in to Azure if default credentials are not available. See [migrate-passwordless](https://learn.microsoft.com/azure/cosmos-db/nosql/migrate-passwordless?tabs=sign-in-azure-cli%2Cdotnet%2Cazure-portal-create%2Cazure-portal-associate%2Capp-service-identity) for how to configure Cosmos DB for passwordless access.
- Using RBAC with a specific user-assigned managed identity by setting `UseRbacAuth` to true and specifying `AccountEndpoint` and `ManagedIdentityClientId`.
- Using RBAC with explicit service principal credentials by setting `UseRbacAuth` to true and specifying `AccountEndpoint`, `TenantId`, `ClientId`, and either `ClientSecret` or `ClientCertificatePath`.

> For a complete walkthrough of Microsoft Entra ID authentication, supported credential flows, and Azure RBAC role requirements across all Azure extensions, see the [Authentication guide](../../docs/authentication.md).
>
> **RBAC prerequisite**: The database and container must already exist, including the destination container with the intended partition key. DMT does not create them in RBAC mode, and `RecreateContainer: true` is rejected. The source needs the Cosmos DB Built-in Data Reader role and the sink needs the Cosmos DB Built-in Data Contributor role.
>
> **Security warning**: `ClientSecret` and `ClientCertificatePassword` are plaintext in settings files. Do not commit secrets to source control. Prefer runtime injection through .NET configuration providers such as environment variables, command-line args (for example `--SourceSettings:ClientSecret=...`, `--SinkSettings:ClientSecret=...`, `--SourceSettings:ClientCertificatePassword=...`, or `--SinkSettings:ClientCertificatePassword=...`), or User Secrets.
>
> **Implementation note**: The RBAC service principal path now uses explicit credential-selection logic with additional validation and error wrapping. This was added to make auth-path behavior deterministic and testable, avoid unmanaged certificate object lifetime handling in the tool process, and surface actionable messages when certificate/credential configuration is invalid.

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

### Content Response On Write

The `EnableContentResponseOnWrite` setting controls whether the full response body is returned for write operations (Create, Upsert, Patch, Replace). When set to `false`, only headers and status code are returned, which can drastically reduce network IO and improve performance. This is especially beneficial for high-throughput scenarios where the response body is not needed. Default is `true`.

**Note**: This setting applies to sink (write) operations.

For more information, see [CosmosClientOptions.EnableContentResponseOnWrite Property](https://learn.microsoft.com/dotnet/api/microsoft.azure.cosmos.cosmosclientoptions.enablecontentresponseonwrite).

Example:

```json
{
    "ConnectionString": "AccountEndpoint=https://...",
    "Database": "myDb",
    "Container": "myContainer",
    "EnableContentResponseOnWrite": false
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
- `UseDefaultProxyCredentials` (`false` by default) - When `true`, includes default credentials in the WebProxy request. Use this when connecting through an authenticated proxy that returns `407 Proxy Authentication Required`. See [WebProxy.Credentials Property](https://learn.microsoft.com/dotnet/api/system.net.webproxy.credentials).
- `UseDefaultCredentials` (`false` by default) - When `true`, configures the underlying HttpClient with default network credentials. Use this when the connection to CosmosDB requires authentication through a proxy.
- `PreAuthenticate` (`false` by default) - When `true`, enables pre-authentication on the HttpClient, which sends credentials with the initial request rather than waiting for a 401/407 challenge. This can save extra round-trips but should only be used when the endpoint is trusted.
- `EnableNetHttpLogging` (`false` by default) - When `true`, enables `.NET` `System.Net.Http` diagnostic events to help investigate connection issues. These events are emitted at Debug level and can include endpoint/request details.

### Connectivity troubleshooting (.NET HTTP diagnostics)

When connection failures are not verbose enough, enable `EnableNetHttpLogging` and run the tool with `Debug` logging.

```json
{
  "SourceSettings": {
    "ConnectionMode": "Gateway",
    "LimitToEndpoint": true,
    "DisableSslValidation": true,
    "EnableNetHttpLogging": true
  }
}
```

Set logging to Debug before running:

- Linux/macOS: `export Logging__LogLevel__Default=Debug`
- Windows (cmd): `set Logging__LogLevel__Default=Debug`
- Windows (PowerShell): `$env:Logging__LogLevel__Default = "Debug"`

This emits `System.Net.Http` and `System.Net.Security` events at Debug level, which helps distinguish TLS/certificate failures from transport/proxy/network failures.
Configuration follows normal .NET precedence, so environment variables override `appsettings.json` values.
Command-line arguments have highest precedence and override both environment variables and `appsettings.json`.

### Always Encrypted

Source and Sink support Always Encrypted as an optional parameter. When `InitClientEncryption` is set to `true`, the extension will initialize the Cosmos client with the Always Encrypted feature enabled. This allows for the use of encrypted fields in the Cosmos DB container. The extension will automatically decrypt the fields when reading from the source and encrypt the fields when writing to the sink.
</br>
The extension will also automatically handle the encryption keys and encryption policy for the client, but it requires `UseRbacAuth` to be set to `true` and the user/service principal to have the necessary permissions to access the key vault.
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

Or with RBAC and a specific user-assigned managed identity:

```json
{
  "UseRbacAuth": true,
  "AccountEndpoint": "https://source.documents.azure.com:443/",
  "ManagedIdentityClientId": "<user-assigned-identity-client-id>",
  "Database":"myDb",
  "Container":"myContainer",
  "IncludeMetadataFields": false,
  "PartitionKeyValue":"123",
  "Query":"SELECT * FROM c WHERE c.category='event'",
  "InitClientEncryption": false
}
```

Or with RBAC and explicit service principal credentials:

```json
{
  "UseRbacAuth": true,
  "AccountEndpoint": "https://source.documents.azure.com:443/",
  "TenantId": "<tenant-id>",
  "ClientId": "<client-id>",
  "ClientSecret": "<client-secret>",
  "Database":"myDb",
  "Container":"myContainer",
  "IncludeMetadataFields": false,
  "PartitionKeyValue":"123",
  "Query":"SELECT * FROM c WHERE c.category='event'",
  "InitClientEncryption": false
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
  - `DeleteStream`
  - `Delete`

#### **Rate-Limit Retries (HTTP 429)**

The Cosmos DB SDK already honors service-directed retry delays internally. Its
documented defaults are [9 retries](https://learn.microsoft.com/dotnet/api/microsoft.azure.cosmos.cosmosclientoptions.maxretryattemptsonratelimitedrequests)
and [30 seconds of cumulative retry wait](https://learn.microsoft.com/dotnet/api/microsoft.azure.cosmos.cosmosclientoptions.maxretrywaittimeonratelimitedrequests)
per SDK operation. DMT leaves these SDK settings unchanged.

DMT's additional sink retry layer applies after an SDK operation still returns
or throws 429, for **all six write modes** above:

- **`MaxRetryCount`**: Defaults to `5`. Number of additional application attempts
  per item, beyond the initial SDK call. Must be nonnegative; `0` disables only
  these additional retries, not the SDK's internal retries.
- **`InitialRetryDurationMs`**: Defaults to `200`. Positive initial fallback
  delay in milliseconds, used only when there is no usable service delay.

For each 429, DMT reads the latest `CosmosException.RetryAfter`, backed by the
`x-ms-retry-after-ms` response header. Stream 429 responses are converted to SDK
exceptions to use the same path. A positive, supported service delay takes
priority over `InitialRetryDurationMs`, with no shortening or jitter. For
example, a 61-second service hint waits 61 seconds, even though fallback waits
are capped at 30 seconds. A different hint on the next attempt replaces the
previous one; retry state is not shared across items.

Missing, malformed, zero, or negative hints that the SDK represents without
overflow use bounded exponential backoff. Zero is not treated as permission for
an immediate retry (the SDK also substitutes a nonzero wait for zero).
The fallback starts at `InitialRetryDurationMs`, doubles for each additional
attempt, and adds nonnegative, thread-safe jitter of less than half the initial
delay (the jitter range itself is bounded using 30,000 ms). **The total fallback
delay is capped at 30 seconds per wait**, avoiding overflow and excessively long
exponential schedules with large retry counts. With defaults, the first waits
are 200-299 ms, 400-499 ms, and 800-899 ms. This cap never applies to a service
hint. If a service delay overflows the SDK's `TimeSpan` conversion or exceeds
.NET 8's supported single wait of 4,294,967,294 ms, the write fails explicitly
instead of retrying early.

Cancellation interrupts application waits and is passed to SDK calls. Each
stream attempt gets a fresh complete payload; owned request streams and
responses are disposed even on failure. Successful items are counted once.
Partition keys retain their JSON scalar types (string, number, boolean, null,
or undefined) rather than converting numbers and booleans to strings.
Delete-not-found is treated as success for both delete modes only for a 404 with
substatus zero (or an absent stream substatus). Session-related 404s such as
404/1002 remain failures. Non-429 failures are not retried by this application
layer.

When a batch encounters a permanent failure or exhausts retries, DMT cancels
the other calls and retry waits in that batch, drains their tasks, and propagates
the original failure rather than a sibling cancellation. It does not shorten a
service delay and retry early. Confirmed successes in the failed batch are
counted once, and both batch progress and cumulative totals for started batches
are reported before failure or cancellation propagates. Unconfirmed outcomes
are not claimed to be failures or successes: cancellation cannot undo writes
already accepted by the service.

After retry exhaustion, the sink logs the write mode, status, and number of
additional application retries, then propagates the final 429. These messages
do not include document payloads, IDs, credentials, or raw exception bodies.
At the sink transfer boundary, Cosmos failures are wrapped in a
`DataTransferException` with a safe status/substatus message and the original
exception retained as `InnerException` for programmatic inspection. Core logs
only that safe message, not the exception object or its inner diagnostics.
Ordinary file, HTTP, and JSON source failures keep their original diagnostics.
Each additional attempt can itself incur SDK retries, so `MaxRetryCount` is
**not an elapsed-time budget**. Large values can still make migrations take a
long time. Retries do not guarantee completion against insufficient throughput;
consider reducing `BatchSize` or increasing available RU/s. Adaptive concurrency,
unlimited retries, source retries, and resumable migrations are not provided by
this setting. See [troubleshooting 429 responses](https://learn.microsoft.com/azure/cosmos-db/troubleshoot-request-rate-too-large).

#### **Connection Settings**

- **`ConnectionMode`**: Controls how the client connects to the Cosmos DB service. Options:
  - `Gateway` (default)
  - `Direct`

- **`WebProxy`**: Optional. Specifies the proxy server URL to use for connections (e.g., `http://yourproxy.server.com/`).
- **`UseDefaultProxyCredentials`**: Optional, defaults to `false`. When `true`, includes default credentials in the WebProxy request. Use this when connecting through an authenticated proxy that returns `407 Proxy Authentication Required`. See [WebProxy.Credentials Property](https://learn.microsoft.com/dotnet/api/system.net.webproxy.credentials).
- **`UseDefaultCredentials`**: Optional, defaults to `false`. When `true`, configures the underlying HttpClient with default network credentials. Use this when the connection to CosmosDB requires authentication through a proxy.
- **`PreAuthenticate`**: Optional, defaults to `false`. When `true`, enables pre-authentication on the HttpClient, which sends credentials with the initial request rather than waiting for a 401/407 challenge. This can save extra round-trips but should only be used when the endpoint is trusted.
- **`EnableNetHttpLogging`**: Optional, defaults to `false`. When `true`, enables `.NET` `System.Net.Http` diagnostic events for troubleshooting. Events are logged at Debug level and may include endpoint/request details.

- **`LimitToEndpoint`**: Optional, defaults to `false`. When the value of this property is false, the Cosmos DB SDK will automatically discover
  write and read regions, and use them when the configured application region is not available.
  When set to `true`, availability is limited to the endpoint specified.
  - **Note**: [CosmosClientOptions.LimitToEndpoint Property](https://learn.microsoft.com/dotnet/api/microsoft.azure.cosmos.cosmosclientoptions.limittoendpoint). When using the Cosmos DB Emulator Container for Linux it has been observed
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

Or with RBAC and explicit service principal credentials:

```json
{
  "UseRbacAuth": true,
  "AccountEndpoint": "https://target.documents.azure.com:443/",
  "TenantId": "<tenant-id>",
  "ClientId": "<client-id>",
  "ClientCertificatePath": "./certs/sp-auth.pfx",
  "ClientCertificatePassword": "<optional-password>",
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
