using System.Collections.Concurrent;

namespace TrainArena.Game;

/// <summary>
/// Process-wide delay scheduler for room timers (question end, auto-advance, async timeouts, cleanup).
/// Replaces static ConcurrentDictionaries on <c>GameHub</c>.
/// </summary>
public sealed class RoomScheduler : IDisposable
{
    /// <summary>How long Finished rooms stay available for RejoinHost / review restore.</summary>
    public static readonly TimeSpan FinishedRetention = TimeSpan.FromMinutes(30);

    private readonly ConcurrentDictionary<string, CancellationTokenSource> _questionOrAsyncTimers =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _autoAdvanceTimers =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cleanupTimers =
        new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>Schedule (or replace) the question-end / async-timeout timer for a room.</summary>
    public void ScheduleQuestionOrAsync(string code, TimeSpan delay, Func<CancellationToken, Task> action) =>
        Schedule(_questionOrAsyncTimers, code, delay, action);

    /// <summary>Schedule (or replace) the auto-advance timer for a room.</summary>
    public void ScheduleAutoAdvance(string code, TimeSpan delay, Func<CancellationToken, Task> action) =>
        Schedule(_autoAdvanceTimers, code, delay, action);

    /// <summary>Schedule (or replace) deferred session removal after Finished.</summary>
    public void ScheduleCleanup(string code, TimeSpan delay, Func<CancellationToken, Task> action) =>
        Schedule(_cleanupTimers, code, delay, action);

    private void Schedule(
        ConcurrentDictionary<string, CancellationTokenSource> map,
        string code,
        TimeSpan delay,
        Func<CancellationToken, Task> action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(action);
        ObjectDisposedException.ThrowIf(_disposed, this);

        Cancel(map, code);
        var cts = new CancellationTokenSource();
        map[code] = cts;
        var token = cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay < TimeSpan.Zero ? TimeSpan.Zero : delay, token);
                await action(token);
            }
            catch (OperationCanceledException)
            {
                // Superseded or cancelled.
            }
            catch
            {
                // Timer best-effort.
            }
        }, CancellationToken.None);
    }

    public bool CancelQuestionOrAsync(string code) => Cancel(_questionOrAsyncTimers, code);

    public bool CancelAutoAdvance(string code) => Cancel(_autoAdvanceTimers, code);

    public bool CancelCleanup(string code) => Cancel(_cleanupTimers, code);

    private static bool Cancel(ConcurrentDictionary<string, CancellationTokenSource> map, string code)
    {
        if (!map.TryRemove(code, out var existing))
        {
            return false;
        }

        existing.Cancel();
        existing.Dispose();
        return true;
    }

    /// <summary>Cancel gameplay timers for a room (question/async + auto-advance). Cleanup timer kept unless CancelAll.</summary>
    public void CancelGameplay(string code)
    {
        CancelQuestionOrAsync(code);
        CancelAutoAdvance(code);
    }

    /// <summary>Cancel all timers for a room (including deferred cleanup).</summary>
    public void CancelAll(string code)
    {
        CancelGameplay(code);
        CancelCleanup(code);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var key in _questionOrAsyncTimers.Keys.ToList())
        {
            CancelQuestionOrAsync(key);
        }

        foreach (var key in _autoAdvanceTimers.Keys.ToList())
        {
            CancelAutoAdvance(key);
        }

        foreach (var key in _cleanupTimers.Keys.ToList())
        {
            CancelCleanup(key);
        }
    }
}
