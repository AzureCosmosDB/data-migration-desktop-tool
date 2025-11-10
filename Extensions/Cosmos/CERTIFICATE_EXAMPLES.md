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
        "CertificatePath": "C:\\certificates\\cosmos-custom.cer",
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
        "CertificatePath": "C:\\certificates\\my-cosmos-cert.pem",
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
        "CertificatePath": "C:\\certificates\\source-cosmos.cer"
    },
    "SinkSettings": {
        "ConnectionString": "AccountEndpoint=https://dest-cosmos.domain.com:8081/;AccountKey=...",
        "Database": "DestDB",
        "Container": "DestContainer",
        "CertificatePath": "C:\\certificates\\dest-cosmos.cer", 
        "PartitionKeyPath": "/id",
        "WriteMode": "Upsert"
    }
}
```

## Example 5: Enterprise PFX Certificate with Password

For enterprise environments using PFX/P12 certificates with mutual TLS authentication:

```json
{
    "Source": "json",
    "Sink": "cosmos-nosql",
    "SourceSettings": {
        "FilePath": "C:\\data\\enterprise-data.json"
    },
    "SinkSettings": {
        "UseRbacAuth": true,
        "AccountEndpoint": "https://enterprise-cosmos.company.com:443/",
        "Database": "EnterpriseDB",
        "Container": "SecureContainer",
        "CertificatePath": "C:\\enterprise-certs\\cosmos-client.pfx",
        "CertificatePassword": "SecureP@ssw0rd!",
        "PartitionKeyPath": "/tenantId"
    }
}
```

## Example 6: Enterprise PFX Certificate without Password

For enterprise environments with unprotected PFX certificates:

```json
{
    "Source": "cosmos-nosql",
    "Sink": "json",
    "SourceSettings": {
        "ConnectionString": "AccountEndpoint=https://enterprise-cosmos.company.com:8081/;AccountKey=...",
        "Database": "EnterpriseDB",
        "Container": "AuditLogs",
        "CertificatePath": "C:\\enterprise-certs\\cosmos-client.p12"
    },
    "SinkSettings": {
        "FilePath": "C:\\exports\\audit-logs.json"
    }
}
```

## Example 7: Enterprise Multi-Environment Migration

Migrating from staging to production with different PFX certificates:

```json
{
    "Source": "cosmos-nosql",
    "Sink": "cosmos-nosql",
    "SourceSettings": {
        "UseRbacAuth": true,
        "AccountEndpoint": "https://staging-cosmos.company.com:443/",
        "Database": "StagingDB",
        "Container": "DataContainer",
        "CertificatePath": "C:\\certs\\staging-client.pfx",
        "CertificatePassword": "StagingPass123!"
    },
    "SinkSettings": {
        "UseRbacAuth": true,
        "AccountEndpoint": "https://prod-cosmos.company.com:443/",
        "Database": "ProductionDB",
        "Container": "DataContainer",
        "CertificatePath": "C:\\certs\\prod-client.pfx",
        "CertificatePassword": "ProductionSecurePass456!",
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
   - `.pfx` (PKCS#12, with or without password)
   - `.p12` (PKCS#12, with or without password)

4. **PFX/P12 Certificate Security**
   - Never hardcode `CertificatePassword` in configuration files
   - Use environment variables or secure configuration providers for passwords
   - Store PFX files with restricted access permissions (600 on Unix, appropriate ACLs on Windows)
   - Consider using Azure Key Vault or other secure key management systems
   - Ensure PFX files contain both certificate and private key

5. **Validate certificate authenticity**
   - Ensure certificate files come from trusted sources
   - Verify certificate thumbprints when possible
   - Use proper certificate authorities for production
   - For PFX certificates, verify the certificate chain and expiration dates

## Troubleshooting

If you encounter certificate validation issues:

1. **Verify certificate file exists and is readable**
2. **Check certificate format and encoding**
3. **Ensure certificate matches the server's certificate**
4. **For PFX certificates, verify the password is correct**
5. **For emulator issues, try `DisableSslValidation: true` (development only)**
6. **For production, contact your network administrator for proper certificates**
7. **Check that PFX files contain both certificate and private key**
8. **Verify certificate expiration dates and certificate chain validity**
