using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace RoleWorkspacePreview;

/// <summary>실제 주문·배차·운영 인수가 아닌 격리 표본의 포장 한 개와 픽업 결과입니다.</summary>
public sealed record 살뜰마트표본전표상태(
    string LabelId, string QrCode, string ConfirmationCode, long Revision, int PrintRequestCount,
    bool PickedUp, DateTimeOffset? PickupAtUtc, bool Active);

public sealed partial class SsalddelMartSampleRuntime
{
    public const string SampleDriverId = "sample-mart-driver";
    private readonly TimeProvider _pickupClock = timeProvider ?? TimeProvider.System;
    private 살뜰마트표본픽업DbContext? _pickupDb;
    public int PickupCount { get; private set; }

    public Task<bool> IssueSlipAsync(string orderId, CancellationToken cancellationToken = default)
        => ExecuteAsync(async () =>
        {
            if (!Ready()) return false;
            await RefreshAsync(cancellationToken);
            if (!Orders.Any(order => order.SampleOrderId == orderId)) return Fail("선택한 표본 주문을 찾을 수 없습니다.");
            if (!await _pickupDb!.Slips.AnyAsync(slip => slip.OrderId == orderId, cancellationToken))
            {
                _pickupDb.Slips.Add(NewSlip(orderId, 1));
                try { await _pickupDb.SaveChangesAsync(cancellationToken); }
                finally { _pickupDb.ChangeTracker.Clear(); }
            }
            await RefreshAsync(cancellationToken);
            return true;
        }, cancellationToken);

    public Task<bool> ReissueSlipAsync(string orderId, CancellationToken cancellationToken = default)
        => ExecuteAsync(async () =>
        {
            if (!Ready()) return false;
            await RefreshAsync(cancellationToken);
            var order = Orders.SingleOrDefault(candidate => candidate.SampleOrderId == orderId);
            if (order is null) return Fail("선택한 표본 주문을 찾을 수 없습니다.");
            if (order.Lines.Any(line => line.Packed) || order.Slip is { PickedUp: true }) return Fail("포장 전의 전표만 재발급할 수 있습니다.");
            var slip = await _pickupDb!.Slips.SingleOrDefaultAsync(candidate => candidate.OrderId == orderId, cancellationToken);
            if (slip is null) return Fail("픽업용 전표를 먼저 준비해 주세요.");
            try
            {
                if (slip.Revision >= 999999) return Fail("전표 판본 한도에 도달했습니다. 표본을 초기화해 주세요.");
                var next = NewSlip(orderId, checked(slip.Revision + 1), slip.ConfirmationCode);
                slip.LabelId = next.LabelId; slip.QrCode = next.QrCode; slip.ConfirmationCode = next.ConfirmationCode;
                slip.Revision = next.Revision; slip.Active = true; slip.PrintRequestCount = 0;
                slip.FailedNumberAttempts = 0; slip.NumberBlockedUntilUtc = null;
                await _pickupDb.SaveChangesAsync(cancellationToken);
            }
            finally { _pickupDb.ChangeTracker.Clear(); }
            await RefreshAsync(cancellationToken);
            return true;
        }, cancellationToken);

