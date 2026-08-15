using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using GlobalEnums;
using HarmonyLib;
using Needleforge.Data;
using Silksong.ModMenu.Elements;
using Silksong.ModMenu.Models;
using Silksong.ModMenu.Plugin;
using UnityEngine;
using VoidCrest;

namespace VoidCrestMovesetCompat;

[BepInDependency("io.dervoce.voidcrest")]
[BepInDependency("io.github.needleforge")]
[BepInPlugin("io.github.erymanthis.voidcrestmovesetcompat", "VoidCrestMovesetCompat", "1.0.0")]
public sealed class Plugin : BaseUnityPlugin
{
    private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string VoidCrestConfigFileName = "io.dervoce.voidcrest.cfg";
    private const string VoidCrestBaseCrestSection = "General";
    private const string VoidCrestBaseCrestKey = "Base Crest";
    private static readonly FieldInfo? HeroConfigsField = typeof(HeroController).GetField("configs", InstanceFlags);
    private static readonly FieldInfo? ToolCrestHeroConfigField = typeof(ToolCrest).GetField("heroConfig", InstanceFlags);
    private static readonly FieldInfo? CrestConfigField = typeof(HeroController).GetField("crestConfig", InstanceFlags);
    private static readonly FieldInfo? DashFsmEditField = typeof(HeroConfigNeedleforge).GetField("_dashFsm", InstanceFlags);
    private static readonly FieldInfo? DownSlashTypeField = typeof(HeroControllerConfig).GetField("downSlashType", InstanceFlags);
    private static readonly FieldInfo? DownSlashEventField = typeof(HeroControllerConfig).GetField("downSlashEvent", InstanceFlags);
    private static readonly PropertyInfo? CurrentConfigGroupProperty = typeof(HeroController).GetProperty("CurrentConfigGroup", InstanceFlags);
    private static readonly MethodInfo? UpdateConfigMethod = typeof(HeroController).GetMethod("UpdateConfig", InstanceFlags);
    private static readonly MethodInfo? SetConfigGroupMethod = typeof(HeroController).GetMethod("SetConfigGroup", InstanceFlags);
    private static readonly Type? VoidSpoolType = AccessTools.TypeByName("VoidCrest.voidspool");
    private static readonly FieldInfo? VoidMassField = VoidSpoolType?.GetField("_voidMass", StaticFlags);
    private static readonly PropertyInfo? VoidMassProperty = VoidSpoolType?.GetProperty("voidMass", StaticFlags);
    private static readonly FieldInfo? VoidSpoolCloneField = VoidSpoolType?.GetField("spoolClone", StaticFlags);
    private static readonly FieldInfo? SilkChunksField = typeof(SilkSpool).GetField("silkChunks", InstanceFlags);
    private static readonly FieldInfo? HunterUpgStateField = typeof(HeroController).GetField("hunterUpgState", InstanceFlags);
    private static readonly FieldInfo? NormalSlashField = typeof(HeroController).GetField("normalSlash", InstanceFlags);
    private static readonly FieldInfo? DownSlashField = typeof(HeroController).GetField("downSlash", InstanceFlags);
    private static readonly FieldInfo? DownSpikeField = typeof(HeroController).GetField("downSpike", InstanceFlags);
    private static readonly FieldInfo? WallSlashField = typeof(HeroController).GetField("wallSlash", InstanceFlags);
    private static readonly FieldInfo? ToolCrestSpriteField = typeof(ToolCrest).GetField("crestSprite", InstanceFlags);
    private static readonly FieldInfo? ToolCrestSilhouetteField = typeof(ToolCrest).GetField("crestSilhouette", InstanceFlags);
    private static readonly FieldInfo? ToolCrestGlowField = typeof(ToolCrest).GetField("crestGlow", InstanceFlags);
    private static readonly FieldInfo? ToolCrestSlotsField = typeof(ToolCrest).GetField("slots", InstanceFlags);
    private const string VoidCrestArtworkResource = "VoidCrestMovesetCompat.Assets.Void_Crest_Master_Thick.png";
    private const float VoidCrestDisplayScale = 1.2f;
    // Pixel centers from Void_Crest_(Slot_Placement).PNG, converted through Void Crest's
    // existing 696 x 735 sprite output at 100 pixels per Unity unit.
    private static readonly Vector2[] VoidCrestSlotPositions =
    {
        new(-0.007f, 2.189f), // 1: upper crown
        new(0.000f, -0.294f), // 2: central ring
        new(-0.075f, -2.017f), // 3: lower hook
        new(-2.005f, 0.768f), // 4: left wing
        new(2.114f, 0.732f), // 5: right wing
        new(1.420f, -0.258f) // 6: lower-right spur
    };
    private static readonly string[] ConfigGroupObjectFields =
    {
        "NormalSlashObject",
        "AlternateSlashObject",
        "UpSlashObject",
        "AltUpSlashObject",
        "DownSlashObject",
        "AltDownSlashObject",
        "WallSlashObject",
        "ChargeSlash",
        "TauntSlash"
    };
    private static readonly Dictionary<string, string[]> ConfigAliases = new(StringComparer.Ordinal)
    {
        ["Hunter"] = new[] { "Default" },
        ["Hunter_v2"] = new[] { "Default" },
        ["Hunter_v3"] = new[] { "Default" },
        ["Hunter_V2"] = new[] { "Default" },
        ["Hunter_V3"] = new[] { "Default" },
        ["Beast"] = new[] { "Warrior", "Beast" },
        ["Warrior"] = new[] { "Warrior", "Beast" },
        ["Architect"] = new[] { "Toolmaster" },
        ["Toolmaster"] = new[] { "Toolmaster" },
        ["Witch"] = new[] { "Witch", "Whip" },
        ["Whip"] = new[] { "Witch", "Whip" },
        ["Shaman"] = new[] { "Spell", "Shaman" },
        ["Spell"] = new[] { "Spell", "Shaman" },
        ["Reaper"] = new[] { "Reaper" },
        ["Wanderer"] = new[] { "Wanderer" },
        ["Cloakless"] = new[] { "Cloakless" },
        ["Cursed"] = new[] { "Cursed" }
    };

    private static readonly HashSet<string> ApprovedBaseCrests = new(StringComparer.Ordinal)
    {
        "Hunter",
        "Beast",
        "Warrior",
        "Architect",
        "Toolmaster",
        "Witch",
        "Whip",
        "Shaman",
        "Spell",
        "Reaper",
        "Wanderer",
        "Cloakless",
        "Cursed"
    };

    private static readonly string[] OrderedBaseCrests =
    {
        "Hunter",
        "Wanderer",
        "Reaper",
        "Toolmaster",
        "Spell",
        "Warrior",
        "Cursed",
        "Cloakless"
    };

