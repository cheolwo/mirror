namespace 홍달.Services.Options;

public sealed class DatabaseInitializationOptions
{
    public const string SectionName = "DatabaseInitialization";

    /// <summary>
    /// 개발 환경처럼 단일 인스턴스로 실행할 때만 시작 시 스키마와 기준 데이터를 준비합니다.
    /// 운영 배포에서는 false로 두고 --initialize-database 명령을 배포 단계에서 한 번 실행합니다.
    /// </summary>
    public bool RunAtStartup { get; set; }

    public bool FailOnError { get; set; } = true;
}
