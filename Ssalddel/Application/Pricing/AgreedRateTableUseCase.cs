using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Finance;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Contracts.Common.Pricing;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Domain.Pricing;
using Ssalddel.Application.Shipper.Request;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.도메인.음식;
using Ssalddel.Services.Community;
using Ssalddel.Contracts.Common.Operations;

namespace Ssalddel.Application.Pricing;

public sealed class AgreedRateTableException(string code, int statusCode = 400) : Exception(code)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

public interface IAgreedRateTableUseCase
{
    Task<IReadOnlyList<AgreedRateTableDto>> ListAsync(AgreedRatePartyDto actor, CancellationToken ct);
    Task<AgreedRateTableDto> GetAsync(string id, AgreedRatePartyDto actor, CancellationToken ct);
    Task<AgreedRateTableDto> ProposeAsync(AgreedRatePartyDto actor, AgreedRateTableProposalRequest request, CancellationToken ct);
    Task<AgreedRateTableDto> ActAsync(string id, AgreedRatePartyDto actor, AgreedRateTableActionRequest request, CancellationToken ct);
    Task<AgreedRateTablePreviewDto> PreviewAsync(string id, AgreedRatePartyDto actor, AgreedRateTablePreviewRequest request, CancellationToken ct);
    Task<AgreedRateTableSelectionDto> SelectAsync(string id, AgreedRatePartyDto actor, AgreedRateTableSelectionRequest request, CancellationToken ct);
    Task<AgreedRateTableSelectionDto> GetSelectionAsync(string id, string workId, AgreedRatePartyDto actor, CancellationToken ct);
    Task<AgreedRateTableSnapshotDto?> SelectedAsync(string businessKind, string workId, string driverId, string vehicleCode, CancellationToken ct);
    Task<string?> SelectedDriverAsync(string businessKind, string workId, CancellationToken ct);
}

/// <summary>인증된 본인 범위는 Controller가 결속하고 당사자·판본·금액 상태는 서버가 검증합니다.</summary>
public sealed class AgreedRateTableUseCase(SsalddelContext db, TimeProvider clock, I운송원장Mongo동기화Service? ledgerSync = null) : IAgreedRateTableUseCase
{
    private sealed record Terms(FoodDeliveryPayoutPricingPolicy? FoodPricing, AgreedCargoPricingDto? CargoPricing);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<IReadOnlyList<AgreedRateTableDto>> ListAsync(AgreedRatePartyDto actor, CancellationToken ct)
    {
        ValidateActor(actor);
        var rows = await db.Set<AgreedRateTableRecord>().AsNoTracking().Where(x =>
            x.ProposerKind == actor.PartyKind && x.ProposerId == actor.PartyId
            || x.CounterpartyKind == actor.PartyKind && x.CounterpartyId == actor.PartyId)
            .OrderByDescending(x => x.AgreementId).Take(200).ToArrayAsync(ct);
        return rows.Where(x => PartyMatches(x, actor)).Select(x => ToDto(x, actor)).ToArray();
    }
    public async Task<AgreedRateTableDto> GetAsync(string id, AgreedRatePartyDto actor, CancellationToken ct)
        => ToDto(await OwnedAsync(id, actor, tracking: false, ct), actor);

