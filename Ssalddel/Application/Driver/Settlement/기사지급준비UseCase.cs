using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Driver.Settlement;
using Ssalddel.Contracts.Shipper.Request;
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

    public 기사지급준비UseCase(SsalddelContext db)
    {
        _db = db;
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

        var items = transports
            .Select(transport =>
            {
                var request = 요청찾기(transport, requestsById);
                faresByRequestId.TryGetValue(request?.의뢰Id ?? transport.의뢰Id, out var fare);
                var requestId = request?.의뢰Id ?? transport.의뢰Id;
                return 항목생성(
                    transport,
                    request,
                    fare,
                    settlementAccount is not null,
                    accountVerified,
                    openAbnormalIncidentRequestIds.Contains(requestId));
            })
            .ToArray();

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

        return new 기사지급준비항목응답
        {
            TransportId = transport.Id,
            TransportNumber = transport.운송번호,
            RequestId = request?.의뢰Id ?? transport.의뢰Id,
            CompletedAtUtc = transport.도착!.Value,
            ExpectedPayoutAmount = fare?.기사지급예정운임,
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

    internal static 기사지급준비판정 준비상태판정(
        화주운송의뢰? request,
        decimal? expectedPayoutAmount,
        bool hasSettlementAccount,
        bool settlementAccountVerified,
        bool hasOpenAbnormalTransportIncident = false)
    {
        if (request is null)
        {
            return new(
                기사지급준비상태코드.원천의뢰없음,
                "운송 원천 의뢰를 확인할 수 없어 지급 준비를 진행할 수 없습니다.",
                false);
        }

        if (hasOpenAbnormalTransportIncident)
        {
            return new(
                기사지급준비상태코드.비정상운송검토보류,
                "비정상 운송 사건의 운영 검토가 끝날 때까지 기사 지급 준비를 보류합니다.",
                false);
        }

        if (expectedPayoutAmount is not > 0)
        {
            return new(
                기사지급준비상태코드.지급예정운임없음,
                "기사 지급 예정 운임이 기록되지 않아 금액 검토가 필요합니다.",
                false);
        }

        if (string.Equals(request.정산시점, 정산시점.현장지급.ToString(), StringComparison.Ordinal))
        {
            return request.현장수금확인일시.HasValue
                ? new(
                    기사지급준비상태코드.현장수금확인,
                    "기사의 현장 수금 확인이 기록되었습니다. 플랫폼 지급 대상이 아닙니다.",
                    false)
                : new(
                    기사지급준비상태코드.현장수금대기,
                    "현장 수금 확인이 아직 기록되지 않았습니다.",
                    false);
        }

        var shipperCollectionConfirmed =
            string.Equals(request.결제상태, 상태값.결제상태.결제완료, StringComparison.Ordinal)
            || string.Equals(request.정산상태, 운임정산상태.입금확인완료.ToString(), StringComparison.Ordinal)
            || string.Equals(request.정산상태, 운임정산상태.정산완료.ToString(), StringComparison.Ordinal);
        if (!shipperCollectionConfirmed)
        {
            return new(
                기사지급준비상태코드.화주수납대기,
                "화주의 결제 또는 입금 확인이 완료되지 않았습니다.",
                false);
        }

        if (!hasSettlementAccount)
        {
            return new(
                기사지급준비상태코드.정산계좌없음,
                "기사 정산계좌를 등록해야 지급 준비를 진행할 수 있습니다.",
                false);
        }

        if (!settlementAccountVerified)
        {
            return new(
                기사지급준비상태코드.정산계좌미확인,
                "기사 정산계좌의 본인 확인이 완료되지 않았습니다.",
                false);
        }

        return new(
            기사지급준비상태코드.지급준비가능,
            "화주 수납과 정산계좌 확인이 완료되어 지급 준비가 가능합니다.",
            true);
    }

    private static Result<기사지급준비목록응답> 상태실패(string message, int statusCode)
        => Result.Fail<기사지급준비목록응답>(
            new Error(message).WithMetadata("StatusCode", statusCode));
}

internal sealed record 기사지급준비판정(string Code, string Message, bool IsReady);
