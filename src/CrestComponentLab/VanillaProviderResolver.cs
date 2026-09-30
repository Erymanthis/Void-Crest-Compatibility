using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CrestComponentLab;

internal static class VanillaProviderResolver
{
    private static readonly FieldInfo? ConfigsField = typeof(HeroController).GetField(
        "configs",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly IReadOnlyDictionary<ComponentSource, string[]> ConfigAliases =
        new Dictionary<ComponentSource, string[]>
        {
            [ComponentSource.Hunter] = new[] { "Default", "Hunter", "Hunter_v2", "Hunter_v3" },
            [ComponentSource.Wanderer] = new[] { "Wanderer" },
            [ComponentSource.Reaper] = new[] { "Reaper" },
            [ComponentSource.Architect] = new[] { "Toolmaster", "Architect" },
            [ComponentSource.Shaman] = new[] { "Shaman", "Spell" },
            [ComponentSource.Beast] = new[] { "Warrior", "Beast" },
            [ComponentSource.Witch] = new[] { "Witch", "Whip" },
            [ComponentSource.Cloakless] = new[] { "Cloakless" }
        };

    internal static HeroController.ConfigGroup? ResolveGroup(ComponentSource source)
    {
        HeroController? hero = HeroController.instance;
        HeroController.ConfigGroup[]? configs = hero == null
            ? null
            : ConfigsField?.GetValue(hero) as HeroController.ConfigGroup[];
        if (source == ComponentSource.None || configs == null ||
            !ConfigAliases.TryGetValue(source, out string[]? aliases))
        {
            return null;
        }

        foreach (string alias in aliases)
        {
            HeroController.ConfigGroup? match = configs.FirstOrDefault(group =>
                group != null &&
                (string.Equals(group.Config?.name, alias, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(group.ActiveRoot?.name, alias, StringComparison.OrdinalIgnoreCase)));
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    internal static GameObject? ResolveAttack(ComponentSource source, AttackSlot slot)
    {
        HeroController.ConfigGroup? group = ResolveGroup(source);
        if (group == null)
        {
            return null;
        }

        return slot switch
        {
            AttackSlot.Normal => group.NormalSlashObject,
            AttackSlot.Alternate => group.AlternateSlashObject,
            AttackSlot.Up => group.UpSlashObject,
            AttackSlot.AlternateUp => group.AltUpSlashObject,
            AttackSlot.Down => group.DownSlashObject,
            AttackSlot.AlternateDown => group.AltDownSlashObject,
            AttackSlot.Wall => group.WallSlashObject,
            AttackSlot.Dash => group.DashStab,
            AttackSlot.Charge => group.ChargeSlash,
            AttackSlot.Taunt => group.TauntSlash,
            _ => null
        };
    }

    internal static ToolCrest? ResolveToolCrest(ComponentSource source)
    {
        return source switch
        {
            ComponentSource.Hunter => FirstAlive(
                GlobalSettings.Gameplay.HunterCrest3,
                GlobalSettings.Gameplay.HunterCrest2,
                GlobalSettings.Gameplay.HunterCrest),
            ComponentSource.Wanderer => GlobalSettings.Gameplay.WandererCrest,
            ComponentSource.Reaper => GlobalSettings.Gameplay.ReaperCrest,
            ComponentSource.Architect => GlobalSettings.Gameplay.ToolmasterCrest,
            ComponentSource.Shaman => GlobalSettings.Gameplay.SpellCrest,
            ComponentSource.Beast => GlobalSettings.Gameplay.WarriorCrest,
            ComponentSource.Witch => GlobalSettings.Gameplay.WitchCrest,
            ComponentSource.Cloakless => GlobalSettings.Gameplay.CloaklessCrest,
            _ => null
        };
    }

    private static ToolCrest? FirstAlive(params ToolCrest[] candidates)
    {
        foreach (ToolCrest candidate in candidates)
        {
            if (candidate)
            {
                return candidate;
            }
        }

        return null;
    }
}
