using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Transport;
using 살뜰.Data;
using 살뜰.Services;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;

namespace Ssalddel.Hubs
{
    [Authorize(Roles = 역할명.기사)]
    public class DispatchRecommendationHub : Hub
    {
        private readonly SsalddelContext _db;
        private readonly IDriverLocationStore _driverLocationStore;
        private readonly I국내화물운송기사상태Service _국내화물운송기사상태Service;
        private readonly I배차추천Service _dispatchRecommendationService;

        public DispatchRecommendationHub(
            SsalddelContext db,
            IDriverLocationStore driverLocationStore,
            I국내화물운송기사상태Service 국내화물운송기사상태Service,
            I배차추천Service dispatchRecommendationService)
        {
            _db = db;
            _driverLocationStore = driverLocationStore;
            _국내화물운송기사상태Service = 국내화물운송기사상태Service;
            _dispatchRecommendationService = dispatchRecommendationService;
        }

        public override async Task OnConnectedAsync()
        {
            var driverId = GetDriverId();
            if (!string.IsNullOrWhiteSpace(driverId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GetDriverGroup(driverId));
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var driverId = GetDriverId();
            if (!string.IsNullOrWhiteSpace(driverId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetDriverGroup(driverId));
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task UpdateDriverStatus(DriverStatusUpdateRequest request)
        {
            var driverId = RequireDriverId();
            if (request == null) throw new HubException("상태 정보가 필요합니다.");
            if (string.IsNullOrWhiteSpace(request.운행상태)) throw new HubException("운행상태가 필요합니다.");

            await UpdateDriverStatusAsync(driverId, request.운행상태.Trim());

            if (string.Equals(request.운행상태.Trim(), 상태값.기사운행상태.운행중, StringComparison.OrdinalIgnoreCase))
            {
                await _dispatchRecommendationService.SendToDriverAsync(driverId);
            }
        }

        public async Task SubmitLocationUpdate(DriverLocationUpdateRequest request)
        {
            var driverId = RequireDriverId();
            if (request == null) throw new HubException("위치 정보가 필요합니다.");
            if (!request.위도.HasValue || !request.경도.HasValue) throw new HubException("위도와 경도가 필요합니다.");

            var currentStatus = await GetDriverOperatingStatusAsync(driverId);
            var requestedStatus = string.IsNullOrWhiteSpace(request.운행상태) ? currentStatus : request.운행상태.Trim();
            if (!string.Equals(requestedStatus, 상태값.기사운행상태.운행중, StringComparison.OrdinalIgnoreCase))
            {
                throw new HubException("운행중 상태에서만 위치를 전송할 수 있습니다.");
            }

            var snapshot = new DriverLocationSnapshot(
                driverId,
                request.위도.Value,
                request.경도.Value,
                request.정확도_m,
                requestedStatus,
                request.기록시각 ?? DateTime.UtcNow,
                DateTime.UtcNow);

            _driverLocationStore.Upsert(snapshot);
            await StoreLocationAsync(snapshot);
            await _국내화물운송기사상태Service.위치갱신Async(
                snapshot,
                상차접근허용반경Km: request.상차접근허용반경Km);
            await UpdateDriverStatusAsync(driverId, requestedStatus);

            await _dispatchRecommendationService.SendToDriverAsync(driverId);
        }


        private async Task UpdateDriverStatusAsync(string driverId, string status)
        {
            var driver = await _db.용달기사.FirstOrDefaultAsync(d => d.기사Id == driverId);
            if (driver == null)
            {
                return;
            }

            driver.운행상태 = status;
            driver.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        private async Task<string> GetDriverOperatingStatusAsync(string driverId)
        {
            var driver = await _db.용달기사.FirstOrDefaultAsync(d => d.기사Id == driverId);
            return driver?.운행상태 ?? string.Empty;
        }

        private async Task StoreLocationAsync(DriverLocationSnapshot snapshot)
        {
            _db.기사위치기록.Add(new 기사위치기록
            {
                기사Id = snapshot.DriverId,
                위도 = snapshot.Latitude,
                경도 = snapshot.Longitude,
                정확도_m = snapshot.AccuracyM,
                기록시각 = snapshot.RecordedAtUtc,
                CreatedAt = snapshot.ReceivedAtUtc,
                UpdatedAt = snapshot.ReceivedAtUtc
            });

            await _db.SaveChangesAsync();
        }


        private string RequireDriverId()
        {
            var driverId = GetDriverId();
            if (string.IsNullOrWhiteSpace(driverId))
            {
                throw new HubException("driverId가 필요합니다.");
            }

            return driverId;
        }

        private string? GetDriverId()
        {
            return Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        }

        private static string GetDriverGroup(string driverId) => $"driver-{driverId}";
    }

    public abstract class 거리기반추천응답
    {
        public decimal? 직선거리Km { get; set; }
        public decimal? 주행거리Km { get; set; }

        public string 거리표시 => 직선거리Km.HasValue || 주행거리Km.HasValue
            ? $"직선 {직선거리Km?.ToString("0.0") ?? "-"}km / 주행 {주행거리Km?.ToString("0.0") ?? "-"}km"
            : "거리 미정";
    }

    public sealed class DispatchRecommendationDto : 거리기반추천응답
    {
        public string 의뢰Id { get; set; } = string.Empty;
        public string 화물종류 { get; set; } = string.Empty;
        public string 운송의뢰유형코드 { get; set; } = "GeneralCargoTransport";
        public string 운송의뢰유형표시 { get; set; } = "일반 화물";
        public bool 공동주문운송여부 { get; set; }
        public bool 세대배송포함여부 { get; set; }
        public int? 세대배송건수 { get; set; }
        public string 세대배송업무표시 { get; set; } = "상하차";
        public string 픽업지 { get; set; } = string.Empty;
        public string 하차지 { get; set; } = string.Empty;
        public decimal? 픽업_위도 { get; set; }
        public decimal? 픽업_경도 { get; set; }
        public decimal? 하차_위도 { get; set; }
        public decimal? 하차_경도 { get; set; }
        public decimal? 픽업거리Km { get; set; }
        public decimal? 공차거리Km { get; set; }
        public decimal? 운송거리Km { get; set; }
        public decimal? 복귀예상거리Km { get; set; }
        public decimal? 지금바로복귀거리Km { get; set; }
        public decimal? 복귀우회증가거리Km { get; set; }
        public decimal? 총공차거리Km { get; set; }
        public decimal? 예상톨비 { get; set; }
        public decimal? 예상연료비 { get; set; }
        public decimal? 예상총비용 { get; set; }
        public decimal? 예상수익 { get; set; }
        public decimal? 예상추가순이익 { get; set; }
        public decimal? 분당추가수익 { get; set; }
        public string 추천유형 { get; set; } = string.Empty;
        public decimal? 추가예상시간분 { get; set; }
        public decimal? 기존배송지연분 { get; set; }
        public decimal? 기존경로거리Km { get; set; }
        public decimal? 삽입경로거리Km { get; set; }
        public decimal? 기존경로소요시간분 { get; set; }
        public decimal? 삽입경로소요시간분 { get; set; }
        public decimal? 삽입추가톨비 { get; set; }
        public decimal? 추천점수 { get; set; }
        public string 추천사유 { get; set; } = string.Empty;
        public bool 일정삽입가능여부 { get; set; }
        public bool 전체일정완수가능여부 { get; set; }
        public int? 최적삽입인덱스 { get; set; }
        public bool 경로변경이점여부 { get; set; }
        public decimal? 경로변경절감분 { get; set; }
        public string[] 권장경로순서 { get; set; } = Array.Empty<string>();
        public decimal? 최대시간위반분 { get; set; }
        public string[] 일정위반사유 { get; set; } = Array.Empty<string>();
        public bool 복귀지기준추천여부 { get; set; }
        public string? 복귀지출처 { get; set; }
        public string? 복귀추천사유 { get; set; }
        public bool? 권역복귀정책선택됨 { get; set; }
        public bool? 권역복귀활성여부 { get; set; }
        public bool? 권역복귀확인됨 { get; set; }
        public decimal? 권역복귀가점 { get; set; }
        public decimal? 전체일정기존복귀거리Km { get; set; }
        public decimal? 전체일정후보복귀거리Km { get; set; }
        public string[] 배지 { get; set; } = Array.Empty<string>();
        public string[] 경고 { get; set; } = Array.Empty<string>();
        public bool 차량적합여부 { get; set; } = true;
        public string[] 차량부적합사유 { get; set; } = Array.Empty<string>();
        public string[] 차량경고 { get; set; } = Array.Empty<string>();
        public DateTime? 추천시작시각 { get; set; }
        public DateTime? 추천만료시각 { get; set; }
        public int 추천라운드 { get; set; }

        public new decimal? 직선거리Km
        {
            get => base.직선거리Km;
            set => base.직선거리Km = value;
        }

        public new decimal? 주행거리Km
        {
            get => base.주행거리Km;
            set => base.주행거리Km = value;
        }
        public string 상태 { get; set; } = string.Empty;
        public string 배차상태 { get; set; } = string.Empty;
        public 운송실행프로필Dto 운송실행프로필 { get; set; } = new();
    }

    public sealed class DriverStatusUpdateRequest
    {
        public string 운행상태 { get; set; } = string.Empty;
    }

    public sealed class DriverLocationUpdateRequest
    {
        public decimal? 위도 { get; set; }
        public decimal? 경도 { get; set; }
        public decimal? 정확도_m { get; set; }
        public decimal? 상차접근허용반경Km { get; set; }
        public string? 운행상태 { get; set; }
        public DateTime? 기록시각 { get; set; }
    }
}
