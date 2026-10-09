using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace RoleWorkspacePreview;

// 기존 제품 component와 ViewModel의 표시·재조회만 검토합니다. 쓰기 API나 실제 사용자 정보를 제공하지 않습니다.
public sealed class OrderLedgerRolePreviewFixture : I주문원장Service
{
    public string Scenario { get; set; } = "food";
    private bool recoveryFailed;
    public Task<주문원장역할별조회공개Dto?> 주문원장보호조회Async(string id, CancellationToken cancellationToken = default)
    {
        if (Scenario == "error") throw new InvalidOperationException("주문 정보를 불러오지 못했습니다. 다시 시도해 주세요.");
        if (Scenario == "recovery" && !recoveryFailed)
        { recoveryFailed = true; throw new InvalidOperationException("일시적으로 주문 조회가 끊겼습니다. 다시 시도해 주세요."); }
        var hidden = Scenario == "hidden";
        var food = Scenario == "food";
        return Task.FromResult<주문원장역할별조회공개Dto?>(new()
        {
            주문원장Id = id, 조회역할 = "주문자", 주문원장상태 = "진행중", 주문원장조회근거 = "소유자",
            주문원장상세 = new() { 원장Id = id, 제목 = food ? "음식 주문" : "생활 물품 주문", Revision = 3,
                상태 = "진행중", 현재단계Key = food ? "전달완료" : "배송중", 생성자표시명 = "본인", 수정시각Utc = new(2026, 10, 8, 3, 0, 0, DateTimeKind.Utc) },
            업무안내 = new() { 현재단계 = hidden ? "업무 상태 확인 필요" : food ? "수령 확인" : "배송 진행 확인", 담당역할 = food ? "주문자" : "기사",
                대기이유 = hidden ? "필수 업무의 상태를 볼 수 없습니다. 조회 권한과 연결 상태를 확인해 주세요." : food ? "기사가 전달을 완료했습니다. 실제로 받았는지 확인해 주세요." : "물품 배송이 진행 중입니다. 배송 상세에서 최신 상태를 확인해 주세요.",
                업무종류 = hidden ? null : food ? "FoodOrder" : "DistributedOrder", 원천Id = hidden ? null : food ? "preview-food" : "preview-wave",
                다음행동안내 = food ? "수령 확인 화면 열기" : "배송·수령 상세 보기" },
            관련원장목록 = [new() { 원장Id = "preview-child", 원장템플릿Key = "cargo-transport", 주문안역할 = "운송", 필수여부 = true,
                표시순서 = 0, 상세조회가능여부 = !hidden, 조회근거 = hidden ? "공개요청필요" : "직접참여",
                원장상세 = hidden ? null : new() { 원장Id = "preview-child", 제목 = "배송 업무", 상태 = "진행중" } }],
            상세공개요청필요수 = hidden ? 1 : 0
        });
    }
    public Task<주문원장역할별조회공개Dto?> 주문원장역할조회Async(string id, string viewCode, CancellationToken ct = default) => 주문원장보호조회Async(id, ct);
    public Task<주문원장통합공개Dto?> 하위원장연결Async(string id, 주문하위원장연결ClientRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<주문원장통합공개Dto?> 하위원장분리Async(string id, string childId, long? revision = null, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<주문원장서명상태공개Dto?> 주문원장서명상태조회Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<주문원장서명상태공개Dto?> 주문원장서명준비Async(string id, 주문원장서명준비ClientRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<주문원장서명상태공개Dto?> 주문원장서명등록Async(string id, 주문원장서명등록ClientRequest request, CancellationToken ct = default) => throw new NotSupportedException();
}
