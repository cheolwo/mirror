using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CargoRelatedDriverLocationR8Tests
{
    [Fact]
    public void UnspecifiedUtcRetainsTheServerInstantWithoutKoreanLocalShift()
    {
        var recorded = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(-1), DateTimeKind.Unspecified);
        var normalized = ShipperRequestDetailPresentation.NormalizeUtc(recorded);
        Assert.Equal(recorded.Ticks, normalized.Ticks);
        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
        var item = ShipperWorkspaceAdapter.Map(new()
        {
            의뢰Id = "r8", 운송상태 = "운송중", 확정기사Id = "driver", 기사최근위도 = 37.58m,
            기사최근경도 = 127.08m, 기사최근위치시각Utc = recorded
        });
        var marker = Assert.Single(item.Markers!.Where(marker => marker.Kind == NeighborhoodMapMarkerKinds.Driver));
        Assert.Equal(new DateTimeOffset(normalized), marker.MeasuredAt);
        Assert.Equal(new DateTimeOffset(normalized.AddMinutes(10)), marker.ExpiresAt);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-601)]
    [InlineData(-600)]
    public void FutureOrExpiredUnspecifiedUtcDoesNotBecomeCurrentLocation(int seconds)
    {
        var now = DateTime.UtcNow;
        var source = new ShipperRequestDetailSnapshot { AssignedDriverId = "driver", TransportStatus = "운송중",
            DriverLocationRecordedAtUtc = DateTime.SpecifyKind(now.AddSeconds(seconds), DateTimeKind.Unspecified) };
        Assert.True(ShipperRequestDetailPresentation.IsDriverLocationStale(source, now));
        Assert.DoesNotContain("최신", ShipperRequestDetailPresentation.ResolveDriverLocationFreshness(source, now));
    }
}
