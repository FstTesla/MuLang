param(
    [Parameter(Mandatory)]
    [string] $Version
)

$ErrorActionPreference = 'Stop'
$versionPattern = '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-(alpha|beta|rc)\.([1-9][0-9]*))?$'
$match = [regex]::Match($Version, $versionPattern)

if (!$match.Success)
{
    throw "Version '$Version' is not a supported MuLang release version."
}

$components = @(
    [int64]$match.Groups[1].Value
    [int64]$match.Groups[2].Value
    [int64]$match.Groups[3].Value
)

if ($components | Where-Object { $_ -gt 65534 })
{
    throw "Version '$Version' cannot be represented as a VSIX version."
}

$channel = $match.Groups[4].Value
$revision = if ([string]::IsNullOrEmpty($channel))
{
    65534
}
else
{
    [int64]$sequence = $match.Groups[5].Value

    if ($sequence -gt 19999)
    {
        throw "Prerelease sequence '$sequence' cannot be represented as a VSIX version."
    }

    switch ($channel)
    {
        'alpha' { $sequence }
        'beta' { 20000 + $sequence }
        'rc' { 40000 + $sequence }
    }
}

"$($components[0]).$($components[1]).$($components[2]).$revision"
