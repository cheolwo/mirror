using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Application.Food;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Services.Food;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Application.WorldProjection;

public interface I운영지역장면조회UseCase
{
    Task<OperationalWorldSceneResponse> 조회Async(
        string areaStableId,
        long cursor,
        CancellationToken cancellationToken);

    Task<OperationalWorldSceneResponse> 조회Async(
        string areaStableId,
        long cursor,
        string schemaVersion,
        CancellationToken cancellationToken);
}

public interface I관찰운영검증ProjectionReader
{
    Task<OperationalWorldSceneItem[]> 지역목록Async(
        string areaStableId,
        DateTime utcNow,
        CancellationToken cancellationToken);
}

public sealed class Empty관찰운영검증ProjectionReader : I관찰운영검증ProjectionReader
{
    public Task<OperationalWorldSceneItem[]> 지역목록Async(
        string areaStableId,
        DateTime utcNow,
        CancellationToken cancellationToken)
        => Task.FromResult(Array.Empty<OperationalWorldSceneItem>());
}

/// <summary>
/// 서로 다른 운영 원장을 변경하지 않고, 현재 사용자에게 허용된 최소 상태 사본만 한 지역 장면으로 조합합니다.
/// 한 자료원의 실패가 다른 자료원의 갱신을 막지 않도록 자료원별로 격리합니다.
/// </summary>
public sealed class 운영지역장면조회UseCase(
    SsalddelContext db,
    ICurrentUserAccessor currentUser,
    I음식배달완료WorldSnapshot조회UseCase foodReader,
    I진행중음식배달WorldProjectionReader activeFoodReader,
    I창고WorldSnapshot조회UseCase warehouseReader,
    I운영WorldAreaResolver areaResolver,
    I관찰운영검증ProjectionReader verificationReader,
    ILogger<운영지역장면조회UseCase> logger) : I운영지역장면조회UseCase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan WarehouseSnapshotLifetime = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan CargoSnapshotLifetime = TimeSpan.FromHours(1);

    public async Task<OperationalWorldSceneResponse> 조회Async(
        string areaStableId,
        long cursor,
        CancellationToken cancellationToken)
        => await 조회Async(
            areaStableId,
            cursor,
            OperationalWorldScenePolicy.SchemaVersionV1,
            cancellationToken);

    public async Task<OperationalWorldSceneResponse> 조회Async(
        string areaStableId,
        long cursor,
        string schemaVersion,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(areaStableId))
            throw new ArgumentException("지역 고유 식별자가 필요합니다.", nameof(areaStableId));
        if (cursor < 0) throw new ArgumentOutOfRangeException(nameof(cursor));
        if (!OperationalWorldScenePolicy.IsSupported(schemaVersion))
            throw new ArgumentException("OperationalWorldSceneSchemaVersionUnsupported", nameof(schemaVersion));

        var area = areaStableId.Trim();
        var now = DateTime.UtcNow;
        var items = new List<OperationalWorldSceneItem>();
        var failures = new List<OperationalWorldSceneSourceFailure>();

        await ReadFoodAsync(area, items, failures, cancellationToken);
        if (string.Equals(schemaVersion, OperationalWorldScenePolicy.SchemaVersionV2, StringComparison.Ordinal))
            await ReadActiveFoodAsync(area, now, items, failures, cancellationToken);
        await ReadWarehousesAsync(area, now, items, failures, cancellationToken);
        await ReadNeighborhoodHubsAsync(area, now, items, failures, cancellationToken);
        await ReadCargoAsync(area, now, items, failures, cancellationToken);
        if (string.Equals(schemaVersion, OperationalWorldScenePolicy.SchemaVersionV2, StringComparison.Ordinal))
            await ReadVerificationSamplesAsync(area, now, items, failures, cancellationToken);

        foreach (var item in items)
            ApplyV2Defaults(item);

        var visibleItems = items
            .Where(item => item.ExpiresAtUtc > now)
            .Where(item => cursor == 0 || item.PublishedAtUtc.Ticks > cursor)
            .OrderBy(item => item.PublishedAtUtc)
            .ThenBy(item => item.SnapshotStableId, StringComparer.Ordinal)
            .ToArray();
        var nextCursor = Math.Max(
            cursor,
            items.Count == 0 ? now.Ticks : items.Max(item => item.PublishedAtUtc.Ticks));

        return new OperationalWorldSceneResponse
        {
            SchemaVersion = schemaVersion,
            AreaStableId = area,
            Cursor = nextCursor,
            IsFullSnapshot = cursor == 0,
            AsOfUtc = now,
            Items = visibleItems,
            SourceFailures = failures.ToArray()
        };
    }

    private async Task ReadVerificationSamplesAsync(
        string area,
        DateTime now,
        ICollection<OperationalWorldSceneItem> items,
        ICollection<OperationalWorldSceneSourceFailure> failures,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var item in await verificationReader.지역목록Async(area, now, cancellationToken))
                items.Add(item);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            RecordFailure("ObservableOperationsVerification", "VerificationProjectionReadFailed", ex, failures);
        }
    }

    private async Task ReadActiveFoodAsync(
        string area,
        DateTime now,
        ICollection<OperationalWorldSceneItem> items,
        ICollection<OperationalWorldSceneSourceFailure> failures,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var item in await activeFoodReader.지역목록Async(area, now, cancellationToken))
                items.Add(item);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            RecordFailure("FoodDeliveryOS.Active", "ActiveProjectionReadFailed", ex, failures);
        }
    }

    private static void ApplyV2Defaults(OperationalWorldSceneItem item)
    {
        if (string.IsNullOrWhiteSpace(item.WorkStableId))
            item.WorkStableId = item.SnapshotStableId;
        if (string.IsNullOrWhiteSpace(item.LifecycleStageCode))
            item.LifecycleStageCode = item.ActivityCode;
        if (string.IsNullOrWhiteSpace(item.AttentionStateCode))
            item.AttentionStateCode = string.Equals(
                item.ItemKind,
                OperationalWorldSceneItemKinds.CompletedLifecycle,
                StringComparison.Ordinal)
                ? OperationalWorldAttentionStateCodes.Completed
                : OperationalWorldAttentionStateCodes.Active;
        if (string.IsNullOrWhiteSpace(item.ObjectKindCode))
            item.ObjectKindCode = item.ItemKind;
        if (string.IsNullOrWhiteSpace(item.SemanticPlaceStableId))
            item.SemanticPlaceStableId = item.OperatingSystemId switch
            {
                OperationalWorldOperatingSystemIds.FoodDelivery => "semantic-place:area:food-delivery",
                OperationalWorldOperatingSystemIds.DomesticCargoTransport => "semantic-place:area:cargo",
                OperationalWorldOperatingSystemIds.WarehouseCommerceFulfillment => "semantic-place:area:warehouse",
                OperationalWorldOperatingSystemIds.SsalddelMartUrbanLogistics => "semantic-place:area:mart",
                _ => "semantic-place:area:operations"
            };
        item.RelationStableIds ??= Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(item.SourceKindCode))
            item.SourceKindCode = OperationalWorldSceneSourceKinds.OperationalProjection;
    }

    private async Task ReadNeighborhoodHubsAsync(
        string area,
        DateTime now,
        ICollection<OperationalWorldSceneItem> items,
        ICollection<OperationalWorldSceneSourceFailure> failures,
        CancellationToken cancellationToken)
    {
        try
        {
            var hubs = await db.생활권물류거점.AsNoTracking()
                .Where(hub => hub.생활권Key == area
                              && (hub.상태Code == 살뜰.도메인.창고.생활권물류거점상태Codes.Pilot
                                  || hub.상태Code == 살뜰.도메인.창고.생활권물류거점상태Codes.Active))
                .OrderBy(hub => hub.StableId)
                .Take(100)
                .Select(hub => new
                {
                    hub.StableId,
                    hub.상태Code,
                    hub.대략위치Label,
                    hub.기사인계가능,
                    hub.주문자수령가능,
                    hub.현재예약건수,
                    hub.최대동시보관건수,
                    hub.Revision,
                    hub.UpdatedAtUtc
                })
                .ToArrayAsync(cancellationToken);

            foreach (var hub in hubs)
            {
                items.Add(new OperationalWorldSceneItem
                {
                    SnapshotStableId = "world-observation:" + hub.StableId,
                    AreaStableId = area,
                    OperatingSystemId = OperatingSystemIds.SsalddelMartUrbanLogistics,
                    ItemKind = OperationalWorldSceneItemKinds.WarehouseActor,
                    RoleCode = "NeighborhoodMicroHub",
                    ActivityCode = hub.현재예약건수 >= hub.최대동시보관건수 ? "CapacityFull" : hub.상태Code,
                    Revision = hub.Revision,
                    OccurredAtUtc = hub.UpdatedAtUtc,
                    PublishedAtUtc = now,
                    ExpiresAtUtc = now.Add(WarehouseSnapshotLifetime),
                    RepresentationDataJson = JsonSerializer.Serialize(new
                    {
                        hub.StableId,
                        hub.대략위치Label,
                        hub.기사인계가능,
                        hub.주문자수령가능,
                        hub.현재예약건수,
                        hub.최대동시보관건수,
                        exactAddressIncluded = false,
                        observationPresentationOnly = true
                    }, JsonOptions)
                });
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            RecordFailure(OperatingSystemIds.SsalddelMartUrbanLogistics, "NeighborhoodHubSourceReadFailed", ex, failures);
        }
    }

    private async Task ReadFoodAsync(
        string area,
        ICollection<OperationalWorldSceneItem> items,
        ICollection<OperationalWorldSceneSourceFailure> failures,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await foodReader.지역목록Async(area, 200, cancellationToken);
            foreach (var source in response.Items)
            {
                items.Add(new OperationalWorldSceneItem
                {
                    SnapshotStableId = source.SnapshotStableId,
                    AreaStableId = source.AreaStableId,
                    OperatingSystemId = OperatingSystemIds.FoodDelivery,
                    ItemKind = OperationalWorldSceneItemKinds.CompletedLifecycle,
                    RoleCode = "FoodDeliveryTeam",
                    ActivityCode = source.OutcomeCode,
                    Revision = source.LifecycleRevision,
                    OccurredAtUtc = source.CompletedAtUtc,
                    PublishedAtUtc = source.PublishedAtUtc,
                    ExpiresAtUtc = source.ExpiresAtUtc,
                    RepresentationDataJson = JsonSerializer.Serialize(new
                    {
                        source.Actors,
                        source.Milestones
                    }, JsonOptions)
                });
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            RecordFailure("FoodDeliveryOS", "SourceReadFailed", ex, failures);
        }
    }

    private async Task ReadWarehousesAsync(
        string area,
        DateTime now,
        ICollection<OperationalWorldSceneItem> items,
        ICollection<OperationalWorldSceneSourceFailure> failures,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = currentUser.UserId?.Trim();
            if (string.IsNullOrWhiteSpace(userId)) return;
            var warehouseQuery = db.창고.AsNoTracking().Where(warehouse => warehouse.IsActive);
            if (!string.Equals(currentUser.Role, 역할명.서버관리자, StringComparison.OrdinalIgnoreCase))
            {
                warehouseQuery = warehouseQuery.Where(warehouse =>
                    warehouse.소유자UserId == userId
                    || db.창고사용자.Any(link => link.창고Id == warehouse.Id && link.UserId == userId));
            }

            var warehouses = await warehouseQuery
                .OrderBy(warehouse => warehouse.Id)
                .Take(20)
                .Select(warehouse => new { warehouse.Id, warehouse.주소 })
                .ToArrayAsync(cancellationToken);

            foreach (var warehouse in warehouses.Where(warehouse =>
                         areaResolver.ResolveAddresses(warehouse.주소) == area))
            {
                var snapshot = await warehouseReader.조회Async(warehouse.Id, cancellationToken);
                if (snapshot.IsFailed)
                {
                    failures.Add(new OperationalWorldSceneSourceFailure
                    {
                        SourceCode = "WarehouseCommerceFulfillmentOS",
                        ErrorCode = "SnapshotUnavailable",
                        Retryable = true
                    });
                    continue;
                }

                var publishedAt = now;
                var expiresAt = now.Add(WarehouseSnapshotLifetime);
                foreach (var task in snapshot.Value.Tasks)
                {
                    items.Add(new OperationalWorldSceneItem
                    {
                        SnapshotStableId = task.StableId,
                        AreaStableId = area,
                        OperatingSystemId = OperatingSystemIds.WarehouseCommerceFulfillment,
                        ItemKind = OperationalWorldSceneItemKinds.WarehouseTask,
                        RoleCode = task.TaskKind == "PutAway" ? "DockWorker" : "Picker",
                        ActivityCode = task.TaskKind,
                        Revision = task.UpdatedAtUtc.UtcTicks,
                        OccurredAtUtc = task.UpdatedAtUtc.UtcDateTime,
                        PublishedAtUtc = publishedAt,
                        ExpiresAtUtc = expiresAt,
                        RepresentationDataJson = JsonSerializer.Serialize(new
                        {
                            task.WarehouseStableId,
                            task.InventoryItemStableId,
                            task.TaskKind,
                            task.Quantity,
                            task.LocationCode,
                            task.Status,
                            task.CanExecute
                        }, JsonOptions)
                    });
                }

                foreach (var npc in snapshot.Value.Npcs)
                {
                    items.Add(new OperationalWorldSceneItem
                    {
                        SnapshotStableId = npc.StableId,
                        AreaStableId = area,
                        OperatingSystemId = OperatingSystemIds.WarehouseCommerceFulfillment,
                        ItemKind = OperationalWorldSceneItemKinds.WarehouseActor,
                        RoleCode = npc.RoleCode,
                        ActivityCode = npc.ActivityCode,
                        Revision = RevisionFromText(snapshot.Value.Revision + ":" + npc.StableId),
                        OccurredAtUtc = now,
                        PublishedAtUtc = publishedAt,
                        ExpiresAtUtc = expiresAt,
                        RepresentationDataJson = JsonSerializer.Serialize(new
                        {
                            npc.WarehouseStableId,
                            npc.SourceTaskStableId,
                            npc.RouteCode,
                            npc.CurrentWaypointKey,
                            npc.DestinationWaypointKey
                        }, JsonOptions)
                    });
                }

                foreach (var handoff in snapshot.Value.InboundHandoffs)
                {
                    items.Add(new OperationalWorldSceneItem
                    {
                        SnapshotStableId = handoff.StableId,
                        AreaStableId = area,
                        OperatingSystemId = OperatingSystemIds.DomesticCargoTransport,
                        ItemKind = OperationalWorldSceneItemKinds.CargoHandoff,
                        RoleCode = "CargoWarehouseHandoff",
                        ActivityCode = handoff.HandoffStateCode,
                        Revision = handoff.Revision,
                        OccurredAtUtc = handoff.GeneratedAt.UtcDateTime,
                        PublishedAtUtc = publishedAt,
                        ExpiresAtUtc = expiresAt,
                        RepresentationDataJson = JsonSerializer.Serialize(new
                        {
                            handoff.CargoStableId,
                            handoff.TransportTaskStableId,
                            handoff.InboundTaskStableId,
                            handoff.HandoffStateCode,
                            handoff.Movements
                        }, JsonOptions)
                    });
                }
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            RecordFailure("WarehouseCommerceFulfillmentOS", "SourceReadFailed", ex, failures);
        }
    }

    private async Task ReadCargoAsync(
        string area,
        DateTime now,
        ICollection<OperationalWorldSceneItem> items,
        ICollection<OperationalWorldSceneSourceFailure> failures,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = currentUser.UserId?.Trim();
            if (string.IsNullOrWhiteSpace(userId)) return;
            var cutoff = now.Subtract(CargoSnapshotLifetime);
            var query = db.운송원장.AsNoTracking().Where(transport =>
                transport.배차업무유형 == 상태값.배차업무유형.용달운송
                && transport.상태 == 기사운송상태코드.인수완료
                && transport.UpdatedAt > cutoff);
            if (!string.Equals(currentUser.Role, 역할명.서버관리자, StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(transport =>
                    transport.화주Id == userId
                    || transport.기사_운송자 == userId
                    || transport.확정기사Id == userId);
            }

            var transports = await query.OrderByDescending(x => x.UpdatedAt).Take(100).ToArrayAsync(cancellationToken);
            foreach (var transport in transports.Where(transport =>
                         areaResolver.ResolveAddresses(transport.픽업_도로명주소, transport.하차_도로명주소) == area))
            {
                var requestId = !string.IsNullOrWhiteSpace(transport.의뢰Id)
                    ? transport.의뢰Id
                    : transport.운송번호;
                var events = await db.운송이벤트.AsNoTracking()
                    .Where(entry => entry.의뢰Id == requestId
                                    && entry.이벤트타입 == 운송이벤트유형.기사운송상태변경)
                    .OrderBy(entry => entry.이벤트시각)
                    .ThenBy(entry => entry.Id)
                    .ToArrayAsync(cancellationToken);
                if (!TryBuildCargoItem(transport, events, area, now, out var item))
                {
                    failures.Add(new OperationalWorldSceneSourceFailure
                    {
                        SourceCode = OperatingSystemIds.DomesticCargoTransport,
                        ErrorCode = "CompletionProofInvalid",
                        Retryable = false
                    });
                    continue;
                }

                items.Add(item);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            RecordFailure(OperatingSystemIds.DomesticCargoTransport, "SourceReadFailed", ex, failures);
        }
    }

    private static bool TryBuildCargoItem(
        운송원장 transport,
        IReadOnlyList<운송이벤트> events,
        string area,
        DateTime now,
        out OperationalWorldSceneItem item)
    {
        item = null!;
        var requiredStages = new[]
        {
            기사운송상태코드.상차지도착,
            기사운송상태코드.상차완료,
            기사운송상태코드.하차지도착,
            기사운송상태코드.인수완료
        };
        var selected = new List<(string Stage, DateTime At, string Token)>();
        var searchFrom = 0;
        foreach (var required in requiredStages)
        {
            var found = -1;
            string token = string.Empty;
            for (var index = searchFrom; index < events.Count; index++)
            {
                if (!TryReadTransportEvent(events[index].메타데이터, out var target, out var candidateToken)
                    || !string.Equals(target, required, StringComparison.Ordinal)) continue;
                found = index;
                token = candidateToken;
                break;
            }
            if (found < 0) return false;
            selected.Add((required, events[found].이벤트시각, token));
            searchFrom = found + 1;
        }

        var completionToken = selected[^1].Token;
        if (string.IsNullOrWhiteSpace(completionToken)) return false;
        var proof = new 운영업무완료증명
        {
            OperatingSystemId = OperatingSystemIds.DomesticCargoTransport,
            WorkStableId = "cargo-work:" + completionToken,
            Revision = selected.Count,
            OutcomeCode = "CargoOperationalDeliveryCompleted",
            CompletedAtUtc = selected[^1].At,
            Stages = selected.Select((stage, index) => new 운영업무완료단계
            {
                Sequence = index + 1,
                StageCode = stage.Stage,
                OccurredAtUtc = stage.At
            }).ToArray()
        };
        var errors = 운영업무완료증명Validator.Validate(proof, new 운영업무완료프로필
        {
            OperatingSystemId = OperatingSystemIds.DomesticCargoTransport,
            OutcomeCode = "CargoOperationalDeliveryCompleted",
            RequiredStageCodes = requiredStages
        });
        if (errors.Length > 0) return false;

        var publishedAt = selected[^1].At;
        item = new OperationalWorldSceneItem
        {
            SnapshotStableId = "cargo-completed:" + completionToken,
            AreaStableId = area,
            OperatingSystemId = OperatingSystemIds.DomesticCargoTransport,
            ItemKind = OperationalWorldSceneItemKinds.CompletedLifecycle,
            RoleCode = "CargoDeliveryTeam",
            ActivityCode = proof.OutcomeCode,
            Revision = proof.Revision,
            OccurredAtUtc = proof.CompletedAtUtc,
            PublishedAtUtc = publishedAt,
            ExpiresAtUtc = publishedAt.Add(CargoSnapshotLifetime),
            RepresentationDataJson = JsonSerializer.Serialize(new
            {
                shipperActorStableId = "actor:synthetic:" + completionToken + ":shipper",
                driverActorStableId = "actor:synthetic:" + completionToken + ":driver",
                proof.Stages,
                financialClosureIncluded = false
            }, JsonOptions)
        };
        return item.ExpiresAtUtc > now;
    }

    private static bool TryReadTransportEvent(string json, out string targetState, out string token)
    {
        targetState = string.Empty;
        token = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("targetState", out var target)) return false;
            targetState = target.GetString() ?? string.Empty;
            if (document.RootElement.TryGetProperty("completionProjectionToken", out var tokenElement))
                token = tokenElement.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(targetState);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void RecordFailure(
        string source,
        string errorCode,
        Exception exception,
        ICollection<OperationalWorldSceneSourceFailure> failures)
    {
        logger.LogWarning(exception, "운영 지역 장면 자료원 조회 실패. Source={Source}", source);
        failures.Add(new OperationalWorldSceneSourceFailure
        {
            SourceCode = source,
            ErrorCode = errorCode,
            Retryable = true
        });
    }

    private static long RevisionFromText(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return BitConverter.ToInt64(hash, 0) & long.MaxValue;
    }
}
