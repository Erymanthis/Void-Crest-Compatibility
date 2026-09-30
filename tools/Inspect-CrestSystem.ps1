[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $AssemblyCSharp,

    [Parameter(Mandatory = $true)]
    [string] $MonoCecil,

    [ValidateSet('Markdown', 'Json')]
    [string] $Format = 'Markdown',

    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
$assemblyPath = (Resolve-Path -LiteralPath $AssemblyCSharp).Path
$cecilPath = (Resolve-Path -LiteralPath $MonoCecil).Path
Add-Type -Path $cecilPath

$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($assemblyPath)
$allTypes = [System.Collections.Generic.List[object]]::new()

function Add-TypeTree {
    param([System.Collections.IEnumerable] $Types)

    foreach ($type in $Types) {
        $allTypes.Add($type)
        if ($type.HasNestedTypes) {
            Add-TypeTree -Types $type.NestedTypes
        }
    }
}

Add-TypeTree -Types $assembly.MainModule.Types

$hero = $allTypes | Where-Object FullName -eq 'HeroController' | Select-Object -First 1
if ($null -eq $hero) {
    throw "HeroController was not found in '$assemblyPath'."
}

$stateTypes = @($hero.NestedTypes |
    Where-Object Name -match 'CrestStateInfo$' |
    Sort-Object Name |
    ForEach-Object {
        [ordered]@{
            name = $_.Name
            fields = @($_.Fields | Where-Object { -not $_.IsStatic } | ForEach-Object {
                [ordered]@{ name = $_.Name; type = $_.FieldType.FullName }
            })
        }
    })

$configGroup = $hero.NestedTypes | Where-Object Name -eq 'ConfigGroup' | Select-Object -First 1
$configFields = @($configGroup.Fields |
    Where-Object { -not $_.IsStatic -and $_.Name -notmatch 'k__BackingField' } |
    ForEach-Object { [ordered]@{ name = $_.Name; type = $_.FieldType.FullName } })

$callSites = [System.Collections.Generic.List[object]]::new()
foreach ($type in $allTypes) {
    foreach ($method in $type.Methods) {
        if (-not $method.HasBody) {
            continue
        }

        $methodReferences = @($method.Body.Instructions |
            ForEach-Object Operand |
            Where-Object { $_ -is [Mono.Cecil.MethodReference] })
        $crestGetters = @($methodReferences |
            Where-Object {
                $_.DeclaringType.FullName -eq 'GlobalSettings.Gameplay' -and
                $_.Name -match '^get_.*Crest'
            } |
            ForEach-Object { $_.Name.Substring(4) } |
            Sort-Object -Unique)
        $equipChecks = @($methodReferences |
            Where-Object {
                $_.DeclaringType.FullName -eq 'ToolBase' -and $_.Name -eq 'get_IsEquipped'
            }).Count
        $stateReferences = @($method.Body.Instructions |
            ForEach-Object Operand |
            Where-Object { $null -ne $_ -and $_.ToString() -match 'CrestStateInfo' } |
            ForEach-Object ToString |
            Sort-Object -Unique)

        if ($crestGetters.Count -eq 0 -and $equipChecks -eq 0 -and $stateReferences.Count -eq 0) {
            continue
        }

        $callSites.Add([ordered]@{
            method = $method.FullName
            crestGetters = $crestGetters
            equippedChecks = $equipChecks
            stateReferences = $stateReferences
        })
    }
}

$inventory = [ordered]@{
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    assembly = [ordered]@{
        path = $assemblyPath
        name = $assembly.Name.Name
        version = $assembly.Name.Version.ToString()
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $assemblyPath).Hash
    }
    crestStateTypes = $stateTypes
    configGroupFields = $configFields
    crestDependentCallSites = @($callSites | Sort-Object { $_.method })
}

if ($Format -eq 'Json') {
    $rendered = $inventory | ConvertTo-Json -Depth 12
}
else {
    $builder = [Text.StringBuilder]::new()
    [void] $builder.AppendLine('# Generated Silksong Crest-System Inventory')
    [void] $builder.AppendLine()
    [void] $builder.AppendLine('This file is generated from managed IL. It records structural evidence, not observed gameplay behavior.')
    [void] $builder.AppendLine()
    [void] $builder.AppendLine("- Assembly: ``$($inventory.assembly.name)`` $($inventory.assembly.version)")
    [void] $builder.AppendLine("- SHA-256: ``$($inventory.assembly.sha256)``")
    [void] $builder.AppendLine("- Generated UTC: $($inventory.generatedUtc)")
    [void] $builder.AppendLine()
    [void] $builder.AppendLine('## Crest state structures')
    [void] $builder.AppendLine()
    foreach ($state in $stateTypes) {
        [void] $builder.AppendLine("### ``HeroController/$($state.name)``")
        [void] $builder.AppendLine()
        foreach ($field in $state.fields) {
            [void] $builder.AppendLine("- ``$($field.type) $($field.name)``")
        }
        [void] $builder.AppendLine()
    }

    [void] $builder.AppendLine('## HeroController.ConfigGroup fields')
    [void] $builder.AppendLine()
    foreach ($field in $configFields) {
        [void] $builder.AppendLine("- ``$($field.type) $($field.name)``")
    }
    [void] $builder.AppendLine()
    [void] $builder.AppendLine('## Crest-dependent managed call sites')
    [void] $builder.AppendLine()
    [void] $builder.AppendLine('| Method | Crest getters | `IsEquipped` calls | State references |')
    [void] $builder.AppendLine('|---|---|---:|---|')
    foreach ($site in $inventory.crestDependentCallSites) {
        $method = $site.method.Replace('|', '\|')
        $getters = ($site.crestGetters -join ', ').Replace('|', '\|')
        $states = ($site.stateReferences -join '<br>').Replace('|', '\|')
        [void] $builder.AppendLine("| ``$method`` | $getters | $($site.equippedChecks) | $states |")
    }
    $rendered = $builder.ToString()
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $rendered
}
else {
    $resolvedOutput = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
    $directory = Split-Path -Parent $resolvedOutput
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        [IO.Directory]::CreateDirectory($directory) | Out-Null
    }
    [IO.File]::WriteAllText($resolvedOutput, $rendered, [Text.UTF8Encoding]::new($false))
    Get-Item -LiteralPath $resolvedOutput
}
