using FluentResults;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Metadata;

namespace Hongdal.Services.Community;

public interface I커뮤니티대화UseCase
{
    Task<Result<다이어그램대화방목록Response>> 다이어그램대화방목록Async(
        string? communityId,
        string? ledgerId,
        string? diagramId,
        string? participantUserId,
        int limit,
        CancellationToken cancellationToken);

    Task<Result<다이어그램대화메시지목록Response>> 다이어그램메시지목록Async(
        string roomId,
        int limit,
        CancellationToken cancellationToken);
}

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Participation,
    HongdalModuleKind.Application,
    "공동 원장과 다이어그램에 연결된 대화방·메시지 맥락을 조회",
    ReleaseStage = HongdalCommunityV0ReleaseStages.ClosedLoop,
    Boundary = "대화 기록은 참여자의 합의를 대신하지 않으며 연락처와 비공개 참여 정보는 권한 범위 안에서만 조회해야 합니다.")]
[HongdalApiWorkflow(HongdalWorkflow.CommunityTrust)]
[HongdalUseCase("커뮤니티 다이어그램 대화 조회", Summary = "다이어그램 대화방과 저장된 메시지를 조회해 커뮤니티 소통 맥락을 복원합니다.")]
[HongdalUseCaseActor(HongdalActor.CommunityMember)]
[HongdalUseCaseActor(HongdalActor.PlatformOperator, HongdalUseCaseActorRole.Supporting)]
public sealed class 커뮤니티대화UseCase : I커뮤니티대화UseCase
{
    private readonly I커뮤니티대화저장소 _대화저장소;

    public 커뮤니티대화UseCase(I커뮤니티대화저장소 대화저장소)
    {
        _대화저장소 = 대화저장소;
    }

    public async Task<Result<다이어그램대화방목록Response>> 다이어그램대화방목록Async(
        string? communityId,
        string? ledgerId,
        string? diagramId,
        string? participantUserId,
        int limit,
        CancellationToken cancellationToken)
    {
        var items = await _대화저장소.대화방목록조회Async(
            new 커뮤니티대화방조회조건
            {
                커뮤니티Id = Clean(communityId),
                유형 = 커뮤니티대화방유형.Diagram,
                원장Id = Clean(ledgerId),
                다이어그램Id = Clean(diagramId),
                참여자UserId = Clean(participantUserId),
                Limit = limit <= 0 ? 50 : limit
            },
            cancellationToken);

        return Result.Ok(new 다이어그램대화방목록Response
        {
            Items = items.Select(ToResponse).ToArray()
        });
    }

    public async Task<Result<다이어그램대화메시지목록Response>> 다이어그램메시지목록Async(
        string roomId,
        int limit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return Result.Fail<다이어그램대화메시지목록Response>("다이어그램 대화방 ID가 필요합니다.");
        }

        var normalizedRoomId = roomId.Trim();
        var items = await _대화저장소.메시지목록조회Async(
            new 커뮤니티메시지조회조건
            {
                대화방Id = normalizedRoomId,
                Limit = limit <= 0 ? 80 : limit
            },
            cancellationToken);

        return Result.Ok(new 다이어그램대화메시지목록Response
        {
            RoomId = normalizedRoomId,
            Items = items.Select(ToResponse).ToArray()
        });
    }

    private static 다이어그램대화방Response ToResponse(커뮤니티대화방Dto dto)
        => new()
        {
            RoomId = dto.대화방Id,
            CommunityId = dto.커뮤니티Id,
            ConversationType = dto.유형,
            Title = dto.제목,
            LedgerId = dto.원장Id,
            LedgerTemplateKey = dto.원장템플릿Key,
            DiagramId = dto.다이어그램Id,
            DiagramName = dto.다이어그램이름,
            WorkContext = dto.업무Context,
            Participants = dto.참여자목록.Select(ToResponse).ToArray(),
            LastMessageId = dto.마지막메시지Id,
            LastMessageSummary = dto.마지막메시지요약,
            LastMessageKind = ToDiagramMessageKind(dto.마지막메시지종류),
            LastMessageAtUtc = dto.마지막메시지시각Utc,
            CreatedAtUtc = dto.생성시각Utc,
            UpdatedAtUtc = dto.수정시각Utc
        };

    private static 다이어그램대화방참여자Response ToResponse(커뮤니티대화방참여자Dto dto)
        => new()
        {
            UserId = dto.UserId,
            DisplayName = dto.DisplayName,
            RoleLabel = dto.RoleLabel,
            ParticipationState = dto.ParticipationState,
            LastReadMessageId = dto.마지막읽은MessageId,
            LastReadAtUtc = dto.마지막읽은시각Utc
        };

    private static DiagramChatMessageResponse ToResponse(커뮤니티메시지Dto dto)
        => new()
        {
            MessageId = dto.MessageId,
            RoomId = dto.대화방Id,
            SenderUserId = dto.보낸사람UserId,
            SenderDisplayName = dto.보낸사람표시명,
            Message = dto.메시지,
            DiagramId = dto.다이어그램Id,
            DiagramName = dto.다이어그램이름,
            MessageKind = ToDiagramMessageKind(dto.메시지종류),
            SentAtUtc = dto.생성시각Utc
        };

    private static string ToDiagramMessageKind(string? messageKind)
    {
        if (string.IsNullOrWhiteSpace(messageKind))
        {
            return DiagramCollaborationMessageKinds.Text;
        }

        if (string.Equals(messageKind, 커뮤니티메시지종류.Diagram, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(messageKind, 커뮤니티메시지종류.WorkAction, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(messageKind, 커뮤니티메시지종류.Ledger, StringComparison.OrdinalIgnoreCase))
        {
            return DiagramCollaborationMessageKinds.DiagramNote;
        }

        return messageKind.Trim();
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
