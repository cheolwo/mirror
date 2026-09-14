using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.DeliveryZones;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Services.WorldProjection.AdministrativeDongDiorama;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Data;
using 살뜰.도메인.배달권;

namespace 살뜰.Services.DeliveryZones;

public interface I배달운영권역관리Service
{
    Task<IReadOnlyList<행정동운영ModuleDto>> 행정동Module목록Async(
        string sourceScopeStableId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<배달운영권역Dto>> 목록Async(CancellationToken cancellationToken = default);

    Task<배달운영권역Dto?> 조회Async(
        string deliveryTerritoryStableId,
        CancellationToken cancellationToken = default);

    Task<배달운영권역Dto> Draft생성Async(
        배달운영권역Draft생성Request request,
        string actorUserStableId,
        CancellationToken cancellationToken = default);

    Task<배달운영권역Dto> 행정동교체Async(
        string deliveryTerritoryStableId,
        배달운영권역행정동교체Request request,
        string actorUserStableId,
        CancellationToken cancellationToken = default);
}

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.PlatformDeliveryZoneLedger,
    SsalddelCodeLayer.Application,
    "공식 행정동 셀을 관리자가 Draft 배달운영권역으로 묶고 revision·멱등성·Outbox를 보존한다.",
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    ContractType = typeof(I배달운영권역관리Service),
    FlowOrder = 32,
    Boundary = "운영 활성, 주문 노출, 배차, 정산, 협력권역 또는 Unity 상태를 변경하지 않는다.")]
