using System;
using System.Collections.Generic;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Domain
{
    /// <summary>준비 모판의 단일 인원 배치 권위. 공격·경제·운영 상태와 분리한다.</summary>
    public sealed class 역방어준비Session : I역방어준비Runtime
    {
        private readonly object gate=new object();
        private readonly string sessionId, mapRevision, mapHash;
        private readonly HashSet<string> slots;
        private readonly Dictionary<string,(string Slot,long Expected)> receipts=new Dictionary<string,(string,long)>(StringComparer.Ordinal);
        private readonly List<역방어배치ActionRecord> actions=new List<역방어배치ActionRecord>();
        private string assignedSlotId=string.Empty;
        private long revision;
        public const string UnitId="synthetic:defense-unit:1";

        public 역방어준비Session(string sessionId,string mapRevision,string mapHash,IEnumerable<string> allowedSlots)
        {
            if(string.IsNullOrWhiteSpace(sessionId)||string.IsNullOrWhiteSpace(mapRevision)||!ValidHash(mapHash)||allowedSlots==null)
                throw new ArgumentException("DefenseFixtureInvalid");
            this.sessionId=sessionId;this.mapRevision=mapRevision;this.mapHash=mapHash;
            slots=new HashSet<string>(StringComparer.Ordinal);
            foreach(var slot in allowedSlots)
                if(string.IsNullOrWhiteSpace(slot)||!slots.Add(slot))throw new ArgumentException("DefenseSlotCatalogInvalid");
            if(slots.Count==0)throw new ArgumentException("DefenseSlotCatalogEmpty");
        }

        public 역방어준비Snapshot Read() {lock(gate)return Snapshot();}
        public 역방어배치ActionRecord[] ReadActions() {lock(gate)return actions.ToArray();}
        public 역방어준비Result Preview(string requestSessionId,string slotId,long expectedRevision)
        {lock(gate){var code=Validate(requestSessionId,slotId,expectedRevision);return Result(code=="Ready",code);}}

        public 역방어준비Result Assign(string requestSessionId,string commandId,string slotId,long expectedRevision)
        {
            lock(gate)
            {
                if(requestSessionId!=sessionId)return Result(false,"DefenseSessionMismatch");
                if(string.IsNullOrWhiteSpace(commandId)||commandId.Length>128)return Result(false,"DefenseCommandInvalid");
                if(receipts.TryGetValue(commandId,out var receipt))
                    return receipt.Slot==slotId&&receipt.Expected==expectedRevision
                        ?Result(true,"AlreadyApplied"):Result(false,"DefenseCommandConflict");
                var code=Validate(requestSessionId,slotId,expectedRevision);
                if(code!="Ready")return Result(false,code);
                if(assignedSlotId==slotId)return Result(false,"DefenseAlreadyAssigned");
                // 한 세션의 비영속 명령 기록을 무제한 보관하지 않는다. 기존 상태는 보존한다.
                if(actions.Count>=1024)return Result(false,"DefenseSessionCapacityReached");
                var previous=assignedSlotId;
                assignedSlotId=slotId;revision++;
                actions.Add(new 역방어배치ActionRecord(commandId,previous,slotId,revision));
                receipts.Add(commandId,(slotId,expectedRevision));
                return Result(true,"Assigned");
            }
        }
        private string Validate(string requestSessionId,string slotId,long expectedRevision)
        {
            if(requestSessionId!=sessionId)return "DefenseSessionMismatch";
            if(expectedRevision!=revision)return "DefenseRevisionConflict";
            if(string.IsNullOrWhiteSpace(slotId)||!slots.Contains(slotId))return "DefenseSlotUnknown";
            return "Ready";
        }
        private 역방어준비Snapshot Snapshot()=>new 역방어준비Snapshot(sessionId,mapRevision,mapHash,UnitId,assignedSlotId,revision,actions.Count);
        private 역방어준비Result Result(bool accepted,string code)=>new 역방어준비Result(accepted,code,Snapshot());
        private static bool ValidHash(string value)
        {
            if(value==null||value.Length!=64)return false;
            foreach(var c in value)if(!Uri.IsHexDigit(c))return false;
            return true;
        }
    }
}
