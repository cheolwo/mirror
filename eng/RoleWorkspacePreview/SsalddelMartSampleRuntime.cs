using FluentResults;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.ViewSettings;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Audit;
using 살뜰.도메인.마트;
using 살뜰.도메인.창고;

namespace RoleWorkspacePreview;

public sealed record 살뜰마트표본품목상태(
    string LineId, string ProductName, int Quantity, string Sku, string RackCode,
    string PickingStatus, bool Packed);

public sealed record 살뜰마트표본주문상태(
    string SampleOrderId, string DisplayLabel, IReadOnlyList<살뜰마트표본품목상태> Lines,
    bool AllPicked, bool AllPacked, bool MockHandedOff, string Stage)
{
    public 살뜰마트표본전표상태? Slip { get; init; }
}

/// <summary>
/// 전용 loopback 표본의 서버 측 상태 소유자. 실제 피킹·포장 UseCase를 별도 SQLite 메모리 DB에서 실행합니다.
/// 상품명·수량 외의 SKU, 위치, 재고 lot, 작업자와 기사 인계는 합성이고 운영 배차/외부 Event 소비자는 연결하지 않습니다.
/// </summary>
public sealed partial class SsalddelMartSampleRuntime(SsalddelMartSampleInput input, TimeProvider? timeProvider = null) : IAsyncDisposable
{
    private const string WorkerId = "sample-mart-worker";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly RecordingLog _log = new();
    private readonly RecordingPublisher _publisher = new();
    private SqliteConnection? _connection;
    private SsalddelContext? _db;
    private bool _disposed;

