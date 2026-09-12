using System.Text.Json;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.Operations;
using 살뜰.도메인.화주;

namespace Ssalddel.Application.Shipper.Request;

public interface I화주운송업무담당자UseCase
{
    Task<Result<운송업무담당자배정응답>> 조회Async(
        string 운송의뢰Id,
        CancellationToken cancellationToken = default);

    Task<Result<운송업무담당자배정응답>> 변경Async(
        string 운송의뢰Id,
        운송업무담당자배정변경요청 request,
        CancellationToken cancellationToken = default);

    Task<bool> 권한보유Async(
        화주운송의뢰 운송의뢰,
        string 권한Code,
        CancellationToken cancellationToken = default);

    Task<bool> 모든권한보유Async(
        화주운송의뢰 운송의뢰,
        IReadOnlyCollection<string> 권한Codes,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<string>> 조회가능운송의뢰IdsAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 운송 의뢰별 주 담당자 1명과 보조 담당자의 권한을 판본화해 관리합니다.
/// 담당자는 화주 역할을 넘겨받지 않으며, 배정 변경은 화주 또는 서버 관리자만 실행할 수 있습니다.
/// </summary>
[SsalddelApiWorkflow(SsalddelWorkflow.DomesticTransport)]
[SsalddelUseCase(
    "화주 운송 업무 담당자 관리",
    Summary = "화주가 운송 건의 주 담당자 한 명과 보조 담당자별 권한을 판본화하고 기존 운송 업무 Command의 권한을 판정합니다.")]
[SsalddelUseCaseActor(SsalddelActor.Shipper)]
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.OperationalLogisticsOs,
    SsalddelCodeLayer.Application,
    "운송 의뢰별 담당자 집합을 append-only 판본으로 저장하고 현재 판본의 업무 권한을 판정한다.",
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    FlowOrder = 15,
    StepKey = "application.shipper-transport-operator-authority",
    ExecutionStage = SsalddelCodeExecutionStage.Confirm,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "화주·원천 주문·비용 귀속을 변경하지 않는다. 담당자는 다른 담당자를 지정하거나 과거 판본을 삭제할 수 없다.")]
public sealed class 화주운송업무담당자UseCase : I화주운송업무담당자UseCase
{
    private const string PrimarySlot = "PRIMARY";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SsalddelContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly TimeProvider _timeProvider;

