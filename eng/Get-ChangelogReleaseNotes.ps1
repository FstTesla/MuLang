param(
    [Parameter(Mandatory)]
    [string] $Path,

    [Parameter(Mandatory)]
    [string] $Version,

    [switch] $AllowMissing
)

$ErrorActionPreference = 'Stop'
$changelog = [System.IO.File]::ReadAllText($Path)
$escapedVersion = [regex]::Escape($Version)
$pattern =
    '(?ms)^## `(?<base>[^`\r\n]+)` → `' +
    $escapedVersion +
    '` - [^\r\n]+\r?\n\r?\n(?<notes>.*?)(?=^## |\z)'
$matches = [regex]::Matches($changelog, $pattern)

if ($matches.Count -eq 0)
{
    if ($AllowMissing)
    {
        return
    }

    throw "'$Path' does not contain release notes for version '$Version'."
}

if ($matches.Count -gt 1)
{
    throw "'$Path' contains duplicate release notes for version '$Version'."
}

$notes = $matches[0].Groups['notes'].Value.Trim()

if ([string]::IsNullOrWhiteSpace($notes))
{
    throw "Release notes for version '$Version' in '$Path' are empty."
}

$notes
