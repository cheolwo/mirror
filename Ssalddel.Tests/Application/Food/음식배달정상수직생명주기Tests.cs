using System.Text.Json;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Food;
using Ssalddel.Application.Food.Commands;
using Ssalddel.Application.Food.Handlers;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Operations;
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
using 살뜰.Services.Settlement;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;
using 살뜰.도메인.창고;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Food;

/// <summary>
/// 배차 후보 선정은 배차 엔진의 독립 시험 범위로 두고, 그 결과로 만들어진 추천 운송 원장부터
/// 실제 음식 주문 Handler와 기사 업무 Service, 완료 World 투영까지 한 판본으로 관통합니다.
/// </summary>
public sealed class 음식배달정상수직생명주기Tests
{
    [Fact]
    public async Task 주문등록부터_수령확인과_Unity용익명상태사본까지_한생명주기로이어진다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var publisher = new RecordingPublisher();
        var store = new EfSsalddelFoodOrderStore(database.Context);
        var register = new 음식주문등록CommandHandler(
            store,
            new PassThroughMenuValidationService(),
            publisher,
            database.Context);
        var accept = new 음식점주문수락CommandHandler(
            store,
            publisher,
            database.Context);
        var prepare = new 음식점주문진행변경CommandHandler(
            store,
            publisher,
            database.Context);

        var order = await register.Handle(
            new 음식주문등록Command(CreateOrderRequest()),
            default);
        order = Assert.IsType<음식주문응답>(await accept.Handle(
            new 음식점주문수락Command(
                order.주문번호,
                new 음식점주문수락요청
                {
                    클라이언트요청Id = Guid.NewGuid(),
                    음식점명 = "면목 식당",
                    음식점주소 = "서울특별시 중랑구 면목동 면목로 2",
                    음식점상세주소 = "1층",
                    음식점위도 = 37.5801m,
                    음식점경도 = 127.0888m,
                    조리예상분 = 10
                },
                "restaurant-41"),
            default));
        order = Assert.IsType<음식주문응답>(await prepare.Handle(
            new 음식점주문진행변경Command(
                order.주문번호,
                new 음식점주문진행변경요청
                {
                    클라이언트요청Id = Guid.NewGuid(),
                    작업 = 음식점주문진행작업코드.픽업준비,
                    사유 = "조리와 포장을 마쳐 기사 픽업을 기다립니다."
                },
                "restaurant-41"),
            default));

        var driverId = "food-driver-7";
        var offerId = $"food-dispatch:{order.주문번호}";
        database.Context.운송원장.Add(CreateRecommendedTransport(offerId, order.주문번호, driverId));
        await database.Context.SaveChangesAsync();

        var driverWork = new 음식배달기사업무Service(
            database.Context,
            new InMemoryDriverLocationStore(),
            new NoOpRouteService(),
            new NoOpQueueTransition(),
            new InMemory음식배달권실행공간Store(),
            store,
            new NoOpFoodLedgerOutbox(),
            new NoOpTransportSync(),
            new NoOpRestaurantNotification(),
            new NoOpSettlement(),
            publisher,
            new TestCurrentUserAccessor(driverId, "Driver"),
            NullLogger<음식배달기사업무Service>.Instance);

        Assert.True((await driverWork.수락Async(driverId, offerId)).IsSuccess);
        Assert.True((await driverWork.픽업완료Async(driverId, offerId)).IsSuccess);
        Assert.True((await driverWork.전달완료Async(driverId, offerId)).IsSuccess);

        var receiptRequest = new 주문자음식주문수령확인요청
        {
            클라이언트요청Id = Guid.NewGuid(),
            확인메모 = "정상 수령"
        };
        var receipt = new 주문자음식주문수령확인CommandHandler(store, publisher);
        var completed = Assert.IsType<음식주문응답>(await receipt.Handle(
            new 주문자음식주문수령확인Command(order.주문번호, receiptRequest, "customer-1"),
            default));
        var duplicate = Assert.IsType<음식주문응답>(await receipt.Handle(
            new 주문자음식주문수령확인Command(order.주문번호, receiptRequest, "customer-1"),
            default));

        var expectedStages = new[]
        {
            음식주문상태코드.주문대기,
            음식주문상태코드.조리중,
            음식주문상태코드.픽업대기,
            음식주문상태코드.기사배정,
            음식주문상태코드.픽업완료,
            음식주문상태코드.전달완료,
            음식주문상태코드.수령확인
        };
        Assert.Equal(음식주문상태코드.수령확인, completed.상태);
        Assert.Equal(completed.Revision, duplicate.Revision);
        Assert.Equal(expectedStages, completed.상태이력.Select(x => x.다음상태));
        Assert.Single(await database.Context.음식마트원장동기화Outbox
            .Where(x => x.동기화유형 == 음식마트원장동기화유형코드.음식배달완료WorldProjection)
            .ToListAsync());

