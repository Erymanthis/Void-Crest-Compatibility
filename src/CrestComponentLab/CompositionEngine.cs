using System;
using System.Collections.Generic;
using System.Reflection;
using Needleforge.Attacks;
using Needleforge.Data;
using UnityEngine;

namespace CrestComponentLab;

internal static class CompositionEngine
{
    private static readonly string[] DashConfigFields =
    {
        "dashStabSpeed",
        "dashStabTime",
        "forceShortDashStabBounce",
        "dashStabBounceJumpSpeed",
        "dashStabSteps"
    };

    private static readonly string[] ChargeConfigFields =
    {
        "chargeSlashRecoils",
        "chargeSlashLungeSpeed",
        "chargeSlashLungeDeceleration",
        "chargeSlashChain"
    };

    private static readonly BindingFlags ConfigFieldFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly MethodInfo? UpdateConfigMethod = typeof(HeroController).GetMethod(
        "UpdateConfig",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly FieldInfo? CrestConfigField = typeof(HeroController).GetField(
        "crestConfig",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static GameObject? activeGenerationRoot;
    private static int generation;

    internal static bool IsInitialized => Plugin.Crest?.Moveset.ConfigGroup != null;

    internal static void ConfigureDefinition(MovesetData moveset)
    {
        CompositionRecipe recipe = Plugin.ActiveRecipe;
        ComponentSource core = recipe.Core;
        ComponentSource dash = recipe.Dash;
        ComponentSource charge = recipe.Charge;

        moveset.Slash = core == ComponentSource.None
            ? new InertAttack(AttackSlot.Normal)
            : new ProviderAttack(core, AttackSlot.Normal);
        moveset.UpSlash = core == ComponentSource.None
            ? new InertAttack(AttackSlot.Up)
            : new ProviderAttack(core, AttackSlot.Up);
        moveset.WallSlash = core == ComponentSource.None
            ? new InertAttack(AttackSlot.Wall)
            : new ProviderAttack(core, AttackSlot.Wall);
        moveset.DownSlash = core == ComponentSource.None
            ? new InertDownAttack(AttackSlot.Down)
            : new ProviderDownAttack(core, AttackSlot.Down);
        moveset.AltSlash = core == ComponentSource.None
            ? null
            : new ProviderAttack(core, AttackSlot.Alternate);
        moveset.AltUpSlash = core == ComponentSource.None
            ? null
            : new ProviderAttack(core, AttackSlot.AlternateUp);
        moveset.AltDownSlash = core == ComponentSource.None
            ? null
            : new ProviderDownAttack(core, AttackSlot.AlternateDown);
        moveset.DashSlash = dash == ComponentSource.None
            ? new InertDashAttack()
            : new ProviderDashAttack(dash);
        moveset.ChargedSlash = charge == ComponentSource.None
            ? new InertChargedAttack()
            : new ProviderChargedAttack(charge);
    }

    internal static void PrepareForInitialization(MovesetData moveset)
    {
        ConfigureDefinition(moveset);
        HeroConfigNeedleforge? config = ComposeHeroConfig(Plugin.ActiveRecipe);
        if (config != null)
        {
            moveset.HeroConfig = config;
        }
    }

    internal static bool ApplyRecipe(CompositionRecipe recipe)
    {
        CrestData? crest = Plugin.Crest;
        HeroController? hero = HeroController.instance;
        HeroController.ConfigGroup? target = crest?.Moveset.ConfigGroup;
        if (crest == null || target?.ActiveRoot == null || hero == null)
        {
            return false;
        }

        HeroConfigNeedleforge? config = ComposeHeroConfig(recipe);
        if (config == null)
        {
            return false;
        }

        GameObject stageRoot = new($"Component Lab Recipe {++generation}");
        stageRoot.transform.SetParent(target.ActiveRoot.transform, false);
        stageRoot.SetActive(false);

        ComponentSource coreObjects = recipe.Core == ComponentSource.None
            ? ComponentSource.Hunter
            : recipe.Core;
        SlotObjects staged = new()
        {
            Normal = ProviderClone.Create(coreObjects, AttackSlot.Normal, stageRoot),
            Alternate = recipe.Core == ComponentSource.None ? null : ProviderClone.Create(coreObjects, AttackSlot.Alternate, stageRoot),
            Up = ProviderClone.Create(coreObjects, AttackSlot.Up, stageRoot),
            AlternateUp = recipe.Core == ComponentSource.None ? null : ProviderClone.Create(coreObjects, AttackSlot.AlternateUp, stageRoot),
            Down = ProviderClone.Create(coreObjects, AttackSlot.Down, stageRoot),
            AlternateDown = recipe.Core == ComponentSource.None ? null : ProviderClone.Create(coreObjects, AttackSlot.AlternateDown, stageRoot),
            Wall = ProviderClone.Create(coreObjects, AttackSlot.Wall, stageRoot),
            Dash = recipe.Dash == ComponentSource.None ? null : ProviderClone.Create(recipe.Dash, AttackSlot.Dash, stageRoot),
            Charge = recipe.Charge == ComponentSource.None ? null : ProviderClone.Create(recipe.Charge, AttackSlot.Charge, stageRoot),
            Taunt = recipe.Core == ComponentSource.None ? null : ProviderClone.Create(recipe.Core, AttackSlot.Taunt, stageRoot)
        };

        if (!ValidateCore(staged, config))
        {
            UnityEngine.Object.Destroy(stageRoot);
            return false;
        }

        SlotObjects previous = SlotObjects.From(target);
        HeroConfigNeedleforge? previousConfig = crest.Moveset.HeroConfig;
        GameObject? previousGeneration = activeGenerationRoot;

        try
        {
            crest.Moveset.HeroConfig = config;
            staged.AssignTo(target);
            ClearResolvedComponents(target);
            stageRoot.SetActive(true);
            target.Setup();

            if (crest.IsEquipped)
            {
                CrestConfigField?.SetValue(hero, config);
                UpdateConfigMethod?.Invoke(hero, null);
            }

            if (previousGeneration)
            {
                previousGeneration.SetActive(false);
                UnityEngine.Object.Destroy(previousGeneration);
            }
            else
            {
                foreach (GameObject? oldObject in previous.All)
                {
                    if (!oldObject)
                    {
                        continue;
                    }

                    oldObject.SetActive(false);
                    UnityEngine.Object.Destroy(oldObject);
                }
            }

            activeGenerationRoot = stageRoot;
            LogComposition("bench apply", recipe);
            return true;
        }
        catch (Exception exception)
        {
            Plugin.LogWarning($"Recipe staging failed and was rolled back: {exception.GetType().Name}: {exception.Message}");
            stageRoot.SetActive(false);
            UnityEngine.Object.Destroy(stageRoot);
            if (previousConfig != null)
            {
                crest.Moveset.HeroConfig = previousConfig;
            }

            previous.AssignTo(target);
            ClearResolvedComponents(target);
            target.Setup();
            if (crest.IsEquipped && previousConfig != null)
            {
                CrestConfigField?.SetValue(hero, previousConfig);
                UpdateConfigMethod?.Invoke(hero, null);
            }

            return false;
        }
    }

    internal static void FinalizeInitialization()
    {
        HeroController.ConfigGroup? target = Plugin.Crest?.Moveset.ConfigGroup;
        if (target == null)
        {
            return;
        }

        ReplaceTaunt(target, Plugin.ActiveRecipe.Core);
        target.Setup();
        LogComposition("initialization", Plugin.ActiveRecipe);
    }

    private static HeroConfigNeedleforge? ComposeHeroConfig(CompositionRecipe recipe)
    {
        HeroController.ConfigGroup? neutral = VanillaProviderResolver.ResolveGroup(ComponentSource.Hunter);
        HeroController.ConfigGroup? core = VanillaProviderResolver.ResolveGroup(recipe.Core);
        HeroController.ConfigGroup? dash = VanillaProviderResolver.ResolveGroup(recipe.Dash);
        HeroController.ConfigGroup? charge = VanillaProviderResolver.ResolveGroup(recipe.Charge);
        // Core owns the baseline controller contract. When it is None, Hunter is used only as
        // an internal non-executing safety baseline; dash and charge contribute only their fields.
        HeroController.ConfigGroup? baseGroup = core ?? neutral ?? dash ?? charge;
        if (baseGroup?.Config == null)
        {
            Plugin.LogWarning("No vanilla config group is available yet; composition will retry after HeroController initialization.");
            return null;
        }

        HeroConfigNeedleforge config = HeroConfigNeedleforge.Copy(baseGroup.Config);
        config.name = Plugin.CrestId;

        if (dash?.Config != null && !ReferenceEquals(dash, baseGroup))
        {
            CopyNamedFields(dash.Config, config, DashConfigFields);
        }

        if (charge?.Config != null && !ReferenceEquals(charge, baseGroup))
        {
            CopyNamedFields(charge.Config, config, ChargeConfigFields);
        }

        SetNamedField(config, "canNailCharge", recipe.Charge != ComponentSource.None);
        SetNamedField(config, "canBind", true);
        return config;
    }

    private static void SetNamedField(HeroControllerConfig target, string name, object value)
    {
        typeof(HeroControllerConfig).GetField(name, ConfigFieldFlags)?.SetValue(target, value);
    }

    private static void CopyNamedFields(
        HeroControllerConfig source,
        HeroControllerConfig target,
        IEnumerable<string> names)
    {
        Type type = typeof(HeroControllerConfig);
        foreach (string name in names)
        {
            FieldInfo? field = type.GetField(name, ConfigFieldFlags);
            if (field != null)
            {
                field.SetValue(target, field.GetValue(source));
            }
        }
    }

    private static void ReplaceTaunt(HeroController.ConfigGroup target, ComponentSource source)
    {
        if (target.TauntSlash && target.TauntSlash.transform.IsChildOf(target.ActiveRoot.transform))
        {
            UnityEngine.Object.DestroyImmediate(target.TauntSlash);
        }

        target.TauntSlash = source == ComponentSource.None
            ? null
            : ProviderClone.Create(source, AttackSlot.Taunt, target.ActiveRoot);
    }

    private static bool ValidateCore(SlotObjects staged, HeroControllerConfig config)
    {
        if (!HasSlashParts(staged.Normal) || !HasSlashParts(staged.Up) || !HasSlashParts(staged.Wall))
        {
            Plugin.LogWarning("The requested core provider is missing a required slash or damage component.");
            return false;
        }

        if (config.DownSlashType == HeroControllerConfig.DownSlashTypes.Custom)
        {
            return true;
        }

        bool hasDownDamage = staged.Down && staged.Down.GetComponent<DamageEnemies>();
        bool hasDownAttack = config.DownSlashType == HeroControllerConfig.DownSlashTypes.DownSpike
            ? staged.Down && staged.Down.GetComponent<Downspike>()
            : staged.Down && staged.Down.GetComponent<NailSlash>();
        if (!hasDownAttack || !hasDownDamage)
        {
            Plugin.LogWarning($"The requested core provider is missing components for downslash type {config.DownSlashType}.");
            return false;
        }

        return true;
    }

    private static bool HasSlashParts(GameObject? value) =>
        value && value.GetComponent<NailSlash>() && value.GetComponent<DamageEnemies>();

    private static void ClearResolvedComponents(HeroController.ConfigGroup target)
    {
        foreach (FieldInfo field in typeof(HeroController.ConfigGroup).GetFields(ConfigFieldFlags))
        {
            if (field.Name.Contains("k__BackingField") && typeof(Component).IsAssignableFrom(field.FieldType))
            {
                field.SetValue(target, null);
            }
        }
    }

    private static void LogComposition(string reason, CompositionRecipe recipe)
    {
        HeroController.ConfigGroup? group = Plugin.Crest?.Moveset.ConfigGroup;
        Plugin.LogInfo(
            $"Composition {reason}: {recipe}, " +
            $"targetConfig={group?.Config?.name ?? "<null>"}, root={group?.ActiveRoot?.name ?? "<null>"}.");
    }

    private sealed class SlotObjects
    {
        internal GameObject? Normal { get; set; }
        internal GameObject? Alternate { get; set; }
        internal GameObject? Up { get; set; }
        internal GameObject? AlternateUp { get; set; }
        internal GameObject? Down { get; set; }
        internal GameObject? AlternateDown { get; set; }
        internal GameObject? Wall { get; set; }
        internal GameObject? Dash { get; set; }
        internal GameObject? DashAlternate { get; set; }
        internal GameObject? Charge { get; set; }
        internal GameObject? Taunt { get; set; }

        internal IEnumerable<GameObject?> All
        {
            get
            {
                yield return Normal;
                yield return Alternate;
                yield return Up;
                yield return AlternateUp;
                yield return Down;
                yield return AlternateDown;
                yield return Wall;
                yield return Dash;
                yield return DashAlternate;
                yield return Charge;
                yield return Taunt;
            }
        }

        internal static SlotObjects From(HeroController.ConfigGroup group) => new()
        {
            Normal = group.NormalSlashObject,
            Alternate = group.AlternateSlashObject,
            Up = group.UpSlashObject,
            AlternateUp = group.AltUpSlashObject,
            Down = group.DownSlashObject,
            AlternateDown = group.AltDownSlashObject,
            Wall = group.WallSlashObject,
            Dash = group.DashStab,
            DashAlternate = group.DashStabAlt,
            Charge = group.ChargeSlash,
            Taunt = group.TauntSlash
        };

        internal void AssignTo(HeroController.ConfigGroup group)
        {
            group.NormalSlashObject = Normal;
            group.AlternateSlashObject = Alternate;
            group.UpSlashObject = Up;
            group.AltUpSlashObject = AlternateUp;
            group.DownSlashObject = Down;
            group.AltDownSlashObject = AlternateDown;
            group.WallSlashObject = Wall;
            group.DashStab = Dash;
            group.DashStabAlt = DashAlternate;
            group.ChargeSlash = Charge;
            group.TauntSlash = Taunt;
        }
    }
}
