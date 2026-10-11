using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Services.Food;
using Ssalddel.Tests.Services.Dispatch.Common;
using 살뜰.Data;
using 살뜰.Services.Dispatch.AI;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;

namespace Ssalddel.Tests.Services.Dispatch.Queue;

public sealed partial class 음식배달배차업무정책Tests
{
    [Fact]
    public async Task Ollama_OFF는기존선정과사유를그대로반환하고모델을호출하지않는다()
    {
        var (states, spaces) = await OllamaCandidatesAsync();
        var ai = new OllamaPolicyPort((_, _) => throw new InvalidOperationException(), enabled: false);
        var policy = CreateOllamaPolicy(states, spaces, ai);
        var review = await policy.후보검토Async(Queue());

        Assert.Equal(review.선정후보, await policy.다음후보선정Async(Queue()));
        Assert.Equal(0, ai.Calls);
    }

    [Fact]
    public async Task 읽기전용후보검토는Ollama_ON이어도모델을호출하지않는다()
    {
        var (states, spaces) = await OllamaCandidatesAsync();
        var ai = new OllamaPolicyPort((_, _) => throw new InvalidOperationException());

        Assert.NotNull((await CreateOllamaPolicy(states, spaces, ai).후보검토Async(Queue())).선정후보);
        Assert.Equal(0, ai.Calls);
    }

