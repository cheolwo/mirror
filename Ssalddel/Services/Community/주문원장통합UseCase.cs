using FluentResults;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Services.Community;

public interface I주문원장통합UseCase
{
    Task<Result<주문자원장목록응답>> 주문자목록조회Async(
        string 주문자UserId,
        주문자원장목록조회요청 request,
        CancellationToken cancellationToken = default);

    Task<Result<주문원장통합Dto>> 조회Async(
        string 주문원장Id,
        CancellationToken cancellationToken = default);

    Task<Result<주문원장통합Dto>> 하위원장연결Async(
        string 주문원장Id,
        주문하위원장연결요청 request,
        string 변경자,
        CancellationToken cancellationToken = default);

    Task<Result<주문원장통합Dto>> 하위원장분리Async(
        string 주문원장Id,
        string 하위원장Id,
        long? 기대Revision,
        string 변경자,
        CancellationToken cancellationToken = default);
}

[SsalddelApiWorkflow(SsalddelWorkflow.CommunityTrust)]
[SsalddelUseCase("주문 원장 통합", Summary = "개별주문을 루트로 개별수입·개별수출 확장을 연결하고, 공동수출에서는 개별수출별 신고를 보존한 채 물류 집계를 조합합니다.")]
[SsalddelUseCaseActor(SsalddelActor.CommunityMember)]
[SsalddelUseCaseActor(SsalddelActor.PlatformOperator, SsalddelUseCaseActorRole.Supporting)]
public sealed class 주문원장통합UseCase : I주문원장통합UseCase
{
    private readonly I커뮤니티원장저장소 _원장저장소;
    private readonly 주문원장조회Service _조회;

    public 주문원장통합UseCase(I커뮤니티원장저장소 원장저장소)
        : this(원장저장소, 빈주문원장서명저장소.Instance)
    {
    }

    public 주문원장통합UseCase(
        I커뮤니티원장저장소 원장저장소,
        I주문원장서명저장소 서명저장소)
    {
        _원장저장소 = 원장저장소;
        _조회 = new 주문원장조회Service(원장저장소, 서명저장소);
    }

    public Task<Result<주문자원장목록응답>> 주문자목록조회Async(
        string 주문자UserId, 주문자원장목록조회요청 request, CancellationToken cancellationToken = default)
        => _조회.주문자목록조회Async(주문자UserId, request, cancellationToken);

    public Task<Result<주문원장통합Dto>> 조회Async(string 주문원장Id, CancellationToken cancellationToken = default)
        => _조회.조회Async(주문원장Id, cancellationToken);

