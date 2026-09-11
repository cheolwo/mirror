using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.ApiMetadata;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Data;
using 살뜰.Services.Versioning;

namespace Ssalddel.Application.Food;

public interface I음식배달완료WorldSnapshot조회UseCase
{
    Task<음식배달완료WorldSnapshot목록응답> 지역목록Async(
        string areaStableId,
        int take,
        CancellationToken cancellationToken);
}

[SsalddelApiWorkflow(SsalddelWorkflow.FoodDelivery)]
[SsalddelUseCase(
    "음식 배달 완료 World 상태 사본 지역 조회",
    Summary = "FoodDeliveryOS의 검증된 정상 완료 주기를 개인정보 없는 온라인 전용 지역 장면 목록으로 제공합니다.")]
public sealed class 음식배달완료WorldSnapshot조회UseCase(SsalddelContext db)
    : I음식배달완료WorldSnapshot조회UseCase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<음식배달완료WorldSnapshot목록응답> 지역목록Async(
        string areaStableId,
        int take,
        CancellationToken cancellationToken)
    {
        var normalizedArea = string.IsNullOrWhiteSpace(areaStableId)
            ? throw new ArgumentException("지역 고유 식별자가 필요합니다.", nameof(areaStableId))
            : areaStableId.Trim();
        var now = DateTime.UtcNow;
        var rows = await db.음식배달완료WorldSnapshot
            .AsNoTracking()
            .Where(x => x.AreaStableId == normalizedArea && x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.PublishedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);

        return new 음식배달완료WorldSnapshot목록응답
        {
            AreaStableId = normalizedArea,
            AsOfUtc = now,
            Items = rows.Select(ToContract).ToArray()
        };
    }

    private static Ssalddel.WorkflowRules.Contracts.음식배달완료WorldSnapshot ToContract(
        살뜰.도메인.음식.음식배달완료WorldSnapshot row)
        => new()
        {
            SnapshotStableId = row.SnapshotStableId,
            AreaStableId = row.AreaStableId,
            LifecycleRevision = row.LifecycleRevision,
            OutcomeCode = row.OutcomeCode,
            CompletedAtUtc = row.CompletedAtUtc,
            PublishedAtUtc = row.PublishedAtUtc,
            ExpiresAtUtc = row.ExpiresAtUtc,
            DataPolicyCode = 음식배달완료WorldSnapshot정책.OnlineEphemeral,
            LocalStorageAllowed = false,
            ReplayAllowed = false,
            Actors = new 음식배달완료WorldActorRefs
            {
                OrdererActorStableId = row.OrdererActorStableId,
                RestaurantActorStableId = row.RestaurantActorStableId,
                DriverActorStableId = row.DriverActorStableId
            },
            Milestones = JsonSerializer.Deserialize<음식배달완료WorldMilestone[]>(
                row.MilestonesJson,
                JsonOptions) ?? Array.Empty<음식배달완료WorldMilestone>()
        };
}