    public 화주운송업무담당자UseCase(
        SsalddelContext db,
        ICurrentUserAccessor currentUserAccessor,
        TimeProvider timeProvider)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _timeProvider = timeProvider;
    }

    public async Task<Result<운송업무담당자배정응답>> 조회Async(
        string 운송의뢰Id,
        CancellationToken cancellationToken = default)
    {
        var entity = await FindRequestAsync(운송의뢰Id, cancellationToken);
        if (entity is null
            || !await 권한보유Async(entity, 운송업무권한Codes.진행조회, cancellationToken))
        {
            return Fail<운송업무담당자배정응답>(
                "운송 의뢰를 찾을 수 없습니다.",
                StatusCodes.Status404NotFound,
                "TransportRequestNotFound");
        }

        return Result.Ok(await BuildCurrentResponseAsync(entity, cancellationToken));
    }

    public async Task<Result<운송업무담당자배정응답>> 변경Async(
        string 운송의뢰Id,
        운송업무담당자배정변경요청 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entity = await FindRequestAsync(운송의뢰Id, cancellationToken);
        if (entity is null || !화주권위인가(entity))
        {
            return Fail<운송업무담당자배정응답>(
                "운송 의뢰를 찾을 수 없습니다.",
                StatusCodes.Status404NotFound,
                "TransportRequestNotFound");
        }

        if (request.클라이언트요청Id == Guid.Empty)
        {
            return Fail<운송업무담당자배정응답>(
                "클라이언트요청Id가 필요합니다.",
                StatusCodes.Status400BadRequest,
                "ClientRequestIdRequired");
        }

        if (request.예상Revision < 0)
        {
            return Fail<운송업무담당자배정응답>(
                "예상Revision은 0 이상이어야 합니다.",
                StatusCodes.Status400BadRequest,
                "ExpectedRevisionInvalid");
        }

        var normalizeResult = NormalizeAssignments(request.담당자목록);
        if (normalizeResult.IsFailed)
        {
            return Result.Fail<운송업무담당자배정응답>(normalizeResult.Errors);
        }

        var normalizedAssignments = normalizeResult.Value;
        var normalizedRequestId = entity.의뢰Id;
        var idempotentRows = await _db.운송업무담당자배정
            .AsNoTracking()
            .Where(x => x.운송의뢰Id == normalizedRequestId
                        && x.클라이언트요청Id == request.클라이언트요청Id)
            .OrderBy(x => x.담당자UserId)
            .ToArrayAsync(cancellationToken);

        if (idempotentRows.Length > 0)
        {
            if (!Equivalent(idempotentRows, normalizedAssignments))
            {
                return Fail<운송업무담당자배정응답>(
                    "같은 클라이언트요청Id를 다른 담당자 배정 내용으로 다시 사용할 수 없습니다.",
                    StatusCodes.Status409Conflict,
                    "AssignmentRequestPayloadConflict");
            }

            return Result.Ok(BuildResponse(entity, idempotentRows));
        }

        var knownUsersResult = await ValidateKnownUsersAsync(normalizedAssignments, cancellationToken);
        if (knownUsersResult.IsFailed)
        {
            return Result.Fail<운송업무담당자배정응답>(knownUsersResult.Errors);
        }

        var currentRevision = await CurrentRevisionAsync(normalizedRequestId, cancellationToken);
        if (request.예상Revision != currentRevision)
        {
            return Fail<운송업무담당자배정응답>(
                $"담당자 배정 판본이 변경되었습니다. currentRevision={currentRevision}",
                StatusCodes.Status409Conflict,
                "AssignmentRevisionConflict");
        }

        var nextRevision = checked(currentRevision + 1);
        var actorUserId = _currentUserAccessor.UserId?.Trim() ?? "system";
        var assignedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var shipperId = ResolveShipperId(entity);
        var rows = normalizedAssignments.Select(item => new 운송업무담당자배정
        {
            Id = Guid.NewGuid(),
            운송의뢰Id = normalizedRequestId,
            배정세트Revision = nextRevision,
            클라이언트요청Id = request.클라이언트요청Id,
            화주Id = shipperId,
            담당자UserId = item.UserId,
            담당유형Code = item.RoleCode,
            주담당Slot = item.RoleCode == 운송업무담당유형Codes.주담당 ? PrimarySlot : null,
            권한CodesJson = SerializePermissions(item.PermissionCodes),
            지정자UserId = actorUserId,
            지정시각Utc = assignedAtUtc
        }).ToArray();

        await _db.운송업무담당자배정.AddRangeAsync(rows, cancellationToken);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Fail<운송업무담당자배정응답>(
                "담당자 배정 판본이 동시에 변경되었습니다. 현재 판본을 다시 조회해 주세요.",
                StatusCodes.Status409Conflict,
                "AssignmentRevisionConflict");
        }

        return Result.Ok(BuildResponse(entity, rows));
    }

    public Task<bool> 권한보유Async(
        화주운송의뢰 운송의뢰,
        string 권한Code,
        CancellationToken cancellationToken = default)
        => 모든권한보유Async(운송의뢰, [권한Code], cancellationToken);

    public async Task<bool> 모든권한보유Async(
        화주운송의뢰 운송의뢰,
        IReadOnlyCollection<string> 권한Codes,
        CancellationToken cancellationToken = default)
    {
        if (권한Codes.Count == 0)
        {
            return true;
        }

        if (권한Codes.Any(code => !운송업무권한Codes.지원하는가(code)))
        {
            return false;
        }

        if (화주권위인가(운송의뢰))
        {
            return true;
        }

        var currentUserId = _currentUserAccessor.UserId?.Trim();
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return false;
        }

        var currentRevision = await CurrentRevisionAsync(운송의뢰.의뢰Id, cancellationToken);
        if (currentRevision == 0)
        {
            return false;
        }

        var assignment = await _db.운송업무담당자배정
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.운송의뢰Id == 운송의뢰.의뢰Id
                     && x.배정세트Revision == currentRevision
                     && x.담당자UserId == currentUserId,
                cancellationToken);
        if (assignment is null)
        {
            return false;
        }

        var granted = ResolvePermissions(assignment.담당유형Code, assignment.권한CodesJson);
        return 권한Codes.All(granted.Contains);
    }

    public async Task<IReadOnlySet<string>> 조회가능운송의뢰IdsAsync(
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserAccessor.UserId?.Trim();
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var userRows = await _db.운송업무담당자배정
            .AsNoTracking()
            .Where(x => x.담당자UserId == currentUserId)
            .ToArrayAsync(cancellationToken);
        if (userRows.Length == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var candidateRequestIds = userRows
            .Select(x => x.운송의뢰Id)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var revisionRows = await _db.운송업무담당자배정
            .AsNoTracking()
            .Where(x => candidateRequestIds.Contains(x.운송의뢰Id))
            .Select(x => new { x.운송의뢰Id, x.배정세트Revision })
            .ToArrayAsync(cancellationToken);
        var latestByRequest = revisionRows
            .GroupBy(x => x.운송의뢰Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Max(x => x.배정세트Revision), StringComparer.Ordinal);

        return userRows
            .Where(row => latestByRequest.TryGetValue(row.운송의뢰Id, out var latest)
                          && row.배정세트Revision == latest
                          && ResolvePermissions(row.담당유형Code, row.권한CodesJson)
                              .Contains(운송업무권한Codes.진행조회))
            .Select(row => row.운송의뢰Id)
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task<화주운송의뢰?> FindRequestAsync(
        string requestId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return null;
        }

        var normalized = requestId.Trim();
        return await _db.화주운송의뢰
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.의뢰Id == normalized, cancellationToken);
    }

    private async Task<운송업무담당자배정응답> BuildCurrentResponseAsync(
        화주운송의뢰 entity,
        CancellationToken cancellationToken)
    {
        var revision = await CurrentRevisionAsync(entity.의뢰Id, cancellationToken);
        if (revision == 0)
        {
            var shipperId = ResolveShipperId(entity);
            return new 운송업무담당자배정응답
            {
                운송의뢰Id = entity.의뢰Id,
                화주Id = shipperId,
                Revision = 0,
                암묵적화주주담당여부 = true,
                담당자관리가능여부 = 화주권위인가(entity),
                담당자목록 =
                [
                    new 운송업무담당자응답
                    {
                        UserId = shipperId,
                        담당유형Code = 운송업무담당유형Codes.주담당,
                        권한Codes = 운송업무권한Codes.주담당기본권한,
                        지정자UserId = shipperId,
                        지정시각Utc = entity.CreatedAt
                    }
                ]
            };
        }

        var rows = await _db.운송업무담당자배정
            .AsNoTracking()
            .Where(x => x.운송의뢰Id == entity.의뢰Id && x.배정세트Revision == revision)
            .OrderBy(x => x.담당유형Code)
            .ThenBy(x => x.담당자UserId)
            .ToArrayAsync(cancellationToken);
        return BuildResponse(entity, rows);
    }

    private 운송업무담당자배정응답 BuildResponse(
        화주운송의뢰 entity,
        IReadOnlyCollection<운송업무담당자배정> rows)
        => new()
        {
            운송의뢰Id = entity.의뢰Id,
            화주Id = ResolveShipperId(entity),
            Revision = rows.Count == 0 ? 0 : rows.Max(x => x.배정세트Revision),
            암묵적화주주담당여부 = false,
            담당자관리가능여부 = 화주권위인가(entity),
            담당자목록 = rows
                .OrderBy(x => x.담당유형Code == 운송업무담당유형Codes.주담당 ? 0 : 1)
                .ThenBy(x => x.담당자UserId, StringComparer.Ordinal)
                .Select(x => new 운송업무담당자응답
                {
                    UserId = x.담당자UserId,
                    담당유형Code = x.담당유형Code,
                    권한Codes = ResolvePermissions(x.담당유형Code, x.권한CodesJson)
                        .OrderBy(code => code, StringComparer.Ordinal)
                        .ToArray(),
                    지정자UserId = x.지정자UserId,
                    지정시각Utc = x.지정시각Utc
                })
                .ToArray()
        };

    private async Task<long> CurrentRevisionAsync(string requestId, CancellationToken cancellationToken)
        => await _db.운송업무담당자배정
               .AsNoTracking()
               .Where(x => x.운송의뢰Id == requestId)
               .Select(x => (long?)x.배정세트Revision)
               .MaxAsync(cancellationToken)
           ?? 0;

    private async Task<Result> ValidateKnownUsersAsync(
        IReadOnlyCollection<NormalizedAssignment> assignments,
        CancellationToken cancellationToken)
    {
        var requestedUserIds = assignments.Select(x => x.UserId).ToList();
        var knownUserIds = await _db.Users
            .AsNoTracking()
            .Where(x => requestedUserIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);
        var known = knownUserIds.ToHashSet(StringComparer.Ordinal);
        var missing = requestedUserIds.Where(x => !known.Contains(x)).ToArray();
        return missing.Length == 0
            ? Result.Ok()
            : Result.Fail(new Error($"등록되지 않은 담당자 사용자가 있습니다: {string.Join(", ", missing)}")
                .WithMetadata("StatusCode", StatusCodes.Status400BadRequest)
                .WithMetadata("ErrorCode", "AssignmentUserNotFound"));
    }

    private static Result<IReadOnlyList<NormalizedAssignment>> NormalizeAssignments(
        IReadOnlyList<운송업무담당자지정요청>? assignments)
    {
        if (assignments is null || assignments.Count == 0)
        {
            return Fail<IReadOnlyList<NormalizedAssignment>>(
                "주 담당자를 한 명 이상 지정해야 합니다.",
                StatusCodes.Status400BadRequest,
                "PrimaryAssignmentRequired");
        }

        if (assignments.Count > 20)
        {
            return Fail<IReadOnlyList<NormalizedAssignment>>(
                "한 운송 의뢰에는 담당자를 최대 20명까지 지정할 수 있습니다.",
                StatusCodes.Status400BadRequest,
                "AssignmentLimitExceeded");
        }

        var normalized = new List<NormalizedAssignment>(assignments.Count);
        var seenUsers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assignment in assignments)
        {
            var userId = assignment.UserId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(userId) || !seenUsers.Add(userId))
            {
                return Fail<IReadOnlyList<NormalizedAssignment>>(
                    "담당자 UserId는 비어 있을 수 없고 한 판본에서 중복될 수 없습니다.",
                    StatusCodes.Status400BadRequest,
                    "AssignmentUserInvalid");
            }

            var roleCode = assignment.담당유형Code?.Trim() ?? string.Empty;
            if (!운송업무담당유형Codes.지원하는가(roleCode))
            {
                return Fail<IReadOnlyList<NormalizedAssignment>>(
                    "담당 유형은 주 담당 또는 보조 담당이어야 합니다.",
                    StatusCodes.Status400BadRequest,
                    "AssignmentRoleInvalid");
            }

            var requestedPermissions = assignment.권한Codes ?? [];
            var unsupported = requestedPermissions
                .Where(code => !운송업무권한Codes.지원하는가(code))
                .ToArray();
            if (unsupported.Length > 0)
            {
                return Fail<IReadOnlyList<NormalizedAssignment>>(
                    $"지원하지 않는 운송 업무 권한이 있습니다: {string.Join(", ", unsupported)}",
                    StatusCodes.Status400BadRequest,
                    "AssignmentPermissionInvalid");
            }

            var defaults = roleCode == 운송업무담당유형Codes.주담당
                ? 운송업무권한Codes.주담당기본권한
                : 운송업무권한Codes.보조담당기본권한;
            var permissions = defaults
                .Concat(requestedPermissions.Select(x => x.Trim()))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            normalized.Add(new NormalizedAssignment(userId, roleCode, permissions));
        }

        if (normalized.Count(x => x.RoleCode == 운송업무담당유형Codes.주담당) != 1)
        {
            return Fail<IReadOnlyList<NormalizedAssignment>>(
                "활성 주 담당자는 정확히 한 명이어야 합니다.",
                StatusCodes.Status400BadRequest,
                "ExactlyOnePrimaryRequired");
        }

        return Result.Ok<IReadOnlyList<NormalizedAssignment>>(normalized);
    }

    private bool 화주권위인가(화주운송의뢰 entity)
    {
        if (주문자권한검사.IsServerAdmin(_currentUserAccessor))
        {
            return true;
        }

        var currentUserId = _currentUserAccessor.UserId?.Trim();
        return !string.IsNullOrWhiteSpace(currentUserId)
               && string.Equals(ResolveShipperId(entity), currentUserId, StringComparison.Ordinal);
    }

    private static string ResolveShipperId(화주운송의뢰 entity)
        => string.IsNullOrWhiteSpace(entity.화주Id)
            ? entity.주문자UserId.Trim()
            : entity.화주Id.Trim();

    private static bool Equivalent(
        IReadOnlyCollection<운송업무담당자배정> existing,
        IReadOnlyCollection<NormalizedAssignment> requested)
    {
        if (existing.Count != requested.Count)
        {
            return false;
        }

        var existingByUser = existing.ToDictionary(x => x.담당자UserId, StringComparer.Ordinal);
        foreach (var item in requested)
        {
            if (!existingByUser.TryGetValue(item.UserId, out var row)
                || !string.Equals(row.담당유형Code, item.RoleCode, StringComparison.Ordinal))
            {
                return false;
            }

            var persistedPermissions = ResolvePermissions(row.담당유형Code, row.권한CodesJson);
            if (!persistedPermissions.SetEquals(item.PermissionCodes))
            {
                return false;
            }
        }

        return true;
    }

    private static string SerializePermissions(IEnumerable<string> permissions)
        => JsonSerializer.Serialize(
            permissions.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal),
            JsonOptions);

    private static HashSet<string> ResolvePermissions(string roleCode, string? json)
    {
        var defaults = roleCode == 운송업무담당유형Codes.주담당
            ? 운송업무권한Codes.주담당기본권한
            : 운송업무권한Codes.보조담당기본권한;
        var permissions = new HashSet<string>(defaults, StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
        {
            return permissions;
        }

        try
        {
            foreach (var code in JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? [])
            {
                if (운송업무권한Codes.지원하는가(code))
                {
                    permissions.Add(code.Trim());
                }
            }
        }
        catch (JsonException)
        {
            // 손상된 추가 권한은 확대 해석하지 않고 역할별 최소 기본 권한만 사용합니다.
        }

        return permissions;
    }

    private static Result<T> Fail<T>(string message, int statusCode, string errorCode)
        => Result.Fail<T>(new Error(message)
            .WithMetadata("StatusCode", statusCode)
            .WithMetadata("ErrorCode", errorCode));

    private sealed record NormalizedAssignment(
        string UserId,
        string RoleCode,
        IReadOnlyList<string> PermissionCodes);
}
