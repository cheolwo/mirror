using Ssalddel.Contracts.Common.Dispatch;

namespace Ssalddel.Application.Driver.DispatchAction;

/// <summary>기존 앱의 사유 문구를 안전한 분류로 바꾸며 자유문구나 개인정보를 사건·로그에 복제하지 않습니다.</summary>
public static class 배차거절사유Policy
{
    public static string 분류(string? reason) => reason?.Trim() switch
    {
        null or "" or 운영배차거절사유Code.미입력 => 운영배차거절사유Code.미입력,
        "동선이 맞지 않음" or 운영배차거절사유Code.거리부담 => 운영배차거절사유Code.거리부담,
        "시간 조건이 맞지 않음" or "상하차 시간에 맞추기 어려움" or "다른 업무를 진행 중"
            or 운영배차거절사유Code.시간제약 => 운영배차거절사유Code.시간제약,
        "차량 조건이 맞지 않음" or "차량·적재 조건이 맞지 않음"
            or 운영배차거절사유Code.수행용량부족 => 운영배차거절사유Code.수행용량부족,
        운영배차거절사유Code.개인사정 => 운영배차거절사유Code.개인사정,
        // 보수 등 별도 분류가 없는 문구와 알 수 없는 자유서술은 추정하거나 원문 저장하지 않습니다.
        _ => 운영배차거절사유Code.기타
    };
}