    public async Task<Result<주문원장통합Dto>> 하위원장연결Async(
        string 주문원장Id,
        주문하위원장연결요청 request,
        string 변경자,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.하위원장Id))
        {
            return BadRequest("연결할 하위 원장 ID가 필요합니다.");
        }

        var 주문원장결과 = await 주문원장조회Async(주문원장Id, cancellationToken);
        if (주문원장결과.IsFailed)
        {
            return 주문원장결과.ToResult<주문원장통합Dto>();
        }

        var 주문원장 = 주문원장결과.Value;
        if (request.기대Revision.HasValue && request.기대Revision.Value != 주문원장.Revision)
        {
            return Conflict("주문 원장이 다른 요청에서 먼저 변경되었습니다. 최신 원장을 다시 조회한 뒤 재시도해야 합니다.");
        }

        var 하위원장 = await _원장저장소.원장조회Async(request.하위원장Id.Trim(), cancellationToken);
        if (하위원장 is null)
        {
            return NotFound("연결할 하위 원장을 찾을 수 없습니다.");
        }

        try
        {
            주문원장구성정책.연결검증(주문원장, 하위원장, request.역할);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }

        var 포함원장목록 = 주문원장.포함원장목록.ToList();
        var 기존Index = 포함원장목록.FindIndex(x =>
            string.Equals(x.원장Id, 하위원장.원장Id, StringComparison.OrdinalIgnoreCase));
        if (기존Index >= 0)
        {
            var 기존참조 = 포함원장목록[기존Index];
            var 역할변경 = !string.Equals(기존참조.역할, request.역할.Trim(), StringComparison.OrdinalIgnoreCase);
            if (역할변경 || (기존참조.필수여부 && !request.필수여부))
            {
                var 잠금사유 = 주문원장연결변경Policy.잠금사유(주문원장, 하위원장);
                if (잠금사유 is not null) return Conflict(잠금사유);
            }
            if (!역할변경 && 기존참조.필수여부 == request.필수여부
                && (!request.표시순서.HasValue || request.표시순서 == 기존참조.표시순서))
            {
                // 같은 관계를 재연결해도 판본을 올리거나 원장을 다시 저장하지 않습니다.
                return Result.Ok(await 통합Dto생성Async(주문원장, cancellationToken));
            }
        }
        var 표시순서 = request.표시순서
            ?? (기존Index >= 0
                ? 포함원장목록[기존Index].표시순서
                : 포함원장목록.Select(x => x.표시순서).DefaultIfEmpty(-1).Max() + 1);
        var 참조 = new 커뮤니티포함원장참조Dto
        {
            원장Id = 하위원장.원장Id,
            원장템플릿Key = 하위원장.원장템플릿Key,
            역할 = request.역할.Trim(),
            필수여부 = request.필수여부,
            표시순서 = 표시순서
        };

        if (기존Index >= 0)
        {
            포함원장목록[기존Index] = 참조;
        }
        else
        {
            포함원장목록.Add(참조);
        }

        var 저장결과 = await 주문원장저장Async(주문원장, 포함원장목록, 변경자, cancellationToken);
        if (저장결과.IsFailed)
        {
            return 저장결과.ToResult<주문원장통합Dto>();
        }

        return Result.Ok(await 통합Dto생성Async(저장결과.Value, cancellationToken));
    }

    public async Task<Result<주문원장통합Dto>> 하위원장분리Async(
        string 주문원장Id,
        string 하위원장Id,
        long? 기대Revision,
        string 변경자,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(하위원장Id))
        {
            return BadRequest("분리할 하위 원장 ID가 필요합니다.");
        }

        var 주문원장결과 = await 주문원장조회Async(주문원장Id, cancellationToken);
        if (주문원장결과.IsFailed)
        {
            return 주문원장결과.ToResult<주문원장통합Dto>();
        }

        var 주문원장 = 주문원장결과.Value;
        if (기대Revision.HasValue && 기대Revision.Value != 주문원장.Revision)
        {
            return Conflict("주문 원장이 다른 요청에서 먼저 변경되었습니다. 최신 원장을 다시 조회한 뒤 재시도해야 합니다.");
        }

        var 분리대상 = 주문원장.포함원장목록.FirstOrDefault(x =>
            string.Equals(x.원장Id, 하위원장Id.Trim(), StringComparison.OrdinalIgnoreCase));
        if (분리대상 is null) return NotFound("주문 원장에 연결된 하위 원장을 찾을 수 없습니다.");
        var 하위원장 = await _원장저장소.원장조회Async(분리대상.원장Id, cancellationToken);
        var 분리잠금사유 = 주문원장연결변경Policy.잠금사유(주문원장, 하위원장);
        if (분리잠금사유 is not null) return Conflict(분리잠금사유);

        var 포함원장목록 = 주문원장.포함원장목록
            .Where(x => !string.Equals(x.원장Id, 하위원장Id.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (포함원장목록.Length == 주문원장.포함원장목록.Count)
        {
            return NotFound("주문 원장에 연결된 하위 원장을 찾을 수 없습니다.");
        }

        var 저장결과 = await 주문원장저장Async(주문원장, 포함원장목록, 변경자, cancellationToken);
        if (저장결과.IsFailed)
        {
            return 저장결과.ToResult<주문원장통합Dto>();
        }

        return Result.Ok(await 통합Dto생성Async(저장결과.Value, cancellationToken));
    }

    private Task<Result<커뮤니티원장Dto>> 주문원장조회Async(string 주문원장Id, CancellationToken cancellationToken)
        => _조회.주문원장조회Async(주문원장Id, cancellationToken);

    private async Task<Result<커뮤니티원장Dto>> 주문원장저장Async(
        커뮤니티원장Dto 주문원장,
        IReadOnlyList<커뮤니티포함원장참조Dto> 포함원장목록,
        string 변경자,
        CancellationToken cancellationToken)
    {
        try
        {
            var 저장된원장 = await _원장저장소.원장저장Async(
                new 커뮤니티원장저장요청
                {
                    원장Id = 주문원장.원장Id,
                    기대Revision = 주문원장.Revision,
                    커뮤니티Id = 주문원장.커뮤니티Id,
                    원장템플릿Key = 주문원장.원장템플릿Key,
                    제목 = 주문원장.제목,
                    원함 = 주문원장.원함,
                    상태 = 주문원장.상태,
                    현재단계Key = 주문원장.현재단계Key,
                    대상OsCode = 주문원장.대상OsCode,
                    대상OsName = 주문원장.대상OsName,
                    생성자UserId = 주문원장.생성자UserId,
                    생성자표시명 = 주문원장.생성자표시명,
                    블록목록 = 주문원장.블록목록,
                    참여자목록 = 주문원장.참여자목록,
                    포함원장목록 = 포함원장목록,
                    다이어그램스냅샷 = 주문원장.다이어그램스냅샷,
                    외부참조 = 주문원장.외부참조,
                    확장속성 = 주문원장.확장속성
                },
                string.IsNullOrWhiteSpace(변경자) ? "system" : 변경자.Trim(),
                cancellationToken);
            return Result.Ok(저장된원장);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict<커뮤니티원장Dto>(exception.Message);
        }
    }

    private Task<주문원장통합Dto> 통합Dto생성Async(커뮤니티원장Dto 주문원장, CancellationToken cancellationToken)
        => _조회.통합Dto생성Async(주문원장, cancellationToken);

    private static Result<T> BadRequest<T>(string message)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", StatusCodes.Status400BadRequest));

    private static Result<주문원장통합Dto> BadRequest(string message)
        => BadRequest<주문원장통합Dto>(message);

    private static Result<T> NotFound<T>(string message)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", StatusCodes.Status404NotFound));

    private static Result<주문원장통합Dto> NotFound(string message)
        => NotFound<주문원장통합Dto>(message);

    private static Result<T> Conflict<T>(string message)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", StatusCodes.Status409Conflict));

    private static Result<주문원장통합Dto> Conflict(string message)
        => Conflict<주문원장통합Dto>(message);

}

