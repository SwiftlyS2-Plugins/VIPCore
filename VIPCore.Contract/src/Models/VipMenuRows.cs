namespace VIPCore.Contract;

// What a row of VIPCore's menus (or of a module page built the same way) stands for, set as the
// component's Tag so a renderer can draw it from data rather than parse its text. A row whose state
// changes gets a new record and the session is invalidated, so a Tag never goes stale.

/// <summary>A perk on the Perks page.</summary>
public sealed record VipFeatureRow(string FeatureKey);

/// <summary>The heading over a feature's choices, or over one <see cref="VipFeatureOption.Group"/> of them.</summary>
public sealed record VipSectionRow(string FeatureKey, string? Group = null);

/// <summary>One choice: a Customize option, a gun.</summary>
public sealed record VipOptionRow(string FeatureKey, VipFeatureOption Option, bool Selected);

/// <summary>A VIP in the users list.</summary>
public sealed record VipUserRow(VipUserInfo User, bool Selected);

/// <summary>One of the selected VIP's grants; the expanded one shows its controls.</summary>
public sealed record VipGrantRow(VipGrant Grant, bool Expanded);

/// <summary>
/// A length to grant (null <see cref="Length"/>: permanent), or with <see cref="Extends"/> a step that extends that grant.
/// </summary>
public sealed record VipLengthRow(TimeSpan? Length, bool Selected, VipGrant? Extends = null);

/// <summary>A player to grant to. <see cref="Team"/> is "T", "CT" or "SPEC"; <see cref="Group"/> their active VIP group.</summary>
public sealed record VipPlayerRow(ulong SteamId, string Name, string? Team, string? Group, bool Selected);

/// <summary>A group to grant.</summary>
public sealed record VipGroupRow(VipGroupInfo Group, bool Selected);

/// <summary>Where a grant applies: every server on the database, or this one.</summary>
public sealed record VipScopeRow(bool Global, bool Selected);

/// <summary>A page's action or its summary line; <see cref="Grant"/> is the grant it acts on, if any.</summary>
public sealed record VipActionRow(VipAction Action, VipGrant? Grant = null);

public enum VipAction
{
    Search,
    Summary,
    Submit,
    MakePermanent,
    Remove,
    AddGroup,
    RemoveAll,
}
