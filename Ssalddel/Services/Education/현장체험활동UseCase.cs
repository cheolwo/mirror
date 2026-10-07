using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using FluentResults;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Education;
using Ssalddel.Services.Community;
using Microsoft.AspNetCore.Http;

namespace Ssalddel.Services.Education;

public interface I현장체험활동UseCase
{
    Task<Result<현장체험활동응답>> 생성Async(
        현장체험활동생성요청? request,
        string actorUserId,
        CancellationToken cancellationToken);

    Task<Result<현장체험활동응답>> 조회Async(
        string 원장Id,
        string actorUserId,
        string? 검토학교Key,
        bool 운영자권한,
        CancellationToken cancellationToken);

    Task<Result<현장체험활동응답>> 활동기록Async(
        string 원장Id,
        현장체험활동기록요청? request,
        string actorUserId,
        CancellationToken cancellationToken);

    Task<Result<현장체험활동응답>> 보호자승인Async(
        string 원장Id,
        현장체험보호자승인요청? request,
        string actorUserId,
        CancellationToken cancellationToken);

    Task<Result<현장체험활동응답>> 현장지도자확인Async(
        string 원장Id,
        string 활동기록Id,
        현장체험지도자확인요청? request,
        string actorUserId,
        CancellationToken cancellationToken);

    Task<Result<현장체험활동응답>> 학교제출Async(
        string 원장Id,
        현장체험학교제출요청? request,
        string actorUserId,
        CancellationToken cancellationToken);

    Task<Result<현장체험활동응답>> 학교결정Async(
        string 원장Id,
        현장체험학교결정요청? request,
        string actorUserId,
        string? 검토학교Key,
        bool 운영자권한,
        CancellationToken cancellationToken);
}

[SsalddelApiWorkflow(SsalddelWorkflow.CommunityTrust)]
[SsalddelUseCase("교육 현장 체험 활동", Summary = "학생 활동을 원장으로 기록하고 보호자 승인, 학교 제출, 학교 결정 이력을 연결합니다.")]
[SsalddelUseCaseActor(SsalddelActor.CommunityMember)]
[SsalddelUseCaseActor(SsalddelActor.PlatformOperator, SsalddelUseCaseActorRole.Supporting)]
public sealed class 현장체험활동UseCase : I현장체험활동UseCase
{
    private readonly I커뮤니티원장저장소 _원장저장소;
    private readonly I교육기관제출대기열 _제출대기열;

    public 현장체험활동UseCase(I커뮤니티원장저장소 원장저장소, I교육기관제출대기열 제출대기열)
    {
        _원장저장소 = 원장저장소;
        _제출대기열 = 제출대기열;
    }

    public async Task<Result<현장체험활동응답>> 생성Async(
        현장체험활동생성요청? request,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("request body is required");
        }

        var validation = ValidateCreate(request, actorUserId);
        if (validation is not null)
        {
            return BadRequest(validation);
        }

        var blocks = new List<커뮤니티원장블록Dto>
        {
            Block(
                "student-plan",
                현장체험활동원장상수.학생계획Block,
                "학생과 학교",
                "작성완료",
                ("학생표시명", request.학생표시명),
                ("학교식별Key", request.학교식별Key),
                ("학교명", request.학교명),
                ("학년반", request.학년반)),
            Block(
                "activity-plan",
                현장체험활동원장상수.활동계획Block,
                "현장 체험 활동 계획",
                "작성완료",
                ("활동목표", request.활동목표),
                ("활동장소", request.활동장소),
                ("시작예정시각", Format(request.시작예정시각)),
                ("종료예정시각", Format(request.종료예정시각)),
                ("계획활동", JsonSerializer.Serialize(request.계획활동)),
                ("현장담당자", request.현장담당자),
                ("학교제출처Key", request.학교제출처Key),
                ("학교담당이메일", request.학교담당이메일)),
            Block(
                "guardian-approval",
                현장체험활동원장상수.보호자승인Block,
                "보호자 승인",
                "승인대기",
                ("보호자표시명", request.보호자표시명),
                ("승인여부", "false"))
        };

