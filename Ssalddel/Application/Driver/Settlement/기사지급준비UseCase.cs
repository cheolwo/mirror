using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Driver.Settlement;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Application.Settlement;
using Ssalddel.Services.PrivacySupport;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Application.Driver.Settlement;

public interface I기사지급준비UseCase
{
    Task<Result<기사지급준비목록응답>> 월별조회Async(
        string? 기사Id,
        int? year,
        int? month,
        CancellationToken cancellationToken = default);
}

[SsalddelApiWorkflow(SsalddelWorkflow.DomesticTransport)]
[SsalddelUseCase(
    "기사 운송대금 지급 준비 조회",
    Summary = "화주 수납과 기사 지급을 구분하고 완료 운송별 약정 운임과 지급 준비 조건을 조회합니다.")]
[SsalddelUseCaseActor(SsalddelActor.Driver)]
public sealed class 기사지급준비UseCase : I기사지급준비UseCase
{
    private readonly SsalddelContext _db;
    private readonly I화물분쟁지급보류Query? _disputes;

    public 기사지급준비UseCase(SsalddelContext db, I화물분쟁지급보류Query? disputes = null)
    {
        _db = db;
        _disputes = disputes;
    }

    public async Task<Result<기사지급준비목록응답>> 월별조회Async(
        string? 기사Id,
        int? year,
        int? month,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(기사Id))
        {
            return 상태실패("기사 인증 정보가 없습니다.", StatusCodes.Status401Unauthorized);
        }

        var now = DateTime.UtcNow;
        var targetYear = year ?? now.Year;
        var targetMonth = month ?? now.Month;
        if (targetYear is < 2000 or > 2100 || targetMonth is < 1 or > 12)
        {
            return 상태실패("year와 month가 올바르지 않습니다.", StatusCodes.Status400BadRequest);
        }

        var driverExists = await _db.용달기사
            .AsNoTracking()
            .AnyAsync(x => x.기사Id == 기사Id, cancellationToken);
        if (!driverExists)
        {
            return 상태실패("용달기사 정보를 찾을 수 없습니다.", StatusCodes.Status404NotFound);
        }

        var startUtc = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var endUtc = startUtc.AddMonths(1);
        var transports = await _db.운송원장
            .AsNoTracking()
            .Where(x =>
                (x.확정기사Id == 기사Id || x.기사_운송자 == 기사Id) &&
                x.도착.HasValue &&
                x.도착.Value >= startUtc &&
                x.도착.Value < endUtc)
            .OrderByDescending(x => x.도착)
            .ToListAsync(cancellationToken);

        var requestKeys = transports
            .SelectMany(x => new[] { x.의뢰Id, x.운송번호 })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var requestKeyList = requestKeys.ToList();
        var requests = await _db.화주운송의뢰
            .AsNoTracking()
            .Where(x => requestKeyList.Contains(x.의뢰Id))
            .ToListAsync(cancellationToken);
        var requestsById = requests.ToDictionary(x => x.의뢰Id, StringComparer.Ordinal);

        var fareCompositions = await _db.운임구성
            .AsNoTracking()
            .Where(x => requestKeyList.Contains(x.의뢰Id))
            .ToListAsync(cancellationToken);
        var faresByRequestId = fareCompositions
            .GroupBy(x => x.의뢰Id, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(item => item.UpdatedAt).First(),
                StringComparer.Ordinal);

        var openAbnormalIncidentRequestIds = (await _db.비정상운송사건
                .AsNoTracking()
                .Where(x => requestKeyList.Contains(x.운송의뢰Id)
                            && (x.정산보류적용여부
                                || x.상태Code == 비정상운송사건상태Codes.운영검토대기))
                .Select(x => x.운송의뢰Id)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var settlementAccount = await _db.Set<기사정산계좌>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.기사Id == 기사Id, cancellationToken);
        var accountVerified = string.Equals(
            settlementAccount?.확인상태,
            기사정산계좌확인상태.확인완료,
            StringComparison.Ordinal);

        var items = new List<기사지급준비항목응답>(transports.Count);
        foreach (var transport in transports)
        {
            var request = 요청찾기(transport, requestsById);
            faresByRequestId.TryGetValue(request?.의뢰Id ?? transport.의뢰Id, out var fare);
            var requestId = request?.의뢰Id ?? transport.의뢰Id;
            var item = 항목생성(
                    transport,
                    request,
                    fare,
                    settlementAccount is not null,
                    accountVerified,
                    openAbnormalIncidentRequestIds.Contains(requestId));
            if (item.IsReadyForPayoutPreparation)
            {
                var dispute = await 보호지원화물지급보류Query.확인Async(_disputes, requestId, cancellationToken);
                if (dispute.Hold)
                {
                    item.IsReadyForPayoutPreparation = false;
                    item.ReadinessCode = dispute.Code;
                    item.ReadinessMessage = dispute.Message;
                }
            }
            items.Add(item);
        }

