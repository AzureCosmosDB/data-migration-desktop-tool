$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'Get-ContainerBuildVersion.ps1'
foreach ($case in @(
    @('release', 'v4.0.0', 'latest', '4.0.0'),
    @('release', '4.0.0', 'latest', '4.0.0'),
    @('release', '4.0.0-rc.1', 'latest', '4.0.0-rc.1'),
    @('workflow_dispatch', '', '4.0.0', '4.0.0'),
    @('workflow_dispatch', '', 'v4.0.0', '4.0.0'),
    @('workflow_dispatch', '', 'latest', '1.0.0'),
    @('workflow_dispatch', '', 'development', '1.0.0')
)) {
    $actual = & $script -EventName $case[0] -ReleaseTag $case[1] -ManualTag $case[2]
    if ($actual -cne $case[3]) { throw "Unexpected build version for $($case -join ', '): $actual" }
}
foreach ($tag in @('4.0', '04.0.0', '4.0.0-01', '65535.0.0', '4.0.0;echo bad', "4.0.0`nBAD=value", "4.0.0`n", '$(echo bad)')) {
    $rejected = $false
    try { $null = & $script -EventName workflow_dispatch -ManualTag $tag } catch { $rejected = $true }
    if (-not $rejected) { throw "Invalid version was accepted: $tag" }
}
$rejected = $false
try { $null = & $script -EventName release -ReleaseTag latest } catch { $rejected = $true }
if (-not $rejected) { throw 'Non-versioned release was accepted.' }
Write-Host 'Container application version selection passed: 7 accepted and 9 rejected cases.'
