using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Mart;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.도메인.마트;
using 살뜰.도메인.창고;

namespace Ssalddel.Services.Operations;

public sealed partial class 마트기사업무Service
{
    private sealed record 전표코드(string Qr, string Pin);

    public async Task<Result<마트포장전표Dto>> 전표조회Async(string orderReference, CancellationToken cancellationToken = default)
    {
        if (!gate.Enabled) return Fail<마트포장전표Dto>("MartDriverWorkflowDisabled", 403);
        if (!await WarehouseAccessAsync(orderReference, cancellationToken)) return Fail<마트포장전표Dto>("MartSlipNotFound", 404);
        var label = await db.Set<마트포장전표>().AsNoTracking().SingleOrDefaultAsync(x => x.주문참조번호 == orderReference, cancellationToken);
        return label is null ? Fail<마트포장전표Dto>("MartSlipNotIssued", 404) : SlipResult(label);
    }

    public Task<Result<마트포장전표Dto>> 전표발급Async(string orderReference, 마트포장전표발급요청 request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var digest = InputDigest("IssueLabel", orderReference, request);
        return TransactionAsync(async () =>
        {
            if (!await WarehouseAccessAsync(orderReference, cancellationToken)) return Fail<마트포장전표Dto>("MartSlipNotFound", 404);
            var receipt = await ReceiptAsync(request.ClientRequestId, cancellationToken);
            if (receipt is not null && !SameReceipt(receipt, "IssueLabel", orderReference, digest))
                return Fail<마트포장전표Dto>("MartDriverRequestConflict");
            var existing = await db.Set<마트포장전표>().SingleOrDefaultAsync(x => x.주문참조번호 == orderReference, cancellationToken);
            if (receipt is not null) return existing is null ? Fail<마트포장전표Dto>("MartSlipNotIssued", 404) : SlipResult(existing);
            var preparation = await source.조회Async(orderReference, cancellationToken);
            var orderState = await db.마트주문.AsNoTracking().Where(x => x.주문참조번호 == orderReference).Select(x => x.상태).SingleAsync(cancellationToken);
            var cancelled = await db.출고예정.AsNoTracking().AnyAsync(x => x.주문참조번호 == orderReference && x.상태 == 출고상태.취소, cancellationToken);
            // 전표는 포장 전에 붙일 수 있습니다. 실제 출고는 픽업 transaction의 전체 준비/재고 관문만 통과합니다.
            if (!preparation.원천결속됨 || orderState == "취소" || cancelled || existing?.픽업시각Utc is not null)
                return Fail<마트포장전표Dto>("MartDriverPreparationNotReady");
            if (existing is null)
            {
                var labelId = Guid.NewGuid().ToString("N");
                var codes = new 전표코드($"SSALDDEL-MART-PKG:{labelId}:1:{Convert.ToHexString(RandomNumberGenerator.GetBytes(24))}",
                    RandomNumberGenerator.GetInt32(0, 10000).ToString("D4", System.Globalization.CultureInfo.InvariantCulture));
                existing = new 마트포장전표 { 주문참조번호 = orderReference, 전표Id = labelId, Revision = 1,
                    보호코드Json = protector.Protect(JsonSerializer.Serialize(codes)) ?? throw new InvalidOperationException("MartSlipProtectionUnavailable"),
                    QrDigest = Digest(codes.Qr), 활성여부 = true, 발급시각Utc = Now };
                db.Set<마트포장전표>().Add(existing);
            }
            AddReceipt(request.ClientRequestId, "IssueLabel", orderReference, orderReference, digest);
            return SlipResult(existing);
        }, request.ClientRequestId, cancellationToken);
    }

