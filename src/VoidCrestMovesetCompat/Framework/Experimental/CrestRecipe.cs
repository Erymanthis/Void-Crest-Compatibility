using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace VoidCrestMovesetCompat.Framework.Experimental;

public sealed class CrestFeatureSelection
{
    public CrestFeatureSelection(string slotId, string featureId)
    {
        if (string.IsNullOrWhiteSpace(slotId))
        {
            throw new ArgumentException("A slot ID is required.", nameof(slotId));
        }

        if (string.IsNullOrWhiteSpace(featureId))
        {
            throw new ArgumentException("A feature ID is required.", nameof(featureId));
        }

        SlotId = slotId.Trim();
        FeatureId = featureId.Trim();
    }

    public string SlotId { get; }

    public string FeatureId { get; }
}

/// <summary>
/// Immutable declaration of which feature supplies each exclusive slot and which additive
/// features (passives, state services, and lifecycle hooks) should also be installed.
/// </summary>
public sealed class CrestRecipe
{
    internal CrestRecipe(
        string id,
        string hostCrestId,
        IDictionary<string, CrestFeatureSelection> slots,
        IEnumerable<string> additiveFeatureIds)
    {
        Id = id;
        HostCrestId = hostCrestId;
        Slots = new ReadOnlyDictionary<string, CrestFeatureSelection>(
            new Dictionary<string, CrestFeatureSelection>(slots, StringComparer.Ordinal));
        AdditiveFeatureIds = Array.AsReadOnly(additiveFeatureIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray());
    }

    public string Id { get; }

    public string HostCrestId { get; }

    public IReadOnlyDictionary<string, CrestFeatureSelection> Slots { get; }

    public IReadOnlyCollection<string> AdditiveFeatureIds { get; }

    public IEnumerable<string> GetAllFeatureIds()
    {
        return Slots.Values.Select(selection => selection.FeatureId)
            .Concat(AdditiveFeatureIds)
            .Distinct(StringComparer.Ordinal);
    }
}

public sealed class CrestRecipeBuilder
{
    private readonly string id;
    private readonly string hostCrestId;
    private readonly Dictionary<string, CrestFeatureSelection> slots = new(StringComparer.Ordinal);
    private readonly HashSet<string> additiveFeatureIds = new(StringComparer.Ordinal);

    private CrestRecipeBuilder(string id, string hostCrestId)
    {
        this.id = RequireValue(id, nameof(id));
        this.hostCrestId = RequireValue(hostCrestId, nameof(hostCrestId));
    }

    public static CrestRecipeBuilder Create(string id, string hostCrestId)
    {
        return new CrestRecipeBuilder(id, hostCrestId);
    }

    public CrestRecipeBuilder Use(string slotId, string featureId)
    {
        CrestFeatureSelection selection = new(slotId, featureId);
        slots[selection.SlotId] = selection;
        return this;
    }

    public CrestRecipeBuilder Add(string featureId)
    {
        additiveFeatureIds.Add(RequireValue(featureId, nameof(featureId)));
        return this;
    }

    public CrestRecipe Build()
    {
        return new CrestRecipe(id, hostCrestId, slots, additiveFeatureIds);
    }

    private static string RequireValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value.Trim();
    }
}

public enum CrestRecipeIssueSeverity
{
    Information,
    Warning,
    Error
}

public sealed class CrestRecipeIssue
{
    public CrestRecipeIssue(CrestRecipeIssueSeverity severity, string code, string message, string? featureId = null)
    {
        Severity = severity;
        Code = code;
        Message = message;
        FeatureId = featureId;
    }

    public CrestRecipeIssueSeverity Severity { get; }

    public string Code { get; }

    public string Message { get; }

    public string? FeatureId { get; }
}

public sealed class CrestRecipeValidationResult
{
    internal CrestRecipeValidationResult(IEnumerable<CrestRecipeIssue> issues)
    {
        Issues = Array.AsReadOnly(issues.ToArray());
    }

