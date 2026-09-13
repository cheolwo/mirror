using System.Collections.Generic;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Application
{
    /// <summary>UI에는 계약만 전달한다. 운영 세션과 무관한 명시적 준비 모판 진입점.</summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E2,
        "운영과 독립된 방어 준비 Core 세션을 조립한다.",
        Boundary = "메모리 모판만 생성하며 Hosted 연결이나 운영 효과를 활성화하지 않는다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E2로컬권위Adapter)]
    public static class 역방어준비RuntimeFactory
    {
        public static I역방어준비Runtime Create(string sessionId,string mapRevision,string mapHash,IEnumerable<string> allowedSlots)
            =>new 역방어준비Session(sessionId,mapRevision,mapHash,allowedSlots);
    }
}
