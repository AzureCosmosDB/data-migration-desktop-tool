# CognitiveSearch Extension

This extension is built to facilitate both a data Source and Sink for the migration tool to be able to read and write to Azure Cognitive Search Index. 

> **Note**: When specifying the CognitiveSearch extension as the Source or Sink property in configuration, utilize the name **CognitiveSearch**.
> 
## Configuration Settings

The CognitiveSearch has a couple required and optional settings for configuring the connection to the Cognitive Search Index you are connecting to.

The following are the required settings that must be defined for using either the data Source or Sink:

- `Endpoint` - This defines the endpoint of Cognitive Search service. This is required.
- `ApiKey` - The API key credential used to authenticate requests against the Search service. You need to use an admin key to perform Sink. This is required.
- `Index` - This defines the name of the Index to connect to on the Cognitive Search Index. This is required.

### Source

 > **important**: Only fields marked as retrievable will be output.

```json
{
  "Endpoint": "https://<cognitive-search-resouce-name>.search.windows.net",
  "ApiKey": "<admin-key or query-key>",
  "Index": "example-index",
  "ODataFilter": "Rooms/any(room: room/BaseRate lt 200.0) and Rating ge 4"
}
```
- `ODataFilter` - This is optional. This enables you to specify an OData filter to be applied to the data being retrieved. This is used in cases where only a subset of data  is needed in the migration. For more information, see [OData filter reference - Azure Cognitive Search | Microsoft Learn](https://learn.microsoft.com/en-us/azure/search/search-query-odata-filter).

### Sink

```json
{
  "Endpoint": "https://<cognitive-search-resouce-name>.search.windows.net",
  "ApiKey": "<admin-key>",
  "Index": "example-index",
  "BatchSize": "100",
  "IndexAction": "Upload"
}
```

- `BatchSize` - Sets the number of items to accumulate before inserting. This is optional.(`100` by default) 
- `IndexAction` - This defines the operation to perform on a document in an indexing batch. `Upload`,`MergeOrUpload`,`Merge`,`Delete` can be defined. See [IndexActionType Enum (Azure.Search.Documents.Models) - Azure for .NET Developers | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/azure.search.documents.models.indexactiontype?view=azure-dotnet#fields)  for details on specific behavior. This is optional.(`Uplaod` by default)

