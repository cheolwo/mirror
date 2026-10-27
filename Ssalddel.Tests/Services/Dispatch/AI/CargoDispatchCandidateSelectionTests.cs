using 살뜰.Services.Dispatch.AI;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;

namespace Ssalddel.Tests.Services.Dispatch.AI;

public sealed class CargoDispatchCandidateSelectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 비활성이나연결없음이면_기존후보를그대로반환하고추가호출하지않는다(bool missing)
    {
        var initial = Trace(Row("a"), Row("b"));
        var ai = new FakeAI { Enabled = false };
        var result = await CargoDispatchCandidateSelection.SelectAsync(initial, missing ? null : ai,
            _ => throw new InvalidOperationException("Disabled mode must not refresh."));
        Assert.Same(initial.선정후보, result);
        Assert.Equal(0, ai.Calls);
    }

    [Fact]
    public async Task 초기규칙후보없음이면_모델이나재조회를호출하지않는다()
    {
        var ai = new FakeAI();
        var result = await CargoDispatchCandidateSelection.SelectAsync(Trace() with { 선정후보 = null }, ai,
            _ => throw new InvalidOperationException("No baseline must not refresh."));
        Assert.Null(result);
        Assert.Equal(0, ai.Calls);
    }

    [Fact]
    public async Task 초기와현재허용된선택은_최신서버후보의점수와수행근거를보존한다()
    {
        var initial = Trace(Row("a"), Row("b"));
        var latest = Row("b", score: 15m, basis: "current-basis");
        var current = Trace(Row("a", score: 12m), latest);
        var ai = new FakeAI { Decision = new("b", "Applied") };
        var sequence = new List<string>();
        ai.OnSelect = (_, _) => { sequence.Add("model"); return Task.FromResult(ai.Decision); };

        var result = await CargoDispatchCandidateSelection.SelectAsync(initial, ai, _ =>
        {
            sequence.Add("refresh"); return Task.FromResult<용달운송후보검토결과?>(current);
        });

        Assert.Equal("b", result?.DriverId);
        Assert.Equal(15m, result?.추천점수);
        Assert.Same(latest.서버후보!.화물수행근거, result?.화물수행근거);
        Assert.Equal(latest.서버후보.추천사유 + " · CargoDispatchAI.Applied", result?.추천사유);
        Assert.Equal(new[] { "model", "refresh" }, sequence);
        Assert.Equal(1, ai.Calls);
    }

    [Theory]
    [InlineData("excluded")]
    [InlineData("unlisted")]
    [InlineData("unknown")]
    [InlineData("")]
    public async Task 초기탈락이나허용목록밖ID는_현재규칙으로복귀한다(string requested)
    {
        var initial = Trace(Row("a"), Row("b"), Row("excluded", verdict: "DriverReceivingOff"), Row("unlisted"))
            with { 최종허용기사Ids = ["a", "b", "excluded"] };
        var current = Trace(Row("a", score: 21m, basis: "new-baseline"), Row("b"), Row("excluded"), Row("unlisted"));
        var result = await Select(initial, current, new(requested, "Applied"));
        AssertFallback(current, result, "CandidateChanged");
    }

    [Fact]
    public async Task 응답대기중새로추가된후보는_초기후보처럼선택할수없다()
    {
        var rows = new List<용달운송후보검토항목> { Row("a"), Row("b") };
        var allowed = new List<string> { "a", "b" };
        var initial = new 용달운송후보검토결과(rows[0].서버후보, rows, allowed, null);
        var current = Trace(Row("a"), Row("b"), Row("new"));
        var ai = new FakeAI { OnSelect = (_, _) =>
        {
            rows.Add(Row("new")); allowed.Add("new");
            return Task.FromResult(new CargoDispatchAISelection("new", "Applied"));
        } };
        var result = await CargoDispatchCandidateSelection.SelectAsync(initial, ai,
            _ => Task.FromResult<용달운송후보검토결과?>(current));
        AssertFallback(current, result, "CandidateChanged");
    }

    [Theory]
    [InlineData("removed")]
    [InlineData("receiving-off")]
    [InlineData("no-arrival")]
    [InlineData("lower-aging")]
    [InlineData("server-id-changed")]
    public async Task 응답중후보적격성이바뀌면_현재규칙의최신근거로복귀한다(string change)
    {
        var initial = Trace(Row("a"), Row("b"));
        var changed = Row("b");
        changed = change switch
        {
            "receiving-off" => changed with { 판정Code = "DriverReceivingOff" },
            "no-arrival" => changed with { 픽업도착예상시각Utc = null },
            "lower-aging" => changed with { 대기점수 = 4m },
            "server-id-changed" => changed with { 서버후보 = changed.서버후보! with { DriverId = "other" } },
            _ => changed
        };
        var current = change == "removed" ? Trace(Row("a", basis: "fresh")) : Trace(Row("a", basis: "fresh"), changed);
        AssertFallback(current, await Select(initial, current, new("b", "Applied")), "CandidateChanged");
    }

    [Fact]
    public async Task 모델이현재규칙후보를골라도_그행의최신수행근거를보존한다()
    {
        var initial = Trace(Row("a"), Row("b"));
        var latest = Row("a", score: 50m, basis: "latest-row");
        var current = Trace(latest, Row("b"));
        var result = await Select(initial, current, new("a", "Applied"));
        Assert.Same(latest.서버후보!.화물수행근거, result?.화물수행근거);
        Assert.Equal(50m, result?.추천점수);
        Assert.EndsWith("CargoDispatchAI.Applied", result?.추천사유);
    }

    [Theory]
    [InlineData("Timeout")]
    [InlineData("NetworkFailure")]
    [InlineData("InvalidResponse")]
    [InlineData("Unavailable")]
    [InlineData("SingleCandidate")]
    [InlineData("NoCandidates")]
    [InlineData("Disabled")]
    [InlineData("InvalidConfiguration")]
    [InlineData("ResponseTooLarge")]
    [InlineData("HttpFailure")]
    [InlineData("InvalidSelection")]
    [InlineData("Busy")]
    [InlineData("Cooldown")]
    [InlineData("BudgetExhausted")]
    public async Task 모델실패는_초기후보가아닌현재규칙후보로복귀한다(string status)
    {
        var current = Trace(Row("new-baseline", score: 88m, basis: "new-basis"), Row("b"));
        var result = await Select(Trace(Row("a"), Row("b")), current, new("a", status));
        AssertFallback(current, result, status);
    }

    [Fact]
    public async Task 임의상태문구는_추천사유에복사하지않는다()
    {
        var current = Trace(Row("a"), Row("b"));
        var result = await Select(current, current, new("b", "private-text-and-newline\n"));
        AssertFallback(current, result, "CandidateChanged");
        Assert.DoesNotContain("private-text", result!.추천사유);
    }

    [Fact]
    public async Task 모델예외도_현재규칙으로복귀하고모델을재호출하지않는다()
    {
        var initial = Trace(Row("a"), Row("b"));
        var current = Trace(Row("current"));
        var ai = new FakeAI { OnSelect = (_, _) => throw new HttpRequestException("Synthetic model unavailable.") };
        var refreshes = 0;
        var result = await CargoDispatchCandidateSelection.SelectAsync(initial, ai, _ =>
        {
            refreshes++; return Task.FromResult<용달운송후보검토결과?>(current);
        });
        AssertFallback(current, result, "Unavailable");
        Assert.Equal(1, ai.Calls);
        Assert.Equal(1, refreshes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 재조회시주문이나규칙후보가사라지면_초기선택을반환하지않는다(bool noBaseline)
    {
        var result = await Select(Trace(Row("a"), Row("b")), noBaseline ? Trace() : null, new("b", "Applied"));
        Assert.Null(result);
    }

    [Fact]
    public async Task 정본재조회실패는_오래된후보로숨기지않고전파한다()
    {
        var ai = new FakeAI { Decision = new(null, "Timeout") };
        var expected = new InvalidOperationException("Synthetic source read failure.");
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => CargoDispatchCandidateSelection.SelectAsync(
            Trace(Row("a"), Row("b")), ai, _ => throw expected));
        Assert.Same(expected, actual);
        Assert.Equal(1, ai.Calls);
    }

    [Fact]
    public async Task 단일후보도_모델어댑터결과뒤현재후보를한번재조회한다()
    {
        var current = Trace(Row("a", score: 99m));
        var ai = new FakeAI { Decision = new("a", "SingleCandidate") };
        var refreshes = 0;
        var result = await CargoDispatchCandidateSelection.SelectAsync(Trace(Row("a")), ai, _ =>
        {
            refreshes++; return Task.FromResult<용달운송후보검토결과?>(current);
        });
        AssertFallback(current, result, "SingleCandidate");
        Assert.Equal(1, ai.Calls);
        Assert.Equal(1, refreshes);
    }

    [Fact]
    public async Task 호출취소는_모델실패로숨기지않고재조회하지않는다()
    {
        using var canceled = new CancellationTokenSource();
        var ai = new FakeAI { OnSelect = (_, token) =>
        {
            canceled.Cancel(); token.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Cancellation was ignored.");
        } };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CargoDispatchCandidateSelection.SelectAsync(
            Trace(Row("a"), Row("b")), ai,
            _ => throw new InvalidOperationException("Canceled request must not refresh."), canceled.Token));
        Assert.Equal(1, ai.Calls);
    }

    [Fact]
    public async Task 모델응답직후취소도_오래된성공이나재조회로넘기지않는다()
    {
        using var canceled = new CancellationTokenSource();
        var ai = new FakeAI { OnSelect = (_, _) =>
        {
            canceled.Cancel(); return Task.FromResult(new CargoDispatchAISelection("b", "Applied"));
        } };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CargoDispatchCandidateSelection.SelectAsync(
            Trace(Row("a"), Row("b")), ai,
            _ => throw new InvalidOperationException("Canceled request must not refresh."), canceled.Token));
    }

    [Fact]
    public async Task 재조회가취소를무시해도_취소된후보를반환하지않는다()
    {
        using var canceled = new CancellationTokenSource();
        var initial = Trace(Row("a"), Row("b"));
        var ai = new FakeAI();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CargoDispatchCandidateSelection.SelectAsync(initial, ai, token =>
        {
            Assert.Equal(canceled.Token, token);
            canceled.Cancel(); return Task.FromResult<용달운송후보검토결과?>(initial);
        }, canceled.Token));
        Assert.Equal(1, ai.Calls);
    }

    private static Task<배차추천후보?> Select(용달운송후보검토결과 initial, 용달운송후보검토결과? current, CargoDispatchAISelection decision)
        => CargoDispatchCandidateSelection.SelectAsync(initial, new FakeAI { Decision = decision },
            _ => Task.FromResult(current));

    private static void AssertFallback(용달운송후보검토결과 current, 배차추천후보? result, string status)
    {
        Assert.NotNull(result);
        Assert.Equal(current.선정후보!.DriverId, result.DriverId);
        Assert.Equal(current.선정후보.추천점수, result.추천점수);
        Assert.Same(current.선정후보.화물수행근거, result.화물수행근거);
        Assert.Equal(current.선정후보.추천사유 + " · CargoDispatchAI." + status, result.추천사유);
    }

    private static 용달운송후보검토결과 Trace(params 용달운송후보검토항목[] rows)
        => new(rows.FirstOrDefault()?.서버후보, rows, rows.Select(x => x.기사Id).ToArray(), null);

    private static 용달운송후보검토항목 Row(string id, decimal score = 10m, string? basis = null, string verdict = "CandidateAllowed")
    {
        var reason = "server-reason-" + id;
        return new(id, verdict, 1m, score - 5m, 5m, score, reason)
        {
            픽업도착예상시각Utc = new DateTime(2026, 10, 21, 4, 0, 0, DateTimeKind.Utc),
            서버후보 = new(id, score, reason)
            {
                화물수행근거 = new(false, true, new(37.58m, 127.08m),
                    new DateTime(2026, 10, 21, 3, 55, 0, DateTimeKind.Utc), null, "CurrentLocationEstimate")
                    { 원장판본 = basis ?? "basis-" + id }
            }
        };
    }

    private sealed class FakeAI : ICargoDispatchCandidateAI
    {
        public bool Enabled { get; init; } = true;
        public int Calls { get; private set; }
        public CargoDispatchAISelection Decision { get; init; } = new("b", "Applied");
        public Func<용달운송후보검토결과, CancellationToken, Task<CargoDispatchAISelection>>? OnSelect { get; set; }
        public Task<CargoDispatchAISelection> SelectAsync(용달운송후보검토결과 trace, CancellationToken cancellationToken = default)
        {
            Calls++;
            return OnSelect?.Invoke(trace, cancellationToken) ?? Task.FromResult(Decision);
        }
    }
}