        return Result.Ok(new 기사지급준비목록응답
        {
            DriverId = 기사Id,
            Year = targetYear,
            Month = targetMonth,
            HasSettlementAccount = settlementAccount is not null,
            SettlementAccountVerificationStatus = settlementAccount?.확인상태 ?? string.Empty,
            ExpectedPayoutTotal = items.Sum(x => x.ExpectedPayoutAmount ?? 0m),
            ReadyForPayoutPreparationTotal = items
                .Where(x => x.IsReadyForPayoutPreparation)
                .Sum(x => x.ExpectedPayoutAmount ?? 0m),
            OnSiteCollectionConfirmedTotal = items
                .Where(x => x.ReadinessCode == 기사지급준비상태코드.현장수금확인)
                .Sum(x => x.ExpectedPayoutAmount ?? 0m),
            Items = items
        });
    }

    private static 화주운송의뢰? 요청찾기(
        운송원장 transport,
        IReadOnlyDictionary<string, 화주운송의뢰> requestsById)
    {
        if (!string.IsNullOrWhiteSpace(transport.의뢰Id) &&
            requestsById.TryGetValue(transport.의뢰Id, out var byRequestId))
        {
            return byRequestId;
        }

        return !string.IsNullOrWhiteSpace(transport.운송번호) &&
               requestsById.TryGetValue(transport.운송번호, out var byTransportNumber)
            ? byTransportNumber
            : null;
    }

    private static 기사지급준비항목응답 항목생성(
        운송원장 transport,
        화주운송의뢰? request,
        운임구성? fare,
        bool hasSettlementAccount,
        bool settlementAccountVerified,
        bool hasOpenAbnormalTransportIncident)
    {
        var readiness = 준비상태판정(
            request,
            fare?.기사지급예정운임,
            hasSettlementAccount,
            settlementAccountVerified,
            hasOpenAbnormalTransportIncident);

        var agreement = 저장합의조회(fare, request, transport);
        var agreementStatus = string.IsNullOrWhiteSpace(fare?.합의요금표SnapshotJson) ? "NotSelected"
            : agreement is null ? "InvalidEvidence" : "StoredAcceptedAgreement";
        if (agreementStatus == "InvalidEvidence") readiness = new("RateTableEvidenceInvalid",
            "저장된 사전 합의 수행 대가와 판본 근거를 확인해 주세요.", false);

        return new 기사지급준비항목응답
        {
            TransportId = transport.Id,
            TransportNumber = transport.운송번호,
            RequestId = request?.의뢰Id ?? transport.의뢰Id,
            CompletedAtUtc = transport.도착!.Value,
            ExpectedPayoutAmount = request?.모의처리처수행 == true ? null : fare?.기사지급예정운임,
            AgreedRateTable = agreement,
            AgreedRateTableEvidenceStatusCode = agreementStatus,
            CurrencyCode = "KRW",
            AmountSource = fare?.기사지급예정운임.HasValue == true
                ? "운임구성.기사지급예정운임"
                : string.Empty,
            SettlementTiming = request?.정산시점 ?? string.Empty,
            ShipperPaymentStatus = request?.결제상태 ?? string.Empty,
            FreightSettlementStatus = request?.정산상태 ?? string.Empty,
            ReadinessCode = readiness.Code,
            ReadinessMessage = readiness.Message,
            IsReadyForPayoutPreparation = readiness.IsReady
        };
    }

    internal static Ssalddel.Contracts.Common.Pricing.AgreedRateTableSnapshotDto? 저장합의조회(
        운임구성? fare, 화주운송의뢰? request, 운송원장 transport)
    {
        if (string.IsNullOrWhiteSpace(fare?.합의요금표SnapshotJson) || request is null) return null;
        try
        {
            var snapshot = System.Text.Json.JsonSerializer.Deserialize<Ssalddel.Contracts.Common.Pricing.AgreedRateTableSnapshotDto>(fare.합의요금표SnapshotJson,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            if (!Ssalddel.Domain.Pricing.AgreedRateTableEvidencePolicy.IsValid(snapshot)
                || snapshot!.BusinessKind != Ssalddel.Contracts.Common.Pricing.AgreedRateBusinessKinds.CargoTransport
                || snapshot.VehicleCode != request.차량종류 || fare.의뢰Id != request.의뢰Id || request.운임구성Id != fare.Id
                || fare.예상거리Km is not > 0 || fare.거리계산방식 is null) return null;
            var driver = new Ssalddel.Contracts.Common.Pricing.AgreedRatePartyDto(
                Ssalddel.Contracts.Common.Pricing.AgreedRatePartyKinds.CargoDriver, transport.확정기사Id ?? transport.기사_운송자 ?? "");
            var owner = new Ssalddel.Contracts.Common.Pricing.AgreedRatePartyDto(
                Ssalddel.Contracts.Common.Pricing.AgreedRatePartyKinds.Shipper, request.화주Id);
            if (!(snapshot.Proposer == driver && snapshot.Counterparty == owner || snapshot.Counterparty == driver && snapshot.Proposer == owner)) return null;
            var terms = snapshot.CargoPricing!;
            var frozenTotal = 화주운송기준운임계산기.Calculate(new() { 차량종류 = snapshot.VehicleCode,
                대기료 = fare.대기료, 수작업비 = fare.수작업비, 할증 = fare.할증 },
                new(snapshot.VehicleCode, terms.BaseFareKrw, terms.PerKmKrw, terms.MinimumFareKrw, "StoredAcceptedAgreement"), fare.예상거리Km.Value).최종운임;
            return frozenTotal == fare.기사지급예정운임 ? snapshot : null;
        }
        catch (System.Text.Json.JsonException) { return null; }
        catch (ArgumentException) { return null; }
    }

    internal static 기사지급준비판정 준비상태판정(
        화주운송의뢰? request,
        decimal? expectedPayoutAmount,
        bool hasSettlementAccount,
        bool settlementAccountVerified,
        bool hasOpenAbnormalTransportIncident = false)
        => 기사지급준비Policy.판정(request, expectedPayoutAmount, hasSettlementAccount,
            settlementAccountVerified, hasOpenAbnormalTransportIncident);

    private static Result<기사지급준비목록응답> 상태실패(string message, int statusCode)
        => Result.Fail<기사지급준비목록응답>(
            new Error(message).WithMetadata("StatusCode", statusCode));
}
