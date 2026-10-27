using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Food;

public sealed partial class 음식배달정상수직생명주기Tests
{
    [Fact]
    public async Task R76_유효자격과동일상태근거와명시검증계획은_계획과보수를함께저장하고알림을요청한다()
    {
        await using var f = await R76StateFixture.CreateAsync();
        var evidence = await f.Evidence();
        var notification = new R76Notification();
        var eligibility = new R76Eligibility { WarningRequired = true };
        var result = await R76Apply(f, notification, eligibility).추천잠금적용Async(R76Plan(f, evidence));
        Assert.Equal(1, result.잠금건수);
        Assert.Equal(1, notification.Calls);
        Assert.Empty(eligibility.Acknowledged);
        Assert.Equal(3m, eligibility.HandlingTimes[R76StateFixture.Id].상차분);
        Assert.Equal(2m, eligibility.HandlingTimes[R76StateFixture.Id].하차분);
        var queue = await f.Db.운송원장.AsNoTracking().SingleAsync();
        Assert.Equal(5000m, queue.기사지급예정액);
        Assert.Equal("driver", queue.현재추천대상기사Id);
        var saved = JsonSerializer.Deserialize<국내화물추천저장근거>(queue.추천판정근거Json!);
        Assert.Equal(evidence.입력지문, saved!.판정근거!.입력지문);
        Assert.Equal(saved.조율입력지문, saved.수행계획!.입력지문);
        Assert.Equal(3m, saved.수행계획.상하차작업시간[R76StateFixture.Id].상차분);
        Assert.Equal(2m, saved.수행계획.상하차작업시간[R76StateFixture.Id].하차분);
    }

    [Theory]
    [InlineData("cargo")]
    [InlineData("payout")]
    [InlineData("selection")]
    [InlineData("location")]
    [InlineData("receiving")]
    [InlineData("actual")]
    [InlineData("qualification")]
    public async Task R76_추천저장직전상태가바뀌면_추천판본과보수와알림을기록하지않는다(string change)
    {
        await using var f = await R76StateFixture.CreateAsync();
        var proposal = R76Plan(f, await f.Evidence());
        await f.Change(change);
        var notification = new R76Notification();
        var result = await R76Apply(f, notification, new R76Eligibility()).추천잠금적용Async(proposal);
        Assert.Equal(0, result.잠금건수);
        Assert.Equal(0, notification.Calls);
        var queue = await f.Db.운송원장.AsNoTracking().SingleAsync();
        Assert.Null(queue.현재추천대상기사Id);
        Assert.Null(queue.추천판정근거Json);
        Assert.Null(queue.기사지급예정액);
        Assert.Equal(0, queue.추천라운드);
    }

    [Fact]
    public async Task R76_실경로검사중수신을끄면_검사후재확인에서추천과알림을보류한다()
    {
        await using var f = await R76StateFixture.CreateAsync();
        var proposal = R76Plan(f, await f.Evidence());
        var notification = new R76Notification();
        var eligibility = new R76Eligibility { DuringCheck = () => f.Availability.On = false };
        var result = await R76Apply(f, notification, eligibility).추천잠금적용Async(proposal);
        Assert.Equal(0, result.잠금건수);
        Assert.Equal(0, notification.Calls);
        Assert.Null((await f.Db.운송원장.AsNoTracking().SingleAsync()).추천판정근거Json);
    }

    [Fact]
    public async Task R76_계획의입력지문이달라지면_현재상태가같아도추천을저장하지않는다()
    {
        await using var f = await R76StateFixture.CreateAsync();
        var result = R76Plan(f, await f.Evidence());
        result = result with { 검증계획목록 = [result.검증계획목록[0] with { 입력지문 = "different-review-input" }] };
        var notification = new R76Notification();
        Assert.Equal(0, (await R76Apply(f, notification, new R76Eligibility()).추천잠금적용Async(result)).잠금건수);
        Assert.Equal(0, notification.Calls);
    }

    [Fact]
    public async Task R76_추천저장시남은시간이줄면_현재이동과명시작업시간을합쳐약속을재검사한다()
    {
        await using var f = await R76StateFixture.CreateAsync();
        var proposal = R76Plan(f, await f.Evidence());
        var notification = new R76Notification();
        // 명시적 대역: 이전 계획은 20분 여유, 저장 직전에는 14분만 남은 상황이다.
        // 이동 10분만으로는 가능하지만 상차 3분·하차 2분까지 하면 불가능하다.
        var eligibility = new R76Eligibility { AvailableMinutes = 14m, TravelMinutes = 10m };
        var result = await R76Apply(f, notification, eligibility).추천잠금적용Async(proposal);
        Assert.Equal(0, result.잠금건수);
        Assert.Equal(0, notification.Calls);
        Assert.Equal(3m, eligibility.HandlingTimes[R76StateFixture.Id].상차분);
        Assert.Null((await f.Db.운송원장.AsNoTracking().SingleAsync()).추천판정근거Json);
    }

