using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace VIPCore.Services;

internal static class MenuExtensions
{
    // The classic builder filled an empty menu with SwiftlyS2's own "No options" row; Core.Menu draws only the title.
    public static IMenu WithDefaultOption(this IMenu menu)
    {
        if (menu.GetComponents(MenuRegion.Body).Count == 0)
        {
            menu.Add(MenuRegion.Body, new NoOptionsComponent());
        }

        return menu;
    }

    public static IMenu FreezeWhileOpen(this IMenu menu, ISwiftlyCore core)
    {
        menu.Opened += session => SetFrozen(core, session.Player, true);
        menu.Closed += session => SetFrozen(core, session.Player, false);
        return menu;
    }

    /// <summary>Moves the selection to a component by id; nothing happens when it is not on the page.</summary>
    public static void FocusOn(this IMenuSession session, string? componentId)
    {
        if (componentId is null) return;

        var index = 0;
        foreach (var component in session.Menu.GetComponents(MenuRegion.Body))
        {
            if (!component.IsFocusable || !component.IsVisible(session)) continue;
            if (component.Id == componentId)
            {
                session.SetFocus(index);
                return;
            }

            index++;
        }
    }

    // Opened fires for the first open and again whenever a child page returns here; only the first should move the selection.
    public static IMenu FocusOnFirstOpen(this IMenu menu, string componentId)
    {
        Action<IMenuSession>? focus = null;
        focus = session =>
        {
            menu.Opened -= focus;
            session.FocusOn(componentId);
        };
        menu.Opened += focus;
        return menu;
    }

    public static void InvalidateSessions(this IMenu menu)
    {
        foreach (var session in menu.Sessions)
        {
            session.Invalidate();
        }
    }

    // Not deferred on the game thread: unloading closes these menus while cancelling the plugin's scheduler, which drops a queued NextTick.
    private static void SetFrozen(ISwiftlyCore core, IPlayer player, bool frozen)
    {
        if (core.IsGameThread)
        {
            ApplyFrozen(player, frozen);
            return;
        }

        core.Scheduler.NextTick(() => ApplyFrozen(player, frozen));
    }

    private static void ApplyFrozen(IPlayer player, bool frozen)
    {
        if (!player.IsValid || player.PlayerPawn is not { IsValid: true } pawn)
        {
            return;
        }

        var moveType = frozen ? MoveType_t.MOVETYPE_NONE : MoveType_t.MOVETYPE_WALK;
        pawn.MoveType = moveType;
        pawn.ActualMoveType = moveType;
        pawn.MoveTypeUpdated();
    }

    private sealed class NoOptionsComponent : TextComponent
    {
        public override MenuNode Render(IMenuComponentRenderContext context)
            => new MenuTextNode(context.Translate("menu.default_option"), Style);
    }
}
