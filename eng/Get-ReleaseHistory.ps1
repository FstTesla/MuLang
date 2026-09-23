param(
    [Parameter(Mandatory)]
    [string] $Version
)

$ErrorActionPreference = 'Stop'
$versionPattern = '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-(alpha|beta|rc)\.([1-9][0-9]*))?$'

if ($Version -notmatch $versionPattern)
{
    throw "Version '$Version' is invalid."
}

$currentTag = "v$Version"
$tags = @(
    git tag --merged HEAD --list 'v*'
)

if ($LASTEXITCODE -ne 0)
{
    throw 'Could not read the release tag history.'
}

$versions = @(
    $tags |
        Where-Object { $_ -ne $currentTag } |
        ForEach-Object { $_.Substring(1) } |
        Where-Object { $_ -match $versionPattern }
)
$stableVersions = @(
    $versions |
        Where-Object { !$_.Contains('-', [StringComparison]::Ordinal) }
)

[PSCustomObject]@{
    HasPreviousRelease = $versions.Count -gt 0
    HasPreviousStable = $stableVersions.Count -gt 0
}
