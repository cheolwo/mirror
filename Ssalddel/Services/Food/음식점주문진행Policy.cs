using Ssalddel.Contracts.Food;

namespace Ssalddel.Services.Food;

public sealed record 음식점주문진행판정(
    string 다음상태,
    int? 조리예상분,
    string 이력사유);

public static class 음식점주문진행Policy
{
    public static 음식점주문진행판정 판정(
        string? 현재상태,
        음식점주문진행변경요청 request,
        bool 배차확정 = false,
        bool 조리시작됨 = true,
        int? 계획조리분 = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        var current = 음식배달업무상태전이Guard.정본상태확인(현재상태);
        var action = request.작업?.Trim();
        if (!음식점주문진행작업코드.지원여부(action))
        {
            throw new ArgumentException("지원하지 않는 음식점 주문 진행 작업입니다.", nameof(request));
        }

        var decision = action switch
        {
            음식점주문진행작업코드.조리시작 => StartCooking(current, request, 배차확정, 조리시작됨, 계획조리분),
            음식점주문진행작업코드.거절 => Reject(current, request),
            음식점주문진행작업코드.조리시간변경 => ChangePreparationTime(current, request),
            음식점주문진행작업코드.픽업준비 => 조리시작됨
                ? MarkPickupReady(current)
                : throw new InvalidOperationException("조리를 시작한 뒤 픽업 준비를 완료할 수 있습니다."),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };

        음식배달업무상태전이Guard.허용확인(current, decision.다음상태);
        return decision;
    }

    private static 음식점주문진행판정 StartCooking(
        string current, 음식점주문진행변경요청 request,
        bool assigned, bool started, int? plannedMinutes)
    {
        if (current != 음식주문상태코드.기사배정 || !assigned)
            throw new InvalidOperationException("유효한 기사 배차가 확정된 뒤에만 조리를 시작할 수 있습니다.");
        if (started)
            throw new InvalidOperationException("이미 조리를 시작한 주문입니다.");
        if (!request.예상Revision.HasValue)
            throw new ArgumentException("조리 시작에는 현재 주문 revision이 필요합니다.");
        var minutes = 음식점조리시간정책.Clamp(request.조리예상분 ?? plannedMinutes ?? 15);
        return new 음식점주문진행판정(음식주문상태코드.조리중, minutes, "음식점 조리 시작");
    }

    private static 음식점주문진행판정 Reject(
        string current,
        음식점주문진행변경요청 request)
    {
        EnsureCurrent(current, 음식주문상태코드.주문대기, request.작업);
        var reason = request.사유?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("주문 거절 사유가 필요합니다.", nameof(request));
        }

        return new 음식점주문진행판정(
            음식주문상태코드.거절,
            null,
            $"음식점 주문 거절 · {reason}");
    }

    private static 음식점주문진행판정 ChangePreparationTime(
        string current,
        음식점주문진행변경요청 request)
    {
        if (current is not (음식주문상태코드.주문확인 or 음식주문상태코드.조리중 or 음식주문상태코드.기사배정))
        {
            throw new InvalidOperationException(
                $"{request.작업} 작업이 가능한 주문 상태가 아닙니다. 현재상태={current}, 필요상태={음식주문상태코드.조리중} 또는 {음식주문상태코드.기사배정}");
        }
        if (request.조리예상분 is null)
        {
            throw new ArgumentException("변경할 조리 예상 시간이 필요합니다.", nameof(request));
        }

        var minutes = 음식점조리시간정책.Clamp(request.조리예상분.Value);
        return new 음식점주문진행판정(
            current,
            minutes,
            $"조리 예상 시간 변경 · {minutes}분");
    }

    private static 음식점주문진행판정 MarkPickupReady(string current)
    {
        // 기사 배정은 조리 완료가 아니다. 배정 뒤 준비 완료를 기록해도
        // 배차 상태를 픽업대기로 되돌리지 않고 기존 기사 진행을 보존한다.
        if (current != 음식주문상태코드.기사배정)
            EnsureCurrent(current, 음식주문상태코드.조리중, 음식점주문진행작업코드.픽업준비);
        return new 음식점주문진행판정(
            current == 음식주문상태코드.기사배정 ? current : 음식주문상태코드.픽업대기,
            0,
            "음식점 픽업 준비 완료");
    }

    private static void EnsureCurrent(
        string current,
        string expected,
        string? action)
    {
        if (!string.Equals(current, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{action} 작업이 가능한 주문 상태가 아닙니다. 현재상태={current}, 필요상태={expected}");
        }
    }
}
