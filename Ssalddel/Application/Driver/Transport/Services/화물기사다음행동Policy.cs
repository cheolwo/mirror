namespace Ssalddel.Application.Driver.Transport;

public static class 화물기사다음행동Policy
{
    public static int Priority(string? status) => status switch
    {
        "하차지도착" => 0,
        "상차완료" or "운송중" => 1,
        "상차지도착" => 2,
        "배차대기" or "배차확정" or "확정" or "매칭중" or "이동중" => 3,
        _ => 9
    };

    public static string NextAction(string? status) => status switch
    {
        "하차지도착" => "하차 완료 증빙",
        "상차완료" or "운송중" => "하차지로 이동",
        "상차지도착" => "상차 확인·인수증 증빙",
        "배차대기" or "배차확정" or "확정" or "매칭중" or "이동중" => "상차지로 이동",
        _ => "상태 확인"
    };
}
