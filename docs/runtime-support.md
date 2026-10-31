# Runtime support and servicing

## .NET 10 baseline

This source targets .NET 10 LTS in all 26 core, shared, extension and test
projects. `global.json` selects stable SDK **10.0.403** without roll-forward or
previews; `Directory.Build.props` selects runtime **10.0.14**. These versions were
checked against Microsoft's [release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json)
on October 31, 2026. .NET 10 support ends November 14, 2028; .NET 8 support ends
November 10, 2026. See the [.NET support policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core).

**This is not a release announcement.** A DMT release version and publication
require maintainer approval. Previously published archives/images still embed
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
- .NET 10 audits transitive NuGet packages. Do not suppress advisories or NU1701
  to make an upgrade appear clean. The existing MongoDB/Parquet dependency trees
  include SharpCompress/Snappier advisories, SQL Client includes Pkcs, and the
  test-only SQLite tree includes a native SQLite advisory. These require explicit
  dependency remediation/review before release; a successful compile is not a
  security disposition.

## Validation and release gates

`internal-test-build.yml` builds all six self-contained packages, then runs
native discovery and synthetic JSON round trips on each OS/architecture.
`runtime-info.json` retains the runtime configuration used to create each
single-file executable so the bundled runtime can be inspected; native smoke
checks also require that exact configuration to be embedded in the executable. Docker
validation builds a local image and runs discovery plus a migration with mounted
configuration/data; it does not push an image. Cross-publishing alone is not
native execution. Cloud CI results, not merely the existence of these jobs,
determine which platforms have actually passed.

Windows and stable Linux Cosmos emulator tests must execute their integration
cases. Preview vNext permits only the two named missing/null and malformed-key
conformance exclusions already enforced by the TRX guard; no new exclusions are
introduced for .NET 10. Local tests without emulators or SQL Server do not prove
live-service, proxy authentication, RBAC negotiation or SQL import behavior.

Before publication, maintainers must:

1. Review the final commit's full test, emulator TRX, six-package/native smoke and
   container evidence; resolve outstanding dependency/native/service limits.
2. Select the DMT release version and approve release notes/publication. No new
   version is implied by this runtime upgrade; .NET-tool distribution remains
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
Updating a host runtime or moving an image tag without rebuilding/verifying DMT
does not service the embedded runtime.
