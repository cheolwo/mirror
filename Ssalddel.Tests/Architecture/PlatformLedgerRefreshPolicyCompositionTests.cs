using 살뜰.Services.Notifications;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Architecture;

public sealed class PlatformLedgerRefreshPolicyCompositionTests
{
    [Fact]
    public void 화주와관리자원장상세는_공통30초보완조회정책을사용한다()
    {
        var policy = Read(
            "Ssalddel.Client.Infrastructure",
            "Transport/TransportRequestLedgerObserver.cs");
        var shipper = Read(
            "SsalddelApp",
            "ViewModels/Shipper/ShipperRequestDetailPageViewModel.cs");
        var adminRequestPage = Read(
            "SsalddelAdmin",
            "Components/Pages/RequestDetail.razor");
        var adminRequest = string.Join("\n", adminRequestPage, Read(
            "SsalddelAdmin",
            "Services/운송의뢰상세ReadSession.cs"));
        var adminTransport = Read(
            "SsalddelAdmin",
            "Components/Pages/TransportWorkflowDetail.razor");

        Assert.Contains(
            "FallbackPollingInterval = TimeSpan.FromSeconds(30)",
            policy);
        Assert.Contains(
            "TransportRequestLedgerRefreshPolicy.FallbackPollingInterval",
            shipper);
        Assert.Contains(
            "TransportRequestLedgerRefreshPolicy.FallbackPollingInterval",
            adminRequest);
        Assert.Contains("운송의뢰상세ReadSession", adminRequestPage);
        Assert.Contains(
            "TransportRequestLedgerRefreshPolicy.FallbackPollingInterval",
            adminTransport);
        Assert.DoesNotContain("TimeSpan.FromSeconds(15)", shipper);
        Assert.DoesNotContain("TimeSpan.FromSeconds(15)", adminRequest);
        Assert.DoesNotContain("TimeSpan.FromSeconds(15)", adminTransport);
    }

    [Fact]
    public void 운송원장실시간경로는_인증Claim그룹과_서버재조회신호만사용한다()
    {
        var contract = Read(
            "Ssalddel.Contracts",
            "Common/Transport/TransportRequestLedgerRealtimeDtos.cs");
        var hub = Read(
            "Ssalddel",
            "Hubs/TransportRequestLedgerHub.cs");
        var publisher = Read(
            "Ssalddel",
            "Services/Transport/TransportRequestLedgerRealtimeService.cs");
        var client = Read(
            "Ssalddel.Client.Infrastructure",
            "Transport/TransportRequestLedgerRealtimeClient.cs");
        var program = Read("Ssalddel", "Program.cs");

        Assert.Contains("HubPath = \"/hubs/transport-ledger\"", contract);
        Assert.Contains("[Authorize]", hub);
        Assert.Contains("FindFirstValue(ClaimTypes.NameIdentifier)", hub);
        Assert.DoesNotContain("JoinGroup", hub);
        Assert.Contains("TransportRequestLedgerHub.UserGroup", publisher);
        Assert.Contains("request.주문자UserId", publisher);
        Assert.Contains("transport?.확정기사Id", publisher);
        Assert.Contains("_observer.RequestRefresh", client);
        Assert.Contains("path.StartsWithSegments(TransportRequestLedgerRealtime.HubPath)", program);
    }

    [Fact]
    public void 배차수락Push도_인증주문자UserId를우선하고_화주Id는호환값으로유지한다()
    {
        var source = Read(
            "Ssalddel",
            "Application/Driver/DispatchAction/Handlers/배차수락사후처리EventHandler.Notification.cs");

        Assert.Contains("CargoWorkflowProgressNotifications.ReserveAcceptedAsync(", source);
        var request = new 화주운송의뢰
        {
            의뢰Id = "composition-request", 주문자UserId = "authenticated-orderer", 화주Id = "legacy-business"
        };
        var row = CargoWorkflowProgressNotifications.Create("cargo.accepted", request, null, "driver", DateTime.UtcNow);
        Assert.Equal("authenticated-orderer", Command알림Payload.Parse(row.PayloadJson).TargetUserId);
        request.주문자UserId = string.Empty;
        row = CargoWorkflowProgressNotifications.Create("cargo.accepted", request, null, "driver", DateTime.UtcNow);
        Assert.Equal("legacy-business", Command알림Payload.Parse(row.PayloadJson).TargetUserId);
    }

    [Fact]
    public void 음식점수락후생성된배차대기는_관련자원장실시간재조회신호를발행한다()
    {
        var source = Read(
            "Ssalddel",
            "Services/Food/음식배차요청OutboxService.cs");

        Assert.Contains("ITransportRequestLedgerRealtimeService transportLedgerRealtime", source);
        Assert.Contains("transportLedgerRealtime.PublishAsync(", source);
        Assert.Contains("order.주문번호", source);
        Assert.Contains("FoodDispatchRequested", source);
    }

    private static string Read(string project, string relativePath)
        => File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            project,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ssalddel.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Ssalddel 저장소 루트를 찾지 못했습니다.");
    }
}