    public async Task<AgreedRateTableDto> ProposeAsync(AgreedRatePartyDto actor, AgreedRateTableProposalRequest r, CancellationToken ct)
    {
        ValidateActor(actor); Require(actor.PartyKind == r.ActorPartyKind, "RateTableActorMismatch", 403);
        var digest = Digest(new { action = "Propose", request = r });
        if (await ReplayAsync<AgreedRateTableDto>(actor, r.RequestId, digest, ct) is { } replay) return replay;
        var other = new AgreedRatePartyDto(r.CounterpartyPartyKind, Clean(r.CounterpartyPartyId));
        ValidateActor(other); Require(actor != other, "RateTableSameParty");
        Require(ValidPair(r.BusinessKind, actor.PartyKind, other.PartyKind), "RateTableBusinessPartyMismatch");
        await RegisteredPartyAsync(actor, ct); await RegisteredPartyAsync(other, ct);
        ValidateUtcWindow(r.EffectiveFromUtc, r.EffectiveUntilUtc);
        Require(r.EffectiveUntilUtc > Now, "RateTableExpired");
        var terms = new Terms(r.FoodPricing, r.CargoPricing);
        ValidateTerms(r.BusinessKind, r.VehicleCode, terms, r.EffectiveFromUtc, r.EffectiveUntilUtc);
        var row = new AgreedRateTableRecord
        {
            AgreementId = Guid.NewGuid().ToString("N"), BusinessKind = r.BusinessKind, VehicleCode = r.VehicleCode.Trim(),
            ProposerKind = actor.PartyKind, ProposerId = actor.PartyId,
            CounterpartyKind = other.PartyKind, CounterpartyId = other.PartyId,
            TermsJson = Serialize(terms), TermsDigest = AgreedRateTableEvidencePolicy.TermsDigest(terms.FoodPricing, terms.CargoPricing),
            EffectiveFromUtc = r.EffectiveFromUtc, EffectiveUntilUtc = r.EffectiveUntilUtc,
            HistoryJson = Serialize(new[] { new AgreedRateTableEventDto("Propose", actor, 1, Now) })
        };
        db.Add(row);
        var dto = ToDto(row, actor); AddReceipt(actor, r.RequestId, digest, dto);
        await SaveAsync(ct); return dto;
    }
    public async Task<AgreedRateTableDto> ActAsync(string id, AgreedRatePartyDto actor, AgreedRateTableActionRequest r, CancellationToken ct)
    {
        ValidateActor(actor); Require(actor.PartyKind == r.ActorPartyKind, "RateTableActorMismatch", 403);
        var digest = Digest(new { action = "Act", agreementId = id, request = r });
        if (await ReplayAsync<AgreedRateTableDto>(actor, r.RequestId, digest, ct) is { } replay) return replay;
        var row = await OwnedAsync(id, actor, tracking: true, ct);
        Require(row.Revision == r.ExpectedRevision, "RateTableRevisionConflict", 409);
        if (r.ActionCode is "Accept" or "Reject")
        {
            Require(IsCounterparty(row, actor), "RateTableCounterpartyRequired", 403);
            Require(row.StatusCode == "Proposed", "RateTableStateConflict", 409);
            Require(Now < row.EffectiveUntilUtc, "RateTableExpired");
            row.StatusCode = r.ActionCode == "Accept" ? "Accepted" : "Rejected";
            if (r.ActionCode == "Accept") row.AcceptedAtUtc = Now;
        }
        else if (r.ActionCode == "Withdraw")
        {
            Require(row.StatusCode is "Proposed" or "Accepted", "RateTableStateConflict", 409);
            row.StatusCode = "Withdrawn";
        }
        else throw new AgreedRateTableException("RateTableActionInvalid");
        row.Revision++;
        var history = Deserialize<List<AgreedRateTableEventDto>>(row.HistoryJson);
        history.Add(new(r.ActionCode, actor, row.Revision, Now)); row.HistoryJson = Serialize(history);
        var dto = ToDto(row, actor); AddReceipt(actor, r.RequestId, digest, dto);
        await SaveAsync(ct); return dto;
    }
    public async Task<AgreedRateTablePreviewDto> PreviewAsync(string id, AgreedRatePartyDto actor, AgreedRateTablePreviewRequest r, CancellationToken ct)
    {
        Require(actor.PartyKind == r.ActorPartyKind, "RateTableActorMismatch", 403);
        var row = await OwnedAsync(id, actor, tracking: false, ct); EnsureUsable(row, r.ExpectedRevision);
        return Calculate(Snapshot(row), r, Now);
    }
    public async Task<AgreedRateTableSelectionDto> SelectAsync(string id, AgreedRatePartyDto actor, AgreedRateTableSelectionRequest r, CancellationToken ct)
    {
        ValidateActor(actor);
        Require(actor.PartyKind == r.ActorPartyKind, "RateTableActorMismatch", 403);
        var workId = Clean(r.WorkId);
        Require(workId.Length is > 0 and <= 191 && workId == r.WorkId, "RateTableWorkRequired");
        var digest = Digest(new { action = "Select", agreementId = id, request = r });
        if (await ReplayAsync<AgreedRateTableSelectionDto>(actor, r.RequestId, digest, ct) is { } replay)
        {
            await SynchronizeSelectedAsync(replay.BusinessKind, replay.WorkId, actor, ct);
            return await GetSelectionAsync(id, replay.WorkId, actor, ct);
        }
        // DB 재시도 전략과 같은 transaction 안에서 합의와 업무 상태를 검사합니다.
        var attempt = 0;
        var persisted = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0)
            {
                db.ChangeTracker.Clear();
                if (await ReplayAsync<AgreedRateTableSelectionDto>(actor, r.RequestId, digest, ct) is { } saved) return saved;
            }
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var row = await OwnedAsync(id, actor, tracking: false, ct); EnsureUsable(row, r.ExpectedRevision);
            await FenceWorkAsync(row, actor, workId, ct);
            var existing = await db.Set<AgreedRateTableSelectionRecord>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.BusinessKind == row.BusinessKind && x.WorkId == workId, ct);
            Require(existing is null, "RateTableWorkAlreadySelected", 409);
            var snapshot = Snapshot(row);
            var selection = new AgreedRateTableSelectionRecord
            {
                BusinessKind = row.BusinessKind, WorkId = workId, AgreementId = row.AgreementId,
                AgreementRevision = row.Revision, SnapshotJson = Serialize(snapshot),
                SelectionRequestId = r.RequestId,
                SelectingPartyKind = actor.PartyKind, SelectingPartyId = actor.PartyId, SelectedAtUtc = Now
            };
            if (row.BusinessKind == AgreedRateBusinessKinds.CargoTransport)
                await ApplyCargoPayoutAsync(selection, snapshot, ct);
            db.Add(selection);
            var dto = SelectionDto(selection);
            AddReceipt(actor, r.RequestId, digest, dto); await SaveAsync(ct); await transaction.CommitAsync(ct);
            return dto;
        });
        await SynchronizeSelectedAsync(persisted.BusinessKind, workId, actor, ct);
        return await GetSelectionAsync(id, workId, actor, ct);
    }
    public async Task<AgreedRateTableSelectionDto> GetSelectionAsync(string id, string workId, AgreedRatePartyDto actor, CancellationToken ct)
    {
        var agreement = await OwnedAsync(id, actor, tracking: false, ct);
        var selected = await db.Set<AgreedRateTableSelectionRecord>().AsNoTracking().SingleOrDefaultAsync(x =>
            x.BusinessKind == agreement.BusinessKind && x.WorkId == workId && x.AgreementId == id, ct)
            ?? throw new AgreedRateTableException("RateTableSelectionNotFound", 404);
        Require(AgreedRateTableEvidencePolicy.MatchesStoredAgreement(Deserialize<AgreedRateTableSnapshotDto>(selected.SnapshotJson),
            agreement, selected.AgreementRevision), "RateTableSelectionEvidenceInvalid");
        // 과거 업무의 선택 근거는 철회 뒤에도 열람합니다. 새 견적 적용에는 SelectedAsync가 현재 유효성을 검사합니다.
        return SelectionDto(selected) with
        {
            CanRetrySynchronization = selected.BusinessKind == AgreedRateBusinessKinds.CargoTransport
                && selected.SynchronizationStatusCode != "Synchronized"
                && actor == new AgreedRatePartyDto(selected.SelectingPartyKind, selected.SelectingPartyId)
        };
    }
    public async Task<AgreedRateTableSnapshotDto?> SelectedAsync(string businessKind, string workId, string driverId, string vehicleCode, CancellationToken ct)
    {
        var selection = await db.Set<AgreedRateTableSelectionRecord>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.BusinessKind == businessKind && x.WorkId == workId, ct);
        if (selection is null) return null;
        var row = await db.Set<AgreedRateTableRecord>().AsNoTracking().SingleAsync(x => x.AgreementId == selection.AgreementId, ct);
        EnsureUsable(row, selection.AgreementRevision);
        var driverKind = businessKind == AgreedRateBusinessKinds.FoodDelivery ? AgreedRatePartyKinds.FoodDriver : AgreedRatePartyKinds.CargoDriver;
        Require(PartyMatches(row, new(driverKind, driverId)), "RateTableSelectedDriverMismatch");
        Require(string.Equals(row.VehicleCode, vehicleCode, StringComparison.Ordinal), "RateTableVehicleMismatch");
        var frozen = Deserialize<AgreedRateTableSnapshotDto>(selection.SnapshotJson);
        Require(AgreedRateTableEvidencePolicy.MatchesStoredAgreement(frozen, row, selection.AgreementRevision),
            "RateTableSelectionEvidenceInvalid");
        return frozen;
    }

    public async Task<string?> SelectedDriverAsync(string businessKind, string workId, CancellationToken ct)
    {
        var selected = await db.Set<AgreedRateTableSelectionRecord>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.BusinessKind == businessKind && x.WorkId == workId, ct);
        if (selected is null) return null;
        var row = await db.Set<AgreedRateTableRecord>().AsNoTracking().SingleAsync(x => x.AgreementId == selected.AgreementId, ct);
        EnsureUsable(row, selected.AgreementRevision);
        var frozen = Deserialize<AgreedRateTableSnapshotDto>(selected.SnapshotJson);
        Require(AgreedRateTableEvidencePolicy.MatchesStoredAgreement(frozen, row, selected.AgreementRevision), "RateTableSelectionEvidenceInvalid");
        var kind = businessKind == AgreedRateBusinessKinds.FoodDelivery ? AgreedRatePartyKinds.FoodDriver : AgreedRatePartyKinds.CargoDriver;
        return frozen.Proposer.PartyKind == kind ? frozen.Proposer.PartyId : frozen.Counterparty.PartyId;
    }

    public static AgreedRateTablePreviewDto Calculate(AgreedRateTableSnapshotDto snapshot, AgreedRateTablePreviewRequest r, DateTime atUtc)
    {
        Require(r.DistanceKm is >= 0 and <= 1000, "RateTableDistanceRequired");
        Require(r.DistanceBasisCode is "AppDisplayed" or "RouteEstimate" or "ConfirmedRoute", "RateTableDistanceBasisRequired");
        FoodDeliveryPayoutReviewLine? food = null; decimal total;
        if (snapshot.BusinessKind == AgreedRateBusinessKinds.FoodDelivery)
        {
            Require(r.WaitingFeeKrw == 0 && r.ManualWorkFeeKrw == 0 && r.SurchargeKrw == 0, "RateTableFoodCargoTermsMismatch");
            food = 음식배달지급검토Policy.건별계산(new(snapshot.FoodPricing!, [new("preview", DateOnly.FromDateTime(atUtc),
                r.DistanceKm, r.DistanceBasisCode, "None", 0m, 0m, 0m)])).Lines[0]; total = food.GrossPayoutKrw;
        }
        else
        {
            Require(r.CostSharingPolicy is null && r.MaxUserChargeKrw is null, "RateTableCargoFoodTermsMismatch");
            Money(r.WaitingFeeKrw); Money(r.ManualWorkFeeKrw); Money(r.SurchargeKrw);
            var terms = snapshot.CargoPricing!;
            total = 화주운송기준운임계산기.Calculate(new() { 차량종류 = snapshot.VehicleCode, 대기료 = r.WaitingFeeKrw,
                수작업비 = r.ManualWorkFeeKrw, 할증 = r.SurchargeKrw },
                new(snapshot.VehicleCode, terms.BaseFareKrw, terms.PerKmKrw, terms.MinimumFareKrw, $"AgreedRateTable:{snapshot.AgreementId}:{snapshot.Revision}"),
                r.DistanceKm, r.DistanceBasisCode != "ConfirmedRoute", r.DistanceBasisCode).최종운임;
        }
        decimal? restaurant = null, user = null;
        if (r.CostSharingPolicy is { } sharing)
        {
            var costs = DeliveryCostSharingCalculator.Calculate(total, sharing);
            Require(costs.IsAllowed, costs.ReasonCode ?? "RateTableCostSharingInvalid");
            restaurant = costs.RestaurantChargeKrw; user = costs.CustomerChargeKrw;
            if (r.MaxUserChargeKrw is { } maximum)
            { Money(maximum); Require(user <= maximum, "RateTableUserChargeLimitExceeded"); }
        }
        else Require(r.MaxUserChargeKrw is null, "RateTableCostSharingRequired");
        return new(snapshot, total, food, restaurant, user);
    }

    private async Task ApplyCargoPayoutAsync(AgreedRateTableSelectionRecord selection, AgreedRateTableSnapshotDto snapshot, CancellationToken ct)
    {
        var work = await db.화주운송의뢰.AsNoTracking().SingleAsync(x => x.의뢰Id == selection.WorkId, ct);
        var fare = await db.운임구성.SingleOrDefaultAsync(x => x.Id == work.운임구성Id && x.의뢰Id == work.의뢰Id, ct);
        Require(fare is not null && fare.예상거리Km is > 0 && !string.IsNullOrWhiteSpace(fare.거리계산방식), "RateTableStoredDistanceRequired");
        var basis = "RouteEstimate";
        var storedFare = fare!;
        var result = Calculate(snapshot, new() { DistanceKm = storedFare.예상거리Km!.Value, DistanceBasisCode = basis,
            WaitingFeeKrw = storedFare.대기료, ManualWorkFeeKrw = storedFare.수작업비, SurchargeKrw = storedFare.할증 }, Now);
        var review = new 화주운송요금정책검토Service().검토(new() { 기본운임 = storedFare.기본운임,
            거리운임 = storedFare.거리운임, 최소운임 = storedFare.최소운임, 대기료 = storedFare.대기료, 수작업비 = storedFare.수작업비,
            할증 = storedFare.할증, 최종운임 = storedFare.최종운임, 기사지급예정운임 = result.TotalAmountKrw }, work.결제예정금액);
        Require(!review.정책위반, "RateTableCargoPricingPolicyConflict");
        // 기준견적·화주 결제예정액·수금·지급 상태는 그대로이고 사전 합의 수행 대가만 저장합니다.
        storedFare.기사지급예정운임 = result.TotalAmountKrw; storedFare.UpdatedAt = Now;
        storedFare.합의요금표SnapshotJson = Serialize(snapshot);
        selection.AgreedAmountKrw = result.TotalAmountKrw; selection.DistanceKm = storedFare.예상거리Km;
        selection.DistanceBasisCode = basis; selection.SynchronizationStatusCode = "Pending";
    }
    private async Task SynchronizeSelectedAsync(string business, string workId, AgreedRatePartyDto actor, CancellationToken ct)
    {
        if (business != AgreedRateBusinessKinds.CargoTransport || ledgerSync is null) return;
        var selection = await db.Set<AgreedRateTableSelectionRecord>().SingleAsync(x => x.BusinessKind == business && x.WorkId == workId, ct);
        if (selection.SynchronizationStatusCode == "Synchronized") return;
        var work = await db.화주운송의뢰.AsNoTracking().SingleAsync(x => x.의뢰Id == workId, ct);
        var agreement = await db.Set<AgreedRateTableRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.AgreementId == selection.AgreementId, ct);
        var fare = await db.운임구성.AsNoTracking().SingleOrDefaultAsync(x => x.Id == work.운임구성Id && x.의뢰Id == workId, ct);
        Require(agreement is not null && AgreedRateTableEvidencePolicy.MatchesStoredAgreement(
            Deserialize<AgreedRateTableSnapshotDto>(selection.SnapshotJson), agreement, selection.AgreementRevision)
            && fare is not null && fare.합의요금표SnapshotJson == selection.SnapshotJson && fare.기사지급예정운임 == selection.AgreedAmountKrw,
            "RateTableSelectionEvidenceInvalid");
        // 동기화 실패는 DB 성공을 숨기지 않습니다. 같은 명시 요청으로 원 선택을 재확인·재시도합니다.
        try
        {
            if (await ledgerSync.화주운송의뢰동기화Async(work, actor.PartyId, ct) is null) return;
            selection.SynchronizationStatusCode = "Synchronized"; await SaveAsync(ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
        catch (Microsoft.Extensions.Options.OptionsValidationException) { }
    }
    private static AgreedRateTableSelectionDto SelectionDto(AgreedRateTableSelectionRecord selection)
        => new(selection.BusinessKind, selection.WorkId, Deserialize<AgreedRateTableSnapshotDto>(selection.SnapshotJson))
        {
            SelectionRequestId = selection.SelectionRequestId,
            AgreedAmountKrw = selection.AgreedAmountKrw, DistanceKm = selection.DistanceKm,
            DistanceBasisCode = selection.DistanceBasisCode,
            StatusCode = selection.BusinessKind == AgreedRateBusinessKinds.FoodDelivery ? "SelectedForNextOffer"
                : selection.SynchronizationStatusCode == "Synchronized" ? "Applied" : "LedgerSyncPending"
        };

    private async Task FenceWorkAsync(AgreedRateTableRecord agreement, AgreedRatePartyDto actor, string workId, CancellationToken ct)
    {
        if (agreement.BusinessKind == AgreedRateBusinessKinds.FoodDelivery)
        {
            var owner = agreement.ProposerKind == AgreedRatePartyKinds.Restaurant ? agreement.ProposerId : agreement.CounterpartyId;
            var restaurantId = long.Parse(owner, System.Globalization.CultureInfo.InvariantCulture);
            Require(actor == new AgreedRatePartyDto(AgreedRatePartyKinds.Restaurant, owner), "RateTableWorkOwnerRequired", 403);
            var order = await db.음식주문.AsNoTracking().SingleOrDefaultAsync(x => x.주문번호 == workId && x.음식점Id == restaurantId, ct);
            Require(order is not null && order.주문번호 == workId, "RateTableWorkNotFound", 404);
            Require((order!.상태 is "주문대기" or "주문접수" or "주문확인" or "조리중" or "픽업대기") && !order.주문자본인수령여부,
                "RateTableWorkAlreadyFrozen", 409);
            var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.원본의뢰Id == workId
                && x.배차업무유형 == 상태값.배차업무유형.음식배달, ct);
            Require(queue is not null, "RateTableWorkNotFound", 404);
            Require(queue!.상태 == 상태값.배차대기상태.대기 && queue.배차큐단계 == 상태값.배차큐단계.계획배차 && queue.현재추천대상기사Id is null && queue.확정기사Id is null
                && string.IsNullOrEmpty(queue.기사제안요금계산근거Json), "RateTableWorkAlreadyFrozen", 409);
            var updated = await db.운송원장.Where(x => x.Id == queue.Id && x.UpdatedAt == queue.UpdatedAt
                && x.상태 == 상태값.배차대기상태.대기 && x.배차큐단계 == 상태값.배차큐단계.계획배차 && x.현재추천대상기사Id == null && x.확정기사Id == null
                && (x.기사제안요금계산근거Json == null || x.기사제안요금계산근거Json == ""))
                .ExecuteUpdateAsync(b => b.SetProperty(x => x.UpdatedAt, Now), ct);
            Require(updated == 1, "RateTableWorkRevisionConflict", 409);
        }
        else
        {
            var owner = agreement.ProposerKind == AgreedRatePartyKinds.Shipper ? agreement.ProposerId : agreement.CounterpartyId;
            Require(actor == new AgreedRatePartyDto(AgreedRatePartyKinds.Shipper, owner), "RateTableWorkOwnerRequired", 403);
            // 기존 수락/견적 수정과 같은 운송원장 → 의뢰 잠금 순서를 유지합니다.
            var transport = await db.운송원장.AsNoTracking().FirstOrDefaultAsync(x => x.의뢰Id == workId || x.운송번호 == workId, ct);
            var work = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == workId && x.화주Id == owner, ct);
            Require(work is not null && work.의뢰Id == workId && work.화주Id == owner, "RateTableWorkNotFound", 404);
            Require(work!.상태 == 상태값.의뢰상태.생성됨 && work.배차상태 == 상태값.배차상태.미시작
                && work.결제상태 == 상태값.결제상태.결제대기
                && (work.정산상태 is "결제대기" or "청구대기" or "후불승인대기" or "인수증대기")
                && !work.모의처리처수행
                && (transport is null || transport.상태 == "대기" && string.IsNullOrWhiteSpace(transport.확정기사Id)
                    && string.IsNullOrWhiteSpace(transport.기사_운송자) && string.IsNullOrWhiteSpace(transport.현재추천대상기사Id)),
                "RateTableWorkAlreadyFrozen", 409);
            Require(!await db.비정상운송사건.AsNoTracking().AnyAsync(x => x.운송의뢰Id == workId
                && (x.정산보류적용여부 || x.상태Code == 비정상운송사건상태Codes.운영검토대기), ct), "RateTableWorkHeld", 409);
            Require(string.Equals(work.차량종류, agreement.VehicleCode, StringComparison.Ordinal), "RateTableVehicleMismatch");
            var updated = await db.화주운송의뢰.Where(x => x.Id == work.Id && x.UpdatedAt == work.UpdatedAt
                && x.상태 == 상태값.의뢰상태.생성됨 && x.배차상태 == 상태값.배차상태.미시작 && x.결제상태 == 상태값.결제상태.결제대기
                && (x.정산상태 == "결제대기" || x.정산상태 == "청구대기" || x.정산상태 == "후불승인대기" || x.정산상태 == "인수증대기"))
                .ExecuteUpdateAsync(b => b.SetProperty(x => x.UpdatedAt, Now), ct);
            Require(updated == 1, "RateTableWorkRevisionConflict", 409);
        }
    }
    private async Task<AgreedRateTableRecord> OwnedAsync(string id, AgreedRatePartyDto actor, bool tracking, CancellationToken ct)
    {
        ValidateActor(actor); var query = db.Set<AgreedRateTableRecord>().AsQueryable(); if (!tracking) query = query.AsNoTracking();
        var row = await query.SingleOrDefaultAsync(x => x.AgreementId == id
            && (x.ProposerKind == actor.PartyKind && x.ProposerId == actor.PartyId
                || x.CounterpartyKind == actor.PartyKind && x.CounterpartyId == actor.PartyId), ct);
        return row is not null && row.AgreementId == id && PartyMatches(row, actor)
            ? row : throw new AgreedRateTableException("RateTableNotFound", 404);
    }
    private async Task RegisteredPartyAsync(AgreedRatePartyDto party, CancellationToken ct)
    {
        var exists = party.PartyKind switch
        {
            AgreedRatePartyKinds.Restaurant => long.TryParse(party.PartyId, out var id)
                && party.PartyId == id.ToString(System.Globalization.CultureInfo.InvariantCulture)
                && await db.음식점공개프로필.AnyAsync(x => x.Id == id, ct),
            AgreedRatePartyKinds.FoodDriver => await db.배달기사.Where(x => x.기사Id == party.PartyId).Select(x => x.기사Id).FirstOrDefaultAsync(ct) == party.PartyId,
            AgreedRatePartyKinds.CargoDriver => await db.용달기사.Where(x => x.기사Id == party.PartyId).Select(x => x.기사Id).FirstOrDefaultAsync(ct) == party.PartyId,
            AgreedRatePartyKinds.Shipper => await db.Users.Where(x => x.Id == party.PartyId).Select(x => x.Id).FirstOrDefaultAsync(ct) == party.PartyId
                || await db.화주운송의뢰.Where(x => x.화주Id == party.PartyId).Select(x => x.화주Id).FirstOrDefaultAsync(ct) == party.PartyId,
            _ => false
        };
        Require(exists, "RateTablePartyUnavailable");
    }
    private void EnsureUsable(AgreedRateTableRecord row, long revision)
    {
        Require(row.Revision == revision, "RateTableRevisionConflict", 409);
        Require(row.StatusCode == "Accepted" && row.AcceptedAtUtc.HasValue, "RateTableNotAccepted");
        Require(Now >= row.EffectiveFromUtc, "RateTableNotYetEffective"); Require(Now < row.EffectiveUntilUtc, "RateTableExpired");
    }
    private AgreedRateTableDto ToDto(AgreedRateTableRecord row, AgreedRatePartyDto actor)
    {
        var terms = Deserialize<Terms>(row.TermsJson);
        var active = row.StatusCode == "Accepted" && Now >= row.EffectiveFromUtc && Now < row.EffectiveUntilUtc;
        return new()
        {
            AgreementId = row.AgreementId, BusinessKind = row.BusinessKind, VehicleCode = row.VehicleCode,
            Proposer = new(row.ProposerKind, row.ProposerId), Counterparty = new(row.CounterpartyKind, row.CounterpartyId), CurrentParty = actor,
            FoodPricing = terms.FoodPricing, CargoPricing = terms.CargoPricing,
            EffectiveFromUtc = row.EffectiveFromUtc, EffectiveUntilUtc = row.EffectiveUntilUtc, AcceptedAtUtc = row.AcceptedAtUtc,
            Revision = row.Revision, StatusCode = row.StatusCode,
            EffectiveStatusCode = row.StatusCode != "Accepted" ? row.StatusCode == "Proposed" ? "NotAccepted" : row.StatusCode
                : Now < row.EffectiveFromUtc ? "NotYetEffective" : Now >= row.EffectiveUntilUtc ? "Expired" : "Active",
            CanAccept = IsCounterparty(row, actor) && row.StatusCode == "Proposed" && Now < row.EffectiveUntilUtc,
            CanReject = IsCounterparty(row, actor) && row.StatusCode == "Proposed" && Now < row.EffectiveUntilUtc,
            CanWithdraw = row.StatusCode is "Proposed" or "Accepted", CanSelect = active && actor.PartyKind is AgreedRatePartyKinds.Restaurant or AgreedRatePartyKinds.Shipper,
            CanPreview = active,
            History = Deserialize<List<AgreedRateTableEventDto>>(row.HistoryJson)
        };
    }
    private static AgreedRateTableSnapshotDto Snapshot(AgreedRateTableRecord row)
    {
        var terms = Deserialize<Terms>(row.TermsJson); Require(row.AcceptedAtUtc.HasValue, "RateTableNotAccepted");
        return new(row.AgreementId, row.Revision, row.BusinessKind, row.VehicleCode,
            new(row.ProposerKind, row.ProposerId), new(row.CounterpartyKind, row.CounterpartyId),
            terms.FoodPricing, terms.CargoPricing, row.EffectiveFromUtc, row.EffectiveUntilUtc, row.AcceptedAtUtc!.Value, row.TermsDigest);
    }
    private async Task<T?> ReplayAsync<T>(AgreedRatePartyDto actor, Guid requestId, string digest, CancellationToken ct) where T : class
    {
        Require(requestId != Guid.Empty, "RateTableRequestIdRequired");
        var receipt = await db.Set<AgreedRateTableCommandReceipt>().AsNoTracking().SingleOrDefaultAsync(x =>
            x.PartyKind == actor.PartyKind && x.PartyId == actor.PartyId && x.RequestId == requestId, ct);
        if (receipt is null) return null;
        Require(receipt.PartyKind == actor.PartyKind && receipt.PartyId == actor.PartyId, "RateTableRequestContentConflict", 409);
        Require(receipt.InputDigest == digest, "RateTableRequestContentConflict", 409); return Deserialize<T>(receipt.ResultJson);
    }
    private void AddReceipt<T>(AgreedRatePartyDto actor, Guid requestId, string digest, T value)
        => db.Add(new AgreedRateTableCommandReceipt { PartyKind = actor.PartyKind, PartyId = actor.PartyId,
            RequestId = requestId, InputDigest = digest, ResultJson = Serialize(value), RecordedAtUtc = Now });
    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); throw new AgreedRateTableException("RateTableRevisionConflict", 409); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); throw new AgreedRateTableException("RateTableWriteConflict", 409); }
    }
    private static void ValidateTerms(string business, string vehicle, Terms terms, DateTime from, DateTime until)
    {
        Require(!string.IsNullOrWhiteSpace(vehicle) && vehicle.Length <= 80 && vehicle == vehicle.Trim(), "RateTableVehicleRequired");
        if (business == AgreedRateBusinessKinds.FoodDelivery)
        {
            Require(vehicle is "Car" or "Motorcycle" && terms.FoodPricing is not null && terms.CargoPricing is null, "RateTableFoodTermsRequired");
            var policy = terms.FoodPricing!;
            Require(policy.EffectiveFrom <= DateOnly.FromDateTime(from) && policy.EffectiveThrough >= DateOnly.FromDateTime(until.AddTicks(-1)), "RateTablePolicyDatesMismatch");
            음식배달지급검토Policy.건별계산(new(policy, [new("validation", policy.EffectiveFrom, 0m, "RouteEstimate", "None", 0m, 0m, 0m)]));
        }
        else
        {
            Require(business == AgreedRateBusinessKinds.CargoTransport && terms.CargoPricing is not null && terms.FoodPricing is null, "RateTableCargoTermsRequired");
            Money(terms.CargoPricing!.BaseFareKrw); Money(terms.CargoPricing.PerKmKrw); Money(terms.CargoPricing.MinimumFareKrw);
        }
    }
    private static void ValidateUtcWindow(DateTime from, DateTime until)
        => Require(from.Kind == DateTimeKind.Utc && until.Kind == DateTimeKind.Utc && from < until, "RateTableUtcWindowRequired");
    private static bool ValidPair(string business, string one, string two)
        => business == AgreedRateBusinessKinds.FoodDelivery && (one, two) is (AgreedRatePartyKinds.Restaurant, AgreedRatePartyKinds.FoodDriver) or (AgreedRatePartyKinds.FoodDriver, AgreedRatePartyKinds.Restaurant)
           || business == AgreedRateBusinessKinds.CargoTransport && (one, two) is (AgreedRatePartyKinds.Shipper, AgreedRatePartyKinds.CargoDriver) or (AgreedRatePartyKinds.CargoDriver, AgreedRatePartyKinds.Shipper);
    private static bool PartyMatches(AgreedRateTableRecord row, AgreedRatePartyDto actor)
        => row.ProposerKind == actor.PartyKind && row.ProposerId == actor.PartyId || IsCounterparty(row, actor);
    private static bool IsCounterparty(AgreedRateTableRecord row, AgreedRatePartyDto actor)
        => row.CounterpartyKind == actor.PartyKind && row.CounterpartyId == actor.PartyId;
    private static void ValidateActor(AgreedRatePartyDto actor)
        => Require(actor.PartyKind is AgreedRatePartyKinds.Restaurant or AgreedRatePartyKinds.FoodDriver or AgreedRatePartyKinds.Shipper or AgreedRatePartyKinds.CargoDriver
                   && actor.PartyId.Length is > 0 and <= 191 && actor.PartyId == actor.PartyId.Trim(), "RateTableActorInvalid", 403);
    private static void Money(decimal money) => Require(money is >= 0 and <= 1000000000m && decimal.Round(money, 2) == money, "RateTableMoneyInvalid");
    private static string Clean(string? value) => value?.Trim() ?? "";
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static T Deserialize<T>(string value)
    {
        try { return JsonSerializer.Deserialize<T>(value, Json) ?? throw new AgreedRateTableException("RateTableEvidenceInvalid"); }
        catch (JsonException) { throw new AgreedRateTableException("RateTableEvidenceInvalid"); }
    }
    private static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(value)))).ToLowerInvariant();
    private static void Require(bool condition, string code, int status = 400) { if (!condition) throw new AgreedRateTableException(code, status); }
}
