using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Services.Operations;
using 살뜰.Data;
using 살뜰.도메인.운송;

namespace Ssalddel.Application.Driver.Transport;

public sealed class CargoCompletionRecoveryOptions
{
    public const string SectionName = "CargoCompletionRecovery";
    public bool Enabled { get; set; }
    public int BatchSize { get; set; } = 20;
    public int IntervalSeconds { get; set; } = 15;
    public int MaximumAttempts { get; set; } = 5;
    public int LeaseSeconds { get; set; } = 120;
    public int RetrySeconds { get; set; } = 30;
}

internal sealed class CargoCompletionRecoveryJob
{
    public int Version { get; set; } = 1;
    public long TransportId { get; set; }
    public string ActorId { get; set; } = "";
    public string ProofDigest { get; set; } = "";
    public long CompletedAtUtcTicks { get; set; }
    public string State { get; set; } = "Pending";
    public int Attempts { get; set; }
    public int ManualRetries { get; set; }
    public int Revision { get; set; }
    public string? LeaseToken { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public bool HandoffPrepared { get; set; }
    public bool PaymentPreparationDone { get; set; }
    public string ReasonCode { get; set; } = "";
}

public static class CargoCompletionRecoveryMarker
{
    internal const string Key = "cargoCloseoutR69";
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static JsonNode? Create(운송원장 transport, DateTime now)
    {
        var digest = 화물운송사진증빙Policy.CompletedPhotoDigest(transport);
        return digest is null || transport.도착 is null ? null : JsonSerializer.SerializeToNode(new CargoCompletionRecoveryJob
        { TransportId = transport.Id, ActorId = transport.기사_운송자, ProofDigest = digest,
            CompletedAtUtcTicks = UtcMicrosecondTicks(transport.도착.Value), NextAttemptAtUtc = now }, Json);
    }
    public static long UtcMicrosecondTicks(DateTime value)
    { var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return utc.Ticks / 10 * 10; }
    internal static CargoCompletionRecoveryJob? Read(string metadata)
    {
        try { return JsonNode.Parse(metadata)?[Key]?.Deserialize<CargoCompletionRecoveryJob>(Json) is { Version: 1 } job ? job : null; }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { return null; }
    }
    internal static string Write(string metadata, CargoCompletionRecoveryJob job)
    { var root = JsonNode.Parse(metadata) as JsonObject ?? throw new InvalidOperationException("CargoRecoveryMetadataInvalid");
        root[Key] = JsonSerializer.SerializeToNode(job, Json); return root.ToJsonString(); }
}

public interface ICargoCompletionRecoveryService
{
    Task<bool> ProcessTransportAsync(long transportId, CancellationToken ct = default);
    Task<int> ProcessPendingAsync(int take = 20, CancellationToken ct = default);
    Task<bool> RetryAsync(long eventId, int expectedAttempts, CancellationToken ct = default);
}

/// <summary>r69부터 원자 저장된 완료 이벤트만 재처리한다. 후속 인계·입금 준비는 실수납·송금이 아니다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalLogisticsOs, SsalddelCodeLayer.Application,
    "신규 화물 완료 이벤트에 결속한 화주 인계와 입금 준비를 영속 CAS·lease로 재개한다.",
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    FlowOrder = 80, StepKey = "application.cargo-completion-durable-followups",
    ExecutionStage = SsalddelCodeExecutionStage.Persistence,
    ReadsFrom = SsalddelCodeDataScope.OperationalState, WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "r69 서버 사진 근거가 있는 신규 완료 이벤트만 처리한다. 과거 재생·운영 활성화·실수납·송금은 실행하지 않는다. worker 기본 OFF.")]
