using System;
using System.Linq;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.Contracts
{
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
        "Simulation 음식배달 상태를 공통 수명주기 상태 사본으로 변환한다.",
        Boundary = "계약 변환만 수행하며 운영 원장이나 Unity 표현을 변경하지 않는다.")]
    public static class Simulation음식배달수명주기Adapter
    {
        public static 음식배달수명주기Snapshot ToLifecycleSnapshot(
            Simulation음식배달Snapshot source,
            string sourceRevision)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(source.FoodOrderStableId))
                throw new ArgumentException("FoodDeliveryOrderIdentityMissing", nameof(source));
            return new 음식배달수명주기Snapshot
            {
                SourceCode = 음식배달상태원천코드.SimulationCore,
                SourceRevision = sourceRevision ?? string.Empty,
                OrderStableId = source.FoodOrderStableId,
                OrderRevision = source.Revision,
                OrderStateCode = source.StateCode,
                RestaurantStableId = source.RestaurantFacilityStableId,
                OrdererStableId = source.OrdererStableId,
                AcceptedAtUtc = null,
                SourceRefs = source.SourceStableIds?.ToArray() ?? Array.Empty<string>()
            };
        }
    }
}
