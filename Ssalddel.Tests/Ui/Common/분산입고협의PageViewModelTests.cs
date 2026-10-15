using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 분산입고협의PageViewModelTests
{
    public static IEnumerable<object[]> Commands()
    {
        yield return ["sites", "site-a", "set-acceptance-policy", "sites/site-a/acceptance-policy", new DistributedSiteAcceptancePolicyRequest(), "SiteAcceptancePolicy"];
        yield return ["inbound", "plan-a", "propose-inbound", "inbound-plans/plan-a/lines/line-a/proposals", new DistributedInboundProposalRequest(), "InboundProposal"];
        yield return ["inbound", "plan-a", "consent-inbound", "inbound-plans/plan-a/lines/line-a/consents", new DistributedInboundConsentRequest { TermsVersion = 2, Granted = true }, "InboundConsent"];
        yield return ["inbound", "plan-a", "reject-inbound", "inbound-plans/plan-a/lines/line-a/decisions", new DistributedInboundDecisionRequest { TermsVersion = 2, Action = "Reject", Reason = "농산물은 받지 않습니다" }, "InboundDecision"];
        yield return ["inbound", "plan-a", "cancel-inbound", "inbound-plans/plan-a/lines/line-a/decisions", new DistributedInboundDecisionRequest { TermsVersion = 2, Action = "Cancel", Reason = "일정 변경" }, "InboundDecision"];
        yield return ["inbound", "plan-a", "note-inbound", "inbound-plans/plan-a/lines/line-a/notes", new DistributedInboundNoteRequest { TermsVersion = 2, Note = "도착 전에 연락해 주세요" }, "InboundNote"];
        yield return ["agreements", "agreement-a", "consent-pending-terms", "storage-agreements/approvals/consents", new DistributedStorageAgreementConsentRequest { TermsVersion = 2, Granted = true }, "AgreementConsent"];
    }

    [Theory, MemberData(nameof(Commands))]
    public async Task 저장응답이유실되어도_같은요청영수증을조회하고_중복요청하지않는다(string screen, string id, string action, string suffix, DistributedCommandRequest body, string kind)
    {
        var t = new 분산입고협의TestTransport { LoseResponse = true, AggregateId = id, ReceiptKind = kind };
        using var model = new 분산이행PageViewModel(new(t, "warehouse"), screen); await model.LoadAsync(id);
        body.ExpectedRevision = 1;
        await model.ExecuteAsync(action, suffix, body, id, [action], true);
        Assert.True(model.HasPendingCommand); Assert.Single(t.Posts); var requestId = model.RequestId;
        await model.RetryPendingAsync();
        Assert.False(model.HasPendingCommand); Assert.Null(model.Error); Assert.Single(t.Posts);
        Assert.Equal(requestId, t.Posts[0].RequestId); Assert.Contains(t.Gets, path => path.Contains("/receipts/"));
    }

    [Theory, MemberData(nameof(Commands))]
    public async Task 앱화면이재생성되어도_원요청형식과번호를복구한다(string screen, string id, string action, string suffix, DistributedCommandRequest body, string kind)
    {
        var store = new VolatileWorkflowRecoveryStore(); var t = new 분산입고협의TestTransport { LoseResponse = true, AggregateId = id, ReceiptKind = kind };
        Guid? requestId;
        using (var first = new 분산이행PageViewModel(new(t, "warehouse"), screen, recoveryStore: store))
        { await first.LoadAsync(id); body.ExpectedRevision = 1; await first.ExecuteAsync(action, suffix, body, id, [action], true); requestId = first.RequestId; Assert.True(first.HasPendingCommand); }
        using var reopened = new 분산이행PageViewModel(new(t, "warehouse"), screen, recoveryStore: store); await reopened.LoadAsync(id);
        Assert.Null(reopened.Error); Assert.False(reopened.HasPendingCommand); Assert.Single(t.Posts);
        Assert.Equal(requestId, t.Posts[0].RequestId); Assert.IsType(body.GetType(), t.Posts[0]);
    }

    [Theory]
    [InlineData("GoodsCategoryDenied", "받지 않는 품목")]
    [InlineData("CapacityExceeded", "수용량이 부족")]
    [InlineData("DistributedInboundTermsVersionMismatch", "협의 판본이 바뀌었습니다")]
    public async Task 서버거절이유는_원시코드대신_다음에할일을안내한다(string code, string expected)
    {
        var t = new 분산입고협의TestTransport { ErrorCode = code };
        using var model = new 분산이행PageViewModel(new(t, "warehouse"), "inbound"); await model.LoadAsync("plan-a");
        await model.ExecuteAsync("consent-inbound", "inbound-plans/plan-a/lines/line-a/consents", new DistributedInboundConsentRequest { ExpectedRevision = 1 }, "plan-a", ["consent-inbound"], true);
        Assert.Contains(expected, model.Error); Assert.DoesNotContain(code, model.Error); Assert.False(model.HasPendingCommand);
    }
}

