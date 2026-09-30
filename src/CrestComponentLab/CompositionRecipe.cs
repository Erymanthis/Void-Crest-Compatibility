using System;

namespace CrestComponentLab;

internal readonly struct CompositionRecipe : IEquatable<CompositionRecipe>
{
    internal CompositionRecipe(
        ComponentSource core,
        ComponentSource dash,
        ComponentSource charge,
        ComponentSource bind,
        ComponentSource specialBind)
    {
        Core = core;
        Dash = dash;
        Charge = charge;
        Bind = bind;
        SpecialBind = specialBind;
    }

    internal ComponentSource Core { get; }
    internal ComponentSource Dash { get; }
    internal ComponentSource Charge { get; }
    internal ComponentSource Bind { get; }
    internal ComponentSource SpecialBind { get; }

    internal static CompositionRecipe FromConfig() => new(
        Plugin.MovesetSource.Value,
        Plugin.DashSource.Value,
        Plugin.ChargeSource.Value,
        Plugin.BindSource.Value,
        Plugin.SpecialBindSource.Value);

    public bool Equals(CompositionRecipe other) =>
        Core == other.Core &&
        Dash == other.Dash &&
        Charge == other.Charge &&
        Bind == other.Bind &&
        SpecialBind == other.SpecialBind;

    public override bool Equals(object? obj) => obj is CompositionRecipe other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (int)Core;
            hash = (hash * 397) ^ (int)Dash;
            hash = (hash * 397) ^ (int)Charge;
            hash = (hash * 397) ^ (int)Bind;
            return (hash * 397) ^ (int)SpecialBind;
        }
    }

    public override string ToString() =>
        $"moveset={Core}, dash={Dash}, charge={Charge}, bind={Bind}, specialBind={SpecialBind}";
}
