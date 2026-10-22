using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Application.Shipper.Request;

/// <summary>생성 입력의 원문·누락값을 보존하며 검증과 제한된 정규화만 수행한다.</summary>
internal static class 화주운송의뢰생성입력Policy
{
    internal static string? 입력오류(의뢰생성Command request)
    {
        if (string.IsNullOrWhiteSpace(request.화물종류))
        {
            return "화물종류 is required";
        }

        if (string.IsNullOrWhiteSpace(request.픽업도로명주소))
        {
            return "픽업 주소 도로명주소 is required";
        }

        if (string.IsNullOrWhiteSpace(request.픽업연락처전화번호))
        {
            return "픽업 연락처 전화번호 is required";
        }

        if (request.픽업시간창시작일시 >= request.픽업시간창종료일시)
        {
            return "pickup.window.startAt must be before endAt";
        }

        if (string.IsNullOrWhiteSpace(request.하차도로명주소))
        {
            return "하차 주소 도로명주소 is required";
        }

        if (request.하차시간창시작일시.HasValue
            && request.하차시간창종료일시.HasValue
            && request.하차시간창시작일시 >= request.하차시간창종료일시)
        {
            return "dropoff.window.startAt must be before endAt";
        }

        if (request.픽업작업시간분 is < 0m || request.하차작업시간분 is < 0m)
            return "상차·하차 작업시간은 0분 이상이어야 합니다. 모르면 비워 두세요.";

        if (결제상태정규화(request.결제상태) != 상태값.결제상태.결제대기)
            return "새 의뢰는 결제 대기로 준비합니다. 결제 완료는 서버의 결제 확인으로만 반영합니다.";

        return null;
    }

    internal static string 접수요청Id정규화(string? value) => value?.Trim() ?? string.Empty;

    internal static bool 모바일접수(string clientRequestId)
        => clientRequestId.StartsWith("shipper-app-", StringComparison.Ordinal)
            && Guid.TryParseExact(clientRequestId[12..], "N", out _);

    internal static string 결제상태정규화(string? value)
        => string.IsNullOrWhiteSpace(value) ? 상태값.결제상태.결제대기 : value.Trim();

    internal static string? 고정의뢰Id(string currentUserId, string clientRequestId, bool isMobileSubmission)
        => clientRequestId.StartsWith(Ssalddel.Contracts.Common.Community.NeighborhoodDeliveryRoutes.ClientRequestPrefix, StringComparison.Ordinal)
            || isMobileSubmission
            ? new Guid(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(currentUserId + ":" + clientRequestId)).AsSpan(0, 16)).ToString()
            : null;

    internal static bool 운임구성필요(의뢰생성Command request)
        => request.최종운임.HasValue
           || request.기본운임.HasValue
           || request.거리운임.HasValue
           || request.최소운임.HasValue
           || request.Km당단가.HasValue
           || request.예상거리Km.HasValue
           || request.기사지급예정운임.HasValue;
}
