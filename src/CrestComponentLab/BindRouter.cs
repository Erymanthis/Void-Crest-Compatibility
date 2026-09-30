using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HutongGames.PlayMaker;
using Needleforge;
using Needleforge.Data;

namespace CrestComponentLab;

internal static class BindRouter
{
    private static ComponentSource activeSource;
    private static bool routeActive;
    private static bool observedBinding;

    internal static ComponentSource ActiveSource => routeActive ? activeSource : ComponentSource.None;

    internal static void Attach(CrestData crest)
    {
        crest.BindEvent = OnNormalBindStarted;
        crest.BindCompleteEvent = EndRoute;
        // Install the branch once so later bench-gated recipe changes do not depend on
        // Needleforge rerunning its one-time Bind FSM construction pass.
        crest.uniqueBindEvent = new UniqueBindEvent(UniqueBindDirection.UP, OnSpecialBindStarted);
    }

    internal static void OnRecipeActivated()
    {
        EndRoute();
    }

    internal static void Update()
    {
        if (!routeActive)
        {
            return;
        }

        HeroController? hero = HeroController.instance;
        bool binding = hero?.cState.isBinding == true;
        observedBinding |= binding;
        if (observedBinding && !binding)
        {
            EndRoute();
        }
    }

    internal static bool Matches(ComponentSource source) =>
        Plugin.Crest?.IsEquipped == true && ActiveSource == source;

    private static void OnNormalBindStarted(
        FsmInt amountHealed,
        FsmInt numberOfBinds,
        FsmFloat secondsPerBind,
        PlayMakerFSM bindFsm)
    {
        ComponentSource source = Plugin.ActiveRecipe.Bind;
        if (source != ComponentSource.None)
        {
            BeginRoute(source, "normal Bind");
        }
    }

    private static void OnSpecialBindStarted(Action continuation)
    {
        ComponentSource source = Plugin.ActiveRecipe.SpecialBind;
        if (source != ComponentSource.None)
        {
            BeginRoute(source, "special Bind");
            HeroController.instance?.BindCompleted();
        }

        continuation();
    }

    private static void BeginRoute(ComponentSource source, string reason)
    {
        activeSource = source;
        routeActive = source != ComponentSource.None;
        observedBinding = HeroController.instance?.cState.isBinding == true;
        Plugin.LogInfo($"Identity route began for {reason}: {source}.");
    }

    private static void EndRoute()
    {
        if (routeActive)
        {
            Plugin.LogInfo($"Identity route ended: {activeSource}.");
        }

        activeSource = ComponentSource.None;
        routeActive = false;
        observedBinding = false;
    }

}

[HarmonyPatch(typeof(ToolCrest), "get_IsEquipped")]
internal static class ScopedToolEquippedPatch
{
    [HarmonyPostfix]
    private static void Postfix(ToolCrest __instance, ref bool __result)
    {
        if (__result || BindRouter.ActiveSource == ComponentSource.None || Plugin.Crest?.IsEquipped != true)
        {
            return;
        }

        ToolCrest? provider = VanillaProviderResolver.ResolveToolCrest(BindRouter.ActiveSource);
        if (provider != null && ReferenceEquals(__instance, provider))
        {
            __result = true;
        }
    }
}

[HarmonyPatch(typeof(HeroController), "Attack")]
internal static class InertCoreAttackGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix() =>
        Plugin.Crest?.IsEquipped != true || Plugin.ActiveRecipe.Core != ComponentSource.None;
}

[HarmonyPatch]
internal static class ScopedHeroCrestQueryPatch
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
        if (__result || Plugin.Crest?.IsEquipped != true)
        {
            return;
        }

        ComponentSource expected = __originalMethod.Name switch
        {
            nameof(HeroController.IsHunterCrestEquipped) => ComponentSource.Hunter,
            nameof(HeroController.IsArchitectCrestEquipped) => ComponentSource.Architect,
            nameof(HeroController.IsShamanCrestEquipped) => ComponentSource.Shaman,
            _ => ComponentSource.None
        };
        __result = expected != ComponentSource.None && BindRouter.Matches(expected);
    }
}
