param(
    [string] $ResultsDirectory = 'TestResults/guard'
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$project = 'Extensions/Cosmos/Cosmos.DataTransfer.CosmosExtension.UnitTests/'
$originalEndpoint = $env:Cosmos_Endpoint
$originalKey = $env:Cosmos_Key
$originalSummary = $env:GITHUB_STEP_SUMMARY
$originalVariant = $env:Cosmos_EmulatorVariant
$emulatorKey = 'C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=='

function Assert-GateRejects([string] $Path, [string] $EmulatorVariant = 'current') {
    $rejected = $false
    try {
        & "$PSScriptRoot/Assert-CosmosIntegrationResults.ps1" -Path $Path -EmulatorVariant $EmulatorVariant
    } catch {
        Write-Host "Expected rejection: $($_.Exception.Message)"
        $rejected = $true
    }
    if (-not $rejected) {
        throw "Integration result guard incorrectly accepted $Path."
    }
}

try {
    # Negative-test output must not masquerade as the real emulator job's summary.
    $env:GITHUB_STEP_SUMMARY = $null
    $env:Cosmos_EmulatorVariant = 'current'
    $scenarios = @(
        @{ Name = 'unconfigured'; Endpoint = $null; Key = $null; Outcome = 'NotExecuted'; ExitCode = 0 },
        @{ Name = 'missing-key'; Endpoint = 'https://127.0.0.1:1/'; Key = $null; Outcome = 'Failed'; ExitCode = 1 },
        @{ Name = 'missing-endpoint'; Endpoint = $null; Key = $emulatorKey; Outcome = 'Failed'; ExitCode = 1 },
        @{ Name = 'unreachable'; Endpoint = 'https://127.0.0.1:1/'; Key = $emulatorKey; Outcome = 'Failed'; ExitCode = 1 }
    )
    foreach ($scenario in $scenarios) {
        $env:Cosmos_Endpoint = $scenario.Endpoint
        $env:Cosmos_Key = $scenario.Key
        $directory = Join-Path $ResultsDirectory $scenario.Name
        dotnet test $project --no-build --filter 'FullyQualifiedName~CosmosDataSourceExtensionTests' `
            --logger 'trx;LogFileName=cosmos.trx' --results-directory $directory
        if ($LASTEXITCODE -ne $scenario.ExitCode) {
            throw "$($scenario.Name): expected dotnet exit $($scenario.ExitCode), got $LASTEXITCODE."
        }
        $path = Join-Path $directory 'cosmos.trx'
        [xml] $trx = Get-Content -LiteralPath $path -Raw
        $results = @($trx.TestRun.Results.UnitTestResult)
        if ($results.Count -ne 7 -or @($results | Where-Object { $_.outcome -ne $scenario.Outcome }).Count -ne 0) {
            throw "$($scenario.Name): expected seven $($scenario.Outcome) integration tests."
        }
        Assert-GateRejects $path
        Assert-GateRejects $path -EmulatorVariant vnext
    }

    # Exercise successful, missing, and duplicate result handling without an emulator.
    $fixture = Join-Path $ResultsDirectory 'synthetic.trx'
    foreach ($result in $results) {
        $result.outcome = 'Passed'
    }
    $trx.Save([System.IO.Path]::GetFullPath($fixture))
    & "$PSScriptRoot/Assert-CosmosIntegrationResults.ps1" -Path $fixture

    # Preview permits only the two declared capability exclusions, with an explicit reason.
    [xml] $preview = $trx.OuterXml
    $previewFixture = Join-Path $ResultsDirectory 'synthetic-preview.trx'
    $excludedResults = @($preview.TestRun.Results.UnitTestResult | Where-Object {
        $_.testName -in @('Test_SdkPartitionKeys_DistinguishMissingAndNull', 'Test_StreamPartitionKeyMismatch_IsRejected')
    })
    foreach ($result in $excludedResults) {
        $result.outcome = 'NotExecuted'
        $result.Output.ErrorInfo.Message = '[vNext missing/null capability] Synthetic approved exclusion.'
    }
    $preview.Save([System.IO.Path]::GetFullPath($previewFixture))
    & "$PSScriptRoot/Assert-CosmosIntegrationResults.ps1" -Path $previewFixture -EmulatorVariant vnext
    Assert-GateRejects $previewFixture
    $excludedResults[0].Output.ErrorInfo.Message = 'Unexpected skip reason.'
    $preview.Save([System.IO.Path]::GetFullPath($previewFixture))
    Assert-GateRejects $previewFixture -EmulatorVariant vnext
    $excludedResults[0].Output.ErrorInfo.Message = '[vNext missing/null capability] Synthetic approved exclusion.'
    $excludedResults[0].outcome = 'Failed'
    $preview.Save([System.IO.Path]::GetFullPath($previewFixture))
    Assert-GateRejects $previewFixture -EmulatorVariant vnext
    $excludedResults[0].outcome = 'NotExecuted'
    ($preview.TestRun.Results.UnitTestResult | Where-Object testName -eq 'Test_PartitionKeys_RoundTripAndDelete').outcome = 'NotExecuted'
    $preview.Save([System.IO.Path]::GetFullPath($previewFixture))
    Assert-GateRejects $previewFixture -EmulatorVariant vnext

    $null = $trx.TestRun.Results.RemoveChild($results[0])
    $trx.Save([System.IO.Path]::GetFullPath($fixture))
    Assert-GateRejects $fixture
    $null = $trx.TestRun.Results.AppendChild($results[1].CloneNode($true))
    $trx.Save([System.IO.Path]::GetFullPath($fixture))
    Assert-GateRejects $fixture
    $trx.TestRun.Results.RemoveAll()
    $trx.Save([System.IO.Path]::GetFullPath($fixture))
    Assert-GateRejects $fixture
    Assert-GateRejects (Join-Path $ResultsDirectory ([guid]::NewGuid().ToString() + '.trx'))
    Write-Host 'Cosmos integration failure guards passed.'
} finally {
    $env:Cosmos_Endpoint = $originalEndpoint
    $env:Cosmos_Key = $originalKey
    $env:GITHUB_STEP_SUMMARY = $originalSummary
    $env:Cosmos_EmulatorVariant = $originalVariant
}

# The last dotnet invocation intentionally failed; do not return its exit code to CI.
exit 0