        var ledger = await _원장저장소.원장저장Async(
            new 커뮤니티원장저장요청
            {
                커뮤니티Id = "education-private",
                원장템플릿Key = 현장체험활동원장상수.원장템플릿Key,
                제목 = request.제목.Trim(),
                원함 = "플랫폼 활동을 현장 체험 활동으로 기록하고 학교에 제출하고 싶어요.",
                상태 = 현장체험활동상태.계획작성,
                현재단계Key = "activity-plan",
                대상OsCode = 현장체험활동원장상수.대상OsCode,
                대상OsName = 현장체험활동원장상수.대상OsName,
                생성자UserId = actorUserId.Trim(),
                생성자표시명 = request.학생표시명.Trim(),
                블록목록 = blocks,
                참여자목록 = BuildParticipants(request, actorUserId),
                확장속성 = new Dictionary<string, string>
                {
                    ["공개범위"] = "참여자와 교육기관 담당자",
                    ["출석인정결정주체"] = "교육기관"
                }
            },
            actorUserId,
            cancellationToken);

        return Result.Ok(await ToResponseAsync(ledger, cancellationToken));
    }

    public async Task<Result<현장체험활동응답>> 조회Async(
        string 원장Id,
        string actorUserId,
        string? 검토학교Key,
        bool 운영자권한,
        CancellationToken cancellationToken)
    {
        var ledgerResult = await FindLedgerAsync(원장Id, cancellationToken);
        if (ledgerResult.IsFailed)
        {
            return Result.Fail<현장체험활동응답>(ledgerResult.Errors);
        }

        var ledger = ledgerResult.Value;
        if (!운영자권한 && !IsParticipant(ledger, actorUserId) && !CanSchoolReview(ledger, 검토학교Key))
        {
            return Forbidden("이 현장 체험 활동 원장을 조회할 권한이 없습니다.");
        }

        return Result.Ok(await ToResponseAsync(ledger, cancellationToken));
    }

    public async Task<Result<현장체험활동응답>> 활동기록Async(
        string 원장Id,
        현장체험활동기록요청? request,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("request body is required");
        }

        var ledgerResult = await FindLedgerAsync(원장Id, cancellationToken);
        if (ledgerResult.IsFailed)
        {
            return Result.Fail<현장체험활동응답>(ledgerResult.Errors);
        }

        var ledger = ledgerResult.Value;
        if (!string.Equals(ledger.생성자UserId, actorUserId, StringComparison.Ordinal))
        {
            return Forbidden("학생 본인만 활동 기록을 추가할 수 있습니다.");
        }

        if (string.IsNullOrWhiteSpace(request.활동명)
            || string.IsNullOrWhiteSpace(request.활동내용)
            || string.IsNullOrWhiteSpace(request.수행역할))
        {
            return BadRequest("활동명, 활동내용, 수행역할은 필수입니다.");
        }

        if (request.시작시각 == default || request.종료시각 <= request.시작시각)
        {
            return BadRequest("활동 종료시각은 시작시각보다 늦어야 합니다.");
        }

        var blockId = $"activity-{Guid.NewGuid():N}";
        var blocks = ledger.블록목록.Append(Block(
            blockId,
            현장체험활동원장상수.활동기록Block,
            request.활동명,
            HasFieldGuide(ledger) ? "현장확인대기" : "기록완료",
            ("활동내용", request.활동내용),
            ("수행역할", request.수행역할),
            ("시작시각", Format(request.시작시각)),
            ("종료시각", Format(request.종료시각)),
            ("학생기재확인자표시명", request.확인자표시명),
            ("확인메모", request.확인메모),
            ("증빙파일Url목록", JsonSerializer.Serialize(CleanList(request.증빙파일Url목록))))).ToArray();

        var updated = await SaveAsync(
            ledger,
            blocks,
            현장체험활동상태.활동진행,
            "activity-record",
            actorUserId,
            cancellationToken);
        return updated.IsFailed
            ? Result.Fail<현장체험활동응답>(updated.Errors)
            : Result.Ok(await ToResponseAsync(updated.Value, cancellationToken));
    }

    public async Task<Result<현장체험활동응답>> 현장지도자확인Async(
        string 원장Id,
        string 활동기록Id,
        현장체험지도자확인요청? request,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("request body is required");
        }

        var ledgerResult = await FindLedgerAsync(원장Id, cancellationToken);
        if (ledgerResult.IsFailed)
        {
            return Result.Fail<현장체험활동응답>(ledgerResult.Errors);
        }

        var ledger = ledgerResult.Value;
        if (!HasRole(ledger, actorUserId, "현장체험지도자"))
        {
            return Forbidden("이 원장에 지정된 현장체험지도자만 실제 활동을 확인할 수 있습니다.");
        }

        if (string.IsNullOrWhiteSpace(request.지도자표시명))
        {
            return BadRequest("지도자표시명은 필수입니다.");
        }

        var activity = ledger.블록목록.FirstOrDefault(x =>
            x.BlockType == 현장체험활동원장상수.활동기록Block
            && string.Equals(x.BlockId, 활동기록Id, StringComparison.Ordinal));
        if (activity is null)
        {
            return NotFound<현장체험활동응답>("확인할 활동 기록을 찾을 수 없습니다.");
        }

        var data = activity.Data.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        data["실제활동확인여부"] = request.실제활동확인여부 ? "true" : "false";
        data["현장체험지도자UserId"] = actorUserId;
        data["현장체험지도자표시명"] = request.지도자표시명.Trim();
        data["현장확인시각Utc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        SetOptional(data, "현장확인내용", request.확인내용);

        var replacement = new 커뮤니티원장블록Dto
        {
            BlockId = activity.BlockId,
            BlockType = activity.BlockType,
            Title = activity.Title,
            State = request.실제활동확인여부 ? "현장확인완료" : "현장확인거절",
            Data = data
        };
        var blocks = ledger.블록목록
            .Select(x => string.Equals(x.BlockId, activity.BlockId, StringComparison.Ordinal) ? replacement : x)
            .ToArray();
        var updated = await SaveAsync(
            ledger,
            blocks,
            현장체험활동상태.활동진행,
            "field-verification",
            actorUserId,
            cancellationToken);
        return updated.IsFailed
            ? Result.Fail<현장체험활동응답>(updated.Errors)
            : Result.Ok(await ToResponseAsync(updated.Value, cancellationToken));
    }

    public async Task<Result<현장체험활동응답>> 보호자승인Async(
        string 원장Id,
        현장체험보호자승인요청? request,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("request body is required");
        }

        var ledgerResult = await FindLedgerAsync(원장Id, cancellationToken);
        if (ledgerResult.IsFailed)
        {
            return Result.Fail<현장체험활동응답>(ledgerResult.Errors);
        }

        var ledger = ledgerResult.Value;
        if (!HasRole(ledger, actorUserId, "보호자"))
        {
            return Forbidden("원장에 등록된 보호자만 승인할 수 있습니다.");
        }

        if (string.IsNullOrWhiteSpace(request.보호자표시명))
        {
            return BadRequest("보호자표시명은 필수입니다.");
        }

        var blocks = ReplaceSingleBlock(
            ledger,
            현장체험활동원장상수.보호자승인Block,
            Block(
                "guardian-approval",
                현장체험활동원장상수.보호자승인Block,
                "보호자 승인",
                request.승인여부 ? "승인완료" : "승인거절",
                ("승인여부", request.승인여부 ? "true" : "false"),
                ("보호자표시명", request.보호자표시명),
                ("의견", request.의견),
                ("승인시각Utc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture))));

        var updated = await SaveAsync(
            ledger,
            blocks,
            현장체험활동상태.보호자확인,
            "guardian-approval",
            actorUserId,
            cancellationToken);
        return updated.IsFailed
            ? Result.Fail<현장체험활동응답>(updated.Errors)
            : Result.Ok(await ToResponseAsync(updated.Value, cancellationToken));
    }

    public async Task<Result<현장체험활동응답>> 학교제출Async(
        string 원장Id,
        현장체험학교제출요청? request,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("request body is required");
        }

        var ledgerResult = await FindLedgerAsync(원장Id, cancellationToken);
        if (ledgerResult.IsFailed)
        {
            return Result.Fail<현장체험활동응답>(ledgerResult.Errors);
        }

        var ledger = ledgerResult.Value;
        if (!IsParticipant(ledger, actorUserId))
        {
            return Forbidden("원장 참여자만 학교 제출을 요청할 수 있습니다.");
        }

        if (!교육기관제출방식.지원여부(request.전송방식))
        {
            return BadRequest("전송방식은 문서, 이메일, API 중 하나여야 합니다.");
        }

        if (!IsSubmissionReady(ledger))
        {
            return BadRequest("활동 기록 1건 이상, 보호자 승인, 지정된 현장체험지도자의 활동 확인이 완료되어야 학교 제출을 요청할 수 있습니다.");
        }

        var destinationKey = Clean(request.제출처Key) ?? GetData(ledger, 현장체험활동원장상수.활동계획Block, "학교제출처Key");
        var recipientEmail = Clean(request.담당이메일) ?? GetData(ledger, 현장체험활동원장상수.활동계획Block, "학교담당이메일");
        if (request.전송방식 == 교육기관제출방식.Api && string.IsNullOrWhiteSpace(destinationKey))
        {
            return BadRequest("API 제출에는 서버에 등록된 제출처Key가 필요합니다.");
        }

        if (request.전송방식 == 교육기관제출방식.이메일 && !IsValidEmail(recipientEmail))
        {
            return BadRequest("이메일 제출에는 올바른 학교 담당 이메일이 필요합니다.");
        }

        if (ledger.상태 is 현장체험활동상태.출석인정 or 현장체험활동상태.출석미인정)
            return BadRequest("학교 결정이 완료된 활동은 다시 제출할 수 없습니다.");

        var submissionDigest = SubmissionDigest(ledger);
        var previous = ledger.블록목록.LastOrDefault(x =>
            x.BlockType == 현장체험활동원장상수.학교제출Block
            && x.Data.GetValueOrDefault("제출내용Digest") == submissionDigest
            && x.Data.GetValueOrDefault("전송방식") == request.전송방식
            && Clean(x.Data.GetValueOrDefault("제출처Key")) == destinationKey
            && Clean(x.Data.GetValueOrDefault("담당이메일")) == recipientEmail);
        if (previous is not null)
        {
            var response = await ToResponseAsync(ledger, cancellationToken);
            var priorId = previous.Data.GetValueOrDefault("제출Id");
            if (response.제출목록.Any(x => x.제출Id == priorId && x.상태 != 교육기관제출상태.전송실패))
                return Result.Ok(response);
        }

        var submissionId = $"edu-submission-{Guid.NewGuid():N}";
        var blocks = ledger.블록목록.Append(Block(
            submissionId,
            현장체험활동원장상수.학교제출Block,
            "학교 제출 요청",
            교육기관제출상태.전송대기,
            ("제출Id", submissionId),
            ("제출내용Digest", submissionDigest),
            ("제출원장Revision", ledger.Revision.ToString(CultureInfo.InvariantCulture)),
            ("전송방식", request.전송방식),
            ("제출처Key", destinationKey),
            ("담당이메일", recipientEmail),
            ("제출메모", request.제출메모),
            ("요청시각Utc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)))).ToArray();

        var updated = await SaveAsync(
            ledger,
            blocks,
            현장체험활동상태.제출대기,
            "school-submission",
            actorUserId,
            cancellationToken);

        if (updated.IsFailed) return Result.Fail<현장체험활동응답>(updated.Errors);

        await _제출대기열.예약Async(
            submissionId,
            ledger.원장Id,
            request.전송방식,
            destinationKey,
            recipientEmail,
            cancellationToken);
        if (request.전송방식 == 교육기관제출방식.문서)
        {
            await _제출대기열.완료Async(
                submissionId,
                교육기관제출상태.수동제출준비,
                cancellationToken);
        }

        return updated.IsFailed
            ? Result.Fail<현장체험활동응답>(updated.Errors)
            : Result.Ok(await ToResponseAsync(updated.Value, cancellationToken));
    }

    public async Task<Result<현장체험활동응답>> 학교결정Async(
        string 원장Id,
        현장체험학교결정요청? request,
        string actorUserId,
        string? 검토학교Key,
        bool 운영자권한,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("request body is required");
        }

        var ledgerResult = await FindLedgerAsync(원장Id, cancellationToken);
        if (ledgerResult.IsFailed)
        {
            return Result.Fail<현장체험활동응답>(ledgerResult.Errors);
        }

        var ledger = ledgerResult.Value;
        if (!운영자권한 && !CanSchoolReview(ledger, 검토학교Key))
        {
            return Forbidden("원장 학교 범위와 일치하는 선생님만 출석 인정 결정을 기록할 수 있습니다.");
        }

        if (string.IsNullOrWhiteSpace(request.결정기관명) || string.IsNullOrWhiteSpace(request.결정자표시명))
        {
            return BadRequest("결정기관명과 결정자표시명은 필수입니다.");
        }

        if (!ledger.블록목록.Any(x => x.BlockType == 현장체험활동원장상수.학교제출Block))
        {
            return BadRequest("학교 제출 요청이 없는 원장에는 출석 인정 결정을 기록할 수 없습니다.");
        }

        var blocks = ReplaceSingleBlock(
            ledger,
            현장체험활동원장상수.학교결정Block,
            Block(
                "school-decision",
                현장체험활동원장상수.학교결정Block,
                "학교 출석 인정 결정",
                request.출석인정여부 ? 현장체험활동상태.출석인정 : 현장체험활동상태.출석미인정,
                ("출석인정여부", request.출석인정여부 ? "true" : "false"),
                ("결정기관명", request.결정기관명),
                ("결정자표시명", request.결정자표시명),
                ("결정문서번호", request.결정문서번호),
                ("의견", request.의견),
                ("결정자UserId", actorUserId),
                ("결정시각Utc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture))));

        var state = request.출석인정여부 ? 현장체험활동상태.출석인정 : 현장체험활동상태.출석미인정;
        var updated = await SaveAsync(ledger, blocks, state, "school-decision", actorUserId, cancellationToken);
        return updated.IsFailed
            ? Result.Fail<현장체험활동응답>(updated.Errors)
            : Result.Ok(await ToResponseAsync(updated.Value, cancellationToken));
    }

    private async Task<Result<커뮤니티원장Dto>> FindLedgerAsync(string ledgerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ledgerId))
        {
            return BadRequest<커뮤니티원장Dto>("원장Id는 필수입니다.");
        }

        var ledger = await _원장저장소.원장조회Async(ledgerId.Trim(), cancellationToken);
        if (ledger is null || ledger.원장템플릿Key != 현장체험활동원장상수.원장템플릿Key)
        {
            return NotFound<커뮤니티원장Dto>("현장 체험 활동 원장을 찾을 수 없습니다.");
        }

        return Result.Ok(ledger);
    }

    private async Task<Result<커뮤니티원장Dto>> SaveAsync(
        커뮤니티원장Dto ledger,
        IReadOnlyList<커뮤니티원장블록Dto> blocks,
        string state,
        string step,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        if (ledger.상태 is 현장체험활동상태.출석인정 or 현장체험활동상태.출석미인정
            && step != "school-decision")
            return Result.Fail<커뮤니티원장Dto>(new Error("학교 결정이 완료된 활동은 변경할 수 없습니다.")
                .WithMetadata("StatusCode", StatusCodes.Status409Conflict));
        try
        {
            return Result.Ok(await _원장저장소.원장저장Async(
                new 커뮤니티원장저장요청
                {
                    원장Id = ledger.원장Id,
                    기대Revision = ledger.Revision,
                    커뮤니티Id = ledger.커뮤니티Id,
                    원장템플릿Key = ledger.원장템플릿Key,
                    제목 = ledger.제목,
                    원함 = ledger.원함,
                    상태 = state,
                    현재단계Key = step,
                    대상OsCode = ledger.대상OsCode,
                    대상OsName = ledger.대상OsName,
                    생성자UserId = ledger.생성자UserId,
                    생성자표시명 = ledger.생성자표시명,
                    블록목록 = blocks,
                    참여자목록 = ledger.참여자목록,
                    다이어그램스냅샷 = ledger.다이어그램스냅샷,
                    외부참조 = ledger.외부참조,
                    확장속성 = ledger.확장속성
                },
                actorUserId,
                cancellationToken));
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("원장의 현재 상태가 다른 요청에서 먼저 변경되었습니다.", StringComparison.Ordinal))
        {
            return Result.Fail<커뮤니티원장Dto>(new Error("다른 요청이 먼저 처리되었습니다. 최신 활동을 다시 조회한 뒤 재시도해 주세요.")
                .WithMetadata("StatusCode", StatusCodes.Status409Conflict));
        }
    }

    private static string SubmissionDigest(커뮤니티원장Dto ledger)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            ledger.제목,
            Blocks = ledger.블록목록.Where(x => x.BlockType != 현장체험활동원장상수.학교제출Block)
                .OrderBy(x => x.BlockId, StringComparer.Ordinal)
                .Select(x => new { x.BlockId, x.BlockType, x.Title, x.State,
                    Data = x.Data.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray() })
        }))));

    private async Task<현장체험활동응답> ToResponseAsync(
        커뮤니티원장Dto ledger,
        CancellationToken cancellationToken)
    {
        var activityBlocks = ledger.블록목록
            .Where(x => x.BlockType == 현장체험활동원장상수.활동기록Block)
            .ToArray();
        var evidenceCount = activityBlocks.Sum(block => DeserializeList(block.Data, "증빙파일Url목록").Count);
        var submissions = await _제출대기열.원장별조회Async(ledger.원장Id, cancellationToken);
        // Mongo 원장에 확정된 제출 의도가 대기열 예약 전에 중단됐다면 같은 ID로 복구한다.
        foreach (var block in ledger.블록목록.Where(x => x.BlockType == 현장체험활동원장상수.학교제출Block))
        {
            var id = block.Data.GetValueOrDefault("제출Id");
            var way = block.Data.GetValueOrDefault("전송방식");
            if (string.IsNullOrWhiteSpace(id) || !교육기관제출방식.지원여부(way)
                || submissions.Any(x => x.제출Id == id)) continue;
            await _제출대기열.예약Async(id, ledger.원장Id, way!,
                block.Data.GetValueOrDefault("제출처Key"), block.Data.GetValueOrDefault("담당이메일"), cancellationToken);
            if (way == 교육기관제출방식.문서)
                await _제출대기열.완료Async(id, 교육기관제출상태.수동제출준비, cancellationToken);
        }
        submissions = await _제출대기열.원장별조회Async(ledger.원장Id, cancellationToken);
        var decisionValue = GetData(ledger, 현장체험활동원장상수.학교결정Block, "출석인정여부");

        return new 현장체험활동응답
        {
            원장Id = ledger.원장Id,
            제목 = ledger.제목,
            상태 = ledger.상태,
            현재단계 = ledger.현재단계Key,
            학생표시명 = GetData(ledger, 현장체험활동원장상수.학생계획Block, "학생표시명") ?? string.Empty,
            학교명 = GetData(ledger, 현장체험활동원장상수.학생계획Block, "학교명") ?? string.Empty,
            시작예정시각 = ParseDate(GetData(ledger, 현장체험활동원장상수.활동계획Block, "시작예정시각")),
            종료예정시각 = ParseDate(GetData(ledger, 현장체험활동원장상수.활동계획Block, "종료예정시각")),
            활동기록수 = activityBlocks.Length,
            현장확인완료수 = activityBlocks.Count(x => x.State == "현장확인완료"),
            증빙파일수 = evidenceCount,
            보호자승인완료 = GuardianApproved(ledger),
            학교제출요건충족 = IsSubmissionReady(ledger),
            출석인정여부 = bool.TryParse(decisionValue, out var decision) ? decision : null,
            제출목록 = submissions,
            수정시각Utc = ledger.수정시각Utc
        };
    }

    private static string? ValidateCreate(현장체험활동생성요청 request, string actorUserId)
    {
        if (string.IsNullOrWhiteSpace(actorUserId)) return "인증된 사용자 정보가 필요합니다.";
        if (string.IsNullOrWhiteSpace(request.제목)) return "제목은 필수입니다.";
        if (string.IsNullOrWhiteSpace(request.학생표시명)) return "학생표시명은 필수입니다.";
        if (string.IsNullOrWhiteSpace(request.학교식별Key)) return "학교식별Key는 필수입니다.";
        if (string.IsNullOrWhiteSpace(request.학교명)) return "학교명은 필수입니다.";
        if (string.IsNullOrWhiteSpace(request.보호자UserId)) return "보호자UserId는 필수입니다.";
        if (string.IsNullOrWhiteSpace(request.보호자표시명)) return "보호자표시명은 필수입니다.";
        if (string.IsNullOrWhiteSpace(request.현장체험지도자UserId) != string.IsNullOrWhiteSpace(request.현장체험지도자표시명))
            return "현장체험지도자를 지정하려면 UserId와 표시명을 함께 입력해야 합니다.";
        if (string.IsNullOrWhiteSpace(request.활동목표)) return "활동목표는 필수입니다.";
        if (string.IsNullOrWhiteSpace(request.활동장소)) return "활동장소는 필수입니다.";
        if (request.시작예정시각 == default || request.종료예정시각 <= request.시작예정시각)
            return "종료예정시각은 시작예정시각보다 늦어야 합니다.";
        if (request.계획활동.Count == 0 || request.계획활동.All(string.IsNullOrWhiteSpace))
            return "계획활동을 하나 이상 입력해야 합니다.";
        if (!string.IsNullOrWhiteSpace(request.학교담당이메일) && !IsValidEmail(request.학교담당이메일))
            return "학교담당이메일 형식이 올바르지 않습니다.";
        return null;
    }

    private static 커뮤니티원장블록Dto Block(
        string blockId,
        string blockType,
        string title,
        string state,
        params (string Key, string? Value)[] values)
        => new()
        {
            BlockId = blockId,
            BlockType = blockType,
            Title = title.Trim(),
            State = state,
            Data = values
                .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                .ToDictionary(x => x.Key, x => x.Value!.Trim(), StringComparer.OrdinalIgnoreCase)
        };

    private static IReadOnlyList<커뮤니티원장블록Dto> ReplaceSingleBlock(
        커뮤니티원장Dto ledger,
        string blockType,
        커뮤니티원장블록Dto replacement)
        => ledger.블록목록.Where(x => x.BlockType != blockType).Append(replacement).ToArray();

    private static IReadOnlyList<커뮤니티원장참여자Dto> BuildParticipants(
        현장체험활동생성요청 request,
        string actorUserId)
    {
        var participants = new List<커뮤니티원장참여자Dto>
        {
            new()
            {
                UserId = actorUserId.Trim(),
                DisplayName = request.학생표시명.Trim(),
                RoleLabel = "학생",
                ParticipationState = "참여중"
            },
            new()
            {
                UserId = request.보호자UserId.Trim(),
                DisplayName = request.보호자표시명.Trim(),
                RoleLabel = "보호자",
                ParticipationState = "승인대기"
            }
        };

        if (!string.IsNullOrWhiteSpace(request.현장체험지도자UserId))
        {
            participants.Add(new 커뮤니티원장참여자Dto
            {
                UserId = request.현장체험지도자UserId.Trim(),
                DisplayName = request.현장체험지도자표시명!.Trim(),
                RoleLabel = "현장체험지도자",
                ParticipationState = "현장확인대기"
            });
        }

        return participants;
    }

    private static bool IsParticipant(커뮤니티원장Dto ledger, string actorUserId)
        => !string.IsNullOrWhiteSpace(actorUserId)
           && ledger.참여자목록.Any(x => string.Equals(x.UserId, actorUserId, StringComparison.Ordinal));

    private static bool HasRole(커뮤니티원장Dto ledger, string actorUserId, string role)
        => ledger.참여자목록.Any(x => string.Equals(x.UserId, actorUserId, StringComparison.Ordinal)
                                             && string.Equals(x.RoleLabel, role, StringComparison.Ordinal));

    private static bool GuardianApproved(커뮤니티원장Dto ledger)
        => bool.TryParse(GetData(ledger, 현장체험활동원장상수.보호자승인Block, "승인여부"), out var approved) && approved;

    private static bool IsSubmissionReady(커뮤니티원장Dto ledger)
    {
        var activities = ledger.블록목록
            .Where(x => x.BlockType == 현장체험활동원장상수.활동기록Block)
            .ToArray();
        return GuardianApproved(ledger)
               && activities.Length > 0
               && (!HasFieldGuide(ledger) || activities.All(x => x.State == "현장확인완료"));
    }

    private static bool HasFieldGuide(커뮤니티원장Dto ledger)
        => ledger.참여자목록.Any(x => x.RoleLabel == "현장체험지도자");

    private static bool CanSchoolReview(커뮤니티원장Dto ledger, string? schoolKey)
        => !string.IsNullOrWhiteSpace(schoolKey)
           && string.Equals(
               GetData(ledger, 현장체험활동원장상수.학생계획Block, "학교식별Key"),
               schoolKey.Trim(),
               StringComparison.OrdinalIgnoreCase);

    private static void SetOptional(IDictionary<string, string> data, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            data.Remove(key);
            return;
        }

        data[key] = value.Trim();
    }

    private static string? GetData(커뮤니티원장Dto ledger, string blockType, string key)
    {
        var block = ledger.블록목록.LastOrDefault(x => x.BlockType == blockType);
        return block is not null && block.Data.TryGetValue(key, out var value) ? value : null;
    }

    private static IReadOnlyList<string> DeserializeList(IReadOnlyDictionary<string, string> data, string key)
    {
        if (!data.TryGetValue(key, out var json) || string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> CleanList(IEnumerable<string> values)
        => values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToArray();

    private static DateTimeOffset ParseDate(string? value)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result)
            ? result
            : default;

    private static string Format(DateTimeOffset value)
        => value.ToString("O", CultureInfo.InvariantCulture);

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsValidEmail(string? value)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(value) && new MailAddress(value.Trim()).Address == value.Trim();
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static Result<현장체험활동응답> BadRequest(string message)
        => BadRequest<현장체험활동응답>(message);

    private static Result<T> BadRequest<T>(string message)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", StatusCodes.Status400BadRequest));

    private static Result<현장체험활동응답> Forbidden(string message)
        => Result.Fail<현장체험활동응답>(new Error(message).WithMetadata("StatusCode", StatusCodes.Status403Forbidden));

    private static Result<T> NotFound<T>(string message)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", StatusCodes.Status404NotFound));
}
