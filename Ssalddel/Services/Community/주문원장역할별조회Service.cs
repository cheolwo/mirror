using FluentResults;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Application.Warehouse;

namespace Ssalddel.Services.Community;

public static class 주문원장조회역할
{
    public const string 주문자 = "주문자";
    public const string 판매자 = "판매자";
    public const string 창고담당자 = "창고담당자";
    public const string 운송담당자 = "운송담당자";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        주문자,
        판매자,
        창고담당자,
        운송담당자
    };
}

public static class 원장조회근거
{
    public const string 소유자 = "소유자";
    public const string 직접참여 = "직접참여";
    public const string 승인공개 = "승인공개";
    public const string 공개요청필요 = "공개요청필요";
    public const string 원장누락 = "원장누락";
}

public interface I주문원장역할별조회Service
{
    Task<Result<주문원장역할별조회Dto>> 조회Async(
        string 주문원장Id,
        string 현재UserId,
        string 조회역할,
        CancellationToken cancellationToken = default);
}

[SsalddelApiWorkflow(SsalddelWorkflow.CommunityTrust)]
[SsalddelUseCase("주문 원장 역할별 조회", Summary = "주문 원장을 기점으로 역할에 필요한 하위 원장만 조합하고 미승인 원장 상세를 가립니다.")]
public sealed class 주문원장역할별조회Service : I주문원장역할별조회Service
{
    private readonly I커뮤니티원장저장소 _원장저장소;
    private readonly I주문원장공개요청저장소 _공개요청저장소;
    private readonly I분산주문원장연결Adapter? _분산연결;

    public 주문원장역할별조회Service(
        I커뮤니티원장저장소 원장저장소,
        I주문원장공개요청저장소 공개요청저장소,
        I분산주문원장연결Adapter? 분산연결 = null)
    {
        _원장저장소 = 원장저장소;
        _공개요청저장소 = 공개요청저장소;
        _분산연결 = 분산연결;
    }

    public async Task<Result<주문원장역할별조회Dto>> 조회Async(
        string 주문원장Id,
        string 현재UserId,
        string 조회역할,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(주문원장Id) || string.IsNullOrWhiteSpace(현재UserId))
        {
            return BadRequest("주문 원장 ID와 사용자 ID가 필요합니다.");
        }

        if (!주문원장조회역할.All.Contains(조회역할))
        {
            return BadRequest("지원하지 않는 주문 원장 조회 역할입니다.");
        }

        var root = await _원장저장소.원장조회Async(주문원장Id.Trim(), cancellationToken);
        if (root is null)
        {
            return NotFound("주문 원장을 찾을 수 없습니다.");
        }

        if (!주문원장구성정책.주문루트인가(root.원장템플릿Key))
        {
            return BadRequest("역할별 조회는 개별 주문 원장을 기점으로 시작해야 합니다.");
        }

        var references = root.포함원장목록
            .Where(x => 역할에필요한참조인가(조회역할, x.역할))
            .OrderBy(x => x.표시순서)
            .ToArray();
        var childTasks = references
            .Select(x => _원장저장소.원장조회Async(x.원장Id, cancellationToken))
            .ToArray();
        var children = childTasks.Length == 0
            ? []
            : await Task.WhenAll(childTasks);
        var targetIds = references.Select(x => x.원장Id).Append(root.원장Id).ToArray();
        var grantedIds = await _공개요청저장소.승인된대상원장Ids조회Async(
            root.원장Id,
            현재UserId,
            targetIds,
            DateTimeOffset.UtcNow,
            cancellationToken);

        var rootDirect = 직접접근가능(root, 현재UserId);
        var roleChildDirect = children.Any(x => x is not null && 역할참여자인가(x, 현재UserId, 조회역할));
        var roleChildGranted = references.Any(x => grantedIds.Contains(x.원장Id));
        var canEnter = string.Equals(조회역할, 주문원장조회역할.주문자, StringComparison.OrdinalIgnoreCase)
            ? rootDirect
            : roleChildDirect || roleChildGranted;
        if (!canEnter)
        {
            return Forbidden("현재 사용자는 요청한 역할로 이 주문 원장을 조회할 수 없습니다.");
        }