    private async Task<bool> WarehouseAccessAsync(string reference, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(user.UserId) || string.IsNullOrWhiteSpace(reference)) return false;
        if (user.Role is not (역할명.서버관리자 or 역할명.창고관리자 or 역할명.보세창고운영자 or 역할명.FTZ운영자 or 역할명.풀필먼트운영자)) return false;
        var order = await db.마트주문.AsNoTracking().SingleOrDefaultAsync(x => x.주문참조번호 == reference, ct);
        var plans = await db.출고예정.AsNoTracking().Where(x => x.주문참조번호 == reference).ToListAsync(ct);
        if (order is null || plans.Count == 0 || plans.Any(x => x.판매자UserId != order.판매자UserId || x.주문자UserId != order.주문자UserId)) return false;
        if (user.Role == 역할명.서버관리자) return true;
        var ids = plans.Select(x => x.출고창고Id).Distinct().ToArray();
        var allowed = await db.창고.AsNoTracking().Where(x => Enumerable.Contains(ids, x.Id)
            && (x.소유자UserId == user.UserId || db.창고사용자.Any(a => a.창고Id == x.Id && a.UserId == user.UserId)))
            .Select(x => x.Id).ToArrayAsync(ct);
        return allowed.Length == ids.Length;
    }

    private 전표코드? ReadCodes(마트포장전표 label)
    {
        try { return JsonSerializer.Deserialize<전표코드>(protector.Unprotect(label.보호코드Json) ?? ""); }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException) { return null; }
    }

    private Result<마트포장전표Dto> SlipResult(마트포장전표 label)
    {
        var codes = ReadCodes(label);
        return codes is null ? Fail<마트포장전표Dto>("MartSlipProtectionUnavailable", 503) : Result.Ok(new 마트포장전표Dto
        {
            OrderReference = label.주문참조번호, LabelId = label.전표Id, QrCode = codes.Qr, ConfirmationCode = codes.Pin,
            Revision = label.Revision, Active = label.활성여부, IssuedAtUtc = Utc(label.발급시각Utc)
        });
    }

    public Task<Result<마트기사업무항목Dto>> 픽업Async(string 기사Id, string offerId, 마트기사픽업요청 request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Code is null || request.Code.Length > 512 || string.IsNullOrWhiteSpace(request.Code)
            || (request.ByNumber && (request.Code.Length != 4 || request.Code.Any(c => c < '0' || c > '9'))))
            return Task.FromResult(Fail<마트기사업무항목Dto>("MartPickupCodeInvalid", 400));
        var digest = InputDigest("Pickup", offerId, request);
        return TransactionAsync(async () =>
        {
            if (!IsDriver(기사Id)) return Fail<마트기사업무항목Dto>("MartDriverIdentityRequired", 403);
            var receipt = await ReceiptAsync(request.ClientRequestId, cancellationToken);
            if (receipt is not null) return SameReceipt(receipt, "Pickup", offerId, digest)
                ? await ReceiptResultAsync(receipt, cancellationToken) : Fail<마트기사업무항목Dto>("MartDriverRequestConflict");
            var queue = await QueueAsync(offerId, cancellationToken);
            var attempt = await db.Set<마트배송시도>().SingleOrDefaultAsync(x => x.배차의뢰Id == offerId && x.기사Id == 기사Id, cancellationToken);
            if (queue is null || queue.확정기사Id != 기사Id || attempt is null) return Fail<마트기사업무항목Dto>("MartDriverWorkNotFound", 404);
            if (attempt.Revision != request.ExpectedRevision || attempt.상태Code != "Accepted"
                || queue.상태 != 상태값.배차대기상태.확정 || queue.배차큐단계 != 상태값.배차큐단계.확정)
                return Fail<마트기사업무항목Dto>("MartDriverRevisionChanged");
            var label = await db.Set<마트포장전표>().SingleOrDefaultAsync(x => x.주문참조번호 == queue.원본의뢰Id, cancellationToken);
            if (label is null || !label.활성여부 || label.픽업시각Utc.HasValue || label.Revision != request.ExpectedLabelRevision)
                return Fail<마트기사업무항목Dto>("MartSlipRevisionChanged");
            if (request.ByNumber && label.번호차단만료시각Utc > Now) return Fail<마트기사업무항목Dto>("MartPickupNumberTemporarilyBlocked", 429);
            var codes = request.ByNumber ? ReadCodes(label) : null;
            if (request.ByNumber && codes is null) return Fail<마트기사업무항목Dto>("MartSlipProtectionUnavailable", 503);
            var matches = request.ByNumber
                ? CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(codes!.Pin), Encoding.UTF8.GetBytes(request.Code))
                : CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(label.QrDigest), Encoding.UTF8.GetBytes(Digest(request.Code)));
            if (!matches)
            {
                if (request.ByNumber)
                {
                    if (label.번호차단만료시각Utc.HasValue && label.번호차단만료시각Utc <= Now) label.확인실패횟수 = 0;
                    label.확인실패횟수++;
                    if (label.확인실패횟수 >= 5) label.번호차단만료시각Utc = Now.AddMinutes(1);
                }
                // 번호 실패 카운터는 동일 전표 판본 아래 전용 row fence를 사용합니다.
                if (!await TouchQueueAsync(queue, cancellationToken)) return Fail<마트기사업무항목Dto>("MartDriverConcurrentChange");
                AddReceipt(request.ClientRequestId, "Pickup", offerId, queue.원본의뢰Id, digest, false, "MartPickupCodeMismatch", 409);
                return Fail<마트기사업무항목Dto>("MartPickupCodeMismatch");
            }
            var preparation = await source.조회Async(queue.원본의뢰Id, cancellationToken);
            if (!preparation.원천결속됨 || !preparation.픽업준비완료) return Fail<마트기사업무항목Dto>("MartDriverPreparationNotReady");
            var plans = await db.출고예정.AsNoTracking().Where(x => x.주문참조번호 == queue.원본의뢰Id).ToListAsync(cancellationToken);
            if (plans.Any(x => x.상태 != 출고상태.준비중 || x.입고상품Id is null || x.수량 <= 0)) return Fail<마트기사업무항목Dto>("MartOutboundNotReady");
            if (!await TouchQueueAsync(queue, cancellationToken)) return Fail<마트기사업무항목Dto>("MartDriverConcurrentChange");
            foreach (var plan in plans)
            {
                // 첫 조각은 한 출고=한 전량 포장 재고만 허용합니다. 예약·분할·공유 lot의 추론 출고는 하지 않습니다.
                var inventoryChanged = await db.입고상품.Where(x => x.Id == plan.입고상품Id && x.창고Id == plan.출고창고Id
                    && x.가용수량 == plan.수량 && x.예약수량 == 0 && x.상태.StartsWith("포장완료"))
                    .ExecuteUpdateAsync(x => x.SetProperty(y => y.가용수량, 0).SetProperty(y => y.상태, "출고완료").SetProperty(y => y.UpdatedAt, Now), cancellationToken);
                var outboundChanged = await db.출고예정.Where(x => x.Id == plan.Id && x.상태 == 출고상태.준비중
                    && x.운송의뢰Id == offerId && x.입고상품Id == plan.입고상품Id)
                    .ExecuteUpdateAsync(x => x.SetProperty(y => y.상태, 출고상태.출고완료)
                        .SetProperty(y => y.출고처리일시, Now).SetProperty(y => y.UpdatedAt, Now), cancellationToken);
                if (inventoryChanged != 1 || outboundChanged != 1) return Fail<마트기사업무항목Dto>("MartInventoryChanged");
                db.재고이력.Add(new 재고이력 { 입고상품Id = plan.입고상품Id!.Value, 이력유형 = "출고", 변경수량 = -plan.수량,
                    변경후수량 = 0, 원인유형 = "마트기사픽업", 원인Id = plan.Id, 처리UserId = 기사Id, 처리일시 = Now });
                db.재고이동.Add(new 재고이동 { 창고Id = plan.출고창고Id, 입고상품Id = plan.입고상품Id,
                    판매상품Id = plan.판매상품Id, 상품명 = plan.상품명, SKU = plan.SKU, 이동유형 = "출고", 수량 = plan.수량,
                    주문Id = plan.주문Id, 주문참조번호 = plan.주문참조번호, 출고예정Id = plan.Id, 입고요청Id = plan.입고요청Id,
                    운송의뢰Id = offerId, 처리UserId = 기사Id, 발생일시 = Now });
            }
            var pickedAt = Now;
            label.활성여부 = false; label.픽업시각Utc = pickedAt; label.확인실패횟수 = 0; label.번호차단만료시각Utc = null;
            attempt.상태Code = "PickedUp"; attempt.Revision++; attempt.픽업시각Utc = pickedAt; attempt.UpdatedAtUtc = pickedAt;
            attempt.픽업전표Id = label.전표Id; attempt.픽업전표Revision = label.Revision;
            queue.상태 = 상태값.배차상태.상차완료; queue.출발_픽업 = pickedAt;
            await UpdateOrderAsync(queue, "배송중", "배송중", cancellationToken);
            Followups(queue, attempt); AddReceipt(request.ClientRequestId, "Pickup", offerId, queue.원본의뢰Id, digest);
            return Result.Ok(Item(queue, attempt, label, 기사Id));
        }, request.ClientRequestId, cancellationToken);
    }
}
