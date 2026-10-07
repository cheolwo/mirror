using Ssalddel.Contracts.Common.Commerce;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.Services.Commerce;
namespace CommerceProtectionPreview;
/// <summary>루프백 개발 호스트에서만 사용하는 표시·행동 예시입니다. 운영 신원확인·DB 저장·외부 알림을 실행하지 않습니다.</summary>
public sealed class CommercePreviewClient : I통신판매보호Client, ISsalddel현재사용자Context
{
    public string State { get; set; } = "normal";
    public 현재사용자Snapshot 현재사용자 => State == "anonymous" ? 현재사용자Snapshot.익명 : new("preview-user", "사용자 1", []);
    private static DateTime Now => new(2026, 10, 6, 3, 0, 0, DateTimeKind.Utc);
    private void Check() { if (State == "error") throw new InvalidOperationException("개발 검토용 실패"); }
    public Task<통신판매안내Response?> 안내Async(CancellationToken ct)
    {
        Check(); return Task.FromResult<통신판매안내Response?>(new() { Version="commerce-r25-preview", IntermediaryNotice="Mirror는 거래 당사자를 연결하는 통신판매중개 플랫폼입니다. 실제 계약 당사자와 거래 조건을 확인하고 거래하세요.", IsOperationalReady=false,
            Documents=[new("terms","이용약관",[new("거래 당사자와 거래 조건","물건·서비스의 제공자와 받는 사람이 조건을 확인하고 합의합니다. 주문 접수, 기사 수락, 물건 전달과 대금 지급은 서로 다른 단계입니다.")]),new("privacy","개인정보 처리방침",[new("필요한 정보 제공","상세주소와 연락처는 해당 거래의 권한 있는 당사자와 현재 배정된 기사에게 필요한 단계에서 제공합니다."),new("보존과 파기","기사의 완료 상세정보 열람은 3일간 가능합니다. 업무 정보의 실제 파기와 법정 거래기록 보존은 별도로 처리합니다.")]),new("disputes","분쟁 처리 기준",[new("진행 안내","해당 분쟁의 진행 경과는 3영업일, 조사 결과 또는 처리방안은 10영업일 이내 안내합니다.")])] });
    }
    public Task<판매자확인Response?> 판매자Async(CancellationToken ct) { Check(); return Task.FromResult<판매자확인Response?>(new() { SellerKind="Individual", DisplayName="사용자 1", Revision=1, StatusCode="Unverified", MissingRequirements=["신원 확인", "성년 확인"] }); }
    public Task<판매자확인Response?> 등록Async(판매자등록Request request, CancellationToken ct) => Task.FromResult<판매자확인Response?>(new() { SellerKind=request.SellerKind, DisplayName=request.DisplayName, Revision=request.ExpectedRevision+1, StatusCode="Unverified", MissingRequirements=["신원 확인"] });
    public Task<판매자확인Response?> 확인Async(판매자확인Request request, CancellationToken ct) => 판매자Async(ct);
    public Task<판매자공개정보Response?> 공개판매자Async(string? sellerId,long? restaurantId,CancellationToken ct) => Task.FromResult<판매자공개정보Response?>(State=="disclosure"?new() { Revision=restaurantId??1, SellerKind="Individual", DisplayName="예시 판매자", StatusCode="Verified" }:null);
    public Task<판매자공개정보Response?> 공개거래판매자Async(long? postId,string? collaborationId,CancellationToken ct) => 공개판매자Async(null,postId,ct);
    private static 보호지원CaseResponse Case(bool privacy, string id="example-case") => new() { CaseId=id, Kind=privacy?"privacy-rights":"transaction-dispute", RequestKind=privacy?"deletion":"", SourceKind=privacy?"":"food-order", SourceId=privacy?"":"example-order", StatusCode="reviewing", Summary=privacy?"삭제 요청 접수 · 본인 상세에서 처리 경과를 확인합니다.":"거래 문제 접수 · 당사자 상세에서 처리 경과를 확인합니다.", Revision=2, CreatedAtUtc=Now.AddDays(-1), UpdatedAtUtc=Now, ServerNowUtc=Now, ProgressDueAtUtc=privacy?null:Now.AddDays(3), ResultDueAtUtc=Now.AddDays(privacy?10:14), BusinessCalendarVerified=false, AllowedActions=["add-evidence"], History=[new(){Action="submit",RecordedAtUtc=Now.AddDays(-1)},new(){Action="start-review",RecordedAtUtc=Now}] };
    public Task<보호지원ListResponse> 목록Async(bool privacy, int page, CancellationToken ct) { Check(); return Task.FromResult(new 보호지원ListResponse { Page=page, Items=State=="empty"?[]:[Case(privacy)] }); }
    public Task<보호지원CaseResponse?> 상세Async(bool privacy,string id,CancellationToken ct) { Check(); var value=Case(privacy,id); if (privacy && State=="retained") { value.StatusCode="denied"; value.LegalReasonCode="statutory-retention"; value.LegalBasis="법정 보존 대상 기록은 필요한 범위에서 제한하여 보존합니다."; value.RetainUntilUtc=Now.AddYears(3); value.AllowedActions=["appeal"]; } if (State=="retention-pending") { value.RetentionPending=true; value.AllowedActions=[]; } return Task.FromResult<보호지원CaseResponse?>(value); }
    public Task<보호지원CaseResponse?> 분쟁Async(거래분쟁접수Request request,CancellationToken ct) { var value=Case(false); value.StatusCode="received"; return Task.FromResult<보호지원CaseResponse?>(value); }
    public Task<보호지원CaseResponse?> 권리Async(개인정보권리접수Request request,CancellationToken ct) { var value=Case(true); value.StatusCode="received"; return Task.FromResult<보호지원CaseResponse?>(value); }
    public Task<보호지원CaseResponse?> 변경Async(bool privacy,string id,보호지원CommandRequest request,CancellationToken ct) => 상세Async(privacy,id,ct);
}
