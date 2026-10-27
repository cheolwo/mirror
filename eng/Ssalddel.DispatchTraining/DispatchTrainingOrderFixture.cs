using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.DispatchTraining;

/// <summary>합성 접수 입력만 만든다. 견적·접수·배차·실제 물품 적재 확인은 정상 업무 경로가 수행한다.</summary>
internal static class DispatchTrainingOrderFixture
{
    internal static NeighborhoodDeliveryRequest Create(int index, Guid requestId, Guid consent, DateTime createdUtc)
        => new()
        {
            ClientRequestId = requestId, CargoName = $"가상 소형 상자 {index + 1:00}", Quantity = 1,
            WeightKg = 1, VehicleType = DispatchTrainingSeed.VehicleName,
            PackageLengthMm = 200, PackageWidthMm = 150, PackageHeightMm = 100,
            PalletCount = 0, FloorRotationAllowed = true, StackingAllowed = false,
            PrivacyConsentEvidenceId = consent, DispatchRequested = true, DirectPaymentAgreed = true,
            DispatchMode = "automatic", Notes = "합성 운영 연습. 실제 물품·주문·지급이 아닙니다.",
            Pickup = new()
            {
                주소 = new() { 도로명주소 = $"가상 픽업 {index + 1:00}" },
                연락처 = new() { 이름 = "가상 발송인", 전화번호 = "010-0000-0000" },
                작업시간분 = 1m,
                시간창 = new() { 시작일시 = createdUtc.AddMinutes(-1), 종료일시 = createdUtc.AddHours(2) }
            },
            Dropoff = new()
            {
                주소 = new() { 도로명주소 = $"가상 전달 {index + 1:00}" },
                연락처 = new() { 이름 = "가상 수령인", 전화번호 = "010-0000-0001" },
                작업시간분 = 1m,
                시간창 = new() { 시작일시 = createdUtc, 종료일시 = createdUtc.AddHours(2) }
            }
        };
}
