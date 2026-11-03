param(
    [Parameter(Mandatory)]
    [string] $PackagePath
)

$ErrorActionPreference = 'Stop'
$package = (Resolve-Path -LiteralPath $PackagePath).Path
$versions = @{}
([xml](Get-Content -LiteralPath "$PSScriptRoot/../../Directory.Packages.props" -Raw)).Project.ItemGroup.PackageVersion |
    ForEach-Object { $versions[$_.Include] = $_.Version }
$assemblies = @{
    'MongoDB.Driver' = 'MongoDB.Driver'
    'MongoDB.Bson' = 'MongoDB.Driver'
    'MongoDB.Driver.Authentication.AWS' = 'MongoDB.Driver.Authentication.AWS'
    'MongoDB.Driver.Encryption' = 'MongoDB.Driver.Encryption'
    'SharpCompress' = 'SharpCompress'
    'Snappier' = 'Snappier'
    'System.Security.Cryptography.Pkcs' = 'System.Security.Cryptography.Pkcs'
}
foreach ($name in $assemblies.Keys) {
    $path = Join-Path $package "Extensions/$name.dll"
    $actual = [Diagnostics.FileVersionInfo]::GetVersionInfo($path).ProductVersion.Split('+')[0]
    $expected = $versions[$assemblies[$name]]
    if ($actual -ne $expected) { throw "$name binary is $actual; expected $expected." }
    Write-Host "$name binary: $actual"
}
foreach ($obsolete in @('MongoDB.Driver.Core.dll', 'MongoDB.Libmongocrypt.dll')) {
    if (Test-Path -LiteralPath (Join-Path $package "Extensions/$obsolete")) {
        throw "Stale MongoDB 2.x assembly in package: $obsolete"
    }
}

$dependencyJson = Get-Content -LiteralPath (Join-Path $package 'dependency-info.json') -Raw
$dependencies = $dependencyJson | ConvertFrom-Json -AsHashtable
$pkcs = "System.Security.Cryptography.Pkcs/$($versions['System.Security.Cryptography.Pkcs'])"
if (-not $dependencies.libraries.ContainsKey($pkcs)) {
    throw "Bundled core dependency metadata is missing $pkcs."
}
$executable = Join-Path $package $(if (Test-Path -LiteralPath (Join-Path $package 'dmt.exe')) { 'dmt.exe' } else { 'dmt' })
$bundle = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($executable))
if (-not $bundle.Contains($dependencyJson)) {
    throw 'Dependency metadata does not match the metadata embedded in the executable.'
}
Write-Host "Bundled core dependency: $pkcs"
