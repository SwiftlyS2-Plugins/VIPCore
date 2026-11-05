using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;

namespace VIP_GunMenu;

public partial class VIP_GunMenu
{
    private DateTimeOffset? _roundStartedAt;

    [GameEventHandler(HookMode.Pre)]
    public HookResult OnRoundPrestart(EventRoundPrestart @event)
    {
        _roundId++;
        _gunMenuUsed.Clear();
        _commandEnabled = true;
        _roundStartedAt = null;
        RefreshPages();
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnRoundStart(EventRoundStart @event)
    {
        _roundStartedAt = DateTimeOffset.UtcNow;
        if (_pluginConfig.DisableCommandAfterRoundStarts)
        {
            var expectedRoundId = _roundId;
            Core.Scheduler.DelayBySeconds(_pluginConfig.CommandDisableDelayAfterRoundStarts, () =>
            {
                if (_roundId != expectedRoundId) return;
                _commandEnabled = false;
                RefreshPages();
            });
        }
        RefreshPages();
        return HookResult.Continue;
    }

    // When the command stops taking uses this round; null while it takes them all round.
    private DateTimeOffset? GetWindowEnd()
    {
        if (_pluginConfig.DisableCommandAfterRoundStarts && _roundStartedAt is { } started)
            return started.AddSeconds(_pluginConfig.CommandDisableDelayAfterRoundStarts);

        // Loaded mid-round: closed until the next round starts.
        return _commandEnabled ? null : DateTimeOffset.UtcNow;
    }

    private bool IsCommandWindowOpen() => _commandEnabled && (GetWindowEnd() is not { } end || end > DateTimeOffset.UtcNow);
}
