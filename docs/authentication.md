# Authentication with Microsoft Entra ID and Azure RBAC

The Azure Cosmos DB Data Migration Tool (DMT) supports both key-based authentication and
passwordless authentication using **Microsoft Entra ID** (formerly Azure AD) together with
**Azure role-based access control (RBAC)**. This guide is the central reference for configuring
authentication across the Azure extensions. Each extension's own README links back here and adds
extension-specific details.

## How authentication is configured in DMT

DMT is **configuration-file driven**. `Source` and `Sink` select the extensions; authentication is
specified in `SourceSettings` and `SinkSettings` inside `migrationsettings.json` (the file you pass
with `--settings`). DMT does **not**
use dedicated authentication CLI flags such as `--auth-mode entra-id`. Instead, you set
authentication properties in the settings file, and any property can be overridden at runtime via
standard .NET configuration providers — environment variables, command-line arguments, or
[User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets).

For example, to keep a secret out of the settings file you can inject it at runtime:

```bash
SourceSettings__ClientSecret="$MY_CLIENT_SECRET" dmt run --settings ./migrationsettings.json
```

Environment variables use `__` for nested settings; CLI overrides use `:`, for example
`--SourceSettings:EnableInteractiveCredentials=true`. Use `SinkSettings__...` or
`--SinkSettings:...` for the sink. User Secrets are loaded only when
`DOTNET_ENVIRONMENT=Development`, using DMT's `UserSecretsId` (not another application's ID).
For multi-operation files, an operation's settings override the shared source/sink settings;
override `Operations__0__SourceSettings__ClientSecret` to target the first operation when it
defines its own value.

> **Security:** Never commit secrets (`ConnectionString`, `AccountKey`, `ApiKey`, `ClientSecret`,
> `ClientCertificatePassword`) to source control. Prefer passwordless authentication, and when a
> secret is unavoidable, inject it at runtime through protected environment variables or User
> Secrets. CLI overrides also work, but secret values in arguments can be exposed in process
> listings, shell history, or pipeline logs.

## Supported credential flows

