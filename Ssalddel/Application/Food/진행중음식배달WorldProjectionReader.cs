using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Data;

namespace Ssalddel.Application.Food;

public interface I진행중음식배달WorldProjectionReader
{
    Task<OperationalWorldSceneItem[]> 지역목록Async(
        string areaStableId,
        DateTime utcNow,
        CancellationToken cancellationToken);
}

public static class 진행중음식배달WorldProjectionPolicy
{
    public const string SchemaVersion = "food-delivery-active-observation.v1";
    public const string PseudonymizationKeyConfigurationPath =
        "OperationalWorldProjection:PseudonymizationKey";
    public const string ReadinessCode = "ObservationPresentationOnly";
    public static readonly TimeSpan ActiveLookback = TimeSpan.FromHours(2);
    public static readonly TimeSpan RefreshLifetime = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan TombstoneRetention = TimeSpan.FromMinutes(15);
}

/// <summary>
/// 음식 주문 RDB 정본을 변경하지 않고, 진행 단계만 가명·일반화 위치로 투영합니다.
/// 상세 주소와 주문·사용자·기사 식별자는 지역 판정과 가명 생성 뒤 응답에 포함하지 않습니다.
/// </summary>
public sealed class 진행중음식배달WorldProjectionReader(
    SsalddelContext db,
    I운영WorldAreaResolver areaResolver,
    IConfiguration configuration) : I진행중음식배달WorldProjectionReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OperationalWorldSceneItem[]> 지역목록Async(
        string areaStableId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var area = string.IsNullOrWhiteSpace(areaStableId)
            ? throw new ArgumentException("지역 고유 식별자가 필요합니다.", nameof(areaStableId))
            : areaStableId.Trim();
        var key = RequirePseudonymizationKey(configuration);
        var cutoff = utcNow - 진행중음식배달WorldProjectionPolicy.ActiveLookback;
        var terminalCutoff = utcNow - 진행중음식배달WorldProjectionPolicy.TombstoneRetention;

        var orders = await db.음식주문
            .AsNoTracking()
            .Include(order => order.상태이력)
            // 주소는 persistence 경계에서 보호되므로 SQL의 평문 Contains로 지역을 거르지 않습니다.
            // 첫 절편은 최근 변경 500건으로 제한하고 복호화 뒤 보수적 지역 판정을 적용합니다.
            .Where(order => order.UpdatedAt >= cutoff)
            .OrderByDescending(order => order.UpdatedAt)
            .Take(500)
            .ToArrayAsync(cancellationToken);

        var result = new List<OperationalWorldSceneItem>();
        foreach (var order in orders)
        {
            if (!string.Equals(
                    areaResolver.ResolveAddresses(order.음식점주소, order.수령지주소),
                    area,
                    StringComparison.Ordinal))
                continue;

            var stage = 음식주문상태코드.Normalize(order.상태);
            if (stage == 음식주문상태코드.수령확인)
                continue;

            var terminal = stage is 음식주문상태코드.거절 or 음식주문상태코드.취소;
            if (terminal && order.UpdatedAt < terminalCutoff)
                continue;

            var revision = Math.Max(1, order.상태이력.LongCount());
            var occurredAt = order.상태이력
                .OrderByDescending(history => history.전이시각Utc)
                .ThenByDescending(history => history.Id)
                .Select(history => history.전이시각Utc)
                .FirstOrDefault();
            if (occurredAt == default)
                occurredAt = order.UpdatedAt;

            var pseudonym = Pseudonym(key, order.Id, order.주문번호, order.CreatedAt);
            var semanticPlace = SemanticPlace(stage);
            var representation = new
            {
                schemaVersion = 진행중음식배달WorldProjectionPolicy.SchemaVersion,
                displayStageCode = stage,
                semanticMovementCode = Movement(stage),
                readinessCode = 진행중음식배달WorldProjectionPolicy.ReadinessCode,
                distributionApproved = false,
                exactLocationIncluded = false,
                personalDataIncluded = false,
                observationPresentationOnly = true
            };
            var representationJson = JsonSerializer.Serialize(representation, JsonOptions);
            var projectionHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"{진행중음식배달WorldProjectionPolicy.SchemaVersion}|{area}|{pseudonym}|{revision}|{stage}|{semanticPlace}|{terminal}|{representationJson}")));

            result.Add(new OperationalWorldSceneItem
            {
                SnapshotStableId = $"food-delivery-active:{pseudonym}",
                AreaStableId = area,
                OperatingSystemId = OperationalWorldOperatingSystemIds.FoodDelivery,
                ItemKind = OperationalWorldSceneItemKinds.ActiveLifecycle,
                RoleCode = Role(stage),
                ActivityCode = stage,
                Revision = revision,
                OccurredAtUtc = occurredAt,
                PublishedAtUtc = order.UpdatedAt,
                ExpiresAtUtc = terminal
                    ? order.UpdatedAt.Add(진행중음식배달WorldProjectionPolicy.TombstoneRetention)
                    : utcNow.Add(진행중음식배달WorldProjectionPolicy.RefreshLifetime),
                IsTombstone = terminal,
                LocalStorageAllowed = false,
                ReplayAllowed = false,
                RepresentationDataJson = JsonSerializer.Serialize(new
                {
                    representation.schemaVersion,
                    representation.displayStageCode,
                    representation.semanticMovementCode,
                    representation.readinessCode,
                    representation.distributionApproved,
                    representation.exactLocationIncluded,
                    representation.personalDataIncluded,
                    representation.observationPresentationOnly,
                    projectionHashSha256 = projectionHash
                }, JsonOptions),
                WorkStableId = $"food-delivery-work:{pseudonym}",
                LifecycleStageCode = stage,
                AttentionStateCode = terminal
                    ? OperationalWorldAttentionStateCodes.RecoveryPending
                    : OperationalWorldAttentionStateCodes.Active,
                ObjectKindCode = "FoodDeliveryWork",
                SemanticPlaceStableId = semanticPlace,
                RelationStableIds = Array.Empty<string>(),
                SourceKindCode = OperationalWorldSceneSourceKinds.OperationalProjection
            });
        }

        return result
            .OrderBy(item => item.PublishedAtUtc)
            .ThenBy(item => item.SnapshotStableId, StringComparer.Ordinal)
            .ToArray();
    }

    private static byte[] RequirePseudonymizationKey(IConfiguration configuration)
    {
        var value = configuration[진행중음식배달WorldProjectionPolicy.PseudonymizationKeyConfigurationPath];
        if (string.IsNullOrWhiteSpace(value) || Encoding.UTF8.GetByteCount(value) < 32)
            throw new InvalidOperationException("OperationalWorldProjectionPseudonymizationKeyUnavailable");
        return Encoding.UTF8.GetBytes(value);
    }

    private static string Pseudonym(byte[] key, long id, string orderNumber, DateTime createdAt)
    {
        using var hmac = new HMACSHA256(key);
        return Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(
            $"food-delivery|{id}|{orderNumber}|{createdAt.Ticks}")));
    }

    private static string Role(string stage)
        => stage switch
        {
            음식주문상태코드.주문대기 or 음식주문상태코드.조리중 or 음식주문상태코드.픽업대기
                => "RestaurantWorker",
            음식주문상태코드.기사배정 or 음식주문상태코드.픽업완료 => "DeliveryDriver",
            음식주문상태코드.전달완료 => "OrderRecipient",
            _ => "FoodDeliveryTeam"
        };

    private static string SemanticPlace(string stage)
        => stage switch
        {
            음식주문상태코드.주문대기 or 음식주문상태코드.조리중 or 음식주문상태코드.픽업대기
                or 음식주문상태코드.기사배정 => "semantic-place:area:food-restaurant",
            음식주문상태코드.픽업완료 => "semantic-place:area:food-delivery-route",
            음식주문상태코드.전달완료 => "semantic-place:area:food-recipient-zone",
            _ => "semantic-place:area:food-delivery"
        };

    private static string Movement(string stage)
        => stage switch
        {
            음식주문상태코드.기사배정 => "ApproachingPickup",
            음식주문상태코드.픽업완료 => "Delivering",
            음식주문상태코드.전달완료 => "AwaitingReceiptConfirmation",
            음식주문상태코드.거절 or 음식주문상태코드.취소 => "Removed",
            _ => "Stationary"
        };
}
