# Azure Blob Storage Extension

The Azure Blob Storage extension provides reading and writing of formatted files to Azure Blob Storage containers.

> **Note**: This is a Binary Storage Extension that is only used in combination with File Format extensions. 

## Settings

Source and sink require settings used to locate and access the Azure Blob Storage account. This can be done in one of two ways:

- Using a `ConnectionString` that includes an AccountEndpoint and AccountKey
- Using RBAC (Role Based Access Control) by setting `UseRbacAuth` to true and specifying `AccountEndpoint` and optionally `EnableInteractiveCredentials` to prompt the user to log in to Azure if default credentials are not available.

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
