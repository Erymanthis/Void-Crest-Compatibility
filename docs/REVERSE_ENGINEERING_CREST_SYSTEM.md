# Reverse Engineering Silksong's Crest System

## Purpose and confidence model

This document records the evidence needed to build a reusable crest-composition API. It is not
an assertion that every described behavior has already been exposed safely.

Each conclusion belongs to one of three levels:

1. **Verified structure** - present in inspected managed IL or a shipped mod binary.
2. **Implemented behavior** - a code path exists in Void Crest Compatibility 1.0.0.
3. **Observed behavior** - confirmed during gameplay with runtime identity, object, state, and
   lifecycle diagnostics.

The repository establishes levels 1 and 2. The 2026-08-18 test establishes level-3 evidence for
lifecycle execution, config ownership, and the exercised player-facing behavior, documented in
[`evidence/RUNTIME_TEST_2026-08-18.md`](evidence/RUNTIME_TEST_2026-08-18.md). The tester reported
that everything exercised functioned perfectly, explicitly including Hunter Void Focus. Stable
public adapters still require enumerated lifecycle, cleanup, and per-feature coverage rather than
relying on one successful compatibility run.

## Evidence baseline

| Artifact | Evidence | SHA-256 / revision |
|---|---|---|
| Silksong managed game code | Installed `Assembly-CSharp.dll` | `70713BB2B1F1B0C4C6B7007FA6A8446CF79818CB5E308C48C094C4D1DE7114BB` |
| Void Crest Compatibility | Installed 1.0.0 DLL | `2FF64872574A228356D4CB2398C51F59FD7DB4ADC189C948ECD11068AD03C05E` |
| Compatibility source | Git repository and v1.0.0 tag | `4a087dd2554e6350a3cfa0e6a5b80ffe46aa849c` |
| Void Crest dependency | Installed package 0.4.41; assembly identifies as 0.4.4 | `5F3D820FC32073608523698448D64F96446A0D8EB2DCD5219DBDD02F133B9815` |
| Needleforge dependency | Installed 0.9.0 DLL | `B276790B9EAC91E41C4698DF8979638EC845AACEAE6492754B9B9683AF2CFB6A` |

The complete generated structural inventory is in
[`evidence/VANILLA_CREST_INVENTORY.md`](evidence/VANILLA_CREST_INVENTORY.md). Regenerate it with
[`tools/Inspect-CrestSystem.ps1`](../tools/Inspect-CrestSystem.ps1) after a game update.

## Verified crest anatomy

A crest is not represented by one runtime object. The verified managed surface includes:

- `ToolCrest`, which supplies physical equipment identity and a `HeroControllerConfig`;
- `HeroControllerConfig`, which supplies timings, animation configuration, downslash mode, and
  other controller behavior;
- `HeroController.ConfigGroup`, which connects a config to active attack GameObjects;
- attack components and their child GameObjects;
- persistent crest state stored on `HeroController`;
- game methods that query `ToolBase.IsEquipped` or compare crest IDs;
- PlayMaker actions and FSM branches that perform their own crest checks;
- animation and audio systems that can query crest identity independently of an attack object.

The verified `ConfigGroup` object fields are:

| Family | Fields |
|---|---|
| Root/config | `Config`, `ActiveRoot` |
| Normal | `NormalSlashObject`, `AlternateSlashObject` |
| Up | `UpSlashObject`, `AltUpSlashObject` |
| Down | `DownSlashObject`, `AltDownSlashObject` |
| Wall | `WallSlashObject` |
| Dash | `DashStab`, `DashStabAlt` |
| Special attacks | `ChargeSlash`, `TauntSlash` |

This is the minimum first-party moveset surface. A cloned root can contain additional child
objects, but cloning the root alone does not reconnect a field whose source object lives outside
that hierarchy.

## Verified state ownership

`HeroController` contains four explicit crest-state structures:

| State | Verified fields | Primary consumers |
|---|---|---|
| `HunterUpgCrestStateInfo` | `CurrentMeterHits` | Focus thresholds and damage |
| `WandererCrestStateInfo` | queued critical, critical lock | critical-hit roll and buffer processing |
| `ReaperCrestStateInfo` | active flag, remaining duration | harvest state, payouts, hit processing |
| `WarriorCrestStateInfo` | active flag, effect/heal timers, heal count, last attack | Rage damage, healing, timing, animation |

These structures are state containers, not complete feature APIs. Code elsewhere owns their
transitions and side effects.

## Vanilla feature ownership

