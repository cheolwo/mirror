using System;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Application
{
    public interface I프랜차이즈공급망ProjectionWriter
    {
        bool PersistenceEnabled { get; }
        void ReplaceConfirmedState(string sessionStableId,
            프랜차이즈공급망StateSnapshot state);
    }

    public sealed class Disabled프랜차이즈공급망ProjectionWriter
        : I프랜차이즈공급망ProjectionWriter
    {
        public bool PersistenceEnabled => false;

        public void ReplaceConfirmedState(string sessionStableId,
            프랜차이즈공급망StateSnapshot state)
        {
            if (string.IsNullOrWhiteSpace(sessionStableId))
                throw new ArgumentException(nameof(sessionStableId));
            if (state == null) throw new ArgumentNullException(nameof(state));
            // DB가 비활성인 로컬 Simulation은 Aggregate를 권위로 유지한다.
            // 이 경계는 영속 Projection이 있는 것처럼 위장하지 않는 명시적 no-op이다.
        }
    }

    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "프랜차이즈 공급망의 조회·Preview·Confirm과 영속 Projection 인계를 조율한다.",
        Boundary = "Simulation Session 상태와 읽기 전용 Projection만 변경하며 실제 공급 계약·발주·배송을 실행하지 않는다.",
        SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E2세션실행)]
    public sealed class 프랜차이즈공급망SimulationService
    {
        private readonly 경영SimulationSessionAccessor sessions;
        private readonly I프랜차이즈공급망ProjectionWriter projectionWriter;

        public 프랜차이즈공급망SimulationService(
            경영SimulationSessionAccessor sessions,
            I프랜차이즈공급망ProjectionWriter projectionWriter)
        {
            this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            this.projectionWriter = projectionWriter
                ?? throw new ArgumentNullException(nameof(projectionWriter));
        }

        public 프랜차이즈공급망StateSnapshot? Get(string sessionStableId)
            => sessions.Require(sessionStableId).GetFranchiseSupplyState();

        public 프랜차이즈공급망PreviewSnapshot PreviewInitialization(
            string sessionStableId, 프랜차이즈공급망초기화PreviewRequest request)
            => sessions.Require(sessionStableId)
                .PreviewFranchiseSupplyInitialization(request);

        public 프랜차이즈공급망StateSnapshot ConfirmInitialization(
            string sessionStableId,
            프랜차이즈공급망CommandConfirmRequest<
                프랜차이즈공급망초기화PreviewRequest> request)
            => Confirm(sessionStableId, aggregate =>
                aggregate.ConfirmFranchiseSupplyInitialization(request));

        public 프랜차이즈공급망PreviewSnapshot PreviewSourcingAgreement(
            string sessionStableId, 프랜차이즈조달계약PreviewRequest request)
            => sessions.Require(sessionStableId)
                .PreviewFranchiseSourcingAgreement(request);

        public 프랜차이즈공급망StateSnapshot ConfirmSourcingAgreement(
            string sessionStableId,
            프랜차이즈공급망CommandConfirmRequest<
                프랜차이즈조달계약PreviewRequest> request)
            => Confirm(sessionStableId, aggregate =>
                aggregate.ConfirmFranchiseSourcingAgreement(request));

        public 프랜차이즈공급망PreviewSnapshot PreviewStoreSupplyOffer(
            string sessionStableId, 프랜차이즈매장공급안PreviewRequest request)
            => sessions.Require(sessionStableId)
                .PreviewFranchiseStoreSupplyOffer(request);

        public 프랜차이즈공급망StateSnapshot ConfirmStoreSupplyOffer(
            string sessionStableId,
            프랜차이즈공급망CommandConfirmRequest<
                프랜차이즈매장공급안PreviewRequest> request)
            => Confirm(sessionStableId, aggregate =>
                aggregate.ConfirmFranchiseStoreSupplyOffer(request));

        public 프랜차이즈공급망PreviewSnapshot PreviewStoreOrder(
            string sessionStableId, 프랜차이즈매장발주PreviewRequest request)
            => sessions.Require(sessionStableId)
                .PreviewFranchiseStoreOrder(request);

        public 프랜차이즈공급망StateSnapshot ConfirmStoreOrder(
            string sessionStableId,
            프랜차이즈공급망CommandConfirmRequest<
                프랜차이즈매장발주PreviewRequest> request)
            => Confirm(sessionStableId, aggregate =>
                aggregate.ConfirmFranchiseStoreOrder(request));

        private 프랜차이즈공급망StateSnapshot Confirm(
            string sessionStableId,
            Func<경영SimulationSessionAggregate,
                프랜차이즈공급망StateSnapshot> apply)
        {
            var state = apply(sessions.Require(sessionStableId));
            projectionWriter.ReplaceConfirmedState(sessionStableId, state);
            return state;
        }
    }
}
