using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Services.Community;

/// <summary>
/// 조회 권한이 확인된 음식 주문 원장의 표시만 음식 주문 블록 상태에 맞춥니다.
/// 저장 원장, 업무 전이, 상태 이력과 원본 시각은 변경하지 않습니다.
/// </summary>
internal static class FoodOrderLedgerReadPresentation
{
    public static 커뮤니티원장Dto Normalize(커뮤니티원장Dto source)
    {
        if (!string.Equals(source.원장템플릿Key, CommunityLedgerTemplateKeys.FoodOrder, StringComparison.Ordinal))
        {
            return source;
        }

        var blocks = source.블록목록.Where(block =>
            string.Equals(block.BlockId, "food-order", StringComparison.Ordinal)).Take(2).ToArray();
        if (blocks.Length != 1
            || !string.Equals(blocks[0].BlockType, CommunityLedgerBlockTypes.Order, StringComparison.Ordinal)
            || !음식주문상태코드.지원여부(blocks[0].State))
        {
            return source;
        }

        var stage = blocks[0].State!.Trim();
        var status = stage switch
        {
            음식주문상태코드.수령확인 => 커뮤니티원장상태.완료,
            음식주문상태코드.거절 or 음식주문상태코드.취소 => 커뮤니티원장상태.닫힘,
            _ => 커뮤니티원장상태.진행중
        };

        // 같은 Revision의 블록만 사용하며, 새 업무 시각이나 수정 시각을 만들지 않습니다.
        return new 커뮤니티원장Dto
        {
            원장Id = source.원장Id,
            Revision = source.Revision,
            투영완료Revision = source.투영완료Revision,
            투영상태 = source.투영상태,
            투영EventId = source.투영EventId,
            투영마지막오류 = source.투영마지막오류,
            커뮤니티Id = source.커뮤니티Id,
            원장템플릿Key = source.원장템플릿Key,
            제목 = source.제목,
            원함 = source.원함,
            상태 = status,
            현재단계Key = stage,
            대상OsCode = source.대상OsCode,
            대상OsName = source.대상OsName,
            생성자UserId = source.생성자UserId,
            생성자표시명 = source.생성자표시명,
            블록목록 = source.블록목록,
            참여자목록 = source.참여자목록,
            포함원장목록 = source.포함원장목록,
            다이어그램스냅샷 = source.다이어그램스냅샷,
            외부참조 = source.외부참조,
            확장속성 = source.확장속성,
            상태이력 = source.상태이력,
            생성시각Utc = source.생성시각Utc,
            수정시각Utc = source.수정시각Utc
        };
    }
}
