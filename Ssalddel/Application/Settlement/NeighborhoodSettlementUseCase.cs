using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Settlement;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.도메인.정산;

namespace Ssalddel.Application.Settlement;

public interface INeighborhoodSettlementUseCase
{
    Task<NeighborhoodSettlementReceiptDto> CreateAsync(string actorUserId, NeighborhoodSettlementAgreementRequest request, CancellationToken ct);
    Task<NeighborhoodSettlementReceiptDto> TermsAsync(string id, string actorUserId, NeighborhoodSettlementAgreementRequest request, CancellationToken ct);
    Task<NeighborhoodSettlementReceiptDto> ConsentAsync(string id, string actorUserId, NeighborhoodSettlementConsentRequest request, CancellationToken ct);
    Task<NeighborhoodSettlementReceiptDto> AccrueAsync(string id, string actorUserId, NeighborhoodSettlementAccrualRequest request, CancellationToken ct);
    Task<NeighborhoodActivityRewardPreviewDto> ActivityRewardPreviewAsync(string id, string actorUserId, CancellationToken ct);
    Task<NeighborhoodSettlementReceiptDto> AccrueActivityRewardAsync(string id, string actorUserId, NeighborhoodSettlementActivityRewardAccrualRequest request, CancellationToken ct);
    Task<NeighborhoodSettlementReceiptDto> ConfirmEntryAsync(string id, string actorUserId, NeighborhoodSettlementEntryConfirmRequest request, CancellationToken ct);
    Task<NeighborhoodSettlementReceiptDto> DisputeAsync(string id, string actorUserId, NeighborhoodSettlementDisputeRequest request, CancellationToken ct);
    Task<NeighborhoodSettlementReceiptDto> AdjustmentAsync(string id, string actorUserId, NeighborhoodSettlementAdjustmentRequest request, CancellationToken ct);
    Task<NeighborhoodSettlementReceiptDto> ReportPaymentAsync(string id, string actorUserId, NeighborhoodSettlementPaymentReportRequest request, CancellationToken ct);
    Task<NeighborhoodSettlementReceiptDto> ConfirmReceiptAsync(string id, string actorUserId, NeighborhoodSettlementPaymentReceiptRequest request, CancellationToken ct);
    Task<NeighborhoodSettlementSummaryDto> GetAsync(string id, string actorUserId, CancellationToken ct);
    Task<IReadOnlyList<NeighborhoodSettlementSummaryDto>> MySummaryAsync(string actorUserId, CancellationToken ct);
    Task<WorkerWeeklyStatementDto> WeeklyStatementAsync(string actorUserId, DateOnly? completionDateKst, CancellationToken ct);
    Task<NeighborhoodSettlementReceiptDto> ReceiptAsync(string actorUserId, Guid requestId, CancellationToken ct);
}

