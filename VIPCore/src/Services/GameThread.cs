using SwiftlyS2.Shared;

namespace VIPCore.Services;

internal static class GameThread
{
    /// <summary>Runs now when already on the game thread, otherwise on the next tick.</summary>
    public static void Post(ISwiftlyCore core, Action action)
    {
        if (core.IsGameThread)
            action();
        else
            core.Scheduler.NextTick(action);
    }

    /// <summary>Reads game state on the game thread; the caller continues on the thread pool.</summary>
    public static Task<T> Read<T>(ISwiftlyCore core, Func<T> read)
    {
        if (core.IsGameThread)
            return Task.FromResult(read());

        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        core.Scheduler.NextTick(() =>
        {
            try { result.SetResult(read()); }
            catch (Exception ex) { result.SetException(ex); }
        });
        return result.Task;
    }
}
