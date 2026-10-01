using System.Globalization;
using Hongdal.Contracts.Common.Community;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using 홍달.Data;
using 홍달.도메인.공통;
using 홍달.도메인.운송;
using 홍달.도메인.화주;

namespace Hongdal.Services.Community;

public sealed class 운송원장업무투영Handler : I원장업무투영동기화Handler
{
    private readonly HongdalContext _db;
    private readonly ILogger<운송원장업무투영Handler> _logger;

    public 운송원장업무투영Handler(
        HongdalContext db,
        ILogger<운송원장업무투영Handler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public bool 처리대상인가(커뮤니티원장Dto 원장)
        => 운송원장업무투영Snapshot.처리대상인가(원장);

    public async Task 동기화Async(커뮤니티원장Dto 원장, CancellationToken cancellationToken = default)
    {
        var snapshot = 운송원장업무투영Snapshot.생성(원장);
        if (snapshot is null)
        {
            return;
        }

        if (snapshot.IsCargoRequest && !snapshot.IsTransportRequestComplete)
        {
            _logger.LogWarning(
                "커뮤니티 운송 의뢰 노드의 필수 입력이 부족해 RDB 투영을 보류합니다. 원장Id={원장Id}, 누락={누락}",
                원장.원장Id,
                string.Join(", ", snapshot.MissingRequiredFields));
            return;
        }

        var transport = await _db.운송원장
            .FirstOrDefaultAsync(x => x.의뢰Id == snapshot.RequestId || x.운송번호 == snapshot.RequestId, cancellationToken);

        var isNew = transport is null;
        if (transport is null)
        {
            if (!snapshot.CanCreateCoordinationTransport)
            {
                _logger.LogInformation(
                    "참여자 실행 흔적이 있는 커뮤니티 원장은 신규 RDB 배차로 만들지 않습니다. 원장Id={원장Id}, 의뢰Id={의뢰Id}",
                    snapshot.LedgerId,
                    snapshot.RequestId);
                return;
            }

            transport = CreateCoordinationTransport(snapshot, DateTime.UtcNow);
            _db.운송원장.Add(transport);
        }

        var changed = isNew;
        changed |= ApplyTransportProjection(transport, snapshot, isNew);

        var shipperRequest = await _db.화주운송의뢰
            .FirstOrDefaultAsync(x => x.의뢰Id == snapshot.RequestId, cancellationToken);

        if (shipperRequest is null && snapshot.IsCargoRequest)
        {
            shipperRequest = new 화주운송의뢰
            {
                의뢰Id = snapshot.RequestId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.화주운송의뢰.Add(shipperRequest);
            changed = true;
        }

        if (shipperRequest is not null)
        {
            changed |= ApplyShipperRequest(shipperRequest, snapshot);
        }
        else if (!isNew)
        {
            _logger.LogDebug(
                "커뮤니티 원장 업무 투영에서 화주운송의뢰를 찾지 못했습니다. 원장Id={원장Id}, 의뢰Id={의뢰Id}",
                원장.원장Id,
                snapshot.RequestId);
        }

        if (changed)
        {
            transport.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    internal static 운송원장 CreateCoordinationTransport(
        운송원장업무투영Snapshot snapshot,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!snapshot.CanCreateCoordinationTransport)
        {
            throw new InvalidOperationException(
                "Participant execution observations cannot create a new dispatch record.");
        }

        return new 운송원장
        {
            운송번호 = snapshot.RequestId,
            의뢰Id = snapshot.RequestId,
            화주Id = snapshot.ShipperId ?? string.Empty,
            원본의뢰유형 = snapshot.SourceType ?? CommunityLedgerTemplateKeys.CargoTransport,
            원본의뢰Id = snapshot.SourceId ?? snapshot.RequestId,
            커뮤니티원장Id = snapshot.LedgerId,
            커뮤니티원장템플릿Key = snapshot.LedgerTemplateKey,
            커뮤니티원장상태 = snapshot.LedgerState,
            커뮤니티원장동기화시각Utc = nowUtc,
            상태 = 상태값.배차대기상태.대기,
            배차업무유형 = snapshot.DispatchBusinessType,
            배차큐단계 = 상태값.배차큐단계.계획배차,
            배차노출상태 = 상태값.배차노출상태.계획대기,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        };
    }

    internal static bool ApplyTransportProjection(
        운송원장 entity,
        운송원장업무투영Snapshot snapshot,
        bool isNew)
    {
        var changed = false;

        changed |= SetString(entity.화주Id, snapshot.ShipperId, value => entity.화주Id = value);
        changed |= SetString(entity.원본의뢰유형, snapshot.SourceType, value => entity.원본의뢰유형 = value);
        changed |= SetString(entity.원본의뢰Id, snapshot.SourceId, value => entity.원본의뢰Id = value);
        changed |= SetString(entity.커뮤니티원장Id, snapshot.LedgerId, value => entity.커뮤니티원장Id = value);
        changed |= SetString(entity.커뮤니티원장템플릿Key, snapshot.LedgerTemplateKey, value => entity.커뮤니티원장템플릿Key = value);
        changed |= SetString(entity.커뮤니티원장상태, snapshot.LedgerState, value => entity.커뮤니티원장상태 = value);
        // The community ledger coordinates intent and information. Participant decisions are
        // projected back to it after execution; they never originate here as RDB dispatch state.
        changed |= SetString(entity.픽업_도로명주소, snapshot.PickupAddress, value => entity.픽업_도로명주소 = value);
        changed |= SetString(entity.픽업_상세주소, snapshot.PickupAddressDetail, value => entity.픽업_상세주소 = value);
        changed |= SetDecimal(entity.픽업_위도, snapshot.PickupLatitude, value => entity.픽업_위도 = value);
        changed |= SetDecimal(entity.픽업_경도, snapshot.PickupLongitude, value => entity.픽업_경도 = value);
        changed |= SetString(entity.하차_도로명주소, snapshot.DropoffAddress, value => entity.하차_도로명주소 = value);
        changed |= SetString(entity.하차_상세주소, snapshot.DropoffAddressDetail, value => entity.하차_상세주소 = value);
        changed |= SetDecimal(entity.하차_위도, snapshot.DropoffLatitude, value => entity.하차_위도 = value);
        changed |= SetDecimal(entity.하차_경도, snapshot.DropoffLongitude, value => entity.하차_경도 = value);
        changed |= SetString(entity.출발지, snapshot.PickupAddress, value => entity.출발지 = value);
        changed |= SetString(entity.도착지, snapshot.DropoffAddress, value => entity.도착지 = value);
        changed |= SetDecimal(entity.운임, snapshot.Fare, value => entity.운임 = value);
        if (isNew
            || string.IsNullOrWhiteSpace(entity.메모)
            || entity.메모.StartsWith("커뮤니티 원장 투영:", StringComparison.Ordinal))
        {
            changed |= SetString(entity.메모, snapshot.BuildMemo(), value => entity.메모 = value);
        }

        if (entity.배차업무유형 != snapshot.DispatchBusinessType)
        {
            entity.배차업무유형 = snapshot.DispatchBusinessType;
            changed = true;
        }

        entity.커뮤니티원장동기화시각Utc = DateTime.UtcNow;
        changed = true;

        return changed;
    }

    internal static bool ApplyShipperRequest(
        화주운송의뢰 entity,
        운송원장업무투영Snapshot snapshot)
    {
        var changed = false;

        changed |= SetString(entity.화주Id, snapshot.ShipperId, value => entity.화주Id = value);
        changed |= SetString(entity.주문자UserId, snapshot.OrdererUserId, value => entity.주문자UserId = value);
        changed |= SetString(entity.화물종류, snapshot.CargoType, value => entity.화물종류 = value);
        changed |= SetString(entity.화물설명, snapshot.CargoDescription, value => entity.화물설명 = value);
        changed |= SetInt(entity.화물수량, snapshot.CargoQuantity, value => entity.화물수량 = value);
        changed |= SetInt(entity.화물길이Mm, snapshot.CargoLengthMm, value => entity.화물길이Mm = value);
        changed |= SetInt(entity.화물폭Mm, snapshot.CargoWidthMm, value => entity.화물폭Mm = value);
        changed |= SetInt(entity.화물높이Mm, snapshot.CargoHeightMm, value => entity.화물높이Mm = value);
        changed |= SetInt(entity.화물팔레트개수, snapshot.PalletCount, value => entity.화물팔레트개수 = value);
        changed |= SetDecimal(entity.화물중량Kg, snapshot.CargoWeightKg, value => entity.화물중량Kg = value);
        changed |= SetDecimal(entity.화물부피Cbm, snapshot.CargoVolumeCbm, value => entity.화물부피Cbm = value);
        changed |= SetBool(entity.화물파손주의여부, snapshot.CargoFragile, value => entity.화물파손주의여부 = value);
        changed |= SetString(entity.화물온도조건, snapshot.CargoTemperature, value => entity.화물온도조건 = value);
        changed |= SetString(entity.운송방식, snapshot.TransportMethod, value => entity.운송방식 = value);
        changed |= SetString(entity.차량종류, snapshot.VehicleType, value => entity.차량종류 = value);
        changed |= SetString(entity.서비스레벨, snapshot.ServiceLevel, value => entity.서비스레벨 = value);
        changed |= SetString(entity.요청사항, snapshot.RequestNotes, value => entity.요청사항 = value);
        changed |= SetString(entity.클라이언트요청Id, snapshot.ClientRequestId, value => entity.클라이언트요청Id = value);
        changed |= SetString(entity.상태, snapshot.RequestState, value => entity.상태 = value);
        changed |= SetString(entity.결제상태, snapshot.PaymentState, value => entity.결제상태 = value);
        changed |= SetString(entity.픽업_도로명주소, snapshot.PickupAddress, value => entity.픽업_도로명주소 = value);
        changed |= SetString(entity.픽업_상세주소, snapshot.PickupAddressDetail, value => entity.픽업_상세주소 = value);
        changed |= SetDecimal(entity.픽업_위도, snapshot.PickupLatitude, value => entity.픽업_위도 = value);
        changed |= SetDecimal(entity.픽업_경도, snapshot.PickupLongitude, value => entity.픽업_경도 = value);
        changed |= SetString(entity.픽업_연락처_이름, snapshot.PickupContactName, value => entity.픽업_연락처_이름 = value);
        changed |= SetString(entity.픽업_연락처_전화번호, snapshot.PickupContactPhone, value => entity.픽업_연락처_전화번호 = value);
        changed |= SetDateTime(entity.픽업_시간창_시작일시, snapshot.PickupWindowStart, value => entity.픽업_시간창_시작일시 = value);
        changed |= SetDateTime(entity.픽업_시간창_종료일시, snapshot.PickupWindowEnd, value => entity.픽업_시간창_종료일시 = value);
        changed |= SetString(entity.하차_도로명주소, snapshot.DropoffAddress, value => entity.하차_도로명주소 = value);
        changed |= SetString(entity.하차_상세주소, snapshot.DropoffAddressDetail, value => entity.하차_상세주소 = value);
        changed |= SetDecimal(entity.하차_위도, snapshot.DropoffLatitude, value => entity.하차_위도 = value);
        changed |= SetDecimal(entity.하차_경도, snapshot.DropoffLongitude, value => entity.하차_경도 = value);
        changed |= SetString(entity.하차_연락처_이름, snapshot.DropoffContactName, value => entity.하차_연락처_이름 = value);
        changed |= SetString(entity.하차_연락처_전화번호, snapshot.DropoffContactPhone, value => entity.하차_연락처_전화번호 = value);
        changed |= SetNullableDateTime(entity.하차_시간창_시작일시, snapshot.DropoffWindowStart, value => entity.하차_시간창_시작일시 = value);
        changed |= SetNullableDateTime(entity.하차_시간창_종료일시, snapshot.DropoffWindowEnd, value => entity.하차_시간창_종료일시 = value);
        changed |= SetString(entity.결제수단, snapshot.PaymentMethod, value => entity.결제수단 = value);
        changed |= SetString(entity.정산시점, snapshot.SettlementTiming, value => entity.정산시점 = value);
        changed |= SetString(entity.증빙방식, snapshot.EvidenceMethod, value => entity.증빙방식 = value);
        changed |= SetString(entity.수납주체, snapshot.Collector, value => entity.수납주체 = value);
        changed |= SetString(entity.정산상태, snapshot.SettlementState, value => entity.정산상태 = value);
        changed |= SetString(entity.정산메모, snapshot.SettlementNotes, value => entity.정산메모 = value);
        changed |= SetInt(entity.결제예정금액, snapshot.EstimatedPaymentAmount, value => entity.결제예정금액 = value);
        changed |= SetDecimal(entity.대기료, snapshot.WaitingFee, value => entity.대기료 = value);
        changed |= SetDecimal(entity.수작업비, snapshot.ManualHandlingFee, value => entity.수작업비 = value);
        changed |= SetDecimal(entity.할증, snapshot.Surcharge, value => entity.할증 = value);
        changed |= SetBool(entity.세금계산서필요, snapshot.TaxInvoiceRequired, value => entity.세금계산서필요 = value);
        changed |= SetBool(entity.현금영수증필요, snapshot.CashReceiptRequired, value => entity.현금영수증필요 = value);
        changed |= SetDecimal(entity.최종운임, snapshot.Fare, value => entity.최종운임 = value);

        if (changed)
        {
            entity.UpdatedAt = DateTime.UtcNow;
        }

        return changed;
    }

    private static bool SetString(string? current, string? value, Action<string> setter)
    {
        var cleaned = 운송원장업무투영Snapshot.Clean(value);
        if (cleaned is null || string.Equals(current, cleaned, StringComparison.Ordinal))
        {
            return false;
        }

        setter(cleaned);
        return true;
    }

    private static bool SetDecimal(decimal? current, decimal? value, Action<decimal?> setter)
    {
        if (!value.HasValue || current == value)
        {
            return false;
        }

        setter(value);
        return true;
    }

    private static bool SetInt(int? current, int? value, Action<int?> setter)
    {
        if (!value.HasValue || current == value)
        {
            return false;
        }

        setter(value);
        return true;
    }

    private static bool SetBool(bool current, bool? value, Action<bool> setter)
    {
        if (!value.HasValue || current == value.Value)
        {
            return false;
        }

        setter(value.Value);
        return true;
    }

    private static bool SetDateTime(DateTime current, DateTime? value, Action<DateTime> setter)
    {
        if (!value.HasValue || current == value.Value)
        {
            return false;
        }

        setter(value.Value);
        return true;
    }

    private static bool SetNullableDateTime(DateTime? current, DateTime? value, Action<DateTime?> setter)
    {
        if (!value.HasValue || current == value)
        {
            return false;
        }

        setter(value);
        return true;
    }
}

public sealed class 운송원장업무투영Snapshot
{
    public string LedgerId { get; init; } = string.Empty;
    public string LedgerTemplateKey { get; init; } = string.Empty;
    public string LedgerState { get; init; } = string.Empty;
    public string RequestId { get; init; } = string.Empty;
    public string? SourceType { get; init; }
    public string? SourceId { get; init; }
    public string? ShipperId { get; init; }
    public string? OrdererUserId { get; init; }
    public bool ContainsParticipantExecutionObservation { get; init; }
    public string? CargoType { get; init; }
    public string? CargoDescription { get; init; }
    public int? CargoQuantity { get; init; }
    public int? CargoLengthMm { get; init; }
    public int? CargoWidthMm { get; init; }
    public int? CargoHeightMm { get; init; }
    public int? PalletCount { get; init; }
    public decimal? CargoWeightKg { get; init; }
    public decimal? CargoVolumeCbm { get; init; }
    public bool? CargoFragile { get; init; }
    public string? CargoTemperature { get; init; }
    public string? TransportMethod { get; init; }
    public string? VehicleType { get; init; }
    public string? ServiceLevel { get; init; }
    public string? RequestNotes { get; init; }
    public string? ClientRequestId { get; init; }
    public string? RequestState { get; init; }
    public string? PaymentState { get; init; }
    public string? PickupAddress { get; init; }
    public string? PickupAddressDetail { get; init; }
    public decimal? PickupLatitude { get; init; }
    public decimal? PickupLongitude { get; init; }
    public string? PickupContactName { get; init; }
    public string? PickupContactPhone { get; init; }
    public DateTime? PickupWindowStart { get; init; }
    public DateTime? PickupWindowEnd { get; init; }
    public string? DropoffAddress { get; init; }
    public string? DropoffAddressDetail { get; init; }
    public decimal? DropoffLatitude { get; init; }
    public decimal? DropoffLongitude { get; init; }
    public string? DropoffContactName { get; init; }
    public string? DropoffContactPhone { get; init; }
    public DateTime? DropoffWindowStart { get; init; }
    public DateTime? DropoffWindowEnd { get; init; }
    public string? PaymentMethod { get; init; }
    public string? SettlementTiming { get; init; }
    public string? EvidenceMethod { get; init; }
    public string? Collector { get; init; }
    public string? SettlementState { get; init; }
    public string? SettlementNotes { get; init; }
    public int? EstimatedPaymentAmount { get; init; }
    public decimal? WaitingFee { get; init; }
    public decimal? ManualHandlingFee { get; init; }
    public decimal? Surcharge { get; init; }
    public bool? TaxInvoiceRequired { get; init; }
    public bool? CashReceiptRequired { get; init; }
    public decimal? Fare { get; init; }

    public bool IsCargoRequest
        => string.Equals(LedgerTemplateKey, CommunityLedgerTemplateKeys.CargoTransport, StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<string> MissingRequiredFields => BuildMissingRequiredFields();

    public bool IsTransportRequestComplete => MissingRequiredFields.Count == 0;

    public bool CanCreateCoordinationTransport =>
        !ContainsParticipantExecutionObservation;

    public int DispatchBusinessType
        => string.Equals(LedgerTemplateKey, CommunityLedgerTemplateKeys.FoodDelivery, StringComparison.OrdinalIgnoreCase)
            ? 상태값.배차업무유형.음식배달
            : 상태값.배차업무유형.용달운송;

    public static bool 처리대상인가(커뮤니티원장Dto 원장)
    {
        if (string.Equals(원장.원장템플릿Key, CommunityLedgerTemplateKeys.CargoTransport, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (TryGet(원장.외부참조, "RdbTransportProjectionTable", "RdbTransportProjectionType", "화주운송의뢰Id", "운송번호") is not null)
        {
            return true;
        }

        return 원장.블록목록.Any(block =>
        {
            var entityHint = TryGet(block.Data, "업무엔티티");
            return ContainsAny(entityHint, "화주운송의뢰", "운송원장", "운송실행투영")
                   || ContainsAny(block.BlockId, "transport", "pickup", "dropoff")
                   || ContainsAny(block.Title, "운송 의뢰", "상차", "하차");
        });
    }

    public static 운송원장업무투영Snapshot? 생성(커뮤니티원장Dto 원장)
    {
        if (!처리대상인가(원장))
        {
            return null;
        }

        var requestBlock = FindBlock(원장, block =>
            ContainsAny(block.BlockId, "transport-request", "request")
            || ContainsAny(block.Title, "운송 의뢰", "의뢰")
            || ContainsAny(TryGet(block.Data, "업무엔티티"), "화주운송의뢰"));

        var pickupBlock = FindBlock(원장, block =>
            ContainsAny(block.BlockId, "pickup")
            || ContainsAny(block.Title, "상차", "픽업")
            || ContainsAny(TryGet(block.Data, "업무엔티티"), "상차"));

        var dropoffBlock = FindBlock(원장, block =>
            ContainsAny(block.BlockId, "dropoff")
            || ContainsAny(block.Title, "하차", "도착")
            || ContainsAny(TryGet(block.Data, "업무엔티티"), "하차"));

        var settlementBlock = FindBlock(원장, block =>
            ContainsAny(block.BlockId, "settlement", "fare", "payment")
            || ContainsAny(block.Title, "정산", "결제", "운임"));

        var requestId = FirstNonEmpty(
            TryGet(원장.외부참조, "화주운송의뢰Id", "의뢰Id", "RequestId", "requestId"),
            TryGet(원장.외부참조, "운송번호", "TransportNo", "transportNo"),
            TryGet(requestBlock?.Data, "의뢰Id", "RequestId", "requestId"),
            TryGet(requestBlock?.Data, "운송번호", "TransportNo", "transportNo"),
            ExtractTransportLedgerId(원장.원장Id),
            원장.원장Id);

        if (requestId is null)
        {
            return null;
        }

        return new 운송원장업무투영Snapshot
        {
            LedgerId = Clean(원장.원장Id) ?? requestId,
            LedgerTemplateKey = Clean(원장.원장템플릿Key) ?? CommunityLedgerTemplateKeys.CargoTransport,
            LedgerState = Clean(원장.상태) ?? 커뮤니티원장상태.초안,
            RequestId = requestId,
            SourceType = FirstNonEmpty(
                TryGet(원장.외부참조, "원천유형", "SourceType"),
                TryGet(requestBlock?.Data, "원천유형", "SourceType"),
                ResolveDefaultSourceType(원장)),
            SourceId = FirstNonEmpty(
                TryGet(원장.외부참조, "원천Id", "SourceId"),
                TryGet(requestBlock?.Data, "원천Id", "SourceId"),
                requestId),
            ShipperId = FirstNonEmpty(
                TryGet(원장.외부참조, "화주Id", "shipperId"),
                TryGet(requestBlock?.Data, "화주Id", "shipperId"),
                FindParticipantUserId(원장, "화주", "요청자")),
            OrdererUserId = FirstNonEmpty(
                TryGet(requestBlock?.Data, "주문자UserId", "OrdererUserId", "ordererUserId"),
                FindParticipantUserId(원장, "주문자")),
            ContainsParticipantExecutionObservation =
                HasParticipantExecutionObservation(원장, requestBlock),
            CargoType = TryGet(requestBlock?.Data, "화물종류", "CargoType", "cargoType"),
            CargoDescription = FirstNonEmpty(
                TryGet(requestBlock?.Data, "화물설명", "CargoDescription", "cargoDescription"),
                원장.원함),
            CargoQuantity = ParseInt(TryGet(requestBlock?.Data, "화물수량", "CargoQuantity", "cargoQuantity")),
            CargoLengthMm = ParseInt(TryGet(requestBlock?.Data, "화물길이Mm", "CargoLengthMm", "cargoLengthMm")),
            CargoWidthMm = ParseInt(TryGet(requestBlock?.Data, "화물폭Mm", "CargoWidthMm", "cargoWidthMm")),
            CargoHeightMm = ParseInt(TryGet(requestBlock?.Data, "화물높이Mm", "CargoHeightMm", "cargoHeightMm")),
            PalletCount = ParseInt(TryGet(requestBlock?.Data, "팔레트개수", "PalletCount", "palletCount")),
            CargoWeightKg = ParseDecimal(TryGet(requestBlock?.Data, "화물중량Kg", "CargoWeightKg", "cargoWeightKg")),
            CargoVolumeCbm = ParseDecimal(TryGet(requestBlock?.Data, "화물부피Cbm", "CargoVolumeCbm", "cargoVolumeCbm")),
            CargoFragile = ParseBool(TryGet(requestBlock?.Data, "화물파손주의여부", "CargoFragile", "cargoFragile")),
            CargoTemperature = TryGet(requestBlock?.Data, "화물온도조건", "CargoTemperature", "cargoTemperature"),
            TransportMethod = TryGet(requestBlock?.Data, "운송방식", "TransportMethod", "transportMethod"),
            VehicleType = TryGet(requestBlock?.Data, "차량종류", "VehicleType", "vehicleType"),
            ServiceLevel = TryGet(requestBlock?.Data, "서비스레벨", "ServiceLevel", "serviceLevel"),
            RequestNotes = TryGet(requestBlock?.Data, "요청사항", "RequestNotes", "requestNotes"),
            ClientRequestId = TryGet(requestBlock?.Data, "클라이언트요청Id", "ClientRequestId", "clientRequestId"),
            RequestState = TryGet(requestBlock?.Data, "의뢰상태", "RequestState", "requestState"),
            PaymentState = FirstNonEmpty(
                TryGet(requestBlock?.Data, "결제상태", "PaymentState", "paymentState"),
                TryGet(settlementBlock?.Data, "결제상태", "PaymentState", "paymentState")),
            PickupAddress = TryGet(pickupBlock?.Data, "주소", "도로명주소", "PickupAddress", "pickupAddress"),
            PickupAddressDetail = TryGet(pickupBlock?.Data, "상세주소", "AddressDetail", "pickupAddressDetail"),
            PickupLatitude = ParseDecimal(TryGet(pickupBlock?.Data, "위도", "Latitude", "lat", "pickupLatitude")),
            PickupLongitude = ParseDecimal(TryGet(pickupBlock?.Data, "경도", "Longitude", "lng", "pickupLongitude")),
            PickupContactName = TryGet(pickupBlock?.Data, "연락처이름", "ContactName", "pickupContactName"),
            PickupContactPhone = TryGet(pickupBlock?.Data, "연락처전화번호", "ContactPhone", "pickupContactPhone"),
            PickupWindowStart = ParseDateTime(TryGet(pickupBlock?.Data, "시간창시작", "WindowStart", "pickupWindowStart")),
            PickupWindowEnd = ParseDateTime(TryGet(pickupBlock?.Data, "시간창종료", "WindowEnd", "pickupWindowEnd")),
            DropoffAddress = TryGet(dropoffBlock?.Data, "주소", "도로명주소", "DropoffAddress", "dropoffAddress"),
            DropoffAddressDetail = TryGet(dropoffBlock?.Data, "상세주소", "AddressDetail", "dropoffAddressDetail"),
            DropoffLatitude = ParseDecimal(TryGet(dropoffBlock?.Data, "위도", "Latitude", "lat", "dropoffLatitude")),
            DropoffLongitude = ParseDecimal(TryGet(dropoffBlock?.Data, "경도", "Longitude", "lng", "dropoffLongitude")),
            DropoffContactName = TryGet(dropoffBlock?.Data, "연락처이름", "ContactName", "dropoffContactName"),
            DropoffContactPhone = TryGet(dropoffBlock?.Data, "연락처전화번호", "ContactPhone", "dropoffContactPhone"),
            DropoffWindowStart = ParseDateTime(TryGet(dropoffBlock?.Data, "시간창시작", "WindowStart", "dropoffWindowStart")),
            DropoffWindowEnd = ParseDateTime(TryGet(dropoffBlock?.Data, "시간창종료", "WindowEnd", "dropoffWindowEnd")),
            PaymentMethod = TryGet(settlementBlock?.Data, "결제수단", "PaymentMethod", "paymentMethod"),
            SettlementTiming = TryGet(settlementBlock?.Data, "정산시점", "SettlementTiming", "settlementTiming"),
            EvidenceMethod = TryGet(settlementBlock?.Data, "증빙방식", "EvidenceMethod", "evidenceMethod"),
            Collector = TryGet(settlementBlock?.Data, "수납주체", "Collector", "collector"),
            SettlementState = TryGet(settlementBlock?.Data, "정산상태", "SettlementState", "settlementState"),
            SettlementNotes = TryGet(settlementBlock?.Data, "정산메모", "SettlementNotes", "settlementNotes"),
            EstimatedPaymentAmount = ParseInt(TryGet(settlementBlock?.Data, "결제예정금액", "EstimatedPaymentAmount")),
            WaitingFee = ParseDecimal(TryGet(settlementBlock?.Data, "대기료", "WaitingFee", "waitingFee")),
            ManualHandlingFee = ParseDecimal(TryGet(settlementBlock?.Data, "수작업비", "ManualHandlingFee", "manualHandlingFee")),
            Surcharge = ParseDecimal(TryGet(settlementBlock?.Data, "할증", "Surcharge", "surcharge")),
            TaxInvoiceRequired = ParseBool(TryGet(settlementBlock?.Data, "세금계산서필요", "TaxInvoiceRequired")),
            CashReceiptRequired = ParseBool(TryGet(settlementBlock?.Data, "현금영수증필요", "CashReceiptRequired")),
            Fare = ParseDecimal(FirstNonEmpty(
                TryGet(settlementBlock?.Data, "최종운임", "운임", "Fare", "fare"),
                TryGet(settlementBlock?.Data, "결제예정금액", "EstimatedPaymentAmount")))
        };
    }

    public string BuildMemo()
        => $"커뮤니티 원장 투영: {LedgerId}";

    private IReadOnlyList<string> BuildMissingRequiredFields()
    {
        var missing = new List<string>();
        AddMissing(missing, "요청자", FirstNonEmpty(ShipperId, OrdererUserId));
        AddMissing(missing, "화물종류", CargoType);
        AddMissing(missing, "상차지", PickupAddress);
        AddMissing(missing, "상차지 연락처", PickupContactPhone);
        AddMissing(missing, "하차지", DropoffAddress);

        if (!PickupWindowStart.HasValue || !PickupWindowEnd.HasValue)
        {
            missing.Add("상차 시간창");
        }
        else if (PickupWindowStart >= PickupWindowEnd)
        {
            missing.Add("상차 시간창 순서");
        }

        if (DropoffWindowStart.HasValue
            && DropoffWindowEnd.HasValue
            && DropoffWindowStart >= DropoffWindowEnd)
        {
            missing.Add("하차 시간창 순서");
        }

        return missing;
    }

    private static void AddMissing(List<string> missing, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            missing.Add(name);
        }
    }

    public static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static 커뮤니티원장블록Dto? FindBlock(커뮤니티원장Dto 원장, Func<커뮤니티원장블록Dto, bool> predicate)
        => 원장.블록목록.FirstOrDefault(predicate);

    private static string? ResolveDefaultSourceType(커뮤니티원장Dto 원장)
        => string.Equals(원장.원장템플릿Key, CommunityLedgerTemplateKeys.CargoTransport, StringComparison.OrdinalIgnoreCase)
            ? CommunityLedgerTemplateKeys.CargoTransport
            : 원장.원장템플릿Key;

    private static bool HasParticipantExecutionObservation(
        커뮤니티원장Dto 원장,
        커뮤니티원장블록Dto? requestBlock)
    {
        var driverId = FirstNonEmpty(
            TryGet(원장.외부참조, "확정기사Id", "기사Id", "driverId"),
            TryGet(requestBlock?.Data, "확정기사Id", "기사Id", "driverId"),
            FindParticipantUserId(원장, "기사", "운반자", "배달자"));
        if (driverId is not null
            || string.Equals(
                원장.상태,
                커뮤니티원장상태.완료,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Clean(원장.현재단계Key) is 상태값.배차대기상태.확정
            or 상태값.배차상태.상차중
            or 상태값.배차상태.상차완료
            or 상태값.배차상태.운송중
            or 상태값.배차상태.하차완료
            or 상태값.배차상태.인수완료
            or "상차지도착"
            or "하차지도착";
    }

    private static string? ExtractTransportLedgerId(string? ledgerId)
    {
        var cleaned = Clean(ledgerId);
        const string prefix = "transport:";
        return cleaned is not null && cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? cleaned[prefix.Length..]
            : null;
    }

    private static string? FindParticipantUserId(커뮤니티원장Dto 원장, params string[] roleHints)
        => 원장.참여자목록
            .FirstOrDefault(participant => roleHints.Any(hint =>
                ContainsAny(participant.RoleLabel, hint)
                || ContainsAny(participant.DisplayName, hint)))?
            .UserId;

    private static string? TryGet(IReadOnlyDictionary<string, string>? data, params string[] keys)
    {
        if (data is null || data.Count == 0)
        {
            return null;
        }

        foreach (var key in keys)
        {
            foreach (var pair in data)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return Clean(pair.Value);
                }
            }
        }

        return null;
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.Select(Clean).FirstOrDefault(value => value is not null);

    private static bool ContainsAny(string? source, params string[] candidates)
    {
        var text = Clean(source);
        return text is not null && candidates.Any(candidate => text.Contains(candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static decimal? ParseDecimal(string? value)
    {
        var cleaned = Clean(value);
        if (cleaned is null)
        {
            return null;
        }

        if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantValue))
        {
            return invariantValue;
        }

        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.CurrentCulture, out var currentValue)
            ? currentValue
            : null;
    }

    private static int? ParseInt(string? value)
        => int.TryParse(Clean(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static bool? ParseBool(string? value)
        => bool.TryParse(Clean(value), out var parsed) ? parsed : null;

    private static DateTime? ParseDateTime(string? value)
        => DateTime.TryParse(
            Clean(value),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : null;
}
