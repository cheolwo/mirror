using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Application.Admin.Food;

public sealed record 음식배달운영생명주기조화입력(
    string 주문상태,
    string 음식점준비지연상태Code = 음식배달운영지연상태Codes.없음,
    int 음식점준비초과분 = 0,
    string 배달진행지연상태Code = 음식배달운영지연상태Codes.없음,
    int 배달진행초과분 = 0,
    bool 배차원장누락여부 = false,
    bool 배차연결불일치여부 = false,
    bool 기사추천만료여부 = false,
    bool 최근배달시도중단여부 = false,
    bool 공동원장동기화확인필요여부 = false,
    bool 기사알림확인필요여부 = false);

/// <summary>
/// 각 역할 앱의 별도 상태기를 만들지 않고 음식 주문 원장과 배차·Outbox 진단을
/// 현재 단계, 책임 역할, 자동회복 또는 운영자 확인 대상으로 투영합니다.
/// </summary>
public static class 음식배달운영생명주기조화Projector
{
    public static 음식배달운영생명주기조화응답 판정(
        음식배달운영생명주기조화입력 input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var orderState = 음식주문상태코드.Normalize(input.주문상태);
        var (stage, normalRoles) = 정상경로(orderState);
        var workDelays = 업무지연목록(input);
        var automaticRecoveries = 자동회복목록(input);
        var technicalFailures = 기술이상목록(input);
        var delayNeedsReview = input.음식점준비지연상태Code
                                   == 음식배달운영지연상태Codes.운영자확인필요
                               || input.배달진행지연상태Code
                                   == 음식배달운영지연상태Codes.운영자확인필요;
        var delayNeedsAttention = input.음식점준비지연상태Code
                                      == 음식배달운영지연상태Codes.주의
                                  || input.배달진행지연상태Code
                                      == 음식배달운영지연상태Codes.주의;
        var operatorReviewRequired = delayNeedsReview || technicalFailures.Count > 0;
        var automaticRecovery = automaticRecoveries.Count > 0;
        var exceptions = 운영자확인업무지연목록(input)
            .Concat(technicalFailures)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var responsibleRoles = normalRoles.ToList();
        if (automaticRecovery
            && !responsibleRoles.Contains(
                음식배달운영책임주체Codes.배차Engine,
                StringComparer.Ordinal))
        {
            responsibleRoles.Add(음식배달운영책임주체Codes.배차Engine);
        }
        if (operatorReviewRequired)
        {
            responsibleRoles.Add(음식배달운영책임주체Codes.플랫폼운영자);
        }
        if (input.음식점준비지연상태Code != 음식배달운영지연상태Codes.없음)
        {
            responsibleRoles.Add(음식배달운영책임주체Codes.음식점);
        }
        if (input.배달진행지연상태Code != 음식배달운영지연상태Codes.없음)
        {
            responsibleRoles.Add(음식배달운영책임주체Codes.음식배달기사);
        }

        return new 음식배달운영생명주기조화응답
        {
            현재단계Code = stage,
            주의상태Code = operatorReviewRequired
                ? 음식배달운영주의상태Codes.운영자확인필요
                : delayNeedsAttention
                    ? 음식배달운영주의상태Codes.주의
                    : automaticRecovery
                        ? 음식배달운영주의상태Codes.자동회복중
                        : 음식배달운영주의상태Codes.정상,
            현재책임주체Codes = responsibleRoles
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            업무지연Codes = workDelays,
            자동회복Codes = automaticRecoveries,
            기술이상Codes = technicalFailures,
            예외Codes = exceptions,
            음식점준비지연상태Code = input.음식점준비지연상태Code,
            음식점준비초과분 = Math.Max(0, input.음식점준비초과분),
            배달진행지연상태Code = input.배달진행지연상태Code,
            배달진행초과분 = Math.Max(0, input.배달진행초과분),
            정상경로조화여부 = workDelays.Count == 0
                                && automaticRecoveries.Count == 0
                                && technicalFailures.Count == 0,
            자동회복대상여부 = automaticRecovery,
            운영자확인필요여부 = operatorReviewRequired
        };
    }