public sealed class 주문하위원장연결요청
{
    public string 하위원장Id { get; set; } = string.Empty;
    public string 역할 { get; set; } = string.Empty;
    public bool 필수여부 { get; set; }
    public int? 표시순서 { get; set; }
    public long? 기대Revision { get; set; }
}

public sealed class 주문원장통합Dto
{
    public 커뮤니티원장Dto 주문원장 { get; set; } = new();
    public 주문원장서명상태Dto? 주문자서명상태 { get; set; }
    public IReadOnlyList<주문포함원장Dto> 포함원장목록 { get; set; } = [];
    public int 전체하위원장수 { get; set; }
    public int 완료하위원장수 { get; set; }
    public bool 필수하위원장완료여부 { get; set; }
    public string 필수하위원장판정 { get; set; } = "필수업무미지정";
    public int 서명대상주문수 { get; set; }
    public int 서명완료주문수 { get; set; }
    public IReadOnlyList<string> 미서명주문Ids { get; set; } = [];
    public bool 전체주문서명완료여부 { get; set; }
}

public sealed class 주문포함원장Dto
{
    public string 원장Id { get; set; } = string.Empty;
    public string 원장템플릿Key { get; set; } = string.Empty;
    public string 역할 { get; set; } = string.Empty;
    public bool 필수여부 { get; set; }
    public int 표시순서 { get; set; }
    public string 조회상태 { get; set; } = "정상";
    public 커뮤니티원장Dto? 원장 { get; set; }
    public 주문원장서명상태Dto? 주문자서명상태 { get; set; }
}
