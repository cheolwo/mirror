using System;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Application
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "구성 요소의 공통 Core·Application 또는 Adapter 실행 경계를 제공한다.",
        Boundary = "실행 경계는 실제 권위 위치와 E 단계 달성 증거를 분리한다.")]
    public sealed class 경영Simulation물류창고Service
    {
        private readonly 경영SimulationSessionAccessor sessions;

        public 경영Simulation물류창고Service(경영SimulationSessionAccessor sessions)
            => this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));

        public SimulationLogisticsMovementPreviewSnapshot PreviewLogisticsMovement(
            string sessionStableId,
            SimulationLogisticsMovementPreviewRequest request)
            => sessions.Require(sessionStableId).PreviewLogisticsMovement(request);

        public 경영SimulationSessionSnapshot ConfirmLogisticsMovement(
            string sessionStableId,
            SimulationLogisticsMovementConfirmRequest request)
            => sessions.Require(sessionStableId).ConfirmLogisticsMovement(request);

        public SimulationFreightDispatchPreviewSnapshot PreviewFreightDispatch(
            string sessionStableId,
            SimulationFreightDispatchPreviewRequest request)
            => sessions.Require(sessionStableId).PreviewFreightDispatch(request);

        public 경영SimulationSessionSnapshot ConfirmFreightDispatch(
            string sessionStableId,
            SimulationFreightDispatchConfirmRequest request)
            => sessions.Require(sessionStableId).ConfirmFreightDispatch(request);

        public SimulationFreightTransportPreviewSnapshot PreviewFreightTransport(
            string sessionStableId,
            SimulationFreightTransportPreviewRequest request)
            => sessions.Require(sessionStableId).PreviewFreightTransport(request);

        public 경영SimulationSessionSnapshot ConfirmFreightTransport(
            string sessionStableId,
            SimulationFreightTransportConfirmRequest request)
            => sessions.Require(sessionStableId).ConfirmFreightTransport(request);

        public SimulationDecisionPreviewSnapshot PreviewFreightReceipt(
            string sessionStableId,
            SimulationFreightReceiptPreviewRequest request)
            => sessions.Require(sessionStableId).PreviewFreightReceipt(request);

        public 경영SimulationSessionSnapshot ConfirmFreightReceipt(
            string sessionStableId,
            SimulationFreightReceiptConfirmRequest request)
            => sessions.Require(sessionStableId).ConfirmFreightReceipt(request);

        public SimulationDecisionPreviewSnapshot PreviewWarehousePutAway(
            string sessionStableId,
            SimulationWarehousePutAwayPreviewRequest request)
            => sessions.Require(sessionStableId).PreviewWarehousePutAway(request);

        public 경영SimulationSessionSnapshot ConfirmWarehousePutAway(
            string sessionStableId,
            SimulationWarehousePutAwayConfirmRequest request)
            => sessions.Require(sessionStableId).ConfirmWarehousePutAway(request);

        public SimulationDecisionPreviewSnapshot PreviewSupplyChainWork(
            string sessionStableId,
            SimulationSupplyChainWorkPreviewRequest request)
            => sessions.Require(sessionStableId).PreviewSupplyChainWork(request);

        public 경영SimulationSessionSnapshot ConfirmSupplyChainWork(
            string sessionStableId,
            SimulationSupplyChainWorkConfirmRequest request)
            => sessions.Require(sessionStableId).ConfirmSupplyChainWork(request);

        public SimulationNpcRoutineWorkProjection[] GetNpcRoutineWork(
            string sessionStableId, string areaCode)
            => sessions.Require(sessionStableId).GetNpcRoutineWork(areaCode);

        public SimulationSpatialCompositionStateSnapshot GetSpatialComposition(
            string sessionStableId, string areaCode)
            => sessions.Require(sessionStableId).GetSpatialComposition(areaCode);

        public SimulationFulfillmentStateSnapshot? GetFulfillmentState(string sessionStableId)
            => sessions.Require(sessionStableId).GetFulfillmentState();

        public SimulationFulfillmentPlanSnapshot PreviewFulfillment(
            string sessionStableId, SimulationFulfillmentOrderRequest request)
            => sessions.Require(sessionStableId).PreviewFulfillment(request);

        public 경영SimulationSessionSnapshot ConfirmFulfillment(
            string sessionStableId, SimulationFulfillmentCommand request)
            => sessions.Require(sessionStableId).ConfirmFulfillment(request);
    }
}