    /// <summary>인쇄 창을 요청한 기록입니다. 실물 인쇄 성공이나 포장·픽업 완료를 뜻하지 않습니다.</summary>
    public Task<bool> RecordPrintRequestAsync(string orderId, string labelId, long revision,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(async () =>
        {
            if (!Ready()) return false;
            var slip = await _pickupDb!.Slips.SingleOrDefaultAsync(candidate => candidate.OrderId == orderId, cancellationToken);
            try
            {
                if (slip is null || !slip.Active || slip.LabelId != labelId || slip.Revision != revision)
                    return Fail("현재 유효한 전표를 다시 확인해 주세요.");
                slip.PrintRequestCount = checked(slip.PrintRequestCount + 1);
                await _pickupDb.SaveChangesAsync(cancellationToken);
            }
            finally { _pickupDb.ChangeTracker.Clear(); }
            await RefreshAsync(cancellationToken);
            return true;
        }, cancellationToken);

    public Task<bool> PickupByQrAsync(string orderId, string rawCode, string requestId, long expectedRevision,
        string driverId, CancellationToken cancellationToken = default)
        => PickupAsync(orderId, rawCode, requestId, expectedRevision, driverId, byNumber: false, cancellationToken);

    public Task<bool> PickupByNumberAsync(string orderId, string rawCode, string requestId, long expectedRevision,
        string driverId, CancellationToken cancellationToken = default)
        => PickupAsync(orderId, rawCode, requestId, expectedRevision, driverId, byNumber: true, cancellationToken);

    private Task<bool> PickupAsync(string orderId, string rawCode, string requestId, long expectedRevision,
        string driverId, bool byNumber, CancellationToken cancellationToken)
        => ExecuteAsync(async () =>
        {
            if (!Ready()) return false;
            // 요청 경계를 먼저 제한합니다. 원문 코드·입력은 오류/로그/요청 결과에 기록하지 않습니다.
            if (!Guid.TryParse(requestId, out var requestGuid) || requestGuid == Guid.Empty
                || orderId is null || orderId.Length is < 1 or > 80 || rawCode is null || rawCode.Length > 80
                || driverId is null || driverId.Length is < 1 or > 80)
                return Fail("픽업 요청 형식을 확인해 주세요.");
            var normalizedRequestId = requestGuid.ToString("N");
            var inputHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                new { orderId, rawCode, expectedRevision, driverId, byNumber }))));
            var db = _pickupDb!;
            try
            {
                var previous = await db.Commands.AsNoTracking().SingleOrDefaultAsync(
                    command => command.RequestId == normalizedRequestId, cancellationToken);
                if (previous is not null)
                {
                    if (!string.Equals(previous.InputHash, inputHash, StringComparison.Ordinal))
                        return Fail("같은 요청 번호의 입력이 달라 처리하지 않았습니다.");
                    await RefreshAsync(cancellationToken);
                    return previous.Success || Fail(PickupError(previous.ResultCode));
                }
                await RefreshAsync(cancellationToken);
                var order = Orders.SingleOrDefault(candidate => candidate.SampleOrderId == orderId);
                var slip = await db.Slips.SingleOrDefaultAsync(candidate => candidate.OrderId == orderId, cancellationToken);
                var code = "Completed";
                var now = _pickupClock.GetUtcNow();
                if (order is null) code = "OrderUnavailable";
                else if (!string.Equals(driverId, SampleDriverId, StringComparison.Ordinal)) code = "DriverMismatch";
                else if (slip is null || !slip.Active) code = "LabelUnavailable";
                else if (expectedRevision != slip.Revision) code = "RevisionMismatch";
                else if (!order.AllPicked || !order.AllPacked) code = "PackageNotReady";
                else if (byNumber)
                {
                    if (slip.NumberBlockedUntilUtc is { } blocked && blocked > now) code = "NumberThrottled";
                    else
                    {
                        if (slip.NumberBlockedUntilUtc is not null)
                        { slip.FailedNumberAttempts = 0; slip.NumberBlockedUntilUtc = null; }
                        if (!Regex.IsMatch(rawCode, "^[0-9]{4}$", RegexOptions.CultureInvariant)
                            || !string.Equals(rawCode, slip.ConfirmationCode, StringComparison.Ordinal))
                        {
                            code = "NumberMismatch";
                            slip.FailedNumberAttempts++;
                            if (slip.FailedNumberAttempts >= 5) slip.NumberBlockedUntilUtc = now.AddSeconds(60);
                        }
                    }
                }
                else if (!Regex.IsMatch(rawCode, "^SAMPLE-PKG-[0-9a-f]{32}-[1-9][0-9]{0,5}$", RegexOptions.CultureInvariant)
                    || !string.Equals(rawCode, slip.QrCode, StringComparison.Ordinal)) code = "QrMismatch";

                var success = code == "Completed";
                if (success)
                {
                    // 포장 완료만을 검증합니다. 운영 출고·배차·재고 차감·배송/지급 Event는 발생시키지 않습니다.
                    if (!await db.Receipts.AnyAsync(receipt => receipt.OrderId == orderId, cancellationToken))
                    {
                        db.Receipts.Add(new 살뜰마트표본픽업확정Entity
                        {
                            OrderId = orderId, LabelId = slip!.LabelId, Revision = slip.Revision,
                            RequestId = normalizedRequestId, DriverId = SampleDriverId, PickupAtUtc = now
                        });
                        slip.PickupAtUtc = now;
                    }
                    slip!.FailedNumberAttempts = 0; slip.NumberBlockedUntilUtc = null;
                }
                db.Commands.Add(new 살뜰마트표본픽업요청Entity
                {
                    RequestId = normalizedRequestId, InputHash = inputHash, OrderId = orderId,
                    Success = success, ResultCode = code
                });
                // 전표·요청 결과·한 주문 한 인수 기록을 동일 SQLite transaction에서 확정합니다.
                await db.SaveChangesAsync(cancellationToken);
                await RefreshAsync(cancellationToken);
                return success || Fail(PickupError(code));
            }
            finally { db.ChangeTracker.Clear(); }
        }, cancellationToken);

    private async Task InitializePickupStoreAsync(CancellationToken cancellationToken)
    {
        _pickupDb = new 살뜰마트표본픽업DbContext(new DbContextOptionsBuilder<살뜰마트표본픽업DbContext>()
            .UseSqlite(_connection!).Options);
        // EnsureCreated는 같은 연결의 기존 업무 테이블을 발견하면 새 model을 만들지 않으므로,
        // 고유한 표본 전용 테이블만 생성합니다. 운영 DbContext/Entity/Migration은 수정하지 않습니다.
        await _pickupDb.Database.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(cancellationToken);
    }

    private async Task<Dictionary<string, 살뜰마트표본전표상태>> ReadPickupSlipsAsync(CancellationToken cancellationToken)
    {
        if (_pickupDb is null) return new(StringComparer.Ordinal);
        var slips = await _pickupDb.Slips.AsNoTracking().ToArrayAsync(cancellationToken);
        var receipts = await _pickupDb.Receipts.AsNoTracking().ToDictionaryAsync(receipt => receipt.OrderId, cancellationToken);
        PickupCount = receipts.Count;
        return slips.ToDictionary(slip => slip.OrderId, slip =>
        {
            receipts.TryGetValue(slip.OrderId, out var receipt);
            var confirmed = receipt is not null && receipt.LabelId == slip.LabelId && receipt.Revision == slip.Revision;
            return new 살뜰마트표본전표상태(slip.LabelId, slip.QrCode, slip.ConfirmationCode, slip.Revision,
                slip.PrintRequestCount, confirmed, confirmed ? receipt!.PickupAtUtc : null, slip.Active);
        }, StringComparer.Ordinal);
    }

    private static 살뜰마트표본전표Entity NewSlip(string orderId, long revision, string? previousNumber = null)
    {
        var labelId = Guid.NewGuid().ToString("N");
        string number;
        do { number = RandomNumberGenerator.GetInt32(10_000).ToString("D4", CultureInfo.InvariantCulture); }
        while (string.Equals(number, previousNumber, StringComparison.Ordinal));
        return new()
        {
            OrderId = orderId, LabelId = labelId, Revision = revision, Active = true,
            QrCode = $"SAMPLE-PKG-{labelId}-{revision.ToString(CultureInfo.InvariantCulture)}",
            ConfirmationCode = number
        };
    }

    private static string PickupError(string code) => code switch
    {
        "OrderUnavailable" => "선택한 표본 주문을 찾을 수 없습니다.",
        "DriverMismatch" => "이 표본 주문에 배정된 합성 기사만 픽업할 수 있습니다.",
        "LabelUnavailable" => "픽업용 전표를 먼저 준비해 주세요.",
        "RevisionMismatch" => "전표 판본이 바뀌었습니다. 현재 전표를 다시 확인해 주세요.",
        "PackageNotReady" => "주문의 모든 상품 피킹·포장을 완료한 뒤 픽업할 수 있습니다.",
        "NumberThrottled" => "번호 확인을 여러 번 실패했습니다. 잠시 뒤 다시 시도하거나 QR을 스캔해 주세요.",
        "NumberMismatch" => "현재 주문 전표의 네 자리 번호를 확인해 주세요.",
        _ => "현재 주문의 유효한 픽업용 QR을 스캔해 주세요."
    };

    private sealed class 살뜰마트표본픽업DbContext(DbContextOptions<살뜰마트표본픽업DbContext> options) : DbContext(options)
    {
        public DbSet<살뜰마트표본전표Entity> Slips => Set<살뜰마트표본전표Entity>();
        public DbSet<살뜰마트표본픽업요청Entity> Commands => Set<살뜰마트표본픽업요청Entity>();
        public DbSet<살뜰마트표본픽업확정Entity> Receipts => Set<살뜰마트표본픽업확정Entity>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<살뜰마트표본전표Entity>().ToTable("SampleMartPackageSlips").HasKey(row => row.OrderId);
            modelBuilder.Entity<살뜰마트표본전표Entity>().HasIndex(row => row.LabelId).IsUnique();
            modelBuilder.Entity<살뜰마트표본픽업요청Entity>().ToTable("SampleMartPickupCommands").HasKey(row => row.RequestId);
            modelBuilder.Entity<살뜰마트표본픽업확정Entity>().ToTable("SampleMartPickupReceipts").HasKey(row => row.OrderId);
        }
    }

    private sealed class 살뜰마트표본전표Entity
    {
        public string OrderId { get; set; } = "";
        public string LabelId { get; set; } = "";
        public string QrCode { get; set; } = "";
        public string ConfirmationCode { get; set; } = "";
        public long Revision { get; set; }
        public int PrintRequestCount { get; set; }
        public bool Active { get; set; }
        public DateTimeOffset? PickupAtUtc { get; set; }
        public int FailedNumberAttempts { get; set; }
        public DateTimeOffset? NumberBlockedUntilUtc { get; set; }
    }

    private sealed class 살뜰마트표본픽업요청Entity
    {
        public string RequestId { get; set; } = "";
        public string InputHash { get; set; } = "";
        public string OrderId { get; set; } = "";
        public bool Success { get; set; }
        public string ResultCode { get; set; } = "";
    }

    private sealed class 살뜰마트표본픽업확정Entity
    {
        public string OrderId { get; set; } = "";
        public string LabelId { get; set; } = "";
        public long Revision { get; set; }
        public string RequestId { get; set; } = "";
        public string DriverId { get; set; } = "";
        public DateTimeOffset PickupAtUtc { get; set; }
    }
}
