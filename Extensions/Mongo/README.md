# MongoDB Extension

The MongoDB data transfer extension provides source and sink capabilities for reading from and writing to a MongoDB database.

> **Note**: When specifying the MongoDB extension as the Source or Sink property in configuration, utilize the name **MongoDB**.
> 
> **Note for MongoDB Wire Version 2 Users**: If you're connecting to a MongoDB instance that uses wire version 2 (such as older CosmosDB MongoDB API instances that use the `documents.azure.com` endpoint), use the **MongoDB-Legacy (Wire v2)** extension instead. See the [MongoDB Legacy Extension](#mongodb-legacy-extension-wire-version-2) section below for details.
> 
## Settings

Source and sink settings require both `ConnectionString` and `DatabaseName` parameters. The source takes an optional `Collection` parameter (if this parameter is not set, it will read from all collections) and an optional `Query` parameter for filtering documents. The sink requires the `Collection` parameter and will insert all records received from a source into that collection, as well as optional `BatchSize` (default value is 1000) and `IdFieldName` parameters.

### Source

```json
{
    "ConnectionString": "",
    "DatabaseName": "",
    "Collection": "",
    "Query": ""
}
```

#### Query Parameter

The `Query` parameter allows you to filter documents during data migration using MongoDB query syntax in JSON format. This parameter supports two input methods:

1. **Direct JSON Query**: Provide the MongoDB query directly as a JSON string
2. **File Path**: Provide a path to a JSON file containing the MongoDB query

**Examples:**

Direct JSON query:
```json
{
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "sales",
    "Collection": "person",
    "Query": "{\"timestamp\":{\"$gte\":\"2025-01-01\",\"$lt\":\"2025-02-01\"}}"
}
```

Query from file:
```json
{
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "sales",
    "Collection": "person",
    "Query": "/path/to/query.json"
}
```

Where `query.json` contains:
```json
{"timestamp":{"$gte":"2025-01-01","$lt":"2025-02-01"}}
```

**Supported Query Operators:**

The query parameter supports all standard MongoDB query operators including:
- Comparison operators: `$eq`, `$ne`, `$gt`, `$gte`, `$lt`, `$lte`, `$in`, `$nin`
- Logical operators: `$and`, `$or`, `$not`, `$nor`
- Element operators: `$exists`, `$type`
- Array operators: `$all`, `$elemMatch`, `$size`
- And more...

For more information on MongoDB query syntax, see the [MongoDB Query Documentation](https://docs.mongodb.com/manual/tutorial/query-documents/).

### Sink

```json
{
    "ConnectionString": "",
    "DatabaseName": "",
    "Collection": "",
    "IdFieldName": ""
}
```

#### IdFieldName Parameter

The `IdFieldName` parameter allows you to specify which field from the source data should be mapped to MongoDB's `_id` field. When set, the value from the specified field will be used as the document's `_id` in MongoDB, while the original field will also remain in the document.

**Example:**

If your source data has an `id` field that you want to use as MongoDB's `_id`:

```json
{
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "mydb",
    "Collection": "mycollection",
    "IdFieldName": "id"
}
```

**Source data:**
```json
{
    "id": "BSKT_1",
    "CustomerId": 111
}
```

**Result in MongoDB:**
```json
{
    "_id": "BSKT_1",
    "id": "BSKT_1",
    "CustomerId": 111
}
```

**Notes:**
- The field name comparison is case-insensitive
- If `IdFieldName` is not specified or is empty, MongoDB will generate a unique ObjectId for `_id` as usual
- The original field specified in `IdFieldName` will remain in the document along with the `_id` field

# MongoDB Legacy Extension (Wire Version 2)

The MongoDB Legacy extension provides source and sink capabilities for reading from and writing to MongoDB instances that use wire version 2, which is not supported by the standard MongoDB extension. This extension is specifically designed for older MongoDB instances, including Azure Cosmos DB for MongoDB API instances using the `documents.azure.com` endpoint.

> **Note**: When specifying the MongoDB Legacy extension as the Source or Sink property in configuration, utilize the name **MongoDB-Legacy (Wire v2)**.

## Settings

Source and sink settings require both `ConnectionString` and `DatabaseName` parameters. The source takes an optional `Collection` parameter (if this parameter is not set, it will read from all collections). The sink requires the `Collection` parameter and will insert all records received from a source into that collection, as well as an optional `BatchSize` parameter (default value is 1000) to batch the writes into the collection.

### Source

```json
{
    "ConnectionString": "",
    "DatabaseName": "",
    "Collection": ""
}
```

### Sink

```json
{
    "ConnectionString": "",
    "DatabaseName": "",
    "Collection": "",
    "BatchSize": 1000,
    "IdFieldName": ""
}
```

#### IdFieldName Parameter

The `IdFieldName` parameter allows you to specify which field from the source data should be mapped to MongoDB's `_id` field. When set, the value from the specified field will be used as the document's `_id` in MongoDB, while the original field will also remain in the document. See the [MongoDB Extension Sink IdFieldName Parameter](#idfieldname-parameter) section above for more details and examples.

# MongoDB Vector Extension (Beta)

The MongoDB Vector extension is a Sink only extension that builds on the MongoDB extension by providing additional capabilities for generating embeddings using Azure OpenAI APIs.

> **Note**: When specifying the MongoDB Vector extension as the Sink property in configuration, utilize the name **MongoDB-Vector(beta)**.

## Settings

### Additional Source Settings

If using CSFLE (Client Side Field Level Encryption), source sink supports autodecryption providing the following parameters:

- `KeyVaultNamespace`: Database and collection holding the Key Vault and Keys details. Format: `database.collection`
- `KMSProviders`: Key Management Service providers for the Key Vault. For Azure Key Vault support, the following parameters are required:
  - `tenantId`: The Azure Active Directory tenant ID
  - `clientId`: The Azure Active Directory application client ID
  - `clientSecret`: The Azure Active Directory application client secret

```json
{
    "ConnectionString": "",
    "DatabaseName": "",
    "Collection": "",
    "KeyVaultNamespace": "",
    "KMSProviders": {
		"azure": {
			"tenantId": "",
			"clientId": "",
			"clientSecret": ""
		}
	}
}
```


### Additional Sink Settings

The settings are based on the MongoDB extension settings with additional parameters for generating embeddings.

The sink settings require the following additional parameters:

- `GenerateEmbedding`: If set to true, the sink will generate embeddings for the records before writing them to the database. The sink requires the `OpenAIUrl`, `OpenAIKey`, and `OpenAIDeploymentModel` parameters to be set. Following paramaters are required if this is true
- `OpenAIUrl`: The URL of the OpenAI API
- `OpenAIKey`: The API key for the OpenAI API
- `OpenAIDeploymentName`: The deployment model to use for the OpenAI API
- `SourcePropEmbedding`: The property in the source data that should be used to generate the embeddings
- `DestPropEmbedding`: New property name that will be added to the source data with the generated embeddings

```json
{
    "ConnectionString": "",
    "DatabaseName": "",
    "Collection": "",
    "BatchSize: 100,
    "GenerateEmbedding": true | false
    "OpenAIUrl": "",
    "OpenAIKey": "",
    "OpenAIDeploymentModel": "",
    "SourcePropEmbedding": "",
    "DestPropEmbedding": ""
}
```

