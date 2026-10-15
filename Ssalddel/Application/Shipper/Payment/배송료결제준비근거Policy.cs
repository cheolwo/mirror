using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentResults;
using Ssalddel.Contracts.CommonContents;
using 살뜰.Services.Payments;
using 살뜰.도메인.결제;
using 살뜰.도메인.공통;
using 살뜰.도메인.화주;

namespace Ssalddel.Application.Shipper.Payment;

/// <summary>배송료의 원천 금액·혜택을 동결합니다. 업무 상태와 갱신 시각은 견적 판본이 아닙니다.</summary>
public static class 배송료결제준비근거Policy
{
    public const string PolicyRevision = "transport-prepayment-r73";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string PolicyHash = Hash(PolicyRevision + "|KRW|server-payment-amount-or-final-fare|benefit-snapshot|no-work-status");

    public static Result<int> 서버원운임조회(화주운송의뢰 source)
    {
        if (source.결제예정금액 is { } expected)
            return expected > 0 ? Result.Ok(expected) : Fail<int>("PreparationAmountUnavailable", "서버 결제 예정금액을 먼저 확정해 주세요.");
        if (source.최종운임 is not { } fare || fare <= 0 || fare > int.MaxValue || decimal.Truncate(fare) != fare)
            return Fail<int>("PreparationAmountUnavailable", "서버 운임을 원 단위의 유효한 금액으로 먼저 확정해 주세요.");
        return Result.Ok(decimal.ToInt32(fare));
    }

    public static Result<string> 생성(결제 payment, 화주운송의뢰 source, 결제혜택견적응답 benefit)
    {
        var gross = 서버원운임조회(source);
        if (gross.IsFailed) return Result.Fail<string>(gross.Errors);
        if (!ValidBenefit(benefit, gross.Value) || payment.결제금액 != benefit.최종결제금액
            || payment.통화 != "KRW" || !SameTarget(payment, source))
            return Fail<string>("PreparationEvidenceInvalid", "결제 금액과 서버 운임·혜택 근거가 일치하지 않습니다.");
        var payload = new EvidencePayload(PolicyRevision, PolicyHash, payment.결제Id, payment.OrderId,
            Quote(source), new Benefit(benefit.원운임, benefit.포인트사용가능액, benefit.콘텐츠할인금액, benefit.최종결제금액));
        return Result.Ok(JsonSerializer.Serialize(new Evidence(payload, Hash(JsonSerializer.Serialize(payload, JsonOptions))), JsonOptions));
    }

    public static Result 현재근거검증(결제 payment, 화주운송의뢰? source)
    {
        if (source is null || source.모의처리처수행 || !SameTarget(payment, source)
            || source.상태 == 상태값.의뢰상태.취소 || source.배차상태 == 상태값.배차상태.취소)
            return Fail("PreparationSourceUnavailable", "결제 원천 의뢰를 다시 확인해 주세요.");
        var evidence = Read(payment);
        if (evidence.IsFailed) return Result.Fail(evidence.Errors);
        if (evidence.Value.Payload.Source != Quote(source))
            return Fail("PreparationQuoteChanged", "운임 또는 정산 조건이 변경되었습니다. 기존 결제를 승인하지 않고 다시 준비해 주세요.");
        return Result.Ok();
    }

    public static Result<int> 원운임조회(결제 payment)
    {
        var evidence = Read(payment);
        return evidence.IsFailed ? Result.Fail<int>(evidence.Errors) : Result.Ok(evidence.Value.Payload.Benefit.Gross);
    }

    public static Result 혜택근거검증(결제 payment, 결제혜택견적응답 currentBenefit)
    {
        var evidence = Read(payment);
        if (evidence.IsFailed) return Result.Fail(evidence.Errors);
        var expected = evidence.Value.Payload.Benefit;
        if (!ValidBenefit(currentBenefit, expected.Gross)
            || expected != new Benefit(currentBenefit.원운임, currentBenefit.포인트사용가능액,
                currentBenefit.콘텐츠할인금액, currentBenefit.최종결제금액))
            return Fail("PreparationBenefitChanged", "적용 가능한 혜택이 변경되었습니다. 결제 금액을 다시 확인하고 준비해 주세요.");
        return Result.Ok();
    }

