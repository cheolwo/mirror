using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Contracts.Mart;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Common;
using 살뜰.도메인.공통;
using 살뜰.도메인.마트;

namespace Ssalddel.Services.Operations;

public interface I마트기사업무Service
{
    Task<Result<마트기사업무목록Dto>> 조회Async(string 기사Id, CancellationToken cancellationToken = default);
    Task<Result<마트기사업무항목Dto>> 업무조회Async(string 기사Id, string offerId, CancellationToken cancellationToken = default);
    Task<Result<마트기사업무항목Dto>> 수락Async(string 기사Id, string offerId, 마트기사수락요청 request, CancellationToken cancellationToken = default);
    Task<Result<마트기사업무항목Dto>> 거절Async(string 기사Id, string offerId, 마트기사거절요청 request, CancellationToken cancellationToken = default);
    Task<Result<마트기사업무항목Dto>> 픽업Async(string 기사Id, string offerId, 마트기사픽업요청 request, CancellationToken cancellationToken = default);
    Task<Result<마트기사업무항목Dto>> 전달Async(string 기사Id, string offerId, 마트기사전달요청 request, CancellationToken cancellationToken = default);
    Task<Result<마트포장전표Dto>> 전표발급Async(string orderReference, 마트포장전표발급요청 request, CancellationToken cancellationToken = default);
    Task<Result<마트포장전표Dto>> 전표조회Async(string orderReference, CancellationToken cancellationToken = default);
    Task<Result<마트기사업무항목Dto>> 요청결과조회Async(Guid clientRequestId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 마트 전용 서버 업무 권위입니다. 읽기 상태 사본은 원자적인 권한 증빙이 아니므로 최종 transaction 안에서
/// 원천·현재 추천/배정·전표·재고를 다시 검사합니다. 음식 주문·수령 확정·지급 완료를 생성하지 않습니다.
/// </summary>
public sealed partial class 마트기사업무Service(
    SsalddelContext db,
    ICurrentUserAccessor user,
    I마트기사업무Gate gate,
    I마트라스트마일원천Adapter source,
    IPersonalDataEncryptionService protector,
    I운영배차공통UseCase receiving,
    TimeProvider? timeProvider = null) : I마트기사업무Service
{
    internal const string SourceType = "SsalddelMartPackedOrder";
    private DateTime Now => (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private bool IsDriver(string driverId) => !string.IsNullOrWhiteSpace(driverId) && user.UserId == driverId
        && (user.Role == 역할명.기사 || user.Role == 역할명.배달기사 || user.Role == 역할명.용달기사);

    private static Result<T> Fail<T>(string code, int status = 409)
        => Result.Fail<T>(new Error(status == 404 ? "업무를 찾을 수 없거나 조회 권한이 없습니다."
                : "현재 업무 조건을 확인한 뒤 정본을 다시 조회해 주세요.")
            .WithMetadata("StatusCode", status).WithMetadata("ErrorCode", code));

    private static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string InputDigest<T>(string kind, string offerId, T input)
        => Digest(JsonSerializer.Serialize(new { kind, offerId, input }));

    private async Task<Result<T>> TransactionAsync<T>(Func<Task<Result<T>>> body, Guid requestId, CancellationToken ct)
    {
        if (!gate.Enabled) return Fail<T>("MartDriverWorkflowDisabled", 403);
        if (!db.Database.IsRelational()) return Fail<T>("MartDriverRelationalStoreRequired", 503);
        if (requestId == Guid.Empty) return Fail<T>("MartDriverRequestIdRequired", 400);
        var retry = 0;
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                if (retry++ > 0) db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var result = await body();
                // 잘못된 번호 시도도 동일 요청 재전송으로 회수를 늘리지 않도록 실패 영수증과 함께 확정합니다.
                var hasFailureReceipt = db.ChangeTracker.Entries<마트기사요청영수증>()
                    .Any(x => x.State == EntityState.Added && x.Entity.클라이언트요청Id == requestId && !x.Entity.성공여부);
                if (result.IsSuccess || hasFailureReceipt)
                {
                    await db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                }
                else
                {
                    await transaction.RollbackAsync(ct);
                    db.ChangeTracker.Clear();
                }
                return result;
            });
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return Fail<T>("MartDriverConcurrentChange");
        }
    }

