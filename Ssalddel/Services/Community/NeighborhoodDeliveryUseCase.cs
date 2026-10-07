using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Infrastructure.BackgroundJobs;
using Ssalddel.Services.Privacy;
using 살뜰.Data;
using 살뜰.Services.External.Google;
using 살뜰.도메인.공통;

namespace Ssalddel.Services.Community;

public interface I생활배송의뢰UseCase
{
    Task<Result<NeighborhoodDeliveryQuoteResponse>> 견적Async(NeighborhoodDeliveryRequest request, CancellationToken cancellationToken = default);
    Task<Result<NeighborhoodDeliveryResponse>> 등록Async(NeighborhoodDeliveryRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NeighborhoodDeliveryResponse>> 내목록Async(int page = 1, CancellationToken cancellationToken = default);
    Task<NeighborhoodDeliveryResponse?> 내상세Async(string requestId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 생활 교류 글과 구분되는 배송 의뢰를 기존 화주 원장과 현장지급 배차 큐로 연결합니다.
/// 자동 배차는 기존 서버 작업의 실행·기능 게이트를 따르며 기사 수락·수금·입금을 확정하지 않습니다.
/// </summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalLogisticsOs, SsalddelCodeLayer.Application,
    "일반 인증 사용자의 명시적인 생활 화물 배송 의뢰를 기존 운송 원장·직접지급 배차 큐에 연결한다.",
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    StepKey = "application.neighborhood-delivery-request", FlowOrder = 30,
    ReadsFrom = SsalddelCodeDataScope.OperationalState, WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "교류 글은 선택 출처다. 음식 원장·기사 수락·실수금·자동 배차 실행 게이트를 대신하지 않는다.")]
public sealed class 생활배송의뢰UseCase(
    SsalddelContext db,
    ICurrentUserAccessor currentUser,
    I화주운송의뢰UseCase transport,
    I신청개인정보동의증적Service consent,
    IGeocodingService geocoding,
    ISsalddelBackgroundJobActivationPolicy activation,
    I생활배송기사정보제공동의Service? driverDisclosure = null, I생활배송협업Guard? collaborationGuard = null,
    Ssalddel.Services.Commerce.I통신판매거래Guard? commerce = null) : I생활배송의뢰UseCase
{
    internal const string ClientRequestPrefix = NeighborhoodDeliveryRoutes.ClientRequestPrefix;
    private const string MemoPrefix = "neighborhood-delivery:v1:";
    // 기존 client-request 원장을 재사용한다. 한 서버의 동시 재송신을 제한하며 lock 수를 고정한다.
    // 여러 서버의 중복 저장은 SQL의 unique 생활배송접수키와 결정적 의뢰 ID로 제한한다.
    private static readonly SemaphoreSlim[] RegistrationGates = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public async Task<Result<NeighborhoodDeliveryQuoteResponse>> 견적Async(NeighborhoodDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await 입력검사Async(request, submitting: false, cancellationToken);
        if (validation.IsFailed) return Result.Fail<NeighborhoodDeliveryQuoteResponse>(validation.Errors);
        var resolved = await 위치견적Async(request, cancellationToken);
        if (resolved.IsFailed) return Result.Fail<NeighborhoodDeliveryQuoteResponse>(resolved.Errors);
        var active = activation.Evaluate(SsalddelBackgroundWorkloadKeys.DomesticTransportDispatch);
        return Result.Ok(new NeighborhoodDeliveryQuoteResponse
        {
            Fare = resolved.Value.Fare,
            AutomaticDispatchEnabled = active.IsEnabled,
            AutomaticDispatchBlockCode = active.IsEnabled ? string.Empty : active.Code
        });
    }

    public async Task<Result<NeighborhoodDeliveryResponse>> 등록Async(NeighborhoodDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await 입력검사Async(request, submitting: true, cancellationToken, validateSource: false);
        if (validation.IsFailed) return Result.Fail<NeighborhoodDeliveryResponse>(validation.Errors);
        var actor = currentUser.UserId!;
        var clientId = ClientRequestPrefix + request.ClientRequestId.ToString("N");
        var legacyMarker = MemoPrefix + Fingerprint(request) + ":consent:" + request.PrivacyConsentEvidenceId!.Value.ToString("N");
        var marker = request.SourcePostId is > 0
            ? legacyMarker + 생활배송출처Evidence.Marker + request.SourcePostId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : legacyMarker;
        marker += 생활배송배차Policy.신청메모(request);
        var gateHash = SHA256.HashData(Encoding.UTF8.GetBytes(actor + ":" + clientId));
        var gate = RegistrationGates[gateHash[0] % RegistrationGates.Length];
        await gate.WaitAsync(cancellationToken);
        try
        {
            var existing = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(
                x => x.주문자UserId == actor && x.클라이언트요청Id == clientId, cancellationToken);
            string requestId;
            var replay = existing is not null;
            if (existing is not null)
            {
                if (!string.Equals(existing.정산메모, marker, StringComparison.Ordinal)
                    && !string.Equals(existing.정산메모, legacyMarker, StringComparison.Ordinal))
                    return Fail<NeighborhoodDeliveryResponse>("IdempotencyConflict", "같은 요청 번호로 배송 내용을 바꿀 수 없습니다. 기존 의뢰를 확인해 주세요.");
                requestId = existing.의뢰Id;
            }
            else
            {
                if (commerce is not null)
                {
                    try { await commerce.요구Async(actor, null, request.CommerceProtection, "neighborhood-delivery", request.ClientRequestId.ToString("N"), cancellationToken); }
                    catch (Ssalddel.Services.Commerce.거래보호Exception ex) { return Fail<NeighborhoodDeliveryResponse>(ex.Code, ex.Message); }
                }
                var source = await 출처검사Async(request.SourcePostId, cancellationToken);
                if (source.IsFailed) return Result.Fail<NeighborhoodDeliveryResponse>(source.Errors);
                var resolved = await 위치견적Async(request, cancellationToken);
                if (resolved.IsFailed) return Result.Fail<NeighborhoodDeliveryResponse>(resolved.Errors);
                if (request.AgreedFareKrw != resolved.Value.Fare.최종운임)
                    return Fail<NeighborhoodDeliveryResponse>("QuoteChanged", "서버 견적이 바뀌었습니다. 배송비를 다시 확인해 주세요.");
                if (request.CollaborationId is not null)
                {
                    if (collaborationGuard is null) return Fail<NeighborhoodDeliveryResponse>("CollaborationUnavailable", "배송 합의 연결을 확인할 수 없습니다.");
                    var held = await collaborationGuard.획득Async(request, actor, Fingerprint(request), cancellationToken);
                    if (held.IsFailed) return Result.Fail<NeighborhoodDeliveryResponse>(held.Errors);
                }
                var payload = ToTransportRequest(request, clientId, marker, resolved.Value);
                var created = await transport.의뢰생성Async(payload, cancellationToken);
                if (created.IsFailed) return Result.Fail<NeighborhoodDeliveryResponse>(created.Errors);
                requestId = created.Value.의뢰Id;
            }

            var canonical = await transport.의뢰단건조회Async(requestId, cancellationToken);
            if (canonical is null) return Fail<NeighborhoodDeliveryResponse>("RequestNotFound", "본인 배송 의뢰를 다시 조회할 수 없습니다.");

            // 응답 유실·원장 저장 뒤 큐 접수 실패는 같은 요청으로 재개한다. 취소/확정 뒤에는 다시 배차하지 않는다.
            var queueExists = await db.운송원장.AsNoTracking().AnyAsync(x => x.의뢰Id == requestId, cancellationToken);
            if (canonical.의뢰상태 == 상태값.의뢰상태.생성됨
                && (canonical.배차상태 == 상태값.배차상태.미시작 || !queueExists && canonical.배차상태 == 상태값.배차상태.매칭중))
            {
                var queued = await transport.현장지급처리Async(requestId, new 화주운송의뢰현장지급처리요청
                {
                    현장지급메모 = "생활 배송: 기사에게 직접 현장 지급하기로 확인함. 수금·입금 미확인."
                }, cancellationToken);
                if (queued.IsFailed) return Result.Fail<NeighborhoodDeliveryResponse>(queued.Errors);
            }

            if (request.CollaborationId is not null)
            {
                if (collaborationGuard is null) return Fail<NeighborhoodDeliveryResponse>("CollaborationUnavailable", "배송 합의 연결을 확인할 수 없습니다.");
                var linked = await collaborationGuard.연결확정Async(request, actor, requestId, cancellationToken);
                if (linked.IsFailed) return Result.Fail<NeighborhoodDeliveryResponse>(linked.Errors);
            }
            if (request.DispatchMode is not null)
            {
                var pendingQueue = await db.운송원장.SingleAsync(x => x.의뢰Id == requestId, cancellationToken);
                // Mongo projection runs in a separate scope and updates SQL row_version during handoff.
                await db.Entry(pendingQueue).ReloadAsync(cancellationToken);
                if (!pendingQueue.생활배송수락준비완료 && pendingQueue.배차큐단계 != 상태값.배차큐단계.종료 && pendingQueue.확정기사Id is null)
                {
                    pendingQueue.생활배송수락준비완료 = true; pendingQueue.생활배송선택판본++;
                    생활배송배차Policy.대기적용(pendingQueue);
                    await db.SaveChangesAsync(cancellationToken);
                }
            }
            var result = await 내상세Async(requestId, cancellationToken);
            if (result is null) return Fail<NeighborhoodDeliveryResponse>("RequestNotFound", "저장한 배송 의뢰를 다시 조회할 수 없습니다.");
            result.IdempotentReplay = replay;
            return Result.Ok(result);
        }
        catch (DbUpdateException)
        {
            // A competing server may own the unique key. Preserve the original request for canonical retry.
            db.ChangeTracker.Clear();
            return Fail<NeighborhoodDeliveryResponse>("RegistrationPending", "접수 저장 결과를 확인해 주세요. 원 요청 번호로 다시 조회·접수하면 중복 의뢰를 만들지 않습니다.");
        }
        finally { gate.Release(); }
    }

    public async Task<IReadOnlyList<NeighborhoodDeliveryResponse>> 내목록Async(int page = 1, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId)) return Array.Empty<NeighborhoodDeliveryResponse>();
        var actor = currentUser.UserId;
        var ids = await db.화주운송의뢰.AsNoTracking()
            .Where(x => x.주문자UserId == actor && x.클라이언트요청Id.StartsWith(ClientRequestPrefix))
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.의뢰Id)
            .Skip((Math.Clamp(page, 1, 10000) - 1) * 20).Take(20).Select(x => x.의뢰Id).ToListAsync(cancellationToken);
        var items = new List<NeighborhoodDeliveryResponse>();
        foreach (var id in ids)
            if (await 내상세Async(id, cancellationToken) is { } item) items.Add(item);
        return items;
    }

    public async Task<NeighborhoodDeliveryResponse?> 내상세Async(string requestId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId) || string.IsNullOrWhiteSpace(requestId)) return null;
        var actor = currentUser.UserId;
        var source = await db.화주운송의뢰.AsNoTracking().Where(x => x.의뢰Id == requestId
                && x.주문자UserId == actor && x.클라이언트요청Id.StartsWith(ClientRequestPrefix))
            .Select(x => new { x.정산메모 }).SingleOrDefaultAsync(cancellationToken);
        if (source is null) return null;
        var item = await transport.의뢰단건조회Async(requestId, cancellationToken);
        if (item is null) return null;
        var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
        var active = activation.Evaluate(SsalddelBackgroundWorkloadKeys.DomesticTransportDispatch);
        var proposal = item.의뢰상태 != 상태값.의뢰상태.생성됨 || queue?.배차큐단계 == 상태값.배차큐단계.종료
            ? NeighborhoodDeliveryProposalStates.Closed
            : queue is null || !queue.생활배송수락준비완료 ? NeighborhoodDeliveryProposalStates.RegistrationPending
            : !string.IsNullOrWhiteSpace(queue?.확정기사Id) ? NeighborhoodDeliveryProposalStates.Assigned
            : queue?.배차노출상태 == 상태값.배차노출상태.추천중 && !string.IsNullOrWhiteSpace(queue.현재추천대상기사Id)
                ? NeighborhoodDeliveryProposalStates.Proposed
            : !active.IsEnabled && queue?.생활배송배차방식 is not (NeighborhoodDispatchModes.PublicCall or NeighborhoodDispatchModes.Hybrid) ? NeighborhoodDeliveryProposalStates.AwaitingActivation : NeighborhoodDeliveryProposalStates.Queued;
        return new NeighborhoodDeliveryResponse
        {
            DispatchMode = queue?.생활배송배차방식, DispatchRevision = queue?.생활배송선택판본 ?? 0,
            CollaborationId = queue?.생활배송협업Id,
            CanChangeDispatchMode = queue?.생활배송배차방식 is not null && queue.생활배송수락준비완료
                && queue.확정기사Id is null && queue.상태 == 상태값.배차대기상태.대기 && queue.배차큐단계 != 상태값.배차큐단계.종료,
            RequestId = item.의뢰Id, SourcePostId = 생활배송출처Evidence.Read(source.정산메모),
            CargoName = item.화물?.화물종류 ?? item.요약?.화물종류 ?? string.Empty,
            CreatedAtUtc = item.생성일시, RequestStatusCode = item.의뢰상태, DispatchStatusCode = item.배차상태,
            QueueStageCode = queue?.배차큐단계.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty, ProposalStateCode = proposal,
            AutomaticDispatchEnabled = active.IsEnabled, AutomaticDispatchBlockCode = active.IsEnabled ? string.Empty : active.Code,
            FareKrw = item.최종운임, DistanceKm = item.요금옵션?.예상거리Km,
            DistanceBasis = item.요금옵션?.거리계산방식, RateSource = item.요금옵션?.단가출처,
            PaymentStatusCode = item.결제상태, SettlementStatusCode = item.정산상태, Request = item,
            DriverDisclosure = driverDisclosure is null ? null : await driverDisclosure.내상태Async(requestId, cancellationToken)
        };
    }

    private async Task<Result> 입력검사Async(NeighborhoodDeliveryRequest? request, bool submitting, CancellationToken cancellationToken, bool validateSource = true)
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId)) return Fail("AuthenticationRequired", "배송 의뢰는 로그인 후 작성할 수 있습니다.");
        if (request?.DispatchMode is not null && !NeighborhoodDispatchModes.IsKnown(request.DispatchMode)) return Fail("InvalidDispatchChoice", "배차 방식을 확인해 주세요.");
        if (request?.CollaborationId is not null && (string.IsNullOrWhiteSpace(request.CollaborationId) || request.CollaborationId.Length > 100
            || request.ExpectedTermsRevision is null or < 1 || request.DispatchMode is null)) return Fail("AgreementRequired", "현재 협업 조건과 배차 방식을 확인해 주세요.");
        if (request?.CollaborationId is null && request?.ExpectedTermsRevision is not null) return Fail("AgreementRequired", "협업 조건 연결을 확인해 주세요.");
        if (request is null || request.ClientRequestId == Guid.Empty) return Fail("InvalidDeliveryRequest", "배송 요청 번호가 필요합니다.");
        if (string.IsNullOrWhiteSpace(request.CargoName) || request.CargoName.Length > 120
            || request.Quantity is < 1 or > 1000 || request.WeightKg is <= 0 or > 10000
            || string.IsNullOrWhiteSpace(request.VehicleType) || request.VehicleType.Length > 60 || request.Notes?.Length > 1000)
            return Fail("InvalidDeliveryRequest", "물품명·수량·중량과 차량을 확인해 주세요.");
        if (!LocationValid(request.Pickup, pickup: true) || !LocationValid(request.Dropoff, pickup: false))
            return Fail("InvalidDeliveryLocation", "픽업·전달 주소와 담당자 연락처, 픽업 시간창을 확인해 주세요.");
        if (request.PrivacyConsentSourceCode != NeighborhoodDeliveryRoutes.PrivacyConsentSourceCode
            || request.PrivacyConsentEvidenceId is null || request.PrivacyConsentEvidenceId == Guid.Empty)
            return Fail("PrivacyConsentRequired", "생활 배송 신청의 개인정보 수집·이용 동의가 필요합니다.");
        try
        {
            await consent.유효한동의요구Async(request.PrivacyConsentEvidenceId, 신청개인정보업무Codes.운송대행,
                NeighborhoodDeliveryRoutes.PrivacyConsentSourceCode, currentUser.UserId!, cancellationToken);
        }
        catch (InvalidOperationException) { return Fail("PrivacyConsentRequired", "현재 신청에 사용할 수 있는 개인정보 동의를 다시 확인해 주세요."); }
        if (submitting && (!request.DispatchRequested || !request.DirectPaymentAgreed || request.AgreedFareKrw is null or < 0))
            return Fail("DeliveryAgreementRequired", "배송 의뢰와 기사에게 직접 지급할 배송비를 확인해 주세요.");
        return validateSource ? await 출처검사Async(request.SourcePostId, cancellationToken) : Result.Ok();
    }

    private async Task<Result> 출처검사Async(long? sourcePostId, CancellationToken cancellationToken)
    {
        if (sourcePostId.HasValue)
        {
            var sourceId = sourcePostId.Value;
            if (!await db.PlatformCommunityPosts.AsNoTracking().AnyAsync(x => x.Id == sourceId && !x.IsDeleted
                    && x.PublicationStatusCode == Ssalddel.Domain.Community.PlatformCommunityPostPublicationStatusCodes.Published && !x.IsReportBoardPost
                    && x.Category == PlatformCommunityPostCategories.General && x.WorkflowTag == NeighborhoodExchange.WorkflowTag
                    && (x.RoleTag == NeighborhoodExchange.Offer || x.RoleTag == NeighborhoodExchange.Need), cancellationToken))
                return Fail("SourcePostUnavailable", "연결할 생활 교류 글을 확인할 수 없습니다.");
        }
        return Result.Ok();
    }

    private async Task<Result<ResolvedDelivery>> 위치견적Async(NeighborhoodDeliveryRequest request, CancellationToken cancellationToken)
    {
        var pickup = JsonSerializer.Deserialize<LocationContactDTO>(JsonSerializer.Serialize(request.Pickup))!;
        var dropoff = JsonSerializer.Deserialize<LocationContactDTO>(JsonSerializer.Serialize(request.Dropoff))!;
        foreach (var location in new[] { pickup, dropoff })
        {
            cancellationToken.ThrowIfCancellationRequested();
            // 입력 좌표는 가격 권위가 아니다. 기존 생성 Handler와 같은 주소 질의로 서버에서 해결한다.
            var address = location.주소.도로명주소;
            if (!string.IsNullOrWhiteSpace(location.주소.상세주소)) address += " " + location.주소.상세주소;
            var point = await geocoding.GeocodeAsync(address);
            if (!point.HasValue) return Fail<ResolvedDelivery>("AddressNotResolved", "주소 위치를 확인할 수 없습니다. 도로명 주소를 다시 확인해 주세요.");
            location.주소.위도 = point.Value.lat;
            location.주소.경도 = point.Value.lng;
            if (location.주소.위도 is < -90 or > 90 || location.주소.경도 is < -180 or > 180)
                return Fail<ResolvedDelivery>("AddressNotResolved", "주소 위치가 유효하지 않습니다.");
        }
        var result = await transport.기준운임견적Async(new 화주운송기준운임견적요청
        {
            차량종류 = request.VehicleType.Trim(), 상차위도 = pickup.주소.위도, 상차경도 = pickup.주소.경도,
            하차위도 = dropoff.주소.위도, 하차경도 = dropoff.주소.경도
        }, cancellationToken);
        return result.IsFailed ? Result.Fail<ResolvedDelivery>(result.Errors)
            : Result.Ok(new ResolvedDelivery(pickup, dropoff, result.Value));
    }

    private static 화주운송의뢰생성요청 ToTransportRequest(NeighborhoodDeliveryRequest request, string clientId, string marker, ResolvedDelivery resolved)
        => new()
        {
            화주Id = null, 운송방식 = "직송", 차량종류 = request.VehicleType.Trim(), 클라이언트요청Id = clientId,
            신청개인정보동의증적Id = request.PrivacyConsentEvidenceId, 신청출처Code = NeighborhoodDeliveryRoutes.PrivacyConsentSourceCode,
            화물 = new CargoDTO { 화물종류 = request.CargoName.Trim(), 수량 = request.Quantity, 중량Kg = request.WeightKg, 온도조건 = "상온" },
            픽업 = resolved.Pickup, 하차 = resolved.Dropoff,
            결제수단 = 결제수단.현금.ToString(), 결제상태 = 상태값.결제상태.결제대기,
            정산조건 = new 화주운송정산조건DTO { 정산시점 = 정산시점.현장지급, 결제수단 = 결제수단.현금, 수납주체 = 수납주체.기사, 정산메모 = marker },
            요금옵션 = new PricingDTO { 요청사항 = request.Notes?.Trim(), 기사지급예정운임 = resolved.Fare.최종운임 }
        };

    private static bool LocationValid(LocationContactDTO? location, bool pickup)
    {
        if (location?.주소 is null || location.연락처 is null
            || string.IsNullOrWhiteSpace(location.주소.도로명주소) || location.주소.도로명주소.Length > 300
            || location.주소.상세주소?.Length > 300 || string.IsNullOrWhiteSpace(location.연락처.이름)
            || location.연락처.이름.Length > 100 || string.IsNullOrWhiteSpace(location.연락처.전화번호)
            || location.연락처.전화번호.Length > 40) return false;
        if (pickup && (location.시간창 is null || location.시간창.시작일시 == default || location.시간창.종료일시 == default)) return false;
        return location.시간창 is null || location.시간창.시작일시 < location.시간창.종료일시;
    }

    private static string Fingerprint(NeighborhoodDeliveryRequest request)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
    private static Error Failure(string code, string message) => new Error(message)
        .WithMetadata("ErrorCode", code).WithMetadata("StatusCode", code switch
        {
            "AuthenticationRequired" => 401,
            "RequestNotFound" => 404,
            "IdempotencyConflict" or "QuoteChanged" or "RegistrationPending" => 409,
            _ => 400
        });
    private static Result<T> Fail<T>(string code, string message) => Result.Fail<T>(Failure(code, message));
    private static Result Fail(string code, string message) => Result.Fail(Failure(code, message));
    private sealed record ResolvedDelivery(LocationContactDTO Pickup, LocationContactDTO Dropoff, 화주운송기준운임견적응답 Fare);
}
