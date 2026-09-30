[CmdletBinding()]
param(
    [string] $Version = '1.1.1',
    [string] $ReleaseRoot = "$PSScriptRoot\..\release\updates"
)

$ErrorActionPreference = 'Stop'
$versionRoot = (Resolve-Path -LiteralPath (Join-Path $ReleaseRoot $Version)).Path
$plan = Get-Content -LiteralPath (Join-Path $versionRoot 'release-plan.json') -Raw | ConvertFrom-Json

if ($plan.version -ne $Version -or $plan.tag -ne "v$Version") {
    throw 'Release plan version or tag is incorrect.'
}
if ($plan.publication_status -ne 'prepared-only') {
    throw 'Release plan must remain prepared-only until publication is authorized.'
}

$thunderstoreZip = Join-Path $versionRoot $plan.thunderstore.archive
$nexusZip = Join-Path $versionRoot $plan.nexus.archive
$githubRoot = Join-Path $versionRoot 'github'

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-ZipEntryHash {
    param(
        [Parameter(Mandatory)][IO.Compression.ZipArchiveEntry] $Entry
    )

    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $stream = $Entry.Open()
        try {
            ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '')
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $sha.Dispose()
    }
}

$thunderstore = [IO.Compression.ZipFile]::OpenRead($thunderstoreZip)
try {
    $entries = @($thunderstore.Entries | ForEach-Object FullName)
    foreach ($required in @('manifest.json', 'README.md', 'icon.png', 'CHANGELOG.md', 'VoidCrestMovesetCompat.dll')) {
        if ($required -notin $entries) {
            throw "Thunderstore archive is missing root entry: $required"
        }
    }
    if ($entries | Where-Object { $_ -match '/' }) {
        throw 'Thunderstore payload unexpectedly contains nested paths.'
    }
    $dllEntries = @($thunderstore.Entries | Where-Object Name -like '*.dll')
    if ($dllEntries.Count -ne 1 -or $dllEntries[0].Name -ne 'VoidCrestMovesetCompat.dll') {
        throw 'Thunderstore archive contains an unexpected binary.'
    }
    if ((Get-ZipEntryHash -Entry $dllEntries[0]) -ne $plan.tested_dll_sha256) {
        throw 'Thunderstore archive does not contain the tested DLL.'
    }
}
finally {
    $thunderstore.Dispose()
}

$nexus = [IO.Compression.ZipFile]::OpenRead($nexusZip)
try {
    $entries = @($nexus.Entries | ForEach-Object { $_.FullName.Replace('\\', '/') })
    $requiredDll = 'BepInEx/plugins/ManthisCo-Void_Crest_Compatibility/VoidCrestMovesetCompat.dll'
    if ($requiredDll -notin $entries) {
        throw "Nexus archive is missing installable DLL path: $requiredDll"
    }
    foreach ($required in @('INSTALL.md', 'README.md', 'CHANGELOG.md')) {
        if ($required -notin $entries) {
            throw "Nexus archive is missing: $required"
        }
    }
    $dllEntries = @($nexus.Entries | Where-Object Name -like '*.dll')
    if ($dllEntries.Count -ne 1) {
        throw 'Nexus archive contains an unexpected binary.'
    }
    if ((Get-ZipEntryHash -Entry $dllEntries[0]) -ne $plan.tested_dll_sha256) {
        throw 'Nexus archive does not contain the tested DLL.'
    }
}
finally {
    $nexus.Dispose()
}

foreach ($asset in $plan.github.assets) {
    $name = Split-Path -Leaf $asset
    if (-not (Test-Path -LiteralPath (Join-Path $githubRoot $name))) {
        throw "GitHub release asset is missing: $name"
    }
}

$dllHash = $plan.tested_dll_sha256
$checksums = Get-Content -LiteralPath (Join-Path $githubRoot 'SHA256SUMS.txt') -Raw
if ($checksums -notmatch [regex]::Escape($dllHash)) {
    throw 'GitHub checksum file does not contain the tested DLL hash.'
}

Write-Host "Distribution structure tests passed for unpublished $Version payloads."