        var projection = new 음식배달완료WorldProjectionService(
            database.Context,
            new 음식배달완료WorldAreaResolver(),
            NullLogger<음식배달완료WorldProjectionService>.Instance);
        Assert.Equal(1, await projection.대기항목처리Async());
        Assert.Equal(0, await projection.대기항목처리Async());

        var response = await new 음식배달완료WorldSnapshot조회UseCase(database.Context)
            .지역목록Async(음식배달완료WorldAreaStableIds.Myeonmok, 10, default);
        var snapshot = Assert.Single(response.Items);
        Assert.Equal(completed.Revision, snapshot.LifecycleRevision);
        Assert.Equal(expectedStages, snapshot.Milestones.Select(x => x.StageCode));
        Assert.False(snapshot.LocalStorageAllowed);
        Assert.False(snapshot.ReplayAllowed);

        var publicJson = JsonSerializer.Serialize(snapshot);
        Assert.DoesNotContain(order.주문번호, publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("customer-1", publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain(driverId, publicJson, StringComparison.Ordinal);
        Assert.DoesNotContain("101호", publicJson, StringComparison.Ordinal);
    }

    private static 음식주문등록요청 CreateOrderRequest() => new()
    {
        클라이언트요청Id = Guid.NewGuid(),
        음식점Id = 41,
        주문자UserId = "customer-1",
        수령인정보 = new 음식주문수령인정보Dto
        {
            수령인명 = "테스트 주문자",
            연락처 = "010-0000-0000",
            주소 = "서울특별시 중랑구 면목동 면목로 1",
            상세주소 = "101호",
            주문자본인수령여부 = true
        },
        상품목록 =
        [
            new 음식주문상품Dto { 메뉴Id = 10, 상품명 = "비빔밥", 수량 = 1, 단가 = 9000 }
        ]
    };

    private static 운송원장 CreateRecommendedTransport(string offerId, string orderNo, string driverId)
        => new()
        {
            운송번호 = offerId,
            의뢰Id = offerId,
            원본의뢰Id = orderNo,
            원본의뢰유형 = 운송의뢰배차원천유형.음식점주문,
            화주Id = "restaurant-41",
            배차업무유형 = 상태값.배차업무유형.음식배달,
            상태 = 상태값.배차대기상태.대기,
            배차큐단계 = 상태값.배차큐단계.배차추천,
            배차노출상태 = 상태값.배차노출상태.추천중,
            현재추천대상기사Id = driverId,
            추천시작시각 = DateTime.UtcNow,
            추천만료시각 = DateTime.UtcNow.AddMinutes(5),
            추천라운드 = 1,
            픽업_도로명주소 = "서울특별시 중랑구 면목동 면목로 2",
            픽업_상세주소 = "1층",
            픽업_위도 = 37.5801m,
            픽업_경도 = 127.0888m,
            하차_도로명주소 = "서울특별시 중랑구 면목동 면목로 1",
            하차_상세주소 = "101호",
            하차_위도 = 37.5792m,
            하차_경도 = 127.0875m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

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

    private sealed class NoOpRouteService : I배차추천경로Service
    {
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string driverId, 용달기사? driver, DriverLocationSnapshot? currentLocation, 배차추천검색조건? criteria)
            => Task.FromResult<배차경로좌표?>(null);

        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string driverId, 용달기사? driver, DriverLocationSnapshot? currentLocation)
            => Task.FromResult<배차경로좌표?>(null);

        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination)
            => Task.FromResult<배차경로예상결과?>(null);

        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> orderedStops, CancellationToken cancellationToken = default)
            => Task.FromResult<배차경로예상결과?>(null);

        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? routeAnchor, 배차경로좌표? pickup, 배차경로좌표? dropoff)
            => Task.FromResult<배차삽입경로예상결과?>(null);

        public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target) => 0m;
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
            => Task.FromResult<커뮤니티원장Dto?>(null);

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

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestDatabase(SqliteConnection connection, SsalddelContext context)
        {
            _connection = connection;
            Context = context;
        }

        public SsalddelContext Context { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<SsalddelContext>()
                .UseSqlite(connection)
                .Options;
            var context = new SsalddelContext(options, new PassThroughEncryptionService());
            await context.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class PassThroughEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
