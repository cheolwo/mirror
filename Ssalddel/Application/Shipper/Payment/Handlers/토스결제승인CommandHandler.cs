using System.Text.Json;
using FluentResults;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Payment.Events;
using Ssalddel.Contracts.Shipper.Payment;
using 살뜰.도메인.설정;
using 살뜰.Services.Payments;
using 살뜰.도메인.결제;

namespace Ssalddel.Application.Shipper.Payment;

public sealed class 토스결제승인CommandHandler : IRequestHandler<토스결제승인Command, Result<토스결제승인응답>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SsalddelContext _db;
    private readonly I공통결제Service _paymentService;
    private readonly I콘텐츠혜택계산Service _benefitService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public 토스결제승인CommandHandler(
        SsalddelContext db,
        I공통결제Service paymentService,
        I콘텐츠혜택계산Service benefitService,
        ICurrentUserAccessor currentUserAccessor)
    {
        _db = db;
        _paymentService = paymentService;
        _benefitService = benefitService;
        _currentUserAccessor = currentUserAccessor;
    }

    public async Task<Result<토스결제승인응답>> Handle(토스결제승인Command request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PaymentKey))
        {
            return Result.Fail<토스결제승인응답>("paymentKey is required");
        }

        if (string.IsNullOrWhiteSpace(request.OrderId))
        {
            return Result.Fail<토스결제승인응답>("orderId is required");
        }

        if (request.Amount <= 0)
        {
            return Result.Fail<토스결제승인응답>("amount must be greater than 0");
        }

        var payment = await _db.결제.FirstOrDefaultAsync(x => x.OrderId == request.OrderId, cancellationToken);
        if (payment is null)
        {
            return Result.Fail<토스결제승인응답>("결제 요청을 찾을 수 없습니다.");
        }

        var targetId = string.IsNullOrWhiteSpace(payment.대상Id) ? payment.의뢰Id : payment.대상Id;
        var shipperRequest = payment.결제대상유형 == 결제공통정의.결제대상유형.용달운송의뢰
            ? await _db.화주운송의뢰.FirstOrDefaultAsync(x => x.의뢰Id == targetId, cancellationToken)
            : null;
        // 원천 종류가 다른 같은 문자열 의뢰를 결제 권한·모의 여부의 근거로 사용하지 않습니다.
        if (!화주결제권한정책.결제승인권한있음(payment, shipperRequest, _currentUserAccessor.UserId, _currentUserAccessor.Role))
            return Result.Fail<토스결제승인응답>("결제 요청을 찾을 수 없습니다.");
        if (payment.결제대상유형 == 결제공통정의.결제대상유형.음식주문
            && await _db.음식주문.AsNoTracking().AnyAsync(x => x.주문번호 == targetId && x.모의처리처수행, cancellationToken))
            return Result.Fail<토스결제승인응답>("모의 처리처 수행은 결제·지급·정산 대상이 아닙니다.");
        var 진행검증 = 화주운송결제진행정책.결제승인요청검증(
            payment,
            shipperRequest,
            _currentUserAccessor.UserId,
            _currentUserAccessor.Role,
            request.Amount);
        if (!진행검증.통과)
        {
            return Result.Fail<토스결제승인응답>(진행검증.실패사유);
        }

        if (진행검증.이미완료됨)
        {
            return Result.Ok(new 토스결제승인응답
            {
                결제Id = payment.결제Id,
                의뢰Id = payment.의뢰Id,
                OrderId = payment.OrderId,
                PaymentKey = payment.PaymentKey ?? string.Empty,
                결제상태 = payment.결제상태,
                결제응답 = payment.Toss응답Json ?? string.Empty
            });
        }

        var confirmResult = await _paymentService.결제승인Async(
            payment.결제제공자,
            new 결제승인요청(request.PaymentKey, request.OrderId, request.Amount),
            cancellationToken);

        if (!confirmResult.IsSuccess)
        {
            payment.공통결제상태 = 결제공통정의.결제상태.실패;
            payment.원본응답Json = confirmResult.ResponseJson;
            await _db.SaveChangesAsync(cancellationToken);
            return Result.Fail<토스결제승인응답>(confirmResult.ResponseJson);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        payment.PaymentKey = request.PaymentKey;
        payment.외부거래번호 = confirmResult.ExternalTransactionNo;
        payment.결제수단 = string.IsNullOrWhiteSpace(confirmResult.PaymentMethod) ? payment.결제수단 : confirmResult.PaymentMethod;
        payment.결제상태 = 상태값.결제상태.결제완료;
        payment.공통결제상태 = 결제공통정의.결제상태.승인완료;
        payment.Toss응답Json = confirmResult.ResponseJson;
        payment.원본응답Json = confirmResult.ResponseJson;
        payment.승인일시 = DateTime.UtcNow;

        var eventPayload = new 결제승인완료Event(
            payment.Id,
            payment.결제Id,
            payment.결제대상유형,
            payment.대상Id,
            payment.결제제공자,
            payment.결제금액,
            payment.통화,
            payment.승인일시 ?? DateTime.UtcNow);

        _db.결제승인완료Outbox.Add(new 결제승인완료Outbox
        {
            결제레코드Id = payment.Id,
            결제Id = payment.결제Id,
            결제대상유형 = payment.결제대상유형,
            대상Id = payment.대상Id,
            결제제공자 = payment.결제제공자,
            결제금액 = payment.결제금액,
            통화 = payment.통화,
            승인일시Utc = payment.승인일시 ?? DateTime.UtcNow,
            PayloadJson = JsonSerializer.Serialize(eventPayload, JsonOptions),
            처리상태 = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _benefitService.보상사용처리Async(payment.화주Id, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return Result.Ok(new 토스결제승인응답
        {
            결제Id = payment.결제Id,
            의뢰Id = payment.의뢰Id,
            OrderId = payment.OrderId,
            PaymentKey = payment.PaymentKey ?? string.Empty,
            결제상태 = payment.결제상태,
            결제응답 = confirmResult.ResponseJson
        });
    }
    }
