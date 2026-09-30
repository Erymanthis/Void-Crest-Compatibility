[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $Version,

    [string] $TestedDllPath = "$PSScriptRoot\..\src\VoidCrestMovesetCompat\bin\Release\net472\VoidCrestMovesetCompat.dll",

    [string] $OutputRoot = "$PSScriptRoot\..\release\updates"
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$testedDll = (Resolve-Path -LiteralPath $TestedDllPath).Path
$outputRootFull = [IO.Path]::GetFullPath($OutputRoot)
$versionRoot = Join-Path $outputRootFull $Version

function Assert-Equal {
    param([object] $Actual, [object] $Expected, [string] $Label)
    if ($Actual -ne $Expected) {
        throw "$Label mismatch: expected '$Expected', found '$Actual'."
    }
}

function Get-PngDimension {
    param([Parameter(Mandatory)][string] $Path)
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 24 -or
        $bytes[0] -ne 0x89 -or $bytes[1] -ne 0x50 -or $bytes[2] -ne 0x4E -or
        $bytes[3] -ne 0x47) {
        throw "Not a valid PNG header: $Path"
    }

    $width = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes, 16))
    $height = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes, 20))
    [pscustomobject]@{ Width = $width; Height = $height }
}

function New-ZipFromDirectory {
    param([Parameter(Mandatory)][string] $Source, [Parameter(Mandatory)][string] $Destination)
    $entries = @(Get-ChildItem -LiteralPath $Source -Force)
    if ($entries.Count -eq 0) {
        throw "Cannot create an empty archive from $Source."
    }
    Compress-Archive -Path (Join-Path $Source '*') -DestinationPath $Destination -CompressionLevel Optimal
}

$manifestPath = Join-Path $repoRoot 'manifest.json'
$projectPath = Join-Path $repoRoot 'src\VoidCrestMovesetCompat\VoidCrestMovesetCompat.csproj'
$assemblyInfoPath = Join-Path $repoRoot 'src\VoidCrestMovesetCompat\Properties\AssemblyInfo.cs'
$pluginPath = Join-Path $repoRoot 'src\VoidCrestMovesetCompat\Plugin.cs'
$releaseNotesPath = Join-Path $repoRoot "docs\releases\$Version.md"
$published100Dll = Join-Path $repoRoot 'release\ManthisCo-Void_Crest_Compatibility-1.0.0\VoidCrestMovesetCompat.dll'

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
[xml] $project = Get-Content -LiteralPath $projectPath -Raw
$assemblyInfo = Get-Content -LiteralPath $assemblyInfoPath -Raw
$pluginSource = Get-Content -LiteralPath $pluginPath -Raw

Assert-Equal $manifest.version_number $Version 'Manifest version'
Assert-Equal $project.Project.PropertyGroup.Version $Version 'Project version'
if ($manifest.description.Length -gt 250) {
    throw "Thunderstore description exceeds 250 characters: $($manifest.description.Length)."
}
foreach ($dependency in $manifest.dependencies) {
    if ($dependency -notmatch '^[^-]+-[^-]+-\d+\.\d+\.\d+$') {
        throw "Invalid Thunderstore dependency string: $dependency"
    }
}