    public bool HasData => input.HasData;
    public string Notice => input.Notice;
    public bool Initialized { get; private set; }
    public string? Error { get; private set; }
    public bool IsBusy { get; private set; }
    public IReadOnlyList<살뜰마트표본주문상태> Orders { get; private set; } = [];
    public int AuditCount => _log.Entries.Count;
    public int EventCount => _publisher.Notifications.Count;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync(async () =>
        {
            if (Initialized) return true;
            if (!input.HasData) return Fail("검증된 상품 입력이 없어 표본을 준비하지 않았습니다.");
            if (input.Orders.Count == 0 || input.Orders.Any(order =>
                    string.IsNullOrWhiteSpace(order.SampleOrderId) || order.SampleOrderId.Length > 80
                    || order.Items.Count == 0 || order.Items.Any(item =>
                        string.IsNullOrWhiteSpace(item.ProductName) || item.ProductName.Length > 200
                        || item.Quantity <= 0 || item.Quantity > 1000))
                || input.Orders.Select(order => order.SampleOrderId).Distinct(StringComparer.Ordinal).Count() != input.Orders.Count)
                return Fail("표본 주문·상품·수량 입력을 확인해 주세요.");

            try
            {
                _connection = new SqliteConnection("Data Source=:memory:");
                await _connection.OpenAsync(cancellationToken);
                _db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
                    .UseSqlite(_connection).Options, new SampleEncryption());
                await _db.Database.EnsureCreatedAsync(cancellationToken);
                await InitializePickupStoreAsync(cancellationToken);
                await SeedAsync(cancellationToken);
                await RefreshAsync(cancellationToken);
                Initialized = true;
                return true;
            }
            catch
            {
                // 취소도 포함해 부분 연결·seed를 버립니다. 다음 준비가 이전 연결을 덮어쓰지 않습니다.
                await ClearAsync();
                throw;
            }
        }, cancellationToken);

    public Task ResetAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync(async () =>
        {
            await ClearAsync();
            return true;
        }, cancellationToken);

    public Task<bool> StartPickingAsync(string orderId, string lineId, CancellationToken cancellationToken = default)
        => ExecuteAsync(async () =>
        {
            var line = FindLine(orderId, lineId);
            if (line is null) return false;
            var result = await Picking().시작Async(line.LineId, RequestContext(), cancellationToken);
            return await AcceptAsync(result, cancellationToken);
        }, cancellationToken);

    public Task<bool> CompletePickingAsync(string orderId, string lineId, string rackCode,
        bool productConfirmed, bool quantityConfirmed, CancellationToken cancellationToken = default)
        => ExecuteAsync(async () =>
        {
            var line = FindLine(orderId, lineId);
            if (line is null) return false;
            var result = await Picking().완료Async(line.LineId, new 피킹작업완료요청
            {
                RackCode = rackCode, ProductConfirmed = productConfirmed, QuantityConfirmed = quantityConfirmed
            }, RequestContext(), cancellationToken);
            return await AcceptAsync(result, cancellationToken);
        }, cancellationToken);

    public Task<bool> PackOrderAsync(string orderId, bool inventoryConfirmed, bool packageLabelConfirmed,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(async () =>
        {
            if (!Ready()) return false;
            await RefreshAsync(cancellationToken);
            var order = Orders.SingleOrDefault(candidate => candidate.SampleOrderId == orderId);
            if (order is null) return Fail("선택한 표본 주문을 찾을 수 없습니다.");
            if (!order.AllPicked) return Fail("주문의 모든 상품 피킹을 완료한 뒤 포장할 수 있습니다.");
            if (order.Slip is not { Active: true }) return Fail("픽업용 전표를 먼저 준비해 주세요.");
            if (!inventoryConfirmed || !packageLabelConfirmed) return Fail("전체 상품과 포장 표찰을 모두 확인해 주세요.");

            // 한 주문의 상품마다 별도 합성 lot를 준비했으므로 기존 '전체 가용수량 포장' 계약을 변경하지 않습니다.
            // 중간 실패 시 이미 확정된 품목은 유지하고 같은 주문 재요청으로 나머지만 이어갑니다.
            foreach (var line in order.Lines)
            {
                var task = await _db!.피킹포장작업.AsNoTracking()
                    .SingleAsync(candidate => candidate.작업Key == line.LineId, cancellationToken);
                var result = await Packing().완료Async(task.입고상품Id!.Value, new 포장작업완료요청
                {
                    PackagingQuantity = line.Quantity,
                    PackagingType = 포장유형코드.일반포장,
                    InventoryConfirmed = inventoryConfirmed,
                    PackageLabelConfirmed = packageLabelConfirmed,
                    Memo = "살뜰마트 격리 표본: 포장 유형은 합성 설정이며 실제 보관·출고 적합성 근거가 아닙니다."
                }, RequestContext(), cancellationToken);
                if (!await AcceptAsync(result, cancellationToken)) return false;
            }
            await RefreshAsync(cancellationToken);
            return true;
        }, cancellationToken);

    public Task<bool> ConfirmMockHandoffAsync(string orderId, bool mockDriverConfirmed,
        CancellationToken cancellationToken = default)
        // 과거 체크박스 경로는 호환용 실패만 반환합니다. 스캔/번호 검사를 우회하지 않습니다.
        => ExecuteAsync(() => Task.FromResult(Fail("픽업용 전표 QR을 스캔하거나 네 자리 번호로 픽업해 주세요.")), cancellationToken);

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        var db = _db!;
        var warehouse = new 창고 { 창고명 = "살뜰마트 합성 적재 공간", 소유자UserId = WorkerId };
        db.창고.Add(warehouse);
        await db.SaveChangesAsync(cancellationToken);
        var sequence = 0;
        foreach (var sourceOrder in input.Orders)
        {
            var order = new 마트주문
            {
                주문참조번호 = sourceOrder.SampleOrderId, 판매자UserId = WorkerId,
                주문자UserId = "sample-mart-orderer", 상태 = "표본 피킹 대기", 현재단계 = "피킹"
            };
            db.마트주문.Add(order);
            await db.SaveChangesAsync(cancellationToken);
            foreach (var sourceItem in sourceOrder.Items)
            {
                sequence++;
                var lineId = $"sample-pick-{sequence:D3}";
                var sku = $"SAMPLE-SKU-{sequence:D3}";
                var rack = $"SAMPLE-RACK-{sequence:D3}";
                var inbound = new 입고요청
                {
                    창고Id = warehouse.Id, 주문참조번호 = sourceOrder.SampleOrderId,
                    주문자UserId = order.주문자UserId, 판매자UserId = WorkerId,
                    예정상품명 = sourceItem.ProductName, 예정SKU = sku, 예정수량 = sourceItem.Quantity,
                    상태 = "표본 적재 완료", 보관조건 = "표본 가정: 실제 온도 조건 미검증"
                };
                db.입고요청.Add(inbound);
                await db.SaveChangesAsync(cancellationToken);
                var item = new 입고상품
                {
                    입고요청Id = inbound.Id, 창고Id = warehouse.Id, 소유자UserId = WorkerId,
                    판매자UserId = WorkerId, 상품명 = sourceItem.ProductName, SKU = sku,
                    입고수량 = sourceItem.Quantity, 가용수량 = sourceItem.Quantity,
                    상태 = "적재완료", 보관위치 = rack
                };
                db.입고상품.Add(item);
                await db.SaveChangesAsync(cancellationToken);
                var outbound = new 출고예정
                {
                    주문참조번호 = sourceOrder.SampleOrderId, 출고창고Id = warehouse.Id,
                    입고상품Id = item.Id, 입고요청Id = inbound.Id, 상품명 = sourceItem.ProductName,
                    SKU = sku, 수량 = sourceItem.Quantity, 판매자UserId = WorkerId,
                    주문자UserId = order.주문자UserId
                };
                db.출고예정.Add(outbound);
                await db.SaveChangesAsync(cancellationToken);
                inbound.출고예정Id = outbound.Id;
                db.마트주문상품.Add(new 마트주문상품
                {
                    마트주문Id = order.Id, 출고예정Id = outbound.Id,
                    상품명 = sourceItem.ProductName, SKU = sku, 수량 = sourceItem.Quantity
                });
                db.피킹포장작업.Add(new 피킹포장작업
                {
                    작업Key = lineId, 작업유형 = 피킹포장작업유형.피킹, 처리방식 = "피킹포장분리",
                    상태 = 피킹포장작업상태.대기, 창고Id = warehouse.Id, 창고명 = warehouse.창고명,
                    작업자UserId = WorkerId, 작업자표시명 = "합성 피킹 작업자",
                    주문참조번호 = sourceOrder.SampleOrderId,
                    라인Key = outbound.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    입고상품Id = item.Id, 출고예정Id = outbound.Id, 상품명 = sourceItem.ProductName,
                    SKU = sku, 수량 = sourceItem.Quantity, 적재대코드 = rack,
                    묶음바코드 = "SAMPLE-PACK-" + sourceOrder.SampleOrderId,
                    할당사유 = "기록 상품 기반 합성 재고·위치 표본"
                });
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        db.ChangeTracker.Clear();
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var tasks = await _db!.피킹포장작업.AsNoTracking().OrderBy(task => task.Id).ToArrayAsync(cancellationToken);
        var items = await _db.입고상품.AsNoTracking().ToDictionaryAsync(item => item.Id, cancellationToken);
        var slips = await ReadPickupSlipsAsync(cancellationToken);
        Orders = input.Orders.Select((order, index) =>
        {
            var lines = tasks.Where(task => task.주문참조번호 == order.SampleOrderId).Select(task =>
                new 살뜰마트표본품목상태(task.작업Key, task.상품명, task.수량, task.SKU, task.적재대코드!, task.상태,
                    items[task.입고상품Id!.Value].상태.StartsWith("포장완료-", StringComparison.Ordinal))).ToArray();
            var picked = lines.Length > 0 && lines.All(line => line.PickingStatus == 피킹포장작업상태.완료);
            var packed = picked && lines.All(line => line.Packed);
            slips.TryGetValue(order.SampleOrderId, out var slip);
            var handedOff = packed && slip is { PickedUp: true };
            var stage = handedOff ? "표본 픽업 완료" : packed ? "픽업 대기" : picked ? "포장 대기"
                : lines.Any(line => line.PickingStatus != 피킹포장작업상태.대기) ? "피킹 진행" : "피킹 대기";
            return new 살뜰마트표본주문상태(order.SampleOrderId, $"표본 주문 {index + 1:D2}", lines, picked, packed, handedOff, stage) { Slip = slip };
        }).ToArray();
    }

    private 살뜰마트표본품목상태? FindLine(string orderId, string lineId)
    {
        if (!Ready()) return null;
        var line = Orders.SingleOrDefault(order => order.SampleOrderId == orderId)?.Lines.SingleOrDefault(item => item.LineId == lineId);
        if (line is null) Fail("선택한 주문의 피킹 상품을 찾을 수 없습니다.");
        return line;
    }

    private bool Ready() => Initialized && _db is not null || Fail("먼저 표본을 준비해 주세요.");
    private bool Fail(string message) { Error = message; return false; }
    private 피킹작업UseCase Picking() => new(_db!, new SampleCurrentUser(), _log, _publisher);
    private 포장작업UseCase Packing() => new(_db!, new SampleCurrentUser(), _log, _publisher);
    private static 창고작업요청Context RequestContext() => new("SsalddelMartSample", WorkerId, "합성 작업자", 역할명.창고관리자,
        "/workspace/warehouse/ssalddel-mart-sample", "sample-mart-local", "127.0.0.1", "isolated-sample");

    private async Task<bool> AcceptAsync<T>(Result<T> result, CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken);
        return result.IsSuccess || Fail(result.Errors.FirstOrDefault()?.Message ?? "표본 작업을 처리하지 못했습니다.");
    }

    private async Task<bool> ExecuteAsync(Func<Task<bool>> action, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            IsBusy = true;
            Error = null;
            return await action();
        }
        catch (Exception exception) when (exception is DbUpdateException or SqliteException or InvalidOperationException)
        {
            // 원문 예외, SQL과 상품 입력을 로그/화면에 남기지 않습니다. 원본 자료로 대체하지도 않습니다.
            if (!Initialized) await ClearAsync();
            Error = "격리 표본 처리에 실패했습니다. 표본 초기화 후 다시 확인해 주세요.";
            return false;
        }
        finally { IsBusy = false; _gate.Release(); }
    }

    private async Task ClearAsync()
    {
        if (_pickupDb is not null) await _pickupDb.DisposeAsync();
        _pickupDb = null; PickupCount = 0;
        if (_db is not null) await _db.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
        _db = null; _connection = null;
        Orders = []; Initialized = false;
        _log.Entries.Clear(); _publisher.Notifications.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try { if (!_disposed) { await ClearAsync(); _disposed = true; } }
        finally { _gate.Release(); }
    }

    private sealed class SampleCurrentUser : ICurrentUserAccessor
    {
        public string? UserId => WorkerId;
        public string? Role => 역할명.창고관리자;
    }

    private sealed class SampleEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }

    private sealed class RecordingLog : I사용자행위로그Service
    {
        public List<사용자행위로그기록> Entries { get; } = [];
        public Task 기록Async(사용자행위로그기록 entry, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); Entries.Add(entry); return Task.CompletedTask; }
    }

    private sealed class RecordingPublisher : IPublisher
    {
        public List<object> Notifications { get; } = [];
        public Task Publish(object notification, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); Notifications.Add(notification); return Task.CompletedTask; }
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
            => Publish((object)notification, cancellationToken);
    }
}
