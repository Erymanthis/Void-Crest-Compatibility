# Gameplay Validation Matrix

## Goal

Gameplay traces promote a feature from implemented to observed. Use the instrumented build from
this branch and retain the complete BepInEx `LogOutput.log` for each run.

The runtime snapshot distinguishes:

- `host-target` - the transplanted Void/custom ConfigGroup is active;
- `provider-source` - the vanilla provider ConfigGroup is active;
- `other` - neither expected group is active.

Object instance IDs make same-named configs and roots distinguishable.

## Core lifecycle sequence

Run this sequence for every supported Base Crest:

1. Launch into a save with Void equipped.
2. Record the initial attacks, Shadow Dash, normal Bind, and Up-Bind.
3. Exercise every available regular attack, Charge Slash, and Challenge/Taunt attack.
4. Activate and exercise the crest-specific passive or Bind behavior.
5. Change Base Crest away from a bench and rest at a bench.
6. Change Base Crest while sitting and leave the bench.
7. Unequip Void, use a vanilla crest, and re-equip Void.
8. Trigger a scene transition and death/respawn.
9. Exit normally so the disk log is complete.

For every transition, verify:

- physical crest remains `Void` while expected;
- the active config role and root are expected;
- Shadow Dash remains the host-owned dash;
- source vanilla roots are not destroyed or left active accidentally;
- old cloned roots are retired only after the new composition commits;
- no `configGroup was null`, missing-object, Harmony, FSM, or audio warnings occur.

## Feature checks

| Base | Required observations |
|---|---|
| Hunter | Correct unlocked evolution; meter gain; Focus 1/2 thresholds; eligible damage only; low-health coefficient; boss cap; tool/retaliation/Up-Bind exclusion |
| Wanderer | 9-Silk readiness; raw chance; one luck multiplication; forced/locked critical state; Dice behavior |
| Reaper | normal Bind activates harvest; timer; eligible enemy payout; bundle appearance; no Silk; two half-orbs per Voidmass; cap; pool restoration outside Void |
| Beast | normal Bind activates Rage; duration; damage; healing cap; hit extension; attack timing; Bounce Pod; growl/Challenge audio; Up-Bind exclusion |
| Shaman | Skill multiplier without lost hitboxes; grounded Bind; zero heal; eight recovery frames; no retaliation during recovery; Up-Bind preservation |
| Witch | Witch/Whip attack family; horizontal/down recoil; animation flag; Charge Slash; audio; no Witch Bind takeover |
| Architect | normal/alternate/up/down/wall/Charge/Challenge tree; Architect attack branching; host Bind and Shadow Dash |
| Cloakless | complete available attack tree; host Bind and Shadow Dash; no unintended passive activation |

## Recorded run: 2026-08-18

| Observation | Result | Evidence limit |
|---|---|---|
| Overall exercised behavior | Pass | Tester reported that everything exercised functioned perfectly; individual subcases were not separately enumerated. |
| Hunter Void Focus | Pass | Explicitly confirmed by the tester. |
| Bench changes: Spell, Hunter, Toolmaster | Executed | Confirmed by the BepInEx lifecycle log; player reported no behavioral failure. |
| Host/provider config ownership | Unsafe implementation detail | Needleforge reported direct cross-crest config aliasing even though gameplay succeeded. |
| Live DownSlash FSM edit assignment | Ineffective API call | Needleforge reported that its one-time FSM edit pass had already run; no player-facing failure was observed. |

See [`evidence/RUNTIME_TEST_2026-08-18.md`](evidence/RUNTIME_TEST_2026-08-18.md) for the complete
interpretation.

## Summarizing a log

```powershell
./tools/Summarize-CrestRuntimeLog.ps1 `
  -LogPath "<profile>/BepInEx/LogOutput.log" `
  -OutputPath ./tmp/crest-runtime-summary.md
```

The summarizer flags missing groups, unexpected group roles, and the unresolved case where
`UpdateConfig` leaves the provider group active. Its output is a triage aid; gameplay observations
remain authoritative for animation, audio, hitboxes, timing, and visual behavior.
