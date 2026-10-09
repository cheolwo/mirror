using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Pricing;
using Ssalddel.Domain.Pricing;
using 살뜰.Data;
using 살뜰.도메인.화주;

namespace Ssalddel.Application.Pricing;

public static class AgreedCargoAcceptancePolicy
{
    /// <summary>명시 선택한 약정의 기사만 수락합니다. 당시 동결 근거를 현재 요율로 바꾸지 않습니다.</summary>
    public static async Task<string?> CheckAsync(SsalddelContext db, 화주운송의뢰 work, string driverId, CancellationToken ct)
    {
        var selected = await db.Set<AgreedRateTableSelectionRecord>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.BusinessKind == AgreedRateBusinessKinds.CargoTransport && x.WorkId == work.의뢰Id, ct);
        if (selected is null) return null;
        if (selected.SynchronizationStatusCode != "Synchronized") return "RateTableLedgerSyncPending";
        var fare = await db.운임구성.AsNoTracking().SingleOrDefaultAsync(x => x.Id == work.운임구성Id && x.의뢰Id == work.의뢰Id, ct);
        if (fare is null || fare.합의요금표SnapshotJson != selected.SnapshotJson || fare.기사지급예정운임 != selected.AgreedAmountKrw)
            return "RateTableSelectionEvidenceInvalid";
        AgreedRateTableSnapshotDto? snapshot;
        try { snapshot = System.Text.Json.JsonSerializer.Deserialize<AgreedRateTableSnapshotDto>(selected.SnapshotJson,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)); }
        catch (System.Text.Json.JsonException) { return "RateTableSelectionEvidenceInvalid"; }
        if (!AgreedRateTableEvidencePolicy.IsValid(snapshot) || snapshot!.VehicleCode != work.차량종류
            || snapshot.AgreementId != selected.AgreementId || snapshot.Revision != selected.AgreementRevision)
            return "RateTableSelectionEvidenceInvalid";
        var agreement = await db.Set<AgreedRateTableRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.AgreementId == selected.AgreementId, ct);
        if (agreement is null || !AgreedRateTableEvidencePolicy.MatchesStoredAgreement(snapshot, agreement, selected.AgreementRevision)
            || !(snapshot.Proposer == new AgreedRatePartyDto(AgreedRatePartyKinds.Shipper, work.화주Id)
                || snapshot.Counterparty == new AgreedRatePartyDto(AgreedRatePartyKinds.Shipper, work.화주Id)))
            return "RateTableSelectionEvidenceInvalid";
        var driver = new AgreedRatePartyDto(AgreedRatePartyKinds.CargoDriver, driverId);
        return snapshot.Proposer == driver || snapshot.Counterparty == driver ? null : "RateTableSelectedDriverMismatch";
    }
}
