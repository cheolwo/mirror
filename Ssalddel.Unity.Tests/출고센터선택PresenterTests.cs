using Ssalddel.Simulation.Contracts;
using Ssalddel.Unity.Warehouse;

namespace Ssalddel.Unity.Tests;

/// <summary>.NET 소비자 회귀이며 실제 Scene·스캐너·운영 재고 실행 증거가 아니다.</summary>
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "출고 후보·위치 지시·오류 복귀와 최신 세션 재조회 표시 계약을 검증한다.",
    Boundary = "Presenter 자동 시험이며 실제 Unity View·Scene·Play Mode 검증을 뜻하지 않는다.")]
public sealed class 출고센터선택PresenterTests
{
    [Fact]
    public async Task 센터선택은_미리보기만_바꾸고_초안을_외부변경으로부터_보존한다()
    {
        var port = new Port();
        var presenter = Create(port);
        await presenter.조회Async();
        var request = Order();
        await presenter.주문선택Async(request);
        request.Lines[0].Quantity = 999;
        Assert.Equal(0, port.Attempts);
        Assert.True(presenter.예약확정가능);
        Assert.Equal(2, presenter.센터후보.Length);
        Assert.Contains("중랑 센터", presenter.센터후보[0].이름);
        Assert.Contains("선반 LOC-A", Assert.Single(presenter.선반담당지시));

        await presenter.센터선택Async(new[] { "center:b" });
        await presenter.예약확정Async("command:reserve");

        Assert.Equal(new[] { "center:b" }, port.LastCommand!.Order!.AllowedCenterStableIds);
        Assert.Equal(2m, port.LastCommand.Order.Lines.Single().Quantity);
        Assert.Equal(1, port.Attempts);
        Assert.Equal(2, port.Reads);
        Assert.Equal(8, presenter.현재개정);
        Assert.Equal(SimulationFulfillmentStateCodes.Reserved, presenter.현재주문상태);
        Assert.Contains("공릉 센터", Assert.Single(presenter.선반담당지시));
        Assert.Equal(new[] { "공릉 센터" }, presenter.선택센터목록);
        Assert.Contains("담당 나래", Assert.Single(presenter.선반담당지시));
        Assert.False(presenter.예약확정가능);
    }

    [Fact]
    public async Task 센터와분할조건은_다시판정하고_존재하지않는센터는_명령으로보내지않는다()
    {
        var port = new Port();
        var presenter = Create(port);
        await presenter.조회Async();
        await presenter.주문선택Async(Order());
        await presenter.분할출고설정Async(true);
        await presenter.센터선택Async(new[] { "center:a", "center:b" });
        Assert.Equal(3, port.Previews);
        await Assert.ThrowsAsync<InvalidOperationException>(() => presenter.센터선택Async(new[] { "center:unknown" }));
        Assert.Equal(3, port.Previews);
        Assert.Equal(0, port.Attempts);
    }

    [Fact]
    public async Task 불가능한미리보기는_사유를보이고_예약하지않는다()
    {
        var port = new Port { Rejected = true };
        var presenter = Create(port);
        await presenter.조회Async();
        await presenter.주문선택Async(Order());
        Assert.False(presenter.예약확정가능);
        Assert.False(presenter.센터후보[0].선택가능);
        Assert.Contains("WorkerUnavailableOrCapacityExceeded", presenter.보류사유코드);
        Assert.Contains("담당 직원 부재", Assert.Single(presenter.보류사유));
        await Assert.ThrowsAsync<InvalidOperationException>(() => presenter.예약확정Async("command:rejected"));
        Assert.Equal(0, port.Attempts);
    }

    [Fact]
    public async Task stale확정은_한번만보내고_재조회하며_예전안을_재사용하지않는다()
    {
        var port = new Port { AdvanceBeforeConfirm = true };
        var presenter = Create(port);
        await presenter.조회Async();
        await presenter.주문선택Async(Order());
        await Assert.ThrowsAsync<InvalidOperationException>(() => presenter.예약확정Async("command:stale"));
        Assert.Equal(1, port.Attempts);
        Assert.Equal(2, port.Reads);
        Assert.Equal(8, presenter.현재개정);
        Assert.Empty(presenter.센터후보);
        Assert.False(presenter.예약확정가능);
        Assert.Contains("자동 재전송하지 않았", presenter.마지막안내);
        await Assert.ThrowsAsync<InvalidOperationException>(() => presenter.예약확정Async("command:stale-retry"));
        Assert.Equal(1, port.Attempts);
    }

