using FluentResults;
using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Services.Community;

public interface I공간공동운영UseCase
{
    Task<공간공동운영공개Response> 공개Async(string? regionKey = null, int page = 1, CancellationToken cancellationToken = default);
    Task<공간공동운영공개제안Dto?> 초대조회Async(string ledgerId, CancellationToken cancellationToken = default);
    Task<공간공동운영내목록Response> 내목록Async(CancellationToken cancellationToken = default);
    Task<공간공동운영상세Response?> 상세Async(string ledgerId, CancellationToken cancellationToken = default);
    Task<공간공동운영상세Response?> 요청결과Async(Guid clientRequestId, string? ledgerId = null, CancellationToken cancellationToken = default);
    Task<Result<공간공동운영상세Response>> 생성Async(공간공동운영생성Request request, CancellationToken cancellationToken = default);
    Task<Result<공간공동운영상세Response>> 변경Async(string ledgerId, 공간공동운영명령Request request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<공간공동운영연결후보Dto>?> 연결후보Async(string ledgerId, string cycleId, CancellationToken cancellationToken = default);
}

/// <summary>현행 전문 원장의 권한·판본·상태만 조회하며 이 port는 업무를 실행하지 않습니다.</summary>
public interface I공간공동운영전문업무Bridge
{
    Task<IReadOnlyList<공간공동운영원천상태Dto>> ValidateAsync(string actorUserId,
        IReadOnlyList<공간공동운영전문참조Dto> references, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<공간공동운영연결후보Dto>> CandidatesAsync(string actorUserId,
        IReadOnlyList<공간공동운영전문참조Dto> currentRefs, CancellationToken cancellationToken = default);
}

/// <summary>서버 내부 전용. 조회 시 actor의 소유·참여 관계를 검사하며 HTTP 공개 원문이 아닙니다.</summary>
public interface I공간공동운영연결Query
{
    Task<공간공동운영Record?> 조회Async(string ledgerId, string actorUserId, CancellationToken cancellationToken = default);
}

/// <summary>community_ledgers의 전용 보호 블록만 변경합니다. 전문 업무 권한·재고·배차를 만들지 않습니다.</summary>
public interface I공간공동운영Store
{
    Task<공간공동운영Record?> 조회Async(string ledgerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<공간공동운영Record>> 목록Async(int skip = 0, int take = 200, CancellationToken cancellationToken = default);
    Task<bool> 생성Async(공간공동운영Record record, CancellationToken cancellationToken = default);
    Task<bool> 교체Async(공간공동운영Record record, long expectedRevision, string actorUserId, CancellationToken cancellationToken = default);
}

public sealed class 공간공동운영Record
{
    public string LedgerId { get; set; } = "";
    public long Revision { get; set; }
    public long TermsRevision { get; set; }
    public string OwnerUserId { get; set; } = "";
    public string? OperatorUserId { get; set; }
    public string? PendingOperatorUserId { get; set; }
    public long PendingOperatorTermsRevision { get; set; }
    public bool PublishProposal { get; set; }
    public string ExecutionMode { get; set; } = "";
    public string StatusCode { get; set; } = "gathering";
    public 공간공동운영조건Dto Terms { get; set; } = new();
    public List<공간공동운영참여Record> Participants { get; set; } = [];
    public List<공간공동운영회차Dto> Cycles { get; set; } = [];
    public Guid CreateRequestId { get; set; }
    public string CreateFingerprint { get; set; } = "";
    public List<공간공동운영명령Receipt> Receipts { get; set; } = [];
    /// <summary>주문 담당 UseCase가 소유하는 보호 부록. 다른 코어 명령은 그대로 보존합니다.</summary>
    public string? OrderExtensionJson { get; set; }
    /// <summary>현장 시간 약속 담당 UseCase가 소유하는 보호 부록. 주문·전문 업무 상태와 별개로 보존합니다.</summary>
    public string? WorkReservationExtensionJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class 공간공동운영참여Record
{
    public string UserId { get; set; } = "";
    public string RoleCode { get; set; } = "";
    public string StateCode { get; set; } = "interested";
    public bool IntroductionConsented { get; set; }
    public string PublicDisplayName { get; set; } = "";
    public long AgreedTermsRevision { get; set; }
}

public sealed class 공간공동운영명령Receipt
{
    public Guid ClientRequestId { get; set; }
    public string ActorUserId { get; set; } = "";
    public string Fingerprint { get; set; } = "";
}