public sealed class 배달운영권역관리Service(
    SsalddelContext db,
    I행정동배달운영권역Source source,
    I행정동디오라마ProjectionStore dioramaStore) : I배달운영권역관리Service
{
    private const string CreateOperation = "CreateDraft";
    private const string ReplaceAdministrativeDongsOperation = "ReplaceAdministrativeDongs";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<행정동운영ModuleDto>> 행정동Module목록Async(
        string sourceScopeStableId,
        CancellationToken cancellationToken = default)
    {
        var normalizedScope = NormalizeSourceScope(sourceScopeStableId);
        var candidates = await source.조회Async(normalizedScope, cancellationToken);
        var assignments = await db.배달운영권역행정동Memberships
            .AsNoTracking()
            .Include(x => x.배달운영권역)
            .Where(x => x.현행행정동유일성Key != null)
            .ToDictionaryAsync(x => x.행정동고유식별자, StringComparer.Ordinal, cancellationToken);

        var tasks = candidates.Select(async candidate =>
        {
            var manifest = await dioramaStore.FindManifestAsync(
                candidate.AdministrativeAreaStableId,
                cancellationToken);
            assignments.TryGetValue(candidate.AdministrativeAreaStableId, out var assignment);
            return new 행정동운영ModuleDto
            {
                SourceScopeStableId = normalizedScope,
                AdministrativeAreaStableId = candidate.AdministrativeAreaStableId,
                DisplayName = candidate.DisplayName,
                LegalAreas = candidate.LegalAreas.Select(ToLegalAreaDto).ToArray(),
                JurisdictionSourceId = candidate.SourceId,
                JurisdictionDatasetId = candidate.DatasetId,
                JurisdictionSourceVersion = candidate.SourceVersion,
                JurisdictionDataRevision = candidate.DataRevision,
                JurisdictionEvidenceAsOfUtc = candidate.EvidenceAsOfUtc,
                ModuleProfileStatusCode = 행정동운영Module상태Codes.ProfileRegistered,
                JurisdictionReadinessCode = 행정동운영Module상태Codes.OfficialJurisdictionConfirmed,
                DioramaReadinessCode = manifest?.ReadinessCode
                                       ?? 행정동운영Module상태Codes.WaitingForSpatialProjection,
                DioramaSourceVintage = manifest?.SourceVintage,
                DioramaProjectionHashSha256 = manifest?.ProjectionHashSha256,
                ObservationPresentationOnly = manifest?.ObservationPresentationOnly ?? true,
                DistributionApproved = manifest?.DistributionApproved ?? false,
                AssignedDeliveryTerritoryStableId = assignment?.배달운영권역.권역고유식별자,
                AssignedDeliveryTerritoryDisplayName = assignment?.배달운영권역.표시명
            };
        });
        return (await Task.WhenAll(tasks))
            .OrderBy(x => x.AdministrativeAreaStableId, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<IReadOnlyList<배달운영권역Dto>> 목록Async(
        CancellationToken cancellationToken = default)
        => (await db.배달운영권역
                .AsNoTracking()
                .Include(x => x.행정동Memberships)
                .OrderBy(x => x.권역고유식별자)
                .ToListAsync(cancellationToken))
            .Select(ToDto)
            .ToArray();

    public async Task<배달운영권역Dto?> 조회Async(
        string deliveryTerritoryStableId,
        CancellationToken cancellationToken = default)
    {
        var stableId = NormalizeTerritoryStableId(deliveryTerritoryStableId);
        var entity = await FindTerritoryAsync(stableId, false, cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<배달운영권역Dto> Draft생성Async(
        배달운영권역Draft생성Request request,
        string actorUserStableId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stableId = NormalizeTerritoryStableId(request.DeliveryTerritoryStableId);
        var displayName = NormalizeRequired(request.DisplayName, nameof(request.DisplayName), 160);
        var sourceScope = NormalizeSourceScope(request.SourceScopeStableId);
        var administrativeIds = NormalizeAdministrativeIds(
            request.AdministrativeAreaStableIds,
            allowEmpty: false);
        var clientRequestId = NormalizeRequired(request.ClientRequestId, nameof(request.ClientRequestId), 120);
        var actor = NormalizeRequired(actorUserStableId, nameof(actorUserStableId), 160);
        var requestHash = HashRequest(
            CreateOperation,
            stableId,
            displayName,
            sourceScope,
            expectedRevision: 0,
            administrativeIds,
            actor);

        var replay = await TryReplayAsync(
            clientRequestId,
            CreateOperation,
            requestHash,
            cancellationToken);
        if (replay is not null)
            return replay;

        if (await db.배달운영권역.AnyAsync(
                x => x.권역고유식별자 == stableId,
                cancellationToken))
            throw new 배달운영권역ConflictException("DeliveryTerritoryStableIdAlreadyExists");

        var candidates = await ResolveCandidatesAsync(sourceScope, administrativeIds, cancellationToken);
        await EnsureUnassignedAsync(stableId, administrativeIds, cancellationToken);

        var now = DateTime.UtcNow;
        var territory = new 배달운영권역
        {
            권역고유식별자 = stableId,
            표시명 = displayName,
            SourceScopeStableId = sourceScope,
            상태Code = 배달운영권역상태Codes.Draft,
            Revision = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        foreach (var candidate in candidates)
            territory.행정동Memberships.Add(CreateMembership(candidate, now));
        var result = ToDto(territory);

        db.배달운영권역.Add(territory);
        db.배달운영권역CommandReceipts.Add(new 배달운영권역CommandReceipt
        {
            ClientRequestId = clientRequestId,
            OperationCode = CreateOperation,
            RequestHashSha256 = requestHash,
            ActorUserStableId = actor,
            배달운영권역고유식별자 = stableId,
            ResultRevision = territory.Revision,
            ResultJson = JsonSerializer.Serialize(result, JsonOptions),
            CreatedAtUtc = now
        });
        db.배달운영권역변경Outbox.Add(CreateOutbox(
            territory,
            "DeliveryOperatingTerritoryDraftCreated",
            administrativeIds,
            actor,
            now));

        await SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<배달운영권역Dto> 행정동교체Async(
        string deliveryTerritoryStableId,
        배달운영권역행정동교체Request request,
        string actorUserStableId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stableId = NormalizeTerritoryStableId(deliveryTerritoryStableId);
        var sourceScope = NormalizeSourceScope(request.SourceScopeStableId);
        var administrativeIds = NormalizeAdministrativeIds(
            request.AdministrativeAreaStableIds,
            allowEmpty: true);
        var clientRequestId = NormalizeRequired(request.ClientRequestId, nameof(request.ClientRequestId), 120);
        var actor = NormalizeRequired(actorUserStableId, nameof(actorUserStableId), 160);
        if (request.ExpectedRevision < 1)
            throw new ArgumentException("DeliveryTerritoryExpectedRevisionInvalid", nameof(request));
        var requestHash = HashRequest(
            ReplaceAdministrativeDongsOperation,
            stableId,
            displayName: string.Empty,
            sourceScope,
            request.ExpectedRevision,
            administrativeIds,
            actor);

        var replay = await TryReplayAsync(
            clientRequestId,
            ReplaceAdministrativeDongsOperation,
            requestHash,
            cancellationToken);
        if (replay is not null)
            return replay;

        var territory = await FindTerritoryAsync(stableId, true, cancellationToken)
                        ?? throw new KeyNotFoundException("DeliveryTerritoryNotFound");
        if (!string.Equals(territory.상태Code, 배달운영권역상태Codes.Draft, StringComparison.Ordinal))
            throw new 배달운영권역ConflictException("DeliveryTerritoryDraftRequired");
        if (!string.Equals(territory.SourceScopeStableId, sourceScope, StringComparison.Ordinal))
            throw new 배달운영권역ConflictException("DeliveryTerritorySourceScopeImmutable");
        if (territory.Revision != request.ExpectedRevision)
            throw new 배달운영권역ConcurrencyException("DeliveryTerritoryRevisionConflict");

        var candidates = await ResolveCandidatesAsync(sourceScope, administrativeIds, cancellationToken);
        await EnsureUnassignedAsync(stableId, administrativeIds, cancellationToken);
        var candidatesById = candidates.ToDictionary(
            x => x.AdministrativeAreaStableId,
            StringComparer.Ordinal);
        var requested = administrativeIds.ToHashSet(StringComparer.Ordinal);
        var now = DateTime.UtcNow;

        foreach (var membership in territory.행정동Memberships)
        {
            if (!requested.Contains(membership.행정동고유식별자))
            {
                if (string.Equals(
                        membership.상태Code,
                        배달운영권역행정동상태Codes.Included,
                        StringComparison.Ordinal))
                {
                    membership.상태Code = 배달운영권역행정동상태Codes.Excluded;
                    membership.현행행정동유일성Key = null;
                    membership.ExcludedAtUtc = now;
                    membership.UpdatedAtUtc = now;
                }
                continue;
            }

            ApplyCandidate(membership, candidatesById[membership.행정동고유식별자], now);
        }

        var existingIds = territory.행정동Memberships
            .Select(x => x.행정동고유식별자)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var candidate in candidates.Where(x => !existingIds.Contains(x.AdministrativeAreaStableId)))
            territory.행정동Memberships.Add(CreateMembership(candidate, now));

        territory.Revision++;
        territory.UpdatedAtUtc = now;
        var result = ToDto(territory);
        db.배달운영권역CommandReceipts.Add(new 배달운영권역CommandReceipt
        {
            ClientRequestId = clientRequestId,
            OperationCode = ReplaceAdministrativeDongsOperation,
            RequestHashSha256 = requestHash,
            ActorUserStableId = actor,
            배달운영권역고유식별자 = stableId,
            ResultRevision = territory.Revision,
            ResultJson = JsonSerializer.Serialize(result, JsonOptions),
            CreatedAtUtc = now
        });
        db.배달운영권역변경Outbox.Add(CreateOutbox(
            territory,
            "DeliveryOperatingTerritoryAdministrativeDongsReplaced",
            administrativeIds,
            actor,
            now));

        await SaveChangesAsync(cancellationToken);
        return result;
    }

    private async Task<IReadOnlyList<배달운영권역행정동SourceItem>> ResolveCandidatesAsync(
        string sourceScope,
        IReadOnlyList<string> requestedIds,
        CancellationToken cancellationToken)
    {
        var all = await source.조회Async(sourceScope, cancellationToken);
        var byId = all.ToDictionary(x => x.AdministrativeAreaStableId, StringComparer.Ordinal);
        var unknown = requestedIds.Where(x => !byId.ContainsKey(x)).ToArray();
        if (unknown.Length > 0)
            throw new ArgumentException(
                $"AdministrativeDongUnknown:{string.Join(',', unknown)}",
                nameof(requestedIds));
        return requestedIds.Select(x => byId[x]).ToArray();
    }

    private async Task EnsureUnassignedAsync(
        string currentTerritoryStableId,
        IReadOnlyList<string> administrativeIds,
        CancellationToken cancellationToken)
    {
        if (administrativeIds.Count == 0)
            return;
        var conflicts = await db.배달운영권역행정동Memberships
            .AsNoTracking()
            .Include(x => x.배달운영권역)
            .Where(x => x.현행행정동유일성Key != null
                        && administrativeIds.Contains(x.행정동고유식별자)
                        && x.배달운영권역.권역고유식별자 != currentTerritoryStableId)
            .Select(x => x.행정동고유식별자)
            .Distinct()
            .OrderBy(x => x)
            .ToArrayAsync(cancellationToken);
        if (conflicts.Length > 0)
            throw new 배달운영권역ConflictException(
                $"AdministrativeDongAlreadyAssigned:{string.Join(',', conflicts)}");
    }

    private async Task<배달운영권역Dto?> TryReplayAsync(
        string clientRequestId,
        string operationCode,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var receipt = await db.배달운영권역CommandReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ClientRequestId == clientRequestId, cancellationToken);
        if (receipt is null)
            return null;
        if (!string.Equals(receipt.OperationCode, operationCode, StringComparison.Ordinal)
            || !string.Equals(receipt.RequestHashSha256, requestHash, StringComparison.Ordinal))
            throw new 배달운영권역ConflictException("ClientRequestPayloadConflict");

        try
        {
            var result = JsonSerializer.Deserialize<배달운영권역Dto>(receipt.ResultJson, JsonOptions);
            if (result is null
                || !string.Equals(
                    result.DeliveryTerritoryStableId,
                    receipt.배달운영권역고유식별자,
                    StringComparison.Ordinal)
                || result.Revision != receipt.ResultRevision)
            {
                throw new InvalidDataException("DeliveryTerritoryIdempotencyResultMismatch");
            }

            return result;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("DeliveryTerritoryIdempotencyResultInvalid", exception);
        }
    }

    private async Task<배달운영권역?> FindTerritoryAsync(
        string stableId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        IQueryable<배달운영권역> query = db.배달운영권역
            .Include(x => x.행정동Memberships);
        if (!tracked)
            query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(
            x => x.권역고유식별자 == stableId,
            cancellationToken);
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new 배달운영권역ConcurrencyException(
                "DeliveryTerritoryRevisionConflict",
                exception);
        }
    }

    private static 배달운영권역행정동Membership CreateMembership(
        배달운영권역행정동SourceItem candidate,
        DateTime now)
        => new()
        {
            행정동고유식별자 = candidate.AdministrativeAreaStableId,
            행정동표시명 = candidate.DisplayName,
            법정동목록Json = SerializeLegalAreas(candidate.LegalAreas),
            상태Code = 배달운영권역행정동상태Codes.Included,
            현행행정동유일성Key = candidate.AdministrativeAreaStableId,
            관할SourceId = candidate.SourceId,
            관할DataRevision = candidate.DataRevision,
            IncludedAtUtc = now,
            UpdatedAtUtc = now
        };

    private static void ApplyCandidate(
        배달운영권역행정동Membership membership,
        배달운영권역행정동SourceItem candidate,
        DateTime now)
    {
        membership.행정동표시명 = candidate.DisplayName;
        membership.법정동목록Json = SerializeLegalAreas(candidate.LegalAreas);
        membership.관할SourceId = candidate.SourceId;
        membership.관할DataRevision = candidate.DataRevision;
        if (!string.Equals(
                membership.상태Code,
                배달운영권역행정동상태Codes.Included,
                StringComparison.Ordinal))
            membership.IncludedAtUtc = now;
        membership.상태Code = 배달운영권역행정동상태Codes.Included;
        membership.현행행정동유일성Key = candidate.AdministrativeAreaStableId;
        membership.ExcludedAtUtc = null;
        membership.UpdatedAtUtc = now;
    }

    private static 배달운영권역변경Outbox CreateOutbox(
        배달운영권역 territory,
        string eventTypeCode,
        IReadOnlyList<string> administrativeIds,
        string actorUserStableId,
        DateTime now)
    {
        var payload = JsonSerializer.Serialize(new
        {
            deliveryTerritoryStableId = territory.권역고유식별자,
            revision = territory.Revision,
            statusCode = territory.상태Code,
            actorUserStableId,
            administrativeAreaStableIds = administrativeIds
        }, JsonOptions);
        return new 배달운영권역변경Outbox
        {
            EventStableId =
                $"delivery-territory-event:{HashText(territory.권역고유식별자)[..24]}:{territory.Revision}",
            AggregateStableId = territory.권역고유식별자,
            AggregateRevision = territory.Revision,
            EventTypeCode = eventTypeCode,
            ActorUserStableId = actorUserStableId,
            PayloadJson = payload,
            CreatedAtUtc = now
        };
    }

    private static 배달운영권역Dto ToDto(배달운영권역 territory)
        => new()
        {
            DeliveryTerritoryStableId = territory.권역고유식별자,
            DisplayName = territory.표시명,
            SourceScopeStableId = territory.SourceScopeStableId,
            StatusCode = territory.상태Code,
            Revision = territory.Revision,
            AdministrativeDongs = territory.행정동Memberships
                .OrderBy(x => x.행정동고유식별자, StringComparer.Ordinal)
                .Select(x => new 배달운영권역행정동Dto
                {
                    AdministrativeAreaStableId = x.행정동고유식별자,
                    AdministrativeAreaDisplayName = x.행정동표시명,
                    LegalAreas = DeserializeLegalAreas(x.법정동목록Json),
                    MembershipStateCode = x.상태Code,
                    JurisdictionSourceId = x.관할SourceId,
                    JurisdictionDataRevision = x.관할DataRevision,
                    IncludedAtUtc = x.IncludedAtUtc,
                    ExcludedAtUtc = x.ExcludedAtUtc
                })
                .ToArray(),
            AvailableActionIds = string.Equals(
                territory.상태Code,
                배달운영권역상태Codes.Draft,
                StringComparison.Ordinal)
                ? [배달운영권역ActionIds.ReplaceAdministrativeDongs]
                : [],
            CreatedAtUtc = territory.CreatedAtUtc,
            UpdatedAtUtc = territory.UpdatedAtUtc
        };

    private static 행정동운영법정동RefDto ToLegalAreaDto(배달운영권역법정동SourceItem source)
        => new()
        {
            LegalAreaStableId = source.LegalAreaStableId,
            DisplayName = source.DisplayName
        };

    private static string SerializeLegalAreas(IReadOnlyList<배달운영권역법정동SourceItem> values)
        => JsonSerializer.Serialize(
            values.OrderBy(x => x.LegalAreaStableId, StringComparer.Ordinal)
                .Select(ToLegalAreaDto)
                .ToArray(),
            JsonOptions);

    private static IReadOnlyList<행정동운영법정동RefDto> DeserializeLegalAreas(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<행정동운영법정동RefDto[]>(json, JsonOptions)
                   ?? [];
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("DeliveryTerritoryLegalAreaSnapshotInvalid", exception);
        }
    }

    private static IReadOnlyList<string> NormalizeAdministrativeIds(
        IReadOnlyList<string>? values,
        bool allowEmpty)
    {
        var items = (values ?? [])
            .Select(x => x?.Trim() ?? string.Empty)
            .ToArray();
        if ((!allowEmpty && items.Length == 0) || items.Length > 100)
            throw new ArgumentException("DeliveryTerritoryAdministrativeDongCountInvalid", nameof(values));
        if (items.Any(x => !AdministrativeDongDioramaPolicy.IsAdministrativeAreaStableId(x)))
            throw new ArgumentException("AdministrativeDongStableIdInvalid", nameof(values));
        if (items.Distinct(StringComparer.Ordinal).Count() != items.Length)
            throw new ArgumentException("DeliveryTerritoryAdministrativeDongDuplicate", nameof(values));
        return items.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    private static string NormalizeTerritoryStableId(string value)
    {
        const string prefix = "delivery-territory:kr:";
        var normalized = NormalizeRequired(value, nameof(value), 160).ToLowerInvariant();
        if (!normalized.StartsWith(prefix, StringComparison.Ordinal)
            || normalized.Length == prefix.Length
            || normalized.Any(character => !(char.IsAsciiLetterOrDigit(character)
                                              || character is ':' or '-' or '_' or '.')))
            throw new ArgumentException("DeliveryTerritoryStableIdInvalid", nameof(value));
        return normalized;
    }

    private static string NormalizeSourceScope(string value)
    {
        var normalized = NormalizeRequired(value, nameof(value), 160);
        _ = 행정동배달운영권역SourceScopeCatalog.법정동목록(normalized);
        return normalized;
    }

    private static string NormalizeRequired(string value, string parameterName, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maxLength)
            throw new ArgumentException($"{parameterName}Invalid", parameterName);
        return normalized;
    }

    private static string HashRequest(
        string operationCode,
        string stableId,
        string displayName,
        string sourceScope,
        long expectedRevision,
        IReadOnlyList<string> administrativeIds,
        string actorUserStableId)
    {
        var canonical = string.Join('|',
            operationCode,
            stableId,
            displayName,
            sourceScope,
            expectedRevision,
            actorUserStableId,
            string.Join(',', administrativeIds));
        return HashText(canonical);
    }

    private static string HashText(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
}

public sealed class 배달운영권역ConflictException(string message) : Exception(message);

public sealed class 배달운영권역ConcurrencyException : Exception
{
    public 배달운영권역ConcurrencyException(string message) : base(message)
    {
    }

    public 배달운영권역ConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