$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($testedDll).Version.ToString()
Assert-Equal $assemblyVersion "$Version.0" 'Tested DLL assembly version'
$dllBytes = [IO.File]::ReadAllBytes($testedDll)
$dllAscii = [Text.Encoding]::ASCII.GetString($dllBytes)
$dllUnicode = [Text.Encoding]::Unicode.GetString($dllBytes)
foreach ($privateMarker in @('C:\Users\', 'OneDrive')) {
    if ($dllAscii.Contains($privateMarker) -or $dllUnicode.Contains($privateMarker)) {
        throw "Tested DLL contains a private build/tooling marker: $privateMarker"
    }
}
if ($assemblyInfo -notmatch ('AssemblyInformationalVersion\("' + [regex]::Escape($Version) + '"\)')) {
    throw 'Assembly informational version is not current.'
}
if ($pluginSource -notmatch ('BepInPlugin\([^\r\n]+"' + [regex]::Escape($Version) + '"\)')) {
    throw 'BepInPlugin version is not current.'
}
if (-not (Test-Path -LiteralPath $releaseNotesPath)) {
    throw "Canonical release notes are missing: $releaseNotesPath"
}

$iconPath = Join-Path $repoRoot 'icon.png'
$iconDimension = Get-PngDimension -Path $iconPath
Assert-Equal $iconDimension.Width 256 'Thunderstore icon width'
Assert-Equal $iconDimension.Height 256 'Thunderstore icon height'

$expected100Hash = '2FF64872574A228356D4CB2398C51F59FD7DB4ADC189C948ECD11068AD03C05E'
Assert-Equal (Get-FileHash -LiteralPath $published100Dll -Algorithm SHA256).Hash $expected100Hash 'Published 1.0.0 DLL SHA-256'

if (Test-Path -LiteralPath $versionRoot) {
    throw "Release output already exists; move or remove it before rebuilding: $versionRoot"
}

$thunderstoreRoot = Join-Path $versionRoot 'thunderstore'
$thunderstorePackage = Join-Path $thunderstoreRoot 'package'
$nexusRoot = Join-Path $versionRoot 'nexus'
$nexusPackage = Join-Path $nexusRoot 'package'
$nexusPlugin = Join-Path $nexusPackage 'BepInEx\plugins\ManthisCo-Void_Crest_Compatibility'
$githubRoot = Join-Path $versionRoot 'github'

New-Item -ItemType Directory -Path $thunderstorePackage, $nexusPlugin, $githubRoot -Force | Out-Null

Copy-Item -LiteralPath $manifestPath, (Join-Path $repoRoot 'README.md'),
    (Join-Path $repoRoot 'CHANGELOG.md'), $iconPath -Destination $thunderstorePackage
Copy-Item -LiteralPath $testedDll -Destination (Join-Path $thunderstorePackage 'VoidCrestMovesetCompat.dll')

Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md'), (Join-Path $repoRoot 'CHANGELOG.md'),
    (Join-Path $repoRoot 'distribution\nexus\INSTALL.md') -Destination $nexusPackage
Copy-Item -LiteralPath $testedDll -Destination (Join-Path $nexusPlugin 'VoidCrestMovesetCompat.dll')

$thunderstoreZipName = "ManthisCo-Void_Crest_Compatibility-$Version.zip"
$nexusZipName = "Void-Crest-Compatibility-$Version-Nexus.zip"
$thunderstoreZip = Join-Path $thunderstoreRoot $thunderstoreZipName
$nexusZip = Join-Path $nexusRoot $nexusZipName
New-ZipFromDirectory -Source $thunderstorePackage -Destination $thunderstoreZip
New-ZipFromDirectory -Source $nexusPackage -Destination $nexusZip

Copy-Item -LiteralPath $releaseNotesPath -Destination (Join-Path $githubRoot 'RELEASE_NOTES.md')
Copy-Item -LiteralPath $thunderstoreZip, $nexusZip -Destination $githubRoot

$githubThunderstoreZip = Join-Path $githubRoot $thunderstoreZipName
$githubNexusZip = Join-Path $githubRoot $nexusZipName
$checksums = @(
    "SHA256 ($thunderstoreZipName) = $((Get-FileHash -LiteralPath $githubThunderstoreZip -Algorithm SHA256).Hash)",
    "SHA256 ($nexusZipName) = $((Get-FileHash -LiteralPath $githubNexusZip -Algorithm SHA256).Hash)",
    "SHA256 (VoidCrestMovesetCompat.dll) = $((Get-FileHash -LiteralPath $testedDll -Algorithm SHA256).Hash)"
)
$checksums | Set-Content -LiteralPath (Join-Path $githubRoot 'SHA256SUMS.txt') -Encoding utf8

$plan = [ordered]@{
    version = $Version
    tag = "v$Version"
    title = "Void Crest Compatibility $Version"
    tested_dll_sha256 = (Get-FileHash -LiteralPath $testedDll -Algorithm SHA256).Hash
    github = [ordered]@{
        release_notes = 'github/RELEASE_NOTES.md'
        assets = @("github/$thunderstoreZipName", "github/$nexusZipName", 'github/SHA256SUMS.txt')
    }
    nexus = [ordered]@{
        archive = "nexus/$nexusZipName"
        category = 'Main file'
    }
    thunderstore = [ordered]@{
        archive = "thunderstore/$thunderstoreZipName"
        package = "ManthisCo-Void_Crest_Compatibility-$Version"
    }
    publication_status = 'prepared-only'
}
$plan | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $versionRoot 'release-plan.json') -Encoding utf8

Write-Host "Prepared unpublished release payloads in $versionRoot"
Write-Host "Tested DLL SHA-256: $($plan.tested_dll_sha256)"
