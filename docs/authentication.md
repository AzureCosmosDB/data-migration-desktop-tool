# Authentication with Microsoft Entra ID and Azure RBAC

The Azure Cosmos DB Desktop Data Migration Tool (DMT) supports both key-based authentication and
passwordless authentication using **Microsoft Entra ID** (formerly Azure AD) together with
**Azure role-based access control (RBAC)**. This guide is the central reference for configuring
authentication across the Azure extensions. Each extension's own README links back here and adds
extension-specific details.

## How authentication is configured in DMT

DMT is **configuration-file driven**. Authentication is specified through the `Source` and `Sink`
settings inside `migrationsettings.json` (the file you pass with `--settings`). DMT does **not**
use dedicated authentication CLI flags such as `--auth-mode entra-id`. Instead, you set
authentication properties in the settings file, and any property can be overridden at runtime via
standard .NET configuration providers — environment variables, command-line arguments, or
[User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets).

For example, to keep a secret out of the settings file you can inject it at runtime:

```bash
dmt run --settings ./migrationsettings.json --SourceSettings:ClientSecret="$MY_CLIENT_SECRET"
```

> **Security:** Never commit secrets (`ConnectionString`, `AccountKey`, `ApiKey`, `ClientSecret`,
> `ClientCertificatePassword`) to source control. Prefer passwordless authentication, and when a
> secret is unavoidable, inject it at runtime through environment variables, command-line
> arguments, or User Secrets.

## Supported credential flows

