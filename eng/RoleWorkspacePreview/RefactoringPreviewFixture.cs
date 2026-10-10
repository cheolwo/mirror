using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;
using Ssalddel.Ui.Common.Areas.BackOffice.Services;

namespace RoleWorkspacePreview;

// 이 루프백 화면 전용 값 사본입니다. 제품 컴포넌트는 그대로 렌더하고 외부 API·DB·알림을 연결하지 않습니다.
public sealed class RefactoringPreviewFixture(IRoleWorkspaceAccess access) : IRoleWorkspaceApi, I보호지원관리Client
{
    public const string ItemId = "refactoring-order";
    public const string CaseId = "refactoring-case";
    public const string DeliveryId = "refactoring-delivery";
    public event Action? Changed;
    public string Scenario { get; private set; } = "orderer";
    public int FoodReads { get; private set; }
    public int FoodWrites { get; private set; }
    public int RecoveryReads { get; private set; }
    public int RecoveryWrites { get; private set; }
    public bool FoodCompleted { get; private set; }
    public bool RequestReused { get; private set; }
    public string? LastResolution => _original?.ResolutionCode;
    public bool ReceiptStored => _receipt is not null;
    private NeighborhoodMemberRecoveryRequest? _original;
    private NeighborhoodMemberRecoveryRequest? _receipt;
    private string? _originalBody;
    private static readonly DateTime ObservedAt = new(2026, 10, 10, 1, 0, 0, DateTimeKind.Utc);

    public void Reset(string scenario)
    {
        Scenario = scenario;
        FoodReads = FoodWrites = RecoveryReads = RecoveryWrites = 0;
        RequestReused = false; _original = _receipt = null; _originalBody = null;
        FoodCompleted = false;
    }
    public void MarkFoodCompleted() { FoodCompleted = true; Changed?.Invoke(); }

    public string Role => Scenario switch
    {
        "restaurant-accept" or "restaurant-time" => "restaurant",
        "food-driver" => "food-driver", "operator" => "operator", _ => "orderer"
    };
    public string Action => Scenario switch
    {
        "restaurant-accept" => 음식배달가능행동Ids.음식점주문수락,
        "restaurant-time" => 음식배달가능행동Ids.음식점조리시간변경,
        "food-driver" => 음식배달가능행동Ids.기사배달중단,
        "operator" => OperatorRoleWorkspaceAdapter.ReviewInterruption,
        _ => 음식배달가능행동Ids.주문취소
    };

    public Task<T> GetAsync<T>(string roleKey, string relativePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (roleKey != Role) throw new InvalidOperationException("Preview role mismatch.");
        object value = roleKey switch
        {
            "orderer" when relativePath == $"api/v1/food-orders/{ItemId}" => new 주문자음식주문상세응답
            {
                주문 = new() { 주문번호 = ItemId, 상태 = 음식주문상태코드.주문대기 },
                AvailableActions = Allowed(Action)
            },
            "restaurant" when relativePath == $"api/v1/food-orders/restaurant/inbox/{ItemId}" => new 음식주문응답
            {
                주문번호 = ItemId, Revision = 7, 음식점명 = "예시 음식점", 음식점주소 = "서울 중랑구 예시로",
                음식점상세주소 = "1층", 상태 = Scenario == "restaurant-accept" ? 음식주문상태코드.주문대기 : 음식주문상태코드.주문확인,
                조리예상분 = 15, AvailableActions = Allowed(Action)
            },
            "food-driver" when relativePath == "api/v1/driver/food-deliveries/workspace/current" => new FoodDeliveryDriverCurrentWorkDto
            {
                DriverId = access.GetIdentity(roleKey).OwnerId ?? "", UpdatedAtUtc = ObservedAt,
                ActiveDeliveries = [new() { OfferId = ItemId, DeliveryAttemptId = "refactoring-attempt", AttemptRevision = 7,
                    RestaurantName = "예시 음식점", AvailableActions = Allowed(Action) }]
            },
            "operator" when relativePath == $"api/v1/admin/food-orders/{ItemId}/operations-trace" => new 음식주문운영추적응답
            {
                주문번호 = ItemId,
                배달시도목록 = [new() { 시도StableId = "refactoring-attempt", Revision = 7, 시도순번 = 1,
                    상태Code = 음식배달시도상태Code.중단, 중단사유Code = 음식배달중단사유Code.배터리부족, 중단시각Utc = ObservedAt }]
            },
            _ => throw new InvalidOperationException("This preview permits only the configured canonical read.")
        };
        FoodReads++; Changed?.Invoke();
        return Task.FromResult((T)value);
    }

    private static IReadOnlyList<업무가능행동Dto> Allowed(string action)
        => [new() { ActionId = action, ExpectedRevision = 7 }];
    public Task<T?> PostAsync<T>(string roleKey, string relativePath, object body, CancellationToken cancellationToken)
    {
        FoodWrites++; Changed?.Invoke();
        throw new InvalidOperationException("Food preview verifies input and confirmation only; business writes are blocked.");
    }
    public Task<T?> PutAsync<T>(string roleKey, string relativePath, object body, CancellationToken cancellationToken)
        => PostAsync<T>(roleKey, relativePath, body, cancellationToken);
    public Task<T?> UploadAsync<T>(string roleKey, string relativePath, HttpContent body, CancellationToken cancellationToken)
        => throw new InvalidOperationException("Preview uploads are blocked.");

