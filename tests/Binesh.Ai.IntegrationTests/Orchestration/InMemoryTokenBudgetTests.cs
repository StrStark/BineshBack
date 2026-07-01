using Binesh.Ai.Configuration;
using Binesh.Ai.Orchestration;
using Microsoft.Extensions.Options;

namespace Binesh.Ai.IntegrationTests.Orchestration;

public sealed class InMemoryTokenBudgetTests
{
    [Fact]
    public void DefaultBudget_AllowsUntilExhausted()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var budget = MakeBudget(perUserDailyTokens: 1_000, clock);
        var userId = Guid.NewGuid();

        Assert.True(budget.CanProceed(userId));
        budget.Charge(userId, 900);
        Assert.True(budget.CanProceed(userId));   // 100 remaining
        budget.Charge(userId, 200);                 // overdraw to -100
        Assert.False(budget.CanProceed(userId));
        Assert.Equal(-100, budget.Remaining(userId));
    }

    [Fact]
    public void RollingWindow_ResetsAfter24Hours()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var budget = MakeBudget(perUserDailyTokens: 1_000, clock);
        var userId = Guid.NewGuid();

        budget.Charge(userId, 999);
        Assert.True(budget.CanProceed(userId));
        budget.Charge(userId, 50);
        Assert.False(budget.CanProceed(userId));

        clock.Advance(TimeSpan.FromHours(23));
        Assert.False(budget.CanProceed(userId));   // window still active

        clock.Advance(TimeSpan.FromHours(2));
        Assert.True(budget.CanProceed(userId));    // window rolled
        Assert.Equal(1_000, budget.Remaining(userId));
    }

    [Fact]
    public void DisabledBudget_AlwaysAllows()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var budget = MakeBudget(perUserDailyTokens: 0, clock);
        var userId = Guid.NewGuid();

        Assert.True(budget.CanProceed(userId));
        budget.Charge(userId, 99_999_999);
        Assert.True(budget.CanProceed(userId));
        Assert.Equal(int.MaxValue, budget.Remaining(userId));
    }

    [Fact]
    public void PerUserIsolation()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var budget = MakeBudget(perUserDailyTokens: 1_000, clock);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        budget.Charge(a, 1_200);
        Assert.False(budget.CanProceed(a));
        Assert.True(budget.CanProceed(b));
    }

    private static InMemoryTokenBudget MakeBudget(int perUserDailyTokens, FakeClock clock) =>
        new(Options.Create(new OpenAiSettings
        {
            ApiKey = "x", Model = "x", PerUserDailyTokens = perUserDailyTokens,
        }), clock);

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
