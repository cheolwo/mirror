using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentResults;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using 살뜰.Services.Options;

namespace Ssalddel.Services.Community;

public interface I공간공동운영예약UseCase
{
    Task<공간공동운영예약목록Response> 목록Async(string ledgerId, CancellationToken ct = default);
    Task<공간공동운영예약후보Response> 후보Async(string ledgerId, CancellationToken ct = default);
    Task<공간공동운영예약Dto?> 조회Async(string ledgerId, Guid bookingId, CancellationToken ct = default);
    Task<공간공동운영예약Dto?> 요청결과Async(string ledgerId, Guid requestId, CancellationToken ct = default);
    Task<공간공동운영예약취소영향Dto> 취소영향Async(string ledgerId, Guid bookingId, CancellationToken ct = default);
    Task<Result<공간공동운영예약Dto>> 변경Async(string ledgerId, Guid bookingId,
        공간공동운영예약명령Request request, CancellationToken ct = default);
}

/// <summary>기존 보호 원장 안에 당사자의 시간 약속만 저장합니다. HR·재고·주문·배차·금융·외부 알림을 실행하지 않습니다.</summary>
[SsalddelUseCase("공동운영 시간 약속", Summary = "현재 역할과 조건을 확인한 당사자의 약속 제안·수락·거절·취소 영향을 원장 판본에 결속합니다.")]
public sealed class 공간공동운영예약UseCase(I공간공동운영Store store, I공간공동운영연결Query access,
    ICurrentUserAccessor currentUser, ISsalddelExecutionModePolicy execution,
    I공간공동운영전문업무Bridge bridge, TimeProvider clock) : I공간공동운영예약UseCase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private DateTimeOffset Now => clock.GetUtcNow();

    public async Task<공간공동운영예약목록Response> 목록Async(string ledgerId, CancellationToken ct = default)
    {
        var (record, actor) = await Owned(ledgerId, ct); var data = Extension(record);
        var visible = data.Bookings.Where(row => CanView(record, row, actor)).ToArray();
        var ids = visible.Select(row => row.BookingId).ToHashSet();
        var items = new List<공간공동운영예약Dto>();
        foreach (var row in visible.OrderBy(row => row.Terms.StartsAtUtc).ThenBy(row => row.BookingId))
            items.Add(await Response(record, data, row, actor, false, ct));
        return new()
        {
            LedgerId = record.LedgerId, Revision = record.Revision, TermsRevision = record.TermsRevision,
            ExecutionMode = record.ExecutionMode, AsOfUtc = Now, CanPropose = CanPropose(record, actor), Items = items,
            Notices = data.Receipts.Where(row => ids.Contains(row.BookingId) && row.RecipientUserIds.Contains(actor, StringComparer.Ordinal))
                .OrderByDescending(row => row.AtUtc).Take(80).Select(row => new 공간공동운영예약알림Dto
                { EventId = row.RequestId, BookingId = row.BookingId, Action = row.Action, OccurredAtUtc = row.AtUtc,
                    Message = row.Action switch { "propose" => "새 시간 약속이 제안됐어요.", "accept" => "약속 수락 기록이 바뀌었어요.",
                        "decline" => "참여자의 약속 거절이 기록됐어요.", _ => "참여자가 약속을 취소했어요. 연결 업무는 따로 확인해 주세요." } }).ToArray()
        };
    }

    public async Task<공간공동운영예약후보Response> 후보Async(string ledgerId, CancellationToken ct = default)
    {
        var (record, actor) = await Owned(ledgerId, ct); Extension(record);
        Require(CanPropose(record, actor), "SpaceBookingProposalUnavailable", 403);
        var cycles = new List<공간공동운영예약회차후보Dto>();
        foreach (var cycle in record.Cycles.Where(c => c.TermsRevision == record.TermsRevision && c.StatusCode is "prepared" or "running"))
        {
            var sources = await ReadSources(actor, cycle.References, ct);
            cycles.Add(new() { CycleId = cycle.CycleId, Number = cycle.Number, TermsRevision = cycle.TermsRevision, StatusCode = cycle.StatusCode,
                References = cycle.References.Where(reference => sources.Any(source => Same(reference, source) && source.Verified && !source.RequiresAttention))
                    .Select(reference => new 공간공동운영연결후보Dto { Reference = CandidateReference(reference, sources),
                        Label = sources.First(source => Same(reference, source)).Label }).ToArray() });
        }
        return new() { LedgerId = record.LedgerId, Revision = record.Revision, TermsRevision = record.TermsRevision,
            Participants = record.Participants.Where(CurrentParticipant(record)).Select(member => Party(record, member.UserId, member.RoleCode, actor, null)).ToArray(),
            Cycles = cycles };
    }

    public async Task<공간공동운영예약Dto?> 조회Async(string ledgerId, Guid bookingId, CancellationToken ct = default)
    {
        Require(bookingId != Guid.Empty, "SpaceBookingIdInvalid", 400);
        var (record, actor) = await Owned(ledgerId, ct); var data = Extension(record);
        var row = data.Bookings.SingleOrDefault(row => row.BookingId == bookingId && CanView(record, row, actor));
        return row is null ? null : await Response(record, data, row, actor, false, ct);
    }

    public async Task<공간공동운영예약Dto?> 요청결과Async(string ledgerId, Guid requestId, CancellationToken ct = default)
    {
        Require(requestId != Guid.Empty, "SpaceBookingRequestInvalid", 400);
        var (record, actor) = await Owned(ledgerId, ct); var data = Extension(record);
        var receipt = data.Receipts.SingleOrDefault(row => row.RequestId == requestId && row.ActorUserId == actor);
        var booking = receipt is null ? null : data.Bookings.SingleOrDefault(row => row.BookingId == receipt.BookingId && CanView(record, row, actor));
        return booking is null ? null : await Response(record, data, booking, actor, true, ct);
    }

    public async Task<공간공동운영예약취소영향Dto> 취소영향Async(string ledgerId, Guid bookingId, CancellationToken ct = default)
    {
        var (record, actor) = await Owned(ledgerId, ct); var data = Extension(record);
        var row = data.Bookings.SingleOrDefault(row => row.BookingId == bookingId && CanView(record, row, actor));
        Require(row is not null, "SpaceBookingNotFound", 404);
        return await Impact(record, row!, actor, ct);
    }

    public async Task<Result<공간공동운영예약Dto>> 변경Async(string ledgerId, Guid bookingId,
        공간공동운영예약명령Request request, CancellationToken ct = default)
    {
        try
        {
            Require(request is not null && bookingId != Guid.Empty && request.ClientRequestId != Guid.Empty
                && request.ExpectedRevision > 0 && request.ExpectedBookingRevision >= 0
                && request.Action is "propose" or "accept" or "decline" or "cancel", "SpaceBookingCommandInvalid", 400);
            Require(request!.Reason is null || request.Reason.Length <= 300 && !request.Reason.Any(c => char.IsControl(c) && c is not '\n' and not '\r'),
                "SpaceBookingReasonInvalid", 400);
            var (record, actor) = await Owned(ledgerId, ct); var data = Extension(record);
            var fingerprint = Digest(new { ledgerId, bookingId, request });
            var receipt = data.Receipts.SingleOrDefault(row => row.RequestId == request.ClientRequestId);
            if (receipt is not null)
            {
                Require(receipt.ActorUserId == actor && receipt.BookingId == bookingId && receipt.Fingerprint == fingerprint,
                    "SpaceBookingIdempotencyConflict");
                return Result.Ok(await Response(record, data, data.Bookings.Single(row => row.BookingId == bookingId), actor, true, ct));
            }
            Require(record.Revision == request.ExpectedRevision, "SpaceBookingRevisionConflict");
            Require(data.Receipts.Count < 400, "SpaceBookingCommandLimitReached");
            Require(request.ConfirmNonBinding, "SpaceBookingSeparateAuthorityConfirmationRequired", 400);
            var row = data.Bookings.SingleOrDefault(row => row.BookingId == bookingId);
            if (request.Action == "propose")
            {
                Require(row is null && request.ExpectedBookingRevision == 0 && data.Bookings.Count < 100, "SpaceBookingAlreadyExistsOrLimitReached");
                Require(CanPropose(record, actor), "SpaceBookingProposalUnavailable", 403);
                Require(request.ExpectedTermsRevision == record.TermsRevision, "SpaceBookingTermsChanged");
                var terms = await ValidateTerms(record, actor, request.Terms, ct);
                Require(!terms.RequiredForCycle || record.OperatorUserId == actor, "SpaceBookingRequiredOperatorOnly", 403);
                ValidateReplacement(record, data, bookingId, terms, actor);
                Require(!HasConflict(data, bookingId, terms, terms.Parties.Select(p => p.UserId)), "SpaceBookingTimeConflict");
                row = new() { BookingId = bookingId, Revision = 1, CooperationTermsRevision = record.TermsRevision,
                    Terms = terms, UpdatedAtUtc = Now, CreatorUserId = actor,
                    Parties = terms.Parties.Select(p => new BookingParty { UserId = p.UserId, RoleCode = p.RoleCode }).ToList() };
                data.Bookings.Add(row);
            }
            else
            {
                Require(request.Terms is null, "SpaceBookingTermsImmutable", 400);
                Require(row is not null && row.Parties.Any(p => p.UserId == actor), "SpaceBookingPartyRequired", 403);
                Require(row!.Revision == request.ExpectedBookingRevision, "SpaceBookingRecordChanged");
                Require(row.StatusCode is "proposed" or "confirmed", "SpaceBookingClosed");
                var party = row.Parties.Single(p => p.UserId == actor);
                Require(party.StateCode is "invited" or "accepted", "SpaceBookingPartyClosed");
                if (request.Action == "accept")
                {
                    Require(record.StatusCode == "gathering" && record.ExecutionMode == execution.Mode.ToString(), "SpaceBookingCooperationUnavailable");
                    Require(row.CooperationTermsRevision == record.TermsRevision && request.ExpectedTermsRevision == record.TermsRevision,
                        "SpaceBookingTermsChanged");
                    Require(CurrentParties(record, row) && CycleAvailable(record, row.Terms.CycleId), "SpaceBookingParticipantsChanged");
                    Require(ReferencesCurrent(record, row.Terms), "SpaceBookingReferencesChanged");
                    Require(Now < row.Terms.EndsAtUtc, "SpaceBookingWindowExpired");
                    Require(party.StateCode == "invited", "SpaceBookingAlreadyAccepted");
                    var sourceStates = await ReadSources(actor, row.Terms.References, ct);
                    Require(SourcesReady(row.Terms.References, sourceStates), "SpaceBookingSourceChanged");
                    Require(!HasConflict(data, bookingId, row.Terms, row.Parties.Select(p => p.UserId)), "SpaceBookingTimeConflict");
                    party.StateCode = "accepted"; party.AcceptedAtUtc = Now;
                    row.StatusCode = row.Parties.All(p => p.StateCode == "accepted") ? "confirmed" : "proposed";
                }
                else if (request.Action == "decline")
                {
                    Require(party.StateCode == "invited", "SpaceBookingDeclineRequiresInvitation");
                    party.StateCode = "declined"; row.StatusCode = "declined";
                }
                else
                {
                    var impact = await Impact(record, row, actor, ct);
                    Require(impact.CanCancel && request.ImpactDigest is { Length: 64 }
                        && request.ImpactDigest == impact.Digest, "SpaceBookingImpactChanged");
                    // 본인의 시간 약속만 철회합니다. 다른 당사자의 과거 수락과 전문 원장·재고·배송은 변경하지 않습니다.
                    party.StateCode = "cancelled"; row.StatusCode = "cancelled";
                    row.ProfessionalCleanupRequired = impact.ProfessionalCleanupRequired;
                }
                row.Revision++; row.UpdatedAtUtc = Now;
            }
            var recipients = row!.Parties.Select(p => p.UserId).Append(record.OperatorUserId ?? "")
                .Where(Id).Distinct(StringComparer.Ordinal).ToList();
            data.Receipts.Add(new() { RequestId = request.ClientRequestId, BookingId = bookingId, ActorUserId = actor,
                Fingerprint = fingerprint, Action = request.Action, AtUtc = Now, Reason = request.Reason?.Trim(), RecipientUserIds = recipients });
            await Save(record, data, actor, request.ExpectedRevision, ct);
            return Result.Ok(await Response(record, data, row, actor, false, ct));
        }
        catch (공간공동운영예약Exception e)
        { return Result.Fail<공간공동운영예약Dto>(new Error(e.Code).WithMetadata("StatusCode", e.Status).WithMetadata("code", e.Code)); }
    }

    private async Task<(공간공동운영Record, string)> Owned(string ledgerId, CancellationToken ct)
    {
        Require(Id(ledgerId), "SpaceBookingLedgerInvalid", 400);
        var actor = currentUser.UserId?.Trim(); Require(Id(actor), "SpaceBookingAuthenticationRequired", 401);
        var record = await access.조회Async(ledgerId, actor!, ct);
        Require(record is not null, "SpaceBookingCooperationNotFound", 404);
        return (record!, actor!);
    }

    private async Task Save(공간공동운영Record record, ReservationExtension data, string actor, long expected, CancellationToken ct)
    {
        var previous = record.WorkReservationExtensionJson; var previousAt = record.UpdatedAtUtc;
        record.WorkReservationExtensionJson = JsonSerializer.Serialize(data, Json); record.Revision = expected + 1; record.UpdatedAtUtc = Now;
        if (!await store.교체Async(record, expected, actor, ct))
        {
            record.WorkReservationExtensionJson = previous; record.Revision = expected; record.UpdatedAtUtc = previousAt;
            throw new 공간공동운영예약Exception("SpaceBookingRevisionConflict");
        }
    }

    private static ReservationExtension Extension(공간공동운영Record record)
    {
        if (record.WorkReservationExtensionJson is null) return new();
        try
        {
            var data = JsonSerializer.Deserialize<ReservationExtension>(record.WorkReservationExtensionJson, Json);
            Require(data is not null && data.SchemaVersion == 1 && data.Bookings is { Count: <= 100 }
                && data.Receipts is { Count: <= 400 } && data.Bookings.All(StoredBookingValid)
                && data.Bookings.Select(row => row.BookingId).Distinct().Count() == data.Bookings.Count
                && data.Receipts.All(receipt => receipt is not null && receipt.RequestId != Guid.Empty && Id(receipt.ActorUserId)
                    && receipt.Fingerprint is { Length: 64 } && receipt.Action is "propose" or "accept" or "decline" or "cancel"
                    && receipt.RecipientUserIds is { Count: <= 13 } && receipt.RecipientUserIds.All(Id)
                    && data.Bookings.Any(row => row.BookingId == receipt.BookingId))
                && data.Receipts.Select(receipt => receipt.RequestId).Distinct().Count() == data.Receipts.Count,
                "SpaceBookingStoredDataInvalid", 500);
            Require(data!.Bookings.All(row => row.Terms.ReplacesBookingId is not { } oldId || oldId != row.BookingId
                && data.Bookings.Any(old => old.BookingId == oldId && old.StatusCode is "declined" or "cancelled"
                    && old.Terms.CycleId == row.Terms.CycleId && old.Terms.KindCode == row.Terms.KindCode
                    && old.Terms.RequiredForCycle == row.Terms.RequiredForCycle)
                && data.Bookings.Count(other => other.Terms.ReplacesBookingId == oldId) == 1), "SpaceBookingStoredDataInvalid", 500);
            Require(data.Bookings.All(row => !ReplacementCycle(data, row)), "SpaceBookingStoredDataInvalid", 500);
            return data!;
        }
        catch (JsonException) { throw new 공간공동운영예약Exception("SpaceBookingStoredDataInvalid", 500); }
    }

    private static bool StoredBookingValid(Booking row)
        => row is not null && row.BookingId != Guid.Empty && row.Revision > 0 && row.CooperationTermsRevision > 0
            && Id(row.CreatorUserId) && row.StatusCode is "proposed" or "confirmed" or "declined" or "cancelled"
            && row.Terms is not null && Id(row.Terms.CycleId) && 공간공동운영예약종류Codes.All.Contains(row.Terms.KindCode, StringComparer.Ordinal)
            && row.Terms.StartsAtUtc.Offset == TimeSpan.Zero && row.Terms.EndsAtUtc.Offset == TimeSpan.Zero
            && row.Terms.StartsAtUtc < row.Terms.EndsAtUtc && row.Terms.EndsAtUtc - row.Terms.StartsAtUtc <= TimeSpan.FromDays(7)
            && row.Terms.Parties is { Count: >= 2 and <= 12 } && row.Terms.Capacity == row.Terms.Parties.Count
            && row.Terms.Parties.All(p => p is not null && Id(p.UserId) && 공간공동운영역할Codes.IsKnown(p.RoleCode))
            && row.Terms.Parties.Select(p => p.UserId).Distinct(StringComparer.Ordinal).Count() == row.Terms.Parties.Count
            && row.Terms.References is { Count: <= 8 } && row.Terms.References.All(r => r is not null && Id(r.KindCode)
                && Id(r.ReferenceId) && (r.ParentId is null || Id(r.ParentId)))
            && row.Terms.References.Select(r => (r.KindCode, r.ReferenceId)).Distinct().Count() == row.Terms.References.Count
            && row.Parties is { Count: >= 2 and <= 12 } && row.Parties.Count == row.Terms.Parties.Count
            && row.Parties.All(p => p is not null && Id(p.UserId) && 공간공동운영역할Codes.IsKnown(p.RoleCode)
                && p.StateCode is "invited" or "accepted" or "declined" or "cancelled"
                && row.Terms.Parties.Any(term => term.UserId == p.UserId && term.RoleCode == p.RoleCode))
            && row.Parties.Select(p => p.UserId).Distinct(StringComparer.Ordinal).Count() == row.Parties.Count
            && row.Parties.Any(p => p.UserId == row.CreatorUserId)
            && (row.StatusCode != "confirmed" || row.Parties.All(p => p.StateCode == "accepted"));

    private static bool ReplacementCycle(ReservationExtension data, Booking row)
    {
        var seen = new HashSet<Guid> { row.BookingId };
        while (row.Terms.ReplacesBookingId is { } parent)
        {
            if (!seen.Add(parent)) return true;
            row = data.Bookings.Single(booking => booking.BookingId == parent);
        }
        return false;
    }

    private static void ValidateReplacement(공간공동운영Record record, ReservationExtension data, Guid bookingId, 공간공동운영예약조건Dto terms, string actor)
    {
        if (terms.ReplacesBookingId is not { } oldId) return;
        var old = data.Bookings.SingleOrDefault(row => row.BookingId == oldId);
        Require(old is not null && oldId != bookingId && old.StatusCode is "declined" or "cancelled"
            && old.Terms.CycleId == terms.CycleId && old.Terms.KindCode == terms.KindCode
            && old.Terms.RequiredForCycle == terms.RequiredForCycle, "SpaceBookingReplacementInvalid", 400);
        Require(old!.Parties.Any(p => p.UserId == actor) || old.Terms.RequiredForCycle && record.OperatorUserId == actor,
            "SpaceBookingReplacementPartyRequired", 403);
        // 원 약속을 여러 갈래로 해석하지 않습니다. 대체 약속도 닫혔다면 그 마지막 약속을 명시 선택합니다.
        Require(!data.Bookings.Any(row => row.Terms.ReplacesBookingId == oldId), "SpaceBookingReplacementAlreadyExists");
    }

    private async Task<공간공동운영예약조건Dto> ValidateTerms(공간공동운영Record record, string actor,
        공간공동운영예약조건Dto? terms, CancellationToken ct)
    {
        Require(terms is not null && Id(terms.CycleId) && 공간공동운영예약종류Codes.All.Contains(terms.KindCode, StringComparer.Ordinal)
            && terms.StartsAtUtc.Offset == TimeSpan.Zero && terms.EndsAtUtc.Offset == TimeSpan.Zero
            && terms.StartsAtUtc >= Now && terms.StartsAtUtc < terms.EndsAtUtc && terms.EndsAtUtc <= Now.AddDays(366)
            && terms.EndsAtUtc - terms.StartsAtUtc <= TimeSpan.FromDays(7)
            && terms.Parties is { Count: >= 2 and <= 12 } && terms.Capacity == terms.Parties.Count
            && terms.Parties.All(p => p is not null && Id(p.UserId) && 공간공동운영역할Codes.IsKnown(p.RoleCode))
            && terms.Parties.Select(p => p.UserId).Distinct(StringComparer.Ordinal).Count() == terms.Parties.Count
            && terms.Parties.Any(p => p.UserId == actor), "SpaceBookingTermsInvalid", 400);
        Require(CycleAvailable(record, terms!.CycleId), "SpaceBookingCycleChanged");
        Require(terms.Parties.All(p => record.Participants.Any(member => member.UserId == p.UserId && member.RoleCode == p.RoleCode
            && member.StateCode == "accepted" && member.AgreedTermsRevision == record.TermsRevision)), "SpaceBookingParticipantsChanged", 403);
        var cycle = record.Cycles.Single(c => c.CycleId == terms.CycleId);
        Require(terms.References is { Count: <= 8 } && terms.References.All(reference => reference is not null && Id(reference.KindCode)
            && Id(reference.ReferenceId) && (reference.ParentId is null || Id(reference.ParentId))
            && cycle.References.Any(saved => saved.KindCode == reference.KindCode && saved.ReferenceId == reference.ReferenceId && saved.ParentId == reference.ParentId))
            && terms.References.Select(r => (r.KindCode, r.ReferenceId)).Distinct().Count() == terms.References.Count,
            "SpaceBookingReferencesInvalid", 400);
        var sources = await ReadSources(actor, terms.References, ct);
        Require(SourcesReady(terms.References, sources), "SpaceBookingSourceChanged");
        var normalized = Copy(terms);
        // 클라이언트가 판본을 생략해도 제안 시 확인한 원천 판본에 고정합니다.
        normalized.References = terms.References.Select(reference => CandidateReference(reference, sources)).ToArray();
        return normalized;
    }

    private async Task<공간공동운영예약취소영향Dto> Impact(공간공동운영Record record, Booking row, string actor, CancellationToken ct)
    {
        var sources = await ReadSources(actor, row.Terms.References, ct);
        var cycle = record.Cycles.FirstOrDefault(c => c.CycleId == row.Terms.CycleId);
        var cleanup = row.Terms.References.Count > 0 || cycle?.StatusCode == "running";
        var warnings = new List<string> { "본인의 시간 약속 철회만 기록합니다. 다른 참여자의 과거 수락과 실제 업무는 삭제하지 않습니다.",
            "실제 비용·기대 손실·배상액은 확인하지 않았습니다. 자동 감액이나 불이익을 만들지 않습니다.",
            "다른 통합 원장·외부 일정은 확인하지 않았습니다." };
        if (cleanup) warnings.Add("연결된 전문 업무의 중지·반환·인계는 원 업무에서 따로 확인해야 합니다.");
        if (sources.Any(source => !source.Verified)) warnings.Add("연결 업무의 현재 상태를 확인하지 못했습니다. 당사자와 별도 정리가 필요합니다.");
        return new() { LedgerId = record.LedgerId, BookingId = row.BookingId, ParentRevision = record.Revision,
            BookingRevision = row.Revision, TermsRevision = record.TermsRevision, AsOfUtc = Now, ProfessionalCleanupRequired = cleanup,
            CanCancel = (row.StatusCode is "proposed" or "confirmed") && row.Parties.Any(p => p.UserId == actor && (p.StateCode is "invited" or "accepted")),
            Sources = sources, Warnings = warnings,
            Digest = Digest(new { Actor = actor, record.LedgerId, record.Revision, record.TermsRevision, record.ExecutionMode,
                CurrentMode = execution.Mode.ToString(), Booking = row, Cycle = cycle is null ? null : new { cycle.CycleId, cycle.StatusCode, cycle.TermsRevision, cycle.References },
                Sources = sources.OrderBy(s => s.KindCode, StringComparer.Ordinal).ThenBy(s => s.ReferenceId, StringComparer.Ordinal)
                    .Select(s => new { s.KindCode, s.ReferenceId, s.Verified, s.VerifiedRevision, s.StatusCode, s.Completed, s.RequiresAttention }).ToArray() }) };
    }

    private async Task<공간공동운영예약Dto> Response(공간공동운영Record record, ReservationExtension data, Booking row,
        string actor, bool replay, CancellationToken ct)
    {
        var blockers = new List<string>();
        if (record.StatusCode != "gathering") blockers.Add("모임이 중지 또는 종료되어 새 업무를 시작할 수 없습니다.");
        if (record.ExecutionMode != execution.Mode.ToString()) blockers.Add("현재 실행 모드와 다른 약속입니다.");
        if (row.CooperationTermsRevision != record.TermsRevision || !CurrentParties(record, row)) blockers.Add("참여 역할과 현재 조건을 다시 확인해야 합니다.");
        if (!CycleAvailable(record, row.Terms.CycleId)) blockers.Add("현재 조건의 진행 가능한 회차를 확인해야 합니다.");
        if (!ReferencesCurrent(record, row.Terms)) blockers.Add("시간 약속에 연결했던 전문 업무가 현재 회차와 달라졌습니다.");
        if (row.StatusCode != "confirmed") blockers.Add("필수 당사자의 같은 시간 약속 수락이 모두 필요합니다.");
        if (Now >= row.Terms.EndsAtUtc) blockers.Add("약속한 시간창이 지났습니다.");
        if (HasConflict(data, row.BookingId, row.Terms, row.Parties.Select(p => p.UserId))) blockers.Add("같은 통합 원장에서 시간이 겹치는 다른 수락 약속이 있습니다.");
        var sources = await ReadSources(actor, row.Terms.References, ct);
        if (row.Terms.References.Count == 0) blockers.Add("연결한 전문 업무가 없어 실제 업무 준비는 확인하지 못했습니다.");
        else if (!SourcesReady(row.Terms.References, sources)) blockers.Add("연결한 전문 업무의 현재 권한·판본·상태를 확인해야 합니다.");
        var party = row.Parties.SingleOrDefault(p => p.UserId == actor);
        var actions = new List<string>();
        if (party is not null && (row.StatusCode is "proposed" or "confirmed") && (party.StateCode is "invited" or "accepted"))
        {
            actions.Add("cancel");
            if (party.StateCode == "invited")
            {
                actions.Add("decline");
                if (record.StatusCode == "gathering" && record.ExecutionMode == execution.Mode.ToString()
                    && row.CooperationTermsRevision == record.TermsRevision && CurrentParties(record, row)
                    && CycleAvailable(record, row.Terms.CycleId) && Now < row.Terms.EndsAtUtc) actions.Add("accept");
            }
        }
        return new() { LedgerId = record.LedgerId, ParentRevision = record.Revision, BookingId = row.BookingId,
            Revision = row.Revision, CooperationTermsRevision = row.CooperationTermsRevision, Terms = Copy(row.Terms), StatusCode = row.StatusCode,
            Parties = row.Parties.Select(p => Party(record, p.UserId, p.RoleCode, actor, p)).ToArray(), AllowedActions = actions,
            ReadinessBlockers = blockers, ProfessionalReviewReady = blockers.Count == 0,
            ProfessionalCleanupRequired = row.ProfessionalCleanupRequired, UpdatedAtUtc = row.UpdatedAtUtc, Replayed = replay,
            History = data.Receipts.Where(r => r.BookingId == row.BookingId).Select(r => new 공간공동운영예약변경Dto
            { EventId = r.RequestId, Action = r.Action, IsMine = r.ActorUserId == actor, Reason = r.Reason, OccurredAtUtc = r.AtUtc }).ToArray() };
    }

    private async Task<IReadOnlyList<공간공동운영원천상태Dto>> ReadSources(string actor,
        IReadOnlyList<공간공동운영전문참조Dto> references, CancellationToken ct)
    {
        if (references.Count == 0) return [];
        try
        {
            var found = await bridge.ValidateAsync(actor, references, ct);
            return references.Select(reference => found.Count(s => Same(reference, s)) == 1 ? Copy(found.Single(s => Same(reference, s)))
                : Unavailable(reference)).ToArray();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return references.Select(Unavailable).ToArray(); }
    }

    private static 공간공동운영원천상태Dto Unavailable(공간공동운영전문참조Dto reference)
        => new() { KindCode = reference.KindCode, ReferenceId = reference.ReferenceId, StatusCode = "unavailable",
            Label = "연결 업무 확인 필요", RequiresAttention = true };
    private static bool SourcesReady(IReadOnlyList<공간공동운영전문참조Dto> references, IReadOnlyList<공간공동운영원천상태Dto> sources)
        => references.All(reference => sources.Count(source => Same(reference, source) && source.Verified && !source.RequiresAttention
            && (!reference.ExpectedRevision.HasValue || reference.ExpectedRevision == source.VerifiedRevision)) == 1);
    private static bool Same(공간공동운영전문참조Dto reference, 공간공동운영원천상태Dto source)
        => reference.KindCode == source.KindCode && reference.ReferenceId == source.ReferenceId;
    private static bool HasConflict(ReservationExtension data, Guid bookingId, 공간공동운영예약조건Dto terms, IEnumerable<string> users)
    {
        var ids = users.ToHashSet(StringComparer.Ordinal);
        return data.Bookings.Any(other => other.BookingId != bookingId && (other.StatusCode is "proposed" or "confirmed")
            && terms.StartsAtUtc < other.Terms.EndsAtUtc && other.Terms.StartsAtUtc < terms.EndsAtUtc
            && other.Parties.Any(p => ids.Contains(p.UserId) && p.StateCode == "accepted"));
    }
    private bool CanPropose(공간공동운영Record record, string actor)
        => record.StatusCode == "gathering" && record.ExecutionMode == execution.Mode.ToString()
            && record.Participants.Any(p => p.UserId == actor && CurrentParticipant(record)(p))
            && record.Cycles.Any(c => c.TermsRevision == record.TermsRevision && c.StatusCode is "prepared" or "running");
    private static Func<공간공동운영참여Record, bool> CurrentParticipant(공간공동운영Record record)
        => member => member.StateCode == "accepted" && member.AgreedTermsRevision == record.TermsRevision;
    private static bool CurrentParties(공간공동운영Record record, Booking row)
        => row.Parties.All(p => record.Participants.Any(member => member.UserId == p.UserId && member.RoleCode == p.RoleCode && CurrentParticipant(record)(member)));
    private static bool CycleAvailable(공간공동운영Record record, string cycleId)
        => record.Cycles.Any(c => c.CycleId == cycleId && c.TermsRevision == record.TermsRevision && c.StatusCode is "prepared" or "running");
    private static bool ReferencesCurrent(공간공동운영Record record, 공간공동운영예약조건Dto terms)
        => record.Cycles.Any(c => c.CycleId == terms.CycleId && terms.References.All(reference => c.References.Any(saved =>
            saved.KindCode == reference.KindCode && saved.ReferenceId == reference.ReferenceId && saved.ParentId == reference.ParentId)));
    private static bool CanView(공간공동운영Record record, Booking row, string actor)
        => row.Parties.Any(p => p.UserId == actor) || record.OperatorUserId == actor
            && record.Participants.Any(p => p.UserId == actor && p.StateCode == "accepted" && p.RoleCode is 공간공동운영역할Codes.SpaceProvider or 공간공동운영역할Codes.Coordinator);
    private static 공간공동운영예약당사자Dto Party(공간공동운영Record record, string userId, string roleCode, string actor, BookingParty? stored)
    {
        var current = record.Participants.FirstOrDefault(p => p.UserId == userId && p.RoleCode == roleCode);
        return new() { UserId = userId, RoleCode = roleCode, IsMe = userId == actor,
            DisplayName = current is { IntroductionConsented: true, StateCode: not "withdrawn" } && !string.IsNullOrWhiteSpace(current.PublicDisplayName)
                ? current.PublicDisplayName : "함께하는 참여자", PartyStateCode = stored?.StateCode ?? "invited", AcceptedAtUtc = stored?.AcceptedAtUtc };
    }
    private static 공간공동운영전문참조Dto CandidateReference(공간공동운영전문참조Dto reference,
        IReadOnlyList<공간공동운영원천상태Dto> sources)
    {
        var result = Copy(reference);
        result.ExpectedRevision = sources.Single(source => Same(reference, source)).VerifiedRevision;
        return result;
    }

    /// <summary>상위 회차 시작 전의 읽기 전용 관문입니다. 약속이 없는 기존 회차는 보존하며 예약 취소를 임의 대체하지 않습니다.</summary>
    public static IReadOnlyList<string> 회차준비보류사유(공간공동운영Record record, string? cycleId,
        DateTimeOffset now, IReadOnlyList<공간공동운영원천상태Dto>? currentSources = null)
    {
        var data = Extension(record);
        if (cycleId is null) return [];
        var rows = data.Bookings.Where(row => row.Terms.CycleId == cycleId && row.Terms.RequiredForCycle).ToArray();
        if (rows.Length == 0) return [];
        var blockers = new List<string>();
        var activeRows = rows.Where(row => row.StatusCode is not ("cancelled" or "declined")).ToArray();
        var cycle = record.Cycles.FirstOrDefault(c => c.CycleId == cycleId);
        bool Ready(Booking row) => row.StatusCode == "confirmed" && row.Parties.All(p => p.StateCode == "accepted")
            && row.CooperationTermsRevision == record.TermsRevision && CurrentParties(record, row)
            && row.Terms.EndsAtUtc > now && row.Terms.References.Count > 0 && currentSources is not null
            && SourcesReady(row.Terms.References, currentSources) && cycle is not null
            && row.Terms.References.All(reference => cycle.References.Any(saved => saved.KindCode == reference.KindCode
                && saved.ReferenceId == reference.ReferenceId && saved.ParentId == reference.ParentId))
            && !HasConflict(data, row.BookingId, row.Terms, row.Parties.Select(p => p.UserId));
        bool ExplicitlyReplaced(Booking closed)
        {
            var seen = new HashSet<Guid> { closed.BookingId };
            var next = data.Bookings.SingleOrDefault(row => row.Terms.ReplacesBookingId == closed.BookingId);
            while (next is not null && seen.Add(next.BookingId))
            {
                if (Ready(next)) return true;
                if (next.StatusCode is not ("cancelled" or "declined")) return false;
                next = data.Bookings.SingleOrDefault(row => row.Terms.ReplacesBookingId == next.BookingId);
            }
            return false;
        }
        if (rows.Any(row => (row.StatusCode is "cancelled" or "declined") && !ExplicitlyReplaced(row)))
            blockers.Add("거절·취소된 시간 약속의 후속 정리가 필요합니다. 명시한 대체 약속을 모두 수락하기 전에는 자동 대체하지 않습니다.");
        if (activeRows.Any(row => row.StatusCode != "confirmed" || row.Parties.Any(p => p.StateCode != "accepted")))
            blockers.Add("이 회차 시간 약속의 필수 당사자 수락이 모두 필요합니다.");
        if (activeRows.Any(row => row.CooperationTermsRevision != record.TermsRevision || !CurrentParties(record, row)))
            blockers.Add("시간 약속 당사자의 현재 역할과 운영 조건 동의를 다시 확인해 주세요.");
        if (activeRows.Any(row => row.Terms.EndsAtUtc <= now)) blockers.Add("만료된 시간 약속이 있어 새로운 시간 확인이 필요합니다.");
        if (activeRows.Any(row => HasConflict(data, row.BookingId, row.Terms, row.Parties.Select(p => p.UserId))))
            blockers.Add("같은 통합 원장 안에서 수락한 시간 약속이 겹칩니다.");
        if (activeRows.Any(row => row.Terms.References.Any(reference => cycle is null || !cycle.References.Any(saved => saved.KindCode == reference.KindCode
            && saved.ReferenceId == reference.ReferenceId && saved.ParentId == reference.ParentId))))
            blockers.Add("시간 약속에 연결했던 전문 업무가 현재 회차와 달라졌습니다.");
        if (activeRows.Any(row => row.Terms.References.Count == 0))
            blockers.Add("시간 약속에 연결한 전문 업무가 없어 실제 업무 준비를 확인하지 못했습니다.");
        if (activeRows.Any(row => row.Terms.References.Count > 0 && (currentSources is null || !SourcesReady(row.Terms.References, currentSources))))
            blockers.Add("시간 약속에 연결한 전문 업무의 현재 권한·판본·상태를 다시 확인해 주세요.");
        return blockers;
    }
    private static bool Id(string? value) => value is { Length: > 0 and <= 191 } && value == value.Trim() && !value.Any(char.IsControl);
    private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Json), Json)!;
    private static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));
    private static void Require(bool value, string code, int status = 409) { if (!value) throw new 공간공동운영예약Exception(code, status); }

    public sealed class ReservationExtension
    {
        public int SchemaVersion { get; set; } = 1;
        public List<Booking> Bookings { get; set; } = [];
        public List<ReservationReceipt> Receipts { get; set; } = [];
    }
    public sealed class Booking
    {
        public Guid BookingId { get; set; }
        public long Revision { get; set; }
        public long CooperationTermsRevision { get; set; }
        public string CreatorUserId { get; set; } = "";
        public 공간공동운영예약조건Dto Terms { get; set; } = new();
        public List<BookingParty> Parties { get; set; } = [];
        public string StatusCode { get; set; } = "proposed";
        public bool ProfessionalCleanupRequired { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }
    public sealed class BookingParty
    {
        public string UserId { get; set; } = "";
        public string RoleCode { get; set; } = "";
        public string StateCode { get; set; } = "invited";
        public DateTimeOffset? AcceptedAtUtc { get; set; }
    }
    public sealed class ReservationReceipt
    {
        public Guid RequestId { get; set; }
        public Guid BookingId { get; set; }
        public string ActorUserId { get; set; } = "";
        public string Fingerprint { get; set; } = "";
        public string Action { get; set; } = "";
        public string? Reason { get; set; }
        public DateTimeOffset AtUtc { get; set; }
        public List<string> RecipientUserIds { get; set; } = [];
    }
}

public sealed class 공간공동운영예약Exception(string code, int status = 409) : Exception(code)
{ public string Code { get; } = code; public int Status { get; } = status; }
