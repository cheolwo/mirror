using Ssalddel.Contracts.Admin.Progress;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CargoDriverTrackingPresentationTests
{
    private static readonly DateTime Now = new(2026, 10, 24, 4, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(기사위치상태Codes.Delayed, "갱신 지연")]
    [InlineData(기사위치상태Codes.Unavailable, "미수신")]
    [InlineData(기사위치상태Codes.Closed, "종료 후 비공개")]
    [InlineData("UnknownFutureState", "미수신")]
    public void ExplicitStatusDoesNotResurrectEvenFreshCoordinates(string code, string notice)
    {
        var result = CargoDriverLocationPresentation.Create("transport1", "driver1", code, 37.58m, 127.08m, Now, false, Now);
        Assert.Null(result.Marker);
        Assert.Contains(notice, result.Notice);
    }

    [Theory]
    [InlineData(-600, "갱신 지연")]
    [InlineData(1, "시각 확인")]
    public void AvailableStatusCannotMakeExpiredOrFuturePointCurrent(int ageSeconds, string notice)
    {
        var result = CargoDriverLocationPresentation.Create("transport1", "driver1", 기사위치상태Codes.Available,
            37.58m, 127.08m, Now.AddSeconds(ageSeconds), false, Now);
        Assert.Null(result.Marker);
        Assert.Contains(notice, result.Notice);
    }

    [Fact]
    public void AvailablePointUsesOriginalUtcExpiryAndKoreanDisplayWithoutRawCoordinates()
    {
        var measured = DateTime.SpecifyKind(Now.AddSeconds(-30), DateTimeKind.Unspecified);
        var result = CargoDriverLocationPresentation.Create("transport1", "driver1", 기사위치상태Codes.Available,
            37.58m, 127.08m, measured, false, Now);
        Assert.NotNull(result.Marker);
        Assert.Equal("transport1|driver", result.Marker.Id);
        Assert.Equal(NeighborhoodMapMarkerKinds.Driver, result.Marker.Kind);
        Assert.Equal(new DateTimeOffset(Now.AddSeconds(-30)), result.Marker.MeasuredAt);
        Assert.Equal(new DateTimeOffset(Now.AddSeconds(-30).AddMinutes(10)), result.Marker.ExpiresAt);
        Assert.Contains("10/24 12:59:30", result.Notice);
        Assert.DoesNotContain("37.58", result.Notice);
    }

    [Fact]
    public void EndedOrUnassignedTransportNeverShowsDriverPoint()
    {
        Assert.Null(CargoDriverLocationPresentation.Create("1", "driver", 기사위치상태Codes.Available,
            37m, 127m, Now, true, Now).Marker);
        Assert.Null(CargoDriverLocationPresentation.Create("1", null, 기사위치상태Codes.Available,
            37m, 127m, Now, false, Now).Marker);
        Assert.Null(CargoDriverLocationPresentation.Create("1", "driver", 기사위치상태Codes.Available,
            91m, 127m, Now, false, Now).Marker);
    }

    [Fact]
    public async Task OperatorMapOnlyReceivesCurrentServerPointAndExistingReviewAction()
    {
        var measured = DateTime.UtcNow.AddSeconds(-10);
        var api = new Api(new() { Id = 10, 상태 = "상차완료", 기사_운송자 = "driver", 기사위치상태Code = 기사위치상태Codes.Available,
            기사최근위도 = 37.58m, 기사최근경도 = 127.08m, 기사최근위치시각Utc = measured });
        var snapshot = await new CargoOperatorWorkspaceAdapter(api).LoadAsync("10", default);
        var item = Assert.Single(snapshot.Items);
        var pin = Assert.Single(item.Markers!);
        Assert.Equal("10|driver", pin.Id);
        Assert.Equal(new DateTimeOffset(measured.AddMinutes(10)), pin.ExpiresAt);
        Assert.Empty(item.Routes ?? []); // 한 점을 실제 주행 경로로 연결하지 않는다.
        Assert.Contains(item.Summary!.Metrics!, field => field.Label == "기사 위치" && field.Value.Contains("최신 위치"));
        Assert.Equal(CargoOperatorWorkspaceAdapter.ReviewIncidents, Assert.Single(item.Actions!).Key);
        Assert.Equal(1, api.Gets);
    }

    [Fact]
    public void ShipperDelayedStatusSurvivesRedactedTimestampAndPickupCompleteRemainsTrackable()
    {
        var snapshot = ShipperRequestDetailSnapshot.FromContract(new() { 확정기사Id = "driver", 운송상태 = "상차완료",
            기사위치상태Code = 기사위치상태Codes.Delayed });
        Assert.Contains("갱신 지연", ShipperRequestDetailPresentation.ResolveDriverLocationFreshness(snapshot, Now));
        var live = snapshot with { DriverLocationStateCode = 기사위치상태Codes.Available, DriverLocationRecordedAtUtc = Now };
        Assert.Equal("최신 위치", ShipperRequestDetailPresentation.ResolveDriverLocationFreshness(live, Now));
    }

    [Fact]
    public void ShipperCanSeeLocationStatusWithoutExposingRawAssignedDriverIdAsName()
    {
        var item = ShipperWorkspaceAdapter.Map(new() { 의뢰Id = "r", 확정기사Id = "private-driver-id",
            운송상태 = "운송중", 기사위치상태Code = 기사위치상태Codes.Delayed });
        var driver = Assert.Single(item.Sections!, section => section.Title == "배정 기사");
        Assert.Contains(driver.Fields, field => field.Label == "기사" && field.Value == "이름 확인 필요");
        Assert.Contains(driver.Fields, field => field.Value.Contains("갱신 지연"));
        Assert.DoesNotContain(driver.Fields, field => field.Value.Contains("private-driver-id"));
    }

    private sealed class Api(운송진행응답 value) : IRoleWorkspaceApi
    {
        public int Gets { get; private set; }
        public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken)
        {
            Assert.Equal(RoleWorkspaceCatalog.Operator, roleKey);
            Assert.Equal("api/v1/admin/transports?business=freight-delivery", path);
            Gets++;
            return Task.FromResult((T)(object)new[] { value });
        }
        public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken ct) => throw new InvalidOperationException();
        public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken ct) => throw new InvalidOperationException();
        public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken ct) => throw new InvalidOperationException();
    }
}
