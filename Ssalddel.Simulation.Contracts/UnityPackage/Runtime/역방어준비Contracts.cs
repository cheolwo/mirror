using System;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Simulation.Contracts
{
    /// <summary>메모리 전용 방어 준비 계약. 좌표·운영 원장·실제 인물은 소유하지 않는다.</summary>
    [SsalddelEvidenceResponsibility(SsalddelEvidenceStage.E1,
        "독립 방어 준비 세션의 명령과 읽기 사본 계약을 정의한다.",
        Boundary = "운영 API·실제 인물·좌표·저장 권한을 소유하지 않는다.")]
    public interface I역방어준비Runtime
    {
        역방어준비Snapshot Read();
        역방어준비Result Preview(string sessionId, string slotId, long expectedRevision);
        역방어준비Result Assign(string sessionId, string commandId, string slotId, long expectedRevision);
    }

    public sealed class 역방어준비Snapshot
    {
        public string SessionId { get; }
        public string MapRevision { get; }
        public string MapHash { get; }
        public string UnitId { get; }
        public string AssignedSlotId { get; }
        public long Revision { get; }
        public int ActionCount { get; }
        public 역방어준비Snapshot(string sessionId, string mapRevision, string mapHash,
            string unitId, string assignedSlotId, long revision, int actionCount)
        {
            SessionId=sessionId; MapRevision=mapRevision; MapHash=mapHash; UnitId=unitId;
            AssignedSlotId=assignedSlotId; Revision=revision; ActionCount=actionCount;
        }
    }

    public sealed class 역방어준비Result
    {
        public bool Accepted { get; }
        public string Code { get; }
        public 역방어준비Snapshot Snapshot { get; }
        public 역방어준비Result(bool accepted,string code,역방어준비Snapshot snapshot)
        { Accepted=accepted; Code=code; Snapshot=snapshot; }
    }

    public sealed class 역방어배치ActionRecord
    {
        public string CommandId { get; }
        public string PreviousSlotId { get; }
        public string SlotId { get; }
        public long Revision { get; }
        public 역방어배치ActionRecord(string commandId,string previousSlotId,string slotId,long revision)
        { CommandId=commandId; PreviousSlotId=previousSlotId; SlotId=slotId; Revision=revision; }
    }
}