    private async Task<운송원장?> QueueAsync(string offerId, CancellationToken ct)
    {
        var queue = await db.운송원장.SingleOrDefaultAsync(x => x.의뢰Id == offerId, ct);
        if (queue is null || queue.원본의뢰유형 != SourceType || queue.배차업무유형 != 상태값.배차업무유형.음식배달
            || queue.일반회원배송전용 || queue.원본의뢰Id != queue.의뢰Id) return null;
        var order = await db.마트주문.AsNoTracking().SingleOrDefaultAsync(x => x.주문참조번호 == queue.원본의뢰Id, ct);
        if (order is null || order.상태 == "취소" || order.판매자UserId != queue.화주Id || string.IsNullOrWhiteSpace(order.주문자UserId)) return null;
        var plans = await db.출고예정.AsNoTracking().Where(x => x.주문참조번호 == order.주문참조번호).ToListAsync(ct);
        return plans.Count > 0 && plans.All(x => x.판매자UserId == order.판매자UserId
            && x.주문자UserId == order.주문자UserId && x.상태 != 살뜰.도메인.창고.출고상태.취소 && x.운송의뢰Id == queue.의뢰Id) ? queue : null;
    }

    private async Task<마트기사요청영수증?> ReceiptAsync(Guid id, CancellationToken ct)
        => await db.Set<마트기사요청영수증>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.작업자UserId == user.UserId && x.클라이언트요청Id == id, ct);

    private bool SameReceipt(마트기사요청영수증 receipt, string kind, string offerId, string digest)
        => receipt.작업Code == kind && receipt.배차의뢰Id == offerId
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(protector.Unprotect(receipt.보호입력Digest) ?? ""), Encoding.UTF8.GetBytes(digest));

    private void AddReceipt(Guid id, string kind, string offerId, string reference, string digest,
        bool success = true, string resultCode = "Succeeded", int status = 200)
    {
        db.Set<마트기사요청영수증>().Add(new()
        {
            작업자UserId = user.UserId!, 클라이언트요청Id = id, 작업Code = kind, 배차의뢰Id = offerId,
            주문참조번호 = reference, 보호입력Digest = protector.Protect(digest) ?? throw new InvalidOperationException("MartRequestProtectionUnavailable"),
            성공여부 = success, 결과Code = resultCode, 결과StatusCode = status, CreatedAtUtc = Now
        });
        if (success && kind != "IssueLabel")
        {
            var revision = db.Set<마트배송시도>().Local.FirstOrDefault(x => x.배차의뢰Id == offerId)?.Revision ?? 0;
            db.운송이벤트.Add(new 운송이벤트 { 의뢰Id = offerId, 이벤트타입 = "MartDriverWorkStateChanged", 이벤트시각 = Now,
                메타데이터 = JsonSerializer.Serialize(new { Action = kind, Revision = revision, RequestId = id }) });
        }
    }

    private void Followups(운송원장 queue, 마트배송시도? attempt, string kind = "")
    {
        var kinds = kind.Length > 0 ? new[] { kind } : new[] { "TransportLedger", "MartLedger", "DispatchScope" };
        foreach (var item in kinds)
        {
            var revision = attempt?.Revision ?? queue.추천라운드;
            db.Set<마트기사후속Outbox>().Add(new()
            {
                멱등키 = "mart-driver:" + Digest($"{queue.의뢰Id}:{attempt?.시도StableId}:{revision}:{item}:{queue.상태}"),
                배차의뢰Id = queue.의뢰Id, 주문참조번호 = queue.원본의뢰Id, 기사Id = user.UserId!,
                시도StableId = attempt?.시도StableId ?? "", 업무Revision = revision, 종류Code = item,
                CreatedAtUtc = Now, UpdatedAtUtc = Now
            });
        }
    }

    private async Task<bool> TouchQueueAsync(운송원장 queue, CancellationToken ct)
    {
        var old = queue.UpdatedAt;
        var now = Now <= Utc(old) ? Utc(old).AddTicks(10) : Now;
        var changed = await db.운송원장.Where(x => x.Id == queue.Id && x.UpdatedAt == old
            && x.추천라운드 == queue.추천라운드 && x.확정기사Id == queue.확정기사Id)
            .ExecuteUpdateAsync(x => x.SetProperty(y => y.UpdatedAt, now), ct);
        if (changed != 1) return false;
        queue.UpdatedAt = now;
        // MySQL timestamp rowversion은 위 CAS에서 갱신되므로 최종 SaveChanges가 현재 값을 사용하게 다시 읽습니다.
        await db.Entry(queue).ReloadAsync(ct);
        return true;
    }

    private async Task UpdateOrderAsync(운송원장 queue, string status, string stage, CancellationToken ct)
    {
        var order = await db.마트주문.Include(x => x.상품목록).SingleAsync(x => x.주문참조번호 == queue.원본의뢰Id, ct);
        order.상태 = status; order.현재단계 = stage; order.UpdatedAt = Now;
        if (stage is "배송중" or "전달완료")
            foreach (var line in order.상품목록) { line.상태 = status; line.UpdatedAt = Now; }
    }
}