    [Fact]
    public async Task R76_실제추천기록으로대기순위가초기화되어도_저장한안전근거와수락가능라운드는유지한다()
    {
        await using var f = await R76StateFixture.CreateAsync();
        f.States.Current = f.States.Current with { Aging기준시각Utc = f.Now.AddMinutes(-40), Aging점수 = 40m };
        var evidence = await f.Evidence();
        var notification = new R76Notification();
        var result = await R76Apply(f, notification, new R76Eligibility(), new 국내화물운송기사상태Service(f.States))
            .추천잠금적용Async(R76Plan(f, evidence));
        Assert.Equal(1, result.잠금건수);
        Assert.Equal(f.Now, f.States.Current.Aging기준시각Utc);
        Assert.Equal(0m, f.States.Current.Aging점수);
        var current = await f.Evidence();
        Assert.Equal(evidence.입력지문, current.입력지문);
        Assert.NotEqual(evidence.순위관측지문, current.순위관측지문);
        var queue = await f.Db.운송원장.AsNoTracking().SingleAsync();
        var saved = JsonSerializer.Deserialize<국내화물추천저장근거>(queue.추천판정근거Json!)!;
        Assert.Equal(f.Now.AddMinutes(-40), saved.판정근거!.대기순위기준시각Utc);
        Assert.Equal(40m, saved.판정근거.대기순위점수);
        // 수락 Handler가 사용하는 현재 근거와 추천 라운드 관문을 같은 저장본으로 대조한다.
        Assert.True(await f.Snapshots.현재근거확인Async(saved.판정근거));
        Assert.True(Ssalddel.Application.Driver.DispatchAction.배차응답가능정책.추천수락가능(queue, "driver", f.Now));
    }

    private static 국내화물배차조율적용Service R76Apply(R76StateFixture f, R76Notification notification, R76Eligibility eligibility,
        I국내화물운송기사상태Service? driverState = null)
        => new(f.Db, Options.Create(new 배차큐정책Options()), notification, driverState ?? new QualificationDriverState(),
            new InMemory국내화물배달권실행공간Store(), NullLogger<국내화물배차조율적용Service>.Instance,
            신규업무자격: f.Qualification, 상태근거: f.Snapshots, 수신: f.Availability, 적격성: eligibility, clock: new R76Clock(f.Now));

    private static 국내화물배차조율결과 R76Plan(R76StateFixture f, 국내화물배차판정근거 evidence)
    {
        var plan = new 국내화물검증수행계획
        {
            계획Id = "explicit-fixture-plan-" + evidence.입력지문, 기사Id = "driver", 의뢰Ids = [R76StateFixture.Id],
            확인됨 = true, 가능여부 = true, 입력지문 = evidence.입력지문,
            상하차작업시간 = new Dictionary<string, 화물상하차작업시간> { [R76StateFixture.Id] = new(3m, 2m) }
        };
        return new(f.Now,
            [new 국내화물배차제안(1, R76StateFixture.Id, "driver", 1, 100, 1000, 6000, 5000, "명시적 격리 시험", [])
                { 판정근거 = evidence, 수행계획Id = plan.계획Id }], [], [], 1000, 6000, 5000)
            { 판정근거 = evidence, 검증계획목록 = [plan] };
    }

    private sealed class R76Notification : I배차추천알림Service
    {
        public int Calls { get; private set; }
        public Task 추천알림요청생성Async(long id, string request, string driver, int round, CancellationToken ct = default)
        { Calls++; return Task.CompletedTask; }
        public Task<int> 대기알림발송Async(int take = 100, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class R76Eligibility : I화물배차수락적격성Service
    {
        public bool WarningRequired { get; init; }
        public Action? DuringCheck { get; init; }
        public decimal AvailableMinutes { get; init; } = 100m;
        public decimal TravelMinutes { get; init; } = 10m;
        public IReadOnlyCollection<string> Acknowledged { get; private set; } = [];
        public IReadOnlyDictionary<string, 화물상하차작업시간> HandlingTimes { get; private set; } = new Dictionary<string, 화물상하차작업시간>();
        public Task<화물배차수락적격성결과> 평가Async(string id, 화주운송의뢰 request, IReadOnlyCollection<string> codes, CancellationToken ct = default)
            => throw new NotSupportedException("신규 추천은 계획의 명시 작업시간을 전달해야 합니다.");
        public Task<화물배차수락적격성결과> 평가Async(string id, 화주운송의뢰 request, IReadOnlyCollection<string> codes,
            IReadOnlyDictionary<string, 화물상하차작업시간> handling, CancellationToken ct)
        {
            Acknowledged = codes; HandlingTimes = handling; DuringCheck?.Invoke();
            if (!handling.TryGetValue(request.의뢰Id, out var times) || !times.확인됨
                || TravelMinutes + times.상차분!.Value + times.하차분!.Value > AvailableMinutes)
                return Task.FromResult(new 화물배차수락적격성결과(false,
                    화물배차수락오류코드.일정실패, "현재 이동·작업시간으로 약속을 지킬 수 없습니다.", [], [], [], null, null));
            return Task.FromResult(new 화물배차수락적격성결과(!WarningRequired,
                WarningRequired ? 화물배차수락오류코드.경고확인필요 : null, null, [], [], [], 10m, 0m));
        }
    }
}
