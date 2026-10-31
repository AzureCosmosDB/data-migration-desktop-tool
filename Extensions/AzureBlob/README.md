# Azure Blob Storage Extension

The Azure Blob Storage extension provides reading and writing of formatted files to Azure Blob Storage containers.

> **Note**: This is a Binary Storage Extension that is only used in combination with File Format extensions. 

## Settings

Source and sink require settings used to locate and access the Azure Blob Storage account. This can be done in one of two ways:

- Using a `ConnectionString` that includes an AccountEndpoint and AccountKey
- Using RBAC (Role Based Access Control) by setting `UseRbacAuth` to true and specifying `AccountEndpoint` and optionally `EnableInteractiveCredentials` to prompt the user to log in to Azure if default credentials are not available.

When `UseRbacAuth` is `true`, the extension authenticates with `DefaultAzureCredential`, which supports Azure CLI sign-in (`az login`) and managed identities. For a service principal, set `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, and `AZURE_CLIENT_SECRET`. To select a user-assigned managed identity attached to the host, set `AZURE_CLIENT_ID` without service principal secret/certificate variables. These variables are shared across source and sink. The identity needs the `Storage Blob Data Reader` role (source) or `Storage Blob Data Contributor` role (sink) on the storage account.

> For supported credential flows, Azure RBAC role requirements, and CI/CD guidance, see the central [Authentication guide](../../docs/authentication.md).

### Source

`ContainerName` and `BlobName` identify a specific source blob. If it does not
exist, or an access/read request fails (including while streaming its contents),
the migration fails and the CLI returns a nonzero exit code instead of reporting
`Data transfer complete`. Check the container/blob names and the credential's read
access. Source diagnostics identify the container and blob without connection
strings, URL credentials, or signed URL queries; service failures include the HTTP
status when available.

Malformed or non-HTTP locations containing possible URL user-info are withheld
from diagnostics. Exhausted transport retries (including nested aggregate read
failures) receive the same sanitized diagnostics; cancellation is not converted
into a read failure.

An existing blob containing valid empty JSON (`[]`) remains a successful empty
input for the JSON formatter. Source-read failure handling does not guarantee
destination-file atomicity or rollback of records already written.

An optional `ReadBufferSizeInKB` parameter can be used to control stream buffering.

```json
{
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
    "ContainerName": "",
    "BlobName": "",
}
```

Or with RBAC:

```json
{
    "AccountEndpoint": "https://<storage-account>.blob.core.windows.net",
    "ContainerName": "",
    "BlobName": "",
    "UseRbacAuth": true,
    "EnableInteractiveCredentials": true
}
```

Possible values for `"Sink"` name property are `"JSON-AzureBlob(beta)"` and `"Parquet-AzureBlob(beta)"`.

### Sink

An optional `MaxBlockSizeInKB` parameter can also be specified to control the transfer.

```json
"SinkProperties" : {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
    "ContainerName": "",
    "BlobName": "",
}
```