    public Task<보호지원ListResponse> 목록Async(string kind, int page, CancellationToken ct)
        => throw new InvalidOperationException("This preview opens one synthetic closed case only.");
    public Task<보호지원CaseResponse> 상세Async(string caseId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (caseId != CaseId) throw new InvalidOperationException("Preview case mismatch.");
        return Task.FromResult(new 보호지원CaseResponse
        {
            CaseId = CaseId, Kind = 보호지원종류Codes.분쟁, StatusCode = "closed", Revision = 3,
            SourceKind = 보호지원출처Codes.생활배송, SourceId = DeliveryId, RetentionPending = false,
            BusinessCalendarVerified = true, CreatedAtUtc = ObservedAt, UpdatedAtUtc = ObservedAt, ServerNowUtc = ObservedAt
        });
    }
    public Task<보호지원CaseResponse> 변경Async(string caseId, 보호지원CommandRequest request, CancellationToken ct)
        => throw new InvalidOperationException("Support commands are blocked in this recovery preview.");
    public Task<보호지원증거Response> 비공개확인Async(string caseId, CancellationToken ct)
        => throw new InvalidOperationException("This preview contains no private evidence.");

    public Task<NeighborhoodMemberRecoveryContextResponse> 배송복구조회Async(string requestId, NeighborhoodMemberRecoveryRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ValidateRecoveryTarget(requestId, request);
        var ownReceipt = _receipt is not null && request.ClientRequestId == _receipt.ClientRequestId;
        var row = new NeighborhoodMemberRecoveryContextResponse
        {
            RequestId = DeliveryId, SupportCaseId = CaseId, Revision = _receipt is null ? 7 : 8,
            StateCode = _receipt is null ? "Issue" : _receipt.ResolutionCode == "Resume" ? "Assigned" : "Cancelled",
            IssueCode = "BeforePickupReviewRequired", CanResume = _receipt is null, CanCancelBeforePickup = _receipt is null,
            AppliedClientRequestId = ownReceipt ? _receipt!.ClientRequestId : null,
            AppliedResolutionCode = ownReceipt ? _receipt!.ResolutionCode : null
        };
        RecoveryReads++; Changed?.Invoke();
        return Task.FromResult(row);
    }

    public Task<NeighborhoodMemberRecoveryResponse> 배송복구Async(string requestId, NeighborhoodMemberRecoveryRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); ValidateRecoveryTarget(requestId, request);
        if (request.ExpectedRevision != 7 || request.ClientRequestId == Guid.Empty || request.ResolutionCode is not ("Resume" or "CancelBeforePickup"))
            throw new InvalidOperationException("Preview recovery command mismatch.");
        var body = JsonSerializer.Serialize(request);
        if (_original is null) { _original = request; _originalBody = body; }
        else
        {
            RequestReused = _original.ClientRequestId == request.ClientRequestId && _originalBody == body;
            if (!RequestReused) throw new InvalidOperationException("Original recovery request changed.");
        }
        RecoveryWrites++;
        // stored: 저장 후 응답만 유실. retry: 첫 전송은 미저장, 명시 재확인에서 같은 요청을 저장.
        if (Scenario == "support-stored" || RecoveryWrites > 1) _receipt = request;
        Changed?.Invoke();
        if (RecoveryWrites == 1) throw new HttpRequestException("Synthetic recovery response interruption.");
        return Task.FromResult(new NeighborhoodMemberRecoveryResponse
        { RequestId = DeliveryId, Revision = 8, StateCode = request.ResolutionCode == "Resume" ? "Assigned" : "Cancelled" });
    }

    private static void ValidateRecoveryTarget(string requestId, NeighborhoodMemberRecoveryRequest request)
    {
        if (requestId != DeliveryId || request.SupportCaseId != CaseId) throw new InvalidOperationException("Preview recovery target mismatch.");
    }
}

// 계측만 따로 다시 그립니다. 부모 preview 재렌더가 제품 지원 페이지의 재조회 수명을 건드리지 않게 합니다.
public sealed class RefactoringPreviewMetrics : ComponentBase, IDisposable
{
    [Inject] public RefactoringPreviewFixture Fixture { get; set; } = default!;
    protected override void OnInitialized() => Fixture.Changed += Changed;
    private void Changed() => _ = InvokeAsync(StateHasChanged);
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "output"); builder.AddAttribute(1, "hidden", true);
        builder.AddAttribute(2, "data-refactoring-scenario", Fixture.Scenario);
        builder.AddAttribute(3, "data-food-reads", Fixture.FoodReads);
        builder.AddAttribute(4, "data-food-writes", Fixture.FoodWrites);
        builder.AddAttribute(5, "data-food-completed", Fixture.FoodCompleted ? "true" : "false");
        builder.AddAttribute(6, "data-recovery-reads", Fixture.RecoveryReads);
        builder.AddAttribute(7, "data-recovery-writes", Fixture.RecoveryWrites);
        builder.AddAttribute(8, "data-recovery-request-reused", Fixture.RequestReused ? "true" : "false");
        builder.AddAttribute(9, "data-recovery-last-resolution", Fixture.LastResolution);
        builder.AddAttribute(10, "data-recovery-receipt-stored", Fixture.ReceiptStored ? "true" : "false");
        builder.CloseElement();
    }
    public void Dispose() => Fixture.Changed -= Changed;
}
