using System.Collections.Concurrent;
using Binesh.Ai.Configuration;
using Microsoft.Extensions.Options;

namespace Binesh.Ai.Orchestration;

/// <summary>
/// Rolling 24-hour per-user token budget kept in process memory. Adequate
/// for single-instance deployments; a Redis-backed implementation can drop
/// in later without touching the orchestrator. When
/// <see cref="OpenAiSettings.PerUserDailyTokens"/> is 0 the budget is
/// disabled and every <see cref="CanProceed"/> returns true.
/// </summary>
public sealed class InMemoryTokenBudget(IOptions<OpenAiSettings> settings, TimeProvider clock) : ITokenBudget
{
    private readonly ConcurrentDictionary<Guid, UserState> _states = new();
    private readonly int _dailyCap = settings.Value.PerUserDailyTokens;

    public bool CanProceed(Guid userId)
    {
        if (_dailyCap <= 0) { return true; }
        var state = _states.GetOrAdd(userId, _ => new UserState());
        lock (state.Gate)
        {
            Roll(state);
            return state.Consumed < _dailyCap;
        }
    }

    public void Charge(Guid userId, int tokens)
    {
        if (_dailyCap <= 0 || tokens <= 0) { return; }
        var state = _states.GetOrAdd(userId, _ => new UserState());
        lock (state.Gate)
        {
            Roll(state);
            state.Consumed += tokens;
        }
    }

    public int Remaining(Guid userId)
    {
        if (_dailyCap <= 0) { return int.MaxValue; }
        var state = _states.GetOrAdd(userId, _ => new UserState());
        lock (state.Gate)
        {
            Roll(state);
            return _dailyCap - state.Consumed;
        }
    }

    private void Roll(UserState state)
    {
        var now = clock.GetUtcNow();
        if (now - state.WindowStartedAt >= TimeSpan.FromDays(1))
        {
            state.WindowStartedAt = now;
            state.Consumed = 0;
        }
    }

    private sealed class UserState
    {
        public readonly object Gate = new();
        public DateTimeOffset WindowStartedAt = DateTimeOffset.MinValue;
        public int Consumed;
    }
}
