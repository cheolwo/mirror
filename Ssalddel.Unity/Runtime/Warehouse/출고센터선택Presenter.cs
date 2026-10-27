using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Unity.Warehouse
{
    public sealed class 출고센터후보표시
    {
        public string 센터코드 { get; }
        public string 이름 { get; }
        public bool 선택가능 { get; }
        public string 요약 { get; }
        internal 출고센터후보표시(SimulationFulfillmentCenterCandidate value)
        {
            센터코드 = value.CenterStableId;
            이름 = string.IsNullOrWhiteSpace(value.DisplayName) ? value.CenterStableId : value.DisplayName;
            선택가능 = value.IsEligible;
            요약 = (value.IsEligible ? value.CanFulfillWholeOrder ? "한 센터에서 출고 가능" : "다른 센터와 분할 검토" : "선택 불가")
                + " · " + (value.EstimatedDistanceKm.HasValue
                    ? "추정 거리 " + value.EstimatedDistanceKm.Value.ToString(CultureInfo.InvariantCulture) + "km" : "거리 미확인")
                + " · 추정 비용 " + (value.EstimatedCost?.ToString(CultureInfo.InvariantCulture) ?? "미확인")
                + (value.ReasonCodes.Length == 0 ? "" : " · " + string.Join(", ", value.ReasonCodes.Select(출고사유표시.한국어)));
        }
    }

    public sealed class 출고피킹지시표시
    {
        public string 배분코드 { get; }
        public string 센터코드 { get; }
        public string 위치코드 { get; }
        public string 위치바코드 { get; }
        public string 상품바코드 { get; }
        public string 담당코드 { get; }
        public decimal 예정수량 { get; }
        public decimal 피킹수량 { get; }
        public decimal 남은수량 => 예정수량 - 피킹수량;
        public string 단위 { get; }
        public string 요약 { get; }
        internal 출고피킹지시표시(SimulationFulfillmentAllocation value, string center, string worker)
        {
            배분코드 = value.AllocationStableId; 센터코드 = value.CenterStableId;
            위치코드 = value.LocationStableId; 위치바코드 = value.LocationBarcode;
            상품바코드 = value.ProductBarcode; 담당코드 = value.WorkerStableId;
            예정수량 = value.Quantity; 피킹수량 = value.PickedQuantity; 단위 = value.UnitCode;
            요약 = center + " · 선반 " + 위치바코드 + " · 상품 " + value.ProductStableId
                + " · " + 예정수량.ToString(CultureInfo.InvariantCulture) + " " + 단위
                + " · 담당 " + worker + " · 피킹 " + 피킹수량.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal static class 출고사유표시
    {
        internal static string 한국어(string code)
        {
            var label = code switch
            {
                "OrderAlreadyExists" => "이미 확정된 주문",
                "CenterDisabled" => "센터 이용 중지",
                "CenterOutboundDisabled" => "센터 출고 중지",
                "CenterNotAllowed" => "현재 선택 범위 밖의 센터",
                "MatchingStockUnavailable" => "조건에 맞는 위치·상품 재고 없음",
                "WorkerUnavailableOrCapacityExceeded" => "담당 직원 부재 또는 처리 단위·여력 부족",
                "WholeOrderCapacityInsufficient" => "주문 전체를 처리할 재고·담당 여력 부족",
                "NoEligibleCenter" => "출고 가능한 센터 없음",
                "InsufficientEligibleStockOrWorkerCapacity" => "가능한 재고·담당 여력으로 수량을 충족하지 못함",
                "SplitCentersNotAllowed" => "여러 센터 분할 출고가 허용되지 않음",
                _ => "조건 확인 필요",
            };
            return label + " (" + code + ")";
        }
    }

    /// <summary>
    /// 같은 세션 사본의 Revision과 출고 상태를 읽는다. 미리보기·적용 응답을 완료 상태로 그리지 않는다.
    /// sessionReader는 기존 Local/Remote Sessions 조회를 주입하며 별도 원장을 만들지 않는다.
    /// </summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "출고 센터 후보·선반·담당 지시와 가상 예약·피킹·출고 준비의 권위 재조회를 연결한다.",
        Boundary = "UnityEngine 없는 표시·입력 계층이며 실제 스캔·Scene·운영 재고·기사 인계를 대신하지 않는다.")]
    public sealed class 출고센터선택Presenter
    {
        private readonly ISimulationFulfillmentRuntime runtime;
        private readonly Func<string, CancellationToken, ValueTask<경영SimulationSessionSnapshot>> sessionReader;
        private readonly string sessionId;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private 경영SimulationSessionSnapshot? snapshot;
        private SimulationFulfillmentOrderRequest? draft;
        private SimulationFulfillmentPlanSnapshot? preview;
        private long previewRevision = -1;
        private string currentOrderId = string.Empty;

        public string 마지막안내 { get; private set; } = "조회 전";
        public string 선택단계 { get; private set; } = "조회 필요";
        public bool 처리중 => gate.CurrentCount == 0;
        public bool 조회됨 => snapshot != null;
        public bool 구성됨 => snapshot?.Fulfillment != null;
        public long? 현재개정 => snapshot?.Revision;
        public bool 예약확정가능 => 구성됨 && preview != null && preview.IsFeasible
            && previewRevision == snapshot!.Revision;
        public 출고센터후보표시[] 센터후보 => preview == null ? Array.Empty<출고센터후보표시>()
            : preview.CandidateCenters.Select(value => new 출고센터후보표시(value)).ToArray();
        public string[] 보류사유코드 => preview == null ? Array.Empty<string>() : (string[])preview.ReasonCodes.Clone();
        public string[] 보류사유 => 보류사유코드.Select(출고사유표시.한국어).ToArray();
        public string 현재주문상태 => 현재주문()?.StateCode ?? string.Empty;
        public string 현재주문상태표시 => 현재주문상태 switch
        {
            SimulationFulfillmentStateCodes.Reserved => "예약 완료·피킹 대기",
            SimulationFulfillmentStateCodes.Picking => "피킹 진행 중",
            SimulationFulfillmentStateCodes.PackReady => "피킹 충족·포장 준비",
            SimulationFulfillmentStateCodes.Cancelled => "예약 취소",
            SimulationFulfillmentStateCodes.Rejected => "출고 불가",
            _ => "주문 선택 필요",
        };
        public string[] 확정주문목록 => snapshot?.Fulfillment?.Orders
            .Select(value => value.OrderStableId).ToArray() ?? Array.Empty<string>();
        public string[] 선택센터목록 => (현재주문()?.SelectedCenterStableIds ?? preview?.SelectedCenterStableIds
            ?? Array.Empty<string>()).Select(센터이름).ToArray();
        public 출고피킹지시표시[] 피킹지시 => (현재주문()?.Allocations ?? preview?.Allocations
            ?? Array.Empty<SimulationFulfillmentAllocation>()).Select(value => new 출고피킹지시표시(
                value, 센터이름(value.CenterStableId), 담당이름(value.WorkerStableId))).ToArray();
        public string[] 선반담당지시 => 피킹지시.Select(value => value.요약).ToArray();

        public 출고센터선택Presenter(ISimulationFulfillmentRuntime runtime,
            Func<string, CancellationToken, ValueTask<경영SimulationSessionSnapshot>> sessionReader,
            string sessionId)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.sessionReader = sessionReader ?? throw new ArgumentNullException(nameof(sessionReader));
            if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("세션 식별자가 필요합니다.", nameof(sessionId));
            this.sessionId = sessionId;
        }

        public async Task 조회Async(CancellationToken token = default)
        {
            await gate.WaitAsync(token);
            try { await 재조회Async(token); }
            finally { gate.Release(); }
        }

        public async Task 주문선택Async(SimulationFulfillmentOrderRequest request, CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            await gate.WaitAsync(token);
            try
            {
                현재구성확인();
                draft = 복사(request);
                currentOrderId = draft.OrderStableId;
                await 미리보기Async(token);
            }
            finally { gate.Release(); }
        }

        public async Task 확정주문선택Async(string orderId, CancellationToken token = default)
        {
            await gate.WaitAsync(token);
            try
            {
                현재구성확인();
                if (!snapshot!.Fulfillment!.Orders.Any(value => value.OrderStableId == orderId))
                    throw new InvalidOperationException("현재 회차에 확정된 출고 주문을 선택하세요.");
                currentOrderId = orderId; draft = null; preview = null; previewRevision = -1;
                선택단계 = "선반·담당 작업 확인";
                마지막안내 = "저장된 주문의 현재 피킹 지시를 확인합니다.";
            }
            finally { gate.Release(); }
        }

        public async Task 센터선택Async(string[] centerIds, CancellationToken token = default)
        {
            if (centerIds == null) throw new ArgumentNullException(nameof(centerIds));
            await gate.WaitAsync(token);
            try
            {
                현재구성확인();
                if (draft == null) throw new InvalidOperationException("주문을 먼저 선택하세요.");
                if (centerIds.Any(id => !snapshot!.Fulfillment!.Centers.Any(value => value.CenterStableId == id)))
                    throw new InvalidOperationException("현재 회차에 등록된 센터를 선택하세요.");
                draft.AllowedCenterStableIds = centerIds.Distinct(StringComparer.Ordinal).ToArray();
                await 미리보기Async(token);
            }
            finally { gate.Release(); }
        }

        public async Task 분할출고설정Async(bool allowed, CancellationToken token = default)
        {
            await gate.WaitAsync(token);
            try
            {
                현재구성확인();
                if (draft == null) throw new InvalidOperationException("주문을 먼저 선택하세요.");
                draft.AllowSplitCenters = allowed;
                await 미리보기Async(token);
            }
            finally { gate.Release(); }
        }

        public async Task 예약확정Async(string commandId, CancellationToken token = default)
        {
            await gate.WaitAsync(token);
            try
            {
                if (!예약확정가능 || draft == null)
                    throw new InvalidOperationException("최신 상태에서 가능한 출고안을 다시 확인하세요.");
                await 적용Async(new SimulationFulfillmentCommand
                {
                    CommandId = commandId, ExpectedRevision = previewRevision,
                    ActionCode = SimulationFulfillmentActionCodes.Reserve, Order = 복사(draft),
                }, token);
            }
            finally { gate.Release(); }
        }

        public Task 피킹확인Async(string commandId, string allocationId,
            string locationBarcode, string productBarcode, decimal quantity, CancellationToken token = default)
            => 단계적용Async(commandId, SimulationFulfillmentActionCodes.Pick,
                allocationId, locationBarcode, productBarcode, quantity, token);

        public Task 출고준비확인Async(string commandId, CancellationToken token = default)
            => 단계적용Async(commandId, SimulationFulfillmentActionCodes.PackReady, "", "", "", 0m, token);

        public Task 예약취소Async(string commandId, CancellationToken token = default)
            => 단계적용Async(commandId, SimulationFulfillmentActionCodes.Cancel, "", "", "", 0m, token);

        private async Task 단계적용Async(string commandId, string actionCode, string allocationId,
            string locationBarcode, string productBarcode, decimal quantity, CancellationToken token)
        {
            await gate.WaitAsync(token);
            try
            {
                현재구성확인();
                var order = 현재주문() ?? throw new InvalidOperationException("확정된 출고 주문을 먼저 선택하세요.");
                if (actionCode == SimulationFulfillmentActionCodes.Pick
                    && !order.Allocations.Any(value => value.AllocationStableId == allocationId))
                    throw new InvalidOperationException("현재 주문의 피킹 지시를 선택하세요.");
                await 적용Async(new SimulationFulfillmentCommand
                {
                    CommandId = commandId, ExpectedRevision = snapshot!.Revision, ActionCode = actionCode,
                    OrderStableId = order.OrderStableId, AllocationStableId = allocationId,
                    LocationBarcode = locationBarcode, ProductBarcode = productBarcode, Quantity = quantity,
                }, token);
            }
            finally { gate.Release(); }
        }

        private async Task 미리보기Async(CancellationToken token)
        {
            preview = null; previewRevision = -1; 선택단계 = "센터 검토 중";
            try
            {
                var value = await runtime.PreviewFulfillmentAsync(sessionId, 복사(draft!), token);
                if (value.OrderStableId != currentOrderId)
                    throw new InvalidOperationException("요청한 주문과 출고 후보가 일치하지 않습니다.");
                preview = 복사(value); previewRevision = snapshot!.Revision;
                선택단계 = value.IsFeasible ? "출고안 확인" : "센터 다시 선택";
                마지막안내 = value.IsFeasible ? "가능한 출고안입니다. 예약 확정 전에는 재고를 점유하지 않습니다."
                    : "출고할 수 없는 조건을 확인하고 센터나 분할 조건을 다시 선택하세요.";
            }
            catch
            {
                preview = null; previewRevision = -1; 선택단계 = "재조회 필요";
                마지막안내 = "출고 후보를 확인하지 못했습니다. 최신 조회 후 다시 선택하세요.";
                throw;
            }
        }

        private async Task 적용Async(SimulationFulfillmentCommand command, CancellationToken token)
        {
            preview = null; previewRevision = -1; 선택단계 = "적용 확인 중";
            try { await runtime.ConfirmFulfillmentAsync(sessionId, command, token); }
            catch
            {
                snapshot = null;
                try { await 재조회Async(token); } catch { snapshot = null; }
                선택단계 = "다시 확인";
                마지막안내 = "요청의 반영 여부를 확인하지 못했습니다. 자동 재전송하지 않았습니다. 최신 상태를 보고 다시 선택하세요.";
                throw;
            }
            await 재조회Async(token);
            마지막안내 = "요청 후 최신 상태를 조회했습니다. 출고 준비와 실제 기사 인계는 구분됩니다.";
        }

        private async Task 재조회Async(CancellationToken token)
        {
            snapshot = null; preview = null; previewRevision = -1; 선택단계 = "조회 중";
            try
            {
                var value = await sessionReader(sessionId, token);
                if (value == null || value.SessionStableId != sessionId)
                    throw new InvalidOperationException("조회한 세션이 현재 화면과 일치하지 않습니다.");
                snapshot = value;
                선택단계 = value.Fulfillment == null ? "출고 설정 없음"
                    : 현재주문() == null ? "주문·센터 선택" : "선반·담당 작업 확인";
                마지막안내 = value.Fulfillment == null ? "이 회차에는 출고 설정이 없습니다." : "최신 출고 상태";
            }
            catch
            {
                snapshot = null; 선택단계 = "재조회 필요";
                마지막안내 = "최신 상태를 조회하지 못했습니다. 이전 화면으로 성공을 표시하지 않습니다.";
                throw;
            }
        }

        private void 현재구성확인()
        {
            if (!구성됨) throw new InvalidOperationException("최신 출고 설정을 먼저 조회하세요.");
        }
        private SimulationFulfillmentPlanSnapshot? 현재주문()
            => snapshot?.Fulfillment?.Orders.SingleOrDefault(value => value.OrderStableId == currentOrderId);
        private string 센터이름(string id)
            => snapshot?.Fulfillment?.Centers.SingleOrDefault(value => value.CenterStableId == id)?.DisplayName ?? id;
        private string 담당이름(string id)
        {
            var worker = snapshot?.Fulfillment?.Workers.SingleOrDefault(value => value.WorkerStableId == id);
            return worker == null ? id : worker.DisplayName + " (처리 단위 " + worker.CapacityUnitCode + ")";
        }

        private static SimulationFulfillmentOrderRequest 복사(SimulationFulfillmentOrderRequest value)
            => new SimulationFulfillmentOrderRequest
            {
                OrderStableId = value.OrderStableId, AllowSplitCenters = value.AllowSplitCenters,
                AllowedCenterStableIds = (string[])value.AllowedCenterStableIds.Clone(),
                Lines = value.Lines.Select(line => new SimulationFulfillmentOrderLine
                {
                    LineStableId = line.LineStableId, ProductStableId = line.ProductStableId,
                    ProductBarcode = line.ProductBarcode, UnitCode = line.UnitCode, Quantity = line.Quantity,
                }).ToArray(),
                CenterEstimates = value.CenterEstimates.Select(estimate => new SimulationFulfillmentCenterEstimate
                {
                    CenterStableId = estimate.CenterStableId, EstimatedCost = estimate.EstimatedCost,
                    EstimatedDistanceKm = estimate.EstimatedDistanceKm,
                }).ToArray(),
            };

        private static SimulationFulfillmentPlanSnapshot 복사(SimulationFulfillmentPlanSnapshot value)
            => new SimulationFulfillmentPlanSnapshot
            {
                OrderStableId = value.OrderStableId, Order = 복사(value.Order), StateCode = value.StateCode,
                IsFeasible = value.IsFeasible, ReasonCodes = (string[])value.ReasonCodes.Clone(),
                SelectedCenterStableIds = (string[])value.SelectedCenterStableIds.Clone(),
                CandidateCenters = value.CandidateCenters.Select(candidate => new SimulationFulfillmentCenterCandidate
                {
                    CenterStableId = candidate.CenterStableId, DisplayName = candidate.DisplayName,
                    IsEligible = candidate.IsEligible, CanFulfillWholeOrder = candidate.CanFulfillWholeOrder,
                    ReasonCodes = (string[])candidate.ReasonCodes.Clone(), EstimatedCost = candidate.EstimatedCost,
                    EstimatedDistanceKm = candidate.EstimatedDistanceKm, PreferenceScore = candidate.PreferenceScore,
                }).ToArray(),
                Allocations = value.Allocations.Select(allocation => new SimulationFulfillmentAllocation
                {
                    AllocationStableId = allocation.AllocationStableId, LineStableId = allocation.LineStableId,
                    CenterStableId = allocation.CenterStableId, StockStableId = allocation.StockStableId,
                    LocationStableId = allocation.LocationStableId, LocationBarcode = allocation.LocationBarcode,
                    ProductStableId = allocation.ProductStableId, ProductBarcode = allocation.ProductBarcode,
                    UnitCode = allocation.UnitCode, LotStableId = allocation.LotStableId,
                    WorkerStableId = allocation.WorkerStableId, Quantity = allocation.Quantity,
                    PickedQuantity = allocation.PickedQuantity,
                }).ToArray(),
            };
    }
}
