using Needleforge.Attacks;
using UnityEngine;

namespace CrestComponentLab;

internal static class ProviderClone
{
    internal static GameObject? Create(ComponentSource source, AttackSlot slot, GameObject parent)
    {
        GameObject? sourceObject = VanillaProviderResolver.ResolveAttack(source, slot);
        if (!sourceObject)
        {
            Plugin.LogWarning($"Provider {source} has no resolved {slot} object; the component remains inert.");
            return null;
        }

        GameObject clone = Object.Instantiate(sourceObject, parent.transform);
        clone.name = sourceObject.name;
        clone.transform.localPosition = sourceObject.transform.localPosition;
        clone.transform.localRotation = sourceObject.transform.localRotation;
        clone.transform.localScale = sourceObject.transform.localScale;
        return clone;
    }
}

internal sealed class InertAttack : Attack
{
    private readonly AttackSlot safetySlot;

    internal InertAttack(AttackSlot safetySlot)
    {
        this.safetySlot = safetySlot;
        Name = $"Component Lab inert {safetySlot}";
    }

    public override GameObject CreateGameObject(GameObject parent, HeroController hc)
    {
        GameObject = ProviderClone.Create(ComponentSource.Hunter, safetySlot, parent)!;
        return GameObject;
    }
}

internal sealed class InertDownAttack : DownAttack
{
    private readonly AttackSlot safetySlot;

    internal InertDownAttack(AttackSlot safetySlot)
    {
        this.safetySlot = safetySlot;
        Name = $"Component Lab inert {safetySlot}";
    }

    public override GameObject CreateGameObject(GameObject parent, HeroController hc)
    {
        GameObject = ProviderClone.Create(ComponentSource.Hunter, safetySlot, parent)!;
        return GameObject;
    }
}

internal sealed class InertDashAttack : DashAttack
{
    public override GameObject CreateGameObject(GameObject parent, HeroController hc) => null!;
}

internal sealed class InertChargedAttack : ChargedAttack
{
    public override GameObject CreateGameObject(GameObject parent, HeroController hc) => null!;
}

internal sealed class ProviderAttack : Attack
{
    private readonly ComponentSource source;
    private readonly AttackSlot slot;

    internal ProviderAttack(ComponentSource source, AttackSlot slot)
    {
        this.source = source;
        this.slot = slot;
        Name = $"Component Lab {slot} ({source})";
    }

    public override GameObject CreateGameObject(GameObject parent, HeroController hc)
    {
        GameObject = ProviderClone.Create(source, slot, parent)!;
        return GameObject;
    }
}

internal sealed class ProviderDownAttack : DownAttack
{
    private readonly ComponentSource source;
    private readonly AttackSlot slot;

    internal ProviderDownAttack(ComponentSource source, AttackSlot slot)
    {
        this.source = source;
        this.slot = slot;
        Name = $"Component Lab {slot} ({source})";
    }

    public override GameObject CreateGameObject(GameObject parent, HeroController hc)
    {
        GameObject = ProviderClone.Create(source, slot, parent)!;
        return GameObject;
    }
}

internal sealed class ProviderDashAttack : DashAttack
{
    private readonly ComponentSource source;

    internal ProviderDashAttack(ComponentSource source)
    {
        this.source = source;
        Name = $"Component Lab Dash ({source})";
    }

    public override GameObject CreateGameObject(GameObject parent, HeroController hc)
    {
        GameObject = ProviderClone.Create(source, AttackSlot.Dash, parent)!;
        return GameObject;
    }
}

internal sealed class ProviderChargedAttack : ChargedAttack
{
    private readonly ComponentSource source;

    internal ProviderChargedAttack(ComponentSource source)
    {
        this.source = source;
        Name = $"Component Lab Charge ({source})";
    }

    public override GameObject CreateGameObject(GameObject parent, HeroController hc)
    {
        GameObject = ProviderClone.Create(source, AttackSlot.Charge, parent)!;
        return GameObject;
    }
}