    [Fact]
    public async Task Ollama의허용선택은현재후보의점수와고정상태사유만반환한다()
    {
        var (states, spaces) = await OllamaCandidatesAsync();
        var ai = new OllamaPolicyPort((_, _) => Task.FromResult(new FoodDispatchAISelection("alternate", "Applied")));
        var policy = CreateOllamaPolicy(states, spaces, ai);
        var queue = Queue();
        var before = System.Text.Json.JsonSerializer.Serialize(queue);

        var selected = await policy.다음후보선정Async(queue);
        var current = await policy.후보검토Async(queue);

        Assert.Equal("alternate", selected?.DriverId);
        Assert.Equal(Assert.Single(current.후보목록, x => x.기사Id == "alternate").추천점수, selected!.추천점수);
        Assert.Contains("FoodDispatchAI.Applied", selected.추천사유);
        Assert.Equal(1, ai.Calls);
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(queue));
    }

    [Theory]
    [InlineData("unknown", "Applied", "CandidateChanged")]
    [InlineData("alternate", "untrusted free text", "CandidateChanged")]
    [InlineData("alternate", "Timeout", "Timeout")]
    [InlineData("alternate", "InvalidResponse", "InvalidResponse")]
    [InlineData("alternate", "NetworkFailure", "NetworkFailure")]
    [InlineData("alternate", "SingleCandidate", "SingleCandidate")]
    public async Task Ollama의무효응답과실패는현재기준후보로돌아가고자유문장을사유에넣지않는다(
        string driver, string status, string expected)
    {
        var (states, spaces) = await OllamaCandidatesAsync();
        var ai = new OllamaPolicyPort((_, _) => Task.FromResult(new FoodDispatchAISelection(driver, status)));

        var result = await CreateOllamaPolicy(states, spaces, ai).다음후보선정Async(Queue());

        Assert.Equal("baseline", result?.DriverId);
        Assert.Contains($"FoodDispatchAI.{expected}", result!.추천사유);
        Assert.DoesNotContain("untrusted free text", result.추천사유);
        Assert.Equal(1, ai.Calls);
    }

    [Theory]
    [InlineData("receiving")]
    [InlineData("stale")]
    [InlineData("rejected")]
    [InlineData("radius")]
    public async Task Ollama응답중선택기사의관문이바뀌면현재적격후보만반환한다(string condition)
    {
        var (states, spaces) = await OllamaCandidatesAsync();
        var receiving = new TestDispatchAvailability();
        var rejected = new FakeRejectedRequestStore();
        var ai = new OllamaPolicyPort(async (_, _) =>
        {
            if (condition == "receiving")
                await receiving.기사의사변경Async("alternate", new() { 수신의사Code = 운영배차수신의사Code.Off });
            else if (condition == "rejected")
                await rejected.RejectAsync("alternate", Queue().의뢰Id);
            else
                states.Replace([
                    Snapshot("baseline", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, DateTime.UtcNow, 0),
                    Snapshot("alternate", 기사앱식별자.FoodDeliveryDriverApp,
                        condition == "radius" ? 37.53m : 37.502m, 127m,
                        condition == "stale" ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow, 999,
                        allowedRadiusKm: condition == "radius" ? 1m : null)]);
            return new("alternate", "Applied");
        });

        var result = await CreateOllamaPolicy(states, spaces, ai, receiving, rejected).다음후보선정Async(Queue());

        Assert.Equal("baseline", result?.DriverId);
        Assert.Contains("FoodDispatchAI.CandidateChanged", result!.추천사유);
        Assert.Equal(1, ai.Calls);
    }

    [Fact]
    public async Task Ollama오류뒤에도처음기준후보가아닌현재기준후보를반환한다()
    {
        var (states, spaces) = await OllamaCandidatesAsync();
        var receiving = new TestDispatchAvailability();
        var ai = new OllamaPolicyPort(async (_, _) =>
        {
            await receiving.기사의사변경Async("baseline", new() { 수신의사Code = 운영배차수신의사Code.Off });
            return new("baseline", "Timeout");
        });

        var result = await CreateOllamaPolicy(states, spaces, ai, receiving).다음후보선정Async(Queue());

        Assert.Equal("alternate", result?.DriverId);
        Assert.Contains("FoodDispatchAI.Timeout", result!.추천사유);
    }

    [Fact]
    public async Task Ollama응답중모든후보가부적격이되면처음선택을재사용하지않는다()
    {
        var (states, spaces) = await OllamaCandidatesAsync();
        var ai = new OllamaPolicyPort((_, _) =>
        {
            states.Replace([]);
            return Task.FromResult(new FoodDispatchAISelection("alternate", "Applied"));
        });

        Assert.Null(await CreateOllamaPolicy(states, spaces, ai).다음후보선정Async(Queue()));
        Assert.Equal(1, ai.Calls);
    }

    [Fact]
    public async Task Ollama예외는현재기준후보로돌아가지만재조회오류는숨기지않는다()
    {
        var (states, spaces) = await OllamaCandidatesAsync();
        var ai = new OllamaPolicyPort((_, _) => throw new InvalidOperationException("private failure"));
        var result = await CreateOllamaPolicy(states, spaces, ai).다음후보선정Async(Queue());
        Assert.Equal("baseline", result?.DriverId);
        Assert.Contains("FoodDispatchAI.Unavailable", result!.추천사유);
        Assert.DoesNotContain("private failure", result.추천사유);

        var receiving = new TestDispatchAvailability();
        var stale = new OllamaPolicyPort((_, _) =>
        {
            receiving.Unavailable = true;
            return Task.FromResult(new FoodDispatchAISelection("alternate", "Applied"));
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateOllamaPolicy(states, spaces, stale, receiving).다음후보선정Async(Queue()));
    }

    [Fact]
    public async Task 호출자취소는Ollama실패나기존기준후보로바꾸지않는다()
    {
        var (states, spaces) = await OllamaCandidatesAsync();
        using var cancelled = new CancellationTokenSource();
        var ai = new OllamaPolicyPort((_, ct) =>
        {
            cancelled.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new FoodDispatchAISelection("alternate", "Applied"));
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateOllamaPolicy(states, spaces, ai).다음후보선정Async(Queue(), cancellationToken: cancelled.Token));
        Assert.Equal(1, ai.Calls);
    }

    [Fact]
    public async Task 최신수요우선권이더낮은기사는Ollama가선택해도기준기사를넘지못한다()
    {
        var (states, spaces) = await OllamaCandidatesAsync();
        var demand = new DemandParticipationPort(DemandResult(
            DemandDriver("baseline", true, 1, true), DemandDriver("alternate", true, 2)));
        var ai = new OllamaPolicyPort((_, _) => Task.FromResult(new FoodDispatchAISelection("alternate", "Applied")));

        var result = await CreateOllamaPolicy(states, spaces, ai, demand: demand).다음후보선정Async(Queue());

        Assert.Equal("baseline", result?.DriverId);
        Assert.Contains("FoodDispatchAI.CandidateChanged", result!.추천사유);
    }

    [Theory]
    [InlineData("confirmed", false)]
    [InlineData("another-offer", false)]
    [InlineData("coordinates", false)]
    [InlineData("deleted", false)]
    [InlineData("confirmed", true)]
    public async Task 후보선정대기중원장이바뀌면새제안이나후보없음재탐색으로덮어쓰지않는다(
        string changed, bool noCandidates)
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"ollama-queue-change-{Guid.NewGuid():N}").Options;
        await using var db = new SsalddelContext(options, new ApplicationEncryption());
        await using var concurrent = new SsalddelContext(options, new ApplicationEncryption());
        var queue = Queue();
        queue.상태 = 상태값.배차대기상태.대기;
        queue.배차큐단계 = 상태값.배차큐단계.배차추천;
        queue.배차노출상태 = 상태값.배차노출상태.추천대기;
        db.운송원장.Add(queue);
        await db.SaveChangesAsync();
        var selection = new OllamaTransitionSelectionPort(async ct =>
        {
            var current = await concurrent.운송원장.SingleAsync(ct);
            if (changed == "deleted") concurrent.운송원장.Remove(current);
            else if (changed == "confirmed")
            {
                current.상태 = 상태값.배차대기상태.확정;
                current.배차큐단계 = 상태값.배차큐단계.확정;
                current.확정기사Id = "other-confirmed";
            }
            else if (changed == "another-offer")
            {
                current.배차노출상태 = 상태값.배차노출상태.추천중;
                current.현재추천대상기사Id = "other-offer";
                current.추천라운드 = 7;
                current.추천만료시각 = DateTime.UtcNow.AddMinutes(1);
            }
            else current.픽업_위도 = 38m;
            await concurrent.SaveChangesAsync(ct);
            return noCandidates ? 배차추천후보선정결과.적격후보없음("none")
                : 배차추천후보선정결과.선정됨(new("selected", 90m, "FoodDispatchAI.Applied"));
        });
        var notifications = new ApplicationNotificationPort();
        var ai = new OllamaPolicyPort((_, _) => throw new InvalidOperationException());
        var transition = new 배차대기원장전환Service(db, Options.Create(new 배차큐정책Options()), selection,
            notifications, null!, new 음식배달배차흐름Resolver(), new ApplicationPricingPort(),
            new ApplicationFoodRecordPort(), candidateAI: ai);

        var result = await transition.추천대기처리Async(queue.의뢰Id);

        Assert.False(result.전환여부);
        Assert.Equal("FoodDispatchAI.QueueChanged", result.메시지);
        Assert.Equal(0, notifications.Calls);
        Assert.Empty(await db.운송이벤트.ToListAsync());
        Assert.Empty(await db.운영배차활동사건.ToListAsync());
        db.ChangeTracker.Clear();
        var saved = await db.운송원장.SingleOrDefaultAsync();
        if (changed == "deleted") Assert.Null(saved);
        else
        {
            Assert.NotNull(saved);
            Assert.Null(saved!.기사제안요금계산근거Json);
            if (changed == "confirmed") Assert.Equal("other-confirmed", saved.확정기사Id);
            if (changed == "another-offer")
            {
                Assert.Equal("other-offer", saved.현재추천대상기사Id);
                Assert.Equal(7, saved.추천라운드);
            }
            if (changed == "coordinates") Assert.Equal(38m, saved.픽업_위도);
        }
    }

    [Fact]
    public async Task 수요기능OFF이어도Ollama_ON이면직접기사지정으로현재선정을우회할수없다()
    {
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"ollama-direct-{Guid.NewGuid():N}").Options, new ApplicationEncryption());
        var queue = Queue();
        queue.상태 = 상태값.배차대기상태.대기;
        queue.배차큐단계 = 상태값.배차큐단계.배차추천;
        queue.배차노출상태 = 상태값.배차노출상태.추천대기;
        db.운송원장.Add(queue);
        await db.SaveChangesAsync();
        var selection = new OllamaTransitionSelectionPort(_ => Task.FromResult(
            배차추천후보선정결과.선정됨(new 배차추천후보("allowed", 90m, "FoodDispatchAI.Applied"))));
        var notifications = new ApplicationNotificationPort();
        var transition = new 배차대기원장전환Service(db, Options.Create(new 배차큐정책Options()), selection,
            notifications, null!, new 음식배달배차흐름Resolver(), new ApplicationPricingPort(),
            new ApplicationFoodRecordPort(), candidateAI: new OllamaPolicyPort((_, _) => throw new InvalidOperationException()));

        var result = await transition.추천시작Async(queue.의뢰Id, "requested");

        Assert.False(result.전환여부);
        Assert.Equal(1, selection.Calls);
        Assert.Equal(0, notifications.Calls);
        Assert.Null(queue.현재추천대상기사Id);
        Assert.Empty(await db.운송이벤트.ToListAsync());
    }

    [Fact]
    public async Task DB의마이크로초저장정밀도차이만으로동일원장추천을보류하지않는다()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"ollama-queue-precision-{Guid.NewGuid():N}").Options;
        await using var db = new SsalddelContext(options, new ApplicationEncryption());
        await using var concurrent = new SsalddelContext(options, new ApplicationEncryption());
        var queue = Queue();
        queue.상태 = 상태값.배차대기상태.대기;
        queue.배차큐단계 = 상태값.배차큐단계.배차추천;
        queue.배차노출상태 = 상태값.배차노출상태.추천대기;
        queue.UpdatedAt = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc).AddTicks(7);
        queue.추천시작시각 = queue.UpdatedAt;
        queue.추천만료시각 = queue.UpdatedAt.AddMinutes(-1);
        db.운송원장.Add(queue);
        await db.SaveChangesAsync();
        var selection = new OllamaTransitionSelectionPort(async ct =>
        {
            var current = await concurrent.운송원장.SingleAsync(ct);
            current.UpdatedAt = current.UpdatedAt.AddTicks(-7);
            current.추천시작시각 = current.추천시작시각!.Value.AddTicks(-7);
            current.추천만료시각 = current.추천만료시각!.Value.AddTicks(-7);
            await concurrent.SaveChangesAsync(ct);
            return 배차추천후보선정결과.선정됨(new("selected", 90m, "FoodDispatchAI.Applied"));
        });
        var notifications = new ApplicationNotificationPort();
        var transition = new 배차대기원장전환Service(db, Options.Create(new 배차큐정책Options()), selection,
            notifications, null!, new 음식배달배차흐름Resolver(), new ApplicationPricingPort(),
            new ApplicationFoodRecordPort(), candidateAI: new OllamaPolicyPort((_, _) => throw new InvalidOperationException()));

        var result = await transition.추천대기처리Async(queue.의뢰Id);

        Assert.True(result.전환여부);
        Assert.Equal("selected", queue.현재추천대상기사Id);
        Assert.Equal(1, notifications.Calls);
    }

    private static async Task<(FakeDriverStateStore States, InMemory음식배달권실행공간Store Spaces)> OllamaCandidatesAsync()
    {
        var now = DateTime.UtcNow;
        return (new([
            Snapshot("baseline", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 0),
            Snapshot("alternate", 기사앱식별자.FoodDeliveryDriverApp, 37.502m, 127m, now, 0)]),
            await CreateFoodSpaceStoreAsync(("baseline", 37.5001m, 127m), ("alternate", 37.502m, 127m)));
    }

    private static 음식배달배차업무정책 CreateOllamaPolicy(
        I국내화물운송기사상태Store states,
        I음식배달권실행공간Store spaces,
        IFoodDispatchCandidateAI ai,
        I운영배차공통UseCase? receiving = null,
        IDriverRejectedRequestStore? rejected = null,
        I음식배달수요참여Service? demand = null)
        => new(states, rejected ?? new FakeRejectedRequestStore(), new StraightLineRouteService(), spaces,
            new FakeFoodOrderStore(), Options.Create(new 배차큐정책Options
            {
                음식배달동일권최소후보기사수 = 1, 음식배달후보검색반경Km = 5m, 음식배달후보최대조회수 = 20,
                음식배달평균이동속도KmH = 20m, 음식배달픽업조기도착허용분 = 3m, 음식배달픽업지연허용분 = 5m
            }), receiving ?? new TestDispatchAvailability(), 수요참여: demand, candidateAI: ai);

    private sealed class OllamaPolicyPort(
        Func<음식배달후보검토결과, CancellationToken, Task<FoodDispatchAISelection>> select,
        bool enabled = true) : IFoodDispatchCandidateAI
    {
        public bool Enabled => enabled;
        public int Calls { get; private set; }
        public Task<FoodDispatchAISelection> SelectAsync(음식배달후보검토결과 trace, CancellationToken cancellationToken = default)
        {
            Calls++;
            return select(trace, cancellationToken);
        }
    }

    private sealed class OllamaTransitionSelectionPort(
        Func<CancellationToken, Task<배차추천후보선정결과>> select) : I배차추천후보선정Service
    {
        public int Calls { get; private set; }
        public Task<배차추천후보선정결과> 다음후보선정Async(string requestId, string? 제외기사Id = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return select(cancellationToken);
        }
    }
}
