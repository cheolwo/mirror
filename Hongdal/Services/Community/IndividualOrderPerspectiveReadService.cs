using FluentResults;
using Hongdal.Application.CommandProcessing;
using Hongdal.Contracts.Common.Community;
using Microsoft.AspNetCore.Http;

namespace Hongdal.Services.Community;

public interface IIndividualOrderPerspectiveReadService
{
    Task<Result<개별주문관점페이지응답>> QueryAsync(
        string perspectiveCode,
        string? communityLedgerId,
        개별주문관점목록조회요청 request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 역할 클레임이 아니라 개별 주문 루트와 판매·창고·운송·공동 원장의 실제 연결 관계로
/// 현재 사용자가 읽을 수 있는 주문 목록을 계산합니다.
/// </summary>
public sealed class IndividualOrderPerspectiveReadService(
    I커뮤니티원장저장소 ledgerStore,
    I주문원장역할별조회Service roleQueryService,
    ICurrentUserAccessor currentUserAccessor) : IIndividualOrderPerspectiveReadService
{
    private static readonly string[] OrderRootTemplateKeys =
    [
        CommunityLedgerTemplateKeys.Order,
        CommunityLedgerTemplateKeys.FoodOrder,
        CommunityLedgerTemplateKeys.HongdalMart
    ];

    public async Task<Result<개별주문관점페이지응답>> QueryAsync(
        string perspectiveCode,
        string? communityLedgerId,
        개별주문관점목록조회요청 request,
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
            return Failure("지원하지 않는 개별 주문 관점입니다.", StatusCodes.Status400BadRequest);
        }

        Result<IReadOnlyList<OrderCandidate>> candidatesResult;
        if (string.Equals(perspective, 개별주문관점코드.공동원장, StringComparison.OrdinalIgnoreCase))
        {
            candidatesResult = await QueryCommunityLedgerOrdersAsync(
                Clean(communityLedgerId),
                userId,
                cancellationToken);
        }
        else
        {
            candidatesResult = Result.Ok(await QueryRoleOrdersAsync(
                perspective!,
                userId,
                cancellationToken));
        }

        if (candidatesResult.IsFailed)
        {
            return Result.Fail<개별주문관점페이지응답>(candidatesResult.Errors);
        }

        var page = Math.Max(0, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = candidatesResult.Value
            .Select(ToResponse)
            .Where(item => Matches(item, request))
            .ToArray();
        var ordered = Order(items, request.SortBy, request.SortDescending);
        var totalCount = ordered.Length;
        var skip = (int)Math.Min((long)page * pageSize, int.MaxValue);

        return Result.Ok(new 개별주문관점페이지응답
        {
            Items = ordered.Skip(skip).Take(pageSize).ToArray(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    private async Task<IReadOnlyList<OrderCandidate>> QueryRoleOrdersAsync(
        string perspective,
        string userId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<커뮤니티원장Dto> roots;
        if (string.Equals(perspective, 개별주문관점코드.주문자, StringComparison.OrdinalIgnoreCase))
        {
            roots = await ledgerStore.원장목록조회Async(
                new 커뮤니티원장조회조건
                {
                    접근UserId = userId,
                    원장템플릿Keys = OrderRootTemplateKeys,
                    Limit = 200
                },
                cancellationToken);
        }
        else
        {
            var accessible = await ledgerStore.원장목록조회Async(
                new 커뮤니티원장조회조건
                {
                    접근UserId = userId,
                    Limit = 200
                },
                cancellationToken);
            var accessibleIds = accessible
                .Where(item => !주문원장구성정책.주문루트인가(item.원장템플릿Key))
                .Select(item => item.원장Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (accessibleIds.Count == 0)
            {
                return [];
            }

            var rootCandidates = await ledgerStore.원장목록조회Async(
                new 커뮤니티원장조회조건
                {
                    원장템플릿Keys = OrderRootTemplateKeys,
                    포함원장Ids = accessibleIds.ToArray(),
                    Limit = 200
                },
                cancellationToken);
            roots = rootCandidates
                .Where(root => 주문원장구성정책.주문루트인가(root.원장템플릿Key))
                .Where(root => root.포함원장목록.Any(reference =>
                    accessibleIds.Contains(reference.원장Id)
                    && RoleMatches(perspective, reference.역할)))
                .ToArray();
        }

        var queryRole = ToOrderLedgerRole(perspective);
        var results = new List<OrderCandidate>();
        foreach (var root in roots
                     .GroupBy(item => item.원장Id, StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.First()))
        {
            var view = await roleQueryService.조회Async(root.원장Id, userId, queryRole, cancellationToken);
            if (view.IsSuccess)
            {
                results.Add(new OrderCandidate(root, perspective, view.Value, null));
            }
        }

        return results;
    }

    private async Task<Result<IReadOnlyList<OrderCandidate>>> QueryCommunityLedgerOrdersAsync(
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
            return CandidateFailure("공동 원장의 생성자 또는 참여자만 개별 주문을 조회할 수 있습니다.", StatusCodes.Status403Forbidden);
        }

        var roots = new Dictionary<string, 커뮤니티원장Dto>(StringComparer.OrdinalIgnoreCase);
        await CollectIndividualOrdersAsync(communityLedger, roots, 0, cancellationToken);
        IReadOnlyList<OrderCandidate> result = roots.Values
            .Select(root => new OrderCandidate(
                root,
                개별주문관점코드.공동원장,
                null,
                communityLedger.원장Id))
            .ToArray();
        return Result.Ok(result);
    }

    private async Task CollectIndividualOrdersAsync(
        커뮤니티원장Dto container,
        IDictionary<string, 커뮤니티원장Dto> roots,
        int depth,
        CancellationToken cancellationToken)
    {
        if (depth > 2 || container.포함원장목록.Count == 0)
        {
            return;
        }

        var references = container.포함원장목록
            .Where(reference => string.Equals(reference.역할, 주문원장포함역할.개별주문, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(reference.역할, 주문원장포함역할.주문집계, StringComparison.OrdinalIgnoreCase)
                                || 주문원장구성정책.주문루트인가(reference.원장템플릿Key)
                                || string.Equals(reference.원장템플릿Key, CommunityLedgerTemplateKeys.GroupOrder, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var tasks = references.Select(reference => ledgerStore.원장조회Async(reference.원장Id, cancellationToken));
        var children = await Task.WhenAll(tasks);
        foreach (var child in children.Where(child => child is not null).Cast<커뮤니티원장Dto>())
        {
            if (주문원장구성정책.주문루트인가(child.원장템플릿Key))
            {
                roots[child.원장Id] = child;
            }
            else if (string.Equals(child.원장템플릿Key, CommunityLedgerTemplateKeys.GroupOrder, StringComparison.OrdinalIgnoreCase)
                     || 주문원장구성정책.공동구매인가(child.원장템플릿Key))
            {
                await CollectIndividualOrdersAsync(child, roots, depth + 1, cancellationToken);
            }
        }
    }

    private static 개별주문관점항목응답 ToResponse(OrderCandidate candidate)
    {
        var fullRoot = candidate.RoleView?.주문원장상세;
        var communityView = string.Equals(
            candidate.Perspective,
            개별주문관점코드.공동원장,
            StringComparison.OrdinalIgnoreCase);
        var roles = candidate.RoleView?.관련원장목록
            .Select(item => item.주문안역할)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? candidate.Root.포함원장목록
                .Select(item => item.역할)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        return new 개별주문관점항목응답
        {
            주문원장Id = candidate.Root.원장Id,
            Revision = candidate.Root.Revision,
            원장템플릿Key = candidate.Root.원장템플릿Key,
            제목 = fullRoot?.제목
                 ?? (communityView ? candidate.Root.제목 : $"개별 주문 {candidate.Root.원장Id}"),
            상태 = candidate.RoleView?.주문원장상태 ?? candidate.Root.상태,
            현재단계Key = fullRoot?.현재단계Key ?? (communityView ? candidate.Root.현재단계Key : null),
            주문자표시명 = fullRoot?.생성자표시명,
            관계코드 = candidate.Perspective,
            조회근거 = communityView ? "공동원장참여" : candidate.RoleView?.주문원장조회근거 ?? string.Empty,
            공동원장Id = candidate.CommunityLedgerId,
            관련원장역할목록 = roles,
            관련하위원장수 = candidate.RoleView?.관련원장목록.Count ?? candidate.Root.포함원장목록.Count,
            상세공개요청필요수 = candidate.RoleView?.상세공개요청필요수 ?? 0,
            생성시각Utc = candidate.Root.생성시각Utc,
            수정시각Utc = candidate.Root.수정시각Utc
        };
    }

    private static bool Matches(개별주문관점항목응답 item, 개별주문관점목록조회요청 request)
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
        return item.주문원장Id.Contains(search, StringComparison.OrdinalIgnoreCase)
               || item.제목.Contains(search, StringComparison.OrdinalIgnoreCase)
               || item.상태.Contains(search, StringComparison.OrdinalIgnoreCase)
               || item.관련원장역할목록.Any(role => role.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private static 개별주문관점항목응답[] Order(
        IReadOnlyList<개별주문관점항목응답> items,
        string? sortBy,
        bool descending)
    {
        Func<개별주문관점항목응답, object?> selector = sortBy?.Trim() switch
        {
            nameof(개별주문관점항목응답.주문원장Id) => item => item.주문원장Id,
            nameof(개별주문관점항목응답.제목) => item => item.제목,
            nameof(개별주문관점항목응답.상태) => item => item.상태,
            nameof(개별주문관점항목응답.생성시각Utc) => item => item.생성시각Utc,
            _ => item => item.수정시각Utc
        };
        return (descending
                ? items.OrderByDescending(selector)
                : items.OrderBy(selector))
            .ToArray();
    }

    private static bool RoleMatches(string perspective, string role)
        => perspective switch
        {
            개별주문관점코드.판매자 => string.Equals(role, 주문원장포함역할.판매, StringComparison.OrdinalIgnoreCase),
            개별주문관점코드.창고관리자 => string.Equals(role, 주문원장포함역할.창고입고, StringComparison.OrdinalIgnoreCase)
                                             || string.Equals(role, 주문원장포함역할.창고출고, StringComparison.OrdinalIgnoreCase),
            개별주문관점코드.운송담당자 => string.Equals(role, 주문원장포함역할.배송, StringComparison.OrdinalIgnoreCase)
                                           || string.Equals(role, 주문원장포함역할.운송, StringComparison.OrdinalIgnoreCase),
            _ => false
        };

    private static string ToOrderLedgerRole(string perspective)
        => perspective switch
        {
            개별주문관점코드.주문자 => 주문원장조회역할.주문자,
            개별주문관점코드.판매자 => 주문원장조회역할.판매자,
            개별주문관점코드.창고관리자 => 주문원장조회역할.창고담당자,
            개별주문관점코드.운송담당자 => 주문원장조회역할.운송담당자,
            _ => throw new InvalidOperationException($"역할별 주문 원장 조회를 지원하지 않는 관점입니다: {perspective}")
        };

    private static bool SupportedPerspective(string? perspective)
        => perspective is not null
           && new[]
           {
               개별주문관점코드.주문자,
               개별주문관점코드.판매자,
               개별주문관점코드.창고관리자,
               개별주문관점코드.운송담당자,
               개별주문관점코드.공동원장
           }.Contains(perspective, StringComparer.OrdinalIgnoreCase);

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<개별주문관점페이지응답> Failure(string message, int statusCode)
        => Result.Fail<개별주문관점페이지응답>(new Error(message).WithMetadata("StatusCode", statusCode));

    private static Result<IReadOnlyList<OrderCandidate>> CandidateFailure(string message, int statusCode)
        => Result.Fail<IReadOnlyList<OrderCandidate>>(new Error(message).WithMetadata("StatusCode", statusCode));

    private sealed record OrderCandidate(
        커뮤니티원장Dto Root,
        string Perspective,
        주문원장역할별조회Dto? RoleView,
        string? CommunityLedgerId);
}
