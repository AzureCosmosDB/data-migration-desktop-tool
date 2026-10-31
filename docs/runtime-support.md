# Runtime support and servicing

## .NET 10 baseline

This source targets .NET 10 LTS in all core, shared, extension and test
projects. `global.json` selects stable SDK **10.0.403** without roll-forward or
previews; `Directory.Build.props` selects runtime **10.0.14**. These versions were
checked against Microsoft's [release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json)
on October 31, 2026. .NET 10 support ends November 14, 2028; .NET 8 support ends
November 10, 2026. See the [.NET support policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core).

**This is not a release announcement.** DMT **4.0.0** is the selected upcoming
major version; review, merge and publication remain separate approval gates.
Previously published archives/images still embed
their original runtime. Installing a host SDK/runtime does not service them.
Upgrade by replacing the complete DMT installation or pulling an explicitly
.NET 10-based image, retaining your separately stored configuration/data.

## Platforms

The release matrix remains Windows, Linux and macOS, each on x64 and ARM64.
Choose an OS version and architecture supported by both
[.NET 10](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md)
and your database/storage driver. In particular, use a supported Windows edition,
macOS 15 or newer still in Apple's support window, or a supported glibc Linux
distribution such as Ubuntu 22.04/24.04/26.04. Linux archives use `linux-*`, not
`linux-musl-*`: they are not Alpine/musl packages. No x86 or ARM32 DMT archive is
produced. OS support changes independently of .NET's LTS end date.

Self-contained applications still need native OS libraries. Linux needs the
.NET prerequisites (including ICU, OpenSSL and CA roots); SQL integrated
authentication can additionally require Kerberos configuration. The Dockerfile
uses explicit Ubuntu 24.04 `noble` SDK/runtime-dependencies images, rather than
silently inheriting a changing default distribution. It does not install Azure
CLI or use a developer's identity. Image builds default to Linux x64; specify
`RUNTIME=linux-arm64` on a native ARM64 builder.

## Extensions

The packaged extension set is unchanged: JSON, CSV and Parquet (local, Azure Blob
and AWS S3), Cosmos, Azure Table, MongoDB, SQL Server, PostgreSQL and Cognitive
Search. All are discovered using MEF; trimming, AOT and ReadyToRun remain disabled.
Storage assemblies are dependencies of the format extensions, not standalone
source/sink exports.

MongoDB Vector and MongoDB Legacy remain source projects, not newly added release
payloads. Vector uses the existing preview OpenAI dependency; this upgrade does
not promote that feature. Legacy still uses `mongocsharpdriver` 1.11.0, a .NET
Framework-only package that emits NU1701. Retargeting/compiling is not proof of
live legacy-server or native-platform compatibility. Keep legacy and modern
MongoDB driver assemblies in separate extension directories/processes; they
cannot safely overwrite each other's `MongoDB.Bson` payload. Resolve that
compatibility warning and verify a disposable legacy server before claiming
legacy support on a new platform.

