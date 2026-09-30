[CmdletBinding()]
param(
    [string] $AssemblyPath = "$PSScriptRoot\..\src\CrestComponentLab\bin\Release\net472\CrestComponentLab.dll",
    [string] $MonoCecil = "$env:APPDATA\Thunderstore Mod Manager\DataFolder\HollowKnightSilksong\profiles\Void TESTING ONLY\BepInEx\core\Mono.Cecil.dll"
)

$ErrorActionPreference = 'Stop'
$assemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
$cecilPath = (Resolve-Path -LiteralPath $MonoCecil).Path
Add-Type -Path $cecilPath

$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($assemblyPath)
$references = @($assembly.MainModule.AssemblyReferences | ForEach-Object Name)

if ('Needleforge' -notin $references) {
    throw 'CrestComponentLab must reference Needleforge.'
}

$forbiddenReferences = @($references | Where-Object { $_ -match 'VoidCrest|Void_Crest' })
if ($forbiddenReferences.Count -gt 0) {
    throw "Forbidden Void Crest references: $($forbiddenReferences -join ', ')"
}

$forbiddenStrings = [System.Collections.Generic.List[string]]::new()
foreach ($type in $assembly.MainModule.Types) {
    foreach ($method in $type.Methods) {
        if (-not $method.HasBody) {
            continue
        }

        foreach ($instruction in $method.Body.Instructions) {
            if ($instruction.Operand -is [string] -and
                $instruction.Operand -match 'VoidCrest|Void Crest|Void_Crest') {
                $forbiddenStrings.Add("$($type.FullName).$($method.Name): $($instruction.Operand)")
            }
        }
    }
}

if ($forbiddenStrings.Count -gt 0) {
    throw "Forbidden Void Crest implementation strings:`n$($forbiddenStrings -join "`n")"
}

$requiredTypes = @(
    'CrestComponentLab.Plugin',
    'CrestComponentLab.ComponentSource',
    'CrestComponentLab.CompositionRecipe',
    'CrestComponentLab.InertAttack',
    'CrestComponentLab.InertDownAttack',
    'CrestComponentLab.InertDashAttack',
    'CrestComponentLab.InertChargedAttack',
    'CrestComponentLab.ProviderAttack',
    'CrestComponentLab.ProviderDashAttack',
    'CrestComponentLab.ProviderChargedAttack',
    'CrestComponentLab.BindRouter',
    'CrestComponentLab.InertCoreAttackGatePatch'
)

$typeNames = @($assembly.MainModule.Types | ForEach-Object FullName)
foreach ($requiredType in $requiredTypes) {
    if ($requiredType -notin $typeNames) {
        throw "Required type is missing: $requiredType"
    }
}

$sourceEnum = $assembly.MainModule.Types |
    Where-Object FullName -eq 'CrestComponentLab.ComponentSource' |
    Select-Object -First 1
$enumNames = @($sourceEnum.Fields | Where-Object IsStatic | ForEach-Object Name)
foreach ($requiredValue in @('None', 'Hunter', 'Wanderer', 'Reaper', 'Architect', 'Shaman', 'Beast', 'Witch', 'Cloakless')) {
    if ($requiredValue -notin $enumNames) {
        throw "ComponentSource is missing: $requiredValue"
    }
}

foreach ($retiredValue in @('Toolmaster', 'Spell', 'Warrior', 'Cursed')) {
    if ($retiredValue -in $enumNames) {
        throw "ComponentSource still exposes retired/internal name: $retiredValue"
    }
}

Write-Output "PASS: clean-room references, inert gates, bench-recipe composition types, and provider enum verified."
Write-Output "Assembly: $assemblyPath"
Write-Output "SHA256: $((Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA256).Hash)"
