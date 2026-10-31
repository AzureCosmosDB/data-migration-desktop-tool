param(
    [Parameter(Mandatory)]
    [string] $ExpectedJson,
    [Parameter(Mandatory)]
    [string] $ActualJson
)

$ErrorActionPreference = 'Stop'
$expected = [System.Text.Json.Nodes.JsonNode]::Parse($ExpectedJson)
$actual = [System.Text.Json.Nodes.JsonNode]::Parse($ActualJson)
if (-not [System.Text.Json.Nodes.JsonNode]::DeepEquals($expected, $actual)) {
    throw 'Published JSON migration did not preserve the synthetic records.'
}
