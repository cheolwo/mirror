using System.Text.Json;
using FluentResults;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Payment.Events;
using Ssalddel.Contracts.Shipper.Payment;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using 살뜰.도메인.결제;
using 살뜰.도메인.설정;
using 살뜰.Services.Options;

namespace Ssalddel.Application.Shipper.Payment;

public sealed class 페이크결제승인CommandHandler : IRequestHandler<페이크결제승인Command, Result<페이크결제승인응답>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string ProviderName = "FakePG";

    private readonly SsalddelContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ISsalddelExecutionModePolicy _executionModePolicy;
    private readonly IOptions<TransportPrepaymentOptions> _prepaymentOptions;

    public 페이크결제승인CommandHandler(
        SsalddelContext db,
        ICurrentUserAccessor currentUserAccessor,
        IHostEnvironment hostEnvironment,
        ISsalddelExecutionModePolicy executionModePolicy,
        IOptions<TransportPrepaymentOptions>? prepaymentOptions = null)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _hostEnvironment = hostEnvironment;
        _executionModePolicy = executionModePolicy;
        _prepaymentOptions = prepaymentOptions ?? Options.Create(new TransportPrepaymentOptions());
    }

    public async Task<Result<페이크결제승인응답>> Handle(페이크결제승인Command request, CancellationToken cancellationToken)
    {
        if (!_hostEnvironment.IsDevelopment() && !_executionModePolicy.IsSimulation)
        {
            return Result.Fail<페이크결제승인응답>("FakePG 결제 승인 API는 Simulation 또는 Development 환경에서만 사용할 수 있습니다.");
        }

        if (string.IsNullOrWhiteSpace(request.의뢰Id))
        {
            return Result.Fail<페이크결제승인응답>("의뢰Id is required");
        }

        var requestId = request.의뢰Id.Trim();
        var shipperRequest = await _db.화주운송의뢰
            .FirstOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
        if (shipperRequest is null)
        {
            return Result.Fail<페이크결제승인응답>("의뢰를 찾을 수 없습니다.");
        }

        // 완료 결제의 멱등 재조회·Outbox 복구도 현재 결제 권한을 먼저 확인합니다.
        if (!화주결제권한정책.의뢰결제권한있음(shipperRequest, _currentUserAccessor.UserId, _currentUserAccessor.Role))
            return Result.Fail<페이크결제승인응답>("의뢰를 찾을 수 없습니다.");

        // 기존 완료 결제의 Outbox 재생 경로에서도 모의 배송을 운영 정산으로 승격하지 않습니다.
        if (shipperRequest.모의처리처수행)
        {
            return Result.Fail<페이크결제승인응답>("모의 처리처 수행은 결제·지급·정산 대상이 아닙니다.");
        }

        var alreadyCompletedPayment = await FindCompletedFakePaymentAsync(requestId, cancellationToken);
        if (shipperRequest.결제상태 == 상태값.결제상태.결제완료 && alreadyCompletedPayment is not null)
        {
            if (request.Amount != alreadyCompletedPayment.결제금액)
                return Result.Fail<페이크결제승인응답>("기존 결제 금액과 요청 금액이 다릅니다. 같은 결제를 다시 확인해 주세요.");
            await EnsurePaymentApprovedOutboxAsync(alreadyCompletedPayment, cancellationToken);
            return Result.Ok(ToResponse(alreadyCompletedPayment, alreadyCompleted: true));
        }

        var 진행검증 = 화주운송결제진행정책.결제준비요청검증(
            shipperRequest,
            _currentUserAccessor.UserId,
            _currentUserAccessor.Role,
            선결제허용: _executionModePolicy.IsSimulation && _prepaymentOptions.Value.Enabled);
        if (!진행검증.통과)
        {
            return Result.Fail<페이크결제승인응답>(진행검증.실패사유);
        }

        var serverAmount = 배송료결제준비근거Policy.서버원운임조회(shipperRequest);
        if (serverAmount.IsFailed) return Result.Fail<페이크결제승인응답>(serverAmount.Errors);
        if (request.Amount != serverAmount.Value)
            return Result.Fail<페이크결제승인응답>(new Error("요청 금액이 서버의 확정 운임과 다릅니다. 금액을 다시 확인해 주세요.")
                .WithMetadata("ErrorCode", "PreparationAmountMismatch"));
        var amount = serverAmount.Value;
        var initialPrepayment = 화주운송결제진행정책.선결제초기상태인가(shipperRequest);

        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existingPayment = await _db.결제
                .Where(x => x.의뢰Id == requestId && x.PG사 == ProviderName && x.외부거래번호 == idempotencyKey)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingPayment is not null)
            {
                if (existingPayment.결제상태 != 상태값.결제상태.결제완료
                    || existingPayment.결제금액 != amount)
                    return Result.Fail<페이크결제승인응답>("같은 결제의 처리 결과를 먼저 확인해 주세요.");
                return Result.Ok(ToResponse(existingPayment, alreadyCompleted: true));
            }
        }

        var now = DateTime.UtcNow;
        var paymentKey = $"fake_pg_{Guid.NewGuid():N}";
        var responseJson = BuildFakeResponseJson(
            requestId,
            amount,
            paymentKey,
            idempotencyKey,
            request.메모,
            now,
            _executionModePolicy.Mode);
        var payment = new 결제
        {
            결제Id = $"SIM-FPG-{Guid.NewGuid():N}",
            의뢰Id = requestId,
            화주Id = shipperRequest.화주Id,
            결제대상유형 = 결제공통정의.결제대상유형.용달운송의뢰,
            대상Id = requestId,
            PG사 = ProviderName,
            결제제공자 = 결제공통정의.결제제공자.FakePG,
            결제수단 = string.IsNullOrWhiteSpace(request.결제수단) ? shipperRequest.결제수단 : request.결제수단.Trim(),
            결제상태 = 상태값.결제상태.결제완료,
            공통결제상태 = 결제공통정의.결제상태.승인완료,
            결제금액 = amount,
            통화 = "KRW",
            OrderId = $"ssalddel_fake_{Guid.NewGuid():N}",
            주문명 = $"살뜰 FakePG 운송의뢰 {requestId}",
            PaymentKey = paymentKey,
            외부거래번호 = string.IsNullOrWhiteSpace(idempotencyKey) ? paymentKey : idempotencyKey,
            Toss응답Json = responseJson,
            원본응답Json = responseJson,
            CreatedAt = now,
            승인일시 = now
        };

        var previousMethod = shipperRequest.결제수단;
        var previousExpectedAmount = shipperRequest.결제예정금액;
        shipperRequest.결제수단 = payment.결제수단;
        shipperRequest.결제예정금액 ??= amount;
        if (initialPrepayment)
        {
            var preparation = 배송료결제준비근거Policy.생성(payment, shipperRequest,
                new() { 원운임 = amount, 최종결제금액 = amount });
            if (preparation.IsFailed)
            {
                shipperRequest.결제수단 = previousMethod;
                shipperRequest.결제예정금액 = previousExpectedAmount;
                return Result.Fail<페이크결제승인응답>(preparation.Errors);
            }
            payment.준비근거Json = preparation.Value;
        }
        shipperRequest.결제상태 = 상태값.결제상태.결제완료;
        shipperRequest.정산상태 = 상태값.결제상태.결제완료;
        shipperRequest.UpdatedAt = now;

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        await _db.결제.AddAsync(payment, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await EnsurePaymentApprovedOutboxAsync(payment, cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return Result.Ok(ToResponse(payment, alreadyCompleted: false));
    }

    private async Task EnsurePaymentApprovedOutboxAsync(결제 payment, CancellationToken cancellationToken)
    {
        if (payment.Id <= 0)
        {
            throw new InvalidOperationException("FakePG 결제 레코드가 저장되기 전에 결제완료 Outbox를 생성할 수 없습니다.");
        }

        var exists = await _db.결제승인완료Outbox
            .AsNoTracking()
            .AnyAsync(x => x.결제레코드Id == payment.Id, cancellationToken);
        if (exists)
        {
            return;
        }

        var approvedAtUtc = payment.승인일시 ?? payment.CreatedAt;
        var eventPayload = new 결제승인완료Event(
            payment.Id,
            payment.결제Id,
            payment.결제대상유형,
            payment.대상Id,
            payment.결제제공자,
            payment.결제금액,
            payment.통화,
            approvedAtUtc);

        _db.결제승인완료Outbox.Add(new 결제승인완료Outbox
        {
            결제레코드Id = payment.Id,
            결제Id = payment.결제Id,
            결제대상유형 = payment.결제대상유형,
            대상Id = payment.대상Id,
            결제제공자 = payment.결제제공자,
            결제금액 = payment.결제금액,
            통화 = payment.통화,
            승인일시Utc = approvedAtUtc,
            PayloadJson = JsonSerializer.Serialize(eventPayload, JsonOptions),
            처리상태 = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<결제?> FindCompletedFakePaymentAsync(string requestId, CancellationToken cancellationToken)
    {
        return await _db.결제
            .Where(x => x.의뢰Id == requestId
                        && x.PG사 == ProviderName
                        && x.결제상태 == 상태값.결제상태.결제완료)
            .OrderByDescending(x => x.승인일시 ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string? NormalizeIdempotencyKey(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string BuildFakeResponseJson(
        string requestId,
        int amount,
        string paymentKey,
        string? idempotencyKey,
        string? memo,
        DateTime approvedAtUtc,
        SsalddelExecutionMode executionMode)
    {
        return JsonSerializer.Serialize(new
        {
            provider = ProviderName,
            mode = executionMode.ToString(),
            requestId,
            amount,
            currency = "KRW",
            paymentKey,
            idempotencyKey,
            memo,
            approvedAtUtc
        }, JsonOptions);
    }

    private static 페이크결제승인응답 ToResponse(결제 payment, bool alreadyCompleted)
    {
        return new 페이크결제승인응답
        {
            결제Id = payment.결제Id,
            의뢰Id = payment.의뢰Id,
            결제제공자 = payment.결제제공자,
            OrderId = payment.OrderId,
            PaymentKey = payment.PaymentKey ?? string.Empty,
            Amount = payment.결제금액,
            결제상태 = payment.결제상태,
            결제응답 = payment.원본응답Json ?? payment.Toss응답Json ?? string.Empty,
            승인일시Utc = payment.승인일시 ?? payment.CreatedAt,
            이미완료됨 = alreadyCompleted
        };
    }
}
