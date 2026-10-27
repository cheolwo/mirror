using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

/// <summary>권한이 확인된 운송 응답의 위치만 표시합니다. 주소에서 기사 좌표를 추정하지 않습니다.</summary>
public sealed record CargoDriverLocationPresentation(string Notice, NeighborhoodMapMarker? Marker)
{
    public RoleWorkspaceField Field(string label) => new(label, Notice)
    {
        ValueExpiresAtUtc = Marker?.ExpiresAt,
        ExpiredValue = Marker is null ? null : "위치 갱신 지연 · 현재 위치 확인 필요"
    };

    public static CargoDriverLocationPresentation Create(string itemId, string? driverId, string? stateCode,
        decimal? latitude, decimal? longitude, DateTime? recordedAtUtc, bool ended, DateTime? nowUtc = null)
    {
        if (ended || stateCode == 기사위치상태Codes.Closed) return new("운송 종료 후 비공개", null);
        if (string.IsNullOrWhiteSpace(driverId)) return new("배차 전", null);
        if (stateCode == 기사위치상태Codes.Delayed) return new("위치 갱신 지연 · 현재 위치 확인 필요", null);
        // 구버전의 빈 코드는 기존 시간 검증을 유지하되 알 수 없는 새 상태는 좌표를 표시하지 않습니다.
        if (!string.IsNullOrWhiteSpace(stateCode) && stateCode != 기사위치상태Codes.Available)
            return new("위치 미수신", null);
        if (recordedAtUtc is not { } recorded) return new("위치 미수신", null);
        var instant = ShipperRequestDetailPresentation.NormalizeUtc(recorded);
        var age = ShipperRequestDetailPresentation.NormalizeUtc(nowUtc ?? DateTime.UtcNow) - instant;
        if (age < TimeSpan.Zero) return new("위치 시각 확인 필요", null);
        if (age >= ShipperRequestDetailPresentation.DriverLocationFreshnessWindow)
            return new("위치 갱신 지연 · 현재 위치 확인 필요", null);
        if (CargoWorkspacePresentation.Point(latitude, longitude) is not { } point
            || point.Latitude == 0 && point.Longitude == 0) return new("위치 미수신", null);
        var measuredAt = new DateTimeOffset(instant);
        return new("최신 위치 · " + measuredAt.ToOffset(TimeSpan.FromHours(9)).ToString("MM/dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
            new(itemId + "|driver", "운송 기사", NeighborhoodMapMarkerKinds.Driver, point.Latitude, point.Longitude,
                MeasuredAt: measuredAt, ExpiresAt: measuredAt.Add(ShipperRequestDetailPresentation.DriverLocationFreshnessWindow)));
    }
}