public sealed class CargoCompletionRecoveryService(SsalddelContext db,
    I화물운송완료화주인수인계Service handoff, I운송완료입금요청Service payment,
    IOptions<CargoCompletionRecoveryOptions> options, TimeProvider clock,
    ILogger<CargoCompletionRecoveryService> logger) : ICargoCompletionRecoveryService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    public async Task<bool> ProcessTransportAsync(long transportId, CancellationToken ct = default)
    {
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is not null) return false;
        var transport = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.Id == transportId, ct);
        if (transport is null) return false;
        var requestId = string.IsNullOrWhiteSpace(transport.의뢰Id) ? transport.운송번호 : transport.의뢰Id;
        var rows = await Events.Where(x => x.의뢰Id == requestId && x.메타데이터.Contains("\"cargoCloseoutR69\""))
            .OrderByDescending(x => x.Id).ToArrayAsync(ct);
        var row = rows.FirstOrDefault(x => CargoCompletionRecoveryMarker.Read(x.메타데이터)?.TransportId == transportId);
        return row is not null && await ProcessAsync(row, ct);
    }

    public async Task<int> ProcessPendingAsync(int take = 20, CancellationToken ct = default)
    {
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is not null) return 0;
        var query = Events.Where(x => x.메타데이터.Contains("\"cargoCloseoutR69\"")
            && (x.메타데이터.Contains("\"state\":\"Pending\"") || x.메타데이터.Contains("\"state\":\"Retry\"")
                || x.메타데이터.Contains("\"state\":\"Processing\"")));
        var wanted = Math.Clamp(take, 1, 100); var pageSize = Math.Clamp(wanted * 2, 20, 200);
        var ceiling = await query.Select(x => (long?)x.Id).MaxAsync(ct) ?? 0;
        long after = 0; var processed = 0;
        // 이번 조회 시작 시점의 끝 ID까지만 작은 페이지로 순회한다. 미래/유효 lease 행은 업무 처리 개수에 넣지 않는다.
        while (processed < wanted && after < ceiling)
        {
            ct.ThrowIfCancellationRequested();
            var rows = await query.Where(x => x.Id > after && x.Id <= ceiling).OrderBy(x => x.Id).Take(pageSize).ToArrayAsync(ct);
            if (rows.Length == 0) break;
            foreach (var row in rows)
            { after = row.Id; if (await ProcessAsync(row, ct)) processed++; if (processed >= wanted) break; }
        }
        return processed;
    }

    public async Task<bool> RetryAsync(long eventId, int expectedAttempts, CancellationToken ct = default)
    {
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is not null) return false;
        var row = await Events.SingleOrDefaultAsync(x => x.Id == eventId, ct);
        var job = row is null ? null : CargoCompletionRecoveryMarker.Read(row.메타데이터);
        if (row is null || job is null || job.Attempts != expectedAttempts || job.State is not ("Failed" or "Blocked")) return false;
        job.State = "Retry"; job.ManualRetries++; job.Attempts = 0; job.Revision++;
        job.LeaseToken = null; job.LeaseUntilUtc = null; job.NextAttemptAtUtc = Now; job.ReasonCode = "ExplicitRetryRequested";
        return await CompareExchange(row.Id, row.메타데이터, CargoCompletionRecoveryMarker.Write(row.메타데이터, job), ct) == 1;
    }

    private IQueryable<운송이벤트> Events => db.운송이벤트.AsNoTracking().Where(x => x.이벤트타입 == 운송이벤트유형.기사운송상태변경);
    private bool Due(CargoCompletionRecoveryJob job) =>
        (job.Attempts < Math.Clamp(options.Value.MaximumAttempts, 1, 10)
            && (job.State is "Pending" or "Retry") && (job.NextAttemptAtUtc is null || job.NextAttemptAtUtc <= Now))
        || (job.State == "Processing" && job.LeaseUntilUtc <= Now);

    private async Task<bool> ProcessAsync(운송이벤트 row, CancellationToken ct)
    {
        var job = CargoCompletionRecoveryMarker.Read(row.메타데이터);
        if (job is null || job.TransportId <= 0 || job.ActorId.Length == 0 || job.ProofDigest.Length != 64 || !Due(job)) return false;
        if (job.Attempts >= Math.Clamp(options.Value.MaximumAttempts, 1, 10))
        {
            job.State = "Failed"; job.ReasonCode = "CargoRecoveryAttemptsExhausted"; job.Revision++;
            job.LeaseToken = null; job.LeaseUntilUtc = null;
            return await CompareExchange(row.Id, row.메타데이터, CargoCompletionRecoveryMarker.Write(row.메타데이터, job), ct) == 1;
        }
        job.State = "Processing"; job.Attempts++; job.Revision++; job.LeaseToken = Guid.NewGuid().ToString("N");
        job.LeaseUntilUtc = Now.AddSeconds(Math.Clamp(options.Value.LeaseSeconds, 30, 600)); job.ReasonCode = "";
        var claimed = CargoCompletionRecoveryMarker.Write(row.메타데이터, job);
        if (await CompareExchange(row.Id, row.메타데이터, claimed, ct) != 1) return false;
        var phase = "CargoHandoffPreparationFailed";
        try
        {
            var attempt = 0;
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                if (attempt++ > 0) db.ChangeTracker.Clear();
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                // 같은 이벤트 행의 CAS 잠금을 완료 저장까지 유지해 lease 만료 후의 중복 처리를 막는다.
                job.Revision++;
                var locked = CargoCompletionRecoveryMarker.Write(claimed, job);
                if (await CompareExchange(row.Id, claimed, locked, ct) != 1) throw new InvalidOperationException("CargoRecoveryLeaseLost");
                var transport = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.Id == job.TransportId, ct);
                var requestId = transport is null ? "" : string.IsNullOrWhiteSpace(transport.의뢰Id) ? transport.운송번호 : transport.의뢰Id;
                if (transport is null || requestId != row.의뢰Id || transport.기사_운송자 != job.ActorId
                    || transport.상태 != 기사운송상태코드.인수완료 || transport.도착 is null
                    || CargoCompletionRecoveryMarker.UtcMicrosecondTicks(transport.도착.Value) != job.CompletedAtUtcTicks
                    || 화물운송사진증빙Policy.CompletedPhotoDigest(transport) != job.ProofDigest)
                { job.State = "Blocked"; job.ReasonCode = "CargoCompletionEvidenceChanged"; }
                else
                {
                    var result = await handoff.완료결과요청Async(transport.Id, true, ct);
                    if (result.상태Code is not (운영체제업무인계상태Codes.요청됨 or 운영체제업무인계상태Codes.수락됨))
                    { job.State = "Blocked"; job.ReasonCode = "CargoHandoffReviewRequired"; }
                    else
                    {
                        job.HandoffPrepared = true; phase = "CargoPaymentPreparationFailed";
                        var deposit = await payment.준비Async(new(transport.Id, requestId, transport.기사_운송자,
                            "", "", transport.상태, transport.도착!.Value, "cargo-closeout-r69", null), ct);
                        var knownSkip = deposit.사유 is "이미 결제완료된 의뢰입니다." or "이미 입금 요청 또는 정산 처리된 의뢰입니다."
                            or "운송완료후정산 대상이 아닙니다." or "플랫폼 수납 대상이 아닙니다.";
                        if (!deposit.처리됨 && !knownSkip)
                        { job.State = "Blocked"; job.ReasonCode = "CargoPaymentPreparationUnconfirmed"; }
                        else { job.PaymentPreparationDone = true; job.State = "Done";
                            job.ReasonCode = deposit.처리됨 ? "FollowupsPrepared" : "PaymentPreparationNotRequiredOrAlreadyPrepared"; }
                    }
                }
                job.Revision++; job.LeaseUntilUtc = null; job.LeaseToken = null; job.NextAttemptAtUtc = null;
                if (await CompareExchange(row.Id, locked, CargoCompletionRecoveryMarker.Write(locked, job), ct) != 1)
                    throw new InvalidOperationException("CargoRecoveryLeaseLost");
                await tx.CommitAsync(ct);
            });
        }
        catch (OperationCanceledException) { throw; } // 커밋 전 중단은 lease 만료 후 같은 이벤트로 복구한다.
        catch (Exception)
        {
            db.ChangeTracker.Clear();
            job.HandoffPrepared = false; job.PaymentPreparationDone = false;
            job.State = job.Attempts >= Math.Clamp(options.Value.MaximumAttempts, 1, 10) ? "Failed" : "Retry";
            job.ReasonCode = phase; job.Revision++; job.LeaseToken = null; job.LeaseUntilUtc = null;
            job.NextAttemptAtUtc = job.State == "Retry" ? Now.AddSeconds(Math.Clamp(options.Value.RetrySeconds, 1, 3600)) : null;
            if (await CompareExchange(row.Id, claimed, CargoCompletionRecoveryMarker.Write(claimed, job), ct) == 1)
                logger.LogWarning("Cargo completion follow-up pending. EventId={EventId} ReasonCode={ReasonCode} Attempts={Attempts}", row.Id, phase, job.Attempts);
        }
        return true;
    }

    private Task<int> CompareExchange(long id, string expected, string value, CancellationToken ct)
        => db.운송이벤트.Where(x => x.Id == id && x.이벤트타입 == 운송이벤트유형.기사운송상태변경 && x.메타데이터 == expected)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.메타데이터, value), ct);
}

public sealed class CargoCompletionRecoveryWorker(IServiceScopeFactory scopes, IOptions<CargoCompletionRecoveryOptions> options,
    ILogger<CargoCompletionRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Clamp(options.Value.IntervalSeconds, 5, 300)));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<ICargoCompletionRecoveryService>()
                    .ProcessPendingAsync(options.Value.BatchSize, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Cargo completion recovery sweep failed; persisted jobs remain pending."); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }
}
