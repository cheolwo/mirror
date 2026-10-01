using Hongdal.Contracts.Common.Community;

namespace Hongdal.Services.Community;

public interface I커뮤니티원장저장소
{
    Task<커뮤니티원장Dto> 원장저장Async(
        커뮤니티원장저장요청 request,
        string updatedBy,
        CancellationToken cancellationToken = default);

    Task<커뮤니티원장Dto?> 원장조회Async(
        string 원장Id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(
        커뮤니티원장조회조건 query,
        CancellationToken cancellationToken = default);

    Task<커뮤니티원장Dto?> 원장상태변경Async(
        커뮤니티원장상태변경요청 request,
        string updatedBy,
        CancellationToken cancellationToken = default);
}

public interface I커뮤니티원장투영작업저장소
{
    Task<커뮤니티원장투영작업?> 다음작업확보Async(
        TimeSpan leaseTimeout,
        CancellationToken cancellationToken = default);

    Task 완료Async(
        string 원장Id,
        long revision,
        string? processingToken,
        CancellationToken cancellationToken = default);

    Task 실패Async(
        string 원장Id,
        long revision,
        string processingToken,
        string 오류,
        int 최대시도횟수,
        TimeSpan 기본재시도간격,
        CancellationToken cancellationToken = default);
}

public sealed record 커뮤니티원장투영작업(
    커뮤니티원장Dto 원장,
    string EventId,
    string 변경유형,
    string 변경자,
    커뮤니티원장상태변경요청? 상태변경요청,
    DateTime 발생시각Utc,
    string ProcessingToken,
    int 시도횟수);

public static class 커뮤니티원장투영상태
{
    public const string 대기 = "대기";
    public const string 처리중 = "처리중";
    public const string 재시도대기 = "재시도대기";
    public const string 완료 = "완료";
    public const string 실패 = "실패";
}

public sealed class 커뮤니티원장조회조건
{
    public string? 커뮤니티Id { get; set; }
    public string? 원장템플릿Key { get; set; }
    public IReadOnlyList<string> 원장템플릿Keys { get; set; } = [];
    public string? 상태 { get; set; }
    public string? 참여자UserId { get; set; }
    public string? 접근UserId { get; set; }
    public string? 포함원장Id { get; set; }
    public IReadOnlyList<string> 포함원장Ids { get; set; } = [];
    public int Limit { get; set; } = 50;
}

public sealed class 커뮤니티원장저장요청
{
    public string? 원장Id { get; set; }
    public long? 기대Revision { get; set; }
    public string 커뮤니티Id { get; set; } = "platform";
    public string 원장템플릿Key { get; set; } = CommunityLedgerTemplateKeys.CargoTransport;
    public string 제목 { get; set; } = string.Empty;
    public string? 원함 { get; set; }
    public string? 상태 { get; set; }
    public string? 현재단계Key { get; set; }
    public string? 대상OsCode { get; set; }
    public string? 대상OsName { get; set; }
    public string? 생성자UserId { get; set; }
    public string? 생성자표시명 { get; set; }
    public IReadOnlyList<커뮤니티원장블록Dto> 블록목록 { get; set; } = [];
    public bool 블록담당자명시적갱신여부 { get; set; }
    public IReadOnlyList<커뮤니티원장참여자Dto> 참여자목록 { get; set; } = [];
    public IReadOnlyList<커뮤니티포함원장참조Dto>? 포함원장목록 { get; set; }
    public DiagramSnapshotDto? 다이어그램스냅샷 { get; set; }
    public IReadOnlyDictionary<string, string> 외부참조 { get; set; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> 확장속성 { get; set; } = new Dictionary<string, string>();
}

public sealed class 커뮤니티원장상태변경요청
{
    public string 원장Id { get; set; } = string.Empty;
    public long? 기대Revision { get; set; }
    public string 상태 { get; set; } = 커뮤니티원장상태.진행중;
    public string? 이전상태 { get; set; }
    public string? 현재단계Key { get; set; }
    public string? 메모 { get; set; }
}

public sealed class 커뮤니티원장Dto
{
    public string 원장Id { get; set; } = string.Empty;
    public long Revision { get; set; }
    public long 투영완료Revision { get; set; }
    public string 투영상태 { get; set; } = 커뮤니티원장투영상태.대기;
    public string? 투영EventId { get; set; }
    public string? 투영마지막오류 { get; set; }
    public string 커뮤니티Id { get; set; } = string.Empty;
    public string 원장템플릿Key { get; set; } = string.Empty;
    public string 제목 { get; set; } = string.Empty;
    public string? 원함 { get; set; }
    public string 상태 { get; set; } = 커뮤니티원장상태.초안;
    public string? 현재단계Key { get; set; }
    public string? 대상OsCode { get; set; }
    public string? 대상OsName { get; set; }
    public string? 생성자UserId { get; set; }
    public string 생성자표시명 { get; set; } = "익명 참여자";
    public IReadOnlyList<커뮤니티원장블록Dto> 블록목록 { get; set; } = [];
    public IReadOnlyList<커뮤니티원장참여자Dto> 참여자목록 { get; set; } = [];
    public IReadOnlyList<커뮤니티포함원장참조Dto> 포함원장목록 { get; set; } = [];
    public DiagramSnapshotDto? 다이어그램스냅샷 { get; set; }
    public IReadOnlyDictionary<string, string> 외부참조 { get; set; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> 확장속성 { get; set; } = new Dictionary<string, string>();
    public IReadOnlyList<커뮤니티원장상태이력Dto> 상태이력 { get; set; } = [];
    public DateTime 생성시각Utc { get; set; }
    public DateTime 수정시각Utc { get; set; }
}

public sealed class 커뮤니티원장블록Dto
{
    public string BlockId { get; set; } = string.Empty;
    public string BlockType { get; set; } = CommunityLedgerBlockTypes.Generic;
    public string Title { get; set; } = string.Empty;
    public string? State { get; set; }
    public IReadOnlyList<커뮤니티원장블록담당자Dto> 담당자목록 { get; set; } = [];
    public IReadOnlyDictionary<string, string> Data { get; set; } = new Dictionary<string, string>();
}

public sealed class 커뮤니티원장블록담당자Dto
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "익명 참여자";
    public string RoleLabel { get; set; } = "참여자";
    public string ResponsibilityType { get; set; } = CommunityLedgerBlockResponsibilityTypes.Primary;
}

public sealed class 커뮤니티원장참여자Dto
{
    public string? UserId { get; set; }
    public string DisplayName { get; set; } = "익명 참여자";
    public string RoleLabel { get; set; } = "참여자";
    public string ParticipationState { get; set; } = "참여중";
}

public sealed class 커뮤니티포함원장참조Dto
{
    public string 원장Id { get; set; } = string.Empty;
    public string 원장템플릿Key { get; set; } = string.Empty;
    public string 역할 { get; set; } = string.Empty;
    public string 관계유형 { get; set; } = CommunityLedgerRelationTypes.Contains;
    public bool 필수여부 { get; set; }
    public int 표시순서 { get; set; }
}

public sealed class 커뮤니티원장상태이력Dto
{
    public string? EventId { get; set; }
    public string 상태 { get; set; } = string.Empty;
    public string? 이전상태 { get; set; }
    public string? 현재단계Key { get; set; }
    public string? 메모 { get; set; }
    public string 변경자 { get; set; } = "system";
    public DateTime 변경시각Utc { get; set; }
}

public static class 커뮤니티원장상태
{
    public const string 초안 = "초안";
    public const string 진행중 = "진행중";
    public const string 보류 = "보류";
    public const string 완료 = "완료";
    public const string 닫힘 = "닫힘";
}
