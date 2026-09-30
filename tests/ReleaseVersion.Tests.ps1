[CmdletBinding()]
param(
    [string] $ExpectedVersion = '1.1.1'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

function Assert-Equal {
    param(
        [object] $Actual,
        [object] $Expected,
        [string] $Label
    )

    if ($Actual -ne $Expected) {
        throw "$Label mismatch: expected '$Expected', found '$Actual'."
    }
}

$manifestPath = Join-Path $repoRoot 'manifest.json'
$projectPath = Join-Path $repoRoot 'src\VoidCrestMovesetCompat\VoidCrestMovesetCompat.csproj'
$pluginPath = Join-Path $repoRoot 'src\VoidCrestMovesetCompat\Plugin.cs'
$assemblyInfoPath = Join-Path $repoRoot 'src\VoidCrestMovesetCompat\Properties\AssemblyInfo.cs'
$changelogPath = Join-Path $repoRoot 'CHANGELOG.md'

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
[xml] $project = Get-Content -LiteralPath $projectPath -Raw
$pluginSource = Get-Content -LiteralPath $pluginPath -Raw
$assemblyInfo = Get-Content -LiteralPath $assemblyInfoPath -Raw
$changelog = Get-Content -LiteralPath $changelogPath -Raw
$readme = Get-Content -LiteralPath (Join-Path $repoRoot 'README.md') -Raw

Assert-Equal $manifest.version_number $ExpectedVersion 'Root manifest version'
Assert-Equal $project.Project.PropertyGroup.Version $ExpectedVersion 'Project version'

$pluginVersionMatch = [regex]::Match(
    $pluginSource,
    '\[BepInPlugin\([^,]+,\s*[^,]+,\s*"(?<version>[^"]+)"\)\]')
if (-not $pluginVersionMatch.Success) {
    throw 'BepInPlugin version declaration was not found.'
}
Assert-Equal $pluginVersionMatch.Groups['version'].Value $ExpectedVersion 'BepInPlugin version'

$assemblyVersionMatch = [regex]::Match(
    $assemblyInfo,
    'AssemblyVersion\("(?<version>[^"]+)"\)')
if (-not $assemblyVersionMatch.Success) {
    throw 'AssemblyVersion declaration was not found.'
}
Assert-Equal $assemblyVersionMatch.Groups['version'].Value "$ExpectedVersion.0" 'Assembly version'

if ($changelog -notmatch ('Target version:\s*`' + [regex]::Escape($ExpectedVersion) + '`')) {
    throw "CHANGELOG.md does not identify $ExpectedVersion as the unpublished target."
}

foreach ($forgottenStatement in @(
    'packaged locally, unpublished',
    'This release candidate is packaged locally but has not been published',
    'Last published compatibility version'
)) {
    if ($readme -match [regex]::Escape($forgottenStatement) -or
        $changelog -match [regex]::Escape($forgottenStatement)) {
        throw "Packaged documentation still contains a forgotten planning statement: $forgottenStatement"
    }
}

# Guard the already-published 1.0.0 package against accidental mutation.
$publishedRoot = Join-Path $repoRoot 'release\ManthisCo-Void_Crest_Compatibility-1.0.0'
$publishedManifest = Get-Content -LiteralPath (Join-Path $publishedRoot 'manifest.json') -Raw | ConvertFrom-Json
Assert-Equal $publishedManifest.version_number '1.0.0' 'Published manifest version'

$publishedDll = Join-Path $publishedRoot 'VoidCrestMovesetCompat.dll'
$expectedPublishedHash = '2FF64872574A228356D4CB2398C51F59FD7DB4ADC189C948ECD11068AD03C05E'
Assert-Equal (Get-FileHash -LiteralPath $publishedDll -Algorithm SHA256).Hash $expectedPublishedHash 'Published DLL SHA-256'

Add-Type -AssemblyName System.IO.Compression.FileSystem
$publishedZip = Join-Path $repoRoot 'release\ManthisCo-Void_Crest_Compatibility-1.0.0.zip'
$archive = [IO.Compression.ZipFile]::OpenRead($publishedZip)
try {
    $entry = $archive.Entries |
        Where-Object Name -eq 'VoidCrestMovesetCompat.dll' |
        Select-Object -First 1
    if ($null -eq $entry) {
        throw 'Published ZIP does not contain VoidCrestMovesetCompat.dll.'
    }

    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $stream = $entry.Open()
        try {
            $zipHash = ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '')
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $sha.Dispose()
    }
}
finally {
    $archive.Dispose()
}
Assert-Equal $zipHash $expectedPublishedHash 'Published ZIP DLL SHA-256'

Write-Host "Release version gates passed for unpublished $ExpectedVersion; published 1.0.0 remains immutable."
