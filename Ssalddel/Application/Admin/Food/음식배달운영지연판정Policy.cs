using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Application.Admin.Food;

public sealed record 음식배달운영지연판정입력(
    string 주문상태,
    DateTime 조회시각Utc,
    DateTime? 조리예상완료시각Utc = null,
    DateTime? 픽업준비시각Utc = null,
    DateTime? 픽업완료시각Utc = null,
    DateTime? 전달완료시각Utc = null);

public sealed record 음식배달운영지연판정결과(
    string 음식점준비지연상태Code,
    int 음식점준비초과분,
    string 배달진행지연상태Code,
    int 배달진행초과분);

/// <summary>
/// 운영 화면이 같은 사실을 '예상 시간 초과'와 '조리 지연'으로 중복 표시하지 않도록
/// 현재 단계별 지연을 음식점 준비와 배달 진행의 두 업무 의미로만 판정합니다.
/// </summary>
public static class 음식배달운영지연판정Policy
{
    public const int 주의초과분 = 5;
    public const int 운영자확인초과분 = 10;

    // 현재 배차 검토 계약의 42분 기준을 픽업 이후 진행 관찰의 초기 기준으로 재사용합니다.
    // 향후 주문별 동결 전달 약속 시각이 생기면 그 값을 우선하도록 교체합니다.
    public const int 배달진행기준분 = 42;

    public static 음식배달운영지연판정결과 판정(음식배달운영지연판정입력 input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var state = 음식주문상태코드.Normalize(input.주문상태);
        var preparationOverdue = 음식점준비초과분(input, state);
        var deliveryOverdue = 배달진행초과분(input, state);

        return new 음식배달운영지연판정결과(
            지연상태(preparationOverdue),
            preparationOverdue,
            지연상태(deliveryOverdue),
            deliveryOverdue);
    }

    private static int 음식점준비초과분(
        음식배달운영지연판정입력 input,
        string state)
    {
        var preparing = state is 음식주문상태코드.조리중 or 음식주문상태코드.기사배정;
        if (!preparing
            || input.픽업준비시각Utc.HasValue
            || !input.조리예상완료시각Utc.HasValue)
        {
            return 0;
        }

        return 초과분(input.조회시각Utc, input.조리예상완료시각Utc.Value);
    }

    private static int 배달진행초과분(
        음식배달운영지연판정입력 input,
        string state)
    {
        if (state != 음식주문상태코드.픽업완료
            || !input.픽업완료시각Utc.HasValue
            || input.전달완료시각Utc.HasValue)
        {
            return 0;
        }

        var 기준시각Utc = input.픽업완료시각Utc.Value.AddMinutes(배달진행기준분);
        return 초과분(input.조회시각Utc, 기준시각Utc);
    }

    private static int 초과분(DateTime now, DateTime due)
        => Math.Max(0, (int)Math.Floor((now - due).TotalMinutes));

    private static string 지연상태(int overdueMinutes)
        => overdueMinutes >= 운영자확인초과분
            ? 음식배달운영지연상태Codes.운영자확인필요
            : overdueMinutes >= 주의초과분
                ? 음식배달운영지연상태Codes.주의
                : 음식배달운영지연상태Codes.없음;
}