    public IReadOnlyCollection<CrestRecipeIssue> Issues { get; }

    public bool IsValid => Issues.All(issue => issue.Severity != CrestRecipeIssueSeverity.Error);
}

public static class CrestRecipeValidator
{
    public static CrestRecipeValidationResult Validate(CrestRecipe recipe, CrestFeatureRegistry registry)
    {
        if (recipe == null)
        {
            throw new ArgumentNullException(nameof(recipe));
        }

        if (registry == null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        List<CrestRecipeIssue> issues = new();
        Dictionary<string, CrestFeatureDescriptor> selected = new(StringComparer.Ordinal);

        foreach (CrestFeatureSelection selection in recipe.Slots.Values)
        {
            if (!registry.TryGet(selection.FeatureId, out CrestFeatureDescriptor feature))
            {
                issues.Add(new CrestRecipeIssue(
                    CrestRecipeIssueSeverity.Error,
                    "unknown-feature",
                    $"Slot '{selection.SlotId}' references unregistered feature '{selection.FeatureId}'.",
                    selection.FeatureId));
                continue;
            }

            selected[feature.Id] = feature;
            if (StandardCrestSlots.TryGetExpectedKind(selection.SlotId, out CrestFeatureKind expectedKind) &&
                feature.Kind != expectedKind)
            {
                issues.Add(new CrestRecipeIssue(
                    CrestRecipeIssueSeverity.Error,
                    "slot-kind-mismatch",
                    $"Slot '{selection.SlotId}' accepts {expectedKind} features, but '{feature.Id}' is {feature.Kind}.",
                    feature.Id));
            }
        }

        foreach (string featureId in recipe.AdditiveFeatureIds)
        {
            if (!registry.TryGet(featureId, out CrestFeatureDescriptor feature))
            {
                issues.Add(new CrestRecipeIssue(
                    CrestRecipeIssueSeverity.Error,
                    "unknown-feature",
                    $"Additive feature '{featureId}' is not registered.",
                    featureId));
                continue;
            }

            selected[feature.Id] = feature;
        }

        HashSet<string> selectedIds = new(selected.Keys, StringComparer.Ordinal);
        foreach (CrestFeatureDescriptor feature in selected.Values)
        {
            foreach (string dependency in feature.Dependencies.Where(dependency => !selectedIds.Contains(dependency)))
            {
                issues.Add(new CrestRecipeIssue(
                    CrestRecipeIssueSeverity.Error,
                    "missing-dependency",
                    $"Feature '{feature.Id}' requires '{dependency}'.",
                    feature.Id));
            }

            foreach (string conflict in feature.Conflicts.Where(selectedIds.Contains))
            {
                issues.Add(new CrestRecipeIssue(
                    CrestRecipeIssueSeverity.Error,
                    "feature-conflict",
                    $"Feature '{feature.Id}' conflicts with selected feature '{conflict}'.",
                    feature.Id));
            }

            if (feature.RequiresFunctionalIdentity)
            {
                issues.Add(new CrestRecipeIssue(
                    CrestRecipeIssueSeverity.Information,
                    "identity-proxy-required",
                    $"Feature '{feature.Id}' requires a scoped functional-identity adapter.",
                    feature.Id));
            }

            if (feature.LifecycleSensitive)
            {
                issues.Add(new CrestRecipeIssue(
                    CrestRecipeIssueSeverity.Information,
                    "lifecycle-management-required",
                    $"Feature '{feature.Id}' must be revalidated after crest-state resets and configuration updates.",
                    feature.Id));
            }
        }

        CrestRecipeIssue[] normalized = issues
            .GroupBy(issue => $"{issue.Severity}\0{issue.Code}\0{issue.FeatureId}\0{issue.Message}", StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderByDescending(issue => issue.Severity)
            .ThenBy(issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(issue => issue.FeatureId, StringComparer.Ordinal)
            .ToArray();
        return new CrestRecipeValidationResult(normalized);
    }
}
