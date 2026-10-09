using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Services.Community;

/// <summary>이미 보호된 조회 결과로 다음 업무를 안내합니다. 숨겨진 원장이나 실행 권한을 추정하지 않습니다.</summary>
internal static class 주문원장업무안내Projector
{
    public static 주문원장업무안내Dto 생성(커뮤니티원장Dto? root, IReadOnlyList<주문역할별원장항목Dto> items)
    {
        if (root?.원장템플릿Key == CommunityLedgerTemplateKeys.FoodOrder)
            return 음식안내(root);

        var unavailable = items.FirstOrDefault(x => x.필수여부 && x.원장상세 is null);
        if (unavailable is not null)
            return new()
            {
                현재단계 = "업무 상태 확인 필요", 담당역할 = 역할표시(unavailable.주문안역할),
                대기이유 = unavailable.조회근거 == 원장조회근거.원장누락
                    ? "필수 업무 기록을 찾지 못했습니다. 연결 상태를 확인해야 합니다."
                    : "필수 업무의 상세 조회 권한이 없어 진행 상태를 확인할 수 없습니다."
            };

        var pending = items.OrderBy(x => x.표시순서)
            .FirstOrDefault(x => x.필수여부 && x.원장상세?.상태 != 커뮤니티원장상태.완료);
        if (pending is not null)
            return new()
            {
                현재단계 = 역할표시(pending.주문안역할) + " 업무 확인",
                담당역할 = 역할표시(pending.주문안역할),
                대기이유 = pending.원장상세?.상태 == 커뮤니티원장상태.닫힘
                    ? "필수 업무가 중단되었습니다. 담당자와 후속 처리 방법을 확인해 주세요."
                    : "담당 업무의 진행 상태를 확인하고 다음 절차를 진행합니다."
            };

        return new()
        {
            현재단계 = items.Any(x => x.필수여부) ? "수령·정산 확인" : "이행 업무 연결 확인",
            담당역할 = "주문자·담당자",
            대기이유 = items.Any(x => x.필수여부)
                ? "연결된 필수 업무가 완료되었습니다. 주문 수령과 정산·지급 여부는 각각 확인합니다."
                : "아직 필수 이행 업무가 지정되지 않았습니다. 배송 방식과 연결할 업무를 확인해 주세요."
        };
    }

    private static 주문원장업무안내Dto 음식안내(커뮤니티원장Dto root)
    {
        var (step, actor, reason) = root.현재단계Key switch
        {
            음식주문상태코드.주문대기 => ("음식점 확인 대기", "음식점", "음식점의 주문 확인을 기다리고 있습니다."),
            음식주문상태코드.주문확인 => ("기사 배정 확인", "배차 담당자", "유효한 기사 배정 이후 음식점이 조리를 시작합니다."),
            음식주문상태코드.기사배정 => ("음식 준비", "음식점", "기사 배정 후 음식 준비 상태를 확인합니다."),
            음식주문상태코드.조리중 => ("조리 중", "음식점", "음식 준비 완료를 기다리고 있습니다."),
            음식주문상태코드.픽업대기 => ("기사 픽업 대기", "기사", "음식 준비가 끝나 기사의 픽업을 기다리고 있습니다."),
            음식주문상태코드.픽업완료 => ("배달 중", "기사", "기사가 음식을 픽업했습니다. 전달 완료를 기다리고 있습니다."),
            음식주문상태코드.전달완료 => ("음식 수령 확인", "주문자", "기사가 전달 완료를 기록했습니다. 실제 음식을 받았는지 확인해 주세요."),
            음식주문상태코드.수령확인 => ("수령 확인 완료", "주문자", "음식 수령이 확인되었습니다. 정산·지급 완료와는 별도입니다."),
            음식주문상태코드.취소 or 음식주문상태코드.거절 => ("주문 종료", "주문자·음식점", "종료된 주문의 기록과 문제 처리 결과를 확인할 수 있습니다."),
            _ => ("음식 주문 진행 확인", "주문자", "최신 음식 주문 상세에서 현재 단계와 가능한 행동을 확인해 주세요.")
        };
        // 기존 업무 투영과 같은 호환 규칙으로 정식 음식 주문번호를 읽습니다.
        var sourceId = 음식주문원장업무투영Snapshot.생성(root)?.주문번호;
        return new()
        {
            현재단계 = step, 담당역할 = actor, 대기이유 = reason,
            업무종류 = string.IsNullOrWhiteSpace(sourceId) ? null : "FoodOrder",
            원천Id = string.IsNullOrWhiteSpace(sourceId) ? null : sourceId,
            다음행동안내 = string.IsNullOrWhiteSpace(sourceId) ? "주문 정보 새로고침"
                : root.현재단계Key == 음식주문상태코드.전달완료 ? "수령 확인 화면 열기" : "음식 주문 상세 보기",
            조건변경안내 = root.현재단계Key == 음식주문상태코드.주문대기
                ? "직접 취소와 배송 조건 변경의 가능 여부는 최신 음식 주문 화면에서 확인합니다."
                : "수락 이후 취소·변경은 협의 요청으로 접수합니다. 요청만으로 주문이 취소되거나 환불되지 않습니다."
        };
    }

    private static string 역할표시(string role) => role switch
    {
        주문원장포함역할.판매 => "판매자",
        주문원장포함역할.창고입고 or 주문원장포함역할.창고출고 => "공간·창고 담당자",
        주문원장포함역할.배송 or 주문원장포함역할.운송 => "기사·운송 담당자",
        _ => "업무 담당자"
    };
}
