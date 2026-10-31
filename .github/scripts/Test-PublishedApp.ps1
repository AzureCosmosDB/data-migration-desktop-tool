param(
    [Parameter(Mandatory)]
    [string] $PackagePath
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$package = (Resolve-Path -LiteralPath $PackagePath).Path
$executable = Join-Path $package $(if ($IsWindows) { 'dmt.exe' } else { 'dmt' })
$expectedRuntime = ([xml](Get-Content -LiteralPath "$PSScriptRoot/../../Directory.Build.props" -Raw)).Project.PropertyGroup.RuntimeFrameworkVersion
$runtimeJson = Get-Content -LiteralPath (Join-Path $package 'runtime-info.json') -Raw
$runtime = $runtimeJson | ConvertFrom-Json
if ($runtime.runtimeOptions.tfm -ne 'net10.0' -or
    @($runtime.runtimeOptions.includedFrameworks | Where-Object {
        $_.name -eq 'Microsoft.NETCore.App' -and $_.version -eq $expectedRuntime
    }).Count -ne 1) {
    throw "Package does not contain the expected self-contained .NET $expectedRuntime runtime configuration."
}
# Single-file bundles retain runtimeconfig JSON uncompressed. Check the executable,
# not just a sidecar that could be copied from an unrelated build.
$bundle = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($executable))
if (-not $bundle.Contains($runtimeJson)) {
    throw 'Runtime metadata does not match the configuration embedded in the executable.'
}
$bundle = $null

$expectedExtensions = @(
    'AzureTableAPI', 'CognitiveSearch', 'Cosmos-nosql', 'CSV', 'CSV-AWSS3',
    'CSV-AzureBlob', 'JSON', 'JSON-AWSS3', 'JSON-AzureBlob', 'MongoDB',
    'Parquet', 'Parquet-AWSS3', 'Parquet-AzureBlob', 'PostgreSQL', 'SqlServer'
)
foreach ($kind in @('sources', 'sinks')) {
    $sources = ($kind -eq 'sources').ToString().ToLowerInvariant()
    $sinks = ($kind -eq 'sinks').ToString().ToLowerInvariant()
    $output = & $executable list --sources $sources --sinks $sinks 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Published extension discovery failed: $output" }
    $names = @($output | Where-Object { "$_" -match '^\t' } | ForEach-Object { "$_".Trim() })
    if ($names.Count -ne $expectedExtensions.Count -or
        @(Compare-Object ($names | Sort-Object) ($expectedExtensions | Sort-Object)).Count -ne 0) {
        throw "Unexpected published $kind extensions: $($names -join ', ')"
    }
}

$scratch = Join-Path ([IO.Path]::GetTempPath()) "dmt-smoke-$([guid]::NewGuid())"
$null = New-Item -ItemType Directory -Path $scratch
try {
    $inputPath = Join-Path $scratch 'input.json'
    $outputPath = Join-Path $scratch 'output.json'
    $settingsPath = Join-Path $scratch 'settings.json'
    $expectedJson = '[{"id":"one","value":42,"nullable":null},{"id":"two","value":-1,"active":true}]'
    $expectedJson | Set-Content -LiteralPath $inputPath -Encoding utf8NoBOM
    @{
        Source = 'JSON'; Sink = 'JSON'
        SourceSettings = @{ FilePath = $inputPath }
        SinkSettings = @{ FilePath = $outputPath; IncludeNullFields = $true }
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $settingsPath -Encoding utf8NoBOM
    & $executable run --settings $settingsPath
    if ($LASTEXITCODE -ne 0) { throw 'Published JSON migration failed.' }
    & "$PSScriptRoot/Assert-JsonRoundTrip.ps1" -ExpectedJson $expectedJson `
        -ActualJson (Get-Content -LiteralPath $outputPath -Raw)
    Write-Host "Published application passed: .NET $expectedRuntime; 15 sources; 15 sinks; JSON round trip."
} finally {
    Remove-Item -LiteralPath $scratch -Recurse -Force
}
