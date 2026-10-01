using FluentResults;
using Hongdal.Application.CommandProcessing;
using Hongdal.Contracts.Common.Community;
using Microsoft.AspNetCore.Http;

namespace Hongdal.Services.Community;

public interface IGroupOrderPerspectiveReadService
{
    Task<Result<공동주문관점페이지응답>> QueryAsync(
        string perspectiveCode,
        string? communityLedgerId,
        공동주문관점목록조회요청 request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 공동주문을 독립 입력값이 아닌 개별 주문 원장의 집합으로 읽고,
/// 주문자 또는 판매·창고·운송 하위 원장의 실제 참여 관계로 목록 범위를 제한합니다.
/// </summary>
public sealed class GroupOrderPerspectiveReadService(
    I커뮤니티원장저장소 ledgerStore,
    I주문원장통합UseCase integrationUseCase,
    ICurrentUserAccessor currentUserAccessor) : IGroupOrderPerspectiveReadService
{
    private static readonly string[] OrderRootTemplateKeys =
    [
        CommunityLedgerTemplateKeys.Order,
        CommunityLedgerTemplateKeys.FoodOrder,
        CommunityLedgerTemplateKeys.HongdalMart
    ];

    public async Task<Result<공동주문관점페이지응답>> QueryAsync(
        string perspectiveCode,
        string? communityLedgerId,
        공동주문관점목록조회요청 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = Clean(currentUserAccessor.UserId);
        if (userId is null)
        {
            return Failure("로그인 사용자를 확인할 수 없습니다.", StatusCodes.Status401Unauthorized);
        }

        var perspective = Clean(perspectiveCode);
        if (!SupportedPerspective(perspective))
        {
            return Failure("지원하지 않는 공동주문 관점입니다.", StatusCodes.Status400BadRequest);
        }

        Result<IReadOnlyList<GroupOrderCandidate>> candidatesResult;
        if (string.Equals(perspective, 공동주문관점코드.공동원장, StringComparison.OrdinalIgnoreCase))
        {
            candidatesResult = await QueryCommunityLedgerGroupOrdersAsync(
                Clean(communityLedgerId),
                userId,
                cancellationToken);
        }
        else
        {
            candidatesResult = Result.Ok(await QueryRoleGroupOrdersAsync(
                perspective!,
                userId,
                cancellationToken));
        }

        if (candidatesResult.IsFailed)
        {
            return Result.Fail<공동주문관점페이지응답>(candidatesResult.Errors);
        }

        var responses = new List<공동주문관점항목응답>();
        foreach (var candidate in candidatesResult.Value
                     .GroupBy(item => item.GroupOrder.원장Id, StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.First()))
        {
            var integrated = await integrationUseCase.조회Async(candidate.GroupOrder.원장Id, cancellationToken);
            if (integrated.IsSuccess)
            {
                responses.Add(ToResponse(candidate, integrated.Value));
            }
        }

        var page = Math.Max(0, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = responses.Where(item => Matches(item, request)).ToArray();
        var ordered = Order(items, request.SortBy, request.SortDescending);
        var skip = (int)Math.Min((long)page * pageSize, int.MaxValue);

        return Result.Ok(new 공동주문관점페이지응답
        {
            Items = ordered.Skip(skip).Take(pageSize).ToArray(),
            TotalCount = ordered.Length,
            Page = page,
            PageSize = pageSize
        });
    }

    private async Task<IReadOnlyList<GroupOrderCandidate>> QueryRoleGroupOrdersAsync(
        string perspective,
        string userId,
        CancellationToken cancellationToken)
    {
        var candidates = new Dictionary<string, GroupOrderCandidate>(StringComparer.OrdinalIgnoreCase);
        if (string.Equals(perspective, 공동주문관점코드.주문자, StringComparison.OrdinalIgnoreCase))
        {
            var directGroups = await ledgerStore.원장목록조회Async(
                new 커뮤니티원장조회조건
                {
                    접근UserId = userId,
                    원장템플릿Key = CommunityLedgerTemplateKeys.GroupOrder,
                    Limit = 200
                },
                cancellationToken);
            foreach (var group in directGroups.Where(IsGroupOrder))
            {
                candidates[group.원장Id] = new GroupOrderCandidate(group, perspective, "공동주문직접참여", null);
            }

            var individualOrders = await ledgerStore.원장목록조회Async(
                new 커뮤니티원장조회조건
                {
                    접근UserId = userId,
                    원장템플릿Keys = OrderRootTemplateKeys,
                    Limit = 200
                },
                cancellationToken);
            await AddGroupsContainingOrdersAsync(
                candidates,
                individualOrders.Select(item => item.원장Id),
                perspective,
                "개별주문참여",
                cancellationToken);
            return candidates.Values.ToArray();
        }

        var accessibleWorkLedgers = await ledgerStore.원장목록조회Async(
            new 커뮤니티원장조회조건
            {
                접근UserId = userId,
                Limit = 200
            },
            cancellationToken);
        var workLedgerIds = accessibleWorkLedgers
            .Where(item => !주문원장구성정책.주문루트인가(item.원장템플릿Key)
                           && !IsGroupOrder(item))
            .Select(item => item.원장Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (workLedgerIds.Count == 0)
        {
            return [];
        }

        var individualOrderCandidates = await ledgerStore.원장목록조회Async(
            new 커뮤니티원장조회조건
            {
                원장템플릿Keys = OrderRootTemplateKeys,
                포함원장Ids = workLedgerIds.ToArray(),
                Limit = 200
            },
            cancellationToken);
        var relatedOrderIds = individualOrderCandidates
            .Where(order => 주문원장구성정책.주문루트인가(order.원장템플릿Key))
            .Where(order => order.포함원장목록.Any(reference =>
                workLedgerIds.Contains(reference.원장Id)
                && RoleMatches(perspective, reference.역할)))
            .Select(order => order.원장Id)
            .ToArray();
        await AddGroupsContainingOrdersAsync(
            candidates,
            relatedOrderIds,
            perspective,
            "개별주문하위원장참여",
            cancellationToken);
        return candidates.Values.ToArray();
    }

    private async Task AddGroupsContainingOrdersAsync(
        IDictionary<string, GroupOrderCandidate> target,
        IEnumerable<string> individualOrderIds,
        string perspective,
        string accessBasis,
        CancellationToken cancellationToken)
    {
        var orderIds = individualOrderIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (orderIds.Length == 0)
        {
            return;
        }

        var groups = await ledgerStore.원장목록조회Async(
            new 커뮤니티원장조회조건
            {
                원장템플릿Key = CommunityLedgerTemplateKeys.GroupOrder,
                포함원장Ids = orderIds,
                Limit = 200
            },
            cancellationToken);
        var orderIdSet = orderIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups.Where(IsGroupOrder).Where(group => group.포함원장목록.Any(reference =>
                     orderIdSet.Contains(reference.원장Id)
                     && string.Equals(reference.역할, 주문원장포함역할.개별주문, StringComparison.OrdinalIgnoreCase))))
        {
            target[group.원장Id] = new GroupOrderCandidate(group, perspective, accessBasis, null);
        }
    }

    private async Task<Result<IReadOnlyList<GroupOrderCandidate>>> QueryCommunityLedgerGroupOrdersAsync(
        string? communityLedgerId,
        string userId,
        CancellationToken cancellationToken)
    {
        if (communityLedgerId is null)
        {
            return CandidateFailure("조회할 공동 원장을 선택해 주세요.", StatusCodes.Status400BadRequest);
        }

        var communityLedger = await ledgerStore.원장조회Async(communityLedgerId, cancellationToken);
        if (communityLedger is null)
        {
            return CandidateFailure("공동 원장을 찾을 수 없습니다.", StatusCodes.Status404NotFound);
        }

        if (!주문원장역할별조회Service.직접접근가능(communityLedger, userId))
        {
            return CandidateFailure("공동 원장의 생성자 또는 참여자만 공동주문을 조회할 수 있습니다.", StatusCodes.Status403Forbidden);
        }

        if (IsGroupOrder(communityLedger))
        {
            IReadOnlyList<GroupOrderCandidate> direct =
            [
                new(communityLedger, 공동주문관점코드.공동원장, "공동원장참여", communityLedger.원장Id)
            ];
            return Result.Ok(direct);
        }

        var groups = new Dictionary<string, 커뮤니티원장Dto>(StringComparer.OrdinalIgnoreCase);
        await CollectGroupOrdersAsync(communityLedger, groups, 0, cancellationToken);
        IReadOnlyList<GroupOrderCandidate> result = groups.Values
            .Select(group => new GroupOrderCandidate(
                group,
                공동주문관점코드.공동원장,
                "공동원장참여",
                communityLedger.원장Id))
            .ToArray();
        return Result.Ok(result);
    }

    private async Task CollectGroupOrdersAsync(
        커뮤니티원장Dto container,
        IDictionary<string, 커뮤니티원장Dto> groups,
        int depth,
        CancellationToken cancellationToken)
    {
        if (depth > 2 || container.포함원장목록.Count == 0)
        {
            return;
        }

        var references = container.포함원장목록
            .Where(reference => string.Equals(reference.역할, 주문원장포함역할.주문집계, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(reference.원장템플릿Key, CommunityLedgerTemplateKeys.GroupOrder, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(reference.원장템플릿Key, CommunityLedgerTemplateKeys.GroupPurchase, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(reference.원장템플릿Key, CommunityLedgerTemplateKeys.GroupImport, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var children = await Task.WhenAll(references.Select(reference =>
            ledgerStore.원장조회Async(reference.원장Id, cancellationToken)));
        foreach (var child in children.Where(item => item is not null).Cast<커뮤니티원장Dto>())
        {
            if (IsGroupOrder(child))
            {
                groups[child.원장Id] = child;
            }
            else
            {
                await CollectGroupOrdersAsync(child, groups, depth + 1, cancellationToken);
            }
        }
    }

    private static 공동주문관점항목응답 ToResponse(
        GroupOrderCandidate candidate,
        주문원장통합Dto integrated)
    {
        var root = integrated.주문원장;
        var individualOrders = integrated.포함원장목록
            .Where(item => string.Equals(
                item.역할,
                주문원장포함역할.개별주문,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return new 공동주문관점항목응답
        {
            공동주문원장Id = root.원장Id,
            Revision = root.Revision,
            제목 = root.제목,
            상태 = root.상태,
            현재단계Key = root.현재단계Key,
            관계코드 = candidate.Perspective,
            조회근거 = candidate.AccessBasis,
            공동원장Id = candidate.CommunityLedgerId ?? External(root, "SourceGroupPurchaseLedgerId"),
            자동집단Id = External(root, "AutomaticGroupId"),
            상품키 = External(root, "ProductKey"),
            상품명 = External(root, "ProductName"),
            개별주문수 = individualOrders.Length,
            완료개별주문수 = individualOrders.Count(item => item.원장?.상태 == 커뮤니티원장상태.완료),
            필수개별주문완료여부 = individualOrders
                .Where(item => item.필수여부)
                .All(item => item.원장?.상태 == 커뮤니티원장상태.완료),
            서명대상주문수 = integrated.서명대상주문수,
            서명완료주문수 = integrated.서명완료주문수,
            전체주문서명완료여부 = integrated.전체주문서명완료여부,
            미서명주문Ids = integrated.미서명주문Ids,
            생성시각Utc = root.생성시각Utc,
            수정시각Utc = root.수정시각Utc
        };
    }

    private static bool Matches(공동주문관점항목응답 item, 공동주문관점목록조회요청 request)
    {
        if (!string.IsNullOrWhiteSpace(request.Status)
            && !string.Equals(item.상태, request.Status.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Search))
        {
            return true;
        }

        var search = request.Search.Trim();
        return item.공동주문원장Id.Contains(search, StringComparison.OrdinalIgnoreCase)
               || item.제목.Contains(search, StringComparison.OrdinalIgnoreCase)
               || item.상태.Contains(search, StringComparison.OrdinalIgnoreCase)
               || (item.상품명?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
               || (item.상품키?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static 공동주문관점항목응답[] Order(
        IReadOnlyList<공동주문관점항목응답> items,
        string? sortBy,
        bool descending)
    {
        Func<공동주문관점항목응답, object?> selector = sortBy?.Trim() switch
        {
            nameof(공동주문관점항목응답.공동주문원장Id) => item => item.공동주문원장Id,
            nameof(공동주문관점항목응답.제목) => item => item.제목,
            nameof(공동주문관점항목응답.상태) => item => item.상태,
            nameof(공동주문관점항목응답.개별주문수) => item => item.개별주문수,
            nameof(공동주문관점항목응답.생성시각Utc) => item => item.생성시각Utc,
            _ => item => item.수정시각Utc
        };
        return (descending ? items.OrderByDescending(selector) : items.OrderBy(selector)).ToArray();
    }

    private static bool RoleMatches(string perspective, string role)
        => perspective switch
        {
            공동주문관점코드.판매자 => string.Equals(role, 주문원장포함역할.판매, StringComparison.OrdinalIgnoreCase),
            공동주문관점코드.창고관리자 => string.Equals(role, 주문원장포함역할.창고입고, StringComparison.OrdinalIgnoreCase)
                                           || string.Equals(role, 주문원장포함역할.창고출고, StringComparison.OrdinalIgnoreCase),
            공동주문관점코드.운송담당자 => string.Equals(role, 주문원장포함역할.배송, StringComparison.OrdinalIgnoreCase)
                                         || string.Equals(role, 주문원장포함역할.운송, StringComparison.OrdinalIgnoreCase),
            _ => false
        };

    private static bool SupportedPerspective(string? perspective)
        => perspective is not null
           && new[]
           {
               공동주문관점코드.주문자,
               공동주문관점코드.판매자,
               공동주문관점코드.창고관리자,
               공동주문관점코드.운송담당자,
               공동주문관점코드.공동원장
           }.Contains(perspective, StringComparer.OrdinalIgnoreCase);

    private static bool IsGroupOrder(커뮤니티원장Dto ledger)
        => string.Equals(ledger.원장템플릿Key, CommunityLedgerTemplateKeys.GroupOrder, StringComparison.OrdinalIgnoreCase);

    private static string? External(커뮤니티원장Dto ledger, string key)
        => ledger.외부참조.TryGetValue(key, out var value) ? Clean(value) : null;

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<공동주문관점페이지응답> Failure(string message, int statusCode)
        => Result.Fail<공동주문관점페이지응답>(new Error(message).WithMetadata("StatusCode", statusCode));

    private static Result<IReadOnlyList<GroupOrderCandidate>> CandidateFailure(string message, int statusCode)
        => Result.Fail<IReadOnlyList<GroupOrderCandidate>>(new Error(message).WithMetadata("StatusCode", statusCode));

    private sealed record GroupOrderCandidate(
        커뮤니티원장Dto GroupOrder,
        string Perspective,
        string AccessBasis,
        string? CommunityLedgerId);
}
