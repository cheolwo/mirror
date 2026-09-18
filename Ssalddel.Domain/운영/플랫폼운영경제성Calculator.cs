using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Contracts.Admin.Operations;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Domain.운영;

public interface I플랫폼운영경제성Calculator
{
    플랫폼운영경제성평가응답Dto 평가(플랫폼운영경제성평가요청Dto 요청);
}

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.PlatformOperatingEconomics,
    SsalddelCodeLayer.Domain,
    "분류된 수익·비용·현금 항목으로 기여금, 영업이익 후보와 손익분기 주문수를 결정적으로 계산합니다.",
    StepKey = "domain.platform-operating-economics-calculate",
    DependsOnStepKeys = ["contract.platform-operating-economics"],
    FlowOrder = 20,
    ExecutionStage = SsalddelCodeExecutionStage.Preview,
    Effects = SsalddelCodeEffect.None,
    Boundary = "한 시나리오 안에서 통화를 환산하지 않으며, 현금·정산·회계 원장을 쓰지 않습니다.")]
public sealed class 플랫폼운영경제성Calculator : I플랫폼운영경제성Calculator
{
    public 플랫폼운영경제성평가응답Dto 평가(플랫폼운영경제성평가요청Dto 요청)
    {
        ArgumentNullException.ThrowIfNull(요청);
        Validate(요청);

        var items = 요청.항목
            .Select(Normalize)
            .OrderBy(item => item.StableId, StringComparer.Ordinal)
            .ToArray();
        var totals = items
            .GroupBy(item => item.분류Code, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.금액), StringComparer.Ordinal);

        var gross = Total(플랫폼운영경제성금액분류Codes.총거래액);
        var orderRevenue = Total(플랫폼운영경제성금액분류Codes.주문수익후보);
        var recurringRevenue = Total(플랫폼운영경제성금액분류Codes.반복수익후보);
        var passThrough = Total(플랫폼운영경제성금액분류Codes.통과자금);
        var variableCost = Total(플랫폼운영경제성금액분류Codes.변동비용);
        var fixedCost = Total(플랫폼운영경제성금액분류Codes.고정비용);
        var cashReceipt = Total(플랫폼운영경제성금액분류Codes.현금유입);
        var cashPayment = Total(플랫폼운영경제성금액분류Codes.현금유출);
        var contribution = orderRevenue - variableCost;
        decimal? contributionPerOrder = 요청.완료주문수 > 0
            ? contribution / 요청.완료주문수
            : null;
        decimal? contributionMarginRate = orderRevenue > 0m ? contribution / orderRevenue : null;
        var breakEvenOrders = CalculateBreakEvenOrders(fixedCost, recurringRevenue, contributionPerOrder);
        long? additionalOrders = breakEvenOrders.HasValue
            ? Math.Max(0L, breakEvenOrders.Value - 요청.완료주문수)
            : null;
        var warnings = BuildWarnings(요청, contributionPerOrder);

        return new 플랫폼운영경제성평가응답Dto
        {
            시나리오StableId = 요청.시나리오StableId.Trim(),
            국가Code = 요청.국가Code.Trim().ToUpperInvariant(),
            관할Code = 요청.관할Code.Trim(),
            통화Code = 요청.통화Code.Trim().ToUpperInvariant(),
            기간시작일 = 요청.기간시작일,
            기간종료일 = 요청.기간종료일,
            완료주문수 = 요청.완료주문수,
            본인대리인가정Code = 요청.본인대리인가정Code.Trim(),
            총거래액 = RoundMoney(gross),
            주문수익후보 = RoundMoney(orderRevenue),
            반복수익후보 = RoundMoney(recurringRevenue),
            플랫폼보유수익후보 = RoundMoney(orderRevenue + recurringRevenue),
            통과자금 = RoundMoney(passThrough),
            변동비용 = RoundMoney(variableCost),
            고정비용 = RoundMoney(fixedCost),
            총비용 = RoundMoney(variableCost + fixedCost),
            기여금 = RoundMoney(contribution),
            주문당기여금 = contributionPerOrder.HasValue ? RoundMoney(contributionPerOrder.Value) : null,
            기여이익률 = contributionMarginRate.HasValue ? RoundRate(contributionMarginRate.Value) : null,
            영업이익후보 = RoundMoney(orderRevenue + recurringRevenue - variableCost - fixedCost),
            손익분기완료주문수 = breakEvenOrders,
            손익분기추가필요주문수 = additionalOrders,
            손익분기도달 = breakEvenOrders.HasValue && 요청.완료주문수 >= breakEvenOrders.Value,
            현금유입 = RoundMoney(cashReceipt),
            현금유출 = RoundMoney(cashPayment),
            기말가용현금 = RoundMoney(요청.기초가용현금 + cashReceipt - cashPayment),
            결과Hash = BuildHash(요청, items),
            주의사항Codes = warnings
        };

