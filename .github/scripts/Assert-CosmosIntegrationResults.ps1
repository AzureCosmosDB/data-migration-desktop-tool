param(
    [Parameter(Mandatory)]
    [string] $Path,
    [ValidateSet('current', 'vnext')]
    [string] $EmulatorVariant = 'current'
)

$ErrorActionPreference = 'Stop'
[xml] $trx = Get-Content -LiteralPath $Path -Raw
$namespace = [System.Xml.XmlNamespaceManager]::new($trx.NameTable)
$namespace.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
$className = 'Cosmos.DataTransfer.CosmosExtension.UnitTests.CosmosDataSourceExtensionTests'
$requiredTests = @(
    'Test_Json_RoundTrip_WithLongValues',
    'Test_FromJson_WithLongValues',
    'Test_SdkPartitionKeys_DistinguishMissingAndNull',
    'Test_StreamPartitionKeyMismatch_IsRejected',
    'Test_PartitionKeys_RoundTripAndDelete',
    'Test_NestedPartitionKeys_RoundTripAndDelete',
    'Test_HierarchicalPartitionKeys_RoundTripAndDelete'
)
$previewConformanceExclusions = @(
    'Test_SdkPartitionKeys_DistinguishMissingAndNull',
    'Test_StreamPartitionKeyMismatch_IsRejected'
)
$definitions = @($trx.SelectNodes('/t:TestRun/t:TestDefinitions/t:UnitTest', $namespace))
$results = @($trx.SelectNodes('/t:TestRun/t:Results/t:UnitTestResult', $namespace))
$failures = @()
$summary = @('| Integration test | Outcome | Required outcome |', '| --- | --- | --- |')

foreach ($test in $requiredTests) {
    $definition = @($definitions | Where-Object {
        $_.TestMethod.className -eq $className -and $_.TestMethod.name -eq $test
    })
    $result = @($results | Where-Object { $_.testId -in $definition.id })
    $excluded = $EmulatorVariant -eq 'vnext' -and $test -in $previewConformanceExclusions
    $expected = if ($excluded) { 'NotExecuted' } else { 'Passed' }
    if ($definition.Count -ne 1 -or $result.Count -ne 1) {
        $outcome = 'Missing or duplicate result'
    } else {
        $outcome = $result[0].outcome
        if ($excluded -and $outcome -eq 'NotExecuted' -and
            $result[0].Output.ErrorInfo.Message -notlike '*[[]vNext missing/null capability]*') {
            $outcome = 'Skipped without the approved capability reason'
        }
    }
    $summary += "| $test | $outcome | $expected |"
    if ($outcome -ne $expected) {
        $failures += "${test}: $outcome (expected $expected)"
    }
}

if ($EmulatorVariant -eq 'vnext') {
    $summary += ''
    $summary += 'vNext exclusions: undefined/null partition identity and mismatched-key rejection are unsupported (independently reproduced with regular SDK writes). Only the two named conformance tests may skip, with their explicit capability reason. Single/nested positive transfers still test every key value; only explicit-null documents use a separate ID. All other cases and request-capture regressions remain required.'
}
$summary | Write-Output
if ($env:GITHUB_STEP_SUMMARY) {
    $summary | Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY
}
if ($failures.Count -gt 0) {
    throw "Cosmos integration results do not meet the $EmulatorVariant capability requirements. $($failures -join '; ')"
}
