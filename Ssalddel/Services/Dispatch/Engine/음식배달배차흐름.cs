using 살뜰.도메인.배차;

namespace 살뜰.Services.Dispatch.Engine;

public static class 음식배달배차원본유형
{
    public const string 음식점주문 = 운송의뢰배차원천유형.음식점주문;
    public const string 살뜰마트주문 = 운송의뢰배차원천유형.살뜰마트주문;
    public const string 살뜰마트포장완료주문 = 운송의뢰배차원천유형.살뜰마트포장완료주문;

    public static bool IsRestaurantOrder(string? sourceType)
        => 운송의뢰배차원천유형.Is음식점주문(sourceType);

    public static bool IsMartOrder(string? sourceType)
        => 운송의뢰배차원천유형.Is살뜰마트음식주문(sourceType);
}

public sealed record 음식배달배차흐름(
    string 흐름코드,
    string 표시명,
    bool 창고선행작업필요,
    bool 배차시작가능,
    string 배차시작조건);

public interface I음식배달배차흐름Resolver
{
    음식배달배차흐름 Resolve(운송원장 queue);
}

public sealed class 음식배달배차흐름Resolver : I음식배달배차흐름Resolver
{
    public 음식배달배차흐름 Resolve(운송원장 queue)
    {
        if (음식배달배차원본유형.IsRestaurantOrder(queue.원본의뢰유형))
        {
            return new 음식배달배차흐름(
                음식배달배차원본유형.음식점주문,
                "음식점 즉시 배달",
                창고선행작업필요: false,
                배차시작가능: true,
                "음식점 주문은 주문 확인 후 배차하며, 기사 배정이 확정된 뒤 조리를 시작합니다.");
        }

        if (string.Equals(queue.원본의뢰유형, 음식배달배차원본유형.살뜰마트포장완료주문, StringComparison.OrdinalIgnoreCase))
        {
            return new 음식배달배차흐름(
                음식배달배차원본유형.살뜰마트포장완료주문,
                "알뜰살뜰 마트 포장 완료 배달",
                창고선행작업필요: true,
                배차시작가능: true,
                "알뜰살뜰 마트 주문은 피킹과 포장 완료 후 배달기사 배차를 시작합니다.");
        }

        if (음식배달배차원본유형.IsMartOrder(queue.원본의뢰유형))
        {
            return new 음식배달배차흐름(
                음식배달배차원본유형.살뜰마트주문,
                "알뜰살뜰 마트 준비 중 배달",
                창고선행작업필요: true,
                배차시작가능: false,
                "알뜰살뜰 마트 주문은 재고 확인, 피킹, 포장 완료 전에는 배차를 시작하지 않습니다.");
        }

        return new 음식배달배차흐름(
            queue.원본의뢰유형,
            "음식 배달 기본 흐름",
            창고선행작업필요: false,
            배차시작가능: true,
            "원본 유형이 명확하지 않은 음식 배달은 기본 즉시 배차 흐름으로 처리합니다.");
    }
}
