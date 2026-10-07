using Ssalddel.Contracts.Food;
using 살뜰.도메인.음식;

namespace Ssalddel.Services.Food;

public sealed record 음식주문현재조리상태(
    int Round,
    DateTime? CookingStartedAtUtc,
    DateTime? ReadyAtUtc,
    DateTime? RecookingRequestedAtUtc);

/// <summary>
/// 불변 주문 이력에서 현재 음식의 준비 상태만 판정합니다. 기사 교체는 같은 음식을 유지하며,
/// 픽업 후 중단으로 기록된 재조리 요청만 이전 준비 완료를 무효화합니다.
/// </summary>
public static class 음식주문현재조리Policy
{
    public const string 재조리요청이력사유 = "픽업 후 배달 중단 · 재조리·재배차";

    public static 음식주문현재조리상태 계산(음식주문 order)
        => 계산(order.상태이력.OrderBy(x => x.전이시각Utc).ThenBy(x => x.Id)
            .Select(x => new 조리사건(x.다음상태, x.사유, x.전이시각Utc)));

    public static 음식주문현재조리상태 계산(음식주문응답 order)
    {
        var current = 계산(order.상태이력.OrderBy(x => x.전이시각Utc)
            .Select(x => new 조리사건(x.다음상태, x.사유, x.전이시각Utc)));
        // 첫 차수의 구형/표본 응답에는 시각만 있고 해당 이력이 없을 수 있습니다.
        // 재조리 경계가 있으면 과거 시각을 현재 차수에 다시 붙이지 않습니다.
        return current.Round == 1
            ? current with
            {
                CookingStartedAtUtc = current.CookingStartedAtUtc ?? order.조리시작시각Utc,
                ReadyAtUtc = current.ReadyAtUtc ?? order.픽업준비시각Utc
            }
            : current;
    }

    private static 음식주문현재조리상태 계산(IEnumerable<조리사건> history)
    {
        var round = 1;
        DateTime? startedAt = null;
        DateTime? readyAt = null;
        DateTime? recookingAt = null;
        foreach (var item in history)
        {
            if (item.사유 == 재조리요청이력사유 && item.다음상태 == 음식주문상태코드.조리중)
            {
                round++;
                // 기존 중단 전이는 재조리를 조리중으로 시작합니다. 별도 조리 시작 명령을 만들지 않습니다.
                startedAt = recookingAt = item.전이시각Utc;
                readyAt = null;
                continue;
            }
            if (item.사유 == "음식점 조리 시작"
                || (item.사유 == "음식점 주문 수락" && item.다음상태 == 음식주문상태코드.조리중))
                startedAt ??= item.전이시각Utc;
            if (item.다음상태 == 음식주문상태코드.픽업대기
                || item.사유.StartsWith("음식점 픽업 준비 완료", StringComparison.Ordinal)
                || item.사유 == "음식점 주문 확인 · 기존 준비 완료")
                readyAt ??= item.전이시각Utc;
        }
        return new(round, startedAt, readyAt, recookingAt);
    }

    private sealed record 조리사건(string 다음상태, string 사유, DateTime 전이시각Utc);
}