internal sealed class 분산입고협의TestTransport : I분산이행Transport
{
    public event Action? Changed { add { } remove { } }
    public DistributedWorkspaceDto Workspace { get; } = new() { CurrentUserId = "actor", IsSimulation = true,
        Sites = [new() { SiteId = "site-a", Revision = 1, PublicLabel = "내 보관 공간", StatusCode = "Active", AllowedActions = ["set-acceptance-policy"],
            Locations = [new("loc-a", "site-a", "barcode-a", "1층 선반")], AcceptancePolicy = new() { AllowedCategoryCodes = ["industrial"], DeniedCategoryCodes = ["agricultural"],
                AllowFood = false, AllowedPackagingCodes = ["sealed", "boxed"], AllowedTemperatureCodes = ["ambient"], CapacityUnit = "상자", MaximumCapacity = 10, AccessHours = "평일 10~18시" } }],
        StorageAgreements = [new("agreement-a", "site-a", "loc-a", "actor", "owner-private", 1, 1, "Active", true, true,
            new() { AllowedSkus = ["test-sku"], MaximumStoredQuantity = 10, PriceOrFreeTerms = "보관 무상", AccessHours = "평일 10~18시" }, [])],
        InboundPlans = [new("plan-a", "제조업체 물품 입고", "Planned", 1, [new("line-a", "site-a", "loc-a", "owner-private", "test-sku", "lot-a", 3, 0)
            { SellerUserId = "supplier-private", AllowedActions = ["propose-inbound", "consent-inbound", "note-inbound", "reject-inbound", "cancel-inbound"],
                WaitingReasonCodes = ["DistributedInboundApprovalRequired"], NextActorLabel = "공간 제공자", InboundCollaboration = new() { TermsVersion = 1, StatusCode = "Proposed",
                    GoodsOwnerConsented = true, Terms = new() { AgreementId = "agreement-a", AgreementTermsVersion = 1, SupplierUserId = "supplier-private", SupplierTypeCode = "manufacturer",
                        GoodsOwnerUserId = "owner-private", SellerUserId = "supplier-private", Quantity = 3, ArrivalStartUtc = new(2026,10,12,1,0,0,DateTimeKind.Utc),
                        ArrivalEndUtc = new(2026,10,12,3,0,0,DateTimeKind.Utc), StorageEndsAtUtc = new(2026,10,19,3,0,0,DateTimeKind.Utc), AccessHoursConfirmed = true,
                        Goods = new() { CategoryCodes = ["industrial"], IsFood = false, PackagingCode = "boxed", TemperatureCode = "ambient", CapacityUnit = "상자", CapacityPerItem = 1 },
                        SupplyPriceOrFreeTerms = "공급 조건 합의", SupplierResponsibilityTerms = "물품 소유자 위탁", ReturnTerms = "미판매 물품 회수" } } }], ["receive"])] };
    public DistributedInboundLineDto Line => Workspace.InboundPlans[0].Lines[0];
    public void SetLine(DistributedInboundLineDto line) => Workspace.InboundPlans = [Workspace.InboundPlans[0] with { Lines = [line] }];
    public List<DistributedCommandRequest> Posts { get; } = [];
    public List<string> Gets { get; } = [];
    public bool LoseResponse { get; set; }
    public string? ErrorCode { get; set; }
    public string AggregateId { get; set; } = "plan-a";
    public string ReceiptKind { get; set; } = "InboundConsent";
    private readonly Dictionary<Guid, DistributedReceiptDto> receipts = [];
    public RoleWorkspaceIdentity Identity(string roleKey) => new("actor", 1, true);
    public Task InitializeAsync(string roleKey, CancellationToken ct) => Task.CompletedTask;
    public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken ct)
    {
        Gets.Add(path);
        if (path.Contains("/receipts/"))
        { var id = Guid.Parse(path.Split("/receipts/")[1].Split('?')[0]); if (!receipts.TryGetValue(id, out var receipt)) throw new RoleWorkspaceAccessException(404, "미접수"); return Task.FromResult((T)(object)receipt); }
        return Task.FromResult((T)(object)Workspace);
    }
    public Task<T?> PostAsync<TRequest, T>(string roleKey, string path, TRequest body, CancellationToken ct)
    {
        var command = Assert.IsAssignableFrom<DistributedCommandRequest>(body); Posts.Add(command);
        if (ErrorCode is { } error) throw new RoleWorkspaceAccessException(409, "현재 조건 확인", error);
        var revision = command.ExpectedRevision + 1;
        if (AggregateId == "site-a") Workspace.Sites[0].Revision = revision;
        if (AggregateId == "plan-a") Workspace.InboundPlans = [Workspace.InboundPlans[0] with { Revision = revision }];
        if (AggregateId == "agreement-a") Workspace.StorageAgreements = [Workspace.StorageAgreements[0] with { Revision = revision }];
        receipts[command.RequestId] = new(command.RequestId, ReceiptKind, AggregateId, null, 0, revision, DateTime.UtcNow);
        if (LoseResponse) throw new HttpRequestException("응답 유실");
        return Task.FromResult((T?)(object)new DistributedCommandResponse(command.RequestId, AggregateId, revision, "Recorded", false));
    }
    public void Dispose() { }
}
