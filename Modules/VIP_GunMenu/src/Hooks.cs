using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;

namespace VIP_GunMenu;

public partial class VIP_GunMenu
{
    [GameEventHandler(HookMode.Pre)]
    public HookResult OnRoundPrestart(EventRoundPrestart @event)
    {
        _roundId++;
        _gunMenuUsed.Clear();
        _commandEnabled = true;
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnRoundStart(EventRoundStart @event)
    {
        if (_pluginConfig.DisableCommandAfterRoundStarts)
        {
            var expectedRoundId = _roundId;
            Core.Scheduler.DelayBySeconds(_pluginConfig.CommandDisableDelayAfterRoundStarts, () =>
            {
                if (_roundId == expectedRoundId)
                    _commandEnabled = false;
            });
        }
        return HookResult.Continue;
    }
}
