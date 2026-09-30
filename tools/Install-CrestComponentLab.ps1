[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ProfilePath,

    [string] $BuildDirectory = "$PSScriptRoot\..\src\CrestComponentLab\bin\Release\net472",

    [string] $BackupRoot = "$PSScriptRoot\..\release\backups",

    [switch] $AllowAdditionalCrestMods
)

$ErrorActionPreference = 'Stop'
$profile = [System.IO.Path]::GetFullPath($ProfilePath)
$build = [System.IO.Path]::GetFullPath($BuildDirectory)
$backupRootPath = [System.IO.Path]::GetFullPath($BackupRoot)
$plugins = [System.IO.Path]::GetFullPath((Join-Path $profile 'BepInEx\plugins'))
$needleforge = Join-Path $plugins 'Voidlings-Needleforge\Needleforge.dll'
$sourceDll = Join-Path $build 'CrestComponentLab.dll'
$sourcePdb = Join-Path $build 'CrestComponentLab.pdb'
$packageSource = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\src\CrestComponentLab'))
$target = [System.IO.Path]::GetFullPath((Join-Path $plugins 'ManthisCo-Crest_Component_Lab'))

if (-not $plugins.StartsWith($profile, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'The resolved plugins directory is outside the requested profile.'
}

if (-not (Test-Path -LiteralPath $plugins -PathType Container)) {
    throw "Profile plugins directory not found: $plugins"
}

if (-not (Test-Path -LiteralPath $needleforge -PathType Leaf)) {
    throw "Needleforge is not installed in this profile: $needleforge"
}

if (-not (Test-Path -LiteralPath $sourceDll -PathType Leaf)) {
    throw "Release build not found: $sourceDll"
}

$otherCrestDlls = @(Get-ChildItem -LiteralPath $plugins -Recurse -File -Filter '*.dll' |
    Where-Object {
        $_.FullName -notlike "$target*" -and
        ($_.Name -match 'VoidCrest|CrestMovesetCompat' -or $_.DirectoryName -match 'Void_Crest|Crest_Compatibility')
    })
if ($otherCrestDlls.Count -gt 0 -and -not $AllowAdditionalCrestMods) {
    throw "The profile is not clean. Remove these crest mods or pass -AllowAdditionalCrestMods:`n$($otherCrestDlls.FullName -join "`n")"
}

if (Test-Path -LiteralPath $target -PathType Container) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $backup = Join-Path $backupRootPath "CrestComponentLab-$stamp"
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    Get-ChildItem -LiteralPath $target -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $backup -Recurse -Force
    }
}

New-Item -ItemType Directory -Path $target -Force | Out-Null
Copy-Item -LiteralPath $sourceDll -Destination (Join-Path $target 'CrestComponentLab.dll') -Force
if (Test-Path -LiteralPath $sourcePdb -PathType Leaf) {
    Copy-Item -LiteralPath $sourcePdb -Destination (Join-Path $target 'CrestComponentLab.pdb') -Force
}
Copy-Item -LiteralPath (Join-Path $packageSource 'manifest.json') -Destination (Join-Path $target 'manifest.json') -Force
Copy-Item -LiteralPath (Join-Path $packageSource 'README.md') -Destination (Join-Path $target 'README.md') -Force

$sourceHash = (Get-FileHash -LiteralPath $sourceDll -Algorithm SHA256).Hash
$targetHash = (Get-FileHash -LiteralPath (Join-Path $target 'CrestComponentLab.dll') -Algorithm SHA256).Hash
if ($sourceHash -ne $targetHash) {
    throw "Installed hash mismatch. Expected $sourceHash, got $targetHash."
}

[PSCustomObject]@{
    Profile = $profile
    PackageDirectory = $target
    Needleforge = $needleforge
    SHA256 = $targetHash
} | Format-List
