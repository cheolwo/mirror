using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.Contracts.Shipper.Request;

namespace RoleWorkspacePreview;

/// <summary>브라우저 회로 안에서만 같은 초안을 유지한다. API·파일·DB 저장과 접수를 실행하지 않는다.</summary>
public sealed class ShipperMobileContextPreviewFixture
{
    public 운송의뢰작성ViewModel Draft { get; } = new();
    public Guid DraftId { get; } = Guid.NewGuid();
    public int Changes { get; private set; }
    public bool NestedScroll { get; set; }
    public string RecommendationCase { get; private set; } = "success";
    private string? preparedCase;
    public Task ChangedAsync() { Changes++; return Task.CompletedTask; }

    public async Task PrepareRecommendationCaseAsync(string? scenario)
    {
        if (scenario is not ("success" or "empty" or "failure" or "stale") || preparedCase == scenario) return;
        preparedCase = RecommendationCase = scenario;
        Draft.Reset();
        Draft.화물종류 = "검토용 포장 상자"; Draft.화물설명 = "실제 주문이 아닌 화면 확인용 자료";
        Draft.화물수량 = 3; Draft.화물중량Kg = 8m;
        Draft.화물길이Mm = 400; Draft.화물폭Mm = 300; Draft.화물높이Mm = 200;
        Draft.화물부피Cbm = .01m; Draft.화물바닥회전가능여부 = false; Draft.화물적층가능여부 = false;
        Draft.상차도로명주소 = "예시 출발 지역"; Draft.하차도로명주소 = "예시 도착 지역";
        Draft.상차시간창시작일시 = new(2030, 10, 20, 10, 0, 0);
        Draft.상차시간창종료일시 = new(2030, 10, 20, 11, 0, 0);
        Draft.결제예정금액 = 6000;
        if (scenario == "stale")
        {
            await Draft.차량추천조회Async((request, _) => Task.FromResult(Reply(request)));
            Draft.화물기반추천적용();
            Draft.화물높이Mm = 250;
        }
    }

    public Task<차량추천응답> RecommendVehiclesAsync(차량추천요청 request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return RecommendationCase == "failure"
            ? Task.FromException<차량추천응답>(new IOException("화면 예시의 의도된 조회 실패"))
            : Task.FromResult(Reply(request, RecommendationCase == "empty"));
    }

    private static 차량추천응답 Reply(차량추천요청 request, bool empty = false) => new()
    {
        적재조건 = 화물적재입력Policy.계산(화물적재입력Policy.입력생성(request)),
        후보목록 = empty ? [] : [new() { 차량코드 = "preview-only", 차량종류 = "1톤 카고", 단일운송가능여부 = true }],
        추천사유 = ["화면 검토용 응답입니다. 실제 차량 제원 조회나 서버 추천 결과가 아닙니다."]
    };
}
