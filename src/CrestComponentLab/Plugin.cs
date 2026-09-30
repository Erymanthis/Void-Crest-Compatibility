using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Needleforge;
using Needleforge.Data;
using UnityEngine;

namespace CrestComponentLab;

[BepInPlugin(PluginId, PluginName, PluginVersion)]
[BepInDependency(NeedleforgePlugin.Id, "0.9.0")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginId = "io.github.erymanthis.crestcomponentlab";
    public const string PluginName = "CrestComponentLab";
    public const string PluginVersion = "0.2.0";
    public const string CrestId = "ComponentLab";

    private static Harmony harmony = null!;
    private static ManualLogSource log = null!;
    private static Texture2D? iconTexture;
    private static CompositionRecipe activeRecipe;
    private static CompositionRecipe? pendingRecipe;
    private static BenchBoundary pendingBoundary;
    private static bool wasAtBench;

    internal static CrestData? Crest { get; private set; }
    internal static ConfigEntry<ComponentSource> MovesetSource { get; private set; } = null!;
    internal static ConfigEntry<ComponentSource> DashSource { get; private set; } = null!;
    internal static ConfigEntry<ComponentSource> ChargeSource { get; private set; } = null!;
    internal static ConfigEntry<ComponentSource> BindSource { get; private set; } = null!;
    internal static ConfigEntry<ComponentSource> SpecialBindSource { get; private set; } = null!;
    internal static CompositionRecipe ActiveRecipe => activeRecipe;

    private void Awake()
    {
        log = Logger;
        MovesetSource = BindSourceConfig(
            "Core Moveset",
            "Copies regular, alternate, up, down, wall, and Challenge/Taunt attacks. None is genuinely inert.");
        DashSource = BindSourceConfig(
            "Dash Slash",
            "Selects the dash attack independently. None installs no dash-attack object.");
        ChargeSource = BindSourceConfig(
            "Charge Slash",
            "Selects the charged attack independently. None disables nail charging for this crest.");
        BindSource = BindSourceConfig(
            "Normal Bind",
            "Routes vanilla crest identity only while a normal Bind is active. None preserves Needleforge's standard custom-crest Bind.");
        SpecialBindSource = BindSourceConfig(
            "Special Bind",
            "Selects the provider used by the preinstalled Up-Bind route. None performs no provider-specific completion behavior.");

        activeRecipe = CompositionRecipe.FromConfig();
        wasAtBench = PlayerData.instance?.atBench == true;

        Sprite icon = CreateLabIcon();
        Crest = NeedleforgePlugin.AddCrest(CrestId, icon, icon, icon);
        Crest.UnlockedAtStart = true;
        Crest.bindCost = 9;
        Crest.slotsCountForEvaQuest = false;
        CompositionEngine.ConfigureDefinition(Crest.Moveset);
        Crest.Moveset.OnInitialized += CompositionEngine.FinalizeInitialization;
        BindRouter.Attach(Crest);

        foreach (ConfigEntry<ComponentSource> entry in new[]
                 {
                     MovesetSource, DashSource, ChargeSource, BindSource, SpecialBindSource
                 })
        {
            entry.SettingChanged += OnCompositionSettingChanged;
        }

        harmony = new Harmony(PluginId);
        harmony.PatchAll();
        LogInfo("Loaded clean-room Component Lab crest with Needleforge-only mod dependencies.");
    }

    private void Update()
    {
        BindRouter.Update();
        bool isAtBench = PlayerData.instance?.atBench == true;
        bool crossedBoundary = pendingBoundary switch
        {
            BenchBoundary.Enter => !wasAtBench && isAtBench,
            BenchBoundary.Exit => wasAtBench && !isAtBench,
            _ => false
        };

        if (pendingRecipe is { } recipe && crossedBoundary)
        {
            if (CompositionEngine.ApplyRecipe(recipe))
            {
                activeRecipe = recipe;
                pendingRecipe = null;
                pendingBoundary = BenchBoundary.None;
                BindRouter.OnRecipeActivated();
                LogInfo($"Activated queued recipe on bench {(!isAtBench ? "exit" : "enter")}: {recipe}.");
            }
            else
            {
                LogWarning($"Could not activate queued recipe at the bench; it remains queued: {recipe}.");
            }
        }

        wasAtBench = isAtBench;
    }

    private void OnDestroy()
    {
        harmony?.UnpatchSelf();
        if (iconTexture)
        {
            Destroy(iconTexture);
        }
    }

    private ConfigEntry<ComponentSource> BindSourceConfig(string key, string description)
    {
        return Config.Bind("Composition", key, ComponentSource.None, description);
    }

    private static void OnCompositionSettingChanged(object sender, EventArgs args)
    {
        CompositionRecipe requested = CompositionRecipe.FromConfig();
        if (requested.Equals(activeRecipe))
        {
            pendingRecipe = null;
            pendingBoundary = BenchBoundary.None;
            LogInfo("Pending recipe cleared because the configuration matches the active recipe.");
            return;
        }

        pendingRecipe = requested;
        bool isAtBench = PlayerData.instance?.atBench == true;
        pendingBoundary = isAtBench ? BenchBoundary.Exit : BenchBoundary.Enter;
        LogInfo($"Queued recipe for bench {pendingBoundary.ToString().ToLowerInvariant()}: {requested}.");
    }

    private static Sprite CreateLabIcon()
    {
        const int size = 64;
        iconTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Component Lab Crest Icon",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color teal = new Color(0.12f, 0.83f, 0.76f, 1f);
        Color amber = new Color(1f, 0.68f, 0.15f, 1f);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int dx = x - size / 2;
                int dy = y - size / 2;
                int radiusSquared = dx * dx + dy * dy;
                bool ring = radiusSquared >= 20 * 20 && radiusSquared <= 27 * 27;
                bool cross = (Math.Abs(dx) <= 3 && Math.Abs(dy) <= 18) ||
                             (Math.Abs(dy) <= 3 && Math.Abs(dx) <= 18);
                pixels[y * size + x] = ring ? teal : cross ? amber : clear;
            }
        }

        iconTexture.SetPixels(pixels);
        iconTexture.Apply(false, false);
        return Sprite.Create(iconTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    internal static void LogInfo(string message) =>
        log.LogInfo(message);

    internal static void LogWarning(string message) =>
        log.LogWarning(message);
}

internal enum BenchBoundary
{
    None,
    Enter,
    Exit
}

[HarmonyPatch]
internal static class NeedleforgeMovesetInitializationPatch
{
    private static MethodBase? TargetMethod()
    {
        Type? maker = AccessTools.TypeByName("Needleforge.Makers.MovesetMaker");
        return maker == null ? null : AccessTools.Method(maker, "InitializeMoveset");
    }

    [HarmonyPrefix]
    private static void Prefix(MovesetData moveset)
    {
        if (ReferenceEquals(moveset, Plugin.Crest?.Moveset))
        {
            CompositionEngine.PrepareForInitialization(moveset);
        }
    }
}
