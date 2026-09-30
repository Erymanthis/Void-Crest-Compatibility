# Crest Runtime Test - 2026-08-18

## Scope

This report records what can be established from the BepInEx log produced by the
`Void TESTING ONLY` Thunderstore profile. It does not treat a logged code path as proof that its
visuals, timing, damage, or player-facing behavior felt correct.

## Runtime baseline

| Component | Observed runtime value |
|---|---|
| Game | Silksong `1.0.30000` |
| BepInEx | `5.4.23.4` |
| Needleforge | `0.9.0` |
| Void Crest package | Thunderstore record `0.4.41` |
| Void Crest assembly | Plugin reports `0.4.4` |
| Void Crest Compatibility | `1.0.0` |

The official compatibility assembly was loaded, not the instrumented development assembly.
Consequently the log contains no `CrestRuntimeSnapshot` object-identity records.

## Player-reported gameplay result

After completing the run, the tester reported that everything exercised functioned perfectly,
explicitly including Void Focus while using the Hunter base crest. This supplies player-facing
confirmation that the ownership and late-FSM warnings below did not cause an observed behavioral
failure in this run.

The report establishes a successful compatibility result for the exercised configuration. It
does not make direct cross-crest config ownership or late FSM mutation a safe contract for a
general-purpose composition framework: those techniques can still create order dependence,
cross-crest state coupling, and version-sensitive behavior.

## Runtime-confirmed findings

### 1. The current implementation aliases provider config ownership

Needleforge reported both of the following while initializing Void:

- `Moveset.HeroConfig` was not the same object as `Moveset.ConfigGroup.Config` and/or
  `ToolCrest.HeroConfig`.
- `ToolCrest.HeroConfig` was a direct reference to another crest's config.

The compatibility log then reported this post-apply state for the Spell provider:

```text
CurrentConfigGroup=Shaman, UsesVoidCompatGroup=False
```

Later lifecycle messages repeatedly resolved the active group to `Shaman`, `Default`, and
`Toolmaster` for Spell, Hunter, and Toolmaster respectively. This confirms that the current
lifecycle selects the vanilla provider group rather than retaining the transplanted Void host
group as the active group.

This is a working implementation technique, not a safe framework ownership contract. A public
composition API must clone mutable host config, keep the physical crest's config references
internally consistent, and use scoped identity routing for provider-only behavior.

### 2. Reset and bench lifecycle hooks are active

The log observed:

- repeated reapplication after `ResetAllCrestState`;
- queued selection changes while at a bench;
- Hunter applied on bench exit;
- Toolmaster applied on bench enter;
- Spell applied on a later bench exit.

The source and target attack hierarchies were logged for Spell, Hunter, and Toolmaster. Charge
Slash and Taunt Slash external objects were cloned, attack audio was copied, and Void Dash/Bind
audio was explicitly preserved.

These messages prove that the lifecycle and transplantation code paths executed. They do not by
themselves prove correct damage, hitboxes, animation timing, passive behavior, or audio playback.

### 3. Late FSM edit assignment is ineffective

During live Toolmaster and Spell changes, Needleforge reported:

```text
Crest FSM edits have already been run; setting DownSlashFsmEdit has no effect.
```

The warning includes `NormalizeTransplantedDownslashConfig` in its diagnostic stack. The method
continued and the moveset switch completed; this was not a thrown exception. However, any feature
that depends on installing an FSM edit delegate after Needleforge's one-time edit pass is not
reliably transferred.

A framework adapter must therefore do one of the following:

1. register the required edit before Needleforge initializes crest FSMs;
2. patch the already-instantiated PlayMaker FSM through a reversible live-FSM adapter; or
3. reject the recipe at activation time when neither route is available.

### 4. Startup ordering is temporarily inconsistent

Startup produced `configGroup was null!`, followed by successful Needleforge initialization and
compatibility transplantation. The compatibility plugin also timed out waiting 240 frames for the
live Void `ToolCrest` while retaining its data-layer artwork change.

The run recovered, but a stable adapter must represent `waiting for host creation` as an explicit
lifecycle state rather than mutating partial ownership and relying on later resets to converge.

## What this run did not establish

- Runtime object identity for host and provider groups after each lifecycle event.
- Independently enumerated results for every row and subcase in the gameplay validation matrix;
  the overall result was reported as successful, with Hunter Void Focus called out explicitly.
- Death/respawn cleanup or rollback correctness.
- Unequip/re-equip cleanup.
- Whether unpatched crest checks outside the current compatibility allowlist are observable in
  normal play.

Those items remain gameplay-verification requirements for stable feature adapters.
