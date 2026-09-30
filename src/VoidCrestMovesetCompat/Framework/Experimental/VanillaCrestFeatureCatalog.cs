using System;
using System.Collections.Generic;

namespace VoidCrestMovesetCompat.Framework.Experimental;

/// <summary>
/// Evidence-backed metadata for the vanilla features currently understood by the compatibility
/// project. Registration does not imply that a runtime adapter has been implemented or verified.
/// </summary>
public static class VanillaCrestFeatureCatalog
{
    public static readonly string[] SupportedCrestIds =
    {
        "Hunter",
        "Wanderer",
        "Reaper",
        "Architect",
        "Shaman",
        "Beast",
        "Witch",
        "Cloakless"
    };

    private static readonly (string Slot, string Suffix, string Evidence)[] AttackSlots =
    {
        (StandardCrestSlots.NormalAttack, "normal", "HeroController.ConfigGroup.NormalSlashObject"),
        (StandardCrestSlots.AlternateAttack, "alternate", "HeroController.ConfigGroup.AlternateSlashObject"),
        (StandardCrestSlots.UpAttack, "up", "HeroController.ConfigGroup.UpSlashObject"),
        (StandardCrestSlots.AlternateUpAttack, "up-alternate", "HeroController.ConfigGroup.AltUpSlashObject"),
        (StandardCrestSlots.DownAttack, "down", "HeroController.ConfigGroup.DownSlashObject"),
        (StandardCrestSlots.AlternateDownAttack, "down-alternate", "HeroController.ConfigGroup.AltDownSlashObject"),
        (StandardCrestSlots.WallAttack, "wall", "HeroController.ConfigGroup.WallSlashObject"),
        (StandardCrestSlots.DashAttack, "dash", "HeroController.ConfigGroup.DashStab/DashStabAlt"),
        (StandardCrestSlots.ChargeAttack, "charge", "HeroController.ConfigGroup.ChargeSlash"),
        (StandardCrestSlots.ChallengeAttack, "challenge", "HeroController.ConfigGroup.TauntSlash")
    };

    public static CrestFeatureRegistry CreateRegistry()
    {
        CrestFeatureRegistry registry = new();
        foreach (string crestId in SupportedCrestIds)
        {
            RegisterCrestSurface(registry, crestId);
        }

        RegisterStatefulFeatures(registry);
        return registry;
    }

    public static string FeatureId(string crestId, string family, string name)
    {
        return $"vanilla.{NormalizeId(crestId)}.{family}.{name}";
    }

    private static void RegisterCrestSurface(CrestFeatureRegistry registry, string crestId)
    {
        registry.Register(new CrestFeatureDescriptor(
            FeatureId(crestId, "identity", "equipped"),
            $"{crestId} functional identity",
            CrestFeatureKind.Identity,
            crestId,
            CrestTransferMode.Proxy,
            requiresFunctionalIdentity: true,
            lifecycleSensitive: true,
            evidence: "ToolBase.IsEquipped and crest-ID comparisons at feature-specific call sites"));

        foreach ((string slot, string suffix, string evidence) in AttackSlots)
        {
            registry.Register(new CrestFeatureDescriptor(
                FeatureId(crestId, "attack", suffix),
                $"{crestId} {suffix} attack",
                CrestFeatureKind.Attack,
                crestId,
                CrestTransferMode.Clone,
                lifecycleSensitive: true,
                evidence: evidence));
        }

        registry.Register(new CrestFeatureDescriptor(
            FeatureId(crestId, "animation", "profile"),
            $"{crestId} animation profile",
            CrestFeatureKind.Animation,
            crestId,
            CrestTransferMode.Reference,
            lifecycleSensitive: true,
            evidence: "HeroControllerConfig and HeroAnimationController configuration"));
        registry.Register(new CrestFeatureDescriptor(
            FeatureId(crestId, "audio", "profile"),
            $"{crestId} audio profile",
            CrestFeatureKind.Audio,
            crestId,
            CrestTransferMode.Clone,
            requiresFunctionalIdentity: true,
            lifecycleSensitive: true,
            evidence: "Attack AudioSource components and crest-dependent PlayMaker FSM routes"));
    }

