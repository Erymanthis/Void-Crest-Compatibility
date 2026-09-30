namespace CrestComponentLab;

/// <summary>
/// Vanilla providers understood by the clean-room composition experiment.
/// None means no provider. For normal Bind, None preserves Needleforge's standard custom-crest Bind.
/// </summary>
public enum ComponentSource
{
    None,
    Hunter,
    Wanderer,
    Reaper,
    Architect,
    Shaman,
    Beast,
    Witch,
    Cloakless
}

internal enum AttackSlot
{
    Normal,
    Alternate,
    Up,
    AlternateUp,
    Down,
    AlternateDown,
    Wall,
    Dash,
    Charge,
    Taunt
}
