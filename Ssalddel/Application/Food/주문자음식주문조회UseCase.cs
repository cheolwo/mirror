using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Participants;
using Ssalddel.Contracts.Food;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.음식;

namespace Ssalddel.Application.Food;

public interface I주문자음식주문조회UseCase
{
    Task<Result<주문자음식주문목록응답>> 목록Async(
        주문자음식주문목록조회요청 request,
        string? ordererUserId,
        CancellationToken cancellationToken);

    Task<Result<주문자음식주문상세응답>> 상세Async(
        string orderNo,
        string? ordererUserId,
        CancellationToken cancellationToken);
}

[SsalddelApiWorkflow(SsalddelWorkflow.FoodDelivery)]
[SsalddelUseCase(
    "주문자 음식 주문 조회",
    Summary = "로그인한 주문자가 소유한 영속 음식 주문의 목록과 정확한 주문번호 상세만 조회합니다.")]
[SsalddelUseCaseActor(SsalddelActor.Orderer)]
public sealed class 주문자음식주문조회UseCase(
    SsalddelContext db) : I주문자음식주문조회UseCase
{
    public async Task<Result<주문자음식주문목록응답>> 목록Async(
        주문자음식주문목록조회요청 request,
        string? ordererUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = Clean(ordererUserId);
        if (userId is null)
        {
            return Unauthorized<주문자음식주문목록응답>();
        }

        var status = Clean(request.상태);
        if (status is not null && !음식주문상태코드.지원여부(status))
        {
            return Result.Fail<주문자음식주문목록응답>("조회할 음식 주문 상태를 확인해 주세요.");
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var query = db.음식주문
            .AsNoTracking()
            .Where(item => item.주문자UserId == userId);

        if (status is not null)
        {
            query = query.Where(item => item.상태 == status);
        }

        var search = Clean(request.검색어);
        if (search is not null)
        {
            query = query.Where(item =>
                item.주문번호.Contains(search)
                || item.음식점명.Contains(search)
                || item.상품목록.Any(product => product.상품명.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var orders = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(item => item.상품목록)
            .ToArrayAsync(cancellationToken);
        var transports = await LoadLatestTransportsAsync(
            orders.Select(item => item.주문번호).ToArray(),
            cancellationToken);

        return Result.Ok(new 주문자음식주문목록응답
        {
            Items = orders
                .Select(item => ToSummary(item, transports.GetValueOrDefault(item.주문번호)))
                .ToArray(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<주문자음식주문상세응답>> 상세Async(
        string orderNo,
        string? ordererUserId,
        CancellationToken cancellationToken)
    {
        var userId = Clean(ordererUserId);
        if (userId is null)
        {
            return Unauthorized<주문자음식주문상세응답>();
        }

        var cleanOrderNo = Clean(orderNo);
        if (cleanOrderNo is null)
        {
            return Result.Fail<주문자음식주문상세응답>("조회할 음식 주문번호를 확인해 주세요.");
        }

        var order = await db.음식주문
            .AsNoTracking()
            .Include(item => item.상품목록)
            .Include(item => item.상태이력)
            .FirstOrDefaultAsync(
                item => item.주문번호 == cleanOrderNo && item.주문자UserId == userId,
                cancellationToken);
        if (order is null)
        {
            return NotFound<주문자음식주문상세응답>();
        }

        var transport = await LoadLatestTransportAsync(cleanOrderNo, cancellationToken);
        return Result.Ok(ToDetail(order, transport));
    }

    private async Task<IReadOnlyDictionary<string, 운송원장>> LoadLatestTransportsAsync(
        IReadOnlyCollection<string> orderNumbers,
        CancellationToken cancellationToken)
    {
        if (orderNumbers.Count == 0)
        {
            return new Dictionary<string, 운송원장>(StringComparer.Ordinal);
        }

        var transports = await db.운송원장
            .AsNoTracking()
            .Where(item =>
                item.배차업무유형 == 상태값.배차업무유형.음식배달
                && (orderNumbers.Contains(item.원본의뢰Id) || orderNumbers.Contains(item.의뢰Id)))
            .OrderByDescending(item => item.UpdatedAt)
            .ThenByDescending(item => item.Id)
            .ToArrayAsync(cancellationToken);

        var result = new Dictionary<string, 운송원장>(StringComparer.Ordinal);
        foreach (var orderNumber in orderNumbers)
        {
            var latest = transports.FirstOrDefault(item =>
                string.Equals(item.원본의뢰Id, orderNumber, StringComparison.Ordinal)
                || string.Equals(item.의뢰Id, orderNumber, StringComparison.Ordinal));
            if (latest is not null)
            {
                result[orderNumber] = latest;
            }
        }

        return result;
    }

    private async Task<운송원장?> LoadLatestTransportAsync(
        string orderNumber,
        CancellationToken cancellationToken)
        => await db.운송원장
            .AsNoTracking()
            .Where(item =>
                item.배차업무유형 == 상태값.배차업무유형.음식배달
                && (item.원본의뢰Id == orderNumber || item.의뢰Id == orderNumber))
            .OrderByDescending(item => item.UpdatedAt)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private static 주문자음식주문요약응답 ToSummary(
        음식주문 order,
        운송원장? transport = null)
    {
        var products = order.상품목록.OrderBy(item => item.Id).ToArray();
        return new 주문자음식주문요약응답
        {
            주문번호 = order.주문번호,
            음식점Id = order.음식점Id,
            음식점명 = order.음식점명,
            상품요약 = BuildProductSummary(products),
            상품종류수 = products.Length,
            총수량 = products.Sum(item => item.수량),
            총주문금액 = order.총주문금액,
            상태 = 음식주문상태코드.Normalize(order.상태),
            배차상태 = ResolveDispatchStatus(order.배차상태, transport),
            조리예상완료시각Utc = order.조리예상완료시각Utc,
            CreatedAtUtc = order.CreatedAt
        };
    }

    private static 주문자음식주문상세응답 ToDetail(
        음식주문 order,
        운송원장? transport)
        => new()
        {
            주문 = ToSummary(order, transport),
            배달진행 = ToDeliveryProgress(order, transport),
            음식점주소 = order.음식점주소,
            음식점상세주소 = order.음식점상세주소,
            수령인정보 = new 음식주문수령인정보Dto
            {
                수령인명 = order.수령인명,
                연락처 = order.수령인연락처,
                주소 = order.수령지주소,
                상세주소 = order.수령지상세주소,
                요청사항 = order.수령요청사항,
                주문자본인수령여부 = order.주문자본인수령여부
            },
            상품목록 = order.상품목록
                .OrderBy(item => item.Id)
                .Select(item => new 음식주문상품Dto
                {
                    메뉴Id = item.메뉴Id,
                    상품명 = item.상품명,
                    수량 = item.수량,
                    단가 = item.단가
                })
                .ToArray(),
            결제수단 = order.결제수단,
            음식점수락시각Utc = order.음식점수락시각Utc,
            배차요청시각Utc = order.배차요청시각Utc,
            수락메모 = order.수락메모,
            AvailableActions = 음식배달가능행동Projector.주문자용(order.상태, order.상태이력.Count),
            상태이력 = order.상태이력
                .OrderBy(item => item.전이시각Utc)
                .ThenBy(item => item.Id)
                .Select(item => new 음식주문상태전이기록Dto
                {
                    이전상태 = item.이전상태,
                    다음상태 = item.다음상태,
                    사유 = item.사유,
                    전이시각Utc = item.전이시각Utc
                })
                .ToArray()
        };

    private static 주문자음식배달진행응답 ToDeliveryProgress(
        음식주문 order,
        운송원장? transport)
    {
        var currentTransportStatus = Clean(transport?.상태)
                                     ?? Clean(order.배차상태)
                                     ?? 음식주문배차상태코드.미요청;
        var dispatchRequested = transport is not null
                                || currentTransportStatus != 음식주문배차상태코드.미요청;
        var driverAssigned = !string.IsNullOrWhiteSpace(transport?.확정기사Id)
                             || IsDriverAssignedStatus(currentTransportStatus);
        var normalizedOrderStatus = 음식주문상태코드.Normalize(order.상태);
        var delivered = normalizedOrderStatus is
                            음식주문상태코드.전달완료 or
                            음식주문상태코드.수령확인
                        || currentTransportStatus is
                            기사운송상태코드.인수완료 or
                            음식주문배차상태코드.배달완료;
        var receiptConfirmed = normalizedOrderStatus == 음식주문상태코드.수령확인;
        var receiptConfirmedAt = order.상태이력
            .Where(item => item.다음상태 == 음식주문상태코드.수령확인)
            .OrderByDescending(item => item.전이시각Utc)
            .ThenByDescending(item => item.Id)
            .Select(item => (DateTime?)item.전이시각Utc)
            .FirstOrDefault();

        return new 주문자음식배달진행응답
        {
            배차요청됨 = dispatchRequested,
            기사배정됨 = driverAssigned,
            기사전달완료 = delivered,
            주문자수령확인됨 = receiptConfirmed,
            수령확인가능 = normalizedOrderStatus == 음식주문상태코드.전달완료,
            현재운송상태 = currentTransportStatus,
            안내 = ResolveDeliveryGuide(currentTransportStatus, order.상태),
            최근변경시각Utc = Latest(transport?.UpdatedAt, order.UpdatedAt),
            수령확인시각Utc = receiptConfirmedAt
        };
    }

    private static string ResolveDispatchStatus(string? foodDispatchStatus, 운송원장? transport)
    {
        var transportStatus = Clean(transport?.상태);
        if (transportStatus is null)
        {
            return Clean(foodDispatchStatus) ?? 음식주문배차상태코드.미요청;
        }

        return transportStatus switch
        {
            기사운송상태코드.배차대기 => 음식주문배차상태코드.배차대기,
            기사운송상태코드.매칭중 => 음식주문배차상태코드.추천중,
            기사운송상태코드.인수완료 => 음식주문배차상태코드.배달완료,
            기사운송상태코드.상차완료
                or 기사운송상태코드.운송중
                or 기사운송상태코드.하차지도착 => 음식주문배차상태코드.배달중,
            _ when IsDriverAssignedStatus(transportStatus) => 음식주문배차상태코드.기사배정,
            _ => transportStatus
        };
    }

    private static bool IsDriverAssignedStatus(string? status)
        => status is 기사운송상태코드.배차확정
            or 기사운송상태코드.이동중
            or 기사운송상태코드.상차지도착
            or 기사운송상태코드.상차완료
            or 기사운송상태코드.운송중
            or 기사운송상태코드.하차지도착
            or 기사운송상태코드.인수완료
            or 음식주문배차상태코드.기사배정
            or 음식주문배차상태코드.배달중
            or 음식주문배차상태코드.배달완료;

    private static string ResolveDeliveryGuide(string transportStatus, string orderStatus)
    {
        var normalizedOrderStatus = 음식주문상태코드.Normalize(orderStatus);
        if (normalizedOrderStatus == 음식주문상태코드.거절)
        {
            return "음식점이 주문을 처리할 수 없어 거절했습니다. 상세 상태 이력에서 사유를 확인해 주세요.";
        }

        if (normalizedOrderStatus == 음식주문상태코드.취소)
        {
            return "주문이 취소되어 배달이 진행되지 않습니다.";
        }

        if (normalizedOrderStatus == 음식주문상태코드.수령확인)
        {
            return "기사 전달과 주문자 수령 확인이 모두 완료되었습니다.";
        }

        if (normalizedOrderStatus == 음식주문상태코드.전달완료)
        {
            return "기사가 음식 전달을 완료했습니다. 실제 수령 상태를 확인해 주세요.";
        }

        return transportStatus switch
        {
            음식주문배차상태코드.미요청 when 음식주문상태코드.CanRestaurantAccept(orderStatus)
                => "음식점이 주문을 수락하면 배달 기사 배차가 시작됩니다.",
            음식주문배차상태코드.미요청
                => "음식점이 조리와 배달 준비를 확인하고 있습니다.",
            기사운송상태코드.배차대기
                => "음식점이 주문을 수락해 배달 기사를 찾기 시작했습니다.",
            기사운송상태코드.매칭중
                => "배달 기사에게 주문을 제안하고 응답을 기다리고 있습니다.",
            기사운송상태코드.배차확정
                => "배달 기사가 주문을 수락했습니다. 음식점에서 픽업을 준비합니다.",
            기사운송상태코드.이동중
                => "배달 기사가 음식점으로 이동 중입니다.",
            기사운송상태코드.상차지도착
                => "배달 기사가 음식점에 도착했습니다.",
            기사운송상태코드.상차완료 or 기사운송상태코드.운송중
                => "음식을 픽업해 수령지로 이동 중입니다.",
            기사운송상태코드.하차지도착
                => "배달 기사가 수령지에 도착했습니다.",
            기사운송상태코드.인수완료
                => "음식 전달이 완료되었습니다.",
            음식주문배차상태코드.기사배정
                => "배달 기사가 배정되어 음식점 픽업을 준비하고 있습니다.",
            음식주문배차상태코드.배달중
                => "음식을 픽업해 수령지로 이동 중입니다.",
            음식주문배차상태코드.배달완료
                => "음식 전달이 완료되었습니다.",
            음식주문배차상태코드.배차불가
                => "현재 기사 배정을 완료하지 못했습니다. 음식점과 운영 담당자가 배달 가능 여부를 확인하고 있습니다.",
            "추천만료" or "수락취소" or "배차취소"
                => "기존 기사 제안이 종료되어 다시 배달 가능 여부를 확인하고 있습니다.",
            _ => "배달 진행 상태를 확인하고 있습니다."
        };
    }

    private static DateTime? Latest(DateTime? first, DateTime? second)
    {
        if (!first.HasValue)
        {
            return second;
        }

        if (!second.HasValue)
        {
            return first;
        }

        return first.Value >= second.Value ? first : second;
    }

    private static string BuildProductSummary(IReadOnlyList<음식주문상품> products)
    {
        if (products.Count == 0)
        {
            return "상품 정보 없음";
        }

        var names = products
            .Select(item => item.상품명)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Take(2)
            .ToArray();
        var summary = names.Length == 0 ? "상품 정보 없음" : string.Join(", ", names);
        return products.Count > 2 ? $"{summary} 외 {products.Count - 2}종" : summary;
    }

    private static Result<T> Unauthorized<T>()
        => Result.Fail<T>(new Error("음식 주문 내역을 보려면 로그인해 주세요.")
            .WithMetadata("StatusCode", StatusCodes.Status401Unauthorized));

    private static Result<T> NotFound<T>()
        => Result.Fail<T>(new Error("음식 주문을 찾을 수 없습니다.")
            .WithMetadata("StatusCode", StatusCodes.Status404NotFound));

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