    private static void RegisterStatefulFeatures(CrestFeatureRegistry registry)
    {
        string hunterProgression = FeatureId("Hunter", "progression", "evolution");
        registry.Register(new CrestFeatureDescriptor(
            hunterProgression,
            "Hunter evolution progression",
            CrestFeatureKind.Progression,
            "Hunter",
            CrestTransferMode.Reference,
            requiresRuntimeState: true,
            lifecycleSensitive: true,
            evidence: "Hunter/Hunter_v2/Hunter_v3 unlocks and HeroController.HunterUpgState"));
        registry.Register(new CrestFeatureDescriptor(
            FeatureId("Hunter", "passive", "focus"),
            "Hunter Focus",
            CrestFeatureKind.Passive,
            "Hunter",
            CrestTransferMode.Proxy,
            requiresFunctionalIdentity: true,
            requiresRuntimeState: true,
            lifecycleSensitive: true,
            dependencies: new[] { hunterProgression },
            evidence: "DamageEnemies.DoDamage and HeroController.HunterUpgCrestStateInfo"));

        registry.Register(new CrestFeatureDescriptor(
            FeatureId("Wanderer", "passive", "luck"),
            "Wanderer luck and critical hits",
            CrestFeatureKind.Passive,
            "Wanderer",
            CrestTransferMode.Proxy,
            requiresFunctionalIdentity: true,
            requiresRuntimeState: true,
            lifecycleSensitive: true,
            evidence: "HeroController.IsWandererLucky, DamageEnemies.DoDamage, and WandererCrestStateInfo"));

        string reaperState = FeatureId("Reaper", "state", "harvest");
        registry.Register(new CrestFeatureDescriptor(
            reaperState,
            "Reaper harvest state",
            CrestFeatureKind.RuntimeState,
            "Reaper",
            CrestTransferMode.Reference,
            requiresFunctionalIdentity: true,
            requiresRuntimeState: true,
            lifecycleSensitive: true,
            evidence: "HeroController.ReaperCrestStateInfo and HeroController.Update"));
        registry.Register(new CrestFeatureDescriptor(
            FeatureId("Reaper", "bind", "activate-harvest"),
            "Reaper harvest Bind",
            CrestFeatureKind.Bind,
            "Reaper",
            CrestTransferMode.Proxy,
            requiresFunctionalIdentity: true,
            requiresRuntimeState: true,
            lifecycleSensitive: true,
            dependencies: new[] { reaperState },
            evidence: "HeroController.BindCompleted"));
        registry.Register(new CrestFeatureDescriptor(
            FeatureId("Reaper", "passive", "harvest-payout"),
            "Reaper harvest payout",
            CrestFeatureKind.Passive,
            "Reaper",
            CrestTransferMode.Proxy,
            requiresFunctionalIdentity: true,
            requiresRuntimeState: true,
            lifecycleSensitive: true,
            dependencies: new[] { reaperState },
            evidence: "HealthManager.TakeDamage, HeroController.GetReaperPayout, and ReaperBundlePrefab"));

        string beastState = FeatureId("Beast", "state", "rage");
        registry.Register(new CrestFeatureDescriptor(
            beastState,
            "Beast Rage state",
            CrestFeatureKind.RuntimeState,
            "Beast",
            CrestTransferMode.Reference,
            requiresFunctionalIdentity: true,
            requiresRuntimeState: true,
            lifecycleSensitive: true,
            evidence: "HeroController.WarriorCrestStateInfo and HeroController.Update"));
        registry.Register(new CrestFeatureDescriptor(
            FeatureId("Beast", "bind", "activate-rage"),
            "Beast Rage Bind",
            CrestFeatureKind.Bind,
            "Beast",
            CrestTransferMode.Proxy,
            requiresFunctionalIdentity: true,
            requiresRuntimeState: true,
            lifecycleSensitive: true,
            dependencies: new[] { beastState },
            evidence: "HeroController.BindCompleted"));
        registry.Register(new CrestFeatureDescriptor(
            FeatureId("Beast", "passive", "rage"),
            "Beast Rage combat behavior",
            CrestFeatureKind.Passive,
            "Beast",
            CrestTransferMode.Proxy,
            requiresFunctionalIdentity: true,
            requiresRuntimeState: true,
            lifecycleSensitive: true,
            dependencies: new[] { beastState },
            evidence: "DamageEnemies.DoDamage, HeroController.NailHitEnemy, and Warrior config timing"));

        registry.Register(new CrestFeatureDescriptor(
            FeatureId("Shaman", "passive", "skill-damage"),
            "Shaman Silk Skill multiplier",
            CrestFeatureKind.Passive,
            "Shaman",
            CrestTransferMode.Proxy,
            requiresFunctionalIdentity: true,
            lifecycleSensitive: true,
            evidence: "HeroShamanRuneEffect.Refresh and GlobalSettings.Gameplay.SpellCrestRuneDamageMult"));
        registry.Register(new CrestFeatureDescriptor(
            FeatureId("Shaman", "bind", "grounded"),
            "Shaman grounded Bind",
            CrestFeatureKind.Bind,
            "Shaman",
            CrestTransferMode.Proxy,
            requiresFunctionalIdentity: true,
            lifecycleSensitive: true,
            evidence: "Spell Control PlayMaker FSM and HeroController.FallCheck"));

        registry.Register(new CrestFeatureDescriptor(
            FeatureId("Witch", "passive", "recoil"),
            "Witch recoil behavior",
            CrestFeatureKind.Passive,
            "Witch",
            CrestTransferMode.Proxy,
            requiresFunctionalIdentity: true,
            evidence: "HeroController.Recoil, HeroController.RecoilDown, and WitchCrest checks"));
    }

    private static string NormalizeId(string value)
    {
        return value.Trim().ToLowerInvariant().Replace('_', '-').Replace(' ', '-');
    }
}
