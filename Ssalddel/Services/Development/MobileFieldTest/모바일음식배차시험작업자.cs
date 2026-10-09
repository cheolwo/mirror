using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.도메인.공통;

namespace Ssalddel.Services.Development.MobileFieldTest;

/// <summary>허용된 시험 음식 원장만 기존 전환 코어로 진행한다. 자동 수락/조리/픽업/전달은 하지 않는다.</summary>
public sealed class 모바일음식배차시험작업자(
    IServiceScopeFactory scopes, 모바일음식배차시험Options options, 모바일음식배차시험Guard guard,
    모바일음식배차시험자료 fixtures, ILogger<모바일음식배차시험작업자> logger) : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        guard.EnsureEnabled();
        using (var scope = scopes.CreateScope())
            await scope.ServiceProvider.GetRequiredService<모바일음식배차시험준비Service>().PrepareAsync(cancellationToken);
        logger.LogInformation("모바일 음식 배차 시험 준비 완료. Fixture={Fixture} IsEstimated=true ActualMapRequest=false",
            모바일음식배차시험자료.FixtureRevision);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await 한번실행Async(stoppingToken); }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("모바일 음식 배차 시험 스캔 실패. ErrorType={ErrorType}", exception.GetType().Name);
            }
            await Task.Delay(TimeSpan.FromSeconds(options.PollIntervalSeconds), stoppingToken);
        }
    }

    public async Task<int> 한번실행Async(CancellationToken ct)
    {
        guard.EnsureEnabled();
        if (!fixtures.IsReady) throw new InvalidOperationException("MobileFoodDispatchFixtureNotReady");
        using var scope = scopes.CreateScope();
        var services = scope.ServiceProvider;
        if (options.RefreshSyntheticDriverLocation)
            await services.GetRequiredService<모바일음식배차시험준비Service>().RefreshLocationAsync(ct);
        var db = services.GetRequiredService<SsalddelContext>();
        var transition = services.GetRequiredService<I배차대기원장전환Service>();
        var now = DateTime.UtcNow;
        // 계획/만료/재탐색을 한 번만 처리한다. 비시험 건으로 batch가 가득 차는 것도 방지한다.
        var orderer = fixtures.Accounts!.OrdererId;
        var candidates = await (from queue in db.운송원장.AsNoTracking()
                                join order in db.음식주문.AsNoTracking() on queue.원본의뢰Id equals order.주문번호
                                where queue.상태 == 상태값.배차대기상태.대기
                                      && queue.배차업무유형 == 상태값.배차업무유형.음식배달
                                      && queue.원본의뢰유형 == 살뜰.Services.Dispatch.Engine.운송의뢰배차원천유형.음식점주문
                                      && order.주문자UserId == orderer && order.음식점Id == 101
                                      && order.수령지주소 == 모바일음식배차시험자료.DropoffAddress
                                      && order.음식점명 == 모바일음식배차시험자료.RestaurantName
                                      && order.음식점주소 == 모바일음식배차시험자료.PickupAddress
                                      && ((order.음식점위도 == null && order.음식점경도 == null)
                                          || (order.음식점위도 == 37.580450m && order.음식점경도 == 127.087950m))
                                      && !order.모의처리처수행
                                      && (order.상태 == "주문확인" || order.상태 == "조리중"
                                          || order.상태 == "픽업대기" || order.상태 == "기사배정")
                                      && queue.원본의뢰Id == queue.의뢰Id && queue.화주Id == "restaurant:101"
                                      && queue.픽업_도로명주소 == 모바일음식배차시험자료.PickupAddress
                                      && queue.하차_도로명주소 == 모바일음식배차시험자료.DropoffAddress
                                      && queue.픽업_위도 == 37.580450m && queue.픽업_경도 == 127.087950m
                                      && queue.하차_위도 == 37.582000m && queue.하차_경도 == 127.090000m
                                      && ((queue.배차큐단계 == 상태값.배차큐단계.계획배차
                                           && queue.배차노출상태 == 상태값.배차노출상태.계획대기)
                                          || (queue.배차큐단계 == 상태값.배차큐단계.배차추천
                                              && ((queue.배차노출상태 == 상태값.배차노출상태.추천중
                                                   && queue.추천만료시각 <= now)
                                                  || (queue.현재추천대상기사Id == null
                                                      && (queue.배차노출상태 == 상태값.배차노출상태.추천대기
                                                          || queue.배차노출상태 == 상태값.배차노출상태.추천후보없음)))))
                                orderby queue.UpdatedAt, queue.Id
                                select queue).Take(options.BatchSize).ToArrayAsync(ct);
        var processed = 0;
        foreach (var queue in candidates)
        {
            if (!await fixtures.IsFixtureQueueAsync(db, queue, ct)
                || (!string.IsNullOrWhiteSpace(queue.현재추천대상기사Id)
                    && queue.현재추천대상기사Id != fixtures.Accounts.DriverId)) continue;
            if (queue.배차노출상태 == 상태값.배차노출상태.추천중)
                await transition.추천만료처리Async(queue.의뢰Id, ct);
            else if (queue.배차큐단계 == 상태값.배차큐단계.계획배차)
                await transition.계획배차에서추천으로전환Async(queue.의뢰Id, ct);
            else
                await transition.추천대기처리Async(queue.의뢰Id, ct);
            processed++;
        }
        return processed;
    }
}
