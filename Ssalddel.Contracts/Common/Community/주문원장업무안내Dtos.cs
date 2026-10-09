namespace Ssalddel.Contracts.Common.Community;

/// <summary>허용된 원장 조회의 다음 업무 안내입니다. 명령 허가나 완료 판정이 아닙니다.</summary>
public sealed class 주문원장업무안내Dto
{
    public string 현재단계 { get; set; } = "진행 확인";
    public string 담당역할 { get; set; } = "담당자";
    public string 대기이유 { get; set; } = string.Empty;
    public string 다음행동안내 { get; set; } = "주문 정보 새로고침";
    public string? 업무종류 { get; set; }
    public string? 원천Id { get; set; }
    public string 조건변경안내 { get; set; } = "이번 건의 조건은 해당 업무 화면에서 변경 가능 시점과 상대 확인 여부를 확인합니다.";
    public string 완료구분안내 { get; set; } = "전달·수령 확인·정산·지급 확인은 각각의 업무에서 확인합니다.";
}
