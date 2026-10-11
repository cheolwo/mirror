using System.Text.Json;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Food;
using Ssalddel.Application.Food;
using Ssalddel.Application.Food.Commands;
using Ssalddel.Application.Food.Handlers;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Tests.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Common;
using Ssalddel.Contracts.Common.Participants;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Food;
using Ssalddel.Infrastructure.Storage.Memory;
using Ssalddel.Services.Community;
using Ssalddel.Services.Food;
using Ssalddel.Services.Outbox;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;
using 살뜰.Services.Weather;
using 살뜰.Services.Settlement;
using 살뜰.Services.Storage.Local;
using 살뜰.Services.Transport;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;
using 살뜰.도메인.창고;
using 살뜰.도메인.화주;
using 살뜰.도메인.음식;


namespace Ssalddel.Tests.Services.Dispatch.AI;

public sealed partial class FoodDispatchWorkflowScenarioFixture
{
    private sealed class PassThroughMenuValidationService : I음식주문메뉴검증Service
    {
        public Task<음식주문등록요청> 서버기준요청생성Async(
            음식주문등록요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(request);
    }

    private sealed class RecordingPublisher : IPublisher
    {
        public List<object> Notifications { get; } = [];

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed record TestCurrentUserAccessor(string? UserId, string? Role) : ICurrentUserAccessor;

