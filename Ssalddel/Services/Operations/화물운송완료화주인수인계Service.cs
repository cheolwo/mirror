using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Versioning;
using 살뜰.Data;
using 살뜰.Services.Operations;
using 살뜰.도메인.공통;

namespace Ssalddel.Services.Operations;

public static class 화물운송완료화주인수인계Contract
{
    public const string 출발업무유형Code = "CargoTransportCompletion";
    public const string 인계계약Code = OperatingSystemInteractionContractCodes.DomesticCargoCompletionToShipperAcceptance;
    public const string 인계계약Revision = OperatingSystemInteractionContractRevisions.DomesticCargoCompletionToShipperAcceptance;

    public static string 출발업무StableId(long 운송Id)
        => $"cargo-transport-completion:{운송Id}";

    public static string 도착업무StableId(string 운송의뢰Id)
        => $"shipper-delivery-acceptance:{운송의뢰Id.Trim()}";
}

public interface I화물운송완료화주인수인계Service
{
    Task<운영체제업무인계Dto> 완료결과요청Async(
        long 운송Id,
        bool 하차완료증빙확인여부,
        CancellationToken cancellationToken = default);

    Task<운영체제업무인계Dto?> 화주인수Async(
        string 운송의뢰Id,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 화물운송 OS의 완료 결과를 화주 운송관리 OS의 인수·검수 단계로 돌려보냅니다.
/// 하차 증빙의 존재와 완료 시각만 전달하며 사진 경로, 주소, 연락처와 위치는 복제하지 않습니다.
/// </summary>
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.OperationalLogisticsOs,
    SsalddelCodeLayer.Application,
    "화물 운송 완료와 증빙 존재를 화주 인수·검수 책임으로 멱등 반환한다.",
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    FlowOrder = 75,
    StepKey = "application.cargo-completion-shipper-acceptance-handoff",
    ExecutionStage = SsalddelCodeExecutionStage.Confirm,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "운송 완료를 다시 확정하거나 정산·재위탁을 자동 실행하지 않는다. 화주 인수증 등록 전에는 화물운송 OS가 완료 결과 인계 책임을 유지한다.")]