    private static ManualLogSourceAdapter Log = null!;
    private static Harmony Harmony = null!;
    private static ConfigEntry<bool> TintHijacking = null!;
    private static ConfigEntry<bool> WandererFiftyPercentCritChance = null!;
    private static ConfigEntry<bool> WandererLuckTracker = null!;
    private static ConfigEntry<int> SetVoidmassCount = null!;
    private static ConfigEntry<bool> SetMasksToCriticalDamage = null!;
    private static ConfigEntry<string> ActivateHunterFocusLevel = null!;
    private static ConfigEntry<bool> ShamanSpellDamageBonus = null!;
    private static bool resettingMaskDebugAction;
    private static bool resettingHunterFocusSelection;
    private static int liveSlashRetintLogCount;
    private static int heroExtraNailSlashLogCount;
    private static readonly HashSet<string> LoggedDashAliases = new(StringComparer.Ordinal);
    private static readonly HashSet<int> ConfirmedNamedBosses = new();
    private static readonly HashSet<string> BossOverhaulObjectNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "First Weaver",
        "Lost Lace Boss",
        "Silk Boss"
    };
    private static readonly HashSet<string> BossOverhaulComponentTypes = new(StringComparer.Ordinal)
    {
        "LostSinner.Behaviours.Sinner",
        "Awakened_Grand_Mother_Silk.Source.Behaviours.SilkBoss",
        "LostAndChained.Components.LaceBossScene"
    };
    // Community boss rosters define the Hunter cap. These aliases include
    // public names plus known internal object-name forms.
    private static readonly string[] CommunityBossAliases =
    {
        "Bell Beast", "Bell Eater", "Broodmother", "Clover Dancers", "Cogwork Dancers", "Cog Dancers",
        "Crawfather", "Crust King Khann", "Disgraced Chef Lugoli", "Father of the Flame", "First Sinner",
        "Forebrothers", "Fourth Chorus", "Grand Mother Silk", "Great Conchflies", "Groal the Great",
        "Gurr the Outcast", "Lace", "Last Judge", "Lost Garmond", "Moorwing", "Moss Mother", "Nyleth",
        "Palestag", "Phantom", "Pinstress", "Plasmified Zango", "Raging Conchfly", "Raging Vonchfly",
        "Savage Beastfly", "Second Sentinel", "Shrine Guardian Seth", "Sister Splinter", "Skarrsinger Karmelita",
        "Skull Tyrant", "Summoned Saviour", "The Unravelled", "Tormented Trobbio", "Trobbio", "Voltvyrm",
        "Watcher at the Edge", "Widow", "Shakra", "Garmond", "Zaza", "Coral King", "Ant Queen",
        "Flower Queen", "Crow Court", "Grey Warrior", "Bone Flyer Giant", "Song Golem", "Ward Boss",
        "Zap Core Enemy"
    };
    private static string activeBaseCrest = string.Empty;
    private static string lastReportedHunterVersion = string.Empty;
    private string? observedBaseCrest;
    private string? pendingBaseCrest;
    private bool wasAtBench;
    private bool applyPendingOnBenchEnter;
    private bool applyPendingOnBenchExit;
    private bool wasVoidBinding;
    private bool sawVoidSpecialBinding;
    private static bool shamanNormalBindInProgress;
    private static bool shamanBindInvincibilityApplied;
    private static readonly object ShamanRecoveryInvincibilitySource = new();
    private static bool reaperHalfVoidmassPending;
    private static SilkChunk? reaperFractionalDisplayChunk;
    private static bool suppressReaperRetaliationVoidmassGain;

    private sealed class ReaperOrbSpawnState
    {
        public readonly List<GameObject> SpawnedObjects;
        public readonly int StartIndex;
        public readonly bool ShouldVoidify;

        public ReaperOrbSpawnState(List<GameObject> spawnedObjects, int startIndex, bool shouldVoidify)
        {
            SpawnedObjects = spawnedObjects;
            StartIndex = startIndex;
            ShouldVoidify = shouldVoidify;
        }
    }
    private static int wandererTrackedRolls;
    private static int wandererTrackedCrits;
    private static float wandererLastRoll = -1f;
    private static float wandererLastChance;
    private static bool wandererLastWasCrit;
    private GUIStyle? wandererTrackerStyle;
    private static Sprite? customVoidCrestSprite;
    private static Sprite? customVoidCrestSilhouette;
    private static Sprite? customVoidCrestGlow;

    private void Awake()
    {
        Log = new ManualLogSourceAdapter(Logger);
        CanonicalizeHunterConfig();
        TintHijacking = Config.Bind(
            "Visuals",
            "Tint Hijacking",
            true,
            "On: Void tint shader colors slashes. Off: vanilla or skin visuals.");
        TintHijacking.SettingChanged += (_, _) =>
        {
            Log.Info($"Tint Hijacking set to {TintHijacking.Value}; Void slash tint shader is {(TintHijacking.Value ? "enabled" : "disabled")}.");
            RefreshCurrentChargeSlashTintPolicy();
        };
        WandererFiftyPercentCritChance = Config.Bind(
            "Debug",
            "Wanderer 50 Percent Crit Chance",
            false,
            "On: while Void uses Wanderer, set critical-hit chance to 50% for testing. Off: use Wanderer's vanilla critical-hit chance. Vanilla crests are untouched.");
        WandererFiftyPercentCritChance.SettingChanged += (_, _) =>
            Log.Info($"Wanderer 50% crit debug override is {(WandererFiftyPercentCritChance.Value ? "enabled" : "disabled")}.");
        WandererLuckTracker = Config.Bind(
            "Debug",
            "Wanderer Luck Tracker",
            false,
            "Show an in-game panel with live Wanderer crit chance, RNG rolls, crit results, and observed rate.");
        WandererLuckTracker.SettingChanged += (_, _) =>
        {
            ResetWandererLuckTracker();
            Log.Info($"Wanderer Luck Tracker is {(WandererLuckTracker.Value ? "enabled" : "disabled")}.");
        };
        SetVoidmassCount = Config.Bind(
            "Debug",
            "Set Voidmass Count",
            0,
            new ConfigDescription(
                "During gameplay, selecting a value immediately sets the live Voidmass count. This is a one-time debug action, not a persistent override.",
                new AcceptableValueList<int>(Enumerable.Range(0, 11).ToArray())));
        SetVoidmassCount.SettingChanged += (_, _) => SetCurrentVoidmassForDebug(SetVoidmassCount.Value);
        SetMasksToCriticalDamage = Config.Bind(
            "Debug",
            "Set Masks to Critical Damage",
            false,
            "One-shot Hunter test action. On sets current health to exactly 2 Masks, then resets itself to Off. Does nothing unless Void is using Hunter.");
        SetMasksToCriticalDamage.SettingChanged += (_, _) => SetMasksForHunterCriticalDamageDebug();
        ActivateHunterFocusLevel = Config.Bind(
            "Debug",
            "Activate Focus Level",
            "Off",
            new ConfigDescription(
                "Sets Hunter's real combo meter for testing. Focus 1 requires Hunter v2 or v3; Focus 2 requires Hunter v3. Locked levels do nothing.",
                new AcceptableValueList<string>("Off", "Focus 1", "Focus 2")));
        ActivateHunterFocusLevel.SettingChanged += (_, _) => ActivateHunterFocusLevelDebug(ActivateHunterFocusLevel.Value);
        ShamanSpellDamageBonus = Config.Bind(
            "Debug",
            "Shaman Spell Damage Bonus",
            true,
            "On: while Void uses Spell, enable Shaman's vanilla Silk Skill damage bonus. Off: disable the bonus for testing. Vanilla crests are untouched.");
        ShamanSpellDamageBonus.SettingChanged += (_, _) =>
        {
            Log.Info($"Shaman/Spell damage bonus is {(ShamanSpellDamageBonus.Value ? "enabled" : "disabled")} for Void Crest.");
            RefreshLoadedShamanRuneEffects();
        };
        VoidCrestPlugin.enableOrangeHornet.SettingChanged += OnOrangeHornetShaderChanged;
        RegisterModMenuConfigOverrides();
        Harmony = new Harmony("io.github.erymanthis.voidcrestmovesetcompat");
        Harmony.PatchAll();
        observedBaseCrest = GetConfiguredBaseCrest();
        activeBaseCrest = observedBaseCrest;
        ApplyVoidCrestArtworkAndSlots();
        StartCoroutine(ApplyVoidCrestRuntimeWhenReady());
    }

    private static void ApplyVoidCrestArtworkAndSlots()
    {
        try
        {
            CrestData? crest = VoidCrestPlugin.voidCrestData;
            if (crest == null)
            {
                Log.Warning("Void crest data was unavailable while applying the custom crest artwork.");
                return;
            }

            EnsureVoidCrestSpritesLoaded();
            crest.RealSprite = customVoidCrestSprite;
            crest.Silhouette = customVoidCrestSilhouette;
            crest.CrestGlow = customVoidCrestGlow;

            ApplyVoidCrestSlotPositions(crest.slots);
            crest.ApplyAutoSlotNavigation(false, 60f, null);
            Log.Info("Applied the custom Void crest artwork and six-slot placement plan.");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to apply the custom Void crest artwork: {ex}");
        }
    }

    private static void EnsureVoidCrestSpritesLoaded()
    {
        if (customVoidCrestSprite != null && customVoidCrestSilhouette != null && customVoidCrestGlow != null)
        {
            return;
        }

        using Stream? stream = typeof(Plugin).Assembly.GetManifestResourceStream(VoidCrestArtworkResource);
        if (stream == null)
        {
            throw new InvalidOperationException($"Embedded artwork '{VoidCrestArtworkResource}' was not found.");
        }

        byte[] imageBytes = new byte[stream.Length];
        int offset = 0;
        while (offset < imageBytes.Length)
        {
            int read = stream.Read(imageBytes, offset, imageBytes.Length - offset);
            if (read <= 0)
            {
                throw new EndOfStreamException("The embedded Void crest artwork ended unexpectedly.");
            }

            offset += read;
        }

        Sprite baseSprite = VoidCrestSprites.LoadSpriteFromBytes(imageBytes);
        Sprite baseSilhouette = VoidCrestSprites.LoadSpriteFromBytes(imageBytes, Sil: true);
        customVoidCrestSprite = ScaleSpriteDisplay(baseSprite, VoidCrestDisplayScale);
        customVoidCrestSilhouette = ScaleSpriteDisplay(baseSilhouette, VoidCrestDisplayScale);
        customVoidCrestGlow = ScaleSpriteDisplay(
            VoidCrestSprites.MatchSpriteSize(customVoidCrestSilhouette),
            VoidCrestDisplayScale);
        customVoidCrestSprite.name = "Void Crest Master";
        customVoidCrestSilhouette.name = "Void Crest Master Silhouette";
        customVoidCrestGlow.name = "Void Crest Master Glow";

        // Keep Void Crest's own public sprite references consistent for any later consumers.
        VoidCrestSprites.crestSprite = customVoidCrestSprite;
        VoidCrestSprites.crestSilhouette = customVoidCrestSilhouette;
    }

    private static Sprite ScaleSpriteDisplay(Sprite source, float displayScale)
    {
        Rect rect = source.rect;
        Vector2 pivot = source.pivot / new Vector2(rect.width, rect.height);
        return Sprite.Create(
            source.texture,
            rect,
            pivot,
            source.pixelsPerUnit / displayScale,
            0,
            SpriteMeshType.FullRect,
            source.border);
    }

    private static void ApplyVoidCrestSlotPositions(List<ToolCrest.SlotInfo> slots)
    {
        if (slots.Count != VoidCrestSlotPositions.Length)
        {
            Log.Warning($"Expected six Void crest slots but found {slots.Count}; leaving slot placement unchanged.");
            return;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            ToolCrest.SlotInfo slot = slots[i];
            slot.Position = VoidCrestSlotPositions[i] * VoidCrestDisplayScale;
            slots[i] = slot;
        }

        // Directional cast slots follow the vertical spine of the crest:
        // upper crown = Cast + Up, central ring = Cast, lower hook = Cast + Down.
        ToolCrest.SlotInfo topMiddleSlot = slots[0];
        topMiddleSlot.AttackBinding = AttackToolBinding.Up;
        slots[0] = topMiddleSlot;

        ToolCrest.SlotInfo middleSlot = slots[1];
        middleSlot.AttackBinding = AttackToolBinding.Neutral;
        slots[1] = middleSlot;

        ToolCrest.SlotInfo bottomSlot = slots[2];
        bottomSlot.AttackBinding = AttackToolBinding.Down;
        slots[2] = bottomSlot;

        // The side and lower-right positions are ordinary neutral Void slots.
        ToolCrest.SlotInfo upperRightSlot = slots[4];
        upperRightSlot.AttackBinding = AttackToolBinding.Neutral;
        slots[4] = upperRightSlot;
    }

    private static IEnumerator ApplyVoidCrestRuntimeWhenReady()
    {
        for (int frame = 0; frame < 240; frame++)
        {
            CrestData? crest = VoidCrestPlugin.voidCrestData;
            ToolCrest? toolCrest = crest?.ToolCrest;
            if (crest != null && toolCrest)
            {
                ToolCrestSpriteField?.SetValue(toolCrest, customVoidCrestSprite);
                ToolCrestSilhouetteField?.SetValue(toolCrest, customVoidCrestSilhouette);
                ToolCrestGlowField?.SetValue(toolCrest, customVoidCrestGlow);
                ToolCrestSlotsField?.SetValue(toolCrest, crest.slots.ToArray());
                Log.Info("Applied the custom Void crest artwork and slots to the live ToolCrest.");
                yield break;
            }

            yield return null;
        }

        Log.Warning("The live Void ToolCrest was not created within 240 frames; data-layer artwork remains applied.");
    }

    private static void RegisterModMenuConfigOverrides()
    {
        try
        {
            FieldInfo? defaultGeneratorsField = typeof(ConfigEntryFactory).GetField("defaultGenerators", StaticFlags);
            if (defaultGeneratorsField?.GetValue(null) is not IList<ConfigEntryFactory.MenuElementGenerator> generators)
            {
                Log.Warning("Could not access ModMenu default config generators; Base Crest will stay a text field.");
                return;
            }

            if (generators.Contains(GenerateVoidCrestBaseCrestMenuElement))
            {
                return;
            }

            int stringGeneratorIndex = generators
                .Select((generator, index) => new { generator, index })
                .FirstOrDefault(x => x.generator.Method.Name == nameof(ConfigEntryFactory.GenerateStringElement))
                ?.index ?? generators.Count;

            generators.Insert(stringGeneratorIndex, GenerateVoidCrestBaseCrestMenuElement);
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed registering VoidCrest ModMenu config override: {ex}");
        }
    }

    private void Update()
    {
        TrackBenchDeferredBaseCrest();
        RestoreBaseBindCompletionIfNeeded();

        if (reaperHalfVoidmassPending &&
            (PlayerData.instance?.CurrentCrestID != "Void" || !IsSelectedBaseCrest("Reaper")))
        {
            ResetReaperFractionalVoidmass("Reaper branch inactive");
        }
    }

    private void LateUpdate()
    {
        // Void Crest's normal color monitor restores opaque black during its
        // frame update. Apply the Reaper half-pip afterward without patching or
        // initializing VoidCrestColorUtil during the pre-menu loader.
        if (reaperFractionalDisplayChunk && ShouldDisplayReaperHalfVoidmass())
        {
            SetSilkChunkOpacity(reaperFractionalDisplayChunk, 0.5f);
        }
    }

    private void OnDestroy()
    {
        if (VoidCrestPlugin.enableOrangeHornet != null)
        {
            VoidCrestPlugin.enableOrangeHornet.SettingChanged -= OnOrangeHornetShaderChanged;
        }

        HeroInvincibilitySource.Remove(ShamanRecoveryInvincibilitySource);
        ResetReaperFractionalVoidmass("plugin unload");
    }

    private static void OnOrangeHornetShaderChanged(object sender, EventArgs args)
    {
        try
        {
            HeroController? hero = HeroController.instance;
            CrestData? voidCrest = VoidCrestPlugin.voidCrestData;
            ConfigEntry<bool>? orangeHornet = VoidCrestPlugin.enableOrangeHornet;
            if (hero == null || voidCrest == null || !voidCrest.IsEquipped || orangeHornet == null)
            {
                return;
            }

            bool enabled = orangeHornet.Value;
            if (enabled)
            {
                VoidCrestColorUtil.Voidify(hero.gameObject);
            }
            else
            {
                VoidCrestColorUtil.UnvoidifyRoot(hero.gameObject);

                // Restoring Hornet also restores child renderers. Reapply the
                // independently configured aura without recoloring Hornet.
                ConfigEntry<bool>? orangeAura = VoidCrestPlugin.enableOrangeAura;
                if (orangeAura != null && orangeAura.Value)
                {
                    Transform heroLight = hero.transform.Find("HeroLight");
                    if (heroLight != null)
                    {
                        VoidCrestColorUtil.Voidify(heroLight.gameObject);
                    }
                }
            }

            Log.Info($"Applied Orange Hornet Model Shader setting live: {(enabled ? "On" : "Off")}.");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed applying Orange Hornet Model Shader setting live: {ex}");
        }
    }

    private void OnGUI()
    {
        HeroController? hero = HeroController.instance;
        if (WandererLuckTracker == null || !WandererLuckTracker.Value ||
            hero == null || hero.IsPaused() ||
            PlayerData.instance?.CurrentCrestID != "Void" || !IsSelectedBaseCrest("Wanderer"))
        {
            return;
        }

        wandererTrackerStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.UpperLeft,
            wordWrap = false
        };
        wandererTrackerStyle.normal.textColor = Color.white;

        int voidmass = GetCurrentVoidmass();
        float rawChance = GetWandererCritChance();
        float luckModifier = hero.GetLuckModifier();
        float finalChance = Mathf.Clamp01(rawChance * luckModifier);
        float observedChance = wandererTrackedRolls > 0
            ? (float)wandererTrackedCrits / wandererTrackedRolls
            : 0f;
        string lastRoll = wandererLastRoll >= 0f
            ? $"{wandererLastRoll:P2} / {wandererLastChance:P2}  {(wandererLastWasCrit ? "CRIT" : "normal")}"
            : "none";
        string trackerText =
            "WANDERER LUCK TRACKER\n" +
            $"Voidmass: {voidmass}    Ready: {(hero.IsWandererLucky ? "YES" : "NO (needs 9 Silk)")}\n" +
            $"Raw chance: {rawChance:P2}\n" +
            $"Luck modifier: x{luckModifier:0.00}\n" +
            $"Final chance: {finalChance:P2}\n" +
            $"RNG rolls: {wandererTrackedRolls}    Crits: {wandererTrackedCrits}\n" +
            $"Observed: {(wandererTrackedRolls > 0 ? observedChance.ToString("P2") : "n/a")}\n" +
            $"Last roll: {lastRoll}";

        const float width = 390f;
        const float height = 184f;
        float x = Mathf.Max(12f, Screen.width - width - 12f);
        Rect panel = new(x, 12f, width, height);
        GUI.Box(panel, GUIContent.none);
        GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, width - 24f, height - 16f), trackerText, wandererTrackerStyle);
    }

    private void RestoreBaseBindCompletionIfNeeded()
    {
        HeroController? hero = HeroController.instance;
        if (hero == null || PlayerData.instance?.CurrentCrestID != "Void")
        {
            if (hero != null && shamanBindInvincibilityApplied)
            {
                hero.RestoreHero();
            }
            shamanNormalBindInProgress = false;
            shamanBindInvincibilityApplied = false;
            wasVoidBinding = false;
            sawVoidSpecialBinding = false;
            return;
        }

        bool isBinding = hero.cState.isBinding;
        if (isBinding && VoidCrestPlugin.doingSpecialAttack)
        {
            sawVoidSpecialBinding = true;
        }

        bool shamanNormalBindStarted = !wasVoidBinding && isBinding &&
                                       !VoidCrestPlugin.doingSpecialAttack &&
                                       IsSelectedBaseCrest("Spell", "Shaman");
        if (shamanNormalBindStarted)
        {
            shamanNormalBindInProgress = true;
            hero.MakeHeroFucked();
            shamanBindInvincibilityApplied = true;
            Log.Info("Applied Void invincibility to Shaman's grounded normal Bind.");
        }

        // Void's Up-Bind owns its outcome. A normal Void Bind should still
        // complete the selected base crest's native post-Bind state even when
        // the custom Bind FSM does not call HeroController.BindCompleted.
        if (wasVoidBinding && !isBinding && !sawVoidSpecialBinding &&
            IsSelectedBaseCrest("Warrior", "Beast") && !hero.WarriorState.IsInRageMode)
        {
            hero.BindCompleted();
            Log.Info($"Restored Beast Fury after normal Void Bind; active={hero.WarriorState.IsInRageMode}.");
        }
        else if (wasVoidBinding && !isBinding && !sawVoidSpecialBinding &&
                 IsSelectedBaseCrest("Reaper") && !hero.ReaperState.IsInReaperMode)
        {
            hero.BindCompleted();
            Log.Info($"Restored Reaper harvest state after normal Void Bind; active={hero.ReaperState.IsInReaperMode}.");
        }

        if (wasVoidBinding && !isBinding && shamanNormalBindInProgress)
        {
            shamanNormalBindInProgress = false;
            if (shamanBindInvincibilityApplied)
            {
                hero.RestoreHero();
                shamanBindInvincibilityApplied = false;
            }
            StartCoroutine(ShamanBindRecoveryInvincibility());
            Log.Info("Completed Shaman grounded Void Bind with healing suppressed.");
        }

        if (!isBinding)
        {
            sawVoidSpecialBinding = false;
        }

        wasVoidBinding = isBinding;
    }

    private static IEnumerator ShamanBindRecoveryInvincibility()
    {
        // This begins only after cState.isBinding is false, so Void's Bind-hit
        // retaliation cannot trigger during the recovery protection.
        HeroInvincibilitySource.Add(ShamanRecoveryInvincibilitySource);
        for (int frame = 0; frame < 8; frame++)
        {
            yield return null;
        }
        HeroInvincibilitySource.Remove(ShamanRecoveryInvincibilitySource);
        Log.Info("Ended Shaman Bind's 8-frame non-retaliating recovery invincibility.");
    }

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.AddHealth), new[] { typeof(int) })]
    private static class ShamanNormalBindHealingPatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            // Shaman's native grounded route normally performs a vanilla heal.
            // Void's normal Bind intentionally heals zero. Its Up-Bind is never
            // marked as a normal Shaman Bind and therefore remains untouched.
            return !shamanNormalBindInProgress;
        }
    }

    [HarmonyPatch]
    private static class MovesetMakerInitializePatch
    {
        private static MethodBase? TargetMethod()
        {
            Type? movesetMakerType = AccessTools.TypeByName("Needleforge.Makers.MovesetMaker");
            return AccessTools.Method(movesetMakerType, "InitializeMoveset");
        }

        [HarmonyPostfix]
        private static void Postfix(MovesetData moveset)
        {
            try
            {
                ApplyBaseMoveset(moveset);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to apply VoidCrest moveset compatibility patch: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(HeroController), "Start")]
    private static class HeroControllerStartPreparePatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            try
            {
                PrepareVoidHeroConfigForStart();
            }
            catch (Exception ex)
            {
                Log.Error($"Failed preparing VoidCrest hero config before HeroController.Start: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(HeroController), "Start")]
    private static class HeroControllerStartLogPatch
    {
        [HarmonyPostfix]
        private static void Postfix(HeroController __instance)
        {
            if (!ShouldApplyCompat())
            {
                return;
            }

            string baseCrestName = GetActiveBaseCrest();
            LogHeroState(__instance, baseCrestName);
        }
    }

    [HarmonyPatch(typeof(HeroController), "ResetAllCrestState", new[] { typeof(bool) })]
    private static class HeroControllerResetAllCrestStatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(HeroController __instance)
        {
            try
            {
                ReapplyVoidConfigAfterReset(__instance);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed reapplying VoidCrest config after ResetAllCrestState: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(HeroController), "UpdateConfig")]
    private static class HeroControllerUpdateConfigPatch
    {
        [HarmonyPostfix]
        private static void Postfix(HeroController __instance)
        {
            try
            {
                ForceResolvedBaseConfigGroup(__instance);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed resolving the selected Void base crest after UpdateConfig: {ex}");
            }
        }
    }

    [HarmonyPatch]
    private static class VanillaBaseCrestFunctionPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodBase?[] methods =
            {
                AccessTools.PropertyGetter(typeof(HeroController), nameof(HeroController.IsWandererLucky)),
                AccessTools.Method(typeof(HeroController), "Attack", new[] { typeof(AttackDirection) }),
                AccessTools.Method(typeof(HeroController), nameof(HeroController.NailHitEnemy)),
                AccessTools.Method(typeof(HeroController), nameof(HeroController.BindCompleted)),
                AccessTools.Method(typeof(HeroController), "Recoil", new[] { typeof(bool), typeof(bool) }),
                AccessTools.Method(typeof(HeroController), nameof(HeroController.RecoilDown)),
                AccessTools.Method(typeof(HeroController), "FallCheck"),
                AccessTools.Method(typeof(DamageEnemies), nameof(DamageEnemies.DoDamage), new[] { typeof(GameObject), typeof(bool) }),
                AccessTools.Method(typeof(HealthManager), "TakeDamage", new[] { typeof(HitInstance) }),
                AccessTools.Method(typeof(HeroAnimationController), "UpdateToolEquipFlags"),
                AccessTools.Method(typeof(SurfaceWaterRegion), "OnTriggerEnter2D", new[] { typeof(Collider2D) }),
                AccessTools.Method(typeof(BouncePod), nameof(BouncePod.WillRespond), new[] { typeof(HitInstance) }),
                AccessTools.Method(typeof(BouncePod), nameof(BouncePod.Hit), new[] { typeof(HitInstance) })
            };

            foreach (MethodBase? method in methods)
            {
                if (method != null)
                {
                    yield return method;
                }
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            MethodInfo? isEquippedGetter = AccessTools.PropertyGetter(typeof(ToolBase), nameof(ToolBase.IsEquipped));
            MethodInfo crestProxy = AccessTools.Method(typeof(Plugin), nameof(IsBaseCrestFunctionallyEquipped));
            MethodInfo stringEquality = AccessTools.Method(typeof(string), "op_Equality", new[] { typeof(string), typeof(string) });
            MethodInfo stringProxy = AccessTools.Method(typeof(Plugin), nameof(BaseAwareStringEquals));
            MethodInfo? wandererCritChanceGetter = AccessTools.PropertyGetter(typeof(GlobalSettings.Gameplay), nameof(GlobalSettings.Gameplay.WandererCritChance));
            MethodInfo wandererCritChanceProxy = AccessTools.Method(typeof(Plugin), nameof(GetWandererCritChance));
            MethodInfo? floatRandomRange = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), new[] { typeof(float), typeof(float) });
            MethodInfo trackedRandomRangeProxy = AccessTools.Method(typeof(Plugin), nameof(GetTrackedWandererRandomRange));
            int crestChecksReplaced = 0;
            int stringChecksReplaced = 0;
            int critChanceChecksReplaced = 0;
            int floatRandomCallsSeen = 0;

            foreach (CodeInstruction instruction in instructions)
            {
                if (isEquippedGetter != null && instruction.Calls(isEquippedGetter))
                {
                    crestChecksReplaced++;
                    yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, crestProxy)
                        .MoveLabelsFrom(instruction)
                        .MoveBlocksFrom(instruction);
                }
                else if (instruction.Calls(stringEquality))
                {
                    stringChecksReplaced++;
                    yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, stringProxy)
                        .MoveLabelsFrom(instruction)
                        .MoveBlocksFrom(instruction);
                }
                else if (__originalMethod.DeclaringType == typeof(DamageEnemies) &&
                         wandererCritChanceGetter != null && instruction.Calls(wandererCritChanceGetter))
                {
                    critChanceChecksReplaced++;
                    yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, wandererCritChanceProxy)
                        .MoveLabelsFrom(instruction)
                        .MoveBlocksFrom(instruction);
                }
                else if (__originalMethod.DeclaringType == typeof(DamageEnemies) &&
                         floatRandomRange != null && instruction.Calls(floatRandomRange) &&
                         floatRandomCallsSeen++ == 0)
                {
                    yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, trackedRandomRangeProxy)
                        .MoveLabelsFrom(instruction)
                        .MoveBlocksFrom(instruction);
                }
                else
                {
                    yield return instruction;
                }
            }

            Log.Info($"Restored vanilla base-crest checks in {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}: " +
                     $"equipped={crestChecksReplaced}, id={stringChecksReplaced}, wandererChance={critChanceChecksReplaced}.");
        }
    }

    [HarmonyPatch(typeof(DamageEnemies), nameof(DamageEnemies.DoDamage), new[] { typeof(GameObject), typeof(bool) })]
    private static class CrestDamageCompatibilityPatch
    {
        [HarmonyPrefix]
        private static void Prefix(DamageEnemies __instance, GameObject target, ref float __state)
        {
            __state = __instance.DamageMultiplier;
            ToolItem? tool = __instance.RepresentingTool;
            if (tool != null && tool.Type == ToolItemType.Skill && ShouldApplyShamanSpellDamageBonus())
            {
                __instance.DamageMultiplier = __state * GlobalSettings.Gameplay.SpellCrestRuneDamageMult;
            }

            if (ShouldApplyHunterVoidmassDamage(__instance))
            {
                float coefficient = PlayerData.instance.health <= 2 ? 0.35f : 0.25f;
                float voidmassMultiplier = 1f + GetCurrentVoidmass() * coefficient;
                if (IsNamedBoss(target))
                {
                    float focusMultiplier = GetCurrentHunterFocusMultiplier();
                    voidmassMultiplier = Mathf.Min(voidmassMultiplier, 3f / Mathf.Max(1f, focusMultiplier));
                }

                __instance.DamageMultiplier *= voidmassMultiplier;
            }
        }

        [HarmonyPostfix]
        private static void Postfix(DamageEnemies __instance, float __state)
        {
            __instance.DamageMultiplier = __state;
        }

        [HarmonyFinalizer]
        private static Exception? Finalizer(DamageEnemies __instance, float __state, Exception? __exception)
        {
            __instance.DamageMultiplier = __state;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.CallMethodProper), nameof(HutongGames.PlayMaker.Actions.CallMethodProper.OnEnter))]
    private static class ReaperBundlePickupPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(HutongGames.PlayMaker.Actions.CallMethodProper __instance)
        {
            if (PlayerData.instance?.CurrentCrestID != "Void" || !IsSelectedBaseCrest("Reaper") ||
                !string.Equals(__instance.methodName?.Value, nameof(HeroController.AddSilkParts), StringComparison.Ordinal) ||
                !string.Equals(__instance.behaviour?.Value, nameof(HeroController), StringComparison.Ordinal) ||
                !IsReaperBundleObject(__instance.Owner))
            {
                return true;
            }

            AwardReaperOrbVoidmass();
            __instance.Finish();
            return false;
        }
    }

    [HarmonyPatch(typeof(FlingUtils), nameof(FlingUtils.SpawnAndFling), new Type[]
    {
        typeof(FlingUtils.Config), typeof(Transform), typeof(Vector3), typeof(List<GameObject>), typeof(float)
    })]
    private static class ReaperBundleAppearancePatch
    {
        [HarmonyPrefix]
        private static void Prefix(FlingUtils.Config config, ref List<GameObject>? addToList, out ReaperOrbSpawnState? __state)
        {
            GameObject? reaperPrefab = GlobalSettings.Gameplay.ReaperBundlePrefab;
            if (!reaperPrefab || !ReferenceEquals(config.Prefab, reaperPrefab))
            {
                __state = null;
                return;
            }

            addToList ??= new List<GameObject>();
            bool shouldVoidify = PlayerData.instance?.CurrentCrestID == "Void" && IsSelectedBaseCrest("Reaper");
            __state = new ReaperOrbSpawnState(addToList, addToList.Count, shouldVoidify);
        }

        [HarmonyPostfix]
        private static void Postfix(ReaperOrbSpawnState? __state)
        {
            if (__state == null)
            {
                return;
            }

            for (int i = __state.StartIndex; i < __state.SpawnedObjects.Count; i++)
            {
                GameObject orb = __state.SpawnedObjects[i];
                if (!orb)
                {
                    continue;
                }

                if (__state.ShouldVoidify)
                {
                    orb.Voidify();
                }
                else
                {
                    // These objects are pooled. Normalize them when vanilla Reaper
                    // spawns one later so a previous Void appearance cannot leak.
                    orb.UnvoidifyRoot();
                }
            }
        }
    }

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.TakeDamage), new Type[]
    {
        typeof(GameObject), typeof(CollisionSide), typeof(int), typeof(HazardType), typeof(DamagePropertyFlags)
    })]
    [HarmonyBefore("io.dervoce.voidcrest")]
    [HarmonyPriority(Priority.First)]
    private static class ReaperRetaliationVoidmassGuardPatch
    {
        [HarmonyPrefix]
        private static void Prefix(HeroController __instance, out bool __state)
        {
            __state = suppressReaperRetaliationVoidmassGain;
            suppressReaperRetaliationVoidmassGain =
                PlayerData.instance?.CurrentCrestID == "Void" &&
                IsSelectedBaseCrest("Reaper") &&
                __instance.cState.isBinding;
        }

        [HarmonyFinalizer]
        private static Exception? Finalizer(bool __state, Exception? __exception)
        {
            suppressReaperRetaliationVoidmassGain = __state;
            return __exception;
        }
    }

    [HarmonyPatch]
    private static class VoidmassResetClearsReaperFractionPatch
    {
        private static MethodBase TargetMethod()
        {
            return VoidMassProperty?.GetSetMethod(true)
                   ?? throw new MissingMethodException("VoidCrest.voidspool.voidMass setter was not found.");
        }

        [HarmonyPrefix]
        private static void Prefix(ref int value)
        {
            if (!suppressReaperRetaliationVoidmassGain)
            {
                return;
            }

            int current = GetCurrentVoidmass();
            if (value > current)
            {
                value = current;
            }

            // This guard is for Void Crest's one retaliation payout only. Clear it
            // immediately so unrelated resource writes in the same damage call survive.
            suppressReaperRetaliationVoidmassGain = false;
        }

        [HarmonyPostfix]
        private static void Postfix(int value)
        {
            if (value <= 0 || value >= 10)
            {
                ResetReaperFractionalVoidmass(value <= 0 ? "Voidmass reset" : "Voidmass cap reached");
            }
        }
    }

    [HarmonyPatch(typeof(SilkSpool), nameof(SilkSpool.ChangeSilk))]
    [HarmonyAfter("io.dervoce.voidcrest")]
    [HarmonyPriority(Priority.Last)]
    private static class ReaperFractionalVoidmassSpoolPatch
    {
        [HarmonyPrefix]
        private static void Prefix(SilkSpool __instance, ref int silk, ref int silkParts)
        {
            if (IsVoidmassSpool(__instance) && ShouldDisplayReaperHalfVoidmass())
            {
                // Void Crest's earlier prefix resolves the integer value. Add one
                // transient display chunk; the postfix renders it as a half pip.
                silk = Math.Min(GetCurrentVoidmass() + 1, 10);
                silkParts = 0;
            }
        }

        [HarmonyPostfix]
        private static void Postfix(SilkSpool __instance)
        {
            if (!IsVoidmassSpool(__instance))
            {
                return;
            }

            ApplyReaperFractionalPipOpacity(__instance, ShouldDisplayReaperHalfVoidmass());
        }
    }

    [HarmonyPatch]
    private static class VanillaBaseCrestQueryPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(HeroController), nameof(HeroController.IsHunterCrestEquipped));
            yield return AccessTools.Method(typeof(HeroController), nameof(HeroController.IsArchitectCrestEquipped));
            yield return AccessTools.Method(typeof(HeroController), nameof(HeroController.IsShamanCrestEquipped));
        }

        [HarmonyPostfix]
        private static void Postfix(MethodBase __originalMethod, ref bool __result)
        {
            if (__result || PlayerData.instance?.CurrentCrestID != "Void")
            {
                return;
            }

            __result = __originalMethod.Name switch
            {
                nameof(HeroController.IsHunterCrestEquipped) => IsHunterBaseSelected(),
                nameof(HeroController.IsArchitectCrestEquipped) => IsSelectedBaseCrest("Architect", "Toolmaster"),
                nameof(HeroController.IsShamanCrestEquipped) => IsSelectedBaseCrest("Shaman", "Spell"),
                _ => false
            };
        }
    }

    [HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.CheckIfCrestEquipped), "get_IsTrue")]
    private static class CrestAttackAudioFsmCheckPatch
    {
        [HarmonyPostfix]
        private static void Postfix(HutongGames.PlayMaker.Actions.CheckIfCrestEquipped __instance, ref bool __result)
        {
            if (__result || PlayerData.instance?.CurrentCrestID != "Void")
            {
                return;
            }

            HeroController? hero = HeroController.instance;
            ToolCrest? checkedCrest = __instance.Crest?.Value as ToolCrest;
            if (hero == null || checkedCrest == null || !DoesSelectedBaseRepresentCrest(checkedCrest.name))
            {
                return;
            }

            // Charge Slash release audio lives in the private Nail Arts FSM;
            // Challenge/Beast voice routing lives in the other two FSMs.
            // Never proxy checks in spellControl: that would leak Shaman's
            // grounded Bind, Architect Craft Bind, or Witch roots into Void.
            HutongGames.PlayMaker.Fsm? actionFsm = __instance.Fsm;
            bool isShamanGroundBindRoute = ReferenceEquals(actionFsm, hero.spellControl?.Fsm) &&
                                            string.Equals(checkedCrest.name, "Spell", StringComparison.Ordinal) &&
                                            IsSelectedBaseCrest("Spell", "Shaman") &&
                                            !VoidCrestPlugin.doingSpecialAttack;
            if (isShamanGroundBindRoute)
            {
                __result = true;
                string bindStateName = __instance.State?.Name ?? "<unknown>";
                string bindLogKey = $"ShamanBindFSM:{bindStateName}";
                if (LoggedDashAliases.Add(bindLogKey))
                {
                    Log.Info($"Restored Shaman grounded Bind branch in FSM 'Spell Control', state '{bindStateName}'.");
                }
                return;
            }

            bool isAudioRoutingFsm = ReferenceEquals(actionFsm, hero.crestAttacksFSM?.Fsm) ||
                                     ReferenceEquals(actionFsm, hero.silkSpecialFSM?.Fsm) ||
                                     string.Equals(actionFsm?.Name, "Nail Arts", StringComparison.Ordinal);
            if (!isAudioRoutingFsm)
            {
                return;
            }

            __result = true;
            string fsmName = actionFsm?.Name ?? "<unknown>";
            string stateName = __instance.State?.Name ?? "<unknown>";
            string key = $"AudioFSM:{fsmName}:{stateName}:{checkedCrest.name}";
            if (LoggedDashAliases.Add(key))
            {
                Log.Info($"Restored selected crest audio branch in FSM '{fsmName}', state '{stateName}', crest '{checkedCrest.name}'.");
            }
        }
    }

    [HarmonyPatch(typeof(HeroAnimationController), nameof(HeroAnimationController.GetClip))]
    private static class HeroAnimationControllerGetClipPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(HeroAnimationController __instance, string clipName, ref tk2dSpriteAnimationClip __result)
        {
            if (PlayerData.instance?.CurrentCrestID != "Void")
            {
                return true;
            }

            string? fallbackName = clipName switch
            {
                "Shadow Dash" => "Dash",
                "Shadow Dash Down" => "Dash Down",
                _ => null
            };

            if (fallbackName == null)
            {
                return true;
            }

            tk2dSpriteAnimationClip fallback = __instance.GetClip(fallbackName);
            if (fallback == null)
            {
                return true;
            }

            // Keep the requested Shadow Dash name on the cloned clip. Mapping the
            // request directly to Dash makes HeroAnimationController believe the
            // requested clip never started, so it restarts frame zero every update.
            __result = new tk2dSpriteAnimationClip(fallback)
            {
                name = clipName
            };

            if (LoggedDashAliases.Add(clipName))
            {
                Log.Info($"Aliased missing animation '{clipName}' to '{fallbackName}' while preserving clip playback state.");
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(NailSlash), "StartSlash")]
    private static class NailSlashStartPatch
    {
        [HarmonyPostfix]
        private static void Postfix(NailSlash __instance)
        {
            try
            {
                RefreshLiveSlashVisuals(__instance);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed refreshing NailSlash visuals: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(NailAttackBase), nameof(NailAttackBase.OnSlashStarting))]
    private static class NailAttackBaseSlashStartingPatch
    {
        [HarmonyPostfix]
        private static void Postfix(NailAttackBase __instance)
        {
            try
            {
                RefreshLiveSlashVisuals(__instance);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed refreshing base nail attack visuals: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(NailAttackBase), nameof(NailAttackBase.SetNailImbuement))]
    private static class NailAttackBaseImbuementPatch
    {
        [HarmonyPostfix]
        private static void Postfix(NailAttackBase __instance)
        {
            try
            {
                RefreshLiveSlashVisuals(__instance);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed hijacking nail imbuement tint: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(Downspike), "StartSlash")]
    private static class DownspikeStartPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Downspike __instance)
        {
            try
            {
                RefreshLiveSlashVisuals(__instance);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed refreshing Downspike visuals: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(HeroExtraNailSlash), "OnEnable")]
    private static class HeroExtraNailSlashEnablePatch
    {
        [HarmonyPostfix]
        private static void Postfix(HeroExtraNailSlash __instance)
        {
            try
            {
                LogHeroExtraNailSlashEnable(__instance);
                RefreshLiveSlashVisuals(__instance);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed refreshing HeroExtraNailSlash visuals: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(HeroExtraNailSlash), "OnEnable")]
    private static class HeroExtraNailSlashChargeLogResetPatch
    {
        [HarmonyPostfix]
        private static void Postfix(HeroExtraNailSlash __instance)
        {
            try
            {
                if (__instance != null && IsChargeSlashObject(__instance.gameObject))
                {
                    liveSlashRetintLogCount = 0;
                    Log.Info("Reset retint target logging for ChargeSlash.");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed resetting ChargeSlash retint log window: {ex}");
            }
        }
    }

    private static bool ApplyBaseMoveset(MovesetData moveset)
    {
        if (moveset == null || !ReferenceEquals(moveset, VoidCrestPlugin.voidCrestData?.Moveset))
        {
            return false;
        }

        HeroController? hero = HeroController.instance;
        CrestData? voidCrestData = VoidCrestPlugin.voidCrestData;
        if (hero == null || voidCrestData?.Moveset == null || voidCrestData.Moveset.ConfigGroup == null || !ShouldApplyCompat())
        {
            return false;
        }

        string baseCrestName = GetActiveBaseCrest();
        ToolCrest? baseCrest = ToolItemManager.GetCrestByName(baseCrestName);
        if (!baseCrest)
        {
            Log.Warning($"Base crest '{baseCrestName}' was not found at gameplay start.");
            return false;
        }

        HeroController.ConfigGroup? sourceConfigGroup = ResolveSourceConfigGroup(hero, baseCrestName, baseCrest);
        if (sourceConfigGroup == null)
        {
            Log.Warning($"No live ConfigGroup was found for base crest '{baseCrestName}'.");
            return false;
        }

        MovesetData voidMoveset = voidCrestData.Moveset;
        HeroController.ConfigGroup targetConfigGroup = voidMoveset.ConfigGroup;
        HeroConfigNeedleforge? currentVoidConfig = voidMoveset.HeroConfig;
        HeroConfigNeedleforge.FsmEdit? preservedDashEdit = currentVoidConfig?.DashSlashFsmEdit;

        HeroConfigNeedleforge replacementConfig = CloneHeroConfig(sourceConfigGroup.Config);
        replacementConfig.name = voidCrestData.name;
        SetDashSlashFsmEditSilently(replacementConfig, preservedDashEdit);
        voidMoveset.HeroConfig = replacementConfig;

        LogConfigGroupDetails("Source", sourceConfigGroup);
        ReplaceConfigGroupRoot(targetConfigGroup, sourceConfigGroup, preserveDashStab: true);
        RestoreTransplantedAttackAudio(sourceConfigGroup, targetConfigGroup);
        NormalizeTransplantedDownslashConfig(replacementConfig, targetConfigGroup.DownSlashObject);

        // Reconnect the custom dash object that VoidCrest already created.
        if (voidMoveset.DashSlash != null)
        {
            targetConfigGroup.DashStab = EnsureDashRoot(voidMoveset.DashSlash.CreateGameObject(targetConfigGroup.ActiveRoot, hero), targetConfigGroup.ActiveRoot);
        }

        Log.Info(
            $"Downslash config for '{baseCrestName}': source={DescribeDownslashConfig(sourceConfigGroup.Config)}, " +
            $"replacement={DescribeDownslashConfig(replacementConfig)}, target={DescribeDownslashConfig(targetConfigGroup.Config)}");
        targetConfigGroup.Setup();
        ApplyChargeSlashTintPolicy(baseCrestName, sourceConfigGroup.ChargeSlash, targetConfigGroup.ChargeSlash);
        LogConfigGroupDetails("Target", targetConfigGroup);
        LogChargeSlashDetails("Source", sourceConfigGroup.ChargeSlash);
        LogChargeSlashDetails("Target", targetConfigGroup.ChargeSlash);
        Log.Info($"Applied base crest '{baseCrestName}' attack objects to VoidCrest.");
        return true;
    }

    private static bool GenerateVoidCrestBaseCrestMenuElement(ConfigEntryBase entry, out MenuElement menuElement)
    {
        menuElement = null!;

        if (entry is not ConfigEntry<string> stringEntry ||
            !string.Equals(entry.Definition.Section, VoidCrestBaseCrestSection, StringComparison.Ordinal) ||
            !string.Equals(entry.Definition.Key, VoidCrestBaseCrestKey, StringComparison.Ordinal))
        {
            return false;
        }

        string configPath = entry.ConfigFile?.ConfigFilePath ?? string.Empty;
        if (!string.Equals(Path.GetFileName(configPath), VoidCrestConfigFileName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        List<string> choices = new(OrderedBaseCrests);
        string currentValue = CanonicalizeConfiguredBaseCrest(stringEntry.Value?.Trim() ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(currentValue) && !choices.Contains(currentValue, StringComparer.Ordinal))
        {
            choices.Insert(0, currentValue);
        }

        ChoiceElement<string> choiceElement = new(
            entry.LabelName(),
            ChoiceModels.ForValues(choices),
            entry.DescriptionLine());
        choiceElement.SynchronizeWith(stringEntry);
        menuElement = choiceElement;
        return true;
    }

    private void TrackBenchDeferredBaseCrest()
    {
        string? currentBaseCrest = NormalizeQueuedBaseCrest(GetConfiguredBaseCrest());
        if (currentBaseCrest == null)
        {
            return;
        }

        if (!string.Equals(currentBaseCrest, observedBaseCrest, StringComparison.Ordinal))
        {
            observedBaseCrest = currentBaseCrest;
            pendingBaseCrest = currentBaseCrest;

            if (PlayerData.instance?.atBench == true)
            {
                applyPendingOnBenchExit = true;
                applyPendingOnBenchEnter = false;
                Log.Info($"Queued base crest '{pendingBaseCrest}' to apply when leaving the bench.");
            }
            else
            {
                applyPendingOnBenchEnter = true;
                applyPendingOnBenchExit = false;
                Log.Info($"Queued base crest '{pendingBaseCrest}' to apply on the next bench rest.");
            }
        }

        bool isAtBench = PlayerData.instance?.atBench == true;
        if (!wasAtBench && isAtBench && applyPendingOnBenchEnter)
        {
            ApplyPendingBenchChange("bench enter");
        }
        else if (wasAtBench && !isAtBench && applyPendingOnBenchExit)
        {
            ApplyPendingBenchChange("bench exit");
        }

        wasAtBench = isAtBench;
    }

    private void ApplyPendingBenchChange(string trigger)
    {
        string crestToApply = pendingBaseCrest ?? activeBaseCrest;
        string previousBaseCrest = activeBaseCrest;
        activeBaseCrest = crestToApply;

        Log.Info($"Applying queued base crest '{crestToApply}' on {trigger}.");
        if (TryApplyCurrentBaseCrest())
        {
            if (string.Equals(previousBaseCrest, "Reaper", StringComparison.Ordinal) &&
                !string.Equals(crestToApply, "Reaper", StringComparison.Ordinal))
            {
                ResetReaperFractionalVoidmass("base crest changed");
            }

            pendingBaseCrest = null;
            applyPendingOnBenchEnter = false;
            applyPendingOnBenchExit = false;
            return;
        }

        activeBaseCrest = previousBaseCrest;
        Log.Error($"Base crest switch to '{crestToApply}' failed safely; the previous moveset remains active and the change stays queued.");
    }

    private static bool TryApplyCurrentBaseCrest()
    {
        CrestData? voidCrestData = VoidCrestPlugin.voidCrestData;
        MovesetData? moveset = voidCrestData?.Moveset;
        HeroController? hero = HeroController.instance;
        if (moveset == null || hero == null)
        {
            return false;
        }

        try
        {
            if (!ApplyBaseMoveset(moveset))
            {
                return false;
            }

            if (PlayerData.instance?.CurrentCrestID == "Void")
            {
                ReapplyVoidConfigAfterReset(hero);
                ForceResolvedBaseConfigGroup(hero);
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed applying live Void base crest switch: {ex}");
            return false;
        }
    }

    private static string GetConfiguredBaseCrest()
    {
        return CanonicalizeConfiguredBaseCrest(VoidCrestPlugin.BaseCrest?.Value?.Trim() ?? string.Empty);
    }

    private static string GetActiveBaseCrest()
    {
        return string.Equals(activeBaseCrest, "Hunter", StringComparison.Ordinal)
            ? ResolvePlayerHunterVersion()
            : activeBaseCrest;
    }

    private static string? NormalizeQueuedBaseCrest(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string canonical = CanonicalizeConfiguredBaseCrest(value!.Trim());
        return ApprovedBaseCrests.Contains(canonical) ? canonical : null;
    }

    private static void CanonicalizeHunterConfig()
    {
        ConfigEntry<string>? baseCrest = VoidCrestPlugin.BaseCrest;
        if (baseCrest == null)
        {
            return;
        }

        string original = baseCrest.Value?.Trim() ?? string.Empty;
        string canonical = CanonicalizeConfiguredBaseCrest(original);
        if (!string.Equals(original, canonical, StringComparison.Ordinal))
        {
            baseCrest.Value = canonical;
            Log.Info($"Normalized Void Base Crest '{original}' to '{canonical}'. Hunter follows the player's vanilla evolution.");
        }
    }

    private static string CanonicalizeConfiguredBaseCrest(string value)
    {
        return IsHunterVariantName(value) ? "Hunter" : value;
    }

    private static bool IsHunterVariantName(string value)
    {
        return string.Equals(value, "Hunter_V2", StringComparison.Ordinal) ||
               string.Equals(value, "Hunter_V3", StringComparison.Ordinal) ||
               string.Equals(value, "Hunter_v2", StringComparison.Ordinal) ||
               string.Equals(value, "Hunter_v3", StringComparison.Ordinal);
    }

    private static bool IsHunterBaseSelected()
    {
        return string.Equals(activeBaseCrest, "Hunter", StringComparison.Ordinal);
    }

    private static bool ShouldApplyHunterVoidmassDamage(DamageEnemies damage)
    {
        if (PlayerData.instance?.CurrentCrestID != "Void" || !IsHunterBaseSelected() ||
            damage.RepresentingTool != null || VoidCrestPlugin.doingSpecialAttack ||
            !IsCurrentHunterFocusActive())
        {
            return false;
        }

        // Match vanilla Hunter's Nail eligibility while explicitly excluding
        // Tools and Void's special/retaliation paths.
        return damage.attackType == AttackTypes.Nail ||
               damage.attackType == AttackTypes.NailBeam ||
               DamageEnemies.IsNailAttackObject(damage.gameObject);
    }

    private static bool IsCurrentHunterFocusActive()
    {
        HeroController? hero = HeroController.instance;
        if (hero == null)
        {
            return false;
        }

        int meterHits = hero.HunterUpgState.CurrentMeterHits;
        string hunterVersion = ResolvePlayerHunterVersion();
        if (string.Equals(hunterVersion, "Hunter_v3", StringComparison.Ordinal))
        {
            return meterHits >= GlobalSettings.Gameplay.HunterCombo2Hits;
        }

        return string.Equals(hunterVersion, "Hunter_v2", StringComparison.Ordinal) &&
               meterHits >= GlobalSettings.Gameplay.HunterComboHits;
    }

    private static float GetCurrentHunterFocusMultiplier()
    {
        HeroController? hero = HeroController.instance;
        if (hero == null)
        {
            return 1f;
        }

        int meterHits = hero.HunterUpgState.CurrentMeterHits;
        string hunterVersion = ResolvePlayerHunterVersion();
        if (string.Equals(hunterVersion, "Hunter_v3", StringComparison.Ordinal))
        {
            if (meterHits >= GlobalSettings.Gameplay.HunterCombo2Hits + GlobalSettings.Gameplay.HunterCombo2ExtraHits)
            {
                return GlobalSettings.Gameplay.HunterCombo2ExtraDamageMult;
            }

            if (meterHits >= GlobalSettings.Gameplay.HunterCombo2Hits)
            {
                return GlobalSettings.Gameplay.HunterCombo2DamageMult;
            }
        }
        else if (string.Equals(hunterVersion, "Hunter_v2", StringComparison.Ordinal) &&
                 meterHits >= GlobalSettings.Gameplay.HunterComboHits)
        {
            return GlobalSettings.Gameplay.HunterComboDamageMult;
        }

        return 1f;
    }

    private static bool IsNamedBoss(GameObject target)
    {
        if (!target)
        {
            return false;
        }

        HealthManager? health = target.GetComponent<HealthManager>() ?? target.GetComponentInParent<HealthManager>();
        if (!health)
        {
            return false;
        }

        int instanceId = health.GetInstanceID();
        if (ConfirmedNamedBosses.Contains(instanceId))
        {
            return true;
        }

        // These boss-overhaul mods retain exact runtime identities even when they bypass
        // the vanilla boss-title, journal, or BossSceneController registration paths.
        if (BossOverhaulObjectNames.Contains(health.gameObject.name))
        {
            ConfirmNamedBoss(health, $"boss overhaul object '{health.gameObject.name}'");
            return true;
        }

        IEnumerable<MonoBehaviour> bossBehaviours = health.GetComponentsInParent<MonoBehaviour>(true)
            .Concat(health.GetComponentsInChildren<MonoBehaviour>(true))
            .Distinct();
        foreach (MonoBehaviour behaviour in bossBehaviours)
        {
            string componentType = behaviour != null ? behaviour.GetType().FullName ?? string.Empty : string.Empty;
            if (BossOverhaulComponentTypes.Contains(componentType))
            {
                ConfirmNamedBoss(health, $"boss overhaul component '{componentType}'");
                return true;
            }
        }

        BossSceneController? bossScene = BossSceneController.Instance;
        if (bossScene &&
            (bossScene.BossHealthLookup.ContainsKey(health) ||
             (bossScene.bosses != null && bossScene.bosses.Contains(health))))
        {
            ConfirmNamedBoss(health, "BossSceneController");
            return true;
        }

        // Boss-title actions are the strongest normal-world signal and cover
        // story fights such as Cradle Lace that are absent from BossSceneController.
        IEnumerable<PlayMakerFSM> fsms = health.GetComponentsInParent<PlayMakerFSM>(true)
            .Concat(health.GetComponentsInChildren<PlayMakerFSM>(true))
            .Distinct();
        foreach (PlayMakerFSM fsm in fsms)
        {
            foreach (HutongGames.PlayMaker.FsmState state in fsm.FsmStates)
            {
                foreach (HutongGames.PlayMaker.FsmStateAction action in state.Actions)
                {
                    if (action is HutongGames.PlayMaker.Actions.DisplayBossTitle)
                    {
                        ConfirmNamedBoss(health, $"boss title FSM '{fsm.FsmName}'");
                        return true;
                    }

                    EnemyJournalRecord? record = action switch
                    {
                        RecordJournalKill oldRecord => oldRecord.Record?.Value as EnemyJournalRecord,
                        RecordJournalKillV2 currentRecord => currentRecord.Record?.Value as EnemyJournalRecord,
                        _ => null
                    };
                    if (record && record.KillsRequired <= 1)
                    {
                        ConfirmNamedBoss(health, $"one-kill journal record '{record.name}'");
                        return true;
                    }
                }
            }
        }

        // Until the game exposes a universal boss flag, recognize names used
        // by community boss lists against the runtime hierarchy. Matching a
        // HealthManager avoids treating harmless NPC appearances as bosses.
        string hierarchyName = GetObjectPath(health.transform);
        string normalizedHierarchyName = NormalizeBossIdentity(hierarchyName);
        foreach (string alias in CommunityBossAliases)
        {
            if (normalizedHierarchyName.Contains(NormalizeBossIdentity(alias)))
            {
                ConfirmNamedBoss(health, $"community boss alias '{alias}'");
                return true;
            }
        }

        return false;
    }

    private static void ConfirmNamedBoss(HealthManager health, string reason)
    {
        if (ConfirmedNamedBosses.Add(health.GetInstanceID()))
        {
            Log.Info($"Hunter boss cap recognized '{GetObjectPath(health.transform)}' via {reason}.");
        }
    }

    private static string NormalizeBossIdentity(string value)
    {
        return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    private static void SetMasksForHunterCriticalDamageDebug()
    {
        if (resettingMaskDebugAction || SetMasksToCriticalDamage == null || !SetMasksToCriticalDamage.Value)
        {
            return;
        }

        try
        {
            HeroController? hero = HeroController.instance;
            PlayerData? player = PlayerData.instance;
            if (hero != null && player != null && player.CurrentCrestID == "Void" && IsHunterBaseSelected())
            {
                int targetHealth = Mathf.Min(2, player.CurrentMaxHealth);
                if (player.health > targetHealth)
                {
                    // Include temporary blue health so it cannot absorb the
                    // debug adjustment before red Masks reach the target.
                    hero.TakeHealth(player.health - targetHealth + player.healthBlue);
                }
                else if (player.health < targetHealth)
                {
                    hero.AddHealth(targetHealth - player.health);
                }

                Log.Info($"Hunter debug set current health to {player.health} Masks (critical coefficient active at 2 or less).");
            }
            else
            {
                Log.Info("Hunter critical-Masks debug action ignored because Void Hunter is not active.");
            }
        }
        finally
        {
            resettingMaskDebugAction = true;
            SetMasksToCriticalDamage.Value = false;
            resettingMaskDebugAction = false;
        }
    }

    private static string lastAppliedHunterFocusSelection = "Off";

    private static void ActivateHunterFocusLevelDebug(string requestedLevel)
    {
        if (resettingHunterFocusSelection || ActivateHunterFocusLevel == null)
        {
            return;
        }

        HeroController? hero = HeroController.instance;
        bool hunterActive = hero != null && PlayerData.instance?.CurrentCrestID == "Void" && IsHunterBaseSelected();
        ToolCrest? hunterV2 = ToolItemManager.GetCrestByName("Hunter_v2");
        ToolCrest? hunterV3 = ToolItemManager.GetCrestByName("Hunter_v3");
        bool hasV3 = hunterV3 && hunterV3.IsUnlocked;
        bool hasV2OrV3 = hasV3 || (hunterV2 && hunterV2.IsUnlocked);
        bool allowed = requestedLevel switch
        {
            "Off" => hunterActive,
            "Focus 1" => hunterActive && hasV2OrV3,
            "Focus 2" => hunterActive && hasV3,
            _ => false
        };

        if (!allowed || hero == null || HunterUpgStateField == null)
        {
            Log.Info($"Hunter debug '{requestedLevel}' ignored: the active Hunter evolution does not support that Focus level.");
            resettingHunterFocusSelection = true;
            ActivateHunterFocusLevel.Value = lastAppliedHunterFocusSelection;
            resettingHunterFocusSelection = false;
            return;
        }

        int meterHits = requestedLevel switch
        {
            "Focus 1" when hasV3 => GlobalSettings.Gameplay.HunterCombo2Hits,
            "Focus 1" => GlobalSettings.Gameplay.HunterComboHits,
            "Focus 2" => GlobalSettings.Gameplay.HunterCombo2Hits + GlobalSettings.Gameplay.HunterCombo2ExtraHits,
            _ => 0
        };
        HeroController.HunterUpgCrestStateInfo state = hero.HunterUpgState;
        state.CurrentMeterHits = meterHits;
        HunterUpgStateField.SetValue(hero, state);
        lastAppliedHunterFocusSelection = requestedLevel;
        Log.Info($"Hunter debug set {requestedLevel} using vanilla combo meter value {meterHits}.");
    }

    private static string ResolvePlayerHunterVersion()
    {
        string resolved = "Hunter";
        if (PlayerData.instance != null)
        {
            ToolCrest? hunterV3 = ToolItemManager.GetCrestByName("Hunter_v3");
            ToolCrest? hunterV2 = ToolItemManager.GetCrestByName("Hunter_v2");
            if (hunterV3 && hunterV3.IsUnlocked)
            {
                resolved = hunterV3.name;
            }
            else if (hunterV2 && hunterV2.IsUnlocked)
            {
                resolved = hunterV2.name;
            }
        }

        if (!string.Equals(resolved, lastReportedHunterVersion, StringComparison.Ordinal))
        {
            lastReportedHunterVersion = resolved;
            Log.Info($"Resolved canonical Hunter base crest to vanilla progression asset '{resolved}'.");
        }

        return resolved;
    }

    private static void PrepareVoidHeroConfigForStart()
    {
        if (!ShouldApplyCompat() || PlayerData.instance?.CurrentCrestID != "Void")
        {
            return;
        }

        CrestData? voidCrestData = VoidCrestPlugin.voidCrestData;
        ToolCrest? voidToolCrest = voidCrestData?.ToolCrest;
        if (!voidToolCrest)
        {
            return;
        }

        ToolCrest? baseCrest = ToolItemManager.GetCrestByName(GetActiveBaseCrest());
        if (!baseCrest || baseCrest.HeroConfig == null)
        {
            return;
        }

        ToolCrestHeroConfigField?.SetValue(voidToolCrest, baseCrest.HeroConfig);
    }

    private static void ReapplyVoidConfigAfterReset(HeroController hero)
    {
        if (!ShouldApplyCompat() || PlayerData.instance?.CurrentCrestID != "Void")
        {
            return;
        }

        CrestData? voidCrestData = VoidCrestPlugin.voidCrestData;
        HeroConfigNeedleforge? voidConfig = voidCrestData?.Moveset?.HeroConfig;
        if (voidConfig == null)
        {
            return;
        }

        CrestConfigField?.SetValue(hero, voidConfig);
        UpdateConfigMethod?.Invoke(hero, Array.Empty<object>());
        Log.Info($"Reapplied Void compat config after ResetAllCrestState: {DescribeDownslashConfig(voidConfig)}");
    }

    private static void ForceResolvedBaseConfigGroup(HeroController hero)
    {
        if (!ShouldApplyCompat() || PlayerData.instance?.CurrentCrestID != "Void")
        {
            return;
        }

        string baseCrestName = GetActiveBaseCrest();
        ToolCrest? baseCrest = ToolItemManager.GetCrestByName(baseCrestName);
        if (!baseCrest)
        {
            return;
        }

        HeroController.ConfigGroup? resolvedGroup = ResolveSourceConfigGroup(hero, baseCrestName, baseCrest);
        HeroController.ConfigGroup? currentGroup = CurrentConfigGroupProperty?.GetValue(hero) as HeroController.ConfigGroup;
        if (resolvedGroup == null || ReferenceEquals(resolvedGroup, currentGroup))
        {
            return;
        }

        SetConfigGroupMethod?.Invoke(hero, new object?[] { resolvedGroup, null });
        Log.Info($"Resolved active config group to '{resolvedGroup.Config?.name ?? "<null>"}' for Void base crest '{baseCrestName}'.");
    }

    private static void SetDashSlashFsmEditSilently(
        HeroConfigNeedleforge config,
        HeroConfigNeedleforge.FsmEdit? edit)
    {
        if (DashFsmEditField != null)
        {
            DashFsmEditField.SetValue(config, edit);
            return;
        }

        config.DashSlashFsmEdit = edit;
    }

    private static bool ShouldApplyCompat()
    {
        string baseCrestName = GetActiveBaseCrest();
        if (string.IsNullOrWhiteSpace(baseCrestName))
        {
            return false;
        }

        return true;
    }

    private static bool IsBaseCrestFunctionallyEquipped(ToolBase tool)
    {
        if (tool == null)
        {
            return false;
        }

        if (tool.IsEquipped)
        {
            return true;
        }

        ToolCrest? crest = tool as ToolCrest;
        return PlayerData.instance?.CurrentCrestID == "Void" &&
               crest != null && IsSelectedBaseCrest(crest.name);
    }

    private static bool ShouldApplyShamanSpellDamageBonus()
    {
        return PlayerData.instance?.CurrentCrestID == "Void" &&
               IsSelectedBaseCrest("Spell", "Shaman") &&
               (ShamanSpellDamageBonus == null || ShamanSpellDamageBonus.Value);
    }

    private static float GetWandererCritChance()
    {
        if (PlayerData.instance?.CurrentCrestID == "Void" &&
            IsSelectedBaseCrest("Wanderer"))
        {
            HeroController? hero = HeroController.instance;
            if (WandererFiftyPercentCritChance != null && WandererFiftyPercentCritChance.Value)
            {
                // DamageEnemies applies GetLuckModifier after this getter. Divide
                // it out so the debug option remains an actual final 50% chance.
                float luckModifier = hero != null ? hero.GetLuckModifier() : 1f;
                return luckModifier > Mathf.Epsilon ? 0.5f / luckModifier : 0.5f;
            }

            // Vanilla contributes the initial 2%. Without Dice, 3.1 points per
            // Voidmass makes the base 10 Voidmass land at exactly 33%.
            // Magnetite Dice is deliberately excluded here because vanilla
            // applies its luck multiplier downstream of this getter.
            const float voidmassCoefficient = 0.031f;
            return GlobalSettings.Gameplay.WandererCritChance + GetCurrentVoidmass() * voidmassCoefficient;
        }

        return GlobalSettings.Gameplay.WandererCritChance;
    }

    private static int GetCurrentVoidmass()
    {
        try
        {
            return VoidMassField?.GetValue(null) is int value ? Math.Max(0, value) : 0;
        }
        catch (Exception ex)
        {
            string key = $"VoidmassRead:{ex.GetType().FullName}";
            if (LoggedDashAliases.Add(key))
            {
                Log.Warning($"Could not read Voidmass for branch compatibility: {ex.Message}");
            }
            return 0;
        }
    }

    private static bool IsVoidmassSpool(SilkSpool? spool)
    {
        if (!spool)
        {
            return false;
        }

        GameObject? knownClone = VoidSpoolCloneField?.GetValue(null) as GameObject;
        return (knownClone && ReferenceEquals(spool.gameObject, knownClone)) ||
               string.Equals(spool.gameObject.name, "Spool_VoidVersion", StringComparison.Ordinal);
    }

    private static bool ShouldDisplayReaperHalfVoidmass()
    {
        return reaperHalfVoidmassPending && GetCurrentVoidmass() < 10 &&
               PlayerData.instance?.CurrentCrestID == "Void" && IsSelectedBaseCrest("Reaper");
    }

    private static void RefreshReaperFractionalVoidmassDisplay()
    {
        try
        {
            GameObject? spoolObject = VoidSpoolCloneField?.GetValue(null) as GameObject;
            SilkSpool? spool = spoolObject ? spoolObject.GetComponent<SilkSpool>() : null;
            if (spool)
            {
                spool.ChangeSilk(
                    GetCurrentVoidmass(),
                    0,
                    SilkSpool.SilkAddSource.Normal,
                    SilkSpool.SilkTakeSource.Normal);
            }
        }
        catch (Exception ex)
        {
            string key = $"ReaperFractionDisplay:{ex.GetType().FullName}";
            if (LoggedDashAliases.Add(key))
            {
                Log.Warning($"Could not refresh Reaper's half-Voidmass pip: {ex.GetBaseException().Message}");
            }
        }
    }

    private static void ApplyReaperFractionalPipOpacity(SilkSpool spool, bool showHalf)
    {
        SilkChunk? halfChunk = null;
        if (showHalf && SilkChunksField?.GetValue(spool) is IList chunks && chunks.Count > 0)
        {
            halfChunk = chunks[chunks.Count - 1] as SilkChunk;
        }

        if (reaperFractionalDisplayChunk && !ReferenceEquals(reaperFractionalDisplayChunk, halfChunk))
        {
            SetSilkChunkOpacity(reaperFractionalDisplayChunk, 1f);
        }

        reaperFractionalDisplayChunk = halfChunk;
        if (halfChunk)
        {
            SetSilkChunkOpacity(halfChunk, 0.5f);
        }
    }

    private static void SetSilkChunkOpacity(SilkChunk chunk, float opacity)
    {
        tk2dSprite? sprite = chunk.GetComponent<tk2dSprite>();
        if (!sprite)
        {
            return;
        }

        Color color = sprite.color;
        color.a = opacity;
        sprite.color = color;
    }

    private static bool IsReaperBundleObject(GameObject? actionOwner)
    {
        GameObject? prefab = GlobalSettings.Gameplay.ReaperBundlePrefab;
        if (!actionOwner || !prefab)
        {
            return false;
        }

        string prefabName = RemoveCloneSuffix(prefab.name);
        for (Transform? current = actionOwner.transform; current != null; current = current.parent)
        {
            if (ReferenceEquals(current.gameObject, prefab) ||
                string.Equals(RemoveCloneSuffix(current.gameObject.name), prefabName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string RemoveCloneSuffix(string objectName)
    {
        const string cloneSuffix = "(Clone)";
        return objectName.EndsWith(cloneSuffix, StringComparison.Ordinal)
            ? objectName.Substring(0, objectName.Length - cloneSuffix.Length).TrimEnd()
            : objectName;
    }

    private static void AwardReaperOrbVoidmass()
    {
        int current = Mathf.Clamp(GetCurrentVoidmass(), 0, 10);
        if (current >= 10)
        {
            ResetReaperFractionalVoidmass("Voidmass cap reached");
            Log.Info("Collected Reaper Voidmass orb at the 10-Voidmass cap; no Silk was awarded.");
            return;
        }

        if (!reaperHalfVoidmassPending)
        {
            reaperHalfVoidmassPending = true;
            RefreshReaperFractionalVoidmassDisplay();
            Log.Info($"Collected Reaper Voidmass orb: effective Voidmass {current + 0.5f:0.0}/10 (half-unit banked).");
            return;
        }

        reaperHalfVoidmassPending = false;
        int updated = Math.Min(current + 1, 10);
        try
        {
            if (VoidMassProperty?.CanWrite == true)
            {
                VoidMassProperty.SetValue(null, updated, null);
            }
            else if (VoidMassField != null)
            {
                VoidMassField.SetValue(null, updated);
            }
            else
            {
                Log.Warning("Reaper orb conversion could not find Void Crest's live Voidmass storage.");
                return;
            }

            Log.Info($"Collected Reaper Voidmass orb: Voidmass {updated}/10 (two half-units completed).");
        }
        catch (Exception ex)
        {
            // Void Crest's property writes its backing value before refreshing the HUD.
            // Keep that successful resource change if only the visual refresh failed.
            try
            {
                VoidMassField?.SetValue(null, updated);
            }
            catch
            {
                // Report the original failure below.
            }

            Log.Warning($"Reaper orb raised Voidmass to {updated}, but its HUD refresh failed: {ex.GetBaseException().Message}");
        }
    }

    private static void ResetReaperFractionalVoidmass(string reason)
    {
        if (!reaperHalfVoidmassPending)
        {
            return;
        }

        reaperHalfVoidmassPending = false;
        RefreshReaperFractionalVoidmassDisplay();
        Log.Info($"Cleared Reaper's banked half Voidmass: {reason}.");
    }

    private static void SetCurrentVoidmassForDebug(int requestedValue)
    {
        int value = Mathf.Clamp(requestedValue, 0, 10);
        ResetReaperFractionalVoidmass("debug Voidmass count set");
        try
        {
            if (VoidMassProperty?.CanWrite == true)
            {
                // Use Void Crest's property so its spool HUD updates along with
                // the backing count instead of changing gameplay state alone.
                VoidMassProperty.SetValue(null, value, null);
            }
            else if (VoidMassField != null)
            {
                VoidMassField.SetValue(null, value);
            }
            else
            {
                Log.Warning("Set Voidmass Count could not find Void Crest's live Voidmass storage.");
                return;
            }

            ResetWandererLuckTracker();
            Log.Info($"Debug set live Voidmass count to {value}.");
        }
        catch (Exception ex)
        {
            // Void Crest's setter writes the count before refreshing its HUD.
            // Preserve the useful state change if the HUD does not exist yet.
            try
            {
                VoidMassField?.SetValue(null, value);
            }
            catch
            {
                // Report the original setter failure below.
            }

            ResetWandererLuckTracker();
            Log.Warning($"Set live Voidmass count to {value}, but its HUD refresh failed: {ex.GetBaseException().Message}");
        }
    }

    private static float GetTrackedWandererRandomRange(float minimum, float maximum)
    {
        float roll = UnityEngine.Random.Range(minimum, maximum);
        if (WandererLuckTracker != null && WandererLuckTracker.Value &&
            PlayerData.instance?.CurrentCrestID == "Void" && IsSelectedBaseCrest("Wanderer"))
        {
            HeroController? hero = HeroController.instance;
            float chance = Mathf.Clamp01(GetWandererCritChance() * (hero != null ? hero.GetLuckModifier() : 1f));
            wandererTrackedRolls++;
            wandererLastRoll = roll;
            wandererLastChance = chance;
            wandererLastWasCrit = roll <= chance;
            if (wandererLastWasCrit)
            {
                wandererTrackedCrits++;
            }
        }
        return roll;
    }

    private static void ResetWandererLuckTracker()
    {
        wandererTrackedRolls = 0;
        wandererTrackedCrits = 0;
        wandererLastRoll = -1f;
        wandererLastChance = 0f;
        wandererLastWasCrit = false;
    }

    private static void RefreshLoadedShamanRuneEffects()
    {
        foreach (HeroShamanRuneEffect effect in UnityEngine.Object.FindObjectsByType<HeroShamanRuneEffect>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            effect.Refresh();
        }
    }

    private static bool BaseAwareStringEquals(string? left, string? right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return true;
        }

        if (PlayerData.instance?.CurrentCrestID != "Void")
        {
            return false;
        }

        if (string.Equals(left, "Void", StringComparison.Ordinal) && right != null)
        {
            return IsSelectedBaseCrest(right);
        }

        if (string.Equals(right, "Void", StringComparison.Ordinal) && left != null)
        {
            return IsSelectedBaseCrest(left);
        }

        return false;
    }

    private static bool IsSelectedBaseCrest(params string[] names)
    {
        string selected = GetActiveBaseCrest();
        foreach (string name in names)
        {
            if (string.Equals(selected, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DoesSelectedBaseRepresentCrest(string crestName)
    {
        string selected = GetActiveBaseCrest();
        if (string.Equals(selected, crestName, StringComparison.Ordinal))
        {
            return true;
        }

        if (ConfigAliases.TryGetValue(selected, out string[]? selectedAliases) &&
            selectedAliases.Contains(crestName, StringComparer.Ordinal))
        {
            return true;
        }

        // Player-facing Cursed uses the internal Whip/Witch attack family, but
        // this equivalence is deliberately confined to attack/audio routing.
        return string.Equals(selected, "Cursed", StringComparison.Ordinal) &&
               (string.Equals(crestName, "Whip", StringComparison.Ordinal) ||
                string.Equals(crestName, "Witch", StringComparison.Ordinal) ||
                string.Equals(crestName, "Cursed", StringComparison.Ordinal));
    }

    private static bool ShouldRefreshLiveSlashVisuals()
    {
        // RefreshLiveSlashVisuals restores the live slash's normal imbuement/skin
        // color. Run that restoration only when Void tint hijacking is disabled;
        // when enabled, leave VoidCrest's native tint shader in control.
        return TintHijacking != null && !TintHijacking.Value &&
               HeroController.instance != null &&
               PlayerData.instance?.CurrentCrestID == "Void";
    }

    private static void RefreshLiveSlashVisuals(Component? source)
    {
        if (source == null || !ShouldRefreshLiveSlashVisuals())
        {
            return;
        }

        GameObject root = source.gameObject;
        if (root.GetComponent<HeroController>() != null || root == HeroController.instance.gameObject)
        {
            return;
        }

        Color targetColor = GetDesiredSlashColor();
        LiveSlashVisualRetint retint = root.GetComponent<LiveSlashVisualRetint>() ?? root.AddComponent<LiveSlashVisualRetint>();
        retint.Configure(targetColor, 12);
    }

    private static void RefreshCurrentChargeSlashTintPolicy()
    {
        HeroController? hero = HeroController.instance;
        CrestData? voidCrestData = VoidCrestPlugin.voidCrestData;
        if (hero == null || voidCrestData?.Moveset?.ConfigGroup == null)
        {
            return;
        }

        string baseCrestName = GetActiveBaseCrest();
        ToolCrest? baseCrest = ToolItemManager.GetCrestByName(baseCrestName);
        HeroController.ConfigGroup? sourceGroup = baseCrest ? ResolveSourceConfigGroup(hero, baseCrestName, baseCrest) : null;
        ApplyChargeSlashTintPolicy(baseCrestName, sourceGroup?.ChargeSlash, voidCrestData.Moveset.ConfigGroup.ChargeSlash);
    }

    private static void ApplyChargeSlashTintPolicy(string baseCrestName, params GameObject?[] chargeSlashRoots)
    {
        VoidCrestColorUtil.ExemptObjects = RemoveDestroyedObjects(VoidCrestColorUtil.ExemptObjects);
        VoidCrestColorUtil.ExemptSprites = RemoveDestroyedObjects(VoidCrestColorUtil.ExemptSprites);

        bool allowNativeSpellTint = TintHijacking != null && TintHijacking.Value &&
                                    (string.Equals(baseCrestName, "Spell", StringComparison.Ordinal) ||
                                     string.Equals(baseCrestName, "Shaman", StringComparison.Ordinal));

        foreach (GameObject? root in chargeSlashRoots)
        {
            if (!root)
            {
                continue;
            }

            if (allowNativeSpellTint)
            {
                VoidCrestColorUtil.ExemptObjects = RemoveObject(VoidCrestColorUtil.ExemptObjects, root);
                VoidCrestColorUtil.ExemptSprites = RemoveObject(VoidCrestColorUtil.ExemptSprites, root);
                Log.Info($"Allowed Spell ChargeSlash visual root '{GetObjectPath(root.transform)}' through VoidCrest's tint pass.");
            }
            else
            {
                VoidCrestColorUtil.ExemptObjects = AddUniqueObject(VoidCrestColorUtil.ExemptObjects, root);
                VoidCrestColorUtil.ExemptSprites = AddUniqueObject(VoidCrestColorUtil.ExemptSprites, root);
                Log.Info($"Excluded ChargeSlash visual root '{GetObjectPath(root.transform)}' from VoidCrest's continuous recolor pass.");
            }
        }
    }

    private static GameObject[] RemoveObject(GameObject[]? objects, GameObject value)
    {
        return RemoveDestroyedObjects(objects)
            .Where(item => !ReferenceEquals(item, value))
            .ToArray();
    }

    private static GameObject[] RemoveDestroyedObjects(GameObject[]? objects)
    {
        if (objects == null || objects.Length == 0)
        {
            return Array.Empty<GameObject>();
        }

        return objects.Where(item => item).ToArray();
    }

    private static GameObject[] AddUniqueObject(GameObject[]? objects, GameObject value)
    {
        GameObject[] liveObjects = RemoveDestroyedObjects(objects);
        if (liveObjects.Any(item => ReferenceEquals(item, value)))
        {
            return liveObjects;
        }

        return liveObjects.Concat(new[] { value }).ToArray();
    }

    private static Color GetDesiredSlashColor()
    {
        NailImbuementConfig? currentImbuement = HeroController.instance?.NailImbuement?.CurrentImbuement;
        return currentImbuement != null ? currentImbuement.NailTintColor : Color.white;
    }

    private static HeroController.ConfigGroup? ResolveSourceConfigGroup(HeroController hero, string baseCrestName, ToolCrest baseCrest)
    {
        CrestData? customCrest = FindNeedleforgeCrest(baseCrestName);
        if (customCrest?.Moveset?.ConfigGroup != null)
        {
            return customCrest.Moveset.ConfigGroup;
        }

        string heroConfigName = baseCrest.HeroConfig ? baseCrest.HeroConfig.name : "<null>";
        List<string> availableGroups = new();

        HashSet<string> candidateNames = GetCandidateConfigNames(baseCrestName, heroConfigName);
        foreach (HeroController.ConfigGroup group in GetHeroConfigs(hero))
        {
            if (group?.Config == null)
            {
                continue;
            }

            availableGroups.Add(group.Config.name ?? "<unnamed>");

            if (ReferenceEquals(group.Config, baseCrest.HeroConfig))
            {
                return group;
            }

            string? groupName = group.Config.name;
            if (!string.IsNullOrWhiteSpace(groupName) && candidateNames.Contains(groupName!))
            {
                return group;
            }
        }

        Log.Warning($"Could not match base crest '{baseCrestName}' (hero config name '{heroConfigName}'). Available config groups: {string.Join(", ", availableGroups)}");
        return null;
    }

    private static HashSet<string> GetCandidateConfigNames(string baseCrestName, string heroConfigName)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        names.Add(baseCrestName);
        if (!string.IsNullOrWhiteSpace(heroConfigName) && heroConfigName != "<null>")
        {
            names.Add(heroConfigName);
        }

        if (ConfigAliases.TryGetValue(baseCrestName, out string[]? aliases))
        {
            foreach (string alias in aliases)
            {
                names.Add(alias);
            }
        }

        return names;
    }

    private static IEnumerable<HeroController.ConfigGroup> GetHeroConfigs(HeroController hero)
    {
        if (HeroConfigsField?.GetValue(hero) is HeroController.ConfigGroup[] groups)
        {
            return groups;
        }

        return Array.Empty<HeroController.ConfigGroup>();
    }

    private static CrestData? FindNeedleforgeCrest(string crestName)
    {
        FieldInfo? field = typeof(Needleforge.NeedleforgePlugin).GetField("newCrestData", StaticFlags);
        if (field?.GetValue(null) is not IEnumerable crestData)
        {
            return null;
        }

        foreach (object? item in crestData)
        {
            if (item is CrestData crest && string.Equals(crest.name, crestName, StringComparison.Ordinal))
            {
                return crest;
            }
        }

        return null;
    }

    private static HeroConfigNeedleforge CloneHeroConfig(HeroControllerConfig source)
    {
        if (source is HeroConfigNeedleforge needleforgeConfig)
        {
            HeroConfigNeedleforge clone = UnityEngine.Object.Instantiate(needleforgeConfig);
            clone.name = needleforgeConfig.name;
            return clone;
        }

        HeroConfigNeedleforge target = ScriptableObject.CreateInstance<HeroConfigNeedleforge>();
        CopyMatchingFields(source, target);
        target.name = source.name;
        return target;
    }

    private static void CopyMatchingFields(object source, object target)
    {
        Dictionary<string, FieldInfo> targetFields = GetAllInstanceFields(target.GetType())
            .GroupBy(field => field.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (FieldInfo sourceField in GetAllInstanceFields(source.GetType()))
        {
            if (!targetFields.TryGetValue(sourceField.Name, out FieldInfo? targetField))
            {
                continue;
            }

            if (!targetField.FieldType.IsAssignableFrom(sourceField.FieldType) &&
                !sourceField.FieldType.IsAssignableFrom(targetField.FieldType))
            {
                continue;
            }

            targetField.SetValue(target, sourceField.GetValue(source));
        }
    }

    private static IEnumerable<FieldInfo> GetAllInstanceFields(Type type)
    {
        for (Type? current = type; current != null; current = current.BaseType)
        {
            foreach (FieldInfo field in current.GetFields(InstanceFlags | BindingFlags.DeclaredOnly))
            {
                if (!field.Name.StartsWith("m_", StringComparison.Ordinal))
                {
                    yield return field;
                }
            }
        }
    }

    private static void ReplaceConfigGroupRoot(HeroController.ConfigGroup target, HeroController.ConfigGroup source, bool preserveDashStab)
    {
        GameObject? previousRoot = target.ActiveRoot;
        HeroController? hero = HeroController.instance;
        bool keepActive = previousRoot && previousRoot.activeSelf;
        if (hero != null && ReferenceEquals(CurrentConfigGroupProperty?.GetValue(hero), target))
        {
            keepActive = true;
        }

        GameObject clonedRoot = UnityEngine.Object.Instantiate(source.ActiveRoot, source.ActiveRoot.transform.parent);
        clonedRoot.name = source.ActiveRoot.name;
        clonedRoot.SetActive(false);
        target.ActiveRoot = clonedRoot;

        foreach (string fieldName in ConfigGroupObjectFields)
        {
            RemapObjectField(target, source, fieldName, clonedRoot);
        }

        if (!preserveDashStab)
        {
            RemapObjectField(target, source, "DashStab", clonedRoot);
        }

        // SetConfigGroup skips its activation branch when the same ConfigGroup
        // instance remains selected. Preserve active state explicitly for live
        // base-crest switches before retiring the old attack tree.
        clonedRoot.SetActive(keepActive);
        if (previousRoot && previousRoot != source.ActiveRoot)
        {
            previousRoot.SetActive(false);
            UnityEngine.Object.Destroy(previousRoot);
        }
    }

    private static void RestoreTransplantedAttackAudio(HeroController.ConfigGroup source, HeroController.ConfigGroup target)
    {
        int objectCount = 0;
        int sourceCount = 0;
        int copiedCount = 0;
        int missingCount = 0;
        HashSet<int> visitedTargets = new();

        // DashStab intentionally stays out of this list. Void Crest owns Shadow
        // Dash and its audio, just as it owns normal Bind and Up-Bind.
        foreach (string fieldName in ConfigGroupObjectFields)
        {
            FieldInfo? field = typeof(HeroController.ConfigGroup).GetField(fieldName, InstanceFlags);
            GameObject? sourceObject = field?.GetValue(source) as GameObject;
            GameObject? targetObject = field?.GetValue(target) as GameObject;
            if (!sourceObject || !targetObject || !visitedTargets.Add(targetObject.GetInstanceID()))
            {
                continue;
            }

            objectCount++;
            AudioSource[] sourceAudio = sourceObject.GetComponentsInChildren<AudioSource>(true);
            sourceCount += sourceAudio.Length;

            foreach (AudioSource sourceAudioSource in sourceAudio)
            {
                string? relativePath = GetRelativePath(sourceObject.transform, sourceAudioSource.transform);
                Transform? targetTransform = relativePath switch
                {
                    null => null,
                    "" => targetObject.transform,
                    _ => targetObject.transform.Find(relativePath)
                };

                if (targetTransform == null)
                {
                    missingCount++;
                    Log.Warning($"Audio restore could not find '{fieldName}/{relativePath ?? "<external>"}' in the transplanted attack tree.");
                    continue;
                }

                AudioSource[] sourceAtPath = sourceAudioSource.transform.GetComponents<AudioSource>();
                int componentIndex = Array.IndexOf(sourceAtPath, sourceAudioSource);
                AudioSource[] targetAtPath = targetTransform.GetComponents<AudioSource>();
                if (componentIndex < 0 || componentIndex >= targetAtPath.Length)
                {
                    missingCount++;
                    Log.Warning($"Audio restore found no matching AudioSource #{componentIndex} at '{fieldName}/{relativePath}'.");
                    continue;
                }

                CopyAudioSourceSettings(sourceAudioSource, targetAtPath[componentIndex]);
                copiedCount++;
            }
        }

        Log.Info(
            $"Restored base-crest attack audio: crest={GetActiveBaseCrest()}, objects={objectCount}, " +
            $"sources={sourceCount}, copied={copiedCount}, missing={missingCount}; Void Dash/Bind audio preserved.");
    }

    private static void CopyAudioSourceSettings(AudioSource source, AudioSource target)
    {
        target.clip = source.clip;
        target.outputAudioMixerGroup = source.outputAudioMixerGroup;
        target.mute = source.mute;
        target.bypassEffects = source.bypassEffects;
        target.bypassListenerEffects = source.bypassListenerEffects;
        target.bypassReverbZones = source.bypassReverbZones;
        target.playOnAwake = source.playOnAwake;
        target.loop = source.loop;
        target.priority = source.priority;
        target.volume = source.volume;
        target.pitch = source.pitch;
        target.panStereo = source.panStereo;
        target.spatialBlend = source.spatialBlend;
        target.reverbZoneMix = source.reverbZoneMix;
        target.dopplerLevel = source.dopplerLevel;
        target.spread = source.spread;
        target.minDistance = source.minDistance;
        target.maxDistance = source.maxDistance;
        target.rolloffMode = source.rolloffMode;
        target.SetCustomCurve(AudioSourceCurveType.CustomRolloff, source.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
        target.SetCustomCurve(AudioSourceCurveType.SpatialBlend, source.GetCustomCurve(AudioSourceCurveType.SpatialBlend));
        target.SetCustomCurve(AudioSourceCurveType.ReverbZoneMix, source.GetCustomCurve(AudioSourceCurveType.ReverbZoneMix));
        target.SetCustomCurve(AudioSourceCurveType.Spread, source.GetCustomCurve(AudioSourceCurveType.Spread));
    }

    private static void NormalizeTransplantedDownslashConfig(HeroConfigNeedleforge replacementConfig, GameObject? downSlashObject)
    {
        if (replacementConfig.DownSlashType != HeroControllerConfig.DownSlashTypes.Custom || downSlashObject == null)
        {
            return;
        }

        if (downSlashObject.GetComponent<Downspike>() != null)
        {
            DownSlashTypeField?.SetValue(replacementConfig, HeroControllerConfig.DownSlashTypes.DownSpike);
            DownSlashEventField?.SetValue(replacementConfig, null);
            replacementConfig.DownSlashFsmEdit = null;
            Log.Info("Normalized custom downslash to DownSpike for transplanted vanilla moveset.");
            return;
        }

        if (downSlashObject.GetComponent<NailSlash>() != null)
        {
            DownSlashTypeField?.SetValue(replacementConfig, HeroControllerConfig.DownSlashTypes.Slash);
            DownSlashEventField?.SetValue(replacementConfig, null);
            replacementConfig.DownSlashFsmEdit = null;
            Log.Info("Normalized custom downslash to Slash for transplanted vanilla moveset.");
        }
    }

    private static void RemapObjectField(HeroController.ConfigGroup target, HeroController.ConfigGroup source, string fieldName, GameObject clonedRoot)
    {
        FieldInfo? field = typeof(HeroController.ConfigGroup).GetField(fieldName, InstanceFlags);
        if (field == null)
        {
            return;
        }

        GameObject? sourceObject = field.GetValue(source) as GameObject;
        if (!sourceObject)
        {
            field.SetValue(target, null);
            return;
        }

        string? relativePath = GetRelativePath(source.ActiveRoot.transform, sourceObject.transform);
        if (relativePath == null)
        {
            GameObject externalClone = CloneExternalConfigObject(sourceObject, clonedRoot, fieldName);
            field.SetValue(target, externalClone);
            Log.Info($"Cloned external '{fieldName}' object '{sourceObject.name}' under '{clonedRoot.name}'.");
            return;
        }

        GameObject? targetObject = string.IsNullOrEmpty(relativePath)
            ? clonedRoot
            : clonedRoot.transform.Find(relativePath)?.gameObject;
        field.SetValue(target, targetObject);
    }

    private static GameObject CloneExternalConfigObject(GameObject sourceObject, GameObject clonedRoot, string fieldName)
    {
        GameObject clone = UnityEngine.Object.Instantiate(sourceObject, clonedRoot.transform);
        clone.name = sourceObject.name;
        clone.SetActive(sourceObject.activeSelf);
        clone.transform.SetAsLastSibling();

        GameObject container = new($"{fieldName} External");
        container.transform.SetParent(clonedRoot.transform, false);
        container.transform.SetAsLastSibling();
        clone.transform.SetParent(container.transform, false);
        return clone;
    }

    private static GameObject EnsureDashRoot(GameObject created, GameObject root)
    {
        if (created.transform.parent == root.transform)
        {
            return created;
        }

        GameObject dashRoot = new GameObject("Dash Stab Parent");
        dashRoot.transform.SetParent(root.transform, false);
        created.transform.SetParent(dashRoot.transform, false);
        return dashRoot;
    }

    private static string? GetRelativePath(Transform root, Transform target)
    {
        if (target == root)
        {
            return string.Empty;
        }

        List<string> segments = new();
        Transform? current = target;
        while (current != null && current != root)
        {
            segments.Add(current.name);
            current = current.parent;
        }

        if (current != root)
        {
            return null;
        }

        segments.Reverse();
        return string.Join("/", segments);
    }

    private static void LogHeroState(HeroController hero, string baseCrestName)
    {
        HeroController.ConfigGroup? currentGroup = CurrentConfigGroupProperty?.GetValue(hero) as HeroController.ConfigGroup;
        string currentGroupName = currentGroup?.Config?.name ?? "<null>";
        string normalSlashName = GetObjectName(NormalSlashField?.GetValue(hero));
        string downSlashName = GetObjectName(DownSlashField?.GetValue(hero));
        string downSpikeName = GetObjectName(DownSpikeField?.GetValue(hero));
        string wallSlashName = GetObjectName(WallSlashField?.GetValue(hero));
        bool usingVoidCompatGroup = ReferenceEquals(currentGroup, VoidCrestPlugin.voidCrestData?.Moveset?.ConfigGroup);
        string currentDownslashConfig = currentGroup?.Config != null ? DescribeDownslashConfig(currentGroup.Config) : "<null>";

        Log.Info(
            $"Post-apply state for '{baseCrestName}': CurrentConfigGroup={currentGroupName}, UsesVoidCompatGroup={usingVoidCompatGroup}, " +
            $"Config={currentDownslashConfig}, NormalSlash={normalSlashName}, DownSlash={downSlashName}, DownSpike={downSpikeName}, WallSlash={wallSlashName}");
    }

    private static string GetObjectName(object? value)
    {
        return value switch
        {
            Component component when component => component.gameObject.name,
            GameObject gameObject when gameObject => gameObject.name,
            UnityEngine.Object unityObject when unityObject => unityObject.name,
            _ => "<null>"
        };
    }

    private static void LogConfigGroupDetails(string label, HeroController.ConfigGroup group)
    {
        Log.Info(
            $"{label} ConfigGroup '{group.Config?.name ?? "<null>"}' ({DescribeDownslashConfig(group.Config)}): Root={GetObjectName(group.ActiveRoot)}, " +
            $"Normal={DescribeAttackObject(group.NormalSlashObject)}, Up={DescribeAttackObject(group.UpSlashObject)}, " +
            $"Down={DescribeAttackObject(group.DownSlashObject)}, Wall={DescribeAttackObject(group.WallSlashObject)}, " +
            $"Dash={DescribeAttackObject(group.DashStab)}");
    }

    private static string DescribeDownslashConfig(HeroControllerConfig? config)
    {
        if (config == null)
        {
            return "<null>";
        }

        string downslashEvent = string.IsNullOrWhiteSpace(config.DownSlashEvent) ? "<null>" : config.DownSlashEvent;
        return $"{config.name}[Type={config.DownSlashType},Event={downslashEvent}]";
    }

    private static string DescribeAttackObject(GameObject? obj)
    {
        if (!obj)
        {
            return "<null>";
        }

        string slash = obj.GetComponent<NailSlash>() ? "NailSlash" : "-";
        string downspike = obj.GetComponent<Downspike>() ? "Downspike" : "-";
        string damager = obj.GetComponent<DamageEnemies>() ? "DamageEnemies" : "-";
        string audio = obj.GetComponent("AudioSource") != null ? "Audio" : "-";
        string animator = obj.GetComponent<tk2dSpriteAnimator>() ? "Animator" : "-";
        Transform? extraDamager = obj.transform.Find("Extra Damager");
        Transform? clashTink = obj.transform.Find("Clash Tink");

        return $"{obj.name}[{slash},{downspike},{damager},{audio},{animator}; Extra={GetObjectName(extraDamager)}, Clash={GetObjectName(clashTink)}]";
    }

    private static void LogChargeSlashDetails(string label, GameObject? obj)
    {
        if (!obj)
        {
            Log.Info($"{label} ChargeSlash: <null>");
            return;
        }

        Log.Info($"{label} ChargeSlash Root={DescribeAttackObject(obj)}");
        foreach (Transform child in obj.GetComponentsInChildren<Transform>(true))
        {
            string? relativePath = GetRelativePath(obj.transform, child);
            if (relativePath == null)
            {
                continue;
            }

            GameObject current = child.gameObject;
            List<string> componentNames = new();
            if (current.GetComponent<NailSlash>() != null)
            {
                componentNames.Add("NailSlash");
            }
            if (current.GetComponent<Downspike>() != null)
            {
                componentNames.Add("Downspike");
            }
            if (current.GetComponent<DashStabNailAttack>() != null)
            {
                componentNames.Add("DashStabNailAttack");
            }
            if (current.GetComponent<HeroExtraNailSlash>() != null)
            {
                componentNames.Add("HeroExtraNailSlash");
            }
            if (current.GetComponent<tk2dSprite>() != null)
            {
                componentNames.Add("tk2dSprite");
            }
            if (current.GetComponent<tk2dSpriteAnimator>() != null)
            {
                componentNames.Add("tk2dSpriteAnimator");
            }
            if (current.GetComponent<MeshRenderer>() != null)
            {
                componentNames.Add("MeshRenderer");
            }
            if (current.GetComponent<DamageEnemies>() != null)
            {
                componentNames.Add("DamageEnemies");
            }
            if (current.GetComponent<RemapTk2DSpriteAnimator>() != null)
            {
                componentNames.Add("RemapTk2DSpriteAnimator");
            }
            string[] fsmNames = GetPlayMakerFsmNames(current);
            if (fsmNames.Length > 0)
            {
                componentNames.Add($"PlayMakerFSM[{string.Join(", ", fsmNames)}]");
            }

            if (componentNames.Count == 0)
            {
                continue;
            }

            string collectionName = current.GetComponent<tk2dBaseSprite>()?.Collection?.name ?? "<null>";
            Log.Info($"{label} ChargeSlash '{relativePath}': Collection={collectionName}, Components={string.Join(", ", componentNames)}");
        }
    }

    private sealed class ManualLogSourceAdapter
    {
        private readonly BepInEx.Logging.ManualLogSource source;

        public ManualLogSourceAdapter(BepInEx.Logging.ManualLogSource source)
        {
            this.source = source;
        }

        public void Info(string message) => source.LogInfo(message);

        public void Warning(string message) => source.LogWarning(message);

        public void Error(string message) => source.LogError(message);
    }

    private sealed class LiveSlashVisualRetint : MonoBehaviour
    {
        private readonly List<tk2dBaseSprite> tk2dSprites = new();
        private readonly List<SpriteRenderer> spriteRenderers = new();
        private int framesRemaining;
        private Color targetColor = Color.white;

        public void Configure(Color color, int frames)
        {
            targetColor = color;
            framesRemaining = Mathf.Max(framesRemaining, frames);
            CacheTargets();
            Apply();
        }

        private void LateUpdate()
        {
            if (framesRemaining <= 0 || !isActiveAndEnabled)
            {
                Destroy(this);
                return;
            }

            Apply();
            framesRemaining--;
        }

        private void CacheTargets()
        {
            tk2dSprites.Clear();
            spriteRenderers.Clear();

            foreach (tk2dBaseSprite sprite in GetComponentsInChildren<tk2dBaseSprite>(true))
            {
                tk2dSprites.Add(sprite);
            }

            foreach (SpriteRenderer spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                spriteRenderers.Add(spriteRenderer);
            }
        }

        private void Apply()
        {
            foreach (tk2dBaseSprite sprite in tk2dSprites)
            {
                if (sprite)
                {
                    LogLiveRetintTarget(sprite);
                    sprite.color = targetColor;
                }
            }

            foreach (SpriteRenderer spriteRenderer in spriteRenderers)
            {
                if (spriteRenderer)
                {
                    spriteRenderer.color = targetColor;
                }
            }
        }
    }

    private static void LogLiveRetintTarget(tk2dBaseSprite sprite)
    {
        if (liveSlashRetintLogCount >= 120)
        {
            return;
        }

        liveSlashRetintLogCount++;
        string collectionName = sprite.Collection ? sprite.Collection.name : "<null>";
        string spriteName = sprite.CurrentSprite?.name ?? "<null>";
        string objectPath = GetObjectPath(sprite.transform);
        Log.Info($"Retint target '{objectPath}': Collection={collectionName}, Sprite={spriteName}");
    }

    private static bool IsChargeSlashObject(GameObject gameObject)
    {
        for (Transform? current = gameObject.transform; current != null; current = current.parent)
        {
            string name = current.name ?? string.Empty;
            if (name.IndexOf("Charge", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void LogHeroExtraNailSlashEnable(HeroExtraNailSlash? component)
    {
        if (component == null || heroExtraNailSlashLogCount >= 40)
        {
            return;
        }

        heroExtraNailSlashLogCount++;
        GameObject gameObject = component.gameObject;
        string objectPath = GetObjectPath(gameObject.transform);
        Log.Info($"HeroExtraNailSlash enabled at '{objectPath}'.");

        foreach (Transform child in gameObject.GetComponentsInChildren<Transform>(true))
        {
            string? relativePath = GetRelativePath(gameObject.transform, child);
            if (relativePath == null)
            {
                continue;
            }

            GameObject current = child.gameObject;
            List<string> componentNames = new();
            if (current.GetComponent<tk2dSprite>() != null)
            {
                componentNames.Add("tk2dSprite");
            }
            if (current.GetComponent<tk2dSpriteAnimator>() != null)
            {
                componentNames.Add("tk2dSpriteAnimator");
            }
            if (current.GetComponent<MeshRenderer>() != null)
            {
                componentNames.Add("MeshRenderer");
            }
            if (current.GetComponent<DamageEnemies>() != null)
            {
                componentNames.Add("DamageEnemies");
            }
            if (current.GetComponent<RemapTk2DSpriteAnimator>() != null)
            {
                componentNames.Add("RemapTk2DSpriteAnimator");
            }
            string[] fsmNames = GetPlayMakerFsmNames(current);
            if (fsmNames.Length > 0)
            {
                componentNames.Add($"PlayMakerFSM[{string.Join(", ", fsmNames)}]");
            }

            if (componentNames.Count == 0)
            {
                continue;
            }

            string collectionName = current.GetComponent<tk2dBaseSprite>()?.Collection?.name ?? "<null>";
            Log.Info($"HeroExtraNailSlash child '{relativePath}': Collection={collectionName}, Components={string.Join(", ", componentNames)}");
        }
    }

    private static string[] GetPlayMakerFsmNames(GameObject gameObject)
    {
        return gameObject
            .GetComponents<Component>()
            .Where(component => component != null && string.Equals(component.GetType().Name, "PlayMakerFSM", StringComparison.Ordinal))
            .Select(component =>
            {
                PropertyInfo? property = component.GetType().GetProperty("FsmName", InstanceFlags);
                return property?.GetValue(component) as string ?? component.GetType().Name;
            })
            .ToArray();
    }

    private static string GetObjectPath(Transform? transform)
    {
        if (transform == null)
        {
            return "<null>";
        }

        List<string> segments = new();
        for (Transform? current = transform; current != null; current = current.parent)
        {
            segments.Add(current.name);
        }

        segments.Reverse();
        return string.Join("/", segments);
    }
}
