$ErrorActionPreference = 'Stop'

function Assert-True {
    param([bool] $Condition, [string] $Message)
    if (-not $Condition) {
        throw "Assertion failed: $Message"
    }
}

$frameworkFiles = Get-ChildItem -LiteralPath "$PSScriptRoot\..\src\VoidCrestMovesetCompat\Framework\Experimental" -File -Filter '*.cs' |
    Select-Object -ExpandProperty FullName
Add-Type -Path $frameworkFiles -CompilerOptions '/langversion:latest','/nullable:enable'

$namespace = 'VoidCrestMovesetCompat.Framework.Experimental'
$registryType = [Type]::GetType("$namespace.CrestFeatureRegistry")
$catalogType = [Type]::GetType("$namespace.VanillaCrestFeatureCatalog")

$registry = [VoidCrestMovesetCompat.Framework.Experimental.VanillaCrestFeatureCatalog]::CreateRegistry()
Assert-True ($registry.Features.Count -gt 80) 'the vanilla catalog should register the complete attack surface and stateful features'

$hunterProgression = [VoidCrestMovesetCompat.Framework.Experimental.VanillaCrestFeatureCatalog]::FeatureId('Hunter', 'progression', 'evolution')
$hunterFocus = [VoidCrestMovesetCompat.Framework.Experimental.VanillaCrestFeatureCatalog]::FeatureId('Hunter', 'passive', 'focus')
$hunterNormal = [VoidCrestMovesetCompat.Framework.Experimental.VanillaCrestFeatureCatalog]::FeatureId('Hunter', 'attack', 'normal')

$validRecipe = [VoidCrestMovesetCompat.Framework.Experimental.CrestRecipeBuilder]::Create('void-hunter', 'Void').
    Use([VoidCrestMovesetCompat.Framework.Experimental.StandardCrestSlots]::NormalAttack, $hunterNormal).
    Add($hunterProgression).
    Add($hunterFocus).
    Build()
$validResult = [VoidCrestMovesetCompat.Framework.Experimental.CrestRecipeValidator]::Validate($validRecipe, $registry)
Assert-True $validResult.IsValid 'a Hunter recipe with its progression dependency should validate'

$missingDependencyRecipe = [VoidCrestMovesetCompat.Framework.Experimental.CrestRecipeBuilder]::Create('invalid-hunter', 'Void').
    Add($hunterFocus).
    Build()
$missingDependencyResult = [VoidCrestMovesetCompat.Framework.Experimental.CrestRecipeValidator]::Validate($missingDependencyRecipe, $registry)
Assert-True (-not $missingDependencyResult.IsValid) 'Hunter Focus without progression should be rejected'
Assert-True (($missingDependencyResult.Issues | Where-Object Code -eq 'missing-dependency').Count -eq 1) 'the missing dependency should be identified precisely'

$events = [System.Collections.Generic.List[string]]::new()
$transaction = [VoidCrestMovesetCompat.Framework.Experimental.CrestCompositionTransaction]::new('rollback-test')
$transaction.Add([VoidCrestMovesetCompat.Framework.Experimental.CrestTransactionStep]::new(
    'first',
    [Action] { $events.Add('first.apply') },
    [Action] { $events.Add('first.rollback') })) | Out-Null
$transaction.Add([VoidCrestMovesetCompat.Framework.Experimental.CrestTransactionStep]::new(
    'second',
    [Action] { $events.Add('second.apply'); throw 'expected failure' },
    [Action] { $events.Add('second.rollback') })) | Out-Null

$caught = $false
try {
    $transaction.Execute()
}
catch [VoidCrestMovesetCompat.Framework.Experimental.CrestCompositionException] {
    $caught = $true
    Assert-True ($_.Exception.FailedStep.Name -eq 'second') 'the failing step should be reported'
    Assert-True ($_.Exception.RollbackExceptions.Count -eq 0) 'rollback should complete cleanly'
}

Assert-True $caught 'the transaction should surface its controlled failure'
Assert-True (($events -join ',') -eq 'first.apply,second.apply,second.rollback,first.rollback') 'rollback should include the partially applied failing step and run in reverse order'

'Crest framework foundation tests passed.'
