using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Domain.Community;
using Ssalddel.Infrastructure.BackgroundJobs;
using Ssalddel.Services.Commerce;
using Ssalddel.Services.Privacy;
using Ssalddel.Services.Storage;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.화주;

namespace Ssalddel.Services.Community;

public sealed class NeighborhoodMemberDeliveryOptions
{
    public const string SectionName = "NeighborhoodMemberDelivery";
    public bool Enabled { get; set; }
    public bool OperationalEnabled { get; set; }
}

public sealed class NeighborhoodMemberDeliveryException(string code, string text, int status = 409) : Exception(text)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = status;
}

/// <summary>일반 회원의 동의와 한 건 수행만 소유한다. 기사 역할·ID·식품 원장·수금 완료를 만들지 않는다.</summary>
public sealed partial class NeighborhoodMemberDeliveryUseCase(
    SsalddelContext db, ICurrentUserAccessor user, IOptions<NeighborhoodMemberDeliveryOptions> options,
    ISsalddelExecutionModePolicy execution, TimeProvider clock, IPersonalDataEncryptionService protector,
    I신청개인정보동의증적Service consent, I배차추천경로Service routes, IObjectStorageService storage,
    I판매자확인Store? identities = null, Ssalddel.Services.PrivacySupport.보호지원UseCase? support = null,
    ISsalddelBackgroundJobActivationPolicy? activation = null, IOptions<SsalddelExecutionOptions>? executionOptions = null,
    I운송원장Mongo동기화Service? ledgerSync = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private IQueryable<NeighborhoodMemberParticipation> Participants => db.Set<NeighborhoodMemberParticipation>();
    private IQueryable<NeighborhoodMemberDelivery> Deliveries => db.Set<NeighborhoodMemberDelivery>();
    private IQueryable<NeighborhoodMemberOffer> Offers => db.Set<NeighborhoodMemberOffer>();
    private IQueryable<NeighborhoodMemberReceipt> Receipts => db.Set<NeighborhoodMemberReceipt>();
    private IQueryable<NeighborhoodMemberPhoto> Photos => db.Set<NeighborhoodMemberPhoto>();
    public bool Enabled => options.Value.Enabled && executionOptions?.Value.DevelopmentReadOnly != true
        && (execution.IsSimulation || execution.IsOperational && options.Value.OperationalEnabled
            && activation?.Evaluate(SsalddelBackgroundWorkloadKeys.DomesticTransportDispatch).IsEnabled == true);
    private static void Require(bool ok, string code, string text, int status = 409)
    { if (!ok) throw new NeighborhoodMemberDeliveryException(code, text, status); }
    private string Actor()
    {
        Require(!string.IsNullOrWhiteSpace(user.UserId), "AuthenticationRequired", "로그인이 필요합니다.", 401);
        return user.UserId!;
    }
    private void Gate()
    {
        Require(Enabled, "MemberDeliveryDisabled", "일반 회원 배송 제안은 아직 운영에서 열리지 않았습니다.", 404);
        Require(db.Database.IsRelational(), "MemberDeliveryRelationalStorageRequired", "영속 배송 저장소를 확인할 수 없습니다.", 503);
    }
    private async Task AdultAsync(string actor, CancellationToken ct)
    {
        // Simulation은 업무 검증만 허용한다. 실운영은 기존 서버 인증기관의 유효한 성년 증거가 필요하다.
        if (execution.IsSimulation) return;
        var proof = identities is null ? null : (await identities.조회Async(actor, ct))?.Verification;
        Require(proof is not null && proof.ActorUserId == actor && proof.IsAdult && proof.PhoneVerified
            && (proof.EmailVerified || proof.DesignatedIdentityVerified) && proof.VerifiedAtUtc.Kind == DateTimeKind.Utc
            && proof.ExpiresAtUtc.Kind == DateTimeKind.Utc && proof.VerifiedAtUtc != default
            && proof.VerifiedAtUtc <= Now && proof.ExpiresAtUtc > Now,
            "MemberAdultIdentityRequired", "배송 참여에는 서버에서 확인한 유효한 성년 신원이 필요합니다.");
    }
    private string Seal<T>(T value) => protector.Protect(JsonSerializer.Serialize(value, Json))
        ?? throw new NeighborhoodMemberDeliveryException("MemberPrivateStorageUnavailable", "개인 정보 보호 저장을 확인할 수 없습니다.", 503);
    private T Open<T>(string payload)
    {
        try { return JsonSerializer.Deserialize<T>(protector.Unprotect(payload)!, Json)
            ?? throw new NeighborhoodMemberDeliveryException("MemberPrivatePayloadInvalid", "저장된 개인 정보를 다시 확인해 주세요.", 503); }
        catch (Exception ex) when (ex is JsonException or ArgumentException or CryptographicException)
        { throw new NeighborhoodMemberDeliveryException("MemberPrivatePayloadInvalid", "보호된 개인 정보를 확인할 수 없습니다.", 503); }
    }
    private static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));
    private static string Stable(string prefix, string value) => prefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..48];
    private static bool Coordinates(decimal? lat, decimal? lng) => lat is >= -90 and <= 90 && lng is >= -180 and <= 180;
    public static decimal DistanceKm(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        var r = Math.PI / 180; var a = (double)(lat2 - lat1) * r; var b = (double)(lon2 - lon1) * r;
        var h = Math.Pow(Math.Sin(a / 2), 2) + Math.Cos((double)lat1 * r) * Math.Cos((double)lat2 * r) * Math.Pow(Math.Sin(b / 2), 2);
        return (decimal)(6371 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(h, 0, 1))));
    }
    private static string RouteOption(string vehicle) => vehicle switch
    { "오토바이" => "traavoidcar", "자동차" or "승용차" => "trafast", _ => "" };
    private sealed record ActivityPoint(string Label, decimal? Latitude, decimal? Longitude, decimal RadiusKm, string VehicleType, decimal MaxWeightKg);
    private async Task<bool> ReplayAsync(string actor, Guid id, string kind, string aggregate, string digest, CancellationToken ct)
    {
        Require(id != Guid.Empty, "MemberRequestIdRequired", "요청 번호가 필요합니다.", 400);
        var prior = await Receipts.AsNoTracking().SingleOrDefaultAsync(x => x.ActorUserId == actor && x.ClientRequestId == id, ct);
        if (prior is null) return false;
        Require(prior.Kind == kind && prior.AggregateId == aggregate && prior.InputDigest == digest,
            "IdempotencyConflict", "같은 요청 번호로 다른 내용을 보낼 수 없습니다.");
        return true;
    }
    private void Receipt(string actor, Guid id, string kind, string aggregate, string digest)
        => db.Add(new NeighborhoodMemberReceipt { ActorUserId = actor, ClientRequestId = id, Kind = kind,
            AggregateId = aggregate, InputDigest = digest, RecordedAtUtc = Now });
    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new NeighborhoodMemberDeliveryException("MemberRevisionConflict", "동시 변경이 먼저 저장되었습니다. 같은 요청으로 결과를 다시 확인해 주세요."); }
    }
    public async Task<NeighborhoodMemberParticipationResponse> ParticipationAsync(CancellationToken ct = default)
    {
        var row = await Participants.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == Actor(), ct);
        var point = row is null ? new ActivityPoint("", null, null, 3, "오토바이", 10) : Open<ActivityPoint>(row.PrivatePayload);
        return new() { Revision = row?.Revision ?? 0, ReceiveOffers = row?.ReceiveOffers ?? false,
            ActivityLabel = point.Label, Latitude = point.Latitude, Longitude = point.Longitude, RadiusKm = point.RadiusKm,
            VehicleType = point.VehicleType, MaxWeightKg = point.MaxWeightKg, Enabled = Enabled,
            BlockCode = Enabled ? "" : "MemberDeliveryDisabled" };
    }
    public async Task<NeighborhoodMemberParticipationResponse> SaveParticipationAsync(NeighborhoodMemberParticipationSaveRequest request, CancellationToken ct = default)
    {
        var actor = Actor(); var digest = Digest(request);
        Require(request.ExpectedRevision >= 0 && request.ActivityLabel is not null && request.ActivityLabel.Length <= 80 && request.RadiusKm is >= .1m and <= 10m
            && request.MaxWeightKg is > 0 and <= 1000 && RouteOption(request.VehicleType) != "",
            "MemberParticipationInvalid", "활동 이름·반경·이동 수단·운반 한도를 확인해 주세요.", 400);
        if (request.ReceiveOffers) { Gate(); Require(Coordinates(request.Latitude, request.Longitude), "MemberActivityPointRequired", "활동 위치를 선택해 주세요.", 400); await AdultAsync(actor, ct); }
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        if (await ReplayAsync(actor, request.ClientRequestId, "Participation", actor, digest, ct)) return await ParticipationAsync(ct);
        var row = await Participants.SingleOrDefaultAsync(x => x.UserId == actor, ct);
        Require((row?.Revision ?? 0) == request.ExpectedRevision, "MemberRevisionConflict", "참여 설정이 바뀌었습니다. 다시 확인해 주세요.");
        if (row is null) { row = new() { UserId = actor }; db.Add(row); }
        row.ReceiveOffers = request.ReceiveOffers; row.Revision++; row.UpdatedAtUtc = Now;
        row.PrivatePayload = Seal(new ActivityPoint(request.ActivityLabel.Trim(), request.Latitude, request.Longitude,
            request.RadiusKm, request.VehicleType, request.MaxWeightKg));
        Receipt(actor, request.ClientRequestId, "Participation", actor, digest); await SaveAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct); return await ParticipationAsync(ct);
    }
    private async Task RequireApplicationConsentAsync(NeighborhoodMemberDelivery row, CancellationToken ct)
    {
        Require(row.NoticeVersion == NeighborhoodMemberDeliveryRoutes.NoticeVersion, "MemberDisclosureChanged", "배송 정보 제공 동의를 다시 확인해 주세요.");
        try { await consent.유효한동의요구Async(row.ConsentEvidenceId, Ssalddel.Contracts.Common.Privacy.신청개인정보업무Codes.운송대행,
            NeighborhoodDeliveryRoutes.PrivacyConsentSourceCode, row.OwnerUserId, ct); }
        catch (InvalidOperationException) { throw new NeighborhoodMemberDeliveryException("PrivacyConsentRequired", "배송 신청의 개인정보 동의가 만료되거나 철회되었습니다."); }
    }
    private static string Area(string address) => "인근 지역";
    private async Task<화주운송의뢰> SourceAsync(string id, CancellationToken ct)
    {
        var source = await db.화주운송의뢰.SingleOrDefaultAsync(x => x.의뢰Id == id, ct);
        Require(source is not null && source.클라이언트요청Id.StartsWith(NeighborhoodDeliveryRoutes.ClientRequestPrefix, StringComparison.Ordinal),
            "MemberDeliveryNotFound", "생활 배송을 찾을 수 없습니다.", 404); return source!;
    }
    public async Task SyncLedgerAsync(string id, string actor, CancellationToken ct)
    {
        if (ledgerSync is null) return;
        var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == id && x.일반회원배송전용, ct);
        if (queue is not null) await ledgerSync.운송실행투영동기화Async(queue, actor, ct);
    }
}
