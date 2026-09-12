using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Shipper.Request;
using 살뜰.도메인.운송;

namespace Ssalddel.Services.Operations;

public interface I비정상운송사건Service
{
    Task<비정상운송사건접수결과> 접수Async(
        비정상운송사건접수요청 request,
        CancellationToken cancellationToken = default);

    Task<비정상운송사건검토결과> 검토Async(
        비정상운송사건검토요청 request,
        CancellationToken cancellationToken = default);
}

public sealed class 비정상운송사건Service : I비정상운송사건Service
{
    private readonly SsalddelContext _db;

    public 비정상운송사건Service(SsalddelContext db)
    {
        _db = db;
    }

    public async Task<비정상운송사건접수결과> 접수Async(
        비정상운송사건접수요청 request,
        CancellationToken cancellationToken = default)
    {
        var incidentTypeCode = 유형Code찾기(request.예외Code);
        if (incidentTypeCode is null)
        {
            return new(false, false, null);
        }

        var requestId = request.운송의뢰Id.Trim();
        var shipperRequest = await _db.화주운송의뢰
            .FirstOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
        var quantity = 수량범위검증(
            shipperRequest?.화물수량,
            request.정상확인수량,
            request.영향수량);
        var stableId = 사건StableId(request.운송Id, incidentTypeCode);
        var entity = await _db.비정상운송사건
            .FirstOrDefaultAsync(x => x.사건StableId == stableId, cancellationToken);
        var created = entity is null;

        if (entity is null)
        {
            entity = new 비정상운송사건
            {
                사건StableId = stableId,
                운송Id = request.운송Id,
                운송의뢰Id = requestId,
                사건유형Code = incidentTypeCode,
                원본예외Code = request.예외Code.Trim(),
                단계Code = request.단계.Trim(),
                상태Code = 비정상운송사건상태Codes.운영검토대기,
                현재담당Code = 비정상운송사건담당Codes.플랫폼운영검토,
                전체수량 = quantity.전체수량,
                정상확인수량 = quantity.정상확인수량,
                영향수량 = quantity.영향수량,
                업무통제상태Code = 업무통제상태(request.현장진행불가, quantity),
                보류범위Code = 보류범위(request.현장진행불가, quantity),
                현장진행불가 = request.현장진행불가,
                증빙참조있음 = request.증빙참조있음,
                최초신고시각Utc = request.신고시각Utc,
                최근신고시각Utc = request.신고시각Utc,
                Revision = 1,
                CreatedAt = request.신고시각Utc,
                UpdatedAt = request.신고시각Utc
            };
            _db.비정상운송사건.Add(entity);
        }
        else
        {
            entity.단계Code = request.단계.Trim();
            entity.최근신고시각Utc = request.신고시각Utc;
            entity.증빙참조있음 |= request.증빙참조있음;
            entity.전체수량 = quantity.전체수량 ?? entity.전체수량;
            entity.정상확인수량 = quantity.정상확인수량 ?? entity.정상확인수량;
            entity.영향수량 = quantity.영향수량 ?? entity.영향수량;
            entity.현장진행불가 = request.현장진행불가;
            var effectiveQuantity = new 비정상운송수량범위(
                entity.전체수량,
                entity.정상확인수량,
                entity.영향수량);
            entity.업무통제상태Code = 업무통제상태(request.현장진행불가, effectiveQuantity);
            entity.보류범위Code = 보류범위(request.현장진행불가, effectiveQuantity);
            entity.상태Code = 비정상운송사건상태Codes.운영검토대기;
            entity.현재담당Code = 비정상운송사건담당Codes.플랫폼운영검토;
            entity.Revision += 1;
            entity.UpdatedAt = request.신고시각Utc;
        }

        if (shipperRequest is not null)
        {
            var holdStatus = 운임정산상태.비정상운송검토보류.ToString();
            if (!string.Equals(shipperRequest.정산상태, holdStatus, StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(entity.보류전정산상태Code))
            {
                entity.보류전정산상태Code = shipperRequest.정산상태;
            }

            shipperRequest.정산상태 = holdStatus;
            shipperRequest.UpdatedAt = request.신고시각Utc;
            entity.정산보류적용여부 = true;
        }

        return new(true, created, entity);
    }

    public async Task<비정상운송사건검토결과> 검토Async(
        비정상운송사건검토요청 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stableId = request.사건StableId?.Trim();
        if (string.IsNullOrWhiteSpace(stableId))
            throw new ArgumentException("비정상 운송 사건 식별자가 필요합니다.", nameof(request));
        if (request.클라이언트요청Id == Guid.Empty)
            throw new ArgumentException("검토 클라이언트 요청 ID가 필요합니다.", nameof(request));
        if (request.예상Revision <= 0)
            throw new ArgumentException("검토 대상 revision이 필요합니다.", nameof(request));
        var reviewerId = request.검토자UserId?.Trim();
        if (string.IsNullOrWhiteSpace(reviewerId))
            throw new ArgumentException("검토 운영자 식별자가 필요합니다.", nameof(request));
        var reason = request.검토사유?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("검토 사유가 필요합니다.", nameof(request));
        if (reason.Length > 1000)
            throw new ArgumentException("검토 사유는 1000자 이하여야 합니다.", nameof(request));

        var entity = await _db.비정상운송사건
            .SingleOrDefaultAsync(x => x.사건StableId == stableId, cancellationToken);
        if (entity is null)
            return new(false, false, null);

        var decisionCode = 결정Code검증(request.결정Code);
        if (entity.최근검토요청Id == request.클라이언트요청Id)
        {
            var expectedInsuranceStatus = request.보험적용가능성검토요청
                ? 비정상운송보험검토상태Codes.적용가능성검토대기
                : 비정상운송보험검토상태Codes.미요청;
            var partialDecisionMatches = !string.Equals(
                                             decisionCode,
                                             비정상운송해결결과Codes.정상분인수영향분보류,
                                             StringComparison.Ordinal)
                                         || (entity.정상확인수량 == request.정상확인수량
                                             && entity.영향수량 == request.영향수량
                                             && string.Equals(
                                                 entity.보험검토상태Code,
                                                 expectedInsuranceStatus,
                                                 StringComparison.Ordinal));
            if (!string.Equals(entity.해결결과Code, decisionCode, StringComparison.Ordinal)
                || !partialDecisionMatches
                || !string.Equals(entity.최근검토사유, reason, StringComparison.Ordinal)
                || !string.Equals(entity.최근검토자UserId, reviewerId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("같은 검토 요청 ID를 다른 결정 내용으로 다시 사용할 수 없습니다.");
            }

            return new(true, true, entity);
        }

        if (entity.Revision != request.예상Revision)
            throw new DbUpdateConcurrencyException("비정상 운송 사건이 다른 요청에서 먼저 변경됐습니다.");

        var shipperRequest = await _db.화주운송의뢰
            .SingleOrDefaultAsync(x => x.의뢰Id == entity.운송의뢰Id, cancellationToken);
        var now = DateTime.UtcNow;
        switch (decisionCode)
        {
            case 비정상운송해결결과Codes.정상분인수영향분보류:
            {
                var quantity = 수량범위검증(
                    entity.전체수량 ?? shipperRequest?.화물수량,
                    request.정상확인수량,
                    request.영향수량);
                if (!quantity.전체수량.HasValue
                    || !quantity.정상확인수량.HasValue
                    || !quantity.영향수량.HasValue
                    || quantity.정상확인수량 <= 0
                    || quantity.영향수량 <= 0
                    || quantity.영향수량 >= quantity.전체수량)
                {
                    throw new InvalidOperationException("일부 인수 결정에는 전체보다 작은 영향 수량과 1개 이상의 정상 수량이 필요합니다.");
                }
                if (entity.현장진행불가)
                    throw new InvalidOperationException("현장 진행 불가 사건은 일부 인수로 결정할 수 없습니다.");

                entity.전체수량 = quantity.전체수량;
                entity.정상확인수량 = quantity.정상확인수량;
                entity.영향수량 = quantity.영향수량;
                entity.업무통제상태Code = 비정상운송업무통제상태Codes.일부보류;
                entity.보류범위Code = 비정상운송보류범위Codes.영향수량;
                entity.정산보류적용여부 = false;
                entity.보험검토상태Code = request.보험적용가능성검토요청
                    ? 비정상운송보험검토상태Codes.적용가능성검토대기
                    : 비정상운송보험검토상태Codes.미요청;
                entity.상태Code = 비정상운송사건상태Codes.조치결정;
                break;
            }
            case 비정상운송해결결과Codes.전체운송보류:
                entity.업무통제상태Code = 비정상운송업무통제상태Codes.전체보류;
                entity.보류범위Code = 비정상운송보류범위Codes.전체운송;
                entity.정산보류적용여부 = true;
                entity.상태Code = 비정상운송사건상태Codes.조치결정;
                break;
            case 비정상운송해결결과Codes.운송재개:
                entity.업무통제상태Code = 비정상운송업무통제상태Codes.재개;
                entity.보류범위Code = 비정상운송보류범위Codes.없음;
                entity.정산보류적용여부 = false;
                entity.상태Code = 비정상운송사건상태Codes.조치결정;
                break;
            case 비정상운송해결결과Codes.사건종료:
                entity.업무통제상태Code = 비정상운송업무통제상태Codes.종료;
                entity.보류범위Code = 비정상운송보류범위Codes.없음;
                entity.정산보류적용여부 = false;
                entity.상태Code = 비정상운송사건상태Codes.종료;
                break;
        }

        entity.해결결과Code = decisionCode;
        entity.최근검토요청Id = request.클라이언트요청Id;
        entity.최근검토자UserId = reviewerId;
        entity.최근검토사유 = reason;
        entity.최근검토시각Utc = now;
        entity.Revision += 1;
        entity.UpdatedAt = now;

        if (shipperRequest is not null)
        {
            if (entity.정산보류적용여부)
            {
                shipperRequest.정산상태 = 운임정산상태.비정상운송검토보류.ToString();
            }
            else
            {
                var anotherHoldExists = await _db.비정상운송사건
                    .AsNoTracking()
                    .AnyAsync(
                        x => x.운송의뢰Id == entity.운송의뢰Id
                             && x.사건StableId != entity.사건StableId
                             && (x.정산보류적용여부
                                 || x.상태Code == 비정상운송사건상태Codes.운영검토대기),
                        cancellationToken);
                if (!anotherHoldExists
                    && string.Equals(
                        shipperRequest.정산상태,
                        운임정산상태.비정상운송검토보류.ToString(),
                        StringComparison.Ordinal))
                {
                    shipperRequest.정산상태 = string.IsNullOrWhiteSpace(entity.보류전정산상태Code)
                        ? 운임정산상태.입금확인완료.ToString()
                        : entity.보류전정산상태Code;
                }
            }

            shipperRequest.UpdatedAt = now;
        }

        return new(true, false, entity);
    }

    public static string? 유형Code찾기(string? 예외Code)
        => 예외Code?.Trim() switch
        {
            "수량불일치" => 비정상운송사건유형Codes.수량불일치,
            "화물훼손" => 비정상운송사건유형Codes.화물훼손,
            "하차지부재" => 비정상운송사건유형Codes.하차지부재,
            _ => null
        };

    public static string 사건StableId(long 운송Id, string 사건유형Code)
        => $"abnormal-transport:{운송Id}:{사건유형Code}";

    private static 비정상운송수량범위 수량범위검증(
        int? 전체수량,
        int? 정상확인수량,
        int? 영향수량)
    {
        if (!정상확인수량.HasValue && !영향수량.HasValue)
            return new(전체수량, null, null);
        if (!정상확인수량.HasValue || !영향수량.HasValue)
            throw new ArgumentException("정상 확인 수량과 영향 수량을 함께 입력해야 합니다.");
        if (정상확인수량 < 0 || 영향수량 <= 0)
            throw new ArgumentException("정상 확인 수량은 0 이상, 영향 수량은 1 이상이어야 합니다.");

        var accountedTotal = checked(정상확인수량.Value + 영향수량.Value);
        var resolvedTotal = 전체수량 ?? accountedTotal;
        if (resolvedTotal <= 0 || accountedTotal != resolvedTotal)
            throw new ArgumentException("정상 확인 수량과 영향 수량의 합이 전체 화물 수량과 같아야 합니다.");

        return new(resolvedTotal, 정상확인수량, 영향수량);
    }

    private static string 업무통제상태(bool 현장진행불가, 비정상운송수량범위 quantity)
        => 현장진행불가
            ? 비정상운송업무통제상태Codes.전체보류
            : quantity.영향수량.HasValue
                ? 비정상운송업무통제상태Codes.일부보류
                : 비정상운송업무통제상태Codes.주의진행;

    private static string 보류범위(bool 현장진행불가, 비정상운송수량범위 quantity)
        => 현장진행불가
            ? 비정상운송보류범위Codes.전체운송
            : quantity.영향수량.HasValue
                ? 비정상운송보류범위Codes.영향수량
                : 비정상운송보류범위Codes.없음;

    private static string 결정Code검증(string? decisionCode)
        => decisionCode?.Trim() switch
        {
            비정상운송해결결과Codes.정상분인수영향분보류 => 비정상운송해결결과Codes.정상분인수영향분보류,
            비정상운송해결결과Codes.전체운송보류 => 비정상운송해결결과Codes.전체운송보류,
            비정상운송해결결과Codes.운송재개 => 비정상운송해결결과Codes.운송재개,
            비정상운송해결결과Codes.사건종료 => 비정상운송해결결과Codes.사건종료,
            _ => throw new ArgumentException("지원하지 않는 비정상 운송 검토 결정입니다.", nameof(decisionCode))
        };
}

public sealed record 비정상운송사건접수요청(
    long 운송Id,
    string 운송의뢰Id,
    string 단계,
    string 예외Code,
    bool 증빙참조있음,
    DateTime 신고시각Utc,
    int? 정상확인수량 = null,
    int? 영향수량 = null,
    bool 현장진행불가 = false);

public sealed record 비정상운송사건접수결과(
    bool 처리대상,
    bool 신규생성,
    비정상운송사건? 사건);

public sealed record 비정상운송사건검토요청(
    string 사건StableId,
    Guid 클라이언트요청Id,
    long 예상Revision,
    string 결정Code,
    int? 정상확인수량,
    int? 영향수량,
    bool 보험적용가능성검토요청,
    string 검토사유,
    string 검토자UserId);

public sealed record 비정상운송사건검토결과(
    bool 찾음,
    bool 멱등재시도,
    비정상운송사건? 사건);

internal sealed record 비정상운송수량범위(
    int? 전체수량,
    int? 정상확인수량,
    int? 영향수량);
