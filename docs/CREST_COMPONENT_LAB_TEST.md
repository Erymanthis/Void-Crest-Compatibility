# Crest Component Lab Test

## Purpose

This test separates five component sources on a new Needleforge crest:

1. core moveset;
2. dash slash;
3. charge slash;
4. normal Bind;
5. Up-Bind identity route.

The project is independent of Void Crest. Its only mod dependency is Needleforge 0.9.0, which
supplies BepInEx, FsmUtil, and SilksongPrepatcher transitively.

## Install

Build:

```powershell
& 'C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\ThirdParty\DotNet\10.0\win-x64\dotnet.exe' `
  build .\src\CrestComponentLab\CrestComponentLab.csproj -c Release `
  -p:ThunderstoreProfileDir='<clean-profile-path>'
```

Install after the clean profile exists:

```powershell
.\tools\Install-CrestComponentLab.ps1 -ProfilePath '<clean-profile-path>'
```

The installer refuses profiles containing Void Crest or Void Crest Compatibility unless the
explicit `-AllowAdditionalCrestMods` diagnostic override is supplied.

## Test 1: genuinely inert default

Leave every setting at `None`, equip `ComponentLab`, and verify:

- normal, alternate, up, down, wall, dash, charge, and Challenge/Taunt inputs produce no attack,
  damage, Hunter downslash, or attack animation;
- normal Bind remains Needleforge's standard custom-crest Bind;
- Up-Bind triggers no special event;
- the log reports all five sources as `None`.

This is the key control case. Private safety components exist only to satisfy `HeroController`'s
non-null assumptions; any visible Hunter attack or attack-related null-reference error is a failure.

## Test 2: one component at a time

For each change, verify that the config value updates immediately but behavior remains on the old
recipe until the gate is crossed:

1. edit away from a bench, test that behavior is unchanged, then sit at a bench;
2. edit while seated, test after leaving the bench;
3. confirm that the log contains `Queued recipe` followed by exactly one `Activated queued recipe`.

| Core | Dash | Charge | Bind | Special | Expected result |
|---|---|---|---|---|---|
| Hunter | None | None | None | None | Hunter regular/up/down/wall/Taunt only; no dash or charge attack |
| None | Hunter | None | None | None | Only Hunter dash attack exists |
| None | None | Hunter | None | None | Only Hunter charge attack exists |
| None | None | None | Reaper | None | Standard attack set remains inert; normal Bind routes Reaper identity |
| None | None | None | None | Beast | No normal provider route; Up-Bind calls Beast's internal `BindCompleted` path under Beast identity |

Repeat with the provider whose behavior is under investigation.

The lab preinstalls its unique-Bind FSM branch. Special Bind must switch through the same live,
bench-gated recipe path as the other four components without a title return or restart.

## Test 3: mixed composition

Use this deliberately mismatched recipe:

```text
Core Moveset = Architect
Dash Slash = Hunter
Charge Slash = Shaman
Normal Bind = Reaper
Special Bind = Beast
```

Verify that changing one setting does not replace any of the other four components. Capture the
entire `BepInEx/LogOutput.log` after a bench re-equip, scene transition, death/respawn, and return
to menu.

## Known question under test

Vanilla config groups expose one hero animation override library. The lab independently clones
attack GameObjects and overlays dash/charge controller fields, but it does not yet merge multiple
hero animation libraries. A mismatched recipe may therefore reveal which hero-level animation
clips are shared dependencies rather than true attack-owned components.
