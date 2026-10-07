using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;

namespace Ssalddel.Services.Development.FoodObserver;

/// <summary>
/// 격리 검증 DB의 음식 배달 큐만 생산 배차 전환 서비스로 진행합니다.
/// Simulation 호스트에서 운영 Quartz 전체를 켜지 않으면서도 관찰 Runner가 큐 단계를 직접 변경하지 않게 합니다.
/// </summary>
public sealed class 음식배달관찰배차작업자(
    IServiceScopeFactory scopes,
    음식배달관찰검증Runner runner,
    ILogger<음식배달관찰배차작업자> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!runner.IsReady && !stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await 한번실행Async(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "격리 음식 배달 배차 작업자 실행에 실패했습니다.");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    public async Task<int> 한번실행Async(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
        var transition = scope.ServiceProvider.GetRequiredService<I배차대기원장전환Service>();
        var now = DateTime.UtcNow;
        var expired = await db.운송원장.AsNoTracking()
            .Where(item => item.상태 == 상태값.배차대기상태.대기
                           && item.배차업무유형 == 상태값.배차업무유형.음식배달
                           && item.배차큐단계 == 상태값.배차큐단계.배차추천
                           && item.배차노출상태 == 상태값.배차노출상태.추천중
                           && item.추천만료시각.HasValue
                           && item.추천만료시각 <= now)
            .OrderBy(item => item.추천만료시각)
            .ThenBy(item => item.Id)
            .Select(item => item.의뢰Id)
            .Take(20)
            .ToArrayAsync(cancellationToken);
        foreach (var requestId in expired)
        {
            await transition.추천만료처리Async(requestId, cancellationToken);
        }

        var planned = await db.운송원장.AsNoTracking()
            .Where(item => item.상태 == 상태값.배차대기상태.대기
                           && item.배차업무유형 == 상태값.배차업무유형.음식배달
                           && item.배차큐단계 == 상태값.배차큐단계.계획배차
                           && item.배차노출상태 == 상태값.배차노출상태.계획대기)
            .OrderBy(item => item.CreatedAt)
            .Select(item => item.의뢰Id)
            .Take(20)
            .ToArrayAsync(cancellationToken);
        foreach (var requestId in planned)
        {
            await transition.계획배차에서추천으로전환Async(requestId, cancellationToken);
        }

        var waiting = await db.운송원장.AsNoTracking()
            .Where(item => item.상태 == 상태값.배차대기상태.대기
                           && item.배차업무유형 == 상태값.배차업무유형.음식배달
                           && item.배차큐단계 == 상태값.배차큐단계.배차추천
                           && item.현재추천대상기사Id == null
                           && (item.배차노출상태 == 상태값.배차노출상태.추천대기
                               || item.배차노출상태 == 상태값.배차노출상태.추천후보없음))
            .OrderBy(item => item.UpdatedAt)
            .Select(item => item.의뢰Id)
            .Take(20)
            .ToArrayAsync(cancellationToken);
        foreach (var requestId in waiting)
        {
            await transition.추천대기처리Async(requestId, cancellationToken);
        }

        return expired.Length + planned.Length + waiting.Length;
    }
}