When an Azure extension is set to RBAC mode (`UseRbacAuth: true`), DMT authenticates using the
[Azure Identity client library](https://learn.microsoft.com/dotnet/api/overview/azure/identity-readme).
The credential is selected based on the settings you provide:

| Credential flow | How to enable | Typical use |
| --- | --- | --- |
| **DefaultAzureCredential** | `UseRbacAuth: true` with an account endpoint and no explicit service principal settings | Local development (`az login`), Managed Identity in Azure, and CI/CD environments. Tries a chain of sources (environment variables, Managed Identity, Azure CLI, etc.). |
| **Azure CLI** | Part of the `DefaultAzureCredential` chain; run `az login` first | Interactive local runs on a developer machine. |
| **Interactive browser** | `UseRbacAuth: true` + `EnableInteractiveCredentials: true` | Local runs where you want to be prompted to sign in if no other credential is available. |
| **System-assigned Managed Identity** | `UseRbacAuth: true` running on an Azure resource with a system-assigned identity | Pipelines and hosts running in Azure (VM, Container Apps, AKS, etc.). |
| **User-assigned Managed Identity** | `UseRbacAuth: true` + `ManagedIdentityClientId: <client-id>` *(Cosmos extension)* | Hosts that have more than one user-assigned identity and need to select a specific one. |
| **Service principal (client secret)** | `UseRbacAuth: true` + `TenantId` + `ClientId` + `ClientSecret` | Unattended automation where a registered app authenticates with a secret. |
| **Service principal (certificate)** | `UseRbacAuth: true` + `TenantId` + `ClientId` + `ClientCertificatePath` (+ optional `ClientCertificatePassword`) | Unattended automation using certificate credentials instead of a secret. |

> For user-assigned Managed Identity, you can also set the `AZURE_CLIENT_ID` environment variable
> instead of `ManagedIdentityClientId`; `DefaultAzureCredential` honors it for every extension.

## Per-extension support matrix

| Extension | Key / connection string | RBAC (`DefaultAzureCredential`) | Interactive login | Service principal (secret/cert) | User-assigned MI via setting |
| --- | --- | --- | --- | --- | --- |
| [Azure Cosmos DB](../Extensions/Cosmos/README.md) | ✅ `ConnectionString` | ✅ | ✅ | ✅ `ClientSecret` / `ClientCertificatePath` | ✅ `ManagedIdentityClientId` |
| [Azure Blob Storage](../Extensions/AzureBlob/README.md) | ✅ `ConnectionString` | ✅ | ✅ | ⚠️ env vars only | ⚠️ `AZURE_CLIENT_ID` env var |
| [Azure Table API](../Extensions/AzureTableAPI/README.md) | ✅ `ConnectionString` | ✅ | ✅ | ⚠️ env vars only | ⚠️ `AZURE_CLIENT_ID` env var |
| [Azure Cognitive Search](../Extensions/CognitiveSearch/README.md) | ✅ `ApiKey` only | ⛔ | ⛔ | ⛔ | ⛔ |

> **Legend:** ✅ supported via a tool setting · ⚠️ supported through the standard Azure SDK
> environment variables (`DefaultAzureCredential`), not a dedicated tool setting · ⛔ not supported.
>
> **Per-side identity (design intent):** Because the tool moves data between a separate source and
> sink, each side can require a *different* identity. Per-extension credential **settings** are
> evaluated independently for `SourceSettings` and `SinkSettings`, so they can express two distinct
> identities in a single run. The Azure SDK environment variables (`AZURE_TENANT_ID`,
> `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`) are **process-global** and therefore apply the *same*
> identity to both sides.
>
> Today, only the **Cosmos** extension exposes explicit per-side credential settings (service
> principal and user-assigned managed identity). For **Blob** and **Table**, RBAC currently resolves
> through a single process-wide `DefaultAzureCredential`, so a passwordless migration that needs
> *different* identities on the source and sink (for example, Blob account A → Blob account B with
> separate identities) is **not yet expressible** — you can still use key-based connection strings
> per side, or a single shared identity via env vars. Extending explicit per-side credential
> settings to Blob and Table (ideally via a shared, reusable credential helper) is tracked as
> planned follow-up work.

## Azure RBAC role requirements

Microsoft Entra ID authentication only succeeds when the signed-in identity has the right role
assignments. Cosmos DB distinguishes **control-plane** roles (manage the account) from
**data-plane** roles (read/write items). Migration reads and writes items, so the identity needs a
**data-plane** role.

### Azure Cosmos DB for NoSQL data-plane roles

| Role | Permissions | Use for |
| --- | --- | --- |
| `Cosmos DB Built-in Data Reader` | Read items, run queries | Source-only migrations |
| `Cosmos DB Built-in Data Contributor` | Read and write items | Sink (and source) migrations |

Assign a data-plane role with the Azure CLI (note the data-plane commands under
`az cosmosdb sql role`):

```bash
az cosmosdb sql role assignment create \
  --account-name <cosmos-account> \
  --resource-group <resource-group> \
  --role-definition-id 00000000-0000-0000-0000-000000000002 \
  --principal-id <object-id-of-identity> \
  --scope "/"
```

`00000000-0000-0000-0000-000000000002` is the built-in **Data Contributor** role; use
`...000000000001` for the built-in **Data Reader** role. See
[Grant data-plane role-based access](https://learn.microsoft.com/azure/cosmos-db/nosql/how-to-grant-data-plane-role-based-access).

### Azure Storage roles (Blob and Table extensions)

Assign a built-in Azure role on the storage account (or container/table scope):

| Service | Read | Read/Write |
| --- | --- | --- |
| Blob | `Storage Blob Data Reader` | `Storage Blob Data Contributor` |
| Table | `Storage Table Data Reader` | `Storage Table Data Contributor` |

See [Authorize access to tables using Microsoft Entra ID](https://learn.microsoft.com/azure/storage/tables/authorize-access-azure-active-directory)
and [Authorize access to blobs using Microsoft Entra ID](https://learn.microsoft.com/azure/storage/blobs/authorize-access-azure-active-directory).

## End-to-end example: passwordless Cosmos → Cosmos migration

This example reads from one Cosmos DB account and writes to another, using RBAC with no keys or
secrets. The identity running DMT (your `az login` user, a Managed Identity, or a service principal)
must have **Data Reader** on the source account and **Data Contributor** on the sink account.

```json
{
  "Source": "Cosmos-nosql",
  "Sink": "Cosmos-nosql",
  "SourceSettings": {
    "UseRbacAuth": true,
    "AccountEndpoint": "https://source-account.documents.azure.com:443/",
    "Database": "myDb",
    "Container": "sourceContainer"
  },
  "SinkSettings": {
    "UseRbacAuth": true,
    "AccountEndpoint": "https://sink-account.documents.azure.com:443/",
    "Database": "myDb",
    "Container": "sinkContainer",
    "PartitionKeyPath": "/id"
  }
}
```

Run it after signing in with the Azure CLI:

```bash
az login
dmt run --settings ./migrationsettings.json
```

### Using a user-assigned Managed Identity (Cosmos)

When the host has more than one user-assigned Managed Identity, set `ManagedIdentityClientId` to
pick the one to authenticate with:

```json
{
  "UseRbacAuth": true,
  "AccountEndpoint": "https://source-account.documents.azure.com:443/",
  "ManagedIdentityClientId": "<user-assigned-identity-client-id>",
  "Database": "myDb",
  "Container": "myContainer"
}
```

### Using a service principal (Cosmos)

```json
{
  "UseRbacAuth": true,
  "AccountEndpoint": "https://source-account.documents.azure.com:443/",
  "TenantId": "<tenant-id>",
  "ClientId": "<client-id>",
  "ClientSecret": "<client-secret>",
  "Database": "myDb",
  "Container": "myContainer"
}
```

Inject the secret at runtime instead of storing it in the file:

```bash
dmt run --settings ./migrationsettings.json --SourceSettings:ClientSecret="$SP_SECRET"
```

## Storage extensions: Azure Blob and Azure Table API

The Blob and Table extensions enable RBAC by setting `UseRbacAuth: true` and an `AccountEndpoint`.
They authenticate with a single process-wide `DefaultAzureCredential`, so the credential resolves
from `az login`, a Managed Identity, or the standard `AZURE_TENANT_ID` / `AZURE_CLIENT_ID` /
`AZURE_CLIENT_SECRET` environment variables (these apply the *same* identity to both source and
sink — see the [per-side identity note](#per-extension-support-matrix) above). The identity needs a
data-plane Storage role on the account (`Storage Blob Data Reader`/`Contributor` or
`Storage Table Data Reader`/`Contributor`).

**Azure Blob (RBAC):**

```json
{
  "UseRbacAuth": true,
  "AccountEndpoint": "https://<storage-account>.blob.core.windows.net",
  "ContainerName": "my-container",
  "BlobName": "data.json",
  "EnableInteractiveCredentials": true
}
```

**Azure Table API (RBAC):**

```json
{
  "UseRbacAuth": true,
  "AccountEndpoint": "https://<storage-account>.table.core.windows.net",
  "Table": "MyTable",
  "PartitionKeyFieldName": "State",
  "RowKeyFieldName": "id",
  "EnableInteractiveCredentials": true
}
```

To authenticate as a service principal, set the `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, and
`AZURE_CLIENT_SECRET` environment variables before running DMT; `DefaultAzureCredential` picks them
up automatically. Dedicated per-side service principal / user-assigned managed identity *settings*
(as the Cosmos extension offers) are planned follow-up work for these extensions.

## CI/CD and container guidance

- **Azure Pipelines / GitHub Actions on Azure-hosted runners:** Prefer a **Managed Identity** or a
  workload-identity federation service connection. With `UseRbacAuth: true` and no explicit
  service principal settings, `DefaultAzureCredential` automatically uses the host identity — no
  secrets in the pipeline.
- **Service principal in a pipeline:** Set `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, and
  `AZURE_CLIENT_SECRET` as secret pipeline variables. `DefaultAzureCredential` (Cosmos, Blob,
  Table) picks them up automatically, or pass them explicitly to the Cosmos extension via
  `TenantId`/`ClientId`/`ClientSecret`. Note that env-var credentials apply the *same* identity to
  both source and sink; to use *different* identities per side today, use the Cosmos extension's
  per-side settings (Blob/Table per-side settings are planned — see the per-side identity note
  above).
- **Docker container:** Pass credentials as environment variables to the container, or mount a
  certificate and reference it with `ClientCertificatePath`. When running on Azure with a Managed
  Identity, the identity flows into the container automatically. Do not bake secrets into images.
- **Local interactive runs:** Run `az login`, or set `EnableInteractiveCredentials: true` to be
  prompted to sign in.

## Troubleshooting

- **`DefaultAzureCredential` failed to retrieve a token:** Ensure you are signed in
  (`az login`) or that a Managed Identity / environment-variable service principal is available.
  See [Troubleshoot Azure Identity authentication issues](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/identity/Azure.Identity/TROUBLESHOOTING.md).
- **403 Forbidden after authenticating:** The identity authenticated but lacks a **data-plane**
  role assignment. For Cosmos, assign a built-in Data Reader/Contributor role (control-plane roles
  such as Contributor do **not** grant data access).
- **Wrong identity selected on a multi-identity host:** Set `ManagedIdentityClientId` (Cosmos) or
  the `AZURE_CLIENT_ID` environment variable to select the intended user-assigned identity.
- **Interactive prompt never appears:** Interactive login only triggers when
  `EnableInteractiveCredentials` is `true` and no other credential in the chain succeeds.

## References

- [Migrate to passwordless connections for Azure Cosmos DB](https://learn.microsoft.com/azure/cosmos-db/nosql/migrate-passwordless?tabs=sign-in-azure-cli%2Cdotnet%2Cazure-portal-create%2Cazure-portal-associate%2Capp-service-identity)
- [Grant data-plane role-based access in Azure Cosmos DB for NoSQL](https://learn.microsoft.com/azure/cosmos-db/nosql/how-to-grant-data-plane-role-based-access)
- [Azure Identity client library for .NET](https://learn.microsoft.com/dotnet/api/overview/azure/identity-readme)
- [Troubleshoot Azure Identity authentication issues](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/identity/Azure.Identity/TROUBLESHOOTING.md)
- [Authorize access to tables using Microsoft Entra ID](https://learn.microsoft.com/azure/storage/tables/authorize-access-azure-active-directory)
- [Authorize access to blobs using Microsoft Entra ID](https://learn.microsoft.com/azure/storage/blobs/authorize-access-azure-active-directory)
