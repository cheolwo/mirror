using Ssalddel.Contracts.Common.Dispatch;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

/// <summary>수신 의사와 미응답 자동중지를 분리하는 표시·정본 대조 정책입니다. 서버 제한은 변경하지 않습니다.</summary>
public static class FoodDriverDispatchReceptionPolicy
{
    public const string AutomaticStopNotice = "연속 3회 미응답으로 신규 배차가 꺼졌습니다. 다시 켜면 받을 수 있습니다.";
    public static bool IsKnown(운영배차수신상태Dto? value)
        => value?.수신의사Code is 운영배차수신의사Code.On or 운영배차수신의사Code.Off;
    public static bool IsReceiving(운영배차수신상태Dto? value)
        => value?.수신의사Code == 운영배차수신의사Code.On && !value.미응답자동중지여부;
    public static bool Confirms(운영배차수신의사변경요청 request, 운영배차수신상태Dto? value)
        => IsKnown(value) && request.수신의사Code == value!.수신의사Code
            && (!request.미응답자동중지해제 || !value.미응답자동중지여부);
}
