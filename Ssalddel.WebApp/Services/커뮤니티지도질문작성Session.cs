using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.WebApp.Services;

internal enum QuestionPurpose
{
    Question,
    Proposal,
    Gathering
}

// 초안의 입력 수명과 사용자가 명시적으로 실행한 한 번의 게시를 소유합니다.
internal sealed class 커뮤니티지도질문작성Session(
    Func<string, 커뮤니티세계지도질문초안Request, CancellationToken, Task<커뮤니티세계지도질문초안Response>> createDraft,
    Func<string, 커뮤니티세계지도질문게시Request, string, CancellationToken, Task<커뮤니티세계지도질문게시Response>> publish)
{
    public bool DraftLoading { get; private set; }
    public bool Publishing { get; private set; }
    public 커뮤니티세계지도질문초안Response? Draft { get; private set; }
    public 커뮤니티세계지도질문게시Response? Published { get; private set; }
    public QuestionPurpose Purpose { get; private set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool ConfirmSourceReference { get; set; }
    public string? Error { get; private set; }

    public bool CanPublish(bool loggedIn)
        => loggedIn
           && !Publishing
           && Draft is not null
           && !string.IsNullOrWhiteSpace(Title)
           && !string.IsNullOrWhiteSpace(Body)
           && !string.IsNullOrWhiteSpace(Nickname)
           && Password.Trim().Length is >= 4 and <= 100
           && ConfirmSourceReference;

    public async Task OpenAsync(
        커뮤니티세계지도ObservationDto observation,
        string datasetCode,
        QuestionPurpose purpose,
        Func<string?> currentNickname)
    {
        if (DraftLoading)
        {
            return;
        }

        DraftLoading = true;
        Error = null;
        Published = null;
        Purpose = purpose;
        try
        {
            var draft = await createDraft(
                observation.StableId,
                new 커뮤니티세계지도질문초안Request
                {
                    DatasetCode = datasetCode,
                    QuestionFocus = QuestionFocus(observation.Title, purpose)
                },
                CancellationToken.None);
            Draft = draft;
            Title = draft.SuggestedPost.Title;
            Body = draft.SuggestedPost.Body;
            Nickname = currentNickname() ?? string.Empty;
            Password = string.Empty;
            ConfirmSourceReference = false;
        }
        catch (Exception exception)
        {
            Draft = null;
            Error = exception.Message;
        }
        finally
        {
            DraftLoading = false;
        }
    }

    // 호출자의 기존 인증/선택 관문을 통과한 명시적 동작만 들어옵니다. 재전송하지 않습니다.
    public async Task PublishAsync(string observationStableId, string datasetCode, string accessToken)
    {
        if (Draft is not { } draft)
        {
            return;
        }

        Publishing = true;
        Error = null;
        try
        {
            Published = await publish(
                observationStableId,
                new 커뮤니티세계지도질문게시Request
                {
                    DatasetCode = datasetCode,
                    Title = Title.Trim(),
                    Body = Body.Trim(),
                    OriginalLanguageCode = draft.SuggestedPost.OriginalLanguageCode,
                    IsInterestGatheringEnabled = Purpose == QuestionPurpose.Gathering,
                    Nickname = Nickname.Trim(),
                    Password = Password,
                    ConfirmSourceReference = ConfirmSourceReference
                },
                accessToken,
                CancellationToken.None);
            Draft = null;
            Password = string.Empty;
            ConfirmSourceReference = false;
        }
        catch (Exception exception)
        {
            Error = exception.Message;
        }
        finally
        {
            Publishing = false;
        }
    }

    public void Reset(bool clearPublished = true)
    {
        Draft = null;
        DraftLoading = false;
        Publishing = false;
        Title = string.Empty;
        Body = string.Empty;
        Nickname = string.Empty;
        Password = string.Empty;
        ConfirmSourceReference = false;
        Error = null;
        if (clearPublished)
        {
            Published = null;
        }
    }

    private static string QuestionFocus(string observationTitle, QuestionPurpose purpose)
        => purpose switch
        {
            QuestionPurpose.Proposal => $"{observationTitle} 자료를 바탕으로 아이디어를 나눠요",
            QuestionPurpose.Gathering => $"{observationTitle} 공개 근거를 함께 확인할 분을 찾습니다",
            _ => $"{observationTitle}에 대해 궁금한 점이 있어요"
        };
}