| Crest feature | Vanilla owner or evidence | Current compatibility strategy |
|---|---|---|
| Hunter progression and Focus | Hunter unlocks, `HunterUpgState`, `DamageEnemies.DoDamage` | Resolve evolution; proxy selected checks; read the real meter |
| Wanderer readiness and luck | `IsWandererLucky`, `WandererState`, damage roll followed by `GetLuckModifier` | Proxy readiness; substitute only the raw chance input |
| Reaper activation | `HeroController.BindCompleted` | Proxy Reaper identity for a normal Void Bind |
| Reaper duration and payout | `ReaperState`, `HeroController.Update`, `HealthManager.TakeDamage`, `GetReaperPayout` | Preserve vanilla state; reinterpret bundle pickup for Voidmass |
| Warrior Rage activation | `HeroController.BindCompleted` | Proxy Warrior identity for a normal Void Bind |
| Warrior Rage effects | `WarriorState`, damage, heal, controller timing, animation methods | Preserve vanilla state and selected call sites |
| Shaman Skill bonus | `HeroShamanRuneEffect.Refresh`, `SpellCrestRuneDamageMult` | Current plugin applies the vanilla constant to eligible Skills |
| Shaman grounded Bind | Spell Control FSM and `HeroController.FallCheck` | Enable the selected branch with Void-specific healing/invincibility rules |
| Witch recoil | `HeroController.Recoil`, `RecoilDown` | Proxy the actual Witch crest identity at the selected call sites |
| Attack audio | attack `AudioSource` objects and crest-sensitive FSMs | Clone audio settings and proxy selected audio FSM checks |

## Identity is a scoped dependency

The physical crest and a requested feature identity can coexist, but there is no verified global
"functional crest" field in the game. Void Crest Compatibility implements the concept by
rewriting checks inside selected methods and selected FSMs.

That implementation is useful evidence, but the public abstraction must be narrower:

```text
feature requests vanilla behavior
        -> identity router checks the feature's allowlist
        -> only the authorized call site sees the provider crest
        -> every other system sees the physical crest
```

The generated inventory found crest checks outside the current combat patch, including:

- `BindOrbHudFrame` presentation;
- `SilkSpool.BindCost`;
- `HeroController.AddSilk`;
- `ActiveCorpse.DoQueuedBurnEffects`;
- inventory curse/slot presentation;
- Shaman rune refresh;
- several state consumers that do not perform an equipment query themselves.

Those are not automatically missing patches. Some must remain owned by the host crest. They do
prove that a global equipment proxy would combine unrelated behavior and cannot be the safe API.

## Transfer modes

Every feature exposed by the framework must declare one transfer mode:

| Mode | Ownership | Typical use |
|---|---|---|
| Reference | Vanilla retains object/state ownership | progression assets and controller state |
| Clone | Host owns a runtime copy | attack GameObjects and mutable audio sources |
| Proxy | Vanilla code is invoked through an identity/call-site adapter | Rage, harvest, luck, recoil |
| Custom | Framework or mod implements the feature contract | altered resources or hybrid behavior |

Mixing modes without an ownership declaration is unsafe. In particular, cloned objects must not
retain references to a source hierarchy that can be disabled, pooled, or destroyed independently.

## Lifecycle requirements

A stable composition has to survive:

- `HeroController.Start` and initial config-group creation;
- `ResetAllCrestState`;
- `UpdateConfig` and `SetConfigGroup`;
- bench-gated selection changes;
- scenes that pool or rebuild attack and resource objects;
- unequipping the host crest;
- plugin unload or save exit.

The current compatibility plugin patches the first three areas, but its switch path is not a true
transaction. It destroys the old root before all later steps succeed and restores only the active
crest name on failure. The framework must keep a reversible snapshot until activation and
verification complete.

The 2026-08-18 runtime test confirmed the source/target issue in the current lifecycle path.
`ForceResolvedBaseConfigGroup` selects the resolved vanilla provider group after the compatibility
config has been assigned. The log reported `CurrentConfigGroup=Shaman,
UsesVoidCompatGroup=False`, repeatedly resolved provider groups after reset, and captured
Needleforge's direct-cross-crest-config warning. The framework must keep the physical host's
mutable config ownership internally consistent and verify object identity and provenance rather
than inferring ownership from names.

The same test showed that assigning `DownSlashFsmEdit` during a live bench switch happens after
Needleforge's one-time FSM edit pass and therefore has no effect. Stateful FSM features require an
early-registration phase or a reversible adapter that patches the instantiated FSM directly.

## Criteria for a stable feature adapter

A vanilla feature becomes stable only when all of the following exist:

1. Structural evidence and a version/hash baseline.
2. A registered feature descriptor with dependencies, conflicts, transfer mode, and provenance.
3. A reversible runtime installer.
4. A call-site allowlist for any identity proxy.
5. Activation and post-reset verification.
6. Cleanup and rollback verification.
7. A gameplay trace covering activation, normal use, reset, switch, unequip, and re-equip.

Until then, the feature is experimental even if it works in Void Crest Compatibility.
