using Ssalddel.Contracts.Common.Inventory;

namespace Ssalddel.Ui.Common.Areas.App.Models;

/// <summary>수정 중인 화면 입력을 API 요청과 분리하며 미전송 null과 명시적 빈값을 보존합니다.</summary>
public static class WarehouseTransportHandoffDraft
{
    public static 재고운송의뢰생성요청 Copy(재고운송의뢰생성요청 source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new()
        {
            출고예정Id = source.출고예정Id,
            입고상품Id = source.입고상품Id,
            요청수량 = source.요청수량,
            하차지주소 = source.하차지주소,
            하차지상세주소 = source.하차지상세주소,
            하차담당자명 = source.하차담당자명?.Trim(),
            하차연락처 = source.하차연락처?.Trim(),
            화물종류 = source.화물종류,
            차량종류 = source.차량종류,
            희망상차일시 = source.희망상차일시,
            희망도착일시 = source.희망도착일시,
            취급메모 = source.취급메모
        };
    }
}
