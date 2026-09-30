using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace VoidCrestMovesetCompat.Framework.Experimental;

/// <summary>
/// Broad behavior families that can be resolved, cloned, composed, and activated independently.
/// This enum is intentionally smaller than the set of concrete feature IDs so third-party
/// frameworks can add new features without changing the core API.
/// </summary>
public enum CrestFeatureKind
{
    Identity,
    Progression,
    Attack,
    Passive,
    Bind,
    RuntimeState,
    Animation,
    Audio,
    Visual,
    Lifecycle,
    Custom
}

public enum CrestTransferMode
{
    /// <summary>Use the provider's live object without taking ownership.</summary>
    Reference,

    /// <summary>Clone the provider's object into host-owned runtime state.</summary>
    Clone,

    /// <summary>Reach the vanilla behavior through a scoped identity or call-site proxy.</summary>
    Proxy,

    /// <summary>Install a custom implementation that preserves the feature contract.</summary>
    Custom
}

/// <summary>
/// Metadata for one independently composable crest feature.
/// Runtime installation is supplied separately by an adapter.
/// </summary>
public sealed class CrestFeatureDescriptor
{
    public CrestFeatureDescriptor(
        string id,
        string displayName,
        CrestFeatureKind kind,
        string providerCrestId,
        CrestTransferMode transferMode,
        bool requiresFunctionalIdentity = false,
        bool requiresRuntimeState = false,
        bool lifecycleSensitive = false,
        IEnumerable<string>? dependencies = null,
        IEnumerable<string>? conflicts = null,
        string? evidence = null)
    {
        Id = RequireValue(id, nameof(id));
        DisplayName = RequireValue(displayName, nameof(displayName));
        Kind = kind;
        ProviderCrestId = RequireValue(providerCrestId, nameof(providerCrestId));
        TransferMode = transferMode;
        RequiresFunctionalIdentity = requiresFunctionalIdentity;
        RequiresRuntimeState = requiresRuntimeState;
        LifecycleSensitive = lifecycleSensitive;
        Dependencies = FreezeIds(dependencies);
        Conflicts = FreezeIds(conflicts);
        Evidence = evidence?.Trim() ?? string.Empty;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public CrestFeatureKind Kind { get; }

    public string ProviderCrestId { get; }

    public CrestTransferMode TransferMode { get; }

    public bool RequiresFunctionalIdentity { get; }

    public bool RequiresRuntimeState { get; }

    public bool LifecycleSensitive { get; }

    public IReadOnlyCollection<string> Dependencies { get; }

    public IReadOnlyCollection<string> Conflicts { get; }

    /// <summary>
    /// Human-readable provenance such as a vanilla method, field, FSM, or inspected asset.
    /// It is diagnostic metadata, not an executable lookup expression.
    /// </summary>
    public string Evidence { get; }

    private static string RequireValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value.Trim();
    }

    private static IReadOnlyCollection<string> FreezeIds(IEnumerable<string>? values)
    {
        string[] normalized = values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
        return Array.AsReadOnly(normalized);
    }
}

/// <summary>
/// Extensible registry. IDs are stable public contracts; display names and implementation
/// adapters can evolve independently.
/// </summary>
public sealed class CrestFeatureRegistry
{
    private readonly Dictionary<string, CrestFeatureDescriptor> features = new(StringComparer.Ordinal);

    public IReadOnlyCollection<CrestFeatureDescriptor> Features =>
        Array.AsReadOnly(features.Values.OrderBy(feature => feature.Id, StringComparer.Ordinal).ToArray());

    public void Register(CrestFeatureDescriptor feature, bool replace = false)
    {
        if (feature == null)
        {
            throw new ArgumentNullException(nameof(feature));
        }

        if (!replace && features.ContainsKey(feature.Id))
        {
            throw new InvalidOperationException($"Crest feature '{feature.Id}' is already registered.");
        }

        features[feature.Id] = feature;
    }

    public bool TryGet(string featureId, out CrestFeatureDescriptor feature)
    {
        if (string.IsNullOrWhiteSpace(featureId))
        {
            feature = null!;
            return false;
        }

        return features.TryGetValue(featureId.Trim(), out feature!);
    }

    public CrestFeatureDescriptor GetRequired(string featureId)
    {
        if (!TryGet(featureId, out CrestFeatureDescriptor feature))
        {
            throw new KeyNotFoundException($"Crest feature '{featureId}' is not registered.");
        }

        return feature;
    }
}

public static class StandardCrestSlots
{
    public const string Identity = "identity";
    public const string Progression = "progression";
    public const string NormalAttack = "attack.normal";
    public const string AlternateAttack = "attack.alternate";
    public const string UpAttack = "attack.up";
    public const string AlternateUpAttack = "attack.up.alternate";
    public const string DownAttack = "attack.down";
    public const string AlternateDownAttack = "attack.down.alternate";
    public const string WallAttack = "attack.wall";
    public const string DashAttack = "attack.dash";
    public const string ChargeAttack = "attack.charge";
    public const string ChallengeAttack = "attack.challenge";
    public const string Bind = "bind";
    public const string Animation = "animation";
    public const string Audio = "audio";
    public const string Visual = "visual";

    private static readonly IReadOnlyDictionary<string, CrestFeatureKind> KnownSlotKinds =
        new ReadOnlyDictionary<string, CrestFeatureKind>(
            new Dictionary<string, CrestFeatureKind>(StringComparer.Ordinal)
            {
                [Identity] = CrestFeatureKind.Identity,
                [Progression] = CrestFeatureKind.Progression,
                [NormalAttack] = CrestFeatureKind.Attack,
                [AlternateAttack] = CrestFeatureKind.Attack,
                [UpAttack] = CrestFeatureKind.Attack,
                [AlternateUpAttack] = CrestFeatureKind.Attack,
                [DownAttack] = CrestFeatureKind.Attack,
                [AlternateDownAttack] = CrestFeatureKind.Attack,
                [WallAttack] = CrestFeatureKind.Attack,
                [DashAttack] = CrestFeatureKind.Attack,
                [ChargeAttack] = CrestFeatureKind.Attack,
                [ChallengeAttack] = CrestFeatureKind.Attack,
                [Bind] = CrestFeatureKind.Bind,
                [Animation] = CrestFeatureKind.Animation,
                [Audio] = CrestFeatureKind.Audio,
                [Visual] = CrestFeatureKind.Visual
            });

    public static bool TryGetExpectedKind(string slotId, out CrestFeatureKind kind)
    {
        return KnownSlotKinds.TryGetValue(slotId, out kind);
    }
}