    private sealed class NoOpRouteService(bool routeAvailable = true) : I배차추천경로Service
    {
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string driverId, 용달기사? driver, DriverLocationSnapshot? currentLocation, 배차추천검색조건? criteria)
            => Task.FromResult<배차경로좌표?>(null);

        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string driverId, 용달기사? driver, DriverLocationSnapshot? currentLocation)
            => Task.FromResult<배차경로좌표?>(null);

        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination, string routeOption, CancellationToken cancellationToken)
            => Task.FromResult<배차경로예상결과?>(new(routeOption == "traavoidcaronly" ? 2.5m : 0m, TimeSpan.FromMinutes(5), 0m,
                routeAvailable ? "Directions5" : "좌표기반도로보정", routeAvailable) { RouteOption = routeOption });

        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination)
            => Task.FromResult<배차경로예상결과?>(routeAvailable ? new(0m, TimeSpan.Zero, 0m, "Directions5", true) : new(1m, TimeSpan.FromMinutes(5), null, "좌표기반도로보정", false));

        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> orderedStops, CancellationToken cancellationToken = default)
            => Task.FromResult<배차경로예상결과?>(null);

        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? routeAnchor, 배차경로좌표? pickup, 배차경로좌표? dropoff)
            => Task.FromResult<배차삽입경로예상결과?>(null);

        public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target) => Math.Abs(source.Latitude - target.Latitude) * 111m + Math.Abs(source.Longitude - target.Longitude) * 88m;
    }

    private sealed class NoOpQueueTransition : I배차대기원장전환Service
    {
        public Task<배차대기원장전환결과> 계획배차에서추천으로전환Async(string requestId, CancellationToken cancellationToken = default)
            => NotChanged(requestId);

        public Task<배차대기원장전환결과> 추천대기처리Async(string requestId, CancellationToken cancellationToken = default)
            => NotChanged(requestId);

        public Task<배차대기원장전환결과> 추천시작Async(string requestId, string driverId, int? timeoutSeconds = null, CancellationToken cancellationToken = default)
            => NotChanged(requestId, driverId);

        public Task<배차대기원장전환결과> 추천거절처리Async(string requestId, string driverId, CancellationToken cancellationToken = default)
            => NotChanged(requestId, driverId);

        public Task<배차대기원장전환결과> 추천거절처리Async(string requestId, string driverId, string? reasonCode, CancellationToken cancellationToken = default)
            => NotChanged(requestId, driverId);

        public Task<배차대기원장전환결과> 추천만료처리Async(string requestId, CancellationToken cancellationToken = default)
            => NotChanged(requestId);

        public Task<배차대기원장전환결과> 공개배차로전환Async(string requestId, CancellationToken cancellationToken = default)
            => NotChanged(requestId);

        public Task<배차대기원장전환결과> 실행주체확정결과동기화Async(string requestId, DispatchConfirmationBoundaryRequest confirmation, CancellationToken cancellationToken = default)
            => NotChanged(requestId);

        public Task<배차대기원장전환결과> 배차수락취소처리Async(string requestId, string driverId, string? reason = null, CancellationToken cancellationToken = default)
            => NotChanged(requestId, driverId);

        private static Task<배차대기원장전환결과> NotChanged(string requestId, string? driverId = null)
            => Task.FromResult(배차대기원장전환결과.전환안됨(
                requestId,
                배차대기원장전환결과코드.단계불일치,
                "정상 생명주기 시험에서는 재배차 전환을 사용하지 않습니다.",
                driverId));
    }

    private sealed class NoOpSettlement : I기사월정산Service
    {
        public Task<기사월정산> 배차확정반영Async(string 기사Id, DateTime? 기준시각Utc = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Create(기사Id, 기준시각Utc));

        public Task<기사월정산> 월마감처리Async(string 기사Id, int 년도, int 월, DateTime? 기준시각Utc = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Create(기사Id, 기준시각Utc));

        public Task<기사월정산> 월말청구결제완료처리Async(string 기사Id, int 년도, int 월, DateTime? 기준시각Utc = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Create(기사Id, 기준시각Utc));

        private static 기사월정산 Create(string driverId, DateTime? now)
        {
            var at = now ?? DateTime.UtcNow;
            return new 기사월정산 { 기사Id = driverId, 년도 = at.Year, 월 = at.Month };
        }
    }

    private sealed class NoOpTransportSync : I운송원장Mongo동기화Service
    {
        public Task<커뮤니티원장Dto?> 화주운송의뢰동기화Async(화주운송의뢰 의뢰, string updatedBy, CancellationToken cancellationToken = default)
            => Task.FromResult<커뮤니티원장Dto?>(null);

        public Task<커뮤니티원장Dto?> 운송실행투영동기화Async(운송원장 운송실행투영, string updatedBy, CancellationToken cancellationToken = default)
            => Task.FromResult<커뮤니티원장Dto?>(new() { 원장Id = 운송원장Mongo동기화Builder.원장Id생성(운송실행투영.의뢰Id!) });

        public Task<운송원장Mongo동기화상태> 상태조회Async(string 의뢰Id, CancellationToken cancellationToken = default)
            => Task.FromResult(운송원장Mongo동기화상태.Empty(의뢰Id, string.Empty));
    }

    private sealed class NoOpFoodLedgerOutbox : I음식마트원장동기화OutboxService
    {
        public Task 음식주문예약후즉시처리Async(음식주문응답 order, string updatedBy, string idempotencyKey, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task 출고원장예약후즉시처리Async(
            IReadOnlyList<출고예정> outbounds,
            IReadOnlyList<입고요청> inbounds,
            string updatedBy,
            string idempotencyKey,
            string? currentStageKey = null,
            string? ledgerTemplateKey = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> 대기항목처리Async(int take = 100, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class NoOpRestaurantNotification : I음식점주문실시간알림Service
    {
        public Task 신규주문알림발송Async(음식주문응답 order, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task 주문상태변경알림발송Async(음식주문응답 order, string reason, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class PricingWeather : I픽업지기상관측Client
    {
        public Task<픽업지기상관측결과> 조회Async(decimal? latitude, decimal? longitude, CancellationToken cancellationToken = default)
            => Task.FromResult(new 픽업지기상관측결과(false, false, 픽업지기상자료상태Code.MissingServiceKey,
                null, null, null, 픽업지기상관측결과.공식자료출처, null));
    }

}
