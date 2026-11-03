param(
    [Parameter(Mandatory)]
    [string] $EventName,
    [string] $ReleaseTag = '',
    [string] $ManualTag = 'latest'
)

$ErrorActionPreference = 'Stop'
$tag = if ($EventName -eq 'release') { $ReleaseTag } else { $ManualTag }
if ($tag -cnotmatch '^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}\z') {
    throw 'The container tag must be a valid Docker tag.'
}
$versionPattern = '^v?(?<version>(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)(?:-(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*)?)\z'
if ($tag -cmatch $versionPattern) {
    $version = $Matches.version
    foreach ($component in ($version.Split('-')[0].Split('.'))) {
        if ([long]$component -gt 65534) { throw 'Application version components must not exceed 65534.' }
    }
    $version
} elseif ($EventName -eq 'release' -or $tag -match '^v?[0-9]') {
    throw 'Release tags must contain a three-part semantic version, optionally prefixed with v.'
} else {
    # Preserve the Dockerfile default for ordinary, non-versioned manual builds.
    '1.0.0'
}