    private static (string Stage, IReadOnlyList<string> Roles) 정상경로(string orderState)
        => orderState switch
        {
            음식주문상태코드.주문대기 => (
                음식배달운영생명주기단계Codes.음식점응답대기,
                [음식배달운영책임주체Codes.음식점]),
            음식주문상태코드.주문확인 => (
                음식배달운영생명주기단계Codes.기사확보대기,
                [음식배달운영책임주체Codes.배차Engine]),
            음식주문상태코드.조리중 => (
                음식배달운영생명주기단계Codes.조리배차병행,
                [
                    음식배달운영책임주체Codes.음식점,
                    음식배달운영책임주체Codes.배차Engine
                ]),
            음식주문상태코드.픽업대기 => (
                음식배달운영생명주기단계Codes.픽업인계,
                [
                    음식배달운영책임주체Codes.음식점,
                    음식배달운영책임주체Codes.배차Engine
                ]),
            음식주문상태코드.기사배정 => (
                음식배달운영생명주기단계Codes.픽업인계,
                [
                    음식배달운영책임주체Codes.음식점,
                    음식배달운영책임주체Codes.음식배달기사
                ]),
            음식주문상태코드.픽업완료 => (
                음식배달운영생명주기단계Codes.배송,
                [음식배달운영책임주체Codes.음식배달기사]),
            음식주문상태코드.전달완료 => (
                음식배달운영생명주기단계Codes.수령확인대기,
                [음식배달운영책임주체Codes.주문자]),
            음식주문상태코드.수령확인 or 음식주문상태코드.거절 or 음식주문상태코드.취소 => (
                음식배달운영생명주기단계Codes.종료,
                []),
            _ => throw new ArgumentOutOfRangeException(nameof(orderState))
        };

    private static IReadOnlyList<string> 업무지연목록(
        음식배달운영생명주기조화입력 input)
    {
        var values = new List<string>();
        if (input.음식점준비지연상태Code != 음식배달운영지연상태Codes.없음)
            values.Add(음식배달운영업무지연Codes.음식점준비지연);
        if (input.배달진행지연상태Code != 음식배달운영지연상태Codes.없음)
            values.Add(음식배달운영업무지연Codes.배달진행지연);
        return values;
    }

    private static IReadOnlyList<string> 자동회복목록(
        음식배달운영생명주기조화입력 input)
    {
        var values = new List<string>();
        if (input.기사추천만료여부)
            values.Add(음식배달운영자동회복Codes.기사추천만료);
        if (input.최근배달시도중단여부)
            values.Add(음식배달운영자동회복Codes.최근배달시도중단);
        return values;
    }

    private static IReadOnlyList<string> 운영자확인업무지연목록(
        음식배달운영생명주기조화입력 input)
    {
        var values = new List<string>();
        if (input.음식점준비지연상태Code
            == 음식배달운영지연상태Codes.운영자확인필요)
        {
            values.Add(음식배달운영업무지연Codes.음식점준비지연);
        }
        if (input.배달진행지연상태Code
            == 음식배달운영지연상태Codes.운영자확인필요)
        {
            values.Add(음식배달운영업무지연Codes.배달진행지연);
        }
        return values;
    }

    private static IReadOnlyList<string> 기술이상목록(
        음식배달운영생명주기조화입력 input)
    {
        var values = new List<string>();
        if (input.배차원장누락여부)
            values.Add(음식배달운영기술이상Codes.배차원장누락);
        if (input.배차연결불일치여부)
            values.Add(음식배달운영기술이상Codes.배차연결불일치);
        if (input.공동원장동기화확인필요여부)
            values.Add(음식배달운영기술이상Codes.공동원장동기화확인필요);
        if (input.기사알림확인필요여부)
            values.Add(음식배달운영기술이상Codes.기사알림확인필요);
        return values;
    }
}
