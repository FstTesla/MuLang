param(
    [Parameter(Mandatory)]
    [string] $PackageDirectory,

    [Parameter(Mandatory)]
    [string] $Version,

    [string] $RepositoryRoot = (Join-Path $PSScriptRoot '..'),

    [string] $ExpectedRepositoryCommit,

    [switch] $TestSourceLinkUrls,

    [string] $FeedUrl,

    [string] $FeedUsername,

    [string] $FeedToken,

    [string] $GitHubOwner,

    [string] $GitHubRepository,

    [string] $GitHubToken,

    [ValidateSet('public', 'private', 'internal')]
    [string] $ExpectedGitHubVisibility
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath($RepositoryRoot)
$contractPath = Join-Path $root 'eng\PackageContract.psd1'
$contract = Import-PowerShellDataFile $contractPath
$packages = @($contract.Packages)
$packageIds = @($packages | ForEach-Object Id)
$expectedArtifacts = @(
    $packageIds |
        ForEach-Object {
            "$_.$Version.nupkg"
            "$_.$Version.snupkg"
        }
)
$actualArtifacts = @(
    Get-ChildItem $PackageDirectory -File |
        Where-Object Extension -In '.nupkg', '.snupkg' |
        ForEach-Object Name
)

if (Compare-Object ($actualArtifacts | Sort-Object) ($expectedArtifacts | Sort-Object))
{
    throw "Package artifact set differs. Expected '$($expectedArtifacts -join ', ')', got '$($actualArtifacts -join ', ')'."
}

foreach ($packageId in $packageIds)
{
    $packageContract = $packages |
        Where-Object Id -EQ $packageId |
        Select-Object -First 1
    $projectPath = Join-Path $root $packageContract.Project
    [xml] $project = [System.IO.File]::ReadAllText($projectPath)
    $description = @(
        $project.Project.PropertyGroup.PackageDescription |
            Where-Object { ![string]::IsNullOrWhiteSpace($_) }
    ) | Select-Object -Last 1

    if ([string]::IsNullOrWhiteSpace($description))
    {
        throw "Project '$projectPath' does not define PackageDescription."
    }

    $arguments = @{
        PackagePath = Join-Path $PackageDirectory "$packageId.$Version.nupkg"
        SymbolPackagePath = Join-Path $PackageDirectory "$packageId.$Version.snupkg"
        Version = $Version
        PackageId = $packageId
        ExpectedDescription = $description.Trim()
        ExpectedPackageDependencies = @($packageContract.Dependencies)
        RepositoryRoot = $RepositoryRoot
        TestSourceLinkUrls = $TestSourceLinkUrls
        SkipConsumerTest = $packageId -ne $contract.ConsumerPackages[0]
        ConsumerPackageIds = @($contract.ConsumerPackages)
    }

    foreach ($name in @(
        'ExpectedRepositoryCommit',
        'FeedUrl',
        'FeedUsername',
        'FeedToken',
        'GitHubOwner',
        'GitHubRepository',
        'GitHubToken',
        'ExpectedGitHubVisibility'
    ))
    {
        $value = Get-Variable -Name $name -ValueOnly

        if (![string]::IsNullOrEmpty($value))
        {
            $arguments[$name] = $value
        }
    }

    & (Join-Path $PSScriptRoot 'Verify-Package.ps1') @arguments
}
