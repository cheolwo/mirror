using FluentResults;
using Hongdal.Contracts.Common.Community;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using 홍달.Data;
using 홍달.Services.Options;
using 홍달.도메인.사용자;

namespace Hongdal.Services.Community;

public interface ICommunityLedgerRoleAccessPolicyStore
{
    Task<CommunityLedgerRoleAccessPolicy?> GetAsync(string ledgerId, CancellationToken cancellationToken = default);

    Task<CommunityLedgerRoleAccessPolicy> SaveAsync(
        CommunityLedgerRoleAccessPolicy policy,
        long? expectedRevision,
        CancellationToken cancellationToken = default);
}

public sealed class MongoCommunityLedgerRoleAccessPolicyStore : ICommunityLedgerRoleAccessPolicyStore
{
    private const string CollectionName = "community_ledger_role_access_policies";
    private readonly IMongoCollection<CommunityLedgerRoleAccessPolicyDocument> _collection;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private bool _indexesReady;

    public MongoCommunityLedgerRoleAccessPolicyStore(IMongoClient mongoClient, IOptions<MongoDbOptions> options)
    {
        _collection = mongoClient
            .GetDatabase(options.Value.Database)
            .GetCollection<CommunityLedgerRoleAccessPolicyDocument>(CollectionName);
    }

    public async Task<CommunityLedgerRoleAccessPolicy?> GetAsync(
        string ledgerId,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        var document = await _collection
            .Find(x => x.LedgerId == ledgerId.Trim())
            .FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : ToModel(document);
    }

    public async Task<CommunityLedgerRoleAccessPolicy> SaveAsync(
        CommunityLedgerRoleAccessPolicy policy,
        long? expectedRevision,
        CancellationToken cancellationToken = default)
    {
        await EnsureIndexesAsync(cancellationToken);
        var existing = await _collection
            .Find(x => x.LedgerId == policy.LedgerId)
            .FirstOrDefaultAsync(cancellationToken);
        var actualRevision = existing?.Revision ?? 0;
        if (expectedRevision.HasValue && expectedRevision.Value != actualRevision)
        {
            throw new InvalidOperationException("역할별 다이어그램 권한이 다른 요청에서 먼저 변경되었습니다. 다시 불러온 뒤 저장해 주세요.");
        }

        var now = DateTime.UtcNow;
        var document = new CommunityLedgerRoleAccessPolicyDocument
        {
            Id = existing?.Id ?? ObjectId.GenerateNewId(),
            LedgerId = policy.LedgerId,
            OwnerUserId = policy.OwnerUserId,
            Grants = policy.Grants.Select(ToDocument).ToArray(),
            Revision = actualRevision + 1,
            UpdatedAtUtc = now
        };
        var filter = existing is null
            ? Builders<CommunityLedgerRoleAccessPolicyDocument>.Filter.Eq(x => x.LedgerId, policy.LedgerId)
            : Builders<CommunityLedgerRoleAccessPolicyDocument>.Filter.And(
                Builders<CommunityLedgerRoleAccessPolicyDocument>.Filter.Eq(x => x.LedgerId, policy.LedgerId),
                Builders<CommunityLedgerRoleAccessPolicyDocument>.Filter.Eq(x => x.Revision, actualRevision));
        var result = await _collection.ReplaceOneAsync(
            filter,
            document,
            new ReplaceOptions { IsUpsert = existing is null },
            cancellationToken);
        if (existing is not null && result.MatchedCount == 0)
        {
            throw new InvalidOperationException("역할별 다이어그램 권한이 다른 요청에서 먼저 변경되었습니다. 다시 불러온 뒤 저장해 주세요.");
        }

        return ToModel(document);
    }

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        if (_indexesReady)
        {
            return;
        }

