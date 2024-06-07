# Azure Blob Storage Extension

The Azure Blob Storage extension provides reading and writing of formatted files to Azure Blob Storage containers.

> **Note**: This is a Binary Storage Extension that is only used in combination with File Format extensions. 

## Settings

Source and Sink settings require the parameters shown below. 

### Source

An optional `ReadBufferSizeInKB` parameter can be used to control stream buffering.

```json
{
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
    "ContainerName": "",
    "BlobName": "",
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
