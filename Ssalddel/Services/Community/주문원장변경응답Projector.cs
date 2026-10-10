using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Services.Community;

/// <summary>연결/분리 응답의 기존 형식에 현재 역할의 보호된 조회 결과만 담습니다.</summary>
internal static class 주문원장변경응답Projector
{
    public static 주문원장통합Dto 생성(주문원장통합Dto changed, 주문원장역할별조회Dto current)
    {
        var root = current.주문원장상세
            ?? throw new InvalidOperationException("변경한 주문 원장의 현재 조회 권한을 확인할 수 없습니다.");
        var items = current.관련원장목록.Select(item => new 주문포함원장Dto
        {
            원장Id = item.원장Id, 원장템플릿Key = item.원장템플릿Key,
            역할 = item.주문안역할, 필수여부 = item.필수여부, 표시순서 = item.표시순서,
            원장 = item.상세조회가능여부 ? item.원장상세 : null,
            조회상태 = item.조회근거 is 원장조회근거.원장누락 or 원장조회근거.공개요청필요
                ? item.조회근거 : "정상",
            주문자서명상태 = item.상세조회가능여부 && item.원장상세 is { } visible
                ? changed.포함원장목록.FirstOrDefault(x => x.원장Id == item.원장Id
                    && x.원장?.Revision == visible.Revision)?.주문자서명상태 : null
        }).ToArray();
        var required = items.Where(x => x.필수여부).ToArray();
        var orders = items.Where(x => x.역할 == 주문원장포함역할.개별주문).ToArray();
        var signed = orders.Count(x => x.주문자서명상태?.전체서명완료여부 == true);
        return new()
        {
            주문원장 = root,
            주문자서명상태 = changed.주문원장.Revision == root.Revision ? changed.주문자서명상태 : null,
            포함원장목록 = items, 전체하위원장수 = items.Length,
            완료하위원장수 = items.Count(x => x.원장?.상태 == 커뮤니티원장상태.완료),
            필수하위원장완료여부 = required.Length > 0 && required.All(x => x.원장?.상태 == 커뮤니티원장상태.완료),
            필수하위원장판정 = required.Length == 0 ? "필수업무미지정"
                : required.Any(x => x.조회상태 == 원장조회근거.원장누락) ? "원장누락"
                : required.Any(x => x.원장 is null) ? "공개요청필요"
                : required.All(x => x.원장!.상태 == 커뮤니티원장상태.완료) ? "필수업무완료" : "필수업무진행중",
            서명대상주문수 = orders.Length, 서명완료주문수 = signed,
            // 숨겨진 주문의 서명 여부도 반환하지 않습니다.
            미서명주문Ids = orders.Where(x => x.원장 is not null && x.주문자서명상태?.전체서명완료여부 != true)
                .Select(x => x.원장Id).ToArray(),
            전체주문서명완료여부 = orders.Length > 0 && signed == orders.Length
        };
    }
}