        var rootFullAccess = (string.Equals(조회역할, 주문원장조회역할.주문자, StringComparison.OrdinalIgnoreCase)
                              && rootDirect)
                             || grantedIds.Contains(root.원장Id);
        var items = references.Select((reference, index) =>
        {
            var child = children[index];
            var direct = child is not null && 직접접근가능(child, 현재UserId);
            var granted = grantedIds.Contains(reference.원장Id);
            var accessBasis = child is null
                ? 원장조회근거.원장누락
                : direct ? 직접접근근거(child, 현재UserId) : granted ? 원장조회근거.승인공개 : 원장조회근거.공개요청필요;

            return new 주문역할별원장항목Dto
            {
                원장Id = reference.원장Id,
                원장템플릿Key = reference.원장템플릿Key,
                주문안역할 = reference.역할,
                필수여부 = reference.필수여부,
                표시순서 = reference.표시순서,
                조회근거 = accessBasis,
                상세조회가능여부 = direct || granted,
                공개요청가능여부 = child is not null && !direct && !granted && !string.IsNullOrWhiteSpace(child.생성자UserId),
                원장상세 = direct || granted ? child : null
            };
        }).ToArray();

        var readRoot = rootFullAccess ? FoodOrderLedgerReadPresentation.Normalize(root) : root;
        var guide = 주문원장업무안내Projector.생성(rootFullAccess ? readRoot : null, items);
        if (_분산연결 is not null && 조회역할 == 주문원장조회역할.주문자 && guide.업무종류 is null)
        {
            foreach (var item in items.Where(x => x.상세조회가능여부))
            {
                var connection = await _분산연결.FindForChildAsync(현재UserId, root.원장Id, item.원장Id, cancellationToken);
                if (connection is null) continue;
                if (connection.OrderLedgerConnectionStatusCode is not ("Connected" or "Pending"))
                {
                    guide.현재단계 = "업무 연결 권한 확인 필요";
                    guide.대기이유 = "업무 연결을 진행할 수 없습니다. 현재 참여 권한과 주문 조건을 담당자에게 확인해 주세요.";
                    break;
                }
                guide.업무종류 = "DistributedOrder";
                // 기존 분산 상세 화면은 주문이 속한 순회를 선택합니다. 영구 OrderId와 표시 선택키를 섞지 않습니다.
                guide.원천Id = connection.WaveId;
                guide.다음행동안내 = "배송·수령 상세 보기";
                if (connection.OrderLedgerConnectionStatusCode == "Pending")
                {
                    guide.현재단계 = "업무 연결 확인 필요";
                    guide.대기이유 = "일부 업무 기록의 연결을 확인하고 있습니다. 배송 상세에서 연결 상태와 재시도를 확인해 주세요.";
                }
                break;
            }
        }
        return Result.Ok(new 주문원장역할별조회Dto
        {
            주문원장Id = root.원장Id,
            조회역할 = 조회역할,
            주문원장상태 = readRoot.상태,
            주문원장상세 = rootFullAccess ? readRoot : null,
            주문원장조회근거 = rootFullAccess
                ? rootDirect ? 직접접근근거(root, 현재UserId) : 원장조회근거.승인공개
                : 원장조회근거.공개요청필요,
            관련원장목록 = items,
            상세공개요청필요수 = items.Count(x => x.조회근거 == 원장조회근거.공개요청필요),
            업무안내 = guide
        });
    }

    private static bool 역할에필요한참조인가(string 조회역할, string 포함역할)
        => 조회역할 switch
        {
            주문원장조회역할.주문자 => true,
            주문원장조회역할.판매자 => string.Equals(포함역할, 주문원장포함역할.판매, StringComparison.OrdinalIgnoreCase),
            주문원장조회역할.창고담당자 => string.Equals(포함역할, 주문원장포함역할.창고입고, StringComparison.OrdinalIgnoreCase)
                                         || string.Equals(포함역할, 주문원장포함역할.창고출고, StringComparison.OrdinalIgnoreCase),
            주문원장조회역할.운송담당자 => string.Equals(포함역할, 주문원장포함역할.배송, StringComparison.OrdinalIgnoreCase)
                                         || string.Equals(포함역할, 주문원장포함역할.운송, StringComparison.OrdinalIgnoreCase),
            _ => false
        };

    private static bool 역할참여자인가(커뮤니티원장Dto 원장, string userId, string 조회역할)
    {
        if (string.Equals(원장.생성자UserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var labels = 조회역할 switch
        {
            주문원장조회역할.판매자 => new[] { "판매", "판매자", "공급", "화주" },
            주문원장조회역할.창고담당자 => ["창고", "입고", "출고", "피킹", "포장"],
            주문원장조회역할.운송담당자 => ["운송", "기사", "배송", "배달"],
            _ => ["주문", "주문자"]
        };
        return 원장.참여자목록.Any(x =>
            string.Equals(x.UserId, userId, StringComparison.OrdinalIgnoreCase)
            && labels.Any(label => x.RoleLabel.Contains(label, StringComparison.OrdinalIgnoreCase)));
    }

    internal static bool 직접접근가능(커뮤니티원장Dto 원장, string userId)
        => string.Equals(원장.생성자UserId, userId, StringComparison.OrdinalIgnoreCase)
           || 원장.참여자목록.Any(x => string.Equals(x.UserId, userId, StringComparison.OrdinalIgnoreCase));

    private static string 직접접근근거(커뮤니티원장Dto 원장, string userId)
        => string.Equals(원장.생성자UserId, userId, StringComparison.OrdinalIgnoreCase)
            ? 원장조회근거.소유자
            : 원장조회근거.직접참여;

    private static Result<주문원장역할별조회Dto> BadRequest(string message)
        => Failure(message, StatusCodes.Status400BadRequest);

    private static Result<주문원장역할별조회Dto> Forbidden(string message)
        => Failure(message, StatusCodes.Status403Forbidden);

    private static Result<주문원장역할별조회Dto> NotFound(string message)
        => Failure(message, StatusCodes.Status404NotFound);

    private static Result<주문원장역할별조회Dto> Failure(string message, int statusCode)
        => Result.Fail<주문원장역할별조회Dto>(new Error(message).WithMetadata("StatusCode", statusCode));
}

public sealed class 주문원장역할별조회Dto
{
    public string 주문원장Id { get; set; } = string.Empty;
    public string 조회역할 { get; set; } = string.Empty;
    public string 주문원장상태 { get; set; } = string.Empty;
    public string 주문원장조회근거 { get; set; } = 원장조회근거.공개요청필요;
    public 커뮤니티원장Dto? 주문원장상세 { get; set; }
    public IReadOnlyList<주문역할별원장항목Dto> 관련원장목록 { get; set; } = [];
    public int 상세공개요청필요수 { get; set; }
    public 주문원장업무안내Dto? 업무안내 { get; set; }
}

public sealed class 주문역할별원장항목Dto
{
    public string 원장Id { get; set; } = string.Empty;
    public string 원장템플릿Key { get; set; } = string.Empty;
    public string 주문안역할 { get; set; } = string.Empty;
    public bool 필수여부 { get; set; }
    public int 표시순서 { get; set; }
    public string 조회근거 { get; set; } = 원장조회근거.공개요청필요;
    public bool 상세조회가능여부 { get; set; }
    public bool 공개요청가능여부 { get; set; }
    public 커뮤니티원장Dto? 원장상세 { get; set; }
}

public interface I주문원장공개요청Service
{
    Task<Result<원장공개요청Dto>> 요청Async(
        string 주문원장Id,
        원장공개요청입력 request,
        string 현재UserId,
        CancellationToken cancellationToken = default);

    Task<Result<원장공개요청Dto>> 결정Async(
        string 주문원장Id,
        string 요청Id,
        원장공개결정입력 request,
        string 현재UserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<원장공개요청Dto>> 받은요청목록Async(
        string 현재UserId,
        CancellationToken cancellationToken = default);
}

public sealed class 주문원장공개요청Service : I주문원장공개요청Service
{
    private readonly I커뮤니티원장저장소 _원장저장소;
    private readonly I주문원장공개요청저장소 _요청저장소;

    public 주문원장공개요청Service(
        I커뮤니티원장저장소 원장저장소,
        I주문원장공개요청저장소 요청저장소)
    {
        _원장저장소 = 원장저장소;
        _요청저장소 = 요청저장소;
    }

    public async Task<Result<원장공개요청Dto>> 요청Async(
        string 주문원장Id,
        원장공개요청입력 request,
        string 현재UserId,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.대상원장Id) || string.IsNullOrWhiteSpace(request.사유))
        {
            return Failure("공개를 요청할 대상 원장과 사유가 필요합니다.", StatusCodes.Status400BadRequest);
        }

        var relation = await 관계원장조회Async(주문원장Id, request.대상원장Id, 현재UserId, cancellationToken);
        if (relation.IsFailed)
        {
            return relation.ToResult<원장공개요청Dto>();
        }

        var (root, target, requesterName) = relation.Value;
        if (주문원장역할별조회Service.직접접근가능(target, 현재UserId))
        {
            return Failure("이미 소유자나 직접 참여자로서 조회할 수 있는 원장입니다.", StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(target.생성자UserId))
        {
            return Failure("공개 요청을 승인할 원장 소유자가 지정되어 있지 않습니다.", StatusCodes.Status409Conflict);
        }

        var now = DateTimeOffset.UtcNow;
        var saved = await _요청저장소.요청생성Async(
            new 원장공개요청기록(
                $"ledger-disclosure-{Guid.NewGuid():N}",
                root.원장Id,
                target.원장Id,
                현재UserId,
                requesterName,
                target.생성자UserId,
                원장공개범위.원장상세,
                request.사유.Trim(),
                원장공개요청상태.승인대기,
                now,
                null,
                now.AddDays(14),
                null),
            cancellationToken);
        return Result.Ok(ToDto(saved));
    }

    public async Task<Result<원장공개요청Dto>> 결정Async(
        string 주문원장Id,
        string 요청Id,
        원장공개결정입력 request,
        string 현재UserId,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(요청Id) || string.IsNullOrWhiteSpace(현재UserId))
        {
            return Failure("공개 요청 결정 정보가 부족합니다.", StatusCodes.Status400BadRequest);
        }

        var existing = await _요청저장소.요청조회Async(요청Id.Trim(), cancellationToken);
        if (existing is null || !string.Equals(existing.주문원장Id, 주문원장Id, StringComparison.OrdinalIgnoreCase))
        {
            return Failure("공개 요청을 찾을 수 없습니다.", StatusCodes.Status404NotFound);
        }

        if (!string.Equals(existing.승인자UserId, 현재UserId, StringComparison.OrdinalIgnoreCase))
        {
            return Failure("대상 원장 소유자만 공개 요청을 결정할 수 있습니다.", StatusCodes.Status403Forbidden);
        }

        var now = DateTimeOffset.UtcNow;
        var decided = await _요청저장소.요청결정Async(
            existing.요청Id,
            현재UserId,
            request.승인여부,
            request.처리메모,
            now,
            now.AddDays(Math.Clamp(request.공개일수, 1, 90)),
            cancellationToken);
        return decided is null
            ? Failure("요청이 이미 처리되었거나 만료되었습니다.", StatusCodes.Status409Conflict)
            : Result.Ok(ToDto(decided));
    }

    public async Task<IReadOnlyList<원장공개요청Dto>> 받은요청목록Async(
        string 현재UserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(현재UserId))
        {
            return [];
        }

        var records = await _요청저장소.받은요청목록Async(현재UserId, cancellationToken);
        return records.Select(ToDto).ToArray();
    }

    private async Task<Result<(커뮤니티원장Dto Root, 커뮤니티원장Dto Target, string RequesterName)>> 관계원장조회Async(
        string rootId,
        string targetId,
        string requesterUserId,
        CancellationToken cancellationToken)
    {
        var root = await _원장저장소.원장조회Async(rootId.Trim(), cancellationToken);
        if (root is null || !주문원장구성정책.주문루트인가(root.원장템플릿Key))
        {
            return RelationFailure("개별 주문 원장을 찾을 수 없습니다.", StatusCodes.Status404NotFound);
        }

        var childIds = root.포함원장목록.Select(x => x.원장Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!string.Equals(root.원장Id, targetId, StringComparison.OrdinalIgnoreCase) && !childIds.Contains(targetId))
        {
            return RelationFailure("주문 원장과 직접 연결된 원장만 공개를 요청할 수 있습니다.", StatusCodes.Status400BadRequest);
        }

        var childTasks = childIds.Select(x => _원장저장소.원장조회Async(x, cancellationToken)).ToArray();
        var children = childTasks.Length == 0 ? [] : await Task.WhenAll(childTasks);
        var requesterLedger = 주문원장역할별조회Service.직접접근가능(root, requesterUserId)
            ? root
            : children.FirstOrDefault(x => x is not null && 주문원장역할별조회Service.직접접근가능(x, requesterUserId));
        if (requesterLedger is null)
        {
            return RelationFailure("주문과 직접 관계된 참여자만 다른 원장의 공개를 요청할 수 있습니다.", StatusCodes.Status403Forbidden);
        }

        var target = string.Equals(root.원장Id, targetId, StringComparison.OrdinalIgnoreCase)
            ? root
            : children.FirstOrDefault(x => x is not null && string.Equals(x.원장Id, targetId, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return RelationFailure("공개를 요청할 원장을 찾을 수 없습니다.", StatusCodes.Status404NotFound);
        }

        var requesterName = string.Equals(requesterLedger.생성자UserId, requesterUserId, StringComparison.OrdinalIgnoreCase)
            ? requesterLedger.생성자표시명
            : requesterLedger.참여자목록.First(x => string.Equals(x.UserId, requesterUserId, StringComparison.OrdinalIgnoreCase)).DisplayName;
        return Result.Ok((root, target, requesterName));
    }

    private static 원장공개요청Dto ToDto(원장공개요청기록 record)
        => new()
        {
            요청Id = record.요청Id,
            주문원장Id = record.주문원장Id,
            대상원장Id = record.대상원장Id,
            요청자표시명 = record.요청자표시명,
            공개범위 = record.공개범위,
            사유 = record.사유,
            상태 = record.상태,
            요청시각Utc = record.요청시각Utc,
            처리시각Utc = record.처리시각Utc,
            만료시각Utc = record.만료시각Utc,
            처리메모 = record.처리메모
        };

    private static Result<원장공개요청Dto> Failure(string message, int statusCode)
        => Result.Fail<원장공개요청Dto>(new Error(message).WithMetadata("StatusCode", statusCode));

    private static Result<(커뮤니티원장Dto Root, 커뮤니티원장Dto Target, string RequesterName)> RelationFailure(
        string message,
        int statusCode)
        => Result.Fail<(커뮤니티원장Dto, 커뮤니티원장Dto, string)>(
            new Error(message).WithMetadata("StatusCode", statusCode));
}

public sealed class 원장공개요청입력
{
    public string 대상원장Id { get; set; } = string.Empty;
    public string 사유 { get; set; } = string.Empty;
}

public sealed class 원장공개결정입력
{
    public bool 승인여부 { get; set; }
    public int 공개일수 { get; set; } = 30;
    public string? 처리메모 { get; set; }
}

public sealed class 원장공개요청Dto
{
    public string 요청Id { get; set; } = string.Empty;
    public string 주문원장Id { get; set; } = string.Empty;
    public string 대상원장Id { get; set; } = string.Empty;
    public string 요청자표시명 { get; set; } = string.Empty;
    public string 공개범위 { get; set; } = 원장공개범위.원장상세;
    public string 사유 { get; set; } = string.Empty;
    public string 상태 { get; set; } = string.Empty;
    public DateTimeOffset 요청시각Utc { get; set; }
    public DateTimeOffset? 처리시각Utc { get; set; }
    public DateTimeOffset 만료시각Utc { get; set; }
    public string? 처리메모 { get; set; }
}
