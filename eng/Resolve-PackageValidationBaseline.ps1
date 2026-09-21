param(
    [string] $CurrentVersion,

    [string] $Override,

    [Parameter(Mandatory)]
    [string] $GitHubToken,

    [string] $GitHubOwner = 'FstTesla',

    [string] $PackageId = 'MuLang'
)

$ErrorActionPreference = 'Stop'
$versionPattern = '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-(alpha|beta|rc)\.([1-9][0-9]*))?$'

function ConvertTo-VersionInfo
{
    param([string] $Value)

    $match = [regex]::Match($Value, $versionPattern)

    if (!$match.Success)
    {
        return $null
    }

    $channelRank = switch ($match.Groups[4].Value)
    {
        'alpha' { 0 }
        'beta' { 1 }
        'rc' { 2 }
        default { 3 }
    }
    $sequence = if ($match.Groups[5].Success)
    {
        [long]$match.Groups[5].Value
    }
    else
    {
        0
    }

    return [PSCustomObject]@{
        Text = $Value
        Major = [long]$match.Groups[1].Value
        Minor = [long]$match.Groups[2].Value
        Patch = [long]$match.Groups[3].Value
        ChannelRank = $channelRank
        Sequence = $sequence
    }
}

function Compare-VersionInfo
{
    param(
        [object] $Left,
        [object] $Right
    )

    foreach (
        $property in 'Major', 'Minor', 'Patch', 'ChannelRank', 'Sequence'
    )
    {
        if ($Left.$property -lt $Right.$property)
        {
            return -1
        }

        if ($Left.$property -gt $Right.$property)
        {
            return 1
        }
    }

    return 0
}

$normalizedOverride = $Override.Trim()

if (
    $normalizedOverride.Equals(
        'none',
        [StringComparison]::OrdinalIgnoreCase
    )
)
{
    return
}

$current = if ([string]::IsNullOrWhiteSpace($CurrentVersion))
{
    $null
}
else
{
    ConvertTo-VersionInfo $CurrentVersion
}

if (
    ![string]::IsNullOrWhiteSpace($CurrentVersion) -and
    $null -eq $current
)
{
    throw "Current version '$CurrentVersion' is invalid."
}

$headers = @{
    Accept = 'application/vnd.github+json'
    Authorization = "Bearer $GitHubToken"
    'X-GitHub-Api-Version' = '2022-11-28'
    'User-Agent' = 'MuLang-package-validation'
}
$published = [System.Collections.Generic.List[object]]::new()

for ($page = 1; ; $page++)
{
    $url =
        "https://api.github.com/users/$GitHubOwner/packages/nuget/$PackageId/versions?per_page=100&page=$page"

    try
    {
        [object[]] $items = Invoke-RestMethod -Uri $url -Headers $headers
    }
    catch
    {
        if (
            $_.Exception.Response.StatusCode -eq
            [System.Net.HttpStatusCode]::NotFound
        )
        {
            $items = @()
        }
        else
        {
            throw
        }
    }

    foreach ($item in $items)
    {
        $version = ConvertTo-VersionInfo $item.name

        if ($null -ne $version)
        {
            $published.Add($version)
        }
    }

    if ($items.Count -lt 100)
    {
        break
    }
}

if (![string]::IsNullOrWhiteSpace($normalizedOverride))
{
    $baseline = ConvertTo-VersionInfo $normalizedOverride

    if ($null -eq $baseline)
    {
        throw "Package validation baseline '$normalizedOverride' is invalid."
    }

    if (
        $null -ne $current -and
        (Compare-VersionInfo $baseline $current) -ge 0
    )
    {
        throw "Package validation baseline '$normalizedOverride' must precede '$CurrentVersion'."
    }

    if ($normalizedOverride -notin @($published | ForEach-Object Text))
    {
        throw "Package validation baseline '$normalizedOverride' is not published on GitHub Packages."
    }
}
else
{
    $baseline = $published |
        Where-Object {
            $null -eq $current -or
            (Compare-VersionInfo $_ $current) -lt 0
        } |
        Sort-Object Major, Minor, Patch, ChannelRank, Sequence |
        Select-Object -Last 1
}

if ($null -ne $baseline)
{
    $baseline.Text
}