        await _indexLock.WaitAsync(cancellationToken);
        try
        {
            if (_indexesReady)
            {
                return;
            }

            await _collection.Indexes.CreateOneAsync(
                new CreateIndexModel<CommunityLedgerRoleAccessPolicyDocument>(
                    Builders<CommunityLedgerRoleAccessPolicyDocument>.IndexKeys.Ascending(x => x.LedgerId),
                    new CreateIndexOptions { Unique = true, Name = "ux_ledger_role_access_ledger_id" }),
                cancellationToken: cancellationToken);
            _indexesReady = true;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private static CommunityLedgerRoleAccessPolicy ToModel(CommunityLedgerRoleAccessPolicyDocument document)
        => new()
        {
            LedgerId = document.LedgerId,
            OwnerUserId = document.OwnerUserId,
            Grants = document.Grants.Select(ToModel).ToArray(),
            Revision = document.Revision,
            UpdatedAtUtc = document.UpdatedAtUtc
        };

    private static CommunityLedgerRoleGrant ToModel(CommunityLedgerRoleGrantDocument document)
        => new()
        {
            TargetUserId = document.TargetUserId,
            TargetDisplayName = document.TargetDisplayName,
            RoleCode = document.RoleCode,
            AccessEnabled = document.AccessEnabled,
            ViewScope = document.ViewScope,
            VisibleNodeIds = document.VisibleNodeIds,
            EditableNodeIds = document.EditableNodeIds,
            CanCoordinateTransport = document.CanCoordinateTransport,
            UpdatedAtUtc = document.UpdatedAtUtc
        };

    private static CommunityLedgerRoleGrantDocument ToDocument(CommunityLedgerRoleGrant grant)
        => new()
        {
            TargetUserId = grant.TargetUserId,
            TargetDisplayName = grant.TargetDisplayName,
            RoleCode = grant.RoleCode,
            AccessEnabled = grant.AccessEnabled,
            ViewScope = grant.ViewScope,
            VisibleNodeIds = grant.VisibleNodeIds,
            EditableNodeIds = grant.EditableNodeIds,
            CanCoordinateTransport = grant.CanCoordinateTransport,
            UpdatedAtUtc = grant.UpdatedAtUtc
        };
}

public interface ICommunityCustomsBrokerDirectory
{
    Task<IReadOnlyList<CommunityLedgerCustomsBrokerCandidateResponse>> ListEligibleAsync(
        CancellationToken cancellationToken = default);

