param(
    [Parameter(Mandatory)]
    [string] $Path,

    [Parameter(Mandatory)]
    [string] $Version
)

$ErrorActionPreference = 'Stop'
$resolvedPath = Resolve-Path $Path
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedPath)

try
{
    $entries = @($archive.Entries)
    $entryNames = @($entries | ForEach-Object FullName)
    $requiredEntries = @(
        'extension.vsixmanifest'
        'Grammars/MuLang.tmLanguage.json'
        'LanguageServer/MuLang.LanguageServer.dll'
        'LanguageServer/MuLang.LanguageServer.exe'
        'LanguageServer/MuLang.LanguageServer.runtimeconfig.json'
        'MuLang-language-configuration.json'
        'MuLang.pkgdef'
        'MuLang.VisualStudio.dll'
    )
    $missingEntries = @(
        $requiredEntries |
            Where-Object { $_ -notin $entryNames }
    )

    if ($missingEntries.Count -gt 0)
    {
        throw "VSIX '$resolvedPath' is missing '$($missingEntries -join ', ')'."
    }

    $unexpectedSymbols = @(
        $entryNames |
            Where-Object { $_.EndsWith('.pdb', [StringComparison]::OrdinalIgnoreCase) }
    )

    if ($unexpectedSymbols.Count -gt 0)
    {
        throw "VSIX '$resolvedPath' contains debug symbols '$($unexpectedSymbols -join ', ')'."
    }

    $manifestEntry = $entries |
        Where-Object FullName -EQ 'extension.vsixmanifest' |
        Select-Object -First 1
    $reader = [System.IO.StreamReader]::new($manifestEntry.Open())

    try
    {
        [xml] $manifest = $reader.ReadToEnd()
    }
    finally
    {
        $reader.Dispose()
    }

    $namespaceManager = [System.Xml.XmlNamespaceManager]::new(
        $manifest.NameTable
    )
    $namespaceManager.AddNamespace(
        'vsix',
        'http://schemas.microsoft.com/developer/vsx-schema/2011'
    )
    $identity = $manifest.SelectSingleNode(
        '/vsix:PackageManifest/vsix:Metadata/vsix:Identity',
        $namespaceManager
    )

    if ($null -eq $identity)
    {
        throw "VSIX '$resolvedPath' does not contain an extension identity."
    }

    if ($identity.Version -ne $Version)
    {
        throw "VSIX '$resolvedPath' has version '$($identity.Version)' instead of '$Version'."
    }

}
finally
{
    $archive.Dispose()
}

"Verified VSIX '$resolvedPath' with version '$Version'."
