using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Hr;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Domain.HumanResources;
using 살뜰.Data;
using 살뜰.Services.Notifications;
using 살뜰.Services.Options;

namespace Ssalddel.Application.HumanResources;

public interface I근무가능요청UseCase
{
    Task<Result<근무연락동의>> 동의조회Async(CancellationToken ct);
    Task<Result<근무연락동의>> 동의저장Async(근무연락동의 request, CancellationToken ct);
    Task<Result<근무가능문맥응답>> 문맥Async(근무가능업무범위 request, CancellationToken ct);
    Task<Result<근무가능목록응답>> 목록Async(long? warehouseId, CancellationToken ct, int page = 0);
    Task<Result<근무가능요청응답>> 생성Async(근무가능요청등록요청 request, CancellationToken ct);
    Task<Result<근무가능요청응답>> 답변Async(Guid invitationId, 근무가능답변요청 request, bool confirm, CancellationToken ct, long? warehouseId = null);
}

/// <summary>근무 의향과 계획상 확보를 저장합니다. 고용·HR 역할·출근·급여는 변경하지 않습니다.</summary>
public sealed class 근무가능요청UseCase(
    SsalddelContext db, ICurrentUserAccessor current, I출고예정검토UseCase outbound,
    IKakaoAlimTalkService kakao, IOptions<KakaoAlimTalkOptions> options,
    TimeProvider? timeProvider = null) : I근무가능요청UseCase
{
    private DateTime Now => (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
    private string UserId => current.UserId?.Trim() ?? "";
    private readonly KakaoAlimTalkOptions settings = options.Value;
    private static Result<T> Fail<T>(int code, string message)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", code));
    private bool KakaoReady => settings.Enabled && !string.IsNullOrWhiteSpace(settings.BaseUrl)
        && !string.IsNullOrWhiteSpace(settings.ApiKey) && !string.IsNullOrWhiteSpace(settings.SenderKey)
        && !string.IsNullOrWhiteSpace(settings.WorkforceAvailabilityTemplateCode)
        && Uri.TryCreate(settings.WorkforceReplyBaseUrl, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);

    public async Task<Result<근무연락동의>> 동의조회Async(CancellationToken ct)
    {
        if (UserId == "") return Fail<근무연락동의>(401, "로그인 후 확인해 주세요.");
        var row = await db.근무연락동의.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == UserId, ct);
        return Result.Ok(new 근무연락동의 { AllowInApp = row?.AllowInApp ?? false,
            AllowKakao = row?.AllowKakao ?? false, Revision = row?.Revision ?? Guid.Empty });
    }

    public Task<Result<근무연락동의>> 동의저장Async(근무연락동의 request, CancellationToken ct)
        => WriteAsync(async () =>
        {
            if (UserId == "") return Fail<근무연락동의>(401, "로그인 후 확인해 주세요.");
            var row = await db.근무연락동의.SingleOrDefaultAsync(x => x.UserId == UserId, ct);
            if ((row?.Revision ?? Guid.Empty) != request.Revision)
                return Fail<근무연락동의>(409, "수신 설정이 바뀌었습니다. 다시 조회해 주세요.");
            if (request.AllowKakao && !request.AllowInApp)
                return Fail<근무연락동의>(400, "답변을 확인할 앱 수신함에도 동의해 주세요.");
            if (row is null) { row = new() { UserId = UserId }; db.근무연락동의.Add(row); }
            row.AllowInApp = request.AllowInApp; row.AllowKakao = request.AllowKakao;
            row.Revision = Guid.NewGuid(); row.UpdatedAtUtc = Now;
            await db.SaveChangesAsync(ct);
            return Result.Ok(new 근무연락동의 { AllowInApp = row.AllowInApp,
                AllowKakao = row.AllowKakao, Revision = row.Revision });
        }, ct);

    private async Task<bool> ManageAsync(long warehouseId, CancellationToken ct)
    {
        if (UserId == "") return false;
        var warehouse = await db.창고.AsNoTracking().SingleOrDefaultAsync(x => x.Id == warehouseId, ct);
        if (warehouse is null) return false;
        if (current.Role == "서버관리자" || warehouse.소유자UserId == UserId) return true;
        var member = await db.창고사용자.AnyAsync(x => x.창고Id == warehouseId && x.UserId == UserId, ct);
        if (!member) return false;
        if (current.Role == "창고관리자") return true;
        var scopeId = warehouseId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return await db.HrRoleAssignments.AnyAsync(x => x.UserId == UserId && x.IsActive
            && x.RoleCode == HrDetailedRoleCodes.WarehouseManager
            && (x.ScopeType == HrScopeTypes.Warehouse && x.ScopeId == scopeId
                || x.ScopeType == HrScopeTypes.Platform && x.ScopeId == HrScopeIds.Global), ct);
    }

    private async Task<Result<OutboundPeriodResponse>> SourceAsync(근무가능업무범위 request, CancellationToken ct)
    {
        if (request.Stage is not ("picking" or "packing") || request.GroupPage < 0)
            return Fail<OutboundPeriodResponse>(400, "피킹 또는 포장 공정과 묶음 페이지를 확인해 주세요.");
        var result = await outbound.목록Async(new 출고예정검토목록조회요청
        {
            WarehouseId = request.WarehouseId, PeriodStartUtc = request.PeriodStartUtc,
            PeriodEndUtc = request.PeriodEndUtc, PeriodStage = request.Stage,
            BatchKey = string.IsNullOrWhiteSpace(request.GroupKey) ? null : request.GroupKey,
            BatchPage = request.GroupPage, BatchPageSize = 6, PageSize = 1
        }, ct);
        if (result.IsFailed) return Result.Fail<OutboundPeriodResponse>(result.Errors);
        return result.Value.Period is { } period ? Result.Ok(period)
            : Fail<OutboundPeriodResponse>(400, "실제 처리 묶음의 조회 구간이 필요합니다.");
    }

    public async Task<Result<근무가능문맥응답>> 문맥Async(근무가능업무범위 request, CancellationToken ct)
    {
        if (UserId == "") return Fail<근무가능문맥응답>(401, "로그인 후 확인해 주세요.");
        if (!await ManageAsync(request.WarehouseId, ct))
            return Fail<근무가능문맥응답>(404, "관리할 창고를 찾을 수 없습니다.");
        var source = await SourceAsync(request, ct);
        if (source.IsFailed) return Result.Fail<근무가능문맥응답>(source.Errors);
        var memberRows = await db.창고사용자.AsNoTracking().Where(x => x.창고Id == request.WarehouseId)
            .OrderBy(x => x.Id).Take(100).ToArrayAsync(ct);
        var ids = memberRows.Select(x => x.UserId).Distinct().ToList();
        var prefs = await db.근무연락동의.AsNoTracking().Where(x => ids.Contains(x.UserId)).ToArrayAsync(ct);
        var users = await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.UserName, x.PhoneNumberConfirmed }).ToArrayAsync(ct);
        return Result.Ok(new 근무가능문맥응답
        {
            WarehouseId = request.WarehouseId, WarehouseName = source.Value.WarehouseName,
            ObservedAtUtc = source.Value.ObservedAtUtc,
            Groups = (source.Value.Workflow?.Groups ?? []).Concat(source.Value.Workflow?.SelectedGroup is { } selected ? [selected] : [])
                .DistinctBy(x => x.Key).ToArray(),
            HasNextGroupPage = source.Value.Workflow?.HasNextPage ?? false,
            Candidates = ids.Select(id =>
            {
                var pref = prefs.SingleOrDefault(x => x.UserId == id);
                var user = users.SingleOrDefault(x => x.Id == id);
                return new 근무가능후보(id, SafeName(user?.UserName),
                    memberRows.First(x => x.UserId == id).역할명,
                    pref?.AllowInApp ?? false, pref?.AllowKakao ?? false,
                    user?.PhoneNumberConfirmed ?? false);
            }).ToArray(),
            Channels = [new(근무연락채널.InApp, "앱 수신함", true, "요청을 본인 수신함에 등록합니다. 읽음·출근 확정이 아닙니다."),
                new(근무연락채널.Kakao, "카카오 알림톡", KakaoReady, KakaoReady
                    ? "별도 연락 동의와 인증된 기존 연락처가 필요합니다. 공급자 접수와 실제 수신은 다릅니다."
                    : "인력 요청용 승인 템플릿·응답 주소 또는 발송 연결 미설정"),
                new(근무연락채널.Sms, "문자", false, "문자 전송 실행기 미연결 · 발송하지 않습니다.")]
        });
    }

    public async Task<Result<근무가능목록응답>> 목록Async(long? warehouseId, CancellationToken ct, int page = 0)
    {
        if (UserId == "") return Fail<근무가능목록응답>(401, "로그인 후 확인해 주세요.");
        if (page is < 0 or > 100000) return Fail<근무가능목록응답>(400, "요청 목록 페이지를 확인해 주세요.");
        if (warehouseId.HasValue && !await ManageAsync(warehouseId.Value, ct))
            return Fail<근무가능목록응답>(404, "관리할 창고를 찾을 수 없습니다.");
        var query = db.근무가능요청.AsNoTracking();
        query = warehouseId.HasValue ? query.Where(x => x.WarehouseId == warehouseId.Value)
            : query.Where(x => db.근무가능대상.Any(y => y.RequestId == x.Id && y.UserId == UserId));
        var rows = await query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(page * 50).Take(51).ToArrayAsync(ct);
        var items = new List<근무가능요청응답>();
        foreach (var row in rows.Take(50)) items.Add(await MapAsync(row, warehouseId.HasValue, ct));
        return Result.Ok(new 근무가능목록응답 { Items = items, HasMore = rows.Length > 50 });
    }

    public async Task<Result<근무가능요청응답>> 생성Async(근무가능요청등록요청 request, CancellationToken ct)
    {
        var result = await WriteAsync(async () =>
        {
            if (UserId == "") return Fail<근무가능요청응답>(401, "로그인 후 확인해 주세요.");
            if (!await ManageAsync(request.WarehouseId, ct))
                return Fail<근무가능요청응답>(404, "관리할 창고를 찾을 수 없습니다.");
            var ids = (request.RecipientUserIds ?? []).Select(x => x?.Trim() ?? "").Distinct().Order().ToList();
            if (request.ClientRequestId == Guid.Empty || !request.ConfirmedAvailabilityOnly
                || ids.Count is < 1 or > 50 || ids.Any(x => x == "" || x.Length > 450)
                || request.NeededWorkers is < 1 or > 50 || request.NeededWorkers > ids.Count
                || request.ProposedHourlyAmount is < 0 or > 100000000
                || request.ProposedHourlyAmount is { } amount && decimal.Round(amount, 2) != amount
                || string.IsNullOrWhiteSpace(request.GroupKey) || request.GroupKey.Length > 200
                || request.WorkStartUtc.Kind != DateTimeKind.Utc || request.WorkEndUtc.Kind != DateTimeKind.Utc
                || request.ReplyByUtc.Kind != DateTimeKind.Utc
                || request.WorkEndUtc <= request.WorkStartUtc
                || request.WorkEndUtc - request.WorkStartUtc > TimeSpan.FromHours(24)
                || request.ReplyByUtc > request.WorkStartUtc
                || request.Channel is not (근무연락채널.InApp or 근무연락채널.Kakao))
                return Fail<근무가능요청응답>(400, "대상·필요 인원·업무 시간·답변 기한과 의향 확인 안내를 확인해 주세요.");
            var payload = Hash(JsonSerializer.Serialize(new { request.WarehouseId, request.Stage,
                request.PeriodStartUtc, request.PeriodEndUtc, request.GroupKey, request.GroupPage, request.WorkStartUtc,
                request.WorkEndUtc, request.ReplyByUtc, request.NeededWorkers,
                request.ProposedHourlyAmount, request.Channel, recipients = ids }));
            var existing = await db.근무가능요청.AsNoTracking().SingleOrDefaultAsync(
                x => x.ManagerUserId == UserId && x.ClientRequestId == request.ClientRequestId, ct);
            if (existing is not null) return existing.PayloadHash == payload
                ? Result.Ok(await MapAsync(existing, true, ct))
                : Fail<근무가능요청응답>(409, "같은 요청 ID를 다른 내용에 사용할 수 없습니다.");
            if (request.WorkStartUtc <= Now || request.ReplyByUtc <= Now)
                return Fail<근무가능요청응답>(400, "업무 시작과 답변 기한은 현재 시각 이후여야 합니다.");
            if (request.Channel == 근무연락채널.Kakao && !KakaoReady)
                return Fail<근무가능요청응답>(409, "카카오 인력 요청 발송 연결이 준비되지 않았습니다.");
            var source = await SourceAsync(request, ct);
            if (source.IsFailed) return Result.Fail<근무가능요청응답>(source.Errors);
            var group = source.Value.Workflow?.SelectedGroup;
            if (group is null || group.Key != request.GroupKey || group.NeedsReview
                || !group.MembershipComplete || group.HasOutsideScopeMembers
                || group.CompletedQuantity is not { } completed || completed < 0 || completed >= group.Quantity)
                return Fail<근무가능요청응답>(409, "현재 범위의 미완료 묶음과 수량 근거를 다시 확인해 주세요.");
            var memberIds = await db.창고사용자.AsNoTracking().Where(x => x.창고Id == request.WarehouseId
                && ids.Contains(x.UserId)).Select(x => x.UserId).Distinct().ToArrayAsync(ct);
            var prefs = await db.근무연락동의.Where(x => ids.Contains(x.UserId)).ToArrayAsync(ct);
            var users = await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id))
                .Select(x => new { x.Id, x.UserName, x.PhoneNumberConfirmed, x.PhoneNumber }).ToArrayAsync(ct);
            if (memberIds.Length != ids.Count || ids.Any(id => !prefs.Any(x => x.UserId == id && x.AllowInApp
                && (request.Channel != 근무연락채널.Kakao || x.AllowKakao))
                || !users.Any(x => x.Id == id && (request.Channel != 근무연락채널.Kakao
                    || x.PhoneNumberConfirmed && !string.IsNullOrWhiteSpace(x.PhoneNumber)))))
                return Fail<근무가능요청응답>(409, "같은 창고 등록·현재 연락 동의·필요 연락처 확인을 다시 해 주세요.");
            var key = Hash($"{request.WarehouseId}|{request.Stage}|{request.GroupKey}|{request.WorkStartUtc:O}|{request.WorkEndUtc:O}");
            var active = await db.근무가능요청.SingleOrDefaultAsync(x => x.ActiveKey == key, ct);
            if (active is not null)
            {
                if (active.WorkEndUtc > Now)
                    return Fail<근무가능요청응답>(409, "같은 업무·시간의 요청이 이미 있습니다. 기존 요청의 답변을 확인해 주세요.");
                active.ActiveKey = null; active.Revision = Guid.NewGuid();
                await db.SaveChangesAsync(ct);
            }
            var row = new 근무가능요청Record
            {
                Id = Guid.NewGuid(), ManagerUserId = UserId, ClientRequestId = request.ClientRequestId,
                PayloadHash = payload, ActiveKey = key, WarehouseId = request.WarehouseId,
                WarehouseName = source.Value.WarehouseName, Stage = request.Stage, GroupKey = group.Key,
                GroupLabel = group.Label, RemainingQuantity = group.Quantity - completed,
                GroupPage = request.GroupPage,
                NeededWorkers = request.NeededWorkers, WorkStartUtc = request.WorkStartUtc,
                WorkEndUtc = request.WorkEndUtc, ReplyByUtc = request.ReplyByUtc,
                SourceObservedAtUtc = source.Value.ObservedAtUtc, ProposedHourlyAmount = request.ProposedHourlyAmount,
                PeriodStartUtc = request.PeriodStartUtc, PeriodEndUtc = request.PeriodEndUtc,
                Channel = request.Channel, CreatedAtUtc = Now
            };
            db.근무가능요청.Add(row);
            foreach (var id in ids)
            {
                var target = new 근무가능대상Record
                {
                    Id = Guid.NewGuid(), RequestId = row.Id, UserId = id,
                    DisplayName = SafeName(users.Single(x => x.Id == id).UserName),
                    DeliveryStatus = request.Channel == 근무연락채널.InApp ? "Inbox" : "Uncertain"
                };
                db.근무가능대상.Add(target);
                if (request.Channel == 근무연락채널.Kakao) newKakaoRequests.Add(target.Id);
            }
            await db.SaveChangesAsync(ct);
            return Result.Ok(await MapAsync(row, true, ct));
        }, ct);
        // Persist the intent before external I/O. Replayed requests never retry uncertain delivery.
        if (result.IsSuccess && request.Channel == 근무연락채널.Kakao)
        {
            try { await SendKakaoOnceAsync(result.Value.RequestId, ct); }
            catch (DbUpdateException) { db.ChangeTracker.Clear(); newKakaoRequests.Clear(); }
        }
        return result.IsSuccess ? Result.Ok(await MapAsync(
            await db.근무가능요청.AsNoTracking().SingleAsync(x => x.Id == result.Value.RequestId, ct), true, ct)) : result;
    }

    private async Task SendKakaoOnceAsync(Guid requestId, CancellationToken ct)
    {
        // A durable claim is required; a crash or uncertain result is not automatically resubmitted.
        var rows = await db.근무가능대상.Where(x => x.RequestId == requestId && x.DeliveryStatus == "Uncertain").ToArrayAsync(ct);
        foreach (var target in rows)
        {
            if (target.RespondedAtUtc.HasValue || target.Status != 근무가능상태코드.Pending) continue;
            // Uncertain is terminal by default. Only a newly-created request grants this local attempt below.
            if (!newKakaoRequests.Remove(target.Id)) continue;
            var pref = await db.근무연락동의.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == target.UserId, ct);
            var row = await db.근무가능요청.AsNoTracking().SingleAsync(x => x.Id == requestId, ct);
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == target.UserId, ct);
            if (!KakaoReady || pref is not { AllowKakao: true, AllowInApp: true } || user is null
                || !user.PhoneNumberConfirmed || string.IsNullOrWhiteSpace(user.PhoneNumber)
                || row.ReplyByUtc <= Now || !await db.창고사용자.AnyAsync(x => x.창고Id == row.WarehouseId && x.UserId == target.UserId, ct))
            { target.DeliveryStatus = "NotSent"; await db.SaveChangesAsync(ct); continue; }
            try
            {
                var link = settings.WorkforceReplyBaseUrl.TrimEnd('/') + "/community/work-availability";
                var body = $"{row.WarehouseName} {(row.Stage == "picking" ? "피킹" : "포장")} · 근무 가능 여부 확인. 앱 수신함에서 시간과 조건을 확인해 주세요.";
                var accepted = await kakao.SendAsync(new(user.PhoneNumber, settings.WorkforceAvailabilityTemplateCode,
                    "근무 가능 여부 확인", body, new Dictionary<string, string> { ["replyUrl"] = link,
                        ["requestId"] = row.Id.ToString("D"), ["warehouse"] = row.WarehouseName }), ct);
                target.DeliveryStatus = accepted ? "ProviderAccepted" : "Uncertain";
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            { target.DeliveryStatus = "Uncertain"; }
            target.Revision = Guid.NewGuid();
            await db.SaveChangesAsync(ct);
        }
    }
    private readonly HashSet<Guid> newKakaoRequests = [];

    public Task<Result<근무가능요청응답>> 답변Async(Guid invitationId, 근무가능답변요청 request, bool confirm, CancellationToken ct, long? warehouseId = null)
        => WriteAsync(async () =>
        {
            if (UserId == "") return Fail<근무가능요청응답>(401, "로그인 후 확인해 주세요.");
            var target = await db.근무가능대상.SingleOrDefaultAsync(x => x.Id == invitationId, ct);
            if (target is null) return Fail<근무가능요청응답>(404, "요청을 찾을 수 없습니다.");
            var row = await db.근무가능요청.SingleAsync(x => x.Id == target.RequestId, ct);
            if (confirm ? warehouseId != row.WarehouseId || !await ManageAsync(row.WarehouseId, ct) : target.UserId != UserId)
                return Fail<근무가능요청응답>(404, "요청을 찾을 수 없습니다.");
            var next = confirm ? 근무가능상태코드.Confirmed : request.Status;
            if (next is not (근무가능상태코드.Available or 근무가능상태코드.Unavailable
                or 근무가능상태코드.Withdrawn or 근무가능상태코드.Confirmed))
                return Fail<근무가능요청응답>(400, "가능·불가·철회 중 하나를 선택해 주세요.");
            if (!confirm && next == 근무가능상태코드.Confirmed)
                return Fail<근무가능요청응답>(400, "인력 계획 확정은 관리자가 확인합니다.");
            if (target.Status == next) return Result.Ok(await MapAsync(row, confirm, ct));
            if (target.Revision != request.Revision)
                return Fail<근무가능요청응답>(409, "답변이 바뀌었습니다. 다시 조회해 주세요.");
            if (target.Status == 근무가능상태코드.Withdrawn
                || row.WorkStartUtc <= Now || !confirm && row.ReplyByUtc <= Now && next != 근무가능상태코드.Withdrawn)
                return Fail<근무가능요청응답>(409, "철회되었거나 답변 기한이 지났습니다.");
            if (target.Status == 근무가능상태코드.Confirmed && next != 근무가능상태코드.Withdrawn)
                return Fail<근무가능요청응답>(409, "확정된 의향은 철회로 변경해 주세요.");
            if (next is 근무가능상태코드.Available or 근무가능상태코드.Confirmed)
            {
                var pref = await db.근무연락동의.SingleOrDefaultAsync(x => x.UserId == target.UserId, ct);
                if (pref?.AllowInApp != true || !await db.창고사용자.AnyAsync(x => x.창고Id == row.WarehouseId && x.UserId == target.UserId, ct))
                    return Fail<근무가능요청응답>(409, "현재 수신 동의와 창고 등록을 다시 확인해 주세요.");
                if (await (from recipient in db.근무가능대상
                    join other in db.근무가능요청 on recipient.RequestId equals other.Id
                    where recipient.UserId == target.UserId && recipient.Id != target.Id
                        && recipient.Status == 근무가능상태코드.Confirmed
                        && other.WorkStartUtc < row.WorkEndUtc && row.WorkStartUtc < other.WorkEndUtc
                    select recipient.Id).AnyAsync(ct))
                    return Fail<근무가능요청응답>(409, "이 시간에 이미 확정한 다른 인력 계획이 있습니다.");
                // This shared recipient token also protects confirmation across two different requests.
                pref.Revision = Guid.NewGuid();
            }
            if (confirm)
            {
                if (target.Status != 근무가능상태코드.Available || row.ConfirmedWorkers >= row.NeededWorkers)
                    return Fail<근무가능요청응답>(409, "현재 가능한 답변과 남은 필요 인원을 확인해 주세요.");
                row.ConfirmedWorkers++;
            }
            else if (target.Status == 근무가능상태코드.Confirmed) row.ConfirmedWorkers--;
            target.Status = next; target.RespondedAtUtc = Now; target.Revision = Guid.NewGuid();
            row.Revision = Guid.NewGuid();
            await db.SaveChangesAsync(ct);
            return Result.Ok(await MapAsync(row, confirm, ct));
        }, ct);

    private async Task<근무가능요청응답> MapAsync(근무가능요청Record row, bool manager, CancellationToken ct)
    {
        var query = db.근무가능대상.AsNoTracking().Where(x => x.RequestId == row.Id);
        if (!manager) query = query.Where(x => x.UserId == UserId);
        var targets = await query.OrderBy(x => x.Id).ToArrayAsync(ct);
        return new()
        {
            RequestId = row.Id, WarehouseId = row.WarehouseId, WarehouseName = row.WarehouseName,
            Stage = row.Stage, GroupKey = row.GroupKey, GroupLabel = row.GroupLabel,
            GroupPage = row.GroupPage,
            RemainingQuantity = row.RemainingQuantity, NeededWorkers = row.NeededWorkers,
            ConfirmedWorkers = row.ConfirmedWorkers, WorkStartUtc = Utc(row.WorkStartUtc),
            WorkEndUtc = Utc(row.WorkEndUtc), ReplyByUtc = Utc(row.ReplyByUtc),
            SourceObservedAtUtc = Utc(row.SourceObservedAtUtc), ProposedHourlyAmount = row.ProposedHourlyAmount,
            PeriodStartUtc = Utc(row.PeriodStartUtc), PeriodEndUtc = Utc(row.PeriodEndUtc),
            Channel = row.Channel, Expired = row.ReplyByUtc <= Now,
            Recipients = targets.Select(x => new 근무가능대상응답
            {
                InvitationId = x.Id, DisplayName = manager ? x.DisplayName : "내 답변",
                Status = x.Status, Revision = x.Revision, DeliveryStatus = x.DeliveryStatus,
                RespondedAtUtc = x.RespondedAtUtc.HasValue ? Utc(x.RespondedAtUtc.Value) : null,
                CanReply = !manager && row.ReplyByUtc > Now && row.WorkStartUtc > Now
                    && x.Status is 근무가능상태코드.Pending or 근무가능상태코드.Available or 근무가능상태코드.Unavailable,
                CanWithdraw = !manager && row.WorkStartUtc > Now && x.Status != 근무가능상태코드.Withdrawn,
                CanConfirm = manager && row.WorkStartUtc > Now && row.ConfirmedWorkers < row.NeededWorkers
                    && x.Status == 근무가능상태코드.Available
            }).ToArray()
        };
    }

    private async Task<Result<T>> WriteAsync<T>(Func<Task<Result<T>>> action, CancellationToken ct)
    {
        try
        {
            if (!db.Database.IsRelational())
            {
                var localResult = await action();
                if (localResult.IsFailed) { db.ChangeTracker.Clear(); newKakaoRequests.Clear(); }
                return localResult;
            }
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var result = await action();
                if (result.IsSuccess) await tx.CommitAsync(ct);
                else { db.ChangeTracker.Clear(); newKakaoRequests.Clear(); }
                return result;
            });
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            newKakaoRequests.Clear();
            return Fail<T>(409, "동시 변경이 있었습니다. 다시 조회 후 같은 요청을 확인해 주세요.");
        }
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static string SafeName(string? value) => string.IsNullOrWhiteSpace(value) || value.Contains('@')
        || value.Count(char.IsDigit) >= 9 ? "등록 작업자" : value[..Math.Min(value.Length, 100)];
}
