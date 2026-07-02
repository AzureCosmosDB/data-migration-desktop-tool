# Azure Blob Storage Extension

The Azure Blob Storage extension provides reading and writing of formatted files to Azure Blob Storage containers.

> **Note**: This is a Binary Storage Extension that is only used in combination with File Format extensions. 

## Settings

Source and sink require settings used to locate and access the Azure Blob Storage account. This can be done in one of two ways:

- Using a `ConnectionString` that includes an AccountEndpoint and AccountKey
- Using RBAC (Role Based Access Control) by setting `UseRbacAuth` to true and specifying `AccountEndpoint` and optionally `EnableInteractiveCredentials` to prompt the user to log in to Azure if default credentials are not available.

When `UseRbacAuth` is `true`, the extension authenticates with `DefaultAzureCredential`, which supports Azure CLI sign-in (`az login`), managed identities, and service principal / user-assigned managed identity selection via the standard `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, and `AZURE_CLIENT_SECRET` environment variables. The identity needs the `Storage Blob Data Reader` role (source) or `Storage Blob Data Contributor` role (sink) on the storage account.

> For supported credential flows, Azure RBAC role requirements, and CI/CD guidance, see the central [Authentication guide](../../docs/authentication.md).

### Source

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
