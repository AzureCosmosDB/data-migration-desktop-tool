param(
    [Parameter(Mandatory)]
    [string] $Path
)

$ErrorActionPreference = 'Stop'
[xml] $trx = Get-Content -LiteralPath $Path -Raw
$namespace = [System.Xml.XmlNamespaceManager]::new($trx.NameTable)
$namespace.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
$className = 'Cosmos.DataTransfer.CosmosExtension.UnitTests.CosmosDataSourceExtensionTests'
$requiredTests = @(
    'Test_Json_RoundTrip_WithLongValues',
    'Test_FromJson_WithLongValues',
    'Test_StreamPartitionKeyMismatch_IsRejected',
    'Test_PartitionKeys_RoundTripAndDelete',
    'Test_NestedPartitionKeys_RoundTripAndDelete',
    'Test_HierarchicalPartitionKeys_RoundTripAndDelete'
)
$definitions = @($trx.SelectNodes('/t:TestRun/t:TestDefinitions/t:UnitTest', $namespace))
$results = @($trx.SelectNodes('/t:TestRun/t:Results/t:UnitTestResult', $namespace))
$failures = @()
$summary = @('| Integration test | Outcome |', '| --- | --- |')

foreach ($test in $requiredTests) {
    $definition = @($definitions | Where-Object {
        $_.TestMethod.className -eq $className -and $_.TestMethod.name -eq $test
    })
    $result = @($results | Where-Object { $_.testId -in $definition.id })
    if ($definition.Count -ne 1 -or $result.Count -ne 1) {
        $outcome = 'Missing or duplicate result'
    } else {
        $outcome = $result[0].outcome
    }
    $summary += "| $test | $outcome |"
    if ($outcome -ne 'Passed') {
        $failures += "${test}: $outcome"
    }
}

$summary | Write-Output
if ($env:GITHUB_STEP_SUMMARY) {
    $summary | Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY
}
if ($failures.Count -gt 0) {
    throw "Cosmos integration tests must execute and pass. $($failures -join '; ')"
}
