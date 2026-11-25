using MediatR;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Driver.Transport;
using 살뜰.Services.Dispatch.Continuity;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Driver.Transport;

public sealed class CargoOrderedMapProjectionR21Tests
{
    private static readonly DateTime Start = new(2026, 10, 27, 4, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task 여러운송의교차정차를_수락시확인한순서와현재원장Id로투영한다(int jobCount)
    {
        var route = new List<(long Id, string Stage)> { (2, "pickup") };
        if (jobCount >= 3) route.Add((3, "pickup"));
        if (jobCount >= 4) route.Add((4, "pickup"));
        route.Add((2, "dropoff"));
        route.Add((1, "dropoff"));
        if (jobCount >= 4) route.Add((4, "dropoff"));
        if (jobCount >= 3) route.Add((3, "dropoff"));
        var stops = route.Select((row, order) => Stop(row.Id, row.Stage, order)).ToArray();
        var plan = Plan(stops.Reverse().ToArray());
        var summaries = Enumerable.Range(1, jobCount).Select(id =>
        {
            var item = Summary(id, id == 1 ? "상차완료" : "확정");
            item.상차시간창시작일시 = stops.SingleOrDefault(x => x.운송Id == id && x.단계유형 == "pickup")?.기준시각;
            item.하차시간창시작일시 = stops.Single(x => x.운송Id == id && x.단계유형 == "dropoff").기준시각;
            return item;
        }).ToArray();
        var result = await Workspace(plan, summaries);

        Assert.True(result.수락정차순서반영됨);
        Assert.Null(result.수락정차순서보류Code);
        Assert.Equal(기사화물운송작업공간응답.CurrentRuleRevision, result.RuleRevision);
        Assert.Equal(2, result.다음행동운송!.Id);
        Assert.Equal("상차지로 이동", result.다음행동);
        Assert.Equal(route.Select(x => (long?)x.Id), result.권장경로정차목록.Select(x => x.운송Id));
        Assert.Equal(route.Select(x => x.Stage == "pickup" ? "상차" : "하차"),
            result.권장경로정차목록.Select(x => x.단계));
        Assert.Equal(Enumerable.Range(0, route.Count), result.권장경로정차목록.Select(x => x.순서));
        for (var index = 0; index < stops.Length; index++)
        {
            var actual = result.권장경로정차목록[index];
            Assert.Equal(stops[index].기준시각, actual.시간창시작일시);
            Assert.Equal(stops[index].시간창종료일시, actual.시간창종료일시);
            Assert.Equal(stops[index].좌표!.Latitude, actual.위도);
            Assert.Equal(stops[index].좌표!.Longitude, actual.경도);
            Assert.True(actual.좌표근거있음);
        }
        Assert.Empty(result.BlockCodes);
    }

    [Fact]
    public async Task 공개보류정차는_ID는구분하되주소와좌표를가리고다른허용정차는유지한다()
    {
        var b = Summary(2, "확정");
        b.개인정보제공보류 = true;
        var stops = new[] { Stop(2, "pickup", 0) with { 주소 = "B의 비공개 상차 주소" },
            Stop(2, "dropoff", 1) with { 주소 = "B의 비공개 하차 주소" }, Stop(1, "dropoff", 2) };
        var result = await Workspace(Plan(stops), Summary(1, "상차완료"), b);

        Assert.All(result.권장경로정차목록.Take(2), row =>
        {
            Assert.Equal(2, row.운송Id);
            Assert.Null(row.위도); Assert.Null(row.경도); Assert.False(row.좌표근거있음);
            Assert.DoesNotContain("비공개", row.주소);
        });
        Assert.NotNull(result.권장경로정차목록[2].위도);
        Assert.NotNull(result.권장경로정차목록[2].경도);
        Assert.Contains("FreightScheduleEvidenceMissing", result.BlockCodes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(999L)]
    public async Task 무ID나현재목록밖정차는_표시문자열이같아도지도좌표를제공하지않는다(long? transportId)
    {
        var result = await Workspace(Plan(Stop(2, "pickup", 0) with { 운송Id = transportId }), Summary(2, "확정"));
        var row = Assert.Single(result.권장경로정차목록);
        Assert.Null(row.운송Id);
        Assert.Null(row.위도); Assert.Null(row.경도); Assert.False(row.좌표근거있음);
        Assert.False(result.수락정차순서반영됨);
        Assert.Equal("FreightAcceptedStopOrder.NextActionMismatch", result.수락정차순서보류Code);
    }

    [Fact]
    public async Task 현재목록에같은원장ID가중복되면_한쪽의허용응답으로좌표를공개하지않는다()
    {
        var allowed = Summary(2, "확정");
        var held = Summary(2, "확정"); held.개인정보제공보류 = true;
        var result = await Workspace(Plan(Stop(2, "pickup", 0)), allowed, held);
        var row = Assert.Single(result.권장경로정차목록);
        Assert.Null(row.운송Id);
        Assert.Null(row.위도); Assert.Null(row.경도); Assert.False(row.좌표근거있음);
        Assert.False(result.수락정차순서반영됨);
        Assert.Equal("FreightAcceptedStopOrder.NextActionMismatch", result.수락정차순서보류Code);
    }

    [Theory]
    [InlineData(91, 127)]
    [InlineData(-91, 127)]
    [InlineData(37, 181)]
    [InlineData(37, -181)]
    [InlineData(0, 0)]
    [InlineData(0, 127)]
    [InlineData(37, 0)]
    public async Task 범위밖이나영점누락좌표는_한쪽만유효해도좌표쌍과근거를함께생략한다(int latitude, int longitude)
    {
        var result = await Workspace(Plan(Stop(2, "pickup", 0) with { 좌표 = new(latitude, longitude) }), Summary(2, "확정"));
        var row = Assert.Single(result.권장경로정차목록);
        Assert.Null(row.위도); Assert.Null(row.경도); Assert.False(row.좌표근거있음);
        Assert.Contains("FreightScheduleEvidenceMissing", result.BlockCodes);
    }

    [Theory]
    [InlineData(90, 180)]
    [InlineData(-90, -180)]
    public async Task 허용된유효좌표쌍은_영점을제외한범위경계를포함해원문대로제공한다(int latitude, int longitude)
    {
        var result = await Workspace(Plan(Stop(2, "pickup", 0) with { 좌표 = new(latitude, longitude) }), Summary(2, "확정"));
        var row = Assert.Single(result.권장경로정차목록);
        Assert.Equal((decimal)latitude, row.위도); Assert.Equal((decimal)longitude, row.경도);
        Assert.True(row.좌표근거있음);
    }

    [Fact]
    public async Task 좌표가없는정차를_앞뒤주소나운송번호로추정하지않는다()
    {
        var result = await Workspace(Plan(Stop(2, "pickup", 0) with { 좌표 = null }), Summary(2, "확정"));
        var row = Assert.Single(result.권장경로정차목록);
        Assert.Equal(2, row.운송Id);
        Assert.Null(row.위도); Assert.Null(row.경도); Assert.False(row.좌표근거있음);
    }

    [Theory]
    [InlineData("SourceChanged")]
    [InlineData("OwnershipChanged")]
    [InlineData("ActiveWorkChanged")]
    public async Task 보류순서는_원본flag가true여도반영됨으로내보내지않고기존다음행동보류를유지한다(string reason)
    {
        var code = "FreightAcceptedStopOrder." + reason;
        var plan = Plan(Stop(2, "pickup", 0), Stop(1, "dropoff", 1)) with { 수락정차순서보류Code = code };
        var a = Summary(1, "상차완료"); a.하차시간창시작일시 = Start;
        var b = Summary(2, "확정"); b.상차시간창시작일시 = Start;
        var result = await Workspace(plan, a, b);
        Assert.False(result.수락정차순서반영됨);
        Assert.Equal(code, result.수락정차순서보류Code);
        Assert.Equal(1, result.다음행동운송!.Id);
        Assert.Contains(code, result.BlockCodes);
        Assert.Contains("다시 확인", result.다음행동);
        Assert.All(result.권장경로정차목록, row => Assert.Null(row.시간창시작일시));
    }

    [Fact]
    public async Task 구판일정은_주소와기존다음행동을보존하고수락순서확인으로승격하지않는다()
    {
        var a = Summary(1, "상차완료"); a.하차시간창시작일시 = Start;
        var b = Summary(2, "확정"); b.상차시간창시작일시 = Start;
        var result = await Workspace(Plan(Stop(2, "pickup", 0), Stop(1, "dropoff", 1)) with { 수락정차순서반영됨 = false },
            a, b);
        Assert.False(result.수락정차순서반영됨);
        Assert.Null(result.수락정차순서보류Code);
        Assert.Equal(1, result.다음행동운송!.Id);
        Assert.Empty(result.BlockCodes);
        Assert.All(result.권장경로정차목록, row => Assert.NotEmpty(row.주소));
        Assert.All(result.권장경로정차목록, row => Assert.Null(row.시간창시작일시));
    }

    [Theory]
    [InlineData("pickup", "확정")]
    [InlineData("dropoff", "운송중")]
    public async Task 수락순서가유효해도_원요청시작이없으면원장출발도착fallback을시간창시작으로보내지않는다(string stage, string status)
    {
        var item = Summary(2, status);
        item.출발_픽업 = Start;
        item.도착 = Start;
        var result = await Workspace(Plan(Stop(2, stage, 0)), item);
        Assert.True(result.수락정차순서반영됨);
        Assert.Null(Assert.Single(result.권장경로정차목록).시간창시작일시);
    }

    [Fact]
    public async Task 확인된상하차시간창시작은_일정기준시각대신원요청투영을사용한다()
    {
        var item = Summary(2, "확정");
        item.상차시간창시작일시 = Start.AddMinutes(1);
        item.하차시간창시작일시 = Start.AddMinutes(21);
        var result = await Workspace(Plan(Stop(2, "pickup", 0), Stop(2, "dropoff", 1)), item);
        Assert.True(result.수락정차순서반영됨);
        Assert.Equal(item.상차시간창시작일시, result.권장경로정차목록[0].시간창시작일시);
        Assert.Equal(item.하차시간창시작일시, result.권장경로정차목록[1].시간창시작일시);
    }

    [Theory]
    [InlineData("pickup", "상차완료")]
    [InlineData("unknown", "확정")]
    public async Task 첫정차단계와현재상태가맞지않으면_지도용순서도확인됨으로표시하지않는다(string stage, string status)
    {
        var result = await Workspace(Plan(Stop(2, stage, 0)), Summary(2, status));
        Assert.False(result.수락정차순서반영됨);
        Assert.Equal("FreightAcceptedStopOrder.NextActionMismatch", result.수락정차순서보류Code);
        if (stage == "unknown")
        {
            Assert.Null(result.권장경로정차목록[0].위도);
            Assert.Null(result.권장경로정차목록[0].경도);
        }
    }

    [Fact]
    public async Task 완료된B를제외한정차에서는_남은A가새첫정차가된다()
    {
        var result = await Workspace(Plan(Stop(1, "dropoff", 0)), Summary(1, "상차완료"), Summary(2, "인수완료"));
        Assert.Single(result.활성운송목록);
        Assert.Equal(1, result.다음행동운송!.Id);
        Assert.Equal(1, Assert.Single(result.권장경로정차목록).운송Id);
        Assert.True(result.수락정차순서반영됨);
    }

    private static 기사운송일정계획 Plan(params 기사운송일정항목[] stops)
        => new("driver-1", Start, null, stops) { 수락정차순서반영됨 = true };

    private static 기사운송일정항목 Stop(long id, string stage, int order)
        => new($"request-{id}", stage, $"합성 장소 {id}", new(37.5m + id / 100m, 127m + order / 100m),
            Start.AddMinutes(order * 10), Start.AddHours(3), order, id, true, false) { 작업시간분 = 2m };

    private static 기사운송요약응답 Summary(long id, string status)
        => new() { Id = id, 운송번호 = $"별도표시번호-{id}", 기사_운송자 = "driver-1", 상태 = status };

    private static async Task<기사화물운송작업공간응답> Workspace(기사운송일정계획 plan, params 기사운송요약응답[] items)
        => await new 화물운송작업공간조회QueryHandler(new Sender(items), new Schedule(plan), new Continuity())
            .Handle(new("driver-1"), default);

    private sealed class Schedule(기사운송일정계획 plan) : I기사운송일정구성Service
    {
        public Task<기사운송일정계획> 구성Async(string 기사Id, 배차경로좌표? 시작좌표, CancellationToken cancellationToken = default)
            => Task.FromResult(plan);
    }

    private sealed class Sender(IReadOnlyList<기사운송요약응답> items) : ISender
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => Task.FromResult((TResponse)(object)items);
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Continuity : I화물연속배차UseCase
    {
        public Task<화물연속배차상태Dto> 조회Async(string id, CancellationToken ct = default) => Task.FromResult(new 화물연속배차상태Dto());
        public Task<IReadOnlyList<화물운송시간약속Dto>> 시간약속목록Async(string id, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<화물운송시간약속Dto>>([]);
        public Task<화물경로위험Dto> 현재위험조회Async(string id, CancellationToken ct = default) => Task.FromResult(new 화물경로위험Dto());
        public Task<화물연속배차상태Dto> 의사변경Async(string id, 화물연속배차의사변경요청 request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<화물다음콜예약Dto?> 추천탐색Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<화물예약검증결과> 수락예약검증Async(string id, string requestId, string? reservationId, long? revision, CancellationToken ct = default) => throw new NotSupportedException();
        public Task 수락완료Async(string id, string requestId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task 시간약속잠금Async(string id, 화주운송의뢰 request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task 완료기록Async(string id, DateTime at, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