When an Azure extension is set to RBAC mode (`UseRbacAuth: true`), DMT authenticates using the
[Azure Identity client library](https://learn.microsoft.com/dotnet/api/overview/azure/identity-readme).
The credential is selected based on the settings you provide:

| Credential flow | How to enable | Typical use |
| --- | --- | --- |
| **DefaultAzureCredential** | `UseRbacAuth: true` with an account endpoint and no explicit service principal settings | Local development (`az login`), Managed Identity in Azure, and CI/CD environments. Tries a chain of sources (environment variables, Managed Identity, Azure CLI, etc.). |
| **Azure CLI** | Part of the `DefaultAzureCredential` chain; run `az login` first | Interactive local runs on a developer machine. |
| **Interactive browser** | `UseRbacAuth: true` + `EnableInteractiveCredentials: true` | Local runs where you want to be prompted to sign in if no other credential is available. |
| **System-assigned Managed Identity** | `UseRbacAuth: true` running on a supported Azure host with an enabled system-assigned identity | For example, a self-hosted runner on an Azure VM with access to its managed identity endpoint. |
| **User-assigned Managed Identity** | `UseRbacAuth: true` + `ManagedIdentityClientId: <client-id>` *(Cosmos extension)* | Hosts that have more than one user-assigned identity and need to select a specific one. |
| **Service principal (client secret)** | `UseRbacAuth: true` + `TenantId` + `ClientId` + `ClientSecret` *(Cosmos extension)* | Unattended automation where a registered app authenticates with a secret. Blob/Table use Azure Identity environment variables instead. |
| **Service principal (certificate)** | `UseRbacAuth: true` + `TenantId` + `ClientId` + `ClientCertificatePath` (+ optional `ClientCertificatePassword`) *(Cosmos extension)* | Unattended automation using certificate credentials instead of a secret. Blob/Table use Azure Identity environment variables instead. |

> For user-assigned Managed Identity, you can also set the `AZURE_CLIENT_ID` environment variable
> instead of `ManagedIdentityClientId`; the Cosmos, Blob, and Table RBAC paths honor it. An explicit
> Cosmos `ManagedIdentityClientId` overrides that default for the managed-identity step only.
> It does **not** force managed identity authentication: environment and workload credentials are
> tried first. It cannot be combined with explicit Cosmos service principal settings.

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
> **Per-side identity:** Because the tool moves data between a separate source and
> sink, each side can require a *different* identity. Per-extension credential **settings** are
> evaluated independently for `SourceSettings` and `SinkSettings`, so they can express two distinct
> identities in a single run. The Azure SDK environment variables (`AZURE_TENANT_ID`,
> `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`) are **process-global** and therefore apply the *same*
> identity to both sides.
>
> Only the **Cosmos** extension exposes explicit per-side credential settings (service principal
> and user-assigned managed identity). **Blob** and **Table** create separate
> `DefaultAzureCredential` instances, but those instances read the same process environment and
> developer sign-ins. They have no per-side setting to select distinct RBAC identities. Use one
> identity authorized for both sides or key-based connection strings per side. For Cosmos,
> separate `ManagedIdentityClientId` values select different identities only when the chain
> reaches managed identity; they do not override an earlier environment/workload credential.

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

> **Existing resources required:** In RBAC mode, DMT does not create the Cosmos database or
> container, and `RecreateContainer: true` is rejected. Create the destination database and
> container beforehand, with the intended partition key, using the Azure portal, CLI, or
> management SDK and appropriate control-plane permissions. Setting `PartitionKeyPath` in a
> migration file does not create or change an RBAC destination container.

### Azure Storage roles (Blob and Table extensions)

Assign a built-in Azure role on the storage account (or container/table scope):

| Service | Read | Read/Write |
| --- | --- | --- |
| Blob | `Storage Blob Data Reader` | `Storage Blob Data Contributor` |
| Table | `Storage Table Data Reader` | `Storage Table Data Contributor` |

See [Authorize access to tables using Microsoft Entra ID](https://learn.microsoft.com/azure/storage/tables/authorize-access-azure-active-directory)
and [Authorize access to blobs using Microsoft Entra ID](https://learn.microsoft.com/azure/storage/blobs/authorize-access-azure-active-directory).

The Storage roles and Table endpoint examples here apply to **Azure Storage Tables**.
Azure Cosmos DB for Table uses its own
[native data-plane RBAC roles](https://learn.microsoft.com/azure/cosmos-db/table/how-to-connect-role-based-access-control);
Storage Table roles and the NoSQL `az cosmosdb sql role` commands above do not authorize that service.
For Storage sinks that need to create a container/table, grant the required create permission at
a scope containing the new resource, such as the storage account.

## End-to-end example: passwordless Cosmos → Cosmos migration

This example reads from one Cosmos DB account and writes to another, using RBAC with no keys or
secrets. The identity running DMT (your `az login` user, a Managed Identity, or a service principal)
must have **Data Reader** on the source account and **Data Contributor** on the sink account.
Both databases and containers must already exist. This example assumes the destination container
uses `/id` as its partition key.

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

Attach the user-assigned managed identity to a supported Azure host and grant it the required
data-plane role. Set `ManagedIdentityClientId` to select it for the managed-identity step:

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
  "Database": "myDb",
  "Container": "myContainer"
}
```

Place these properties in `SourceSettings` and inject the required secret at runtime instead of
storing it in the file (use `SinkSettings__ClientSecret` for a sink):

```bash
SourceSettings__ClientSecret="$SP_SECRET" dmt run --settings ./migrationsettings.json
```

For certificate authentication, use `ClientCertificatePath` instead of `ClientSecret`, pointing
to a certificate file containing a private key. If password-protected, supply
`ClientCertificatePassword` through the corresponding environment variable. Set both `TenantId`
and `ClientId`, and do not configure both a secret and a certificate.

## Storage extensions: Azure Blob and Azure Table API

The Blob and Table extensions enable RBAC by setting `UseRbacAuth: true` and an `AccountEndpoint`.
They each construct `DefaultAzureCredential`, so the credential resolves
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

**Azure Storage Tables via AzureTableAPI (RBAC):**

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
up automatically. For certificate credentials, set `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, and
`AZURE_CLIENT_CERTIFICATE_PATH` (plus `AZURE_CLIENT_CERTIFICATE_PASSWORD` when required), without
`AZURE_CLIENT_SECRET`. Dedicated per-side service principal / managed identity settings are not
available for these extensions.

## CI/CD and container guidance

- **Self-hosted runners/agents on supported Azure resources:** A managed identity can be used
  when it is enabled on the host, accessible from the runner, and has the required data-plane
  roles. Set `UseRbacAuth: true`, leave explicit service principal settings unset, and ensure an
  earlier credential in the default chain is not configured to use a different identity.
- **GitHub-hosted runners:** They do not expose a host managed identity to your job. Configure
  [OpenID Connect with `azure/login@v2`](https://learn.microsoft.com/azure/developer/github/connect-from-azure-openid-connect)
  using a federated identity credential, `id-token: write` permission, and the client, tenant,
  and subscription IDs. Run DMT in a subsequent shell step in the **same job and user context**,
  with Azure CLI installed and its login accessible. DMT's `DefaultAzureCredential` can use that
  CLI login; enabling OIDC permission alone does not authenticate DMT.
- **Microsoft-hosted Azure Pipelines agents:** Use a workload-identity federation service
  connection with an authenticated [`AzureCLI@2` task](https://learn.microsoft.com/azure/devops/pipelines/tasks/reference/azure-cli-v2).
  Run DMT inside that task's script so it can use the task's Azure CLI login. A service connection
  alone does not make credentials available to an arbitrary later script.
- **Service principal in a pipeline:** Set `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, and
  `AZURE_CLIENT_SECRET` as secret pipeline variables. `DefaultAzureCredential` (Cosmos, Blob,
  Table) picks them up automatically, or pass them explicitly to the Cosmos extension via
  `TenantId`/`ClientId`/`ClientSecret`. Note that env-var credentials apply the *same* identity to
  both source and sink; to use *different* identities per side today, use the Cosmos extension's
  per-side settings. Azure Pipelines secret variables must be explicitly mapped to the task's
  environment; GitHub Actions secrets must similarly be supplied using `env`.
- **Docker container:** Pass credentials as environment variables to the container, or mount a
  certificate and reference it with `ClientCertificatePath` (Cosmos) or
  `AZURE_CLIENT_CERTIFICATE_PATH` (default chain). A host's Azure CLI login does not automatically
  appear inside a container. Managed identity requires a supported hosting configuration and a
  reachable identity endpoint. For AKS, configure Microsoft Entra Workload ID for the pod rather
  than assuming the cluster's managed identity is the application's identity. Azure Identity
  1.13.2, used by DMT, supports `WorkloadIdentityCredential` with `AZURE_TENANT_ID`,
  `AZURE_CLIENT_ID`, and `AZURE_FEDERATED_TOKEN_FILE` pointing to the projected token file.
  Do not bake secrets into images.
- **Local interactive runs:** Run `az login`, or set `EnableInteractiveCredentials: true` to be
  prompted to sign in.

## Troubleshooting

- **`DefaultAzureCredential` failed to retrieve a token:** Ensure you are signed in
  (`az login`) or that a Managed Identity / environment-variable service principal is available.
  See [Troubleshoot Azure Identity authentication issues](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/identity/Azure.Identity/TROUBLESHOOTING.md).
- **403 Forbidden:** Check the service's error details. A missing **data-plane** role can cause
  this; for Cosmos, control-plane roles such as Contributor do **not** grant item access. Firewall
  and private-network restrictions can also cause 403 responses. See
  [Troubleshoot Cosmos DB forbidden exceptions](https://learn.microsoft.com/azure/cosmos-db/troubleshoot-forbidden).
- **Wrong identity selected on a multi-identity host:** Set `ManagedIdentityClientId` (Cosmos) or
  the `AZURE_CLIENT_ID` environment variable to select the intended user-assigned identity for
  the managed-identity step. Check for environment/workload credentials earlier in the chain.
- **Interactive prompt never appears:** Interactive login only triggers when
  `EnableInteractiveCredentials` is `true` and the chain reaches the browser credential. A
  configured environment, workload, or managed identity credential that fails authentication
  can stop the chain before then; explicit Cosmos service principal settings bypass the chain.

## References

- [Migrate to passwordless connections for Azure Cosmos DB](https://learn.microsoft.com/azure/cosmos-db/nosql/migrate-passwordless?tabs=sign-in-azure-cli%2Cdotnet%2Cazure-portal-create%2Cazure-portal-associate%2Capp-service-identity)
- [Grant data-plane role-based access in Azure Cosmos DB for NoSQL](https://learn.microsoft.com/azure/cosmos-db/nosql/how-to-grant-data-plane-role-based-access)
- [Azure Identity client library for .NET](https://learn.microsoft.com/dotnet/api/overview/azure/identity-readme)
- [Azure Identity 1.13.2 credential and environment-variable reference](https://github.com/Azure/azure-sdk-for-net/blob/Azure.Identity_1.13.2/sdk/identity/Azure.Identity/README.md)
- [Microsoft Entra Workload ID on AKS](https://learn.microsoft.com/azure/aks/workload-identity-overview)
- [Troubleshoot Azure Identity authentication issues](https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/identity/Azure.Identity/TROUBLESHOOTING.md)
- [Authorize access to tables using Microsoft Entra ID](https://learn.microsoft.com/azure/storage/tables/authorize-access-azure-active-directory)
- [Authorize access to blobs using Microsoft Entra ID](https://learn.microsoft.com/azure/storage/blobs/authorize-access-azure-active-directory)
