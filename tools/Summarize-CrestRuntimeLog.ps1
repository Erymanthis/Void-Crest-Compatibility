[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $LogPath,

    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
$resolvedLog = (Resolve-Path -LiteralPath $LogPath).Path
$snapshotPattern = 'Crest runtime snapshot \[(?<reason>[^\]]+)\]: Physical=(?<physical>[^;]+); Base=(?<base>[^;]+); CurrentRole=(?<role>[^;]+); Current=(?<current>.*?); HostTarget=(?<target>.*?); ProviderSource=(?<provider>.*)$'
$snapshots = [System.Collections.Generic.List[object]]::new()
$relevantLines = [System.Collections.Generic.List[string]]::new()

foreach ($line in [IO.File]::ReadLines($resolvedLog)) {
    if ($line -match $snapshotPattern) {
        $snapshots.Add([ordered]@{
            reason = $Matches.reason
            physical = $Matches.physical
            base = $Matches.base
            role = $Matches.role
            current = $Matches.current
            target = $Matches.target
            provider = $Matches.provider
            originalLine = $line
        })
    }

    if ($line -match 'VoidCrestMovesetCompat|Crest runtime snapshot|Applied base crest|Post-apply state|configGroup was null|Failed.*Void|compatibility patch') {
        $relevantLines.Add($line)
    }
}

$issues = [System.Collections.Generic.List[string]]::new()
if ($snapshots.Count -eq 0) {
    $issues.Add('No crest runtime snapshots were found. Confirm that the instrumented build was loaded and disk logging was enabled.')
}

foreach ($snapshot in $snapshots) {
    if ($snapshot.current -eq '<null>') {
        $issues.Add("[$($snapshot.reason)] CurrentConfigGroup was null for base '$($snapshot.base)'.")
    }
    if ($snapshot.target -eq '<null>') {
        $issues.Add("[$($snapshot.reason)] The transplanted host ConfigGroup was null for base '$($snapshot.base)'.")
    }
    if ($snapshot.role -eq 'other') {
        $issues.Add("[$($snapshot.reason)] The active ConfigGroup was neither the transplanted host nor resolved provider group for base '$($snapshot.base)'.")
    }
    if ($snapshot.reason -match '^update-config' -and $snapshot.role -eq 'provider-source') {
        $issues.Add("[$($snapshot.reason)] UpdateConfig left the vanilla provider group active for base '$($snapshot.base)'; verify Shadow Dash and the transplanted root in gameplay.")
    }
}

$issues = @($issues | Sort-Object -Unique)
$builder = [Text.StringBuilder]::new()
[void] $builder.AppendLine('# Crest Runtime Log Summary')
[void] $builder.AppendLine()
[void] $builder.AppendLine("- Source: ``$resolvedLog``")
[void] $builder.AppendLine("- Runtime snapshots: $($snapshots.Count)")
[void] $builder.AppendLine("- Relevant diagnostic lines: $($relevantLines.Count)")
[void] $builder.AppendLine()
[void] $builder.AppendLine('## Snapshot timeline')
[void] $builder.AppendLine()
[void] $builder.AppendLine('| Reason | Physical | Base | Current role | Current group | Host target | Provider source |')
[void] $builder.AppendLine('|---|---|---|---|---|---|---|')
foreach ($snapshot in $snapshots) {
    [void] $builder.AppendLine("| $($snapshot.reason) | $($snapshot.physical) | $($snapshot.base) | $($snapshot.role) | ``$($snapshot.current)`` | ``$($snapshot.target)`` | ``$($snapshot.provider)`` |")
}

[void] $builder.AppendLine()
[void] $builder.AppendLine('## Findings requiring review')
[void] $builder.AppendLine()
if ($issues.Count -eq 0) {
    [void] $builder.AppendLine('- No structural snapshot anomalies were detected.')
}
else {
    foreach ($issue in $issues) {
        [void] $builder.AppendLine("- $issue")
    }
}

[void] $builder.AppendLine()
[void] $builder.AppendLine('## Relevant raw lines')
[void] $builder.AppendLine()
[void] $builder.AppendLine('```text')
foreach ($line in $relevantLines) {
    [void] $builder.AppendLine($line)
}
[void] $builder.AppendLine('```')
$rendered = $builder.ToString()

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
