param(
    [Parameter(Mandatory)]
    [string] $PackagePath,

    [Parameter(Mandatory)]
    [string] $SymbolPackagePath,

    [Parameter(Mandatory)]
    [string] $Version,

    [string] $PackageId = 'MuLang',

    [Parameter(Mandatory)]
    [string] $ExpectedDescription,

    [string[]] $ExpectedPackageDependencies = @(),

    [string] $RepositoryRoot = (Join-Path $PSScriptRoot '..'),

    [string] $ExpectedRepositoryCommit,

    [switch] $TestSourceLinkUrls,

    [string] $FeedUrl,

    [string] $FeedUsername,

    [string] $FeedToken,

    [int] $FeedRestoreAttempts = 6,

    [string] $GitHubOwner,

    [string] $GitHubRepository,

    [string] $GitHubToken,

    [ValidateSet('public', 'private', 'internal')]
    [string] $ExpectedGitHubVisibility,

    [switch] $SkipConsumerTest,

    [switch] $TestDirectPackages
)

$ErrorActionPreference = 'Stop'
$package = Get-Item $PackagePath
$symbolPackage = Get-Item $SymbolPackagePath
$root = [System.IO.Path]::GetFullPath($RepositoryRoot)
$temporaryRoot = Join-Path (
    [System.IO.Path]::GetTempPath()
) "MuLang-package-verification-$([Guid]::NewGuid().ToString('N'))"

function Get-NuspecMetadata
{
    param([string] $Path)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)

    try
    {
        $entry = $archive.Entries |
            Where-Object FullName -EQ "$PackageId.nuspec" |
            Select-Object -First 1

        if ($null -eq $entry)
        {
            throw "Package '$Path' does not contain $PackageId.nuspec."
        }

        $reader = [System.IO.StreamReader]::new($entry.Open())

        try
        {
            [xml] $document = $reader.ReadToEnd()
        }
        finally
        {
            $reader.Dispose()
        }

        $namespace = [System.Xml.XmlNamespaceManager]::new($document.NameTable)
        $namespace.AddNamespace('n', $document.DocumentElement.NamespaceURI)

        return @{
            Document = $document
            Namespace = $namespace
        }
    }
    finally
    {
        $archive.Dispose()
    }
}

function Get-MetadataValue
{
    param(
        [hashtable] $Metadata,
        [string] $Name
    )

    $node = $Metadata.Document.SelectSingleNode(
        "/n:package/n:metadata/n:$Name",
        $Metadata.Namespace
    )

    if ($null -eq $node)
    {
        return $null
    }

    return $node.InnerText
}

function Assert-Equal
{
    param(
        [string] $Name,
        [AllowNull()]
        [object] $Actual,
        [AllowNull()]
        [object] $Expected
    )

    if ($Actual -cne $Expected)
    {
        throw "$Name differs. Expected '$Expected', got '$Actual'."
    }
}

function Assert-PackageDependencies
{
    param(
        [hashtable] $Metadata,
        [string[]] $Expected
    )

    $dependencyNodes = @(
        $Metadata.Document.SelectNodes(
            '/n:package/n:metadata/n:dependencies/n:group/n:dependency',
            $Metadata.Namespace
        )
    )
    $actual = @(
        $dependencyNodes |
            ForEach-Object id |
            Sort-Object
    )
    $expectedSorted = @($Expected | Sort-Object)

    if (Compare-Object $actual $expectedSorted)
    {
        throw "Package dependencies differ. Expected '$($expectedSorted -join ', ')', got '$($actual -join ', ')'."
    }

    foreach ($dependency in $dependencyNodes)
    {
        Assert-Equal "Package dependency '$($dependency.id)' version" `
            $dependency.version `
            $Version
    }
}

