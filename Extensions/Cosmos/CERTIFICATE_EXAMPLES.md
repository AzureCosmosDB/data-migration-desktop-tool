# Custom Certificate Configuration Examples

This file demonstrates how to use the new custom certificate configuration options in the Cosmos DB data migration tool.

## Example 1: Disable SSL Validation (Development/Emulator Only)

For development with the Cosmos DB emulator when certificates are problematic:

```json
{
    "Source": "json",
    "Sink": "cosmos-nosql",
    "SourceSettings": {
        "FilePath": "C:\\data\\sample-data.json"
    },
    "SinkSettings": {
        "ConnectionString": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
        "Database": "SampleDB",
        "Container": "SampleContainer",
        "DisableSslValidation": true
    }
}
```

## Example 2: Custom Certificate Path

For custom Cosmos DB instances with self-signed or corporate certificates:

```json
{
    "Source": "json", 
    "Sink": "cosmos-nosql",
    "SourceSettings": {
        "FilePath": "C:\\data\\sample-data.json"
    },
    "SinkSettings": {
        "ConnectionString": "AccountEndpoint=https://my-custom-cosmos.company.com:8081/;AccountKey=...",
        "Database": "ProductionDB",
        "Container": "DataContainer", 
        "CustomCertificatePath": "C:\\certificates\\cosmos-custom.cer",
        "PartitionKeyPath": "/id"
    }
}
```

## Example 3: RBAC with Custom Certificate

Using RBAC authentication with a custom certificate:

```json
{
    "Source": "json",
    "Sink": "cosmos-nosql", 
    "SourceSettings": {
        "FilePath": "C:\\data\\sample-data.json"
    },
    "SinkSettings": {
        "UseRbacAuth": true,
        "AccountEndpoint": "https://my-cosmos-account.documents.azure.com:443/",
        "EnableInteractiveCredentials": true,
        "Database": "MyDatabase",
        "Container": "MyContainer",
        "CustomCertificatePath": "C:\\certificates\\my-cosmos-cert.pem",
        "PartitionKeyPath": "/partitionKey"
    }
}
```

## Example 4: Cosmos-to-Cosmos Migration with Custom Certificate

Migrating from one Cosmos DB instance to another with custom certificate validation:

```json
{
    "Source": "cosmos-nosql",
    "Sink": "cosmos-nosql",
    "SourceSettings": {
        "ConnectionString": "AccountEndpoint=https://source-cosmos.domain.com:8081/;AccountKey=...",
        "Database": "SourceDB",
        "Container": "SourceContainer", 
        "CustomCertificatePath": "C:\\certificates\\source-cosmos.cer"
    },
    "SinkSettings": {
        "ConnectionString": "AccountEndpoint=https://dest-cosmos.domain.com:8081/;AccountKey=...",
        "Database": "DestDB",
        "Container": "DestContainer",
        "CustomCertificatePath": "C:\\certificates\\dest-cosmos.cer", 
        "PartitionKeyPath": "/id",
        "WriteMode": "Upsert"
    }
}
```

## Security Notes

⚠️ **Important Security Considerations:**

1. **Never use `DisableSslValidation: true` in production environments**
   - This disables all SSL certificate validation
   - Only use for development with local emulators

2. **Store certificate files securely**
   - Use proper file permissions
   - Store in protected directories
   - Consider using Azure Key Vault for production scenarios

3. **Certificate file formats supported**
   - `.cer` (binary or base64 encoded)
   - `.crt` (binary or base64 encoded)  
   - `.pem` (base64 encoded)

4. **Validate certificate authenticity**
   - Ensure certificate files come from trusted sources
   - Verify certificate thumbprints when possible
   - Use proper certificate authorities for production

## Troubleshooting

If you encounter certificate validation issues:

1. **Verify certificate file exists and is readable**
2. **Check certificate format and encoding**
3. **Ensure certificate matches the server's certificate**
4. **For emulator issues, try `DisableSslValidation: true` (development only)**
5. **For production, contact your network administrator for proper certificates**