        decimal Total(string code) => totals.GetValueOrDefault(code);
    }

    private static 플랫폼운영경제성금액항목Dto Normalize(플랫폼운영경제성금액항목Dto item)
        => new()
        {
            StableId = item.StableId.Trim(),
            분류Code = item.분류Code.Trim(),
            금액 = item.금액,
            근거Revision = item.근거Revision.Trim()
        };

    private static long? CalculateBreakEvenOrders(
        decimal fixedCost,
        decimal recurringRevenue,
        decimal? contributionPerOrder)
    {
        var remainingFixedCost = Math.Max(0m, fixedCost - recurringRevenue);
        if (remainingFixedCost == 0m)
        {
            return 0L;
        }

        if (!contributionPerOrder.HasValue || contributionPerOrder.Value <= 0m)
        {
            return null;
        }

        var calculated = decimal.Ceiling(remainingFixedCost / contributionPerOrder.Value);
        if (calculated > long.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedCost), "손익분기 주문수가 표현 범위를 초과합니다.");
        }

        return decimal.ToInt64(calculated);
    }

    private static IReadOnlyList<string> BuildWarnings(
        플랫폼운영경제성평가요청Dto request,
        decimal? contributionPerOrder)
    {
        var warnings = new List<string>();
        if (string.Equals(
                request.본인대리인가정Code.Trim(),
                플랫폼운영경제성본인대리인가정Codes.미정,
                StringComparison.Ordinal))
        {
            warnings.Add(플랫폼운영경제성주의Codes.본인대리인미정);
        }

        if (request.완료주문수 == 0)
        {
            warnings.Add(플랫폼운영경제성주의Codes.주문당지표계산불가);
        }
        else if (contributionPerOrder <= 0m)
        {
            warnings.Add(플랫폼운영경제성주의Codes.기여금비양수);
        }

        warnings.Add(플랫폼운영경제성주의Codes.실제회계매핑승인필요);
        return warnings;
    }

    private static string BuildHash(
        플랫폼운영경제성평가요청Dto request,
        IReadOnlyList<플랫폼운영경제성금액항목Dto> items)
    {
        var canonical = new StringBuilder()
            .Append(request.시나리오StableId.Trim()).Append('|')
            .Append(request.국가Code.Trim().ToUpperInvariant()).Append('|')
            .Append(request.관할Code.Trim()).Append('|')
            .Append(request.통화Code.Trim().ToUpperInvariant()).Append('|')
            .Append(request.기간시작일.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('|')
            .Append(request.기간종료일.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('|')
            .Append(request.완료주문수.ToString(CultureInfo.InvariantCulture)).Append('|')
            .Append(FormatDecimal(request.기초가용현금)).Append('|')
            .Append(request.본인대리인가정Code.Trim()).Append('|')
            .Append(request.입력Revision.Trim());

        foreach (var item in items)
        {
            canonical.Append('\n')
                .Append(item.StableId).Append('|')
                .Append(item.분류Code).Append('|')
                .Append(FormatDecimal(item.금액)).Append('|')
                .Append(item.근거Revision);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static void Validate(플랫폼운영경제성평가요청Dto request)
    {
        RequireText(request.시나리오StableId, 200, "시나리오 Stable ID");
        RequireAsciiLetters(request.국가Code, 2, "국가 코드");
        RequireText(request.관할Code, 100, "관할 코드");
        RequireAsciiLetters(request.통화Code, 3, "통화 코드");
        RequireText(request.입력Revision, 200, "입력 revision");

        if (request.기간종료일 < request.기간시작일)
        {
            throw new ArgumentException("기간 종료일은 시작일보다 앞설 수 없습니다.", nameof(request));
        }

        if (request.완료주문수 < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "완료 주문 수는 0 이상이어야 합니다.");
        }

        if (request.기초가용현금 < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "기초 가용현금은 0 이상이어야 합니다.");
        }

        if (!플랫폼운영경제성본인대리인가정Codes.All.Contains(request.본인대리인가정Code.Trim()))
        {
            throw new ArgumentException("지원하지 않는 본인·대리인 가정 코드입니다.", nameof(request));
        }

        if (request.항목 is null)
        {
            throw new ArgumentException("금액 항목 목록이 필요합니다.", nameof(request));
        }

        foreach (var item in request.항목)
        {
            if (item is null)
            {
                throw new ArgumentException("금액 항목은 null일 수 없습니다.", nameof(request));
            }

            RequireText(item.StableId, 200, "금액 항목 Stable ID");
            RequireText(item.근거Revision, 200, "금액 항목 근거 revision");
            if (!플랫폼운영경제성금액분류Codes.All.Contains(item.분류Code?.Trim() ?? string.Empty))
            {
                throw new ArgumentException($"지원하지 않는 금액 분류 코드입니다: {item.분류Code}", nameof(request));
            }

            if (item.금액 < 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(request), "금액 항목은 0 이상의 양수로 입력해야 합니다.");
            }
        }

        if (request.항목
            .GroupBy(item => item.StableId.Trim(), StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException("금액 항목 Stable ID를 중복할 수 없습니다.", nameof(request));
        }
    }

    private static void RequireText(string? value, int maximumLength, string displayName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength)
        {
            throw new ArgumentException($"{displayName}은(는) 1~{maximumLength}자여야 합니다.");
        }
    }

    private static void RequireAsciiLetters(string? value, int length, string displayName)
    {
        var normalized = value?.Trim();
        if (normalized is null
            || normalized.Length != length
            || normalized.Any(character => character is not (>= 'A' and <= 'Z') and not (>= 'a' and <= 'z')))
        {
            throw new ArgumentException($"{displayName}는 영문 {length}자여야 합니다.");
        }
    }

    private static string FormatDecimal(decimal value)
        => value.ToString("0.############################", CultureInfo.InvariantCulture);

    private static decimal RoundMoney(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal RoundRate(decimal value)
        => Math.Round(value, 6, MidpointRounding.AwayFromZero);
}
