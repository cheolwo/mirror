using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class DistributedStockReleaseR76UiTests
{
    [Fact]
    public async Task 다른반출합의로전환하면_이전본인확인으로새합의에동의하지않는다()
    {
        var transport = new 분산입고협의TestTransport { AggregateId = "site-a", ReceiptKind = "StockReleaseConsent" };
        transport.Workspace.StockReleases = [Release("release-a"), Release("release-b") with { ProductLabel = "다른 물품" }];
        using var model = new 분산이행PageViewModel(new(transport, "warehouse"), "stock-release"); await model.LoadAsync();
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new 분산입고협의InteractionRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(() => renderer.Start<분산잔여재고반출Screen>(new() { ["Model"] = model }));
        await renderer.Dispatcher.InvokeAsync(() => renderer.Change("반출 합의", "release-a"));
        Assert.Contains("양측 동의 대기", renderer.Text()); Assert.Contains("다음 담당 · 공간 제공자 동의", renderer.Text());
        await renderer.Dispatcher.InvokeAsync(() => renderer.Change("본인의 인계 조건", true));
        await renderer.Dispatcher.InvokeAsync(() => renderer.Change("반출 합의", "release-b"));
        await renderer.Dispatcher.InvokeAsync(() => renderer.Click("반출 조건에 동의"));
        Assert.Empty(transport.Posts); Assert.Contains("다른 물품", renderer.Text());
    }

    [Fact]
    public async Task 종료할공간을바꾸면_이전공간의정리확인으로다른공간을종료하지않는다()
    {
        var transport = new 분산입고협의TestTransport { AggregateId = "site-a", ReceiptKind = "CloseSite" };
        transport.Workspace.Sites[0].AllowedActions = ["close-site"];
        transport.Workspace.Sites = [transport.Workspace.Sites[0], new() { SiteId = "site-b", Revision = 1, PublicLabel = "다른 공간", AllowedActions = ["close-site"] }];
        using var model = new 분산이행PageViewModel(new(transport, "warehouse"), "stock-release"); await model.LoadAsync();
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new 분산입고협의InteractionRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(() => renderer.Start<분산잔여재고반출Screen>(new() { ["Model"] = model }));
        await renderer.Dispatcher.InvokeAsync(() => renderer.Change("종료할 공간", "site-a"));
        await renderer.Dispatcher.InvokeAsync(() => renderer.Change("정리 확인 근거", "첫 공간 현장 확인"));
        await renderer.Dispatcher.InvokeAsync(() => renderer.Change("현장에 남은 물품", true));
        await renderer.Dispatcher.InvokeAsync(() => renderer.Change("종료할 공간", "site-b"));
        await renderer.Dispatcher.InvokeAsync(() => renderer.Click("재고·보관량 확인 후 공간 사용 종료"));
        Assert.Empty(transport.Posts);
    }

    [Fact]
    public async Task 사진절차를거치지않은직접반출명령은_서버에보내지않는다()
    {
        var transport = new 분산입고협의TestTransport();
        using var model = new 분산이행PageViewModel(new(transport, "cargo-driver"), "stock-release"); await model.LoadAsync();
        await model.ExecuteAsync("handover-stock-release", "sites/site-a/stock-release-handovers", new DistributedStockReleaseHandoverRequest
        { ExpectedRevision = 1, ReleaseId = "release-a", Quantity = 1, LocationBarcode = "barcode-a" }, "site-a", ["handover-stock-release"], true);
        Assert.Empty(transport.Posts); Assert.NotNull(model.ValidationError);
    }

    public static IEnumerable<object[]> Commands()
    {
        yield return ["propose-stock-release", "stock-releases", new DistributedStockReleaseProposalRequest { LotId = "lot-a", AssignedUserId = "driver", Quantity = 1, HandoverTerms = "현장 인계", Reason = "정리" }, "StockReleaseProposal"];
        yield return ["consent-stock-release", "stock-release-consents", new DistributedStockReleaseConsentRequest { ReleaseId = "release-a", Granted = true }, "StockReleaseConsent"];
        yield return ["cancel-stock-release", "stock-release-consents", new DistributedStockReleaseConsentRequest { ReleaseId = "release-a", CancelRemaining = true }, "StockReleaseConsent"];
        yield return ["close-site", "close", new DistributedSiteCloseRequest { NoRemainingCustodyConfirmed = true, EvidenceReference = "현장 확인" }, "CloseSite"];
    }

    [Theory, MemberData(nameof(Commands))]
    public async Task 응답유실후앱재생성은_같은요청영수증을조회하고추가명령없이복구한다(string action, string suffix, DistributedCommandRequest body, string kind)
    {
        var store = new VolatileWorkflowRecoveryStore(); var transport = new 분산입고협의TestTransport { LoseResponse = true, AggregateId = "site-a", ReceiptKind = kind };
        Guid? requestId;
        using (var first = new 분산이행PageViewModel(new(transport, "warehouse"), "stock-release", recoveryStore: store))
        {
            await first.LoadAsync(); body.ExpectedRevision = 1;
            await first.ExecuteAsync(action, "sites/site-a/" + suffix, body, "site-a", [action], true);
            requestId = first.RequestId; Assert.True(first.HasPendingCommand); Assert.Single(transport.Posts);
        }
        using var reopened = new 분산이행PageViewModel(new(transport, "warehouse"), "stock-release", recoveryStore: store); await reopened.LoadAsync();
        Assert.Null(reopened.Error); Assert.False(reopened.HasPendingCommand); Assert.Single(transport.Posts);
        Assert.Equal(requestId, transport.Posts[0].RequestId); Assert.IsType(body.GetType(), transport.Posts[0]);
        Assert.Contains(transport.Gets, x => x.Contains("/receipts/", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("DistributedSiteInboundPending", "남은 입고 수량")]
    [InlineData("DistributedStockReleaseReservationRemaining", "주문 담당자")]
    [InlineData("DistributedStockReleasePickedCustodyRemaining", "전달·반환")]
    [InlineData("DistributedSiteStockRemaining", "가용·예약·불량")]
    public async Task 종료보류는_원시코드대신다음에확인할업무를안내한다(string code, string expected)
    {
        var transport = new 분산입고협의TestTransport { ErrorCode = code, AggregateId = "site-a", ReceiptKind = "CloseSite" };
        using var model = new 분산이행PageViewModel(new(transport, "warehouse"), "stock-release"); await model.LoadAsync();
        await model.ExecuteAsync("close-site", "sites/site-a/close", new DistributedSiteCloseRequest { ExpectedRevision = 1, NoRemainingCustodyConfirmed = true, EvidenceReference = "현장 확인" }, "site-a", ["close-site"], true);
        Assert.Contains(expected, model.Error);
        Assert.DoesNotContain(code, model.Error); Assert.False(model.HasPendingCommand);
    }

    private static DistributedStockReleaseDto Release(string id) => new(id, "site-a", "내 보관 공간", 1, "lot-a", "보관 물품", "goods-owner", "actor", "driver", "AwaitingConsent", 3, 0, false, false, "현장 인계 조건", "공간 제공자 동의", ["consent-stock-release", "cancel-stock-release"]);
}
