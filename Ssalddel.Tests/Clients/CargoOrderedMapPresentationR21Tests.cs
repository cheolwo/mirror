using DriverApp.Models.Driver.Samples;
using DriverApp.Services;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Transport;
using System.Text.Json;

namespace Ssalddel.Tests.Clients;

public sealed class CargoOrderedMapPresentationR21Tests
{
    private readonly DriverHomeRoutePlanningService _maps = new();

    [Fact]
    public void B상차하차뒤A하차는_수락순서나표시번호대신_서버정차순서와첫지점만강조한다()
    {
        var workspace = TwoJobs();
        var result = Build(workspace);
        Assert.Equal(["request-2", "request-2", "request-1"], result.Markers.Select(marker => marker.RequestId));
        Assert.Equal([1, 2, 3], result.Markers.Select(marker => marker.StopOrder!.Value));
        Assert.Equal([true, false, false], result.Markers.Select(marker => marker.IsNextStop));
        Assert.Equal("1 · 상차", result.Markers[0].PickupLabel);
        Assert.StartsWith("1. invoice-2 상차 · 마감 ", result.StopSummary.Split('\n')[0]);
        Assert.StartsWith("2. invoice-2 하차 · 마감 ", result.StopSummary.Split('\n')[1]);
        Assert.StartsWith("3. invoice-1 하차 · 마감 ", result.StopSummary.Split('\n')[2]);
        Assert.Equal("수락한 방문 순서입니다.", result.Notice);
        var pins = DriverRecommendationMarkerPresentationPolicy.Build(result.Markers);
        Assert.All(pins.Take(2), pin => Assert.Equal(DriverRecommendationMarkerPresentationPolicy.InactiveTintColor, pin.TintColor));
        Assert.Equal(DriverRecommendationMarkerPresentationPolicy.PickupTintColor, pins[^1].TintColor);
        Assert.Equal("request-2", pins[^1].Source.RequestId);
        Assert.True(pins[^1].IsPickup);
        Assert.Equal(1, pins[^1].ZIndex);
        Assert.All(result.RouteOverlays.Take(2), route => Assert.Equal("#9ca3af", route.StrokeColor));
        Assert.Equal("#f97316", result.RouteOverlays[^1].StrokeColor);
        Assert.Equal("방문 지점 연결", result.RouteOverlays[^1].Caption);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void 여러운송의교차정차는_서버가정한상대순서를보존하고_임의로묶지않는다(int jobCount)
    {
        var summaries = Enumerable.Range(1, jobCount).Select(index => Summary(index, "확정")).ToArray();
        var stops = new List<기사화물경로정차응답>();
        foreach (var id in Enumerable.Range(1, jobCount).Reverse()) stops.Add(Stop(id, "상차", stops.Count));
        foreach (var id in Enumerable.Range(1, jobCount)) stops.Add(Stop(id, "하차", stops.Count));
        var workspace = Workspace(summaries, stops.ToArray(), jobCount);
        var result = Build(workspace);
        Assert.Equal(jobCount * 2, result.Markers.Count);
        Assert.Equal(stops.Select(stop => stop.의뢰Id), result.Markers.Select(marker => marker.RequestId));
        Assert.Equal(jobCount * 2, result.StopSummary.Split('\n').Length);
        Assert.Single(result.Markers.Where(marker => marker.IsNextStop));
        var pins = DriverRecommendationMarkerPresentationPolicy.Build(result.Markers);
        Assert.Equal(jobCount * 2 - 1, pins.Count(pin => pin.IsMuted));
        Assert.Equal(DriverRecommendationMarkerPresentationPolicy.PickupTintColor, pins[^1].TintColor);
    }

    [Fact]
    public void 상차완료재조회는_완료상차를제거하고_다음하차만파랑으로바꾼다()
    {
        var workspace = TwoJobs();
        workspace.활성운송목록.Single(item => item.Id == 2).상태 = "상차완료";
        workspace.권장경로정차목록 = workspace.권장경로정차목록.Skip(1).ToArray();
        var result = Build(workspace);
        Assert.Equal(2, result.Markers.Count);
        Assert.Equal("1 · 하차", result.Markers[0].DropoffLabel);
        Assert.Equal(0, result.Markers[0].PickupLatitude);
        var pins = DriverRecommendationMarkerPresentationPolicy.Build(result.Markers);
        Assert.False(pins[^1].IsPickup);
        Assert.Equal(DriverRecommendationMarkerPresentationPolicy.DropoffTintColor, pins[^1].TintColor);
        Assert.Equal("#2563eb", result.RouteOverlays[^1].StrokeColor);
    }

    [Fact]
    public void B완료후재조회는_B를다시그리지않고_A하차를첫순서로한다()
    {
        var workspace = TwoJobs();
        workspace.활성운송목록 = workspace.활성운송목록.Where(item => item.Id == 1).ToArray();
        workspace.다음행동운송 = workspace.활성운송목록[0];
        workspace.권장경로정차목록 = workspace.권장경로정차목록.Skip(2).ToArray();
        var result = Build(workspace);
        var marker = Assert.Single(result.Markers);
        Assert.Equal("request-1", marker.RequestId);
        Assert.Equal(1, marker.StopOrder);
        Assert.True(marker.IsNextStop);
        Assert.DoesNotContain("invoice-2", result.StopSummary);
    }

    [Fact]
    public void 좌표공백은_앞뒤지점을건너이어그리지않고_남은순번을유지한다()
    {
        var workspace = TwoJobs();
        workspace.권장경로정차목록[1].위도 = null;
        workspace.권장경로정차목록[1].경도 = null;
        workspace.권장경로정차목록[1].좌표근거있음 = false;
        var result = Build(workspace);
        Assert.Equal([1, 3], result.Markers.Select(marker => marker.StopOrder!.Value));
        Assert.Single(result.RouteOverlays);
        Assert.Contains("일부 위치", result.Notice);
        Assert.True(result.HasCoordinateGap);
        Assert.DoesNotContain(result.RouteOverlays.SelectMany(route => route.Points), point => point.Label == "3 · 하차");
    }

    [Fact]
    public void 공개보류는_응답에좌표가잘못남아도_주소와핀을표시하지않는다()
    {
        var workspace = TwoJobs();
        workspace.활성운송목록.Single(item => item.Id == 1).개인정보제공보류 = true;
        workspace.권장경로정차목록[2].주소 = "공개하면 안 되는 고객 주소";
        var result = Build(workspace);
        Assert.DoesNotContain(result.Markers, marker => marker.RequestId == "request-1");
        Assert.DoesNotContain("공개하면 안 되는", JsonSerializer.Serialize(result));
        Assert.Contains("일부 위치", result.Notice);
    }

    [Fact]
    public void 첫정차위치가비공개이면_다음위치를앞당겨강조하지않는다()
    {
        var workspace = TwoJobs();
        workspace.활성운송목록.Single(item => item.Id == 2).개인정보제공보류 = true;
        var result = Build(workspace);
        Assert.Single(result.Markers);
        Assert.False(result.Markers[0].IsNextStop);
        Assert.Equal(3, result.Markers[0].StopOrder);
        Assert.All(DriverRecommendationMarkerPresentationPolicy.Build(result.Markers), pin => Assert.True(pin.IsMuted));
        Assert.Empty(result.RouteOverlays);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void 잘못된연결이나순서는_전체경로로승격하지않는다(int broken)
    {
        var workspace = TwoJobs();
        switch (broken)
        {
            case 0: workspace.권장경로정차목록[1].운송Id = 999; break;
            case 1: workspace.권장경로정차목록[1].순서 = workspace.권장경로정차목록[0].순서; break;
            case 2: workspace.권장경로정차목록[1].단계 = "상차"; break;
            case 3: workspace.활성운송목록 = [workspace.활성운송목록[0], workspace.활성운송목록[1], workspace.활성운송목록[1]]; break;
            case 4: workspace.다음행동운송 = workspace.활성운송목록[0]; break;
            case 5: workspace.권장경로정차목록[0].운송Id = null; break;
            case 6: workspace.활성운송목록[1].상태 = "인수완료"; break;
            case 7: workspace.권장경로정차목록[0].의뢰Id = string.Empty; break;
            case 8: workspace.권장경로정차목록[0].단계 = "하차"; workspace.권장경로정차목록[1].단계 = "상차"; break;
        }
        var result = Build(workspace);
        Assert.Empty(result.StopSummary);
        Assert.DoesNotContain(result.Markers, marker => marker.StopOrder.HasValue);
        Assert.Contains("다시 확인", result.Notice);
    }

    [Fact]
    public void 현재운송과첫정차가다르면_표시용운송번호가같아도_순서를추정하지않는다()
    {
        var workspace = TwoJobs();
        var current = Current(workspace.활성운송목록[0]) with { 의뢰Id = workspace.다음행동운송!.운송번호 };
        var result = _maps.BuildWorkspaceRouteState(Location(), current, workspace, true);
        Assert.Empty(result.StopSummary);
        Assert.DoesNotContain(result.Markers, marker => marker.StopOrder.HasValue);
    }

    [Fact]
    public void 작업공간없는기존소비자는_현재업무를보존하고_수락순서를만들지않는다()
    {
        var current = Current(Summary(1, "상차완료"));
        var result = _maps.BuildWorkspaceRouteState(Location(), current, null, true);
        Assert.Single(result.Markers);
        Assert.Null(result.Markers[0].StopOrder);
        Assert.Empty(result.StopSummary);
        Assert.Contains("다시 확인", result.Notice);
        Assert.Contains("현재 업무의 상하차 지점", result.Notice);
        Assert.Empty(_maps.BuildWorkspaceRouteState(Location(), null, TwoJobs(), true).Markers);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, "FreightAcceptedStopOrder.SourceChanged")]
    [InlineData(true, "FreightAcceptedStopOrder.NextActionMismatch")]
    [InlineData(false, "FreightAcceptedStopOrder.SourceChanged")]
    [InlineData(false, "FreightAcceptedStopOrder.NextActionMismatch")]
    public void 구판이나보류는_기존현재업무만남기고_새전체순서를강조하지않는다(bool applied, string? code)
    {
        var workspace = TwoJobs();
        workspace.수락정차순서반영됨 = applied;
        workspace.수락정차순서보류Code = code;
        var result = Build(workspace);
        Assert.Empty(result.StopSummary);
        if (code is null)
        {
            Assert.Single(result.Markers);
            Assert.Null(result.Markers[0].StopOrder);
            Assert.Contains("현재 업무의 상하차 지점", result.Notice);
        }
        else { Assert.Empty(result.Markers); Assert.Empty(result.RouteOverlays); }
    }

    [Fact]
    public void 반영됨이거짓이어도_차단코드가있으면_구판으로오인해현재강조를복원하지않는다()
    {
        var workspace = TwoJobs();
        workspace.수락정차순서반영됨 = false;
        workspace.BlockCodes = ["FreightAcceptedStopOrder.SourceChanged"];
        var result = Build(workspace);
        Assert.Empty(result.Markers);
        Assert.Empty(result.RouteOverlays);
        Assert.Empty(result.StopSummary);
    }

    [Fact]
    public void 조회실패나계정전환의이전자료는_강조핀과경로를모두비운다()
    {
        var workspace = TwoJobs();
        var result = _maps.BuildWorkspaceRouteState(Location(), Current(workspace.다음행동운송!), workspace, false);
        Assert.Empty(result.Markers);
        Assert.Empty(result.RouteOverlays);
        Assert.Empty(result.StopSummary);
        Assert.Contains("다시 조회", result.Notice);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void 현재나뒤운송의진행이보류되면_운행안내로강조하지않고_기존안내를보존한다(long heldId)
    {
        var workspace = TwoJobs();
        var held = workspace.활성운송목록.Single(item => item.Id == heldId);
        held.운송진행보류 = true;
        held.다음행동안내 = "현장 확인 후 진행해 주세요.";
        var result = Build(workspace);
        Assert.Empty(result.Markers);
        Assert.Empty(result.RouteOverlays);
        Assert.Empty(result.StopSummary);
        Assert.Equal(held.다음행동안내, result.Notice);
    }

    [Fact]
    public void 마감은_서버확인시각을한국시간으로표시하고_근거없으면미확인으로남긴다()
    {
        var workspace = TwoJobs();
        workspace.권장경로정차목록[0].시간창종료일시 = new DateTime(2026, 10, 27, 11, 30, 0, DateTimeKind.Unspecified);
        workspace.권장경로정차목록[1].시간창근거있음 = false;
        var result = Build(workspace);
        Assert.Contains("마감 10.27 20:30 (한국시간)", result.StopSummary.Split('\n')[0]);
        Assert.Contains("마감 미확인", result.StopSummary.Split('\n')[1]);
    }

    [Theory]
    [InlineData(0, 127)]
    [InlineData(91, 127)]
    [InlineData(37, 181)]
    public void 잘못된좌표는_지도중심이나다른정차로대체하지않는다(decimal latitude, decimal longitude)
    {
        var workspace = TwoJobs();
        workspace.권장경로정차목록[0].위도 = latitude;
        workspace.권장경로정차목록[0].경도 = longitude;
        var result = Build(workspace);
        Assert.DoesNotContain(result.Markers, marker => marker.IsNextStop);
        Assert.Equal([2, 3], result.Markers.Select(marker => marker.StopOrder!.Value));
        Assert.Single(result.RouteOverlays);
        Assert.Equal("#9ca3af", result.RouteOverlays[0].StrokeColor);
    }

    [Fact]
    public void 지연된현재위치는_출발선에서제외하되_확인된정차들은보존한다()
    {
        var workspace = TwoJobs();
        var stale = Location() with { 관측시각Utc = DateTime.UtcNow.AddHours(-1) };
        var result = _maps.BuildWorkspaceRouteState(stale, Current(workspace.다음행동운송!), workspace, true);
        Assert.Equal(3, result.Markers.Count);
        Assert.Equal(2, result.RouteOverlays.Count);
        Assert.DoesNotContain(result.RouteOverlays.SelectMany(route => route.Points), point => point.Label == "최근 확인 위치");
        Assert.All(result.RouteOverlays, route => Assert.Equal("#9ca3af", route.StrokeColor));
    }

    [Fact]
    public void 같은좌표의여러정차는_별개순번과다음핀을보존하고_영길이선을만들지않는다()
    {
        var workspace = TwoJobs();
        foreach (var stop in workspace.권장경로정차목록) { stop.위도 = 37.6m; stop.경도 = 127.1m; }
        var result = Build(workspace);
        Assert.Equal(3, result.Markers.Count);
        Assert.Single(result.RouteOverlays);
        var pins = DriverRecommendationMarkerPresentationPolicy.Build(result.Markers);
        Assert.Equal("1 · 상차", pins[^1].Caption);
        Assert.False(pins[^1].IsMuted);
        Assert.All(pins.Take(2), pin => Assert.True(pin.IsMuted));
    }

    [Fact]
    public void 기존추천선택의색상과JSON계약은_정차필드가없으면유지한다()
    {
        var original = new DriverMapMarkerItem("legacy", 37.6, 127.1, 37.61, 127.11, "제목", "설명", "상차 주소");
        var json = JsonSerializer.Serialize(original);
        Assert.DoesNotContain("stopOrder", json);
        Assert.DoesNotContain("isNextStop", json);
        var pins = DriverRecommendationMarkerPresentationPolicy.Build([original], true, "legacy");
        Assert.Equal(DriverRecommendationMarkerPresentationPolicy.PickupTintColor, pins[0].TintColor);
        Assert.Equal(DriverRecommendationMarkerPresentationPolicy.DropoffTintColor, pins[1].TintColor);
        var ordered = original with { StopOrder = 1, IsNextStop = true };
        Assert.Equal(ordered, JsonSerializer.Deserialize<DriverMapMarkerItem>(JsonSerializer.Serialize(ordered)));
    }

    private DriverOrderedRouteMapState Build(기사화물운송작업공간응답 workspace)
        => _maps.BuildWorkspaceRouteState(Location(), Current(workspace.다음행동운송!), workspace, true);

    private static 기사화물운송작업공간응답 TwoJobs()
        => Workspace([Summary(1, "상차완료"), Summary(2, "확정")],
            [Stop(2, "상차", 0), Stop(2, "하차", 1), Stop(1, "하차", 2)], 2);

    private static 기사화물운송작업공간응답 Workspace(기사운송요약응답[] transports, 기사화물경로정차응답[] stops, long firstId)
        => new() { 수락정차순서반영됨 = true, 활성운송목록 = transports,
            다음행동운송 = transports.Single(item => item.Id == firstId), 권장경로정차목록 = stops };

    private static 기사운송요약응답 Summary(long id, string status)
        => new() { Id = id, 운송번호 = $"invoice-{id}", 기사_운송자 = "driver-r21", 상태 = status };

    private static 기사화물경로정차응답 Stop(long id, string stage, int order)
        => new() { 의뢰Id = $"request-{id}", 운송Id = id, 단계 = stage, 순서 = order,
            주소 = $"합성 장소 {order}", 위도 = 37.6m + order * 0.001m, 경도 = 127.1m + order * 0.001m,
            좌표근거있음 = true, 시간창근거있음 = true, 시간창시작일시 = DateTime.UtcNow,
            시간창종료일시 = DateTime.UtcNow.AddHours(2) };

    private static 기사운송샘플항목 Current(기사운송요약응답 summary)
        => new(summary.Id, summary.운송번호, "합성 화물", "현재 상차지", "현재 하차지",
            37.6m, 127.1m, 37.61m, 127.11m, summary.상태, DateTime.UtcNow, 3, 8000, false, false,
            "별도정산", summary.상태 is "상차완료" or "운송중" or "하차지도착" ? "하차 확인" : "상차 확인");

    private static 기사현재위치샘플 Location()
        => new("최근 확인 위치", 37.59m, 127.09m, DateTime.UtcNow)
            { SourceCode = DriverNativeLocationSources.ServerLocationReceived, 관측시각Utc = DateTime.UtcNow.AddSeconds(-1) };
}
