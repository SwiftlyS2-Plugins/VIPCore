namespace VIP_Chat;

public static class UiDeferredWork
{
    // NextTick is owned by the framework. No timer CTS is created or disposed by this module.
    public static void AfterTwoTicks(Action<Action> nextTick, Func<bool> active, Action work)
    {
        nextTick(() =>
        {
            if (!active()) return;
            nextTick(() => { if (active()) work(); });
        });
    }
}
