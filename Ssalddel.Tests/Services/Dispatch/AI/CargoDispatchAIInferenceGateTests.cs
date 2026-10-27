using Microsoft.Extensions.Options;
using 살뜰.Services.Dispatch.AI;

namespace Ssalddel.Tests.Services.Dispatch.AI;

public sealed class CargoDispatchAIInferenceGateTests
{
    [Fact]
    public void 실행중인Lease가있으면즉시Busy이며실패한획득은예산을소모하지않는다()
    {
        var settings = Settings(); settings.MaxCallsPerWindow = 2;
        var gate = new CargoDispatchAIInferenceGate(Options.Create(settings), new Clock());
        using (var first = gate.TryAcquire(out var acquired))
        {
            Assert.NotNull(first); Assert.Equal("Acquired", acquired);
            for (var i = 0; i < 5; i++)
            { Assert.Null(gate.TryAcquire(out var busy)); Assert.Equal("Busy", busy); }
        }
        using (var second = gate.TryAcquire(out var acquired))
        { Assert.NotNull(second); Assert.Equal("Acquired", acquired); }
        Assert.Null(gate.TryAcquire(out var exhausted)); Assert.Equal("BudgetExhausted", exhausted);
    }

    [Fact]
    public void Lease중복해제는다음진행중Lease를해제하지않는다()
    {
        var gate = new CargoDispatchAIInferenceGate(Options.Create(Settings()), new Clock());
        var first = gate.TryAcquire(out _)!;
        first.Dispose();
        using var second = gate.TryAcquire(out _);
        Assert.NotNull(second);
        first.Dispose(); first.RecordFailure(); first.Dispose();
        Assert.Null(gate.TryAcquire(out var busy)); Assert.Equal("Busy", busy);
    }

    [Fact]
    public void 실패휴지기는실패기록이아닌호출해제시점부터적용하고경계시각에해제된다()
    {
        var settings = Settings(); settings.FailureCooldownSeconds = 10;
        var clock = new Clock();
        var gate = new CargoDispatchAIInferenceGate(Options.Create(settings), clock);
        var first = gate.TryAcquire(out _)!;
        first.RecordFailure(); clock.Advance(TimeSpan.FromSeconds(7)); first.Dispose();
        Assert.Null(gate.TryAcquire(out var initial)); Assert.Equal("Cooldown", initial);
        clock.Advance(TimeSpan.FromSeconds(9));
        Assert.Null(gate.TryAcquire(out var still)); Assert.Equal("Cooldown", still);
        clock.Advance(TimeSpan.FromSeconds(1));
        using var next = gate.TryAcquire(out var expired);
        Assert.NotNull(next); Assert.Equal("Acquired", expired);
    }

    [Fact]
    public void 고정시간창예산은성공과실패시도모두세고요청마다시간창을늘리지않는다()
    {
        var settings = Settings(); settings.MaxCallsPerWindow = 2; settings.WindowSeconds = 10; settings.FailureCooldownSeconds = 1;
        var clock = new Clock(); var gate = new CargoDispatchAIInferenceGate(Options.Create(settings), clock);
        using (var first = gate.TryAcquire(out _)) { first!.RecordFailure(); }
        clock.Advance(TimeSpan.FromSeconds(1));
        using (var second = gate.TryAcquire(out _)) Assert.NotNull(second);
        clock.Advance(TimeSpan.FromSeconds(8));
        Assert.Null(gate.TryAcquire(out var blocked)); Assert.Equal("BudgetExhausted", blocked);
        clock.Advance(TimeSpan.FromSeconds(1));
        using var nextWindow = gate.TryAcquire(out var status);
        Assert.NotNull(nextWindow); Assert.Equal("Acquired", status);
    }

    [Fact]
    public void 실패없는해제는휴지기를추가하지않으며새실패는새해제시점으로갱신한다()
    {
        var settings = Settings(); settings.MaxCallsPerWindow = 10; settings.FailureCooldownSeconds = 5;
        var clock = new Clock(); var gate = new CargoDispatchAIInferenceGate(Options.Create(settings), clock);
        using (var first = gate.TryAcquire(out _)) Assert.NotNull(first);
        using (var second = gate.TryAcquire(out _)) { Assert.NotNull(second); second.RecordFailure(); }
        clock.Advance(TimeSpan.FromSeconds(5));
        using (var third = gate.TryAcquire(out _)) { Assert.NotNull(third); third.RecordFailure(); }
        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.Null(gate.TryAcquire(out var status)); Assert.Equal("Cooldown", status);
        clock.Advance(TimeSpan.FromSeconds(1));
        using var fourth = gate.TryAcquire(out status);
        Assert.NotNull(fourth); Assert.Equal("Acquired", status);
    }

    [Theory]
    [InlineData("timeout-low")]
    [InlineData("timeout-high")]
    [InlineData("candidates-low")]
    [InlineData("candidates-high")]
    [InlineData("budget-low")]
    [InlineData("budget-high")]
    [InlineData("window-low")]
    [InlineData("window-high")]
    [InlineData("cooldown-low")]
    [InlineData("cooldown-high")]
    [InlineData("remote-url")]
    [InlineData("other-model")]
    public void 잘못된설정이면공유추론슬롯을열지않는다(string mutation)
    {
        var settings = Settings();
        switch (mutation)
        {
            case "timeout-low": settings.TimeoutSeconds = 0; break;
            case "timeout-high": settings.TimeoutSeconds = 6; break;
            case "candidates-low": settings.MaxCandidates = 1; break;
            case "candidates-high": settings.MaxCandidates = 31; break;
            case "budget-low": settings.MaxCallsPerWindow = 0; break;
            case "budget-high": settings.MaxCallsPerWindow = 11; break;
            case "window-low": settings.WindowSeconds = 9; break;
            case "window-high": settings.WindowSeconds = 121; break;
            case "cooldown-low": settings.FailureCooldownSeconds = 0; break;
            case "cooldown-high": settings.FailureCooldownSeconds = 121; break;
            case "remote-url": settings.BaseUrl = "http://203.0.113.1:12681/"; break;
            case "other-model": settings.Model = "other-model"; break;
        }
        Assert.False(settings.Validate());
        var gate = new CargoDispatchAIInferenceGate(Options.Create(settings), new Clock());
        Assert.Null(gate.TryAcquire(out var status)); Assert.Equal("InvalidConfiguration", status);
    }

    private static CargoDispatchOllamaOptions Settings() => new() { Enabled = true };
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 21, 4, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now += by;
    }
}