/// <summary>합의·실제 업무량·당사자 지급 진술을 분리합니다. 기존 지급 원장이나 실제 자금은 변경하지 않습니다.</summary>
public sealed partial class NeighborhoodSettlementUseCase(SsalddelContext db,
    INeighborhoodSettlementSourceResolver sources, ISsalddelExecutionModePolicy execution,
    IOptions<NeighborhoodSettlementOptions> options, TimeProvider clock) : INeighborhoodSettlementUseCase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, Json)!;
    private static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(value)))).ToLowerInvariant();
    private static string NewId(string kind) => kind + "-" + Guid.NewGuid().ToString("N");
    private static void Require(bool condition, string code, int status = 409)
    { if (!condition) throw new NeighborhoodSettlementException(code, status); }
    private void Gate(string actor)
    {
        Require(options.Value.Enabled && options.Value.Profile == NeighborhoodSettlementOptions.RequiredProfile && execution.IsSimulation,
            "NeighborhoodSettlementDisabled", 404);
        Require(!string.IsNullOrWhiteSpace(actor) && actor == actor.Trim() && actor.Length <= 191, "NeighborhoodSettlementAuthenticationRequired", 401);
        Require(db.Database.IsRelational(), "NeighborhoodSettlementRelationalStorageRequired");
    }
    private static void Revision(NeighborhoodSettlementAgreementRecord row, NeighborhoodSettlementCommand request)
        => Require(row.Revision == request.ExpectedRevision, "NeighborhoodSettlementRevisionConflict");
    private static string Text(string? value, int maximum = 191)
    {
        Require(value is not null && value == value.Trim() && value.Length > 0 && value.Length <= maximum,
            "NeighborhoodSettlementInputInvalid", 400);
        return value!;
    }
    private static bool Party(NeighborhoodSettlementSourceSnapshot source, string actor)
        => source.Participants.Any(x => x.UserId == actor);
    private static bool LineParty(NeighborhoodSettlementTermLine line, string actor)
        => line.PayerUserId == actor || line.PayeeUserId == actor || line.DeliveryCostShares.Any(x => x.PayerUserId == actor);
    private static bool EntryParty(NeighborhoodSettlementEntryDto line, string actor)
        => line.PayerUserId == actor || line.PayeeUserId == actor;
    private static string[] RequiredConsents(NeighborhoodSettlementAgreementRequest terms)
        => terms.Lines.SelectMany(x => new[] { x.PayerUserId, x.PayeeUserId }.Concat(x.DeliveryCostShares.Select(s => s.PayerUserId)))
            .Concat(terms.ActivityRewardTerms is { } reward ? reward.ParticipantUserIds.Append(reward.PayerUserId) : [])
            .Concat(terms.ActivityRewardTerms?.Economics is { } economics ? new[] { economics.PlatformPayeeUserId } : [])
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToArray();
    private static NeighborhoodSettlementAgreementRequest Terms(NeighborhoodSettlementAgreementRecord row)
        => Deserialize<NeighborhoodSettlementAgreementRequest>(row.TermsJson);
    private static NeighborhoodSettlementSourceSnapshot Source(NeighborhoodSettlementAgreementRecord row)
        => Deserialize<NeighborhoodSettlementSourceSnapshot>(row.SourceSnapshotJson);

    private async Task<NeighborhoodSettlementAgreementRecord> Owned(string id, string actor, CancellationToken ct)
    {
        var row = await db.Set<NeighborhoodSettlementAgreementRecord>().SingleOrDefaultAsync(x => x.AgreementId == id, ct)
            ?? throw new NeighborhoodSettlementException("NeighborhoodSettlementNotFound", 404);
        if (row.CreatorUserId == actor || Party(Source(row), actor) || PlatformParty(Terms(row), actor)) return row;
        // 조건 변경 전 본인에게 발생한 채무·수금 근거도 본인 범위에서는 계속 조회합니다.
        var versions = await db.Set<NeighborhoodSettlementAgreementVersionRecord>().AsNoTracking()
            .Where(x => x.AgreementId == id).ToArrayAsync(ct);
        Require(versions.Any(x => Party(Deserialize<NeighborhoodSettlementSourceSnapshot>(x.SourceSnapshotJson), actor)
            || PlatformParty(Deserialize<NeighborhoodSettlementAgreementRequest>(x.TermsJson), actor)),
            "NeighborhoodSettlementNotFound", 404);
        return row;
    }
    private static void AppendHistory(NeighborhoodSettlementAgreementRecord row, string actor, string kind, DateTime at)
    {
        var events = Deserialize<List<History>>(row.HistoryJson);
        events.Add(new(actor, kind, row.TermsVersion, row.Revision, at)); row.HistoryJson = Serialize(events);
    }
    private sealed record History(string ActorUserId, string Kind, int TermsVersion, long Revision, DateTime RecordedAtUtc);
    private void VersionSnapshot(NeighborhoodSettlementAgreementRecord row)
        => db.Add(new NeighborhoodSettlementAgreementVersionRecord
        {
            AgreementId = row.AgreementId, TermsVersion = row.TermsVersion, TermsJson = row.TermsJson,
            SourceSnapshotJson = row.SourceSnapshotJson, ConsentsJson = row.ConsentsJson, CreatedAtUtc = Now
        });

    public Task<NeighborhoodSettlementReceiptDto> CreateAsync(string actor, NeighborhoodSettlementAgreementRequest r, CancellationToken ct)
        => Execute(actor, "Create", "", r, async () =>
        {
            Require(r.ExpectedRevision == 0, "NeighborhoodSettlementRevisionConflict");
            var source = await sources.ResolveAsync(Text(r.SourceKind, 40), Text(r.SourceId, 120), ct);
            var terms = WorkflowTerms(r);
            Require(Party(source, actor), "NeighborhoodSettlementPartyRequired", 403); ValidateTerms(terms, source); ValidateActivityRewardTerms(terms, source);
            await ValidatePlatformShareTerms(terms, source, ct);
            ValidateInventoryCostSelections(terms);
            var row = new NeighborhoodSettlementAgreementRecord
            {
                AgreementId = NewId("nsa"), SourceKind = r.SourceKind, SourceId = r.SourceId, CreatorUserId = actor,
                ActivityKind = terms.ActivityKind, TermsJson = Serialize(terms), SourceSnapshotJson = Serialize(source),
                StatusCode = NeighborhoodSettlementCodes.AwaitingConsent, CreatedAtUtc = Now, UpdatedAtUtc = Now
            };
            AppendHistory(row, actor, "Create", Now); db.Add(row); VersionSnapshot(row);
            return Result(row, r.RequestId, "Create");
        }, ct);

    public Task<NeighborhoodSettlementReceiptDto> TermsAsync(string id, string actor, NeighborhoodSettlementAgreementRequest r, CancellationToken ct)
        => Execute(actor, "Terms", id, r, async () =>
        {
            var row = await Owned(id, actor, ct); Revision(row, r);
            Require(row.SourceKind == r.SourceKind && row.SourceId == r.SourceId, "NeighborhoodSettlementSourceChangeForbidden");
            var source = await sources.ResolveAsync(row.SourceKind, row.SourceId, ct);
            var terms = WorkflowTerms(r);
            Require(Party(source, actor), "NeighborhoodSettlementPartyRequired", 403); ValidateTerms(terms, source); ValidateActivityRewardTerms(terms, source);
            await ValidatePlatformShareTerms(terms, source, ct);
            ValidateInventoryCostSelections(terms);
            row.TermsVersion++; row.Revision++; row.TermsJson = Serialize(terms); row.SourceSnapshotJson = Serialize(source);
            row.ActivityKind = r.ActivityKind; row.ConsentsJson = "[]"; row.StatusCode = NeighborhoodSettlementCodes.AwaitingConsent;
            row.UpdatedAtUtc = Now; AppendHistory(row, actor, "Terms", Now); VersionSnapshot(row);
            return Result(row, r.RequestId, "Terms");
        }, ct);

    public Task<NeighborhoodSettlementReceiptDto> ConsentAsync(string id, string actor, NeighborhoodSettlementConsentRequest r, CancellationToken ct)
        => Execute(actor, "Consent", id, r, async () =>
        {
            var row = await Owned(id, actor, ct); Revision(row, r);
            Require(r.TermsVersion == row.TermsVersion, "NeighborhoodSettlementTermsVersionConflict");
            var required = RequiredConsents(Terms(row)); Require(required.Contains(actor, StringComparer.Ordinal), "NeighborhoodSettlementConsentPartyRequired", 403);
            if (r.Granted) await CurrentSource(row, ct);
            var consents = Deserialize<List<NeighborhoodSettlementConsentDto>>(row.ConsentsJson);
            consents.RemoveAll(x => x.UserId == actor);
            consents.Add(new() { UserId = actor, TermsVersion = row.TermsVersion, Granted = r.Granted, RecordedAtUtc = Now });
            row.ConsentsJson = Serialize(consents);
            row.StatusCode = required.Length > 0 && required.All(user => consents.Any(x => x.UserId == user && x.TermsVersion == row.TermsVersion && x.Granted))
                ? NeighborhoodSettlementCodes.Active : NeighborhoodSettlementCodes.AwaitingConsent;
            row.Revision++; row.UpdatedAtUtc = Now; AppendHistory(row, actor, r.Granted ? "Consent" : "Revoke", Now);
            var version = await db.Set<NeighborhoodSettlementAgreementVersionRecord>().SingleAsync(x => x.AgreementId == id && x.TermsVersion == row.TermsVersion, ct);
            version.ConsentsJson = row.ConsentsJson;
            return Result(row, r.RequestId, "Consent");
        }, ct);

    private async Task<NeighborhoodSettlementSourceSnapshot> CurrentSource(NeighborhoodSettlementAgreementRecord row, CancellationToken ct)
    {
        var current = await sources.ResolveAsync(row.SourceKind, row.SourceId, ct); var frozen = Source(row);
        Require(current.SourceKind == row.SourceKind && current.SourceId == row.SourceId
            && current.SourceVersion == frozen.SourceVersion && current.SourceDigest == frozen.SourceDigest,
            "NeighborhoodSettlementSourceChangedReconfirmationRequired");
        return current;
    }

    private NeighborhoodSettlementReceiptDto Result(NeighborhoodSettlementAgreementRecord row, Guid requestId, string kind,
        IReadOnlyList<string>? entries = null, string? payment = null)
        => new() { RequestId = requestId, AgreementId = row.AgreementId, Kind = kind, Revision = row.Revision,
            StatusCode = row.StatusCode, EntryIds = entries ?? [], PaymentId = payment, RecordedAtUtc = Now };

    private Task<NeighborhoodSettlementReceiptDto> Execute<T>(string actor, string kind, string id, T request,
        Func<Task<NeighborhoodSettlementReceiptDto>> operation, CancellationToken ct) where T : NeighborhoodSettlementCommand
    {
        Gate(actor); Require(request.RequestId != Guid.Empty, "NeighborhoodSettlementRequestIdRequired", 400);
        var digest = Digest(new { Kind = kind, AgreementId = id, Request = request });
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                if (await Replay(actor, request.RequestId, digest, ct) is { } replay) return replay;
                var result = await operation();
                db.Add(new NeighborhoodSettlementCommandReceiptRecord { ActorUserId = actor, RequestId = request.RequestId,
                    AgreementId = result.AgreementId, Kind = kind, InputDigest = digest, ResponseJson = Serialize(result),
                    Revision = result.Revision, RecordedAtUtc = result.RecordedAtUtc });
                await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return result;
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(ct); db.ChangeTracker.Clear();
                throw new NeighborhoodSettlementException("NeighborhoodSettlementRevisionConflict");
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(ct); db.ChangeTracker.Clear();
                if (await Replay(actor, request.RequestId, digest, ct) is { } replay) return replay;
                throw new NeighborhoodSettlementException("NeighborhoodSettlementDuplicateOrConcurrentCommand");
            }
            catch
            { await transaction.RollbackAsync(ct); db.ChangeTracker.Clear(); throw; }
        });
    }
    private async Task<NeighborhoodSettlementReceiptDto?> Replay(string actor, Guid requestId, string digest, CancellationToken ct)
    {
        var row = await db.Set<NeighborhoodSettlementCommandReceiptRecord>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorUserId == actor && x.RequestId == requestId, ct);
        if (row is null) return null;
        Require(row.InputDigest == digest, "NeighborhoodSettlementRequestContentConflict");
        var result = Deserialize<NeighborhoodSettlementReceiptDto>(row.ResponseJson); result.Replayed = true; return result;
    }
    public async Task<NeighborhoodSettlementReceiptDto> ReceiptAsync(string actor, Guid requestId, CancellationToken ct)
    {
        Gate(actor); var row = await db.Set<NeighborhoodSettlementCommandReceiptRecord>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.ActorUserId == actor && x.RequestId == requestId, ct)
            ?? throw new NeighborhoodSettlementException("NeighborhoodSettlementReceiptNotFound", 404);
        return Deserialize<NeighborhoodSettlementReceiptDto>(row.ResponseJson);
    }
}
