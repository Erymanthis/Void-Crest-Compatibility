$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$pluginPath = Join-Path $repoRoot 'src\VoidCrestMovesetCompat\Plugin.cs'
$source = Get-Content -LiteralPath $pluginPath -Raw

function Assert-Match {
    param(
        [string] $Text,
        [string] $Pattern,
        [string] $Message
    )

    if ($Text -notmatch $Pattern) {
        throw $Message
    }
}

$orderedMatch = [regex]::Match(
    $source,
    'private static readonly string\[\] OrderedBaseCrests\s*=\s*\{(?<body>.*?)\};',
    [Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $orderedMatch.Success) {
    throw 'OrderedBaseCrests was not found.'
}

$ordered = $orderedMatch.Groups['body'].Value
foreach ($name in @('Hunter', 'Wanderer', 'Reaper', 'Architect', 'Shaman', 'Beast', 'Witch', 'Cloakless')) {
    Assert-Match $ordered ('"' + [regex]::Escape($name) + '"') "Missing in-game Base Crest choice '$name'."
}

foreach ($legacyName in @('Toolmaster', 'Spell', 'Warrior', 'Cursed', 'Whip')) {
    if ($ordered -match ('"' + [regex]::Escape($legacyName) + '"')) {
        throw "Legacy/internal Base Crest name '$legacyName' is still exposed in the menu."
    }
}

$expectedMappings = @{
    Toolmaster = 'Architect'
    Spell = 'Shaman'
    Warrior = 'Beast'
    Cursed = 'Witch'
    Whip = 'Witch'
}
foreach ($entry in $expectedMappings.GetEnumerator()) {
    $pattern = '"' + [regex]::Escape($entry.Key) + '"\s*=>\s*"' + [regex]::Escape($entry.Value) + '"'
    Assert-Match $source $pattern "Missing legacy migration '$($entry.Key)' -> '$($entry.Value)'."
}

$expectedProviders = @{
    Architect = 'Toolmaster'
    Shaman = 'Spell'
    Beast = 'Warrior'
}
foreach ($entry in $expectedProviders.GetEnumerator()) {
    $pattern = '\["' + [regex]::Escape($entry.Key) + '"\]\s*=\s*"' + [regex]::Escape($entry.Value) + '"'
    Assert-Match $source $pattern "Missing provider mapping '$($entry.Key)' -> '$($entry.Value)'."
}

Assert-Match $source '\["Witch"\]\s*=\s*new\[\]\s*\{\s*"Witch",\s*"Whip"\s*\}' 'Witch does not route through the actual Witch/Whip identity aliases.'
Assert-Match $source 'VoidCrestBootstrapValues[\s\S]*?\["Witch"\]\s*=\s*"Cursed"' 'The private Void Crest 0.4.4 Witch bootstrap is missing.'
Assert-Match $source 'BaseCrestProviderNames[\s\S]*?private static readonly Dictionary<string, string> VoidCrestBootstrapValues' 'The runtime provider and base-mod bootstrap mappings are not separated.'
if ([regex]::Match($source, 'BaseCrestProviderNames\s*=.*?\};', [Text.RegularExpressions.RegexOptions]::Singleline).Value -match '\["Witch"\]\s*=\s*"Cursed"') {
    throw 'The functional Witch provider incorrectly resolves through Cursed.'
}
Assert-Match $source 'ChoiceModels\.ForNamedValues\(choices\)' 'The Base Crest menu does not separate stored provider IDs from player-facing names.'
Assert-Match $source 'NormalizeBaseCrestConfigForVoidCrest\(\);' 'Startup does not normalize display values back to Void Crest provider IDs.'

Assert-Match $source 'return DoesSelectedBaseRepresentCrest\(right\);' 'String crest checks do not resolve the renamed provider aliases.'
Assert-Match $source 'return DoesSelectedBaseRepresentCrest\(left\);' 'Reverse string crest checks do not resolve the renamed provider aliases.'

Write-Host 'Void Crest in-game Base Crest naming tests passed.'