function Assert-PackageContents
{
    param(
        [string] $Path,
        [string[]] $RequiredEntries,
        [string[]] $RequiredAssemblies
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)

    try
    {
        $entries = @($archive.Entries.FullName)

        foreach ($requiredEntry in $RequiredEntries)
        {
            if ($requiredEntry -notin $entries)
            {
                throw "Package '$Path' is missing '$requiredEntry'."
            }
        }

        $assemblies = @($entries | Where-Object { $_ -like 'lib/*.dll' })

        if (Compare-Object $assemblies $RequiredAssemblies)
        {
            throw "Package '$Path' contains an unexpected assembly set."
        }

        $forbiddenEntries = @(
            $entries |
                Where-Object {
                    $_ -match '(^|/)(tests?|obj|bin)(/|$)' -or
                    $_ -match 'packages\.lock\.json$' -or
                    $_ -match '\.tmp\.md$' -or
                    $_ -match '\.cs$'
                }
        )

        if ($forbiddenEntries.Count -ne 0)
        {
            throw "Package '$Path' contains forbidden entries: $($forbiddenEntries -join ', ')."
        }
    }
    finally
    {
        $archive.Dispose()
    }
}

function Expand-Package
{
    param(
        [string] $Path,
        [string] $Destination
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($Path, $Destination)
}

function Assert-Png
{
    param([string] $Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)

    if ($bytes.Length -lt 24)
    {
        throw "'$Path' is not a valid PNG file."
    }

    $signature = [Convert]::ToHexString($bytes[0..7])
    Assert-Equal 'PNG signature' $signature '89504E470D0A1A0A'
    $width = [System.Net.IPAddress]::NetworkToHostOrder(
        [BitConverter]::ToInt32($bytes, 16)
    )
    $height = [System.Net.IPAddress]::NetworkToHostOrder(
        [BitConverter]::ToInt32($bytes, 20)
    )
    Assert-Equal 'Package icon width' $width 128
    Assert-Equal 'Package icon height' $height 128
}

function Invoke-ConsumerTest
{
    param(
        [string] $Source,
        [string] $SourceName,
        [string] $Username,
        [string] $Token,
        [int] $RestoreAttempts,
        [string] $WorkingDirectory,
        [switch] $DirectPackages
    )

    [System.IO.Directory]::CreateDirectory($WorkingDirectory) | Out-Null
    $projectPath = Join-Path $WorkingDirectory 'Consumer.csproj'
    $programPath = Join-Path $WorkingDirectory 'Program.cs'
    $configurationPath = Join-Path $WorkingDirectory 'NuGet.Config'
    $packagesPath = Join-Path $WorkingDirectory 'packages'
    $packageReferences = if ($DirectPackages)
    {
        @"
    <PackageReference Include="MuLang.Compiler" Version="$Version" />
    <PackageReference Include="MuLang.Exporters.DotNet" Version="$Version" />
"@
    }
    else
    {
        @"
    <PackageReference Include="MuLang" Version="$Version" />
"@
    }
    $project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
$packageReferences
  </ItemGroup>
</Project>
"@
    $program = if ($DirectPackages)
    {
        @'
using MuLang.Compiler;
using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Types;
using MuLang.Exporters.DotNet;

EnvironmentSchema environment = new EnvironmentBuilder()
    .AddGlobal("global.value", "value", TypeSymbols.Int)
    .Build();
CompilationResult compilation = MuLangCompiler.Compile(
    "value + 1",
    environment,
    CompilationMode.Expression,
    TypeSymbols.Int
);
DotNetExportResult export = DotNetExporter.Export(
    compilation.Program ?? throw new InvalidOperationException("Compilation failed."),
    environment
);
Func<DotNetRuntimeContext, object?> compiled = export.Delegate ??
    throw new InvalidOperationException("Export failed.");
DotNetRuntimeContext context = new (
    environment,
    [ new KeyValuePair<string, object?>("global.value", 41L) ],
    [ ]
);

return compiled(context) is 42L ? 0 : 1;
'@
    }
    else
    {
        @'
using MuLang;
using MuLang.Core.Environment;
using MuLang.Core.Types;
using MuLang.Exporters.DotNet;

EnvironmentSchema environment = new EnvironmentBuilder()
    .AddGlobal("global.value", "value", TypeSymbols.Int)
    .Build();
CompilationResult compilation = MuLangCompiler.CompileExpression(
    "value + 1",
    environment,
    TypeSymbols.Int
);
Func<DotNetRuntimeContext, object?> compiled = compilation.Delegate ??
    throw new InvalidOperationException("Compilation failed.");
DotNetRuntimeContext context = new (
    environment,
    [ new KeyValuePair<string, object?>("global.value", 41L) ],
    [ ]
);

return compiled(context) is 42L ? 0 : 1;
'@
    }
    $escapedSource = [System.Security.SecurityElement]::Escape($Source)
    $credentials = if ([string]::IsNullOrEmpty($Token))
    {
        ''
    }
    else
    {
        $escapedUsername = [System.Security.SecurityElement]::Escape($Username)
        $escapedToken = [System.Security.SecurityElement]::Escape($Token)
        @"
  <packageSourceCredentials>
    <$SourceName>
      <add key="Username" value="$escapedUsername" />
      <add key="ClearTextPassword" value="$escapedToken" />
    </$SourceName>
  </packageSourceCredentials>
"@
    }
    $configuration = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="$SourceName" value="$escapedSource" />
  </packageSources>
$credentials
</configuration>
"@

    [System.IO.File]::WriteAllText(
        $projectPath,
        $project,
        [System.Text.UTF8Encoding]::new($false)
    )
    [System.IO.File]::WriteAllText(
        $programPath,
        $program,
        [System.Text.UTF8Encoding]::new($false)
    )
    [System.IO.File]::WriteAllText(
        $configurationPath,
        $configuration,
        [System.Text.UTF8Encoding]::new($false)
    )

    $previousPackagesPath = $env:NUGET_PACKAGES
    $env:NUGET_PACKAGES = $packagesPath

    try
    {
        $restored = $false

        for ($attempt = 1; $attempt -le $RestoreAttempts; $attempt++)
        {
            & dotnet restore $projectPath `
                --configfile $configurationPath `
                --no-cache `
                --force-evaluate `
                --nologo `
                --verbosity minimal

            if ($LASTEXITCODE -eq 0)
            {
                $restored = $true
                break
            }

            if ($attempt -lt $RestoreAttempts)
            {
                Start-Sleep -Seconds 10
            }
        }

        if (!$restored)
        {
            throw "Could not restore MuLang $Version from '$Source'."
        }

        & dotnet run `
            --project $projectPath `
            --configuration Release `
            --no-restore `
            --nologo

        if ($LASTEXITCODE -ne 0)
        {
            throw "The consumer test failed for MuLang $Version from '$Source'."
        }
    }
    finally
    {
        $env:NUGET_PACKAGES = $previousPackagesPath
    }
}

function Assert-GitHubPackage
{
    if (
        [string]::IsNullOrEmpty($GitHubOwner) -or
        [string]::IsNullOrEmpty($GitHubRepository) -or
        [string]::IsNullOrEmpty($GitHubToken)
    )
    {
        return
    }

    $headers = @{
        Accept = 'application/vnd.github+json'
        Authorization = "Bearer $GitHubToken"
        'X-GitHub-Api-Version' = '2022-11-28'
        'User-Agent' = 'MuLang-package-verification'
    }
    $packageUrl =
        "https://api.github.com/users/$GitHubOwner/packages/nuget/$PackageId"
    $versionsUrl = "$packageUrl/versions?per_page=100"
    $packageMetadata = $null
    $versions = $null

    for ($attempt = 1; $attempt -le $FeedRestoreAttempts; $attempt++)
    {
        try
        {
            $packageMetadata = Invoke-RestMethod -Uri $packageUrl -Headers $headers
            $versions = Invoke-RestMethod -Uri $versionsUrl -Headers $headers
            break
        }
        catch
        {
            if ($attempt -eq $FeedRestoreAttempts)
            {
                throw
            }

            Start-Sleep -Seconds 10
        }
    }

    Assert-Equal 'GitHub package repository' `
        $packageMetadata.repository.full_name `
        "$GitHubOwner/$GitHubRepository"

    if (![string]::IsNullOrEmpty($ExpectedGitHubVisibility))
    {
        Assert-Equal 'GitHub package visibility' `
            $packageMetadata.visibility `
            $ExpectedGitHubVisibility
    }

    if ($Version -notin @($versions | ForEach-Object name))
    {
        throw "GitHub Packages does not contain $PackageId $Version."
    }

    Write-Output "GitHub package visibility: $($packageMetadata.visibility)"
}

try
{
    [System.IO.Directory]::CreateDirectory($temporaryRoot) | Out-Null
    $packageDirectory = Join-Path $temporaryRoot 'package'
    $symbolDirectory = Join-Path $temporaryRoot 'symbols'
    Assert-PackageContents $package.FullName `
        @(
            "$PackageId.nuspec",
            'LICENSE',
            'MuLang.png',
            'README.md',
            "lib/net10.0/$PackageId.dll",
            "lib/net10.0/$PackageId.xml"
        ) `
        @("lib/net10.0/$PackageId.dll")
    Assert-PackageContents $symbolPackage.FullName `
        @(
            "$PackageId.nuspec",
            "lib/net10.0/$PackageId.pdb"
        ) `
        @()
    Expand-Package $package.FullName $packageDirectory
    Expand-Package $symbolPackage.FullName $symbolDirectory

    if (Get-ChildItem $packageDirectory -Recurse -Filter '*.pdb')
    {
        throw 'The main package contains a PDB that belongs in the symbol package.'
    }

    $metadata = Get-NuspecMetadata $package.FullName
    Assert-Equal 'Package ID' (Get-MetadataValue $metadata 'id') $PackageId
    Assert-Equal 'Package version' (Get-MetadataValue $metadata 'version') $Version
    Assert-Equal 'Package title' (Get-MetadataValue $metadata 'title') $PackageId
    Assert-PackageDependencies $metadata $ExpectedPackageDependencies
    Assert-Equal 'Package authors' (Get-MetadataValue $metadata 'authors') 'FstTesla'
    $description = (
        (Get-MetadataValue $metadata 'description') -replace '\s+', ' '
    ).Trim()
    Assert-Equal 'Package description' `
        $description `
        $ExpectedDescription
    Assert-Equal 'Package icon' (Get-MetadataValue $metadata 'icon') 'MuLang.png'
    Assert-Equal 'Package readme' (Get-MetadataValue $metadata 'readme') 'README.md'
    Assert-Equal 'Package project URL' `
        (Get-MetadataValue $metadata 'projectUrl') `
        'https://github.com/FstTesla/MuLang'
    Assert-Equal 'Package copyright' `
        (Get-MetadataValue $metadata 'copyright') `
        'Copyright (c) 2026 Filippo Mineo'
    Assert-Equal 'Package tags' `
        (Get-MetadataValue $metadata 'tags') `
        'compiler dsl embedded-language expression-language static-typing type-checking dotnet micro-language'

    $license = $metadata.Document.SelectSingleNode(
        '/n:package/n:metadata/n:license',
        $metadata.Namespace
    )
    Assert-Equal 'Package license' $license.InnerText 'Apache-2.0'
    Assert-Equal 'Package license type' $license.GetAttribute('type') 'expression'
    $repository = $metadata.Document.SelectSingleNode(
        '/n:package/n:metadata/n:repository',
        $metadata.Namespace
    )
    Assert-Equal 'Repository type' $repository.GetAttribute('type') 'git'
    $repositoryUrl = $repository.GetAttribute('url').TrimEnd('/')

    if ($repositoryUrl.EndsWith('.git', [StringComparison]::Ordinal))
    {
        $repositoryUrl = $repositoryUrl.Substring(0, $repositoryUrl.Length - 4)
    }

    Assert-Equal 'Repository URL' `
        $repositoryUrl `
        'https://github.com/FstTesla/MuLang'

    if (![string]::IsNullOrEmpty($ExpectedRepositoryCommit))
    {
        Assert-Equal 'Repository commit' `
            $repository.GetAttribute('commit') `
            $ExpectedRepositoryCommit
    }

    $packagedLicense = [System.IO.File]::ReadAllText(
        (Join-Path $packageDirectory 'LICENSE')
    ).Replace("`r`n", "`n")
    $repositoryLicense = [System.IO.File]::ReadAllText(
        (Join-Path $root 'LICENSE')
    ).Replace("`r`n", "`n")
    Assert-Equal 'Packaged license' $packagedLicense $repositoryLicense
    Assert-Png (Join-Path $packageDirectory 'MuLang.png')
    $packagedReadme = [System.IO.File]::ReadAllText(
        (Join-Path $packageDirectory 'README.md')
    ).Replace("`r`n", "`n")
    $repositoryReadme = [System.IO.File]::ReadAllText(
        (Join-Path $root 'README.md')
    ).Replace("`r`n", "`n")
    Assert-Equal 'Packaged README' $packagedReadme $repositoryReadme
    [xml] $documentation = [System.IO.File]::ReadAllText(
        (Join-Path $packageDirectory "lib/net10.0/$PackageId.xml")
    )
    Assert-Equal 'XML documentation assembly' `
        $documentation.doc.assembly.name `
        $PackageId

    if (
        $PackageId -notin 'MuLang.StandardLibrary', 'MuLang.StandardLibrary.DotNet' -and
        @($documentation.doc.members.member).Count -eq 0
    )
    {
        throw 'XML documentation contains no members.'
    }

    $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName(
        (Join-Path $packageDirectory "lib/net10.0/$PackageId.dll")
    )
    $publicKeyToken = [Convert]::ToHexString(
        $assemblyName.GetPublicKeyToken()
    ).ToLowerInvariant()
    Assert-Equal 'Strong-name public key token' `
        $publicKeyToken `
        'cf9a0e9f02f7978b'

    $pdbPath = Join-Path $symbolDirectory "lib/net10.0/$PackageId.pdb"
    $symbolMetadata = Get-NuspecMetadata $symbolPackage.FullName
    Assert-Equal 'Symbol package ID' `
        (Get-MetadataValue $symbolMetadata 'id') `
        $PackageId
    Assert-Equal 'Symbol package version' `
        (Get-MetadataValue $symbolMetadata 'version') `
        $Version
    $sourceLinkJson = & dotnet tool run sourcelink print-json $pdbPath

    if ($LASTEXITCODE -ne 0)
    {
        throw 'Could not read Source Link data from the portable PDB.'
    }

    $sourceLinkText = $sourceLinkJson -join [Environment]::NewLine

    if (
        !$sourceLinkText.Contains(
            'https://raw.githubusercontent.com/FstTesla/MuLang/',
            [StringComparison]::Ordinal
        )
    )
    {
        throw 'The portable PDB does not contain the expected Source Link URL.'
    }

    if (
        ![string]::IsNullOrEmpty($ExpectedRepositoryCommit) -and
        !$sourceLinkText.Contains(
            $ExpectedRepositoryCommit,
            [StringComparison]::OrdinalIgnoreCase
        )
    )
    {
        throw 'The portable PDB does not reference the expected commit.'
    }

    if ($TestSourceLinkUrls)
    {
        & dotnet tool run sourcelink test $pdbPath

        if ($LASTEXITCODE -ne 0)
        {
            throw 'Source Link URL validation failed.'
        }
    }

    if (!$SkipConsumerTest)
    {
        Invoke-ConsumerTest `
            $package.DirectoryName `
            'local' `
            '' `
            '' `
            1 `
            (Join-Path $temporaryRoot 'local-consumer')

        if ($TestDirectPackages)
        {
            Invoke-ConsumerTest `
                $package.DirectoryName `
                'local' `
                '' `
                '' `
                1 `
                (Join-Path $temporaryRoot 'local-direct-consumer') `
                -DirectPackages
        }
    }

    if (
        !$SkipConsumerTest -and
        ![string]::IsNullOrEmpty($FeedUrl)
    )
    {
        if (
            [string]::IsNullOrEmpty($FeedUsername) -or
            [string]::IsNullOrEmpty($FeedToken)
        )
        {
            throw 'Feed username and token are required for published verification.'
        }

        Invoke-ConsumerTest `
            $FeedUrl `
            'published' `
            $FeedUsername `
            $FeedToken `
            $FeedRestoreAttempts `
            (Join-Path $temporaryRoot 'published-consumer')

        if ($TestDirectPackages)
        {
            Invoke-ConsumerTest `
                $FeedUrl `
                'published' `
                $FeedUsername `
                $FeedToken `
                $FeedRestoreAttempts `
                (Join-Path $temporaryRoot 'published-direct-consumer') `
                -DirectPackages
        }
    }

    Assert-GitHubPackage
    Write-Output "$PackageId $Version package verification succeeded."
}
finally
{
    if (Test-Path $temporaryRoot)
    {
        Remove-Item $temporaryRoot -Recurse -Force
    }
}
