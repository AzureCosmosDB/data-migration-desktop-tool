$ErrorActionPreference = 'Stop'
$expected = '[{"id":"one","value":42,"nullable":null},{"id":"two","value":-1,"active":true}]'
$guard = Join-Path $PSScriptRoot 'Assert-JsonRoundTrip.ps1'

& $guard -ExpectedJson $expected -ActualJson $expected
& $guard -ExpectedJson $expected -ActualJson @'
[
  { "nullable": null, "value": 42, "id": "one" },
  { "active": true, "id": "two", "value": -1 }
]
'@

$invalidRecords = [ordered]@{
    'changed ID case' = $expected.Replace('"one"', '"ONE"')
    'numeric string' = $expected.Replace('"value":42', '"value":"42"')
    'negative numeric string' = $expected.Replace('"value":-1', '"value":"-1"')
    'boolean string' = $expected.Replace('"active":true', '"active":"true"')
    'missing null property' = $expected.Replace(',"nullable":null', '')
    'changed property case' = $expected.Replace('"nullable"', '"Nullable"')
    'changed null value' = $expected.Replace('"nullable":null', '"nullable":false')
    'changed number' = $expected.Replace('"value":42', '"value":43')
    'changed boolean' = $expected.Replace('"active":true', '"active":false')
    'extra property' = $expected.Replace('"nullable":null', '"nullable":null,"extra":1')
    'missing record' = '[{"id":"one","value":42,"nullable":null}]'
    'reordered records' = '[{"id":"two","value":-1,"active":true},{"id":"one","value":42,"nullable":null}]'
    'review example' = '[{"id":"ONE","value":"42"},{"id":"TWO","value":"-1","active":"true"}]'
}
foreach ($case in $invalidRecords.GetEnumerator()) {
    $rejected = $false
    try {
        & $guard -ExpectedJson $expected -ActualJson $case.Value
    } catch {
        if ($_.Exception.Message -cne 'Published JSON migration did not preserve the synthetic records.') {
            throw
        }
        $rejected = $true
    }
    if (-not $rejected) { throw "Round-trip guard accepted $($case.Key)." }
}
Write-Host "JSON round-trip guard passed: 2 valid cases and $($invalidRecords.Count) rejected mutations."