    Task<bool> IsEligibleAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed class EfCommunityCustomsBrokerDirectory : ICommunityCustomsBrokerDirectory
{
    private readonly HongdalContext _db;

    public EfCommunityCustomsBrokerDirectory(HongdalContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CommunityLedgerCustomsBrokerCandidateResponse>> ListEligibleAsync(
        CancellationToken cancellationToken = default)
        => await EligibleQuery()
            .OrderBy(x => x.OfficeName)
            .ThenBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

    public Task<bool> IsEligibleAsync(string userId, CancellationToken cancellationToken = default)
        => EligibleQuery().AnyAsync(x => x.ParticipantId == userId.Trim(), cancellationToken);

    private IQueryable<CommunityLedgerCustomsBrokerCandidateResponse> EligibleQuery()
        => (from profile in _db.관세사프로필.AsNoTracking()
            join participant in _db.홍달참여자.AsNoTracking()
                on profile.참여자Id equals participant.Id
            join role in _db.홍달참여자역할.AsNoTracking()
                on participant.Id equals role.참여자Id
            where profile.관리자승인여부
                  && profile.수임가능여부
                  && profile.수입전문여부
                  && participant.활성화여부
                  && role.활성화여부
                  && role.역할유형 == 홍달역할유형.관세사
            select new CommunityLedgerCustomsBrokerCandidateResponse
            {
                ParticipantId = participant.Id,
                DisplayName = participant.표시이름,
                OfficeName = profile.사무소명,
                Region = profile.담당지역,
                SpecialtyMemo = profile.전문품목메모
            }).Distinct();
}

public interface ICommunityLedgerRoleAccessService
{
    Task<Result<CommunityLedgerRoleAccessSettingsResponse>> GetSettingsAsync(
        string ledgerId,
        string? userId,
        CancellationToken cancellationToken);

    Task<Result<CommunityLedgerRoleAccessSettingsResponse>> UpdateSettingsAsync(
        string ledgerId,
        CommunityLedgerRoleAccessUpdateRequest request,
        string? userId,
        CancellationToken cancellationToken);

    Task<CommunityLedgerRoleAccessDecision> EvaluateAsync(
        커뮤니티원장Dto ledger,
        string? userId,
        CancellationToken cancellationToken);
}

public sealed class CommunityLedgerRoleAccessService : ICommunityLedgerRoleAccessService
{
    private readonly I커뮤니티원장저장소 _ledgerStore;
    private readonly ICommunityLedgerRoleAccessPolicyStore _policyStore;
    private readonly ICommunityCustomsBrokerDirectory _brokerDirectory;

    public CommunityLedgerRoleAccessService(
        I커뮤니티원장저장소 ledgerStore,
        ICommunityLedgerRoleAccessPolicyStore policyStore,
        ICommunityCustomsBrokerDirectory brokerDirectory)
    {
        _ledgerStore = ledgerStore;
        _policyStore = policyStore;
        _brokerDirectory = brokerDirectory;
    }

    public async Task<Result<CommunityLedgerRoleAccessSettingsResponse>> GetSettingsAsync(
        string ledgerId,
        string? userId,
        CancellationToken cancellationToken)
    {
        var ledger = await _ledgerStore.원장조회Async(ledgerId, cancellationToken);
        var validation = ValidateManageAccess(ledger, userId);
        if (validation is not null)
        {
            return validation;
        }

        var policy = await _policyStore.GetAsync(ledger!.원장Id, cancellationToken)
                     ?? CommunityLedgerRoleAccessPolicy.Empty(ledger);
        return Result.Ok(await BuildSettingsAsync(ledger, policy, cancellationToken));
    }

    public async Task<Result<CommunityLedgerRoleAccessSettingsResponse>> UpdateSettingsAsync(
        string ledgerId,
        CommunityLedgerRoleAccessUpdateRequest request,
        string? userId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ledger = await _ledgerStore.원장조회Async(ledgerId, cancellationToken);
        var validation = ValidateManageAccess(ledger, userId);
        if (validation is not null)
        {
            return validation;
        }

        if (request.Grants.Count > 50)
        {
            return Fail<CommunityLedgerRoleAccessSettingsResponse>("한 원장에서 조정할 수 있는 역할 권한은 최대 50건입니다.", 400);
        }

        var candidates = await _brokerDirectory.ListEligibleAsync(cancellationToken);
        var candidateById = candidates.ToDictionary(x => x.ParticipantId, StringComparer.OrdinalIgnoreCase);
        var availableNodeIds = CommunityLedgerRoleAccessPolicyEvaluator.GetAvailableNodes(ledger!)
            .Select(x => x.NodeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;
        var grants = new List<CommunityLedgerRoleGrant>();
        foreach (var requestGrant in request.Grants
                     .GroupBy(x => x.TargetUserId?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                     .Select(x => x.Last()))
        {
            if (string.IsNullOrWhiteSpace(requestGrant.TargetUserId)
                || !candidateById.TryGetValue(requestGrant.TargetUserId.Trim(), out var candidate))
            {
                return Fail<CommunityLedgerRoleAccessSettingsResponse>("승인되고 현재 수임 가능한 수입 전문 관세사만 권한 대상으로 지정할 수 있습니다.", 400);
            }

            var viewScope = CommunityLedgerNodeViewScopes.IsSupported(requestGrant.ViewScope)
                ? requestGrant.ViewScope
                : CommunityLedgerNodeViewScopes.RoleOnly;
            var visibleNodeIds = requestGrant.VisibleNodeIds
                .Where(availableNodeIds.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var effectiveVisible = CommunityLedgerRoleAccessPolicyEvaluator.ResolveVisibleNodeIds(
                ledger!,
                viewScope,
                visibleNodeIds);
            var editableNodeIds = requestGrant.EditableNodeIds
                .Where(nodeId => availableNodeIds.Contains(nodeId) && effectiveVisible.Contains(nodeId, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            grants.Add(new CommunityLedgerRoleGrant
            {
                TargetUserId = candidate.ParticipantId,
                TargetDisplayName = string.IsNullOrWhiteSpace(candidate.OfficeName)
                    ? candidate.DisplayName
                    : $"{candidate.OfficeName} · {candidate.DisplayName}",
                RoleCode = CommunityLedgerAccessRoleCodes.CustomsBroker,
                AccessEnabled = requestGrant.AccessEnabled,
                ViewScope = viewScope,
                VisibleNodeIds = visibleNodeIds,
                EditableNodeIds = requestGrant.AccessEnabled ? editableNodeIds : [],
                CanCoordinateTransport = requestGrant.AccessEnabled && requestGrant.CanCoordinateTransport,
                UpdatedAtUtc = now
            });
        }

        CommunityLedgerRoleAccessPolicy saved;
        try
        {
            saved = await _policyStore.SaveAsync(
                new CommunityLedgerRoleAccessPolicy
                {
                    LedgerId = ledger!.원장Id,
                    OwnerUserId = ledger.생성자UserId ?? userId!.Trim(),
                    Grants = grants
                },
                request.ExpectedRevision,
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Fail<CommunityLedgerRoleAccessSettingsResponse>(ex.Message, 409);
        }

        return Result.Ok(await BuildSettingsAsync(ledger!, saved, cancellationToken));
    }

    public async Task<CommunityLedgerRoleAccessDecision> EvaluateAsync(
        커뮤니티원장Dto ledger,
        string? userId,
        CancellationToken cancellationToken)
    {
        if (!CommunityLedgerRoleAccessPolicyEvaluator.IsGroupImport(ledger)
            || string.IsNullOrWhiteSpace(userId))
        {
            return CommunityLedgerRoleAccessDecision.None;
        }

        var normalizedUserId = userId.Trim();
        var canManage = CommunityLedgerRoleAccessPolicyEvaluator.CanManage(ledger, normalizedUserId);
        if (canManage)
        {
            return CommunityLedgerRoleAccessDecision.Manager;
        }

        if (!await _brokerDirectory.IsEligibleAsync(normalizedUserId, cancellationToken))
        {
            return CommunityLedgerRoleAccessDecision.None;
        }

        var policy = await _policyStore.GetAsync(ledger.원장Id, cancellationToken);
        var grant = policy?.Grants.FirstOrDefault(x =>
            string.Equals(x.TargetUserId, normalizedUserId, StringComparison.OrdinalIgnoreCase));
        if (grant is { AccessEnabled: false })
        {
            return CommunityLedgerRoleAccessDecision.None;
        }

        var viewScope = grant?.ViewScope ?? CommunityLedgerNodeViewScopes.RoleOnly;
        var visibleNodeIds = CommunityLedgerRoleAccessPolicyEvaluator.ResolveVisibleNodeIds(
            ledger,
            viewScope,
            grant?.VisibleNodeIds ?? []);
        var editableNodeIds = (grant?.EditableNodeIds ?? [])
            .Where(nodeId => visibleNodeIds.Contains(nodeId, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new CommunityLedgerRoleAccessDecision(
            HasRoleAccess: true,
            UseRoleScope: true,
            CanManage: false,
            RoleCode: CommunityLedgerAccessRoleCodes.CustomsBroker,
            RoleName: "관세사",
            VisibleNodeIds: visibleNodeIds,
            EditableNodeIds: editableNodeIds,
            CanCoordinateTransport: grant?.CanCoordinateTransport == true);
    }

    private async Task<CommunityLedgerRoleAccessSettingsResponse> BuildSettingsAsync(
        커뮤니티원장Dto ledger,
        CommunityLedgerRoleAccessPolicy policy,
        CancellationToken cancellationToken)
        => new()
        {
            LedgerId = ledger.원장Id,
            CanManage = true,
            Revision = policy.Revision,
            UpdatedAtUtc = policy.UpdatedAtUtc,
            Nodes = CommunityLedgerRoleAccessPolicyEvaluator.GetAvailableNodes(ledger),
            CustomsBrokers = await _brokerDirectory.ListEligibleAsync(cancellationToken),
            Grants = policy.Grants.Select(ToResponse).ToArray()
        };

    private static CommunityLedgerRoleGrantResponse ToResponse(CommunityLedgerRoleGrant grant)
        => new()
        {
            TargetUserId = grant.TargetUserId,
            TargetDisplayName = grant.TargetDisplayName,
            RoleCode = grant.RoleCode,
            AccessEnabled = grant.AccessEnabled,
            ViewScope = grant.ViewScope,
            VisibleNodeIds = grant.VisibleNodeIds,
            EditableNodeIds = grant.EditableNodeIds,
            CanCoordinateTransport = grant.CanCoordinateTransport,
            UpdatedAtUtc = grant.UpdatedAtUtc
        };

    private static Result<CommunityLedgerRoleAccessSettingsResponse>? ValidateManageAccess(
        커뮤니티원장Dto? ledger,
        string? userId)
    {
        if (ledger is null)
        {
            return Fail<CommunityLedgerRoleAccessSettingsResponse>("공동수입 원장을 찾을 수 없습니다.", 404);
        }

        if (!CommunityLedgerRoleAccessPolicyEvaluator.IsGroupImport(ledger))
        {
            return Fail<CommunityLedgerRoleAccessSettingsResponse>("역할별 관세사 권한은 공동수입 원장에서만 설정할 수 있습니다.", 400);
        }

        return CommunityLedgerRoleAccessPolicyEvaluator.CanManage(ledger, userId)
            ? null
            : Fail<CommunityLedgerRoleAccessSettingsResponse>("원장 생성자 또는 지정된 구매·수입 담당자만 역할 권한을 변경할 수 있습니다.", 403);
    }

    private static Result<T> Fail<T>(string message, int statusCode)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", statusCode));
}

public static class CommunityLedgerRoleAccessPolicyEvaluator
{
    private static readonly string[] ManagerRoleHints =
    [
        "구매 담당", "구매대표", "구매 대표", "공동구매 대표", "수입 결정", "수입 연결"
    ];

    private static readonly string[] CustomsNodeHints =
    [
        "customs", "clearance", "hs code", "hs-code", "통관", "관세", "세관", "수입신고",
        "해외 선적", "선적", "문서관리번호", "인보이스", "패킹", "awb", "b/l", "반출"
    ];

    public static bool IsGroupImport(커뮤니티원장Dto ledger)
        => string.Equals(
            ledger.원장템플릿Key,
            CommunityLedgerTemplateKeys.GroupImport,
            StringComparison.OrdinalIgnoreCase);

    public static bool CanManage(커뮤니티원장Dto ledger, string? userId)
        => !string.IsNullOrWhiteSpace(userId)
           && (string.Equals(ledger.생성자UserId, userId.Trim(), StringComparison.OrdinalIgnoreCase)
               || ledger.참여자목록.Any(participant =>
                   string.Equals(participant.UserId, userId.Trim(), StringComparison.OrdinalIgnoreCase)
                   && ManagerRoleHints.Any(hint => participant.RoleLabel.Contains(hint, StringComparison.OrdinalIgnoreCase))));

    public static IReadOnlyList<CommunityLedgerRoleAccessNodeResponse> GetAvailableNodes(커뮤니티원장Dto ledger)
    {
        var nodes = ledger.다이어그램스냅샷?.Nodes
            .Select(node => new CommunityLedgerRoleAccessNodeResponse
            {
                NodeId = node.NodeId,
                Title = node.Title,
                Kind = node.Kind,
                IsCustomsRoleNode = IsCustomsRoleNode(node.NodeId, node.Kind, node.Title, node.GroupLabel, node.Description)
            })
            .ToArray();
        if (nodes is { Length: > 0 })
        {
            return nodes;
        }

        return ledger.블록목록.Select(block => new CommunityLedgerRoleAccessNodeResponse
        {
            NodeId = block.BlockId,
            Title = block.Title,
            Kind = block.BlockType,
            IsCustomsRoleNode = IsCustomsRoleNode(block.BlockId, block.BlockType, block.Title, null, null)
        }).ToArray();
    }

    public static IReadOnlyList<string> ResolveVisibleNodeIds(
        커뮤니티원장Dto ledger,
        string viewScope,
        IReadOnlyCollection<string> selectedNodeIds)
    {
        var nodes = GetAvailableNodes(ledger);
        var availableIds = nodes.Select(x => x.NodeId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return viewScope switch
        {
            CommunityLedgerNodeViewScopes.EntireDiagram => availableIds.ToArray(),
            CommunityLedgerNodeViewScopes.SelectedNodes => selectedNodeIds
                .Where(availableIds.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            _ => nodes
                .Where(x => x.IsCustomsRoleNode)
                .Select(x => x.NodeId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    public static bool IsCustomsRoleNode(params string?[] values)
        => values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Any(value => CustomsNodeHints.Any(hint => value!.Contains(hint, StringComparison.OrdinalIgnoreCase)));
}

public sealed class CommunityLedgerRoleAccessPolicy
{
    public string LedgerId { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public IReadOnlyList<CommunityLedgerRoleGrant> Grants { get; set; } = [];
    public long Revision { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public static CommunityLedgerRoleAccessPolicy Empty(커뮤니티원장Dto ledger)
        => new() { LedgerId = ledger.원장Id, OwnerUserId = ledger.생성자UserId ?? string.Empty };
}

public sealed class CommunityLedgerRoleGrant
{
    public string TargetUserId { get; set; } = string.Empty;
    public string TargetDisplayName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = CommunityLedgerAccessRoleCodes.CustomsBroker;
    public bool AccessEnabled { get; set; } = true;
    public string ViewScope { get; set; } = CommunityLedgerNodeViewScopes.RoleOnly;
    public IReadOnlyList<string> VisibleNodeIds { get; set; } = [];
    public IReadOnlyList<string> EditableNodeIds { get; set; } = [];
    public bool CanCoordinateTransport { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed record CommunityLedgerRoleAccessDecision(
    bool HasRoleAccess,
    bool UseRoleScope,
    bool CanManage,
    string RoleCode,
    string RoleName,
    IReadOnlyList<string> VisibleNodeIds,
    IReadOnlyList<string> EditableNodeIds,
    bool CanCoordinateTransport)
{
    public static CommunityLedgerRoleAccessDecision None { get; } =
        new(false, false, false, string.Empty, string.Empty, [], [], false);

    public static CommunityLedgerRoleAccessDecision Manager { get; } =
        new(false, false, true, string.Empty, string.Empty, [], [], false);
}

internal sealed class CommunityLedgerRoleAccessPolicyDocument
{
    [BsonId]
    public ObjectId Id { get; set; }
    public string LedgerId { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public IReadOnlyList<CommunityLedgerRoleGrantDocument> Grants { get; set; } = [];
    public long Revision { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

internal sealed class CommunityLedgerRoleGrantDocument
{
    public string TargetUserId { get; set; } = string.Empty;
    public string TargetDisplayName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = CommunityLedgerAccessRoleCodes.CustomsBroker;
    public bool AccessEnabled { get; set; } = true;
    public string ViewScope { get; set; } = CommunityLedgerNodeViewScopes.RoleOnly;
    public IReadOnlyList<string> VisibleNodeIds { get; set; } = [];
    public IReadOnlyList<string> EditableNodeIds { get; set; } = [];
    public bool CanCoordinateTransport { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
