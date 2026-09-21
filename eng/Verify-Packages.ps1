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
$packageIds = @(
    'MuLang.Core',
    'MuLang.IR',
    'MuLang.Compiler',
    'MuLang.Exporters.DotNet',
    'MuLang',
    'MuLang.StandardLibrary',
    'MuLang.StandardLibrary.DotNet'
)
$dependencies = @{
    'MuLang.Core' = @()
    'MuLang.IR' = @('MuLang.Core')
    'MuLang.Compiler' = @('MuLang.Core', 'MuLang.IR')
    'MuLang.Exporters.DotNet' = @('MuLang.Core', 'MuLang.IR')
    'MuLang' = @('MuLang.Compiler', 'MuLang.Core', 'MuLang.Exporters.DotNet')
    'MuLang.StandardLibrary' = @('MuLang.Core')
    'MuLang.StandardLibrary.DotNet' = @(
        'MuLang.Core',
        'MuLang.Exporters.DotNet',
        'MuLang.StandardLibrary'
    )
}
$descriptions = @{
    'MuLang.Core' = 'Core language types, environment contracts, profiles, diagnostics, and source abstractions for MuLang.'
    'MuLang.IR' = 'Runtime-independent intermediate representation and validation contracts for MuLang exporters.'
    'MuLang.Compiler' = 'Runtime-independent MuLang compiler that parses, validates, and lowers source code to portable IR.'
    'MuLang.Exporters.DotNet' = '.NET exporter, runtime context, and adapter contracts for executing portable MuLang IR.'
    'MuLang' = 'High-level .NET facade for compiling and executing MuLang expressions and programs.'
    'MuLang.StandardLibrary' = 'Optional runtime-independent standard-library declarations for MuLang environments.'
    'MuLang.StandardLibrary.DotNet' = 'Optional .NET implementations for MuLang standard-library declarations.'
}
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
    $arguments = @{
        PackagePath = Join-Path $PackageDirectory "$packageId.$Version.nupkg"
        SymbolPackagePath = Join-Path $PackageDirectory "$packageId.$Version.snupkg"
        Version = $Version
        PackageId = $packageId
        ExpectedDescription = $descriptions[$packageId]
        ExpectedPackageDependencies = $dependencies[$packageId]
        RepositoryRoot = $RepositoryRoot
        TestSourceLinkUrls = $TestSourceLinkUrls
        SkipConsumerTest = $packageId -ne 'MuLang'
        TestDirectPackages = $packageId -eq 'MuLang'
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
