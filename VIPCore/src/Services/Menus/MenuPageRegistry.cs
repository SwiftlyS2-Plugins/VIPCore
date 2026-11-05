using System.Collections.Concurrent;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Players;
using VIPCore.Contract;

using Microsoft.Extensions.Logging;

namespace VIPCore.Services.Menus;

/// <summary>Module pages, listed in the VIP menu after VIPCore's own.</summary>
public class MenuPageRegistry(ISwiftlyCore core)
{
    private static readonly HashSet<string> ReservedIds = new(StringComparer.OrdinalIgnoreCase)
    {
        VipMenuIds.Hub, VipMenuIds.Perks, VipMenuIds.Customize, VipMenuIds.Users, VipMenuIds.UsersList, VipMenuIds.Grant
    };

    private readonly ConcurrentDictionary<string, VipMenuPage> _pages = new(StringComparer.OrdinalIgnoreCase);

    public bool IsEmpty => _pages.IsEmpty;

    public void Register(VipMenuPage page)
    {
        if (page == null || string.IsNullOrWhiteSpace(page.Id)) return;

        if (ReservedIds.Contains(page.Id) || page.Id.StartsWith(VipMenuIds.UsersList + ".", StringComparison.OrdinalIgnoreCase))
        {
            core.Logger.LogWarning("[VIPCore] Menu page id '{Id}' belongs to VIPCore; the page was not added.", page.Id);
            return;
        }

        _pages[page.Id] = page;
    }

    public void Unregister(string pageId)
    {
        if (!string.IsNullOrEmpty(pageId))
            _pages.TryRemove(pageId, out _);
    }

    public VipMenuPage? Find(string pageId) => _pages.TryGetValue(pageId, out var page) ? page : null;

    /// <summary>The pages this player may open, lowest Order first.</summary>
    public IReadOnlyList<VipMenuPage> Available(IPlayer player) =>
        _pages.Values
            .OrderBy(page => page.Order)
            .ThenBy(page => page.Id, StringComparer.OrdinalIgnoreCase)
            .Where(page => IsAvailable(page, player))
            .ToList();

    public string Label(VipMenuPage page, IPlayer player)
    {
        var label = Guard<string?>(page, nameof(VipMenuPage.Label), () => page.Label(player), null, LogLevel.Debug);
        return string.IsNullOrEmpty(label) ? page.Id : label;
    }

    /// <summary>Builds a page under the hub; null when the player may not open it or its builder fails.</summary>
    public IMenu? Build(VipMenuPage page, IPlayer player, IMenu hub)
    {
        if (!IsAvailable(page, player)) return null;

        var menu = Guard<IMenu?>(page, nameof(VipMenuPage.Build), () => page.Build(player), null, LogLevel.Warning);
        if (menu != null)
            menu.Parent = hub;

        return menu;
    }

    private bool IsAvailable(VipMenuPage page, IPlayer player) =>
        Guard(page, nameof(VipMenuPage.IsAvailable), () => page.IsAvailable(player), false, LogLevel.Debug);

    private T Guard<T>(VipMenuPage page, string member, Func<T> call, T fallback, LogLevel level)
    {
        try
        {
            return call();
        }
        catch (Exception ex)
        {
            core.Logger.Log(level, ex, "[VIPCore] Menu page '{Id}' threw from {Member}.", page.Id, member);
            return fallback;
        }
    }
}