public sealed class 화물운송완료화주인수인계Service(
    SsalddelContext db,
    I운영체제업무인계Coordinator handoffCoordinator,
    TimeProvider timeProvider) : I화물운송완료화주인수인계Service
{
    public async Task<운영체제업무인계Dto> 완료결과요청Async(
        long 운송Id,
        bool 하차완료증빙확인여부,
        CancellationToken cancellationToken = default)
    {
        if (운송Id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(운송Id), "운송 ID가 필요합니다.");
        }
        if (!하차완료증빙확인여부)
        {
            throw new InvalidOperationException("하차 완료 증빙이 확인되어야 화주 인수 단계로 반환할 수 있습니다.");
        }

        var transport = await db.운송원장
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == 운송Id, cancellationToken)
            ?? throw new KeyNotFoundException("화주 인수 단계로 반환할 운송 원장을 찾을 수 없습니다.");
        if (!string.Equals(transport.상태, 상태값.배차상태.인수완료, StringComparison.Ordinal)
            || transport.도착 is null)
        {
            throw new InvalidOperationException("인수 완료 시각이 기록된 화물 운송만 화주 인수 단계로 반환할 수 있습니다.");
        }

        var requestId = string.IsNullOrWhiteSpace(transport.의뢰Id)
            ? transport.운송번호.Trim()
            : transport.의뢰Id.Trim();
        if (string.IsNullOrWhiteSpace(requestId)
            || !await db.화주운송의뢰.AsNoTracking().AnyAsync(x => x.의뢰Id == requestId, cancellationToken))
        {
            throw new KeyNotFoundException("완료 운송에 연결된 화주 운송의뢰를 찾을 수 없습니다.");
        }

        var completedAtUtc = AsUtc(transport.도착.Value);
        var sourceRevision = completedAtUtc.Ticks;
        var handoffRequestId = DeterministicGuid($"cargo-transport-completion:{운송Id}:shipper-acceptance:request:v1");
        var handoffStableId = $"os-handoff:{handoffRequestId:N}";
        var handoff = await handoffCoordinator.조회Async(handoffStableId, cancellationToken);
        if (handoff is not null)
        {
            기존인계검증(handoff, transport.Id, requestId, sourceRevision);
            return handoff;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        return await handoffCoordinator.요청Async(new 운영체제업무인계생성요청
        {
            클라이언트요청Id = handoffRequestId,
            출발운영체제Id = OperatingSystemIds.DomesticCargoTransport,
            도착운영체제Id = OperatingSystemIds.ShipperTransportManagement,
            출발업무유형Code = 화물운송완료화주인수인계Contract.출발업무유형Code,
            출발업무StableId = 화물운송완료화주인수인계Contract.출발업무StableId(transport.Id),
            출발업무Revision = sourceRevision,
            인계계약Code = 화물운송완료화주인수인계Contract.인계계약Code,
            인계계약Revision = 화물운송완료화주인수인계Contract.인계계약Revision,
            최소상태사본Json = JsonSerializer.Serialize(new
            {
                cargoRequestStableId = 화주운송의뢰화물운송인계Contract.도착업무StableId(requestId),
                transportExecutionStableId = $"transport-execution:{transport.Id}",
                shipperAcceptanceStableId = 화물운송완료화주인수인계Contract.도착업무StableId(requestId),
                completionStatus = transport.상태,
                completedAtUtc,
                evidenceKind = "dropoff-complete-photo",
                evidenceRegistered = true,
                cargoStageId = OperatingSystemLifecycleStageIds.CargoEvidenceSettlement,
                shipperStageId = OperatingSystemLifecycleStageIds.ShipperDeliveryAcceptance
            }),
            만료시각Utc = now.AddDays(30)
        }, cancellationToken);
    }

    public async Task<운영체제업무인계Dto?> 화주인수Async(
        string 운송의뢰Id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(운송의뢰Id);
        var requestId = 운송의뢰Id.Trim();
        var transport = await db.운송원장
            .AsNoTracking()
            .Where(x => x.의뢰Id == requestId
                        || (x.의뢰Id == string.Empty && x.운송번호 == requestId))
            .OrderByDescending(x => x.도착)
            .ThenByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (transport is null)
        {
            return null;
        }

        var handoffRequestId = DeterministicGuid($"cargo-transport-completion:{transport.Id}:shipper-acceptance:request:v1");
        var handoff = await handoffCoordinator.조회Async($"os-handoff:{handoffRequestId:N}", cancellationToken);
        if (handoff is null)
        {
            return null;
        }

        var sourceRevision = AsUtc(transport.도착 ?? transport.UpdatedAt).Ticks;
        기존인계검증(handoff, transport.Id, requestId, sourceRevision);
        if (handoff.상태Code == 운영체제업무인계상태Codes.수락됨
            || handoff.상태Code is 운영체제업무인계상태Codes.거절됨 or 운영체제업무인계상태Codes.만료됨)
        {
            return handoff;
        }

        return await handoffCoordinator.결정Async(
            handoff.인계StableId,
            new 운영체제업무인계결정요청
            {
                클라이언트요청Id = DeterministicGuid($"cargo-transport-completion:{transport.Id}:shipper-acceptance:accept:v1"),
                예상Revision = handoff.Revision,
                응답운영체제Id = OperatingSystemIds.ShipperTransportManagement,
                결정Code = 운영체제업무인계결정Codes.수락,
                도착업무StableId = 화물운송완료화주인수인계Contract.도착업무StableId(requestId)
            },
            cancellationToken);
    }

    private static void 기존인계검증(
        운영체제업무인계Dto handoff,
        long transportId,
        string requestId,
        long sourceRevision)
    {
        if (handoff.출발운영체제Id != OperatingSystemIds.DomesticCargoTransport
            || handoff.도착운영체제Id != OperatingSystemIds.ShipperTransportManagement
            || handoff.출발업무유형Code != 화물운송완료화주인수인계Contract.출발업무유형Code
            || handoff.출발업무StableId != 화물운송완료화주인수인계Contract.출발업무StableId(transportId)
            || handoff.출발업무Revision != sourceRevision
            || handoff.인계계약Code != 화물운송완료화주인수인계Contract.인계계약Code
            || handoff.인계계약Revision != 화물운송완료화주인수인계Contract.인계계약Revision
            || (!string.IsNullOrWhiteSpace(handoff.도착업무StableId)
                && handoff.도착업무StableId != 화물운송완료화주인수인계Contract.도착업무StableId(requestId)))
        {
            throw new InvalidOperationException("기존 화물 운송 완료 인계가 현재 운송·판본·계약과 일치하지 않습니다.");
        }
    }

    private static DateTime AsUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static Guid DeterministicGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var bytes = new byte[16];
        Array.Copy(hash, bytes, bytes.Length);
        return new Guid(bytes);
    }
}
