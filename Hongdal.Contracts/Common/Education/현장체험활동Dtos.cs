using Hongdal.Contracts.Common.Versioning;

namespace Hongdal.Contracts.Common.Education;

public sealed class 현장체험활동생성요청
{
    public string 제목 { get; set; } = string.Empty;
    public string 학생표시명 { get; set; } = string.Empty;
    public string 학교식별Key { get; set; } = string.Empty;
    public string 학교명 { get; set; } = string.Empty;
    public string? 학년반 { get; set; }
    public string 보호자UserId { get; set; } = string.Empty;
    public string 보호자표시명 { get; set; } = string.Empty;
    public string? 현장체험지도자UserId { get; set; }
    public string? 현장체험지도자표시명 { get; set; }
    public string 활동목표 { get; set; } = string.Empty;
    public string 활동장소 { get; set; } = string.Empty;
    public DateTimeOffset 시작예정시각 { get; set; }
    public DateTimeOffset 종료예정시각 { get; set; }
    public IReadOnlyList<string> 계획활동 { get; set; } = [];
    public string? 현장담당자 { get; set; }
    public string? 학교제출처Key { get; set; }
    public string? 학교담당이메일 { get; set; }
}

public sealed class 현장체험활동기록요청
{
    public string 활동명 { get; set; } = string.Empty;
    public string 활동내용 { get; set; } = string.Empty;
    public string 수행역할 { get; set; } = string.Empty;
    public DateTimeOffset 시작시각 { get; set; }
    public DateTimeOffset 종료시각 { get; set; }
    public string? 확인자표시명 { get; set; }
    public string? 확인메모 { get; set; }
    public IReadOnlyList<string> 증빙파일Url목록 { get; set; } = [];
}

public sealed class 현장체험보호자승인요청
{
    public bool 승인여부 { get; set; }
    public string 보호자표시명 { get; set; } = string.Empty;
    public string? 의견 { get; set; }
}

public sealed class 현장체험지도자확인요청
{
    public bool 실제활동확인여부 { get; set; }
    public string 지도자표시명 { get; set; } = string.Empty;
    public string? 확인내용 { get; set; }
}

public sealed class 현장체험학교제출요청
{
    public string 전송방식 { get; set; } = 교육기관제출방식.문서;
    public string? 제출처Key { get; set; }
    public string? 담당이메일 { get; set; }
    public string? 제출메모 { get; set; }
}

public sealed class 현장체험학교결정요청
{
    public bool 출석인정여부 { get; set; }
    public string 결정기관명 { get; set; } = string.Empty;
    public string 결정자표시명 { get; set; } = string.Empty;
    public string? 결정문서번호 { get; set; }
    public string? 의견 { get; set; }
}

public sealed class 현장체험활동응답
{
    public string 원장Id { get; set; } = string.Empty;
    public string 제목 { get; set; } = string.Empty;
    public string 상태 { get; set; } = string.Empty;
    public string? 현재단계 { get; set; }
    public string 학생표시명 { get; set; } = string.Empty;
    public string 학교명 { get; set; } = string.Empty;
    public DateTimeOffset 시작예정시각 { get; set; }
    public DateTimeOffset 종료예정시각 { get; set; }
    public int 활동기록수 { get; set; }
    public int 현장확인완료수 { get; set; }
    public int 증빙파일수 { get; set; }
    public bool 보호자승인완료 { get; set; }
    public bool 학교제출요건충족 { get; set; }
    public bool? 출석인정여부 { get; set; }
    public IReadOnlyList<현장체험제출상태응답> 제출목록 { get; set; } = [];
    public DateTime 수정시각Utc { get; set; }
}

public sealed class 현장체험제출상태응답
{
    public string 제출Id { get; set; } = string.Empty;
    public string 전송방식 { get; set; } = string.Empty;
    public string 상태 { get; set; } = string.Empty;
    public string? 제출처 { get; set; }
    public string? 마지막오류 { get; set; }
    public DateTime 생성시각Utc { get; set; }
    public DateTime? 전송완료시각Utc { get; set; }
}

public static class 현장체험활동원장상수
{
    public const string 원장템플릿Key = "education-field-experience";
    public const string 대상OsCode = OperatingSystemIds.EducationFieldExperience;
    public const string 대상OsName = "교육 현장 체험 지원 OS";

    public const string 학생계획Block = "education-student-plan";
    public const string 활동계획Block = "education-activity-plan";
    public const string 활동기록Block = "education-activity-record";
    public const string 보호자승인Block = "education-guardian-approval";
    public const string 학교제출Block = "education-school-submission";
    public const string 학교결정Block = "education-school-decision";
}

public static class 현장체험활동상태
{
    public const string 계획작성 = "계획작성";
    public const string 활동진행 = "활동진행";
    public const string 보호자확인 = "보호자확인";
    public const string 제출대기 = "제출대기";
    public const string 학교심사중 = "학교심사중";
    public const string 출석인정 = "출석인정";
    public const string 출석미인정 = "출석미인정";
}

public static class 교육기관제출방식
{
    public const string 문서 = "문서";
    public const string 이메일 = "이메일";
    public const string Api = "API";

    public static bool 지원여부(string? value)
        => value is 문서 or 이메일 or Api;
}

public static class 교육기관제출상태
{
    public const string 전송대기 = "전송대기";
    public const string 전송중 = "전송중";
    public const string 설정대기 = "설정대기";
    public const string 수동제출준비 = "수동제출준비";
    public const string 전송완료 = "전송완료";
    public const string 전송실패 = "전송실패";
}
