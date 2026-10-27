using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Contracts.Common.Community;

/// <summary>생활배송 입력을 기존 화물 적재 정책에 연결합니다. 분류명으로 규격이나 작업시간을 추정하지 않습니다.</summary>
public static class NeighborhoodDeliveryLoadingInput
{
    public static CargoDTO ToCargo(NeighborhoodDeliveryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new()
        {
            화물종류 = request.CargoName?.Trim() ?? string.Empty, 수량 = request.Quantity, 중량Kg = request.WeightKg,
            길이Mm = request.PackageLengthMm, 폭Mm = request.PackageWidthMm, 높이Mm = request.PackageHeightMm,
            부피Cbm = request.VolumeCbm, 팔레트개수 = request.PalletCount,
            바닥회전가능여부 = request.FloorRotationAllowed, 적층가능여부 = request.StackingAllowed, 온도조건 = "상온"
        };
    }

    public static 화물적재입력결과 Calculate(NeighborhoodDeliveryRequest request)
        => 화물적재입력Policy.계산(화물적재입력Policy.입력생성(ToCargo(request), "직송", 요청사항: request.Notes?.Trim()));

    public static IReadOnlyList<string> InputErrors(NeighborhoodDeliveryRequest request)
    {
        var errors = Calculate(request).오류.ToList();
        if (request.Pickup?.작업시간분 < 0) errors.Add("픽업 작업시간은 0분 이상으로 입력해 주세요.");
        if (request.Dropoff?.작업시간분 < 0) errors.Add("전달 작업시간은 0분 이상으로 입력해 주세요.");
        return errors;
    }

    /// <summary>신규 내부 기사 배송의 화면 보완 안내입니다. 과거 자료나 다른 수행 방식의 접수를 변경하지 않습니다.</summary>
    public static IReadOnlyList<string> InternalCourierMissing(NeighborhoodDeliveryRequest request)
    {
        var missing = new List<string>();
        if (request.PackageLengthMm is null or <= 0 || request.PackageWidthMm is null or <= 0 || request.PackageHeightMm is null or <= 0)
            missing.Add("포장 한 개의 길이·폭·높이를 입력해 주세요.");
        if (request.PalletCount is null) missing.Add("팔레트 개수를 확인해 주세요. 사용하지 않으면 0개입니다.");
        if (!request.FloorRotationAllowed.HasValue) missing.Add("바닥 회전 가능 여부를 선택해 주세요.");
        if (!request.StackingAllowed.HasValue) missing.Add("다른 포장을 위에 쌓을 수 있는지 선택해 주세요.");
        if (request.Pickup?.작업시간분 is null) missing.Add("픽업에 걸리는 작업시간을 입력해 주세요.");
        if (request.Dropoff?.작업시간분 is null) missing.Add("전달에 걸리는 작업시간을 입력해 주세요.");
        if (request.Dropoff?.시간창 is not { } window || window.시작일시 == default || window.종료일시 <= window.시작일시)
            missing.Add("기사 배송의 전달 가능 시간과 마감 시각을 정해 주세요.");
        return missing;
    }
}
