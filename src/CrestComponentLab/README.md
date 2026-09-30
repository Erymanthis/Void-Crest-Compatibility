# Crest Component Lab

An intentionally minimal custom crest for testing independent component composition with
Needleforge 0.9.0.

This is a clean-room implementation. It has no dependency on Void Crest or Void Crest
Compatibility and does not use their code, assets, names, resources, or mechanics.

## Default behavior

- Core moveset: `None` (regular/up/down/wall/alternate/Taunt execution is blocked)
- Dash Slash: `None`
- Charge Slash: `None` (nail charging disabled)
- Normal Bind: `None` (Needleforge's unmodified standard custom-crest Bind)
- Special Bind: `None` (no directional special Bind event)

Needleforge and `HeroController` assume several core slash components are non-null. The lab keeps
private Hunter-derived safety objects behind an execution gate when Core is `None`; they satisfy
that internal invariant but cannot run, deal damage, animate, or produce Hunter's downslash.

## Configuration

Edit `BepInEx/config/io.github.erymanthis.crestcomponentlab.cfg` or use a compatible config UI.
Each component accepts an independent vanilla provider:

- Hunter
- Wanderer
- Reaper
- Architect
- Shaman
- Beast
- Witch
- Cloakless

Core moveset includes regular, alternate, up, down, wall, and Challenge/Taunt objects. Dash and
Charge remain controlled only by their own settings. Normal Bind uses a scoped identity route
during a standard Bind. Special Bind installs an Up-Bind route and calls the selected provider's
vanilla `BindCompleted` path only when configured.

Configuration values can be edited while the game is running. A complete five-part recipe is
queued rather than mutating the equipped crest immediately:

- changes made away from a bench activate on the next bench entry;
- changes made while seated activate on bench exit;
- later edits replace the queued recipe, and returning every value to the active recipe cancels it.

The lab installs its Up-Bind branch during startup, so Special Bind can also be changed through the
same live, bench-gated workflow without returning to the title.

## Known experimental boundary

Vanilla crests expose one shared hero animation override library, so a mixed core/dash/charge
recipe cannot yet select three independent hero-animation libraries. Attack GameObjects—including
their damage, child animation, and audio components—are cloned independently. The lab logs the
resolved composition so this remaining shared dependency can be measured during gameplay.