External extension authors should rebuild for `net10.0` against the matching
`Cosmos.DataTransfer.Interfaces` and Common assemblies and central dependencies.
Older `net8.0` binaries might load, but this is not a compatibility guarantee.
Verify discovery **and an actual representative transfer** with the published
host on every intended OS/architecture. Avoid replacing the host's shared
assemblies with stale copies, and include each dependency's correct native RID
assets. .NET 10 changed [single-file native library probing](https://learn.microsoft.com/dotnet/core/compatibility/interop/10.0/native-library-search);
custom P/Invokes must use appropriate search paths rather than rely on implicit
probing of the executable directory.

## Compatibility notes

- Cosmos Encryption **2.0.5** declares Cosmos SDK **>= 3.51.0**. DMT selects
  **3.51.0**, replacing the incompatible 2.0.4/3.49.0 pairing without removing
  encryption or suppressing NU1608. Live encrypted-container/Key Vault behavior
  still requires a safe test environment.
- Microsoft.Extensions, MEF and ConfigurationManager use serviced .NET 10
  packages. System.Text.Json comes from the target framework instead of an
  obsolete explicit 8.x package reference. Async LINQ/Interactive use 7.0.0,
  which interoperates with the AsyncEnumerable APIs now built into .NET 10.
- JSON streaming diagnostics retain global outer-array indices. The 10.0.14
  serializer clears its buffered list between reads, while `ReadStack.JsonPath`
  uses that list's count; the existing correction is still necessary. The
  regression suite covers split buffers, nested errors, null entries, BOMs and
  original exception diagnostics.
- Certificate authentication uses `X509CertificateLoader` for PFX/PKCS#12 files,
  replacing obsolete constructors. The private key remains required, and
  ephemeral key storage remains enabled.
- [.NET 10 configuration binding preserves nulls and empty arrays](https://learn.microsoft.com/dotnet/core/compatibility/extensions/10.0/configuration-null-values-preserved).
  Omit optional settings to retain defaults; do not use `null` as a substitute
  for omission. Null elements in `PartitionKeyPaths` are preserved and rejected
  by validation. An empty JSON array still leaves this `List<string>` property
  unbound (and permits the singular fallback), unlike an explicitly constructed
  empty list, which validation rejects. To use the singular path, omit the
  plural setting rather than relying on empty-array binding.
  Environment/command-line override precedence is unchanged.
- .NET 10 audits transitive NuGet packages. Do not suppress advisories, NU1608 or
  NU1701 to make an upgrade appear clean. The release prerequisites below remove
  the identified vulnerable dependencies; the source-only legacy Mongo NU1701
  compatibility warning remains visible.

## Dependency security prerequisites for 4.0.0

NuGet dependency metadata and the linked advisories were checked on October 31,
2026. Direct references in the consuming projects enforce the central servicing
versions without enabling global transitive pinning or changing unrelated SDKs.

| Consumer | Previous resolved dependency | Serviced dependency |
| --- | --- | --- |
| MongoDB / source-only Vector | Driver/Core 2.30.0, SharpCompress 0.30.1 | Driver/Bson 3.9.0, SharpCompress 0.48.1 |
| MongoDB and Parquet.Net 4.25.0 / IronCompress 1.5.2 | Snappier 1.0.0 / 1.1.6 | Snappier 1.3.1 in both trees and the shared extension folder |
| Core / SQL Server, SqlClient 6.0.1 | System.Security.Cryptography.Pkcs 9.0.0 | 10.0.14; SqlClient stays 6.0.1 |
| SQL tests, Microsoft.Data.Sqlite 9.0.1 | SQLitePCLRaw 2.1.10, vulnerable native SQLite | SQLitePCLRaw bundle/config/provider/core 3.0.5 and SQLite package/engine 3.53.4; Microsoft.Data.Sqlite stays 9.0.1 |

These address [SharpCompress](https://github.com/advisories/GHSA-6c8g-7p36-r338),
[Snappier](https://github.com/advisories/GHSA-pggp-6c3x-2xmx),
Pkcs [CVE-2026-84008](https://github.com/advisories/GHSA-846f-f764-76mx) and
[CVE-2026-70162](https://github.com/advisories/GHSA-2fmv-rv2c-59x7), and
[native SQLite](https://github.com/advisories/GHSA-2m69-gcr7-jv3q).
SQLitePCLRaw.lib.e_sqlite3 2.1.11 is still vulnerable: renaming the dependency
alone is not remediation. The SQL compatibility test opens the actual provider
and requires `sqlite_version()` >= 3.53.4 (the upstream bundle's engine), above
the advisory's 3.50.2 minimum. SQLite remains test-only.

**MongoDB is an intentional, user-approved breaking change.** A direct
SharpCompress 0.48.0 override under Driver 2.30.0 passed restore but failed real
zlib calls with `TypeLoadException` for the removed `NonDisposingStream`.
[Driver 3.9.0](https://github.com/mongodb/mongo-csharp-driver/releases/tag/v3.9.0)
is the first vendor release containing the corresponding API change and the
0.48.1 dependency. Choosing 3.10 or later would unnecessarily raise the server
floor again. The modern MongoDB and Vector extensions now require **MongoDB
server 4.2+**; MONGODB-CR and TLS 1.0/1.1 are no longer supported. The source-only
legacy extension is unchanged, not a newly certified workaround for every older
server.

Encryption and AWS authentication moved to optional driver packages; DMT includes
both at 3.9.0 and explicitly registers both providers. Native libmongocrypt is
1.18.2. Upstream still supplies no Windows ARM64 libmongocrypt: that native package
does **not** support MongoDB client-side field-level decryption. Use the Windows
x64 build under supported emulation or a supported Linux/macOS architecture for
that feature. The ARM64 capability test asserts this limitation, not successful
encryption. Local KMS/provider initialization is tested without cloud credentials;
live encrypted collection and AWS authentication still require a safe test
environment. CLR GUIDs retain the prior CSharpLegacy byte representation,
including nested values and mapped IDs, rather than adopting driver 3's changed
implicit serialization behavior. No process-wide BSON serializer defaults change.

## Validation and release gates

`internal-test-build.yml` builds all six self-contained packages, then runs
native discovery and synthetic JSON/Parquet round trips on each OS/architecture.
`runtime-info.json` retains the runtime configuration used to create each
single-file executable so the bundled runtime can be inspected; native smoke
checks also require that exact configuration to be embedded in the executable. Docker
validation builds a local image and runs discovery plus a migration with mounted
configuration/data; it does not push an image. Cross-publishing alone is not
native execution. Cloud CI results, not merely the existence of these jobs,
determine which platforms have actually passed.

`dependency-info.json` is checked against the metadata embedded in the executable,
and the serviced extension DLLs' actual product versions are checked after all
plugins have been copied to their shared directory. Native jobs additionally run
Mongo zlib/Snappy interoperability, provider/GUID, Parquet Snappy, Pkcs CMS and
SQLite engine compatibility tests. Container validation checks the extracted
image's actual DLLs and performs both JSON and Parquet migrations inside the image.

The GHCR publisher passes the release tag's semantic version (with an optional
leading `v` removed) to Docker's `BUILD_VERSION`. Explicitly versioned manual tags
do the same. Ordinary manual tags such as `latest` retain the existing 1.0.0
development default; they are not a substitute for a versioned release build.
Version/tag validation happens before registry login. The no-publication container
job tests version selection and checks that its requested build version is embedded.

Windows and stable Linux Cosmos emulator tests must execute their integration
cases. Preview vNext permits only the two named missing/null and malformed-key
conformance exclusions already enforced by the TRX guard; no new exclusions are
introduced for .NET 10. Local tests without emulators or SQL Server do not prove
live-service, proxy authentication, RBAC negotiation or SQL import behavior.

Before publication, maintainers must:

1. Review the final commit's full test, emulator TRX, six-package/native smoke and
   container evidence; resolve outstanding dependency/native/service limits.
2. Review the selected DMT 4.0.0 release version and approve release notes/publication.
   This prerequisite work does not publish it; .NET-tool distribution remains
   deferred under [#6](https://github.com/AzureCosmosDB/data-migration-desktop-tool/issues/6).
3. Use the existing release/image publication workflows after approval. Verify
   all six downloaded archives and published image tags contain the intended
   serviced runtime, and repeat discovery/migration checks on those downloads.
4. Update download/install guidance and communicate replacement of old
   self-contained installations/images. Keep
   [#258](https://github.com/AzureCosmosDB/data-migration-desktop-tool/issues/258)
   open until implementation and approved rollout are accounted for.

For ongoing servicing, review Microsoft's .NET 10 security releases monthly and
out-of-band advisories promptly. Update `global.json`, `Directory.Build.props`,
SDK/runtime-dependencies container tags, matching runtime package versions and
this baseline together; retain full-SHA action pins and Dependabot policy.
Rebuild, validate, approve and republish DMT archives/images for runtime patches.
Audit the complete resolved NuGet graph at the same time, including test and
source-only projects. Retain compression/provider and packaged-DLL checks when
servicing the explicitly pinned dependencies; a clean audit alone does not prove
binary compatibility.
Updating a host runtime or moving an image tag without rebuilding/verifying DMT
does not service the embedded runtime.
