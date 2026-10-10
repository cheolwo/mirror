using System.Text.Json;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

namespace Ssalddel.Tests.Ui.Common;

public sealed class DistributedFulfillmentViewModelTests
{
    [Theory]
    [InlineData("warehouse", "sites", "warehouse")]
    [InlineData("shipper", "inventory", "shipper")]
    [InlineData("cargo-driver", "round", "driver")]
    [InlineData("orderer", "orders", "orderer")]
    [InlineData("operator", "waves", "warehouse")]
    public async Task 실제역할의_인증된_조회경로를_사용한다(string role, string screen, string serverRole)
    {
        var transport = new DistributedUiTransport(); using var model = Model(transport, role, screen);
        await model.LoadAsync(); Assert.True(model.IsLoaded); Assert.Equal($"api/v1/distributed-fulfillment/workspace?roleKey={serverRole}", Assert.Single(transport.Gets)); Assert.Empty(transport.Posts);
    }
    [Fact] public async Task 익명은_업무를_조회하지않는다()
    { var t = new DistributedUiTransport { CurrentIdentity = new(null, 0, false) }; using var model = Model(t); await model.LoadAsync(); Assert.True(model.RequiresLogin); Assert.Null(model.Workspace); Assert.Empty(t.Gets); }
    [Fact] public async Task 판본profile이_다르면_정상빈목록으로_인증하지않는다()
    { var t = new DistributedUiTransport(); t.Workspace.Profile = "ordinary"; using var m = Model(t); await m.LoadAsync(); Assert.False(m.IsLoaded); Assert.NotNull(m.Error); Assert.Null(m.Workspace); }
    [Fact] public async Task 다른사용자응답은_화면에_보존하지않는다()
    { var t = new DistributedUiTransport(); t.Workspace.CurrentUserId = "other"; using var m = Model(t); await m.LoadAsync(); Assert.Null(m.Workspace); Assert.NotNull(m.Error); }
    [Fact] public async Task 상품소유자와_본인판매자가_다른_정당한재고를_보존한다()
    {
        var t = new DistributedUiTransport(); t.Workspace.Lots = [new("lot-a", "site-a", "loc-a", "owner-a", "actor", "sku-a", "lot", 1, 4, 3, 1)];
        using var m = Model(t, "shipper", "inventory"); await m.LoadAsync(); Assert.True(m.IsLoaded); Assert.Equal("owner-a", Assert.Single(m.Workspace!.Lots).OwnerUserId);
    }
    [Fact] public async Task 타판매자_재고응답은_차단한다()
    {
        var t = new DistributedUiTransport(); t.Workspace.Lots = [new("lot-a", "site-a", "loc-a", "other-owner", "other-seller", "sku-a", "lot", 1, 4, 3, 1)];
        using var m = Model(t, "shipper", "inventory"); await m.LoadAsync(); Assert.Null(m.Workspace); Assert.NotNull(m.Error);
    }
    [Fact] public async Task 본인소유_위탁판매재고는_읽되_판매권한으로_바꾸지않는다()
    {
        var t = new DistributedUiTransport(); t.Workspace.Lots = [new("lot-a", "site-a", "loc-a", "actor", "delegated-seller", "sku-a", "lot", 1, 4, 3, 1)];
        using var m = Model(t, "shipper", "inventory"); await m.LoadAsync(); Assert.True(m.IsLoaded); Assert.Equal("delegated-seller", Assert.Single(m.Workspace!.Lots).SellerUserId); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 일반운영응답은_정확profile이라도_연습검증으로_인증하지않는다()
    {
        var t = new DistributedUiTransport(); t.Workspace.IsSimulation = false; using var m = Model(t); await m.LoadAsync(); Assert.False(m.IsLoaded); Assert.NotNull(m.Error);
    }
    [Fact] public async Task 고객응답의_타인주문과_거점상세는_차단한다()
    {
        var t = DistributedUiTransport.Round(); t.Workspace.Sites = []; using var m = Model(t, "orderer", "orders"); await m.LoadAsync(); Assert.Null(m.Workspace); Assert.NotNull(m.Error);
    }
    [Fact] public async Task 저장된_방문순서를_화면이_임의정렬하지않는다()
    {
        var t = DistributedUiTransport.Round(); var w = t.Workspace.Waves[0]; w.Stops = w.Stops.Reverse().ToArray(); using var m = Model(t, "cargo-driver", "round"); await m.LoadAsync(); Assert.False(m.IsLoaded); Assert.NotNull(m.Error);
    }
    [Fact] public async Task 동일장소의_여러주문은_현재방문피킹에_함께포함한다()
    {
        var t = DistributedUiTransport.Round(); using var m = Model(t, "cargo-driver", "round"); await m.LoadAsync("wave-a");
        Assert.Equal(2, m.CurrentPickLines.Count); Assert.Equal("site-a", m.CurrentStop!.SiteId); Assert.DoesNotContain(m.CurrentPickLines, line => line.SiteId == "site-b");
    }
    [Fact] public async Task 사용자가_확인하지않으면_허용행동도_전송하지않는다()
    {
        var t = new DistributedUiTransport(); t.Workspace.AllowedActions = ["create-site"]; using var m = Model(t); await m.LoadAsync();
        await m.ExecuteAsync("create-site", "sites", new DistributedSiteCreateRequest(), confirmed: false); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 판매자에게_묶음운영행동이_잘못내려와도_실행하지않는다()
    {
        var t = new DistributedUiTransport(); t.Workspace.AllowedActions = ["create-wave"]; using var m = Model(t, "shipper", "orders"); await m.LoadAsync();
        await m.ExecuteAsync("create-wave", "waves", new DistributedWaveCreateRequest(), confirmed: true); Assert.Empty(t.Posts); Assert.False(m.CanManageWaves);
    }
    [Theory]
    [InlineData("wrong-location", "sku-a", 1)]
    [InlineData("barcode-a", "wrong-sku", 1)]
    [InlineData("barcode-a", "sku-a", 0)]
    [InlineData("barcode-a", "sku-a", 8)]
    public async Task 위치_상품_남은수량_순서가_틀리면_피킹을_전송하지않는다(string barcode, string sku, int quantity)
    {
        var t = DistributedUiTransport.Round(); using var m = Model(t, "cargo-driver", "round"); await m.LoadAsync("wave-a"); await m.PickAsync("res-a", barcode, sku, quantity, "observed", true); Assert.Empty(t.Posts); Assert.NotNull(m.ValidationError);
    }
    [Fact] public async Task 미래거점의_예약은_현재피킹으로_전송하지않는다()
    {
        var t = DistributedUiTransport.Round(); using var m = Model(t, "cargo-driver", "round"); await m.LoadAsync("wave-a"); await m.PickAsync("res-c", "barcode-b", "sku-c", 1, "observed", true); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 피킹은_서버예약소유자와_현재stop판본을_전송하고_근거를_대조한다()
    {
        var t = DistributedUiTransport.Round(); using var m = Model(t, "cargo-driver", "round"); await m.LoadAsync("wave-a"); await PhotoPick(m);
        var request = Assert.IsType<DistributedPickBatchRequest>(Assert.Single(t.Posts).Body); Assert.Equal("owner-a", Assert.Single(request.Lines).OwnerUserId); Assert.Equal("stop-a", request.StopId); Assert.Equal(1, request.ExpectedRevision); Assert.Equal(2, m.SelectedWave!.Revision); Assert.False(m.HasPendingCommand); Assert.NotNull(m.Message);
    }
    [Fact] public async Task 불명확응답은_같은요청번호와_같은입력으로_재확인한다()
    {
        var t = DistributedUiTransport.Round(); var attempt = 0; t.BeforePost = _ => { if (++attempt == 1) throw new HttpRequestException("lost acknowledgement"); };
        using var m = Model(t, "cargo-driver", "round"); await m.LoadAsync("wave-a"); var body = new DistributedRoundAcceptRequest { ExpectedRevision = 1, AcknowledgedWarningCodes = ["window"] };
        await m.ExecuteAsync("accept", "waves/wave-a/accept", body, "wave-a", ["accept"], true); var id = m.RequestId; body.AcknowledgedWarningCodes = ["mutated"]; Assert.True(m.HasPendingCommand); Assert.Null(m.Message);
        await m.RetryPendingAsync(); Assert.Equal(2, t.Posts.Count); Assert.All(t.Posts, post => Assert.Equal(id, post.Body.RequestId)); Assert.All(t.Posts, post => Assert.Equal("window", Assert.Single(Assert.IsType<DistributedRoundAcceptRequest>(post.Body).AcknowledgedWarningCodes))); Assert.False(m.HasPendingCommand);
    }
    [Fact] public async Task ACK후_영수증조회실패는_POST를_다시하지않는다()
    {
        var t = DistributedUiTransport.Round(); var failed = false; t.BeforeGet = path => { if (path.Contains("/receipts/") && !failed) { failed = true; throw new HttpRequestException("receipt unavailable"); } };
        using var m = Model(t, "cargo-driver", "round"); await m.LoadAsync("wave-a"); await PhotoPick(m); Assert.True(m.HasPendingCommand); await m.RetryPendingAsync(); Assert.Single(t.Posts); Assert.False(m.HasPendingCommand);
    }
    [Fact] public async Task ACK후_다른수량영수증은_성공표시하지않는다()
    {
        var t = DistributedUiTransport.Round(); t.AlterReceipt = receipt => receipt with { Quantity = 7 }; using var m = Model(t, "cargo-driver", "round"); await m.LoadAsync("wave-a"); await PhotoPick(m); Assert.True(m.HasPendingCommand); Assert.Null(m.Message); Assert.Null(m.Workspace);
    }
    [Fact] public async Task ACK후_다른종류영수증은_성공표시하지않는다()
    {
        var t = DistributedUiTransport.Round(); t.AlterReceipt = receipt => receipt with { Kind = "Deliver" }; using var m = Model(t, "cargo-driver", "round"); await m.LoadAsync("wave-a"); await PhotoPick(m); Assert.True(m.HasPendingCommand); Assert.Null(m.Message);
    }
    [Fact] public async Task 처리후_오래된판본조회는_완료로_표시하지않는다()
    {
        var t = DistributedUiTransport.Round(); t.KeepOldRevision = true; using var m = Model(t, "cargo-driver", "round"); await m.LoadAsync("wave-a"); await PhotoPick(m); Assert.True(m.HasPendingCommand); Assert.Null(m.Message);
    }
    [Fact] public async Task 충돌응답은_입력을_자동재실행하지않고_새조회를_허용한다()
    {
        var t = DistributedUiTransport.Round(); t.BeforePost = _ => throw new RoleWorkspaceAccessException(409, "conflict"); using var m = Model(t, "cargo-driver", "round"); await m.LoadAsync("wave-a"); await PhotoPick(m); Assert.False(m.HasPendingCommand); Assert.Null(m.Message); await m.CancelPhotoWorkAsync(); Assert.True(m.IsLoaded); Assert.Single(t.Posts);
    }
    [Fact] public async Task 로그인변경은_진행중응답과_비공개업무를_폐기한다()
    {
        var t = new DistributedUiTransport(); var completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously); t.CustomGet = (_, _) => completion.Task;
        using var m = Model(t); var load = m.LoadAsync(); t.SetIdentity(new("other", 2, true)); completion.SetResult(t.Workspace); await load; Assert.Null(m.Workspace); Assert.False(m.IsLoaded); Assert.False(m.IsBusy);
    }
    [Fact] public async Task 로그아웃은_확인중요청을_다른계정에_넘기지않는다()
    {
        var t = DistributedUiTransport.Round(); t.BeforePost = _ => throw new HttpRequestException("timeout"); using var m = Model(t, "cargo-driver", "round"); await m.LoadAsync("wave-a"); await PhotoPick(m); Assert.True(m.HasPendingCommand); t.SetIdentity(new(null, 2, false)); await m.RetryPendingAsync(); Assert.False(m.HasPendingCommand); Assert.True(m.RequiresLogin); Assert.Single(t.Posts);
    }
    [Fact] public async Task 피킹방문지에서는_고객전달을_실행하지않는다()
    {
        var t = DistributedUiTransport.Round(); using var m = Model(t, "cargo-driver", "round"); await m.LoadAsync("wave-a"); await m.DeliverAsync("order-a", "observed", true); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 고객수령확인은_기사전달과_별도_API와_서버영수증으로_확인한다()
    {
        var t = DistributedUiTransport.Customer(); using var m = Model(t, "orderer", "orders"); await m.LoadAsync("wave-a"); await m.AcknowledgeAsync("order-a", "received personally", true);
        var post = Assert.Single(t.Posts); Assert.Contains("/acknowledgements?roleKey=orderer", post.Path); Assert.IsType<DistributedAcknowledgementRequest>(post.Body); Assert.NotNull(m.Message); Assert.Contains(t.Gets, path => path.Contains("/receipts/"));
    }
    [Fact] public async Task 현재배송방문이라도_주문전부_피킹전에는_전달하지않는다()
    {
        var t = DistributedUiTransport.Round(); t.Workspace.Waves[0].CurrentStopId = "stop-c"; using var m = Model(t, "cargo-driver", "round"); await m.LoadAsync("wave-a"); await m.DeliverAsync("order-a", "actual delivery", true); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 전달영수증후_다음stop을_재조회해_피킹작업으로_교체한다()
    {
        var t = DistributedUiTransport.Round(); var wave = t.Workspace.Waves[0]; wave.CurrentStopId = "stop-c";
        wave.Orders = wave.Orders.Select(order => order.OrderId == "order-a" ? order with { PickedQuantity = order.ReservedQuantity } : order).ToArray();
        wave.Reservations = wave.Reservations.Select(line => line.OrderId == "order-a" ? line with { PickedQuantity = line.Quantity, AllowedActions = ["deliver"] } : line).ToArray();
        t.BeforePost = request => { if (request is DistributedDeliveryBatchRequest) wave.CurrentStopId = "stop-b"; };
        using var m = Model(t, "cargo-driver", "round"); await m.LoadAsync("wave-a"); Assert.Single(m.CurrentDeliveryOrders);
        await PhotoDeliver(m); Assert.Empty(m.CurrentDeliveryOrders); Assert.Equal("stop-b", m.CurrentStop!.StopId); Assert.Equal("res-c", Assert.Single(m.CurrentPickLines).ReservationId); Assert.NotNull(m.Message);
    }
    [Fact] public async Task 마지막피킹_영수증후_다음거점의_새예약만_표시한다()
    {
        var t = DistributedUiTransport.Round(); var wave = t.Workspace.Waves[0]; t.BeforePost = _ => wave.CurrentStopId = "stop-b";
        using var m = Model(t, "cargo-driver", "round"); await m.LoadAsync("wave-a"); await PhotoPick(m, 2);
        Assert.Equal("stop-b", m.CurrentStop!.StopId); Assert.Equal("res-c", Assert.Single(m.CurrentPickLines).ReservationId); Assert.NotNull(m.Message);
    }
    [Fact] public async Task 기사전달표시만으로_고객ACK를_허용하지않는다()
    {
        var t = DistributedUiTransport.Customer(); t.Workspace.Waves[0].Orders = t.Workspace.Waves[0].Orders.Select(order => order with { AllowedActions = [] }).ToArray();
        using var m = Model(t, "orderer", "orders"); await m.LoadAsync("wave-a"); await m.AcknowledgeAsync("order-a", "observed", true); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 재진입요청번호는_POST없이_영수증과_현재상태를_확인한다()
    {
        var t = DistributedUiTransport.Round(); var id = Guid.NewGuid(); t.Receipts[id] = new(id, "Accept", "wave-a", null, 0, 1, DateTime.UtcNow);
        using var m = Model(t, "cargo-driver", "round"); await m.RecoverReceiptAsync(id); Assert.NotNull(m.Message); Assert.Empty(t.Posts); Assert.True(m.IsLoaded);
    }
    [Theory] [InlineData("waves/../orders")] [InlineData("waves/id?roleKey=other/orders")] [InlineData("https://example.invalid")]
    public async Task 임의경로를_인증client에_전송하지않는다(string path)
    { var t = new DistributedUiTransport(); var client = new 분산이행Client(t, "warehouse"); await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(path, new DistributedCommandRequest { RequestId = Guid.NewGuid() }, default)); Assert.Empty(t.Posts); }
    [Theory]
    [InlineData("orderer", "orders")]
    [InlineData("shipper", "orders")]
    [InlineData("warehouse", "waves")]
    [InlineData("operator", "waves")]
    public async Task 허용된주문의_취소는_같은요청근거와_최신상태를_대조한다(string role, string screen)
    {
        var t = DistributedUiTransport.Customer(); t.Workspace.Waves[0].Orders = t.Workspace.Waves[0].Orders.Select(order => order with { AllowedActions = ["cancel"] }).ToArray();
        using var m = Model(t, role, screen); await m.LoadAsync("wave-a"); await m.CancelAsync("order-a", "본인 주문 취소", true);
        var post = Assert.Single(t.Posts); var request = Assert.IsType<DistributedCancelRequest>(post.Body);
        Assert.Equal("order-a", request.OrderId); Assert.Equal("본인 주문 취소", request.Reason); Assert.Contains("/cancel?roleKey=", post.Path); Assert.Contains(t.Gets, path => path.Contains("/receipts/")); Assert.NotNull(m.Message);
    }
    [Theory] [InlineData(false, true, "order-a")] [InlineData(true, false, "order-a")] [InlineData(true, true, "other-order")]
    public async Task 취소확인_권한_선택주문이_다르면_다른주문을_처리하지않는다(bool confirmed, bool allowed, string selected)
    {
        var t = DistributedUiTransport.Customer(); t.Workspace.Waves[0].Orders = t.Workspace.Waves[0].Orders.Select(order => order with { AllowedActions = allowed ? ["cancel"] : [] }).ToArray();
        using var m = Model(t, "orderer", "orders"); await m.LoadAsync("wave-a"); await m.CancelAsync(selected, "취소", confirmed); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 기사는_잘못내려온취소권한으로도_고객주문을_취소하지않는다()
    {
        var t = DistributedUiTransport.Round(); t.Workspace.Waves[0].Orders = t.Workspace.Waves[0].Orders.Select(order => order with { AllowedActions = ["cancel"] }).ToArray();
        using var m = Model(t, "cargo-driver", "round"); await m.LoadAsync("wave-a"); await m.CancelAsync("order-a", "임의 취소", true); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 가용0_기존출고예약은_재차감없이_원예약수량으로_명시편입한다()
    {
        var t = ReservedOrder(); using var m = Model(t, "shipper", "orders"); await m.LoadAsync("wave-a");
        await m.AddOrderAsync("cargo-a", "내 기존 예약", [new() { LotId = "lot-a", Quantity = 3, ExistingOutboundPlanId = 17 }], true);
        var request = Assert.IsType<DistributedOrderRequest>(Assert.Single(t.Posts).Body); var line = Assert.Single(request.Lines);
        Assert.Equal(17, line.ExistingOutboundPlanId); Assert.Equal(3, line.Quantity); Assert.Equal(0, Assert.Single(t.Workspace.Lots).AvailableQuantity); Assert.NotNull(m.Message);
    }
    [Theory] [InlineData(2, 17)] [InlineData(3, 18)]
    public async Task 기존예약의_수량이나_조회하지않은예약은_편입하지않는다(int quantity, long id)
    {
        var t = ReservedOrder(); using var m = Model(t, "shipper", "orders"); await m.LoadAsync("wave-a");
        await m.AddOrderAsync("cargo-a", "변경 예약", [new() { LotId = "lot-a", Quantity = quantity, ExistingOutboundPlanId = id }], true); Assert.Empty(t.Posts); Assert.NotNull(m.ValidationError);
    }
    [Fact] public async Task 새예약과_기존예약은_한명령에_섞지않는다()
    {
        var t = ReservedOrder(); using var m = Model(t, "shipper", "orders"); await m.LoadAsync("wave-a");
        await m.AddOrderAsync("cargo-a", "혼합", [new() { LotId = "lot-a", Quantity = 3, ExistingOutboundPlanId = 17 }, new() { LotId = "lot-a", Quantity = 1 }], true); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 가용0_새예약은_기존예약처럼_처리하지않는다()
    {
        var t = ReservedOrder(); using var m = Model(t, "shipper", "orders"); await m.LoadAsync("wave-a"); await m.AddOrderAsync("cargo-a", "새예약", [new() { LotId = "lot-a", Quantity = 1 }], true); Assert.Empty(t.Posts);
    }
    [Theory] [InlineData("")] [InlineData("invented")] [InlineData("FreightVehicleAdvisory,invented")]
    public async Task 실제경고를_확인하지않거나_조회하지않은코드를_추가하면_수락하지않는다(string codes)
    {
        var t = DistributedUiTransport.Round(); t.Workspace.Waves[0].AcceptanceWarnings = [new("cargo-a", "FreightVehicleAdvisory", "실제 차량 적합 조건")];
        using var m = Model(t, "cargo-driver", "round"); await m.LoadAsync("wave-a"); await m.AcceptAsync(codes.Length == 0 ? [] : codes.Split(','), true); Assert.Empty(t.Posts); Assert.NotNull(m.ValidationError);
    }
    [Fact] public async Task 실제경고만_명시확인한_수락은_그코드와_현재판본을_전송한다()
    {
        var t = DistributedUiTransport.Round(); t.Workspace.Waves[0].AcceptanceWarnings = [new("cargo-a", "FreightVehicleAdvisory", "실제 차량 적합 조건")]; t.Workspace.Waves[0].BlockCodes = ["UnrelatedPlanCondition"];
        using var m = Model(t, "cargo-driver", "round"); await m.LoadAsync("wave-a"); await m.AcceptAsync(["FreightVehicleAdvisory"], true);
        var request = Assert.IsType<DistributedRoundAcceptRequest>(Assert.Single(t.Posts).Body); Assert.Equal("FreightVehicleAdvisory", Assert.Single(request.AcknowledgedWarningCodes)); Assert.Equal(1, request.ExpectedRevision); Assert.NotNull(m.Message);
    }
    [Theory]
    [InlineData("Pending", true, true)]
    [InlineData("Blocked", true, false)]
    [InlineData("Connected", true, false)]
    [InlineData("Pending", false, false)]
    public async Task 원주문_연결재확인은_현재판본과_허용행동이_있을때만_전송한다(string status, bool currentRevisionKnown, bool sends)
    {
        var t = DistributedUiTransport.Customer();
        var order = t.Workspace.Waves[0].Orders[0] with { RootOrderLedgerId = "root-a", RootOrderLedgerRevision = currentRevisionKnown ? 7 : null,
            OrderLedgerConnectionStatusCode = status, AllowedActions = ["retry-order-ledger"] };
        t.Workspace.Waves[0].Orders = [order];
        using var m = Model(t, "orderer", "orders"); await m.LoadAsync("wave-a");
        await m.RetryOrderLedgerAsync("order-a");
        Assert.Equal(sends ? 1 : 0, t.Posts.Count);
        if (sends)
        {
            var post = Assert.Single(t.Posts);
            Assert.Contains("/waves/wave-a/orders/order-a/ledger?roleKey=orderer", post.Path);
            Assert.Equal(7, Assert.IsType<DistributedOrderLedgerLinkRequest>(post.Body).ExpectedRootRevision);
            Assert.False(m.HasPendingCommand);
            Assert.Contains("아직 확인 중", m.Message);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 원주문은_조회한목록의선택판본만보내고_미연결작성은기존요청을유지한다(bool connect)
    {
        var t = ReservedOrder(); using var m = Model(t, "shipper", "orders"); await m.LoadAsync("wave-a");
        if (connect) { await m.LoadRootOrderCandidatesAsync("cargo-a"); m.SelectRootOrder("root-a"); Assert.True(m.HasSelectedRootOrder); }
        await m.AddOrderAsync("cargo-a", "같은 배송 주문", [new() { LotId = "lot-a", Quantity = 3, ExistingOutboundPlanId = 17 }], true, connect);
        var request = Assert.IsType<DistributedOrderRequest>(Assert.Single(t.Posts).Body);
        Assert.Equal(connect ? "root-a" : null, request.RootOrderLedgerId); Assert.Equal(connect ? (long?)7 : null, request.ExpectedRootRevision);
        Assert.False(m.HasPendingCommand); Assert.Null(m.SelectedRootOrderId);
    }

    [Fact]
    public async Task 원주문선택을포함한_불명확접수는_동일요청과판본으로재개한다()
    {
        var t = ReservedOrder(); using var m = Model(t, "shipper", "orders"); await m.LoadAsync("wave-a");
        await m.LoadRootOrderCandidatesAsync("cargo-a"); m.SelectRootOrder("root-a"); t.BeforePost = _ => throw new IOException("lost response");
        await m.AddOrderAsync("cargo-a", "원주문 배송", [new() { LotId = "lot-a", Quantity = 3, ExistingOutboundPlanId = 17 }], true, true);
        Assert.True(m.HasPendingCommand); var id = m.RequestId; t.BeforePost = null; await m.RetryPendingAsync();
        Assert.Equal(2, t.Posts.Count); Assert.All(t.Posts, post => { var request = Assert.IsType<DistributedOrderRequest>(post.Body);
            Assert.Equal(id, request.RequestId); Assert.Equal("root-a", request.RootOrderLedgerId); Assert.Equal(7, request.ExpectedRootRevision); });
        Assert.False(m.HasPendingCommand);
    }

    [Theory]
    [InlineData("Actor")]
    [InlineData("Cargo")]
    [InlineData("Wave")]
    [InlineData("Revision")]
    [InlineData("Closed")]
    public async Task 다른문맥이나닫힌원주문후보는_선택과제출에사용하지않는다(string changed)
    {
        var t = ReservedOrder(); t.RootCandidates = changed switch
        {
            "Actor" => t.RootCandidates with { CurrentUserId = "other" }, "Cargo" => t.RootCandidates with { CargoRequestId = "cargo-b" },
            "Wave" => t.RootCandidates with { WaveId = "wave-b" }, "Revision" => t.RootCandidates with { WaveRevision = 2 },
            _ => t.RootCandidates with { Candidates = [new("root-a", 7, "끝난 주문", "완료")] }
        };
        using var m = Model(t, "shipper", "orders"); await m.LoadAsync("wave-a"); await m.LoadRootOrderCandidatesAsync("cargo-a");
        m.SelectRootOrder("root-a"); Assert.False(m.HasSelectedRootOrder); Assert.NotNull(m.RootOrdersError);
        await m.AddOrderAsync("cargo-a", "원주문 배송", [new() { LotId = "lot-a", Quantity = 3, ExistingOutboundPlanId = 17 }], true, true);
        Assert.Empty(t.Posts); Assert.NotNull(m.ValidationError); Assert.NotNull(m.Workspace);
    }

    [Fact]
    public async Task 원주문재조회실패는_이전선택을무효화하고_다시성공한현재판본을선택한다()
    {
        var t = ReservedOrder(); using var m = Model(t, "shipper", "orders"); await m.LoadAsync("wave-a");
        await m.LoadRootOrderCandidatesAsync("cargo-a"); m.SelectRootOrder("root-a");
        t.BeforeGet = path => { if (path.Contains("order-ledger-candidates")) throw new IOException("offline"); };
        await m.LoadRootOrderCandidatesAsync("cargo-a"); Assert.Null(m.SelectedRootOrderId); Assert.Empty(m.RootOrderCandidates);
        Assert.NotNull(m.RootOrdersError); Assert.NotNull(m.Workspace); Assert.NotNull(m.SelectedWave);
        await m.AddOrderAsync("cargo-a", "원주문 배송", [new() { LotId = "lot-a", Quantity = 3, ExistingOutboundPlanId = 17 }], true, true); Assert.Empty(t.Posts);
        t.BeforeGet = null; t.RootCandidates = t.RootCandidates with { Candidates = [new("root-a", 8, "변경된 현재 주문", "진행중")] };
        await m.LoadRootOrderCandidatesAsync("cargo-a"); m.SelectRootOrder("unqueried-root"); Assert.Null(m.SelectedRootOrderId);
        m.SelectRootOrder("root-a"); await m.AddOrderAsync("cargo-a", "원주문 배송", [new() { LotId = "lot-a", Quantity = 3, ExistingOutboundPlanId = 17 }], true, true);
        Assert.Equal(8, Assert.IsType<DistributedOrderRequest>(Assert.Single(t.Posts).Body).ExpectedRootRevision);
    }

    [Fact]
    public async Task 원주문서버401은_로컬로그인캐시가남아도_로그인필요와오류를표시한다()
    {
        var t = ReservedOrder(); using var m = Model(t, "shipper", "orders"); await m.LoadAsync("wave-a");
        await m.LoadRootOrderCandidatesAsync("cargo-a"); m.SelectRootOrder("root-a");
        t.BeforeGet = path => { if (path.Contains("order-ledger-candidates")) throw new RoleWorkspaceAccessException(401, "expired server session"); };
        await m.LoadRootOrderCandidatesAsync("cargo-a");
        Assert.True(t.CurrentIdentity.IsAuthenticated); Assert.True(m.RequiresLogin); Assert.False(m.IsLoaded); Assert.NotNull(m.Error);
        Assert.Empty(m.RootOrderCandidates); Assert.Null(m.SelectedRootOrderId); Assert.False(m.IsLoadingRootOrders); Assert.Empty(t.Posts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 화물전환후늦게온_이전원주문조회결과는_현재목록을덮어쓰지않는다(bool failure)
    {
        var t = ReservedOrder(); t.Workspace.CargoCandidates = [.. t.Workspace.CargoCandidates, t.Workspace.CargoCandidates[0] with { CargoRequestId = "cargo-b" }];
        using var m = Model(t, "shipper", "orders"); await m.LoadAsync("wave-a");
        var old = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        t.CustomGet = (path, _) => path.Contains("cargoRequestId=cargo-a") ? old.Task
            : Task.FromResult<object>(t.RootCandidates with { CargoRequestId = "cargo-b", Candidates = [new("root-b", 9, "둘째 화물의 주문", "초안")] });
        var first = m.LoadRootOrderCandidatesAsync("cargo-a"); await m.LoadRootOrderCandidatesAsync("cargo-b"); m.SelectRootOrder("root-b");
        if (failure) old.SetException(new IOException("old cargo query failed")); else old.SetResult(t.RootCandidates);
        await first;
        Assert.Equal("root-b", m.SelectedRootOrderId); Assert.Equal("root-b", Assert.Single(m.RootOrderCandidates).RootOrderLedgerId);
        Assert.False(m.IsLoadingRootOrders); Assert.Null(m.RootOrdersError);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task 계정이나묶음전환후늦은원주문조회결과는_새업무에남지않는다(bool account, bool failure)
    {
        var t = ReservedOrder(); t.Workspace.Waves = [.. t.Workspace.Waves, new() { WaveId = "wave-b", Label = "다른 묶음", Revision = 1, AllowedActions = ["add-order"] }];
        using var m = Model(t, "shipper", "orders"); await m.LoadAsync("wave-a");
        var late = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        t.CustomGet = (path, _) => path.Contains("order-ledger-candidates") ? late.Task : Task.FromResult<object>(path.Contains("/workspace?") ? t.Workspace : t.Workspace.Waves.Single(w => path.Contains("/waves/" + w.WaveId + "?")));
        var query = m.LoadRootOrderCandidatesAsync("cargo-a");
        if (account) t.SetIdentity(new("new-actor", 2, true)); else await m.ChooseAsync("wave-b");
        if (failure) late.SetException(new IOException("old context query failed")); else late.SetResult(t.RootCandidates);
        await query;
        Assert.Empty(m.RootOrderCandidates); Assert.Null(m.SelectedRootOrderId); Assert.False(m.IsLoadingRootOrders);
        if (account) Assert.Null(m.Workspace); else Assert.Equal("wave-b", m.SelectedWave!.WaveId);
    }

    private static DistributedUiTransport ReservedOrder()
    {
        var t = DistributedUiTransport.Customer(); var now = DateTime.UtcNow; t.Workspace.Waves[0].AllowedActions = ["add-order"];
        t.Workspace.CargoCandidates = [new("cargo-a", "내 화물 표시명", now, now.AddHours(1), now, now.AddHours(2))];
        t.Workspace.Lots = [new("lot-a", "site-a", "loc-a", "owner", "actor", "sku", "원로트", 4, 3, 0, 3)];
        t.Workspace.ExistingOutboundCandidates = [new(17, "cargo-a", "lot-a", "site-a", "owner", "actor", "sku", 3, "예약 상품", "상품 소유 기업")]; return t;
    }
        private static 분산이행PageViewModel Model(DistributedUiTransport t, string role = "warehouse", string screen = "sites") => new(new(t, role), screen, new Test업무사진촬영Adapter());
    private static async Task PhotoPick(분산이행PageViewModel model, int quantity = 1)
    {
        await model.CapturePhotoAsync(DistributedPhotoWorkflowTests.PickWork(model.SelectedWave!.Revision, quantity), "피킹", "pick", "waves/wave-a/pick-batches", ["pick"], quantity);
        await model.CompletePhotoAsync(true);
    }
    private static async Task PhotoDeliver(분산이행PageViewModel model)
    {
        var work = new DistributedEvidenceWorkRequest { Kind = "Deliver", AggregateId = "wave-a", Deliver = new() { ExpectedRevision = model.SelectedWave!.Revision, StopId = "stop-c", Lines = [new() { ReservationId = "res-a", Quantity = 2 }] } };
        await model.CapturePhotoAsync(work, "전달", "deliver", "waves/wave-a/delivery-batches", ["deliver"], 2);
        await model.CompletePhotoAsync(true);
    }
}

internal sealed class DistributedUiTransport : I분산이행Transport
{
    public event Action? Changed;
    public RoleWorkspaceIdentity CurrentIdentity { get; set; } = new("actor", 1, true);
    public DistributedWorkspaceDto Workspace { get; set; } = new() { CurrentUserId = "actor", IsSimulation = true };
    public List<string> Gets { get; } = [];
    public List<(string Path, DistributedCommandRequest Body)> Posts { get; } = [];
    public List<DistributedPhotoUploadRequest> PhotoPosts { get; } = [];
    public Dictionary<Guid, DistributedPhotoEvidenceDto> PhotoEvidence { get; } = [];
    public Action<DistributedPhotoUploadRequest>? BeforePhotoPost { get; set; }
    public Action<DistributedPhotoUploadRequest>? AfterPhotoPost { get; set; }
    public Action<DistributedCommandRequest>? AfterPost { get; set; }
    public Func<DistributedPhotoEvidenceDto, DistributedPhotoEvidenceDto>? AlterPhoto { get; set; }
    public Dictionary<Guid, DistributedReceiptDto> Receipts { get; } = [];
    public Action<string>? BeforeGet { get; set; }
    public Action<DistributedCommandRequest>? BeforePost { get; set; }
    public Func<DistributedReceiptDto, DistributedReceiptDto>? AlterReceipt { get; set; }
    public Func<string, CancellationToken, Task<object>>? CustomGet { get; set; }
    public bool KeepOldRevision { get; set; }
    public DistributedRootOrderCandidatesDto RootCandidates { get; set; } = new("wave-a", "cargo-a", "actor", 1,
        [new("root-a", 7, "조회한 실제 원주문", "초안")]);
    public RoleWorkspaceIdentity Identity(string roleKey) => CurrentIdentity;
    public Task InitializeAsync(string roleKey, CancellationToken ct) => Task.CompletedTask;
    public void SetIdentity(RoleWorkspaceIdentity identity) { CurrentIdentity = identity; Changed?.Invoke(); }
    public async Task<T> GetAsync<T>(string roleKey, string path, CancellationToken ct)
    {
        Gets.Add(path); BeforeGet?.Invoke(path); if (CustomGet is not null) return (T)await CustomGet(path, ct);
        object result = path.Contains("/evidence/uploads/")
            ? PhotoEvidence.TryGetValue(Guid.Parse(path.Split("/evidence/uploads/")[1].Split('?')[0]), out var photo) ? photo : throw new RoleWorkspaceAccessException(404, "photo not recorded")
            : path.Contains("/order-ledger-candidates?") ? RootCandidates : path.Contains("/workspace?") ? Workspace : path.Contains("/receipts/")
            ? Receipts.TryGetValue(Guid.Parse(path.Split("/receipts/")[1].Split('?')[0]), out var receipt) ? receipt : throw new RoleWorkspaceAccessException(404, "not recorded") : Workspace.Waves.Single(wave => path.Contains("/waves/" + wave.WaveId + "?")); return (T)result;
    }
    public Task<T?> PostAsync<TRequest, T>(string roleKey, string path, TRequest body, CancellationToken ct)
    {
        if (body is DistributedPhotoUploadRequest photo)
        {
            PhotoPosts.Add(JsonSerializer.Deserialize<DistributedPhotoUploadRequest>(JsonSerializer.Serialize(photo))!); BeforePhotoPost?.Invoke(photo);
            var proof = new DistributedPhotoEvidenceDto("photo-" + photo.UploadRequestId.ToString("N"), photo.Work.Kind, photo.Work.AggregateId,
                분산사진업무Session.Body(photo.Work)!.RequestId, new string('a', 64), "Uploaded", DateTime.UtcNow, true, "image/jpeg", 128,
                DistributedEvidenceDigests.Payload(분산사진업무Session.Body(photo.Work)!));
            PhotoEvidence[photo.UploadRequestId] = AlterPhoto?.Invoke(proof) ?? proof; AfterPhotoPost?.Invoke(photo);
            return Task.FromResult((T?)(object)(AlterPhoto?.Invoke(proof) ?? proof));
        }
        var command = Assert.IsAssignableFrom<DistributedCommandRequest>(body); Posts.Add((path, command)); BeforePost?.Invoke(command);
        if (command is DistributedStorageAgreementRequest && path.Contains("/terms?"))
        {
            var id = path.Split("/storage-agreements/")[1].Split('/')[0]; var agreement = Workspace.StorageAgreements.Single(agreement => agreement.AgreementId == id);
            var updated = agreement with { Revision = command.ExpectedRevision + 1, TermsVersion = agreement.TermsVersion + 1, Terms = ((DistributedStorageAgreementRequest)command).Terms, SpaceOwnerConsented = false, GoodsOwnerConsented = false };
            Workspace.StorageAgreements = Workspace.StorageAgreements.Select(item => item.AgreementId == id ? updated : item).ToArray();
            Receipts[command.RequestId] = new(command.RequestId, "AgreementTerms", id, null, 0, updated.Revision, DateTime.UtcNow);
            return Task.FromResult((T?)(object)new DistributedCommandResponse(command.RequestId, id, updated.Revision, "Recorded", false));
        }
        var revision = command.ExpectedRevision + 1; var wave = Workspace.Waves.FirstOrDefault(); if (!KeepOldRevision && wave is not null) wave.Revision = revision;
        var kind = command switch { DistributedPickRequest => "Pick", DistributedDeliveryRequest => "Deliver", DistributedPickBatchRequest => "PickBatch", DistributedDeliveryBatchRequest => "DeliverBatch", DistributedInboundReceiptBatchRequest => "ReceiveBatch", DistributedReturnBatchRequest => "ReturnBatch", DistributedAcknowledgementRequest => "Acknowledge", DistributedCancelRequest => "Cancel", DistributedOrderRequest => "AddOrder", DistributedOrderLedgerLinkRequest => "LinkOrderLedger", _ => "Accept" };
        var pick = command as DistributedPickRequest;
        var quantity = command switch { DistributedPickBatchRequest batch => batch.Lines.Sum(line => line.Quantity), DistributedDeliveryBatchRequest batch => batch.Lines.Sum(line => line.Quantity), DistributedInboundReceiptBatchRequest batch => batch.Lines.Sum(line => line.Quantity), DistributedReturnBatchRequest batch => batch.Lines.Sum(line => line.Quantity), _ => pick?.Quantity ?? 0 };
        var receipt = new DistributedReceiptDto(command.RequestId, kind, wave?.WaveId ?? "site-new", pick?.ReservationId, quantity, revision, DateTime.UtcNow);
        Receipts[command.RequestId] = AlterReceipt?.Invoke(receipt) ?? receipt;
        AfterPost?.Invoke(command);
        return Task.FromResult((T?)(object)new DistributedCommandResponse(command.RequestId, receipt.AggregateId, revision, "Accepted", false));
    }
    public void Dispose() { }
    public static DistributedUiTransport Round()
    {
        var t = new DistributedUiTransport(); t.Workspace.Sites = [new() { SiteId = "site-a", PublicLabel = "첫 거점", Revision = 1, Locations = [new("loc-a", "site-a", "barcode-a", "선반 A")] }, new() { SiteId = "site-b", PublicLabel = "둘째 거점", Revision = 1, Locations = [new("loc-b", "site-b", "barcode-b", "선반 B")] }];
        var now = DateTime.UtcNow; t.Workspace.Waves = [new() { WaveId = "wave-a", RoundId = "round-a", Label = "두 거점·두 기업·두 고객", StatusCode = "Accepted", DriverId = "actor", Revision = 1, CurrentStopId = "stop-a", AllowedActions = ["pick", "accept", "deliver"],
            Stops = [new("stop-a", 1, "Pickup", "site-a", ["order-a", "order-b"], now, now, now.AddHours(1), 3, "Pending", "허용된 현재 거점", null, null), new("stop-b", 2, "Pickup", "site-b", ["order-b"], now, now, now.AddHours(1), 3, "Pending", null, null, null), new("stop-c", 3, "Delivery", null, ["order-a"], now, now, now.AddHours(2), 3, "Pending", null, null, null), new("stop-d", 4, "Delivery", null, ["order-b"], now, now, now.AddHours(2), 3, "Pending", null, null, null)],
            Orders = [new("order-a", "첫 주문", "cargo-a", "Reserved", 2, 0, 0, 0, false, false, now, now.AddHours(2), []), new("order-b", "둘째 주문", "cargo-b", "Reserved", 3, 0, 0, 0, false, false, now, now.AddHours(2), [])],
            Reservations = [new("res-a", "order-a", "lot-a", "site-a", "loc-a", "owner-a", "sku-a", 2, 0, 0, 0, 0, 1, ["pick"]), new("res-b", "order-b", "lot-b", "site-a", "loc-a", "owner-b", "sku-b", 2, 0, 0, 0, 0, 2, ["pick"]), new("res-c", "order-b", "lot-c", "site-b", "loc-b", "owner-b", "sku-c", 1, 0, 0, 0, 0, 3, ["pick"])] }]; return t;
    }
    public static DistributedUiTransport Customer()
    {
        var t = new DistributedUiTransport(); var now = DateTime.UtcNow; t.Workspace.Waves = [new() { WaveId = "wave-a", Label = "내 배송", Revision = 1,
            Orders = [new("order-a", "내 주문", "cargo-a", "Delivered", 2, 2, 2, 0, true, false, now, now.AddHours(2), ["acknowledge"])] }]; return t;
    }
}
