using FluentResults;
using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Services.Community;

/// <summary>주문 원장과 같은 판본의 전문 상태·참조·서명을 읽어 응답을 조립한다. 관계를 저장하지 않는다.</summary>
internal sealed class 주문원장조회Service(I커뮤니티원장저장소 원장저장소, I주문원장서명저장소 서명저장소)
{
    private readonly I커뮤니티원장저장소 _원장저장소 = 원장저장소;
    private readonly I주문원장서명저장소 _서명저장소 = 서명저장소;

    private static readonly IReadOnlyList<CommunityLedgerTemplateResponse> 주문자원장종류 =
        CommunityLedgerTemplateCatalog.주문원장종류;

    private static readonly IReadOnlySet<string> 주문자원장템플릿Keys =
        주문자원장종류
            .Select(template => template.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async Task<Result<주문자원장목록응답>> 주문자목록조회Async(
        string 주문자UserId,
        주문자원장목록조회요청 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(주문자UserId))
        {
            return Unauthorized<주문자원장목록응답>("로그인 사용자를 확인할 수 없습니다.");
        }

        var templateKey = string.IsNullOrWhiteSpace(request.원장템플릿Key)
            ? null
            : request.원장템플릿Key.Trim();
        if (templateKey is not null && !주문자원장템플릿Keys.Contains(templateKey))
        {
            return BadRequest<주문자원장목록응답>(
                "주문자 목록은 음식 주문, 살뜰 마트 주문, 같이 주문 또는 같이 수입 원장만 조회할 수 있습니다.");
        }

        var page = Math.Max(0, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var ledgers = new List<커뮤니티원장Dto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var offset = 0;
        while (true)
        {
            var batch = await _원장저장소.원장목록조회Async(new 커뮤니티원장조회조건
            {
                접근UserId = 주문자UserId.Trim(),
                원장템플릿Key = templateKey,
                원장템플릿Keys = templateKey is null
                    ? 주문자원장종류.Select(template => template.Key).ToArray()
                    : [],
                // 음식 원장의 저장 요약은 준비 완료일 수 있습니다. 같은 판본의 업무 블록을
                // 판독한 뒤 상태 검색과 집계를 적용해야 전달/수령 대기 주문이 사라지지 않습니다.
                상태 = templateKey is not null && templateKey != CommunityLedgerTemplateKeys.FoodOrder
                    ? request.상태?.Trim() : null,
                Limit = 200,
                Skip = offset
            }, cancellationToken);
            var added = 0;
            foreach (var ledger in batch)
                if (seen.Add(ledger.원장Id)) { ledgers.Add(ledger); added++; }
            if (batch.Count < 200) break;
            if (added == 0) throw new InvalidOperationException("주문 목록의 다음 페이지를 확인하지 못했습니다.");
            offset += batch.Count;
        }
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var filtered = ledgers
            .Select(FoodOrderLedgerReadPresentation.Normalize)
            .Where(ledger => 주문자원장템플릿Keys.Contains(ledger.원장템플릿Key))
            .Where(ledger => templateKey is null
                             || string.Equals(
                                 ledger.원장템플릿Key,
                                 templateKey,
                                 StringComparison.OrdinalIgnoreCase))
            .Where(ledger => string.IsNullOrWhiteSpace(request.상태)
                             || string.Equals(ledger.상태, request.상태.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(ledger => search is null
                             || ledger.원장Id.Contains(search, StringComparison.OrdinalIgnoreCase)
                             || ledger.제목.Contains(search, StringComparison.OrdinalIgnoreCase)
                             || ledger.상태.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(ledger => ledger.수정시각Utc)
            .ThenByDescending(ledger => ledger.생성시각Utc)
            .ToArray();
        var counts = filtered
            .GroupBy(ledger => ledger.원장템플릿Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var items = filtered
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(주문자원장항목으로)
            .ToArray();

        return Result.Ok(new 주문자원장목록응답
        {
            원장종류목록 = 주문자원장종류
                .Select((template, index) => 주문자원장종류요약으로(
                    template,
                    index,
                    counts.GetValueOrDefault(template.Key)))
                .ToArray(),
            Items = items,
            TotalCount = filtered.Length,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<주문원장통합Dto>> 조회Async(
        string 주문원장Id,
        CancellationToken cancellationToken = default)
    {
        var 주문원장결과 = await 주문원장조회Async(주문원장Id, cancellationToken);
        return 주문원장결과.IsFailed
            ? 주문원장결과.ToResult<주문원장통합Dto>()
            : Result.Ok(await 통합Dto생성Async(주문원장결과.Value, cancellationToken));
    }

    internal async Task<Result<커뮤니티원장Dto>> 주문원장조회Async(
        string 주문원장Id,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(주문원장Id))
        {
            return BadRequest<커뮤니티원장Dto>("주문 원장 ID가 필요합니다.");
        }

        var 주문원장 = await _원장저장소.원장조회Async(주문원장Id.Trim(), cancellationToken);
        if (주문원장 is null)
        {
            return NotFound<커뮤니티원장Dto>("주문 원장을 찾을 수 없습니다.");
        }

        return 주문원장구성정책.통합대상인가(주문원장.원장템플릿Key)
            ? Result.Ok(주문원장)
            : BadRequest<커뮤니티원장Dto>("통합 조회 대상은 주문, 공동구매, 같이 수입 또는 공동수출 원장이어야 합니다.");
    }

    internal async Task<주문원장통합Dto> 통합Dto생성Async(
        커뮤니티원장Dto 주문원장,
        CancellationToken cancellationToken)
    {
        주문원장 = FoodOrderLedgerReadPresentation.Normalize(주문원장);
        var 참조목록 = (await 음식주문원장참조Resolver.조회Async(
            _원장저장소, 주문원장, cancellationToken)).OrderBy(x => x.표시순서).ToArray();
        var 조회작업목록 = 참조목록
            .Select(x => _원장저장소.원장조회Async(x.원장Id, cancellationToken))
            .ToArray();
        var 하위원장목록 = 조회작업목록.Length == 0
            ? []
            : await Task.WhenAll(조회작업목록);
        var 서명조회대상Ids = 주문원장구성정책.주문루트인가(주문원장.원장템플릿Key)
            ? [주문원장.원장Id]
            : 참조목록
                .Where(x => string.Equals(x.역할, 주문원장포함역할.개별주문, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.원장Id)
                .ToArray();
        var 서명기록목록 = await _서명저장소.목록조회Async(서명조회대상Ids, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var 포함항목목록 = 참조목록
            .Select((참조, index) => new 주문포함원장Dto
            {
                원장Id = 참조.원장Id,
                원장템플릿Key = 참조.원장템플릿Key,
                역할 = 참조.역할,
                필수여부 = 참조.필수여부,
                표시순서 = 참조.표시순서,
                원장 = 하위원장목록[index] is { } child ? FoodOrderLedgerReadPresentation.Normalize(child) : null,
                조회상태 = 하위원장목록[index] is null ? "원장누락" : "정상",
                주문자서명상태 = 서명기록목록.TryGetValue(참조.원장Id, out var 서명기록)
                    ? 주문원장서명상태Factory.생성(서명기록, now)
                    : null
            })
            .ToArray();
        var 개별주문항목목록 = 포함항목목록
            .Where(x => string.Equals(x.역할, 주문원장포함역할.개별주문, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var 서명완료주문수 = 개별주문항목목록.Count(x => x.주문자서명상태?.전체서명완료여부 == true);

        return new 주문원장통합Dto
        {
            주문원장 = 주문원장,
            주문자서명상태 = 서명기록목록.TryGetValue(주문원장.원장Id, out var 주문서명기록)
                ? 주문원장서명상태Factory.생성(주문서명기록, now)
                : null,
            포함원장목록 = 포함항목목록,
            전체하위원장수 = 포함항목목록.Length,
            완료하위원장수 = 포함항목목록.Count(x => x.원장?.상태 == 커뮤니티원장상태.완료),
            필수하위원장완료여부 = 포함항목목록.Any(x => x.필수여부) && 포함항목목록
                .Where(x => x.필수여부)
                .All(x => x.원장?.상태 == 커뮤니티원장상태.완료),
            필수하위원장판정 = !포함항목목록.Any(x => x.필수여부) ? "필수업무미지정"
                : 포함항목목록.Any(x => x.필수여부 && x.원장 is null) ? "원장누락"
                : 포함항목목록.Where(x => x.필수여부).All(x => x.원장?.상태 == 커뮤니티원장상태.완료)
                    ? "필수업무완료" : "필수업무진행중",
            서명대상주문수 = 개별주문항목목록.Length,
            서명완료주문수 = 서명완료주문수,
            미서명주문Ids = 개별주문항목목록
                .Where(x => x.주문자서명상태?.전체서명완료여부 != true)
                .Select(x => x.원장Id)
                .ToArray(),
            전체주문서명완료여부 = 개별주문항목목록.Length > 0
                && 서명완료주문수 == 개별주문항목목록.Length
        };
    }

    private static 주문자원장종류요약Dto 주문자원장종류요약으로(
        CommunityLedgerTemplateResponse template,
        int index,
        int count)
    {
        var boundary = 실행경계(template.Key);
        return new 주문자원장종류요약Dto
        {
            표시순서 = index + 1,
            원장템플릿Key = template.Key,
            원장종류명 = template.DisplayName,
            설명 = template.Summary,
            실행경계코드 = boundary.Code,
            실행경계안내 = boundary.Message,
            내원장수 = count
        };
    }

    private static 주문자원장목록항목Dto 주문자원장항목으로(커뮤니티원장Dto ledger)
    {
        var template = CommunityLedgerTemplateCatalog.Find(ledger.원장템플릿Key);
        var boundary = 실행경계(ledger.원장템플릿Key);
        return new 주문자원장목록항목Dto
        {
            원장Id = ledger.원장Id,
            Revision = ledger.Revision,
            원장템플릿Key = ledger.원장템플릿Key,
            원장종류명 = template.DisplayName,
            제목 = ledger.제목,
            상태 = ledger.상태,
            현재단계Key = ledger.현재단계Key,
            실행경계코드 = boundary.Code,
            실행경계안내 = boundary.Message,
            주문자상세조회경로 =
                $"api/v1/community/order-ledgers/{Uri.EscapeDataString(ledger.원장Id)}/views/orderer",
            생성시각Utc = ledger.생성시각Utc,
            수정시각Utc = ledger.수정시각Utc
        };
    }

    private static (string Code, string Message) 실행경계(string templateKey)
        => templateKey switch
        {
            CommunityLedgerTemplateKeys.FoodOrder =>
                (주문자원장실행경계코드.실제주문,
                    "음식 주문은 실제 주문 상태를 기록합니다. 음식점 수락·조리·배달 상태는 음식 주문 API에서 변경하고 이 원장을 다시 조회합니다."),
            CommunityLedgerTemplateKeys.SsalddelMart =>
                (주문자원장실행경계코드.운영원장,
                    "살뜰 마트의 비구속 주문 요청과 실제 피킹·배송 원장은 분리됩니다. 이 원장은 운영 주문이 생성된 뒤의 상태를 표시합니다."),
            CommunityLedgerTemplateKeys.GroupOrder =>
                (주문자원장실행경계코드.집계원장,
                    "같이 주문 원장의 수량과 금액은 연결된 개별 주문에서 계산합니다. 원장 합계를 직접 덮어쓰거나 자동 결제하지 않습니다."),
            CommunityLedgerTemplateKeys.GroupImport =>
                (주문자원장실행경계코드.수입준비,
                    "같이 수입 원장은 비용·LCL/FCL·전문가 인계를 준비합니다. 별도 확인 전에는 발주·계약·수입 신고를 자동 실행하지 않습니다."),
            _ => (string.Empty, string.Empty)
        };

    private static Result<T> BadRequest<T>(string message)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", StatusCodes.Status400BadRequest));

    private static Result<T> NotFound<T>(string message)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", StatusCodes.Status404NotFound));

    private static Result<T> Unauthorized<T>(string message)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", StatusCodes.Status401Unauthorized));

}
