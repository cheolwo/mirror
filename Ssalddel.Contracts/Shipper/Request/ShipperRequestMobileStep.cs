namespace Ssalddel.Contracts.Shipper.Request;

/// <summary>같은 초안을 작은 입력 문맥으로 나누는 모바일 탐색 단계. 기존 상위 단계의 값은 바꾸지 않는다.</summary>
public enum ShipperRequestMobileStep
{
    Cargo,
    Package,
    Pickup,
    Dropoff,
    Schedule,
    Vehicle,
    Payment,
    Review
}

public sealed record ShipperRequestMobileStepDefinition(
    ShipperRequestMobileStep Step,
    string Title,
    string Description,
    ShipperRequestAuthoringStep MajorStep,
    string Path,
    int Number);

/// <summary>표시 순서와 경로의 정본이다. enum 숫자로 앞뒤 단계나 진행 번호를 계산하지 않는다.</summary>
public static class ShipperRequestMobileStepCatalog
{
    public static IReadOnlyList<ShipperRequestMobileStepDefinition> Steps { get; } = Array.AsReadOnly<ShipperRequestMobileStepDefinition>(
    [
        new(ShipperRequestMobileStep.Cargo, "무엇을 보내나요?", "품목·수량·취급 조건", ShipperRequestAuthoringStep.Cargo, ShipperRequestPageRoutes.Cargo, 1),
        new(ShipperRequestMobileStep.Package, "얼마나 크고 무거운가요?", "포장 포함 중량과 크기", ShipperRequestAuthoringStep.Cargo, ShipperRequestPageRoutes.Package, 2),
        new(ShipperRequestMobileStep.Pickup, "어디서 받나요?", "출발지와 연락 대상", ShipperRequestAuthoringStep.Transport, ShipperRequestPageRoutes.Pickup, 3),
        new(ShipperRequestMobileStep.Dropoff, "어디로 보내나요?", "도착지와 연락 대상", ShipperRequestAuthoringStep.Transport, ShipperRequestPageRoutes.Dropoff, 4),
        new(ShipperRequestMobileStep.Schedule, "언제 운송하나요?", "희망 시간과 현장 요청", ShipperRequestAuthoringStep.Transport, ShipperRequestPageRoutes.Schedule, 5),
        new(ShipperRequestMobileStep.Vehicle, "어떤 차량을 이용하나요?", "차량과 운송 방식", ShipperRequestAuthoringStep.Procedure, ShipperRequestPageRoutes.Vehicle, 6),
        new(ShipperRequestMobileStep.Payment, "비용은 어떻게 정하나요?", "운임과 지급 조건", ShipperRequestAuthoringStep.Procedure, ShipperRequestPageRoutes.Procedure, 7),
        new(ShipperRequestMobileStep.Review, "이 내용으로 접수할까요?", "전체 검토와 본인 동의", ShipperRequestAuthoringStep.Review, ShipperRequestPageRoutes.Review, 8)
    ]);

    public static ShipperRequestMobileStepDefinition Get(ShipperRequestMobileStep step)
        => Steps.FirstOrDefault(x => x.Step == step)
            ?? throw new ArgumentOutOfRangeException(nameof(step), step, "지원하지 않는 모바일 의뢰 작성 단계입니다.");

    public static ShipperRequestMobileStep? Previous(ShipperRequestMobileStep step)
    {
        var position = Position(step);
        return position == 0 ? null : Steps[position - 1].Step;
    }

    public static ShipperRequestMobileStep? Next(ShipperRequestMobileStep step)
    {
        var position = Position(step);
        return position == Steps.Count - 1 ? null : Steps[position + 1].Step;
    }

    private static int Position(ShipperRequestMobileStep step)
    {
        _ = Get(step);
        for (var index = 0; index < Steps.Count; index++)
            if (Steps[index].Step == step) return index;
        throw new ArgumentOutOfRangeException(nameof(step));
    }
}