    [Fact]
    public async Task 확정후조회실패를_성공으로위장하지않고_다시조회해_복구한다()
    {
        var port = new Port { FailReadAfterConfirm = true };
        var presenter = Create(port);
        await presenter.조회Async();
        await presenter.주문선택Async(Order());
        await Assert.ThrowsAsync<IOException>(() => presenter.예약확정Async("command:read-loss"));
        Assert.False(presenter.조회됨);
        Assert.False(presenter.예약확정가능);
        Assert.Empty(presenter.선반담당지시);
        Assert.Contains("조회하지 못", presenter.마지막안내);
        port.FailRead = false;
        await presenter.조회Async();
        Assert.Equal(SimulationFulfillmentStateCodes.Reserved, presenter.현재주문상태);
        Assert.Equal(1, port.Attempts);
    }

    [Fact]
    public async Task 저장된주문선택후_현재지시의_실제입력만_단계명령으로보낸다()
    {
        var port = new Port();
        var first = Create(port);
        await first.조회Async();
        await first.주문선택Async(Order());
        await first.예약확정Async("command:reserve");
        var presenter = Create(port);
        await presenter.조회Async();
        Assert.Equal(new[] { "order:one" }, presenter.확정주문목록);
        await presenter.확정주문선택Async("order:one");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            presenter.피킹확인Async("command:foreign", "allocation:other", "LOC-A", "PRODUCT-ONE", 2m));
        Assert.Equal(1, port.Attempts);
        await presenter.피킹확인Async("command:pick", "allocation:one", "LOC-A", "PRODUCT-ONE", 2m);
        Assert.Equal(SimulationFulfillmentActionCodes.Pick, port.LastCommand!.ActionCode);
        Assert.Equal("LOC-A", port.LastCommand.LocationBarcode);
        Assert.Equal("PRODUCT-ONE", port.LastCommand.ProductBarcode);
        Assert.Equal(2m, port.LastCommand.Quantity);
        Assert.Equal(SimulationFulfillmentStateCodes.Picking, presenter.현재주문상태);
        Assert.Equal("allocation:one", Assert.Single(presenter.피킹지시).배분코드);
        Assert.Equal(0m, Assert.Single(presenter.피킹지시).남은수량);
        await presenter.출고준비확인Async("command:pack");
        Assert.Equal(SimulationFulfillmentStateCodes.PackReady, presenter.현재주문상태);
        Assert.Contains("실제 기사 인계는 구분", presenter.마지막안내);
    }

    [Fact]
    public async Task 다른세션이나미구성은_이전상태와섞지않는다()
    {
        var port = new Port { Unconfigured = true };
        var presenter = Create(port);
        await presenter.조회Async();
        Assert.True(presenter.조회됨);
        Assert.False(presenter.구성됨);
        await Assert.ThrowsAsync<InvalidOperationException>(() => presenter.주문선택Async(Order()));
        port.ReturnOtherSession = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => presenter.조회Async());
        Assert.False(presenter.조회됨);
        Assert.Equal(0, port.Attempts);
    }

    [Fact]
    public async Task 취소된입력은_조회나확정을_실행하지않는다()
    {
        var port = new Port();
        var presenter = Create(port);
        using var token = new CancellationTokenSource(); token.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => presenter.조회Async(token.Token));
        Assert.Equal(0, port.Reads);
        Assert.Equal(0, port.Attempts);
    }

    private static 출고센터선택Presenter Create(Port port) => new(port, port.Read, "session:one");
    private static SimulationFulfillmentOrderRequest Order() => new()
    {
        OrderStableId = "order:one",
        Lines = new[] { new SimulationFulfillmentOrderLine
        {
            LineStableId = "line:one", ProductStableId = "product:one", ProductBarcode = "PRODUCT-ONE", UnitCode = "piece", Quantity = 2m,
        } },
    };

    private sealed class Port : ISimulationFulfillmentRuntime
    {
        public int Reads, Previews, Attempts;
        public long Revision = 7;
        public bool Rejected, AdvanceBeforeConfirm, FailReadAfterConfirm, FailRead, Unconfigured, ReturnOtherSession;
        public SimulationFulfillmentCommand? LastCommand;
        private SimulationFulfillmentPlanSnapshot? lastPreview;
        private readonly SimulationFulfillmentStateSnapshot state = new()
        {
            ScenarioStableId = "scenario:test", ConfigurationRevision = "r1",
            Centers = new[]
            {
                new SimulationFulfillmentCenter { CenterStableId = "center:a", DisplayName = "중랑 센터" },
                new SimulationFulfillmentCenter { CenterStableId = "center:b", DisplayName = "공릉 센터" },
            },
            Workers = new[]
            {
                new SimulationFulfillmentWorker { WorkerStableId = "worker:a", CenterStableId = "center:a", DisplayName = "다온", CapacityUnitCode = "piece" },
                new SimulationFulfillmentWorker { WorkerStableId = "worker:b", CenterStableId = "center:b", DisplayName = "나래", CapacityUnitCode = "piece" },
            },
        };

        public ValueTask<경영SimulationSessionSnapshot> Read(string id, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Reads++;
            if (FailRead) throw new IOException("read unavailable");
            return new(new 경영SimulationSessionSnapshot
            {
                SessionStableId = ReturnOtherSession ? "session:other" : id,
                Revision = Revision, Fulfillment = Unconfigured ? null : state,
            });
        }

        public ValueTask<SimulationFulfillmentStateSnapshot?> GetFulfillmentStateAsync(string id, CancellationToken token = default)
            => new(state);

        public ValueTask<SimulationFulfillmentPlanSnapshot> PreviewFulfillmentAsync(string id,
            SimulationFulfillmentOrderRequest request, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested(); Previews++;
            var centerB = request.AllowedCenterStableIds.Contains("center:b");
            lastPreview = new SimulationFulfillmentPlanSnapshot
            {
                OrderStableId = request.OrderStableId, Order = request, IsFeasible = !Rejected,
                ReasonCodes = Rejected ? new[] { "WorkerUnavailableOrCapacityExceeded" } : Array.Empty<string>(),
                CandidateCenters = state.Centers.Select(center => new SimulationFulfillmentCenterCandidate
                {
                    CenterStableId = center.CenterStableId, DisplayName = center.DisplayName,
                    IsEligible = !Rejected, CanFulfillWholeOrder = !Rejected,
                    ReasonCodes = Rejected ? new[] { "WorkerUnavailableOrCapacityExceeded" } : Array.Empty<string>(),
                }).ToArray(),
                SelectedCenterStableIds = new[] { centerB ? "center:b" : "center:a" },
                Allocations = Rejected ? Array.Empty<SimulationFulfillmentAllocation>() : new[]
                {
                    new SimulationFulfillmentAllocation
                    {
                        AllocationStableId = "allocation:one", LineStableId = "line:one",
                        CenterStableId = centerB ? "center:b" : "center:a", LocationBarcode = centerB ? "LOC-B" : "LOC-A",
                        ProductStableId = "product:one", ProductBarcode = "PRODUCT-ONE", UnitCode = "piece",
                        WorkerStableId = centerB ? "worker:b" : "worker:a", Quantity = request.Lines.Single().Quantity,
                    },
                },
            };
            return new(lastPreview);
        }

        public ValueTask<경영SimulationSessionSnapshot> ConfirmFulfillmentAsync(string id,
            SimulationFulfillmentCommand request, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested(); Attempts++; LastCommand = request;
            if (AdvanceBeforeConfirm) { AdvanceBeforeConfirm = false; Revision++; }
            if (request.ExpectedRevision != Revision) throw new InvalidOperationException("stale revision");
            if (request.ActionCode == SimulationFulfillmentActionCodes.Reserve)
            {
                lastPreview!.StateCode = SimulationFulfillmentStateCodes.Reserved;
                state.Orders = new[] { lastPreview! };
            }
            else if (request.ActionCode == SimulationFulfillmentActionCodes.Pick)
            {
                state.Orders.Single().StateCode = SimulationFulfillmentStateCodes.Picking;
                state.Orders.Single().Allocations.Single().PickedQuantity += request.Quantity;
            }
            else if (request.ActionCode == SimulationFulfillmentActionCodes.PackReady)
                state.Orders.Single().StateCode = SimulationFulfillmentStateCodes.PackReady;
            else if (request.ActionCode == SimulationFulfillmentActionCodes.Cancel)
                state.Orders.Single().StateCode = SimulationFulfillmentStateCodes.Cancelled;
            Revision++;
            if (FailReadAfterConfirm) FailRead = true;
            // 표시가 이 응답을 신뢰하면 Revision 검증에서 실패한다.
            return new(new 경영SimulationSessionSnapshot { SessionStableId = id, Revision = 999 });
        }
    }
}
