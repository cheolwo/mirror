using DriverApp.Models.Driver.Samples;
using DriverApp.Models.Driver;
using DriverApp.Services;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Contracts.Driver.Work;

namespace Ssalddel.Tests.Clients.Cargo;

public sealed class DriverNativeLocationR16Tests
{
    private static readonly DateTime Now = new(2026, 10, 5, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void WorkUpdatedAtAndStartLocation_CannotBecomeObservedLocation()
    {
        var location = DriverNativeLocationPolicy.FromServer(new()
        {
            현재위도 = 37.6m, 현재경도 = 127.1m, UpdatedAt = Now
        }, new() { 시작위치 = "출근 장소", 시작시각 = Now });

        var display = DriverNativeLocationPolicy.Present(location, Now);
        Assert.False(display.IsAvailable);
        Assert.Null(location.관측시각Utc);
        Assert.Equal(DateTime.MinValue, location.갱신시각);
        Assert.Equal("위치 미확인", location.위치명);
        Assert.Equal(DriverNativeLocationSources.Unknown, location.SourceCode);
    }

    [Fact]
    public void ReturnCoordinates_AreOnlyCameraFallback()
    {
        var location = DriverNativeLocationPolicy.FromServer(null, new()
        {
            오늘의복귀지위도 = 37.7m, 오늘의복귀지경도 = 127.2m, 오늘의복귀지주소 = "복귀지"
        });

        Assert.Equal(0m, location.위도);
        Assert.Equal(0m, location.경도);
        Assert.Null(DriverNativeLocationPolicy.Present(location, Now).Point);
        Assert.Equal(new DriverNativeLocationPoint(37.7m, 127.2m), DriverNativeLocationPolicy.CameraCenter(location, Now));
        Assert.Contains("복귀지 주변", DriverNativeLocationPolicy.Present(location, Now).Notice);
        Assert.Equal(0m, DriverNativeLocationPolicy.ForCurrentRoute(location, Now).위도);
    }

    [Fact]
    public void OneObservedAxis_CannotBorrowOtherAxisFromReturnLocation()
    {
        var location = DriverNativeLocationPolicy.FromServer(new()
        {
            현재위도 = 37.6m, 최근위치수신시각 = Now
        }, new() { 오늘의복귀지위도 = 37.7m, 오늘의복귀지경도 = 127.2m });

        Assert.Equal(0m, location.경도);
        Assert.False(DriverNativeLocationPolicy.Present(location, Now).IsAvailable);
    }

    [Fact]
    public void LegacyFourArgumentSample_IsNeverAnObservedCurrentPoint()
    {
        var sample = new 기사현재위치샘플("예시 지역", 37.6m, 127.1m, Now);
        var display = DriverNativeLocationPolicy.Present(sample, Now);
        Assert.Equal(DriverNativeLocationSources.Sample, sample.SourceCode);
        Assert.Equal("sample", display.StateCode);
        Assert.Null(display.ObservedAtUtc);
        Assert.Null(display.Point);
    }

    [Fact]
    public void ServerReceivedLocation_UsesSeparatePointAndReceivedTimestamp()
    {
        var location = Observed(Now.AddSeconds(-30));
        var display = DriverNativeLocationPolicy.Present(location, Now);
        Assert.Equal(new DriverNativeLocationPoint(37.6m, 127.1m), display.Point);
        Assert.Equal(Now.AddSeconds(-30), display.ObservedAtUtc);
        Assert.Equal(DriverNativeLocationSources.ServerLocationReceived, display.SourceCode);
        Assert.Contains("최근 확인 위치", display.Notice);
        Assert.Contains("서버 수신", display.Notice);
    }

    [Fact]
    public void TenMinuteBoundary_IsAvailable_ThenExpiresWithoutCameraChange()
    {
        var location = Observed(Now);
        var camera = DriverNativeLocationPolicy.CameraCenter(location, Now);
        Assert.True(DriverNativeLocationPolicy.Present(location, Now.AddMinutes(10)).IsAvailable);
        var later = DriverNativeLocationPolicy.Present(location, Now.AddMinutes(10).AddTicks(1));
        Assert.False(later.IsAvailable);
        Assert.Equal("stale", later.StateCode);
        Assert.Equal(Now, later.ObservedAtUtc);
        Assert.Contains("위치 확인 지연", later.Notice);
        Assert.Equal(new DriverNativeLocationPoint(37.6m, 127.1m), camera);
        Assert.Equal(Now, location.관측시각Utc);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void FutureReceivedTimestamp_IsNotTreatedAsFresh(int secondsInFuture)
    {
        var display = DriverNativeLocationPolicy.Present(Observed(Now.AddSeconds(secondsInFuture)), Now);
        Assert.Equal("unknown", display.StateCode);
        Assert.Null(display.Point);
    }

    [Theory]
    [InlineData(91, 127)]
    [InlineData(37, 181)]
    [InlineData(0, 127)]
    [InlineData(37, 0)]
    public void InvalidOrLegacyMissingCoordinates_CannotBeRendered(int latitude, int longitude)
    {
        var location = DriverNativeLocationPolicy.FromServer(new()
        {
            현재위도 = latitude, 현재경도 = longitude, 최근위치수신시각 = Now
        }, null);
        Assert.False(DriverNativeLocationPolicy.Present(location, Now).IsAvailable);
    }

    [Fact]
    public void MissingObservation_CameraUsesPublicDefaultWithoutCreatingCurrentPoint()
    {
        Assert.Equal(DriverNativeLocationPolicy.DefaultCameraCenter, DriverNativeLocationPolicy.CameraCenter(null, Now));
        Assert.Null(DriverNativeLocationPolicy.Present(null, Now).Point);
    }

    [Fact]
    public void ExpiredCurrentRoutePoint_IsRemovedWhilePickupAndDropoffRemain()
    {
        IReadOnlyList<DriverMapRouteOverlay> routes =
        [new("route", "운송 경로",
            [new(37.6, 127.1, "현재 위치"), new(37.61, 127.11, "현재 운송 상차지"), new(37.62, 127.12, "추천 하차지")])];
        var location = Observed(Now);
        var fresh = Assert.Single(DriverNativeLocationPolicy.RoutesForDisplay(routes, location, Now));
        Assert.Equal("최근 확인 위치", fresh.Points[0].Label);
        var expired = Assert.Single(DriverNativeLocationPolicy.RoutesForDisplay(routes, location, Now.AddMinutes(11)));
        Assert.Equal(2, expired.Points.Count);
        Assert.Equal("현재 운송 상차지", expired.Points[0].Label);
        Assert.Equal(3, routes[0].Points.Count);
    }

    [Fact]
    public void TransportSummary_HasNoInventedPickupOrDropoffCoordinates()
    {
        var mapped = 기사운송표시Mapper.Map(new 기사운송요약응답 { Id = 7, 상태 = "상차지도착", 출발지 = "상차지", 도착지 = "하차지" });
        Assert.Null(mapped.픽업위도);
        Assert.Null(mapped.픽업경도);
        Assert.Null(mapped.하차위도);
        Assert.Null(mapped.하차경도);
        Assert.Equal("상차 완료", mapped.다음행동);
    }

    [Fact]
    public void AuthorizedTransportDetail_CarriesCoordinatesAndExistingWorkFields()
    {
        var mapped = 기사운송표시Mapper.Map(new 기사운송상세응답
        {
            Id = 7, 상태 = "상차완료", 출발지 = "상차지", 도착지 = "하차지", 운임 = 12000m,
            픽업위도 = 37.6m, 픽업경도 = 127.1m, 하차위도 = 37.7m, 하차경도 = 127.2m,
            수령자명 = "수령자", 전달요청 = "업무 요청", 인수증필요 = true
        });
        Assert.Equal(37.6m, mapped.픽업위도);
        Assert.Equal(127.1m, mapped.픽업경도);
        Assert.Equal(37.7m, mapped.하차위도);
        Assert.Equal(127.2m, mapped.하차경도);
        Assert.Equal(12000m, mapped.예상수익);
        Assert.Equal("수령자", mapped.수령자명);
        Assert.Equal("업무 요청", mapped.전달요청);
        Assert.Equal("하차지 도착", mapped.다음행동);
        Assert.True(mapped.인수증필요);
    }

    [Fact]
    public void PrivacyHeldDetail_DoesNotExposeCoordinatesEvenIfPayloadContainsThem()
    {
        var mapped = 기사운송표시Mapper.Map(new 기사운송상세응답
        {
            Id = 7, 개인정보제공보류 = true,
            픽업위도 = 37.6m, 픽업경도 = 127.1m, 하차위도 = 37.7m, 하차경도 = 127.2m
        });
        Assert.Null(mapped.픽업위도);
        Assert.Null(mapped.픽업경도);
        Assert.Null(mapped.하차위도);
        Assert.Null(mapped.하차경도);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("stale")]
    [InlineData("sample")]
    public void UnavailableLocation_DoesNotCalculateOrRankRecommendationDistance(string state)
    {
        var location = state switch
        {
            "stale" => Observed(Now.AddMinutes(-11)),
            "sample" => new 기사현재위치샘플("예시 위치", 37.6m, 127.1m, Now),
            _ => DriverNativeLocationPolicy.FromServer(new() { 현재위도 = 37.6m, 현재경도 = 127.1m }, null)
        };
        var request = new DriverRequestItem { 의뢰Id = "request", 픽업_위도 = 37.61m, 픽업_경도 = 127.11m };
        var item = Assert.Single(DriverNativeLocationPolicy.RecommendationsWithDistance([request], location, Now,
            (_, _, _, _) => throw new InvalidOperationException("미확인 위치로 거리를 계산하면 안 됩니다.")));

        Assert.Null(item.상차지까지거리Km);
        Assert.Null(item.가까운순위);
        Assert.Equal("거리 미확인", item.상차지까지거리표시);
    }

    [Fact]
    public void MissingPickupCoordinate_DoesNotBorrowCurrentCoordinateOrReportZeroDistance()
    {
        var request = new DriverRequestItem { 의뢰Id = "request", 픽업_위도 = 37.61m };
        var item = Assert.Single(DriverNativeLocationPolicy.RecommendationsWithDistance([request], Observed(Now), Now,
            (_, _, _, _) => throw new InvalidOperationException("상차 좌표를 임의로 채우면 안 됩니다.")));

        Assert.Null(item.상차지까지거리Km);
        Assert.Null(item.가까운순위);
        Assert.Equal("거리 미확인", item.상차지까지거리표시);
    }

    [Fact]
    public void VerifiedRecommendationDistance_PassesActualCoordinatesAndKeepsConfirmedZero()
    {
        var request = new DriverRequestItem { 의뢰Id = "request", 픽업_위도 = 37.6m, 픽업_경도 = 127.1m };
        var item = Assert.Single(DriverNativeLocationPolicy.RecommendationsWithDistance([request], Observed(Now), Now,
            (latitude, longitude, pickupLatitude, pickupLongitude) =>
            {
                Assert.Equal(37.6m, latitude);
                Assert.Equal(127.1m, longitude);
                Assert.Equal(request.픽업_위도, pickupLatitude);
                Assert.Equal(request.픽업_경도, pickupLongitude);
                return 0m;
            }));

        Assert.Equal(0m, item.상차지까지거리Km);
        Assert.Equal(1, item.가까운순위);
        Assert.Equal("0.0km", item.상차지까지거리표시);
    }

    [Fact]
    public void ExpiredLocation_DiscardsLegacyNumbersAndDoesNotApplyStaleRadiusFilter()
    {
        IReadOnlyList<추천의뢰표시항목> recommendations =
        [new(new() { 의뢰Id = "far" }, 10000m, 2), new(new() { 의뢰Id = "near" }, 0m, 1)];
        var selected = DriverNativeLocationPolicy.SelectRecommendations(recommendations, Observed(Now), Now.AddMinutes(11), 5m, false);

        Assert.Equal(new[] { "far", "near" }, selected.Select(item => item.의뢰.의뢰Id));
        Assert.All(selected, item =>
        {
            Assert.Null(item.상차지까지거리Km);
            Assert.Null(item.가까운순위);
            Assert.Equal("거리 미확인", item.상차지까지거리표시);
        });
        Assert.Equal(10000m, recommendations[0].상차지까지거리Km);
    }

    [Fact]
    public void FreshLocation_SortsUnknownLastAndAppliesRadiusOnlyToKnownDistance()
    {
        IReadOnlyList<추천의뢰표시항목> recommendations =
        [new(new() { 의뢰Id = "unknown" }, null, null), new(new() { 의뢰Id = "far" }, 8m, 2), new(new() { 의뢰Id = "near" }, 2m, 1)];
        var all = DriverNativeLocationPolicy.SelectRecommendations(recommendations, Observed(Now), Now, null, false);
        Assert.Equal(new[] { "near", "far", "unknown" }, all.Select(item => item.의뢰.의뢰Id));
        var withinRadius = DriverNativeLocationPolicy.SelectRecommendations(recommendations, Observed(Now), Now, 5m, false);
        Assert.Equal("near", Assert.Single(withinRadius).의뢰.의뢰Id);
    }

    [Fact]
    public void UnknownDistance_StillAllowsIncomeOrderingWithoutInventingLocation()
    {
        IReadOnlyList<추천의뢰표시항목> recommendations =
        [new(new() { 의뢰Id = "low", 예상수익 = 1000m }, 1m, 1), new(new() { 의뢰Id = "high", 예상수익 = 5000m }, 2m, 2)];
        var selected = DriverNativeLocationPolicy.SelectRecommendations(recommendations, null, Now, 5m, true);

        Assert.Equal(new[] { "high", "low" }, selected.Select(item => item.의뢰.의뢰Id));
        Assert.All(selected, item => Assert.Null(item.상차지까지거리Km));
    }

    private static 기사현재위치샘플 Observed(DateTime timestamp)
        => DriverNativeLocationPolicy.FromServer(new()
        {
            현재위도 = 37.6m, 현재경도 = 127.1m, 최근위치수신시각 = timestamp
        }, null);
}
