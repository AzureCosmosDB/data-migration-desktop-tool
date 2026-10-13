param(
    [string] $ResultsDirectory = 'TestResults/guard'
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$project = 'Extensions/Cosmos/Cosmos.DataTransfer.CosmosExtension.UnitTests/'
$originalEndpoint = $env:Cosmos_Endpoint
$originalKey = $env:Cosmos_Key
$originalSummary = $env:GITHUB_STEP_SUMMARY
$emulatorKey = 'C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=='

function Assert-GateRejects([string] $Path) {
    $rejected = $false
    try {
        & "$PSScriptRoot/Assert-CosmosIntegrationResults.ps1" -Path $Path
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
    }

    # Exercise successful, missing, and duplicate result handling without an emulator.
    $fixture = Join-Path $ResultsDirectory 'synthetic.trx'
    foreach ($result in $results) {
        $result.outcome = 'Passed'
    }
    $trx.Save([System.IO.Path]::GetFullPath($fixture))
    & "$PSScriptRoot/Assert-CosmosIntegrationResults.ps1" -Path $fixture
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
}

# The last dotnet invocation intentionally failed; do not return its exit code to CI.
exit 0
