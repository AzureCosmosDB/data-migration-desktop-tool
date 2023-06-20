# Azure Blob Storage Extension (beta)

The Azure Blob Storage extension provides writing of formatted files to Azure Blob Storage containers.

> **Note**: This is a Binary Storage Extension that is only used in combination with File Format extensions. 

## Settings

Sink settings require all parameters shown below. An optional `MaxBlockSizeInKB` parameter can also be specified to control the transfer.

### Sink

```json
"Sink": "JSON-AzureBlob(beta)",
"SinkSettings": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
    "ContainerName": "",
    "BlobName": "",
}
```
