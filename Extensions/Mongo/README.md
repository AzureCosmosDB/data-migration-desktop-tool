# MongoDB Extension

The MongoDB data transfer extension provides source and sink capabilities for reading from and writing to a MongoDB database.

> **Note**: When specifying the MongoDB extension as the Source or Sink property in configuration, utilize the name **MongoDB**.
> 
## Settings

Source and sink settings require both `ConnectionString` and `DatabaseName` parameters. The source takes an optional `Collection` parameter (if this parameter is not set, it will read from all collections). The sink requires the `Collection` parameter and will insert all records received from a source into that collection, as well as an optional `BatchSize` parameter (default value is 100) to batch the writes into the collection.

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
    "Collection": ""
}
```

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

