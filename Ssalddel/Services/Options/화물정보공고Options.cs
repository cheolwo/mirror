namespace 살뜰.Services.Options;

/// <summary>실제 운송 Feature와 독립된 비구속 정보 게시 경계. 명시 설정 전 기본 비활성입니다.</summary>
public sealed class 화물정보공고Options
{
    public const string SectionName = "CargoInformationPublication";
    public bool Enabled { get; set; }
}