    public static async Task<Result> 현재혜택검증Async(결제 payment, I콘텐츠혜택계산Service benefits, CancellationToken cancellationToken = default)
    {
        var gross = 원운임조회(payment);
        if (gross.IsFailed) return Result.Fail(gross.Errors);
        var current = await benefits.계산Async(payment.화주Id, gross.Value, cancellationToken);
        return 혜택근거검증(payment, current);
    }

    private static Result<Evidence> Read(결제 payment)
    {
        if (string.IsNullOrWhiteSpace(payment.준비근거Json))
            return Fail<Evidence>("PreparationEvidenceMissing", "이전 결제 준비 근거가 없습니다. 금액을 확인한 뒤 결제를 다시 준비해 주세요.");
        try
        {
            var evidence = JsonSerializer.Deserialize<Evidence>(payment.준비근거Json, JsonOptions);
            if (evidence?.Payload is not { Source: { }, Benefit: { } } payload
                || payload.Revision != PolicyRevision || payload.PolicyHash != PolicyHash
                || evidence.Hash != Hash(JsonSerializer.Serialize(payload, JsonOptions))
                || payload.PaymentId != payment.결제Id || payload.OrderId != payment.OrderId
                || payment.통화 != "KRW" || payload.Benefit.Net != payment.결제금액
                || payload.Source.ShipperId != payment.화주Id || payload.Source.RequestId != payment.의뢰Id
                || payload.Source.RequestId != payment.대상Id
                || payment.결제대상유형 != 결제공통정의.결제대상유형.용달운송의뢰
                || !ValidBenefit(new() { 원운임 = payload.Benefit.Gross, 포인트사용가능액 = payload.Benefit.Points,
                    콘텐츠할인금액 = payload.Benefit.Discount, 최종결제금액 = payload.Benefit.Net }, payload.Benefit.Gross))
                return Fail<Evidence>("PreparationEvidenceInvalid", "결제 준비 근거가 유효하지 않습니다. 결제를 다시 준비해 주세요.");
            return Result.Ok(evidence);
        }
        catch (JsonException) { return Fail<Evidence>("PreparationEvidenceInvalid", "결제 준비 근거를 읽지 못했습니다. 결제를 다시 준비해 주세요."); }
    }

    private static bool SameTarget(결제 payment, 화주운송의뢰 source) => !string.IsNullOrWhiteSpace(source.의뢰Id)
        && payment.결제대상유형 == 결제공통정의.결제대상유형.용달운송의뢰
        && payment.의뢰Id == source.의뢰Id && payment.대상Id == source.의뢰Id && payment.화주Id == source.화주Id;
    private static bool ValidBenefit(결제혜택견적응답 benefit, int gross) => gross > 0 && benefit.원운임 == gross
        && benefit.포인트사용가능액 >= 0 && benefit.콘텐츠할인금액 >= 0 && benefit.최종결제금액 > 0
        && (long)benefit.최종결제금액 == (long)gross - benefit.포인트사용가능액 - benefit.콘텐츠할인금액;
    private static FareQuote Quote(화주운송의뢰 source) => new(source.의뢰Id, source.화주Id, source.주문자UserId,
        source.결제예정금액, source.최종운임, source.운임구성Id, source.대기료, source.수작업비, source.할증,
        source.정산시점, source.수납주체, source.결제수단);
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static Result Fail(string code, string message) => Result.Fail(new Error(message).WithMetadata("ErrorCode", code));
    private static Result<T> Fail<T>(string code, string message) => Result.Fail<T>(new Error(message).WithMetadata("ErrorCode", code));
    private sealed record Evidence(EvidencePayload Payload, string Hash);
    private sealed record EvidencePayload(string Revision, string PolicyHash, string PaymentId, string OrderId, FareQuote Source, Benefit Benefit);
    private sealed record FareQuote(string RequestId, string ShipperId, string OrdererId, int? ExpectedAmount, decimal? FinalFare,
        long? FareCompositionId, decimal? WaitingFee, decimal? HandlingFee, decimal? Surcharge, string SettlementTime, string Collector, string PaymentMethod);
    private sealed record Benefit(int Gross, int Points, int Discount, int Net);
}
