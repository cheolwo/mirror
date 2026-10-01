using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public static class 공동구매절차코드
{
    public const string 제안 = CommunityGroupPurchaseLedgerStageCodes.Proposal;
    public const string 거래경로 = CommunityGroupPurchaseLedgerStageCodes.TradeRoute;
    public const string 수요모집 = CommunityGroupPurchaseLedgerStageCodes.Recruitment;
    public const string 거래상대연결 = CommunityGroupPurchaseLedgerStageCodes.Counterparty;
    public const string 공급조건협상 = CommunityGroupPurchaseLedgerStageCodes.SupplyNegotiation;
    public const string 이의검토 = CommunityGroupPurchaseLedgerStageCodes.Objection;
    public const string 확정안 = CommunityGroupPurchaseLedgerStageCodes.Resolution;
    public const string 전자서명 = CommunityGroupPurchaseLedgerStageCodes.Signature;
    public const string 이행계획 = CommunityGroupPurchaseLedgerStageCodes.FulfillmentPlan;
    public const string 실행 = CommunityGroupPurchaseLedgerStageCodes.Execution;
    public const string 커머스 = CommunityGroupPurchaseLedgerStageCodes.Commerce;
}

public enum 공동구매절차단계상태
{
    대기,
    진행중,
    완료
}

public sealed record 공동구매절차단계정의(
    int 순서,
    string 코드,
    string 제목,
    string 설명);

public sealed record 공동구매절차단계표시(
    int 순서,
    string 코드,
    string 제목,
    string 설명,
    공동구매절차단계상태 상태,
    bool 선택됨)
{
    public string 상태문구 => 상태 switch
    {
        공동구매절차단계상태.완료 => $"완료 · {설명}",
        공동구매절차단계상태.진행중 => $"진행 중 · {설명}",
        _ => $"대기 · {설명}"
    };
}

public static class 공동구매절차카탈로그
{
    public static IReadOnlyList<공동구매절차단계정의> 전체 { get; } =
    [
        new(1, 공동구매절차코드.제안, "제안 글", "상품과 운영 조건 공개"),
        new(2, 공동구매절차코드.거래경로, "거래 경로", "국내 공동구매 또는 공동수입 분기"),
        new(3, 공동구매절차코드.수요모집, "수요 모집", "지역·픽업별 참여와 자동집단 수요 구성"),
        new(4, 공동구매절차코드.거래상대연결, "거래 상대 연결", "생산자·공동구매 대표 또는 해외 판매자 연결"),
        new(5, 공동구매절차코드.공급조건협상, "공급 조건 협상", "가격·수량·품질·통관·물류 조건 합의"),
        new(6, 공동구매절차코드.이의검토, "이의 검토", "협상 결과 공개와 최종 조정"),
        new(7, 공동구매절차코드.확정안, "확정안", "최종 조건과 결의문 작성"),
        new(8, 공동구매절차코드.전자서명, "전자서명", "당사자와 필수 구성원 동의"),
        new(9, 공동구매절차코드.이행계획, "이행 계획", "국내 물류 또는 공동수입 원장 계획"),
        new(10, 공동구매절차코드.실행, "주문 실행", "자동집단·주문원장과 후속 물류 실행"),
        new(11, 공동구매절차코드.커머스, "커머스 이행", "입고·재고·출품·출고 상태 확인")
    ];

    public static 공동구매절차단계정의? 찾기(string? code)
        => 전체.FirstOrDefault(stage =>
            string.Equals(stage.코드, code, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// 여러 공동구매 하위 ViewModel이 공유하는 목록, 선택 항목, 의견과 현재 단계입니다.
/// 페이지마다 별도 복사본을 만들지 않아 사용자 흐름 중 선택 상태가 어긋나는 것을 막습니다.
/// </summary>
public sealed class 공동구매화면상태ViewModel : ObservableObject, IDisposable
{
    private readonly I공동구매원장절차Client _원장절차Client;
    private readonly 업무선택ContextViewModel _선택Context = new();
    private IReadOnlyList<CommunityVoteResponse> _공동구매목록 = [];
    private CommunityVoteResponse? _선택된공동구매;
    private IReadOnlyList<PlatformCommunityPostCommentResponse> _의견목록 = [];
    private string _현재단계코드 = 공동구매절차코드.제안;
    private string _진행단계코드 = 공동구매절차코드.제안;
    private CommunityGroupPurchaseLedgerProgressResponse? _원장절차;
    private string? _원장동기화오류;

    public 공동구매화면상태ViewModel(
        I공동구매원장절차Client 원장절차Client,
        IHongdal현재사용자Context? 현재사용자Context = null)
    {
        _원장절차Client = 원장절차Client;
        this.현재사용자Context = 현재사용자Context;
    }

    public IHongdal현재사용자Context? 현재사용자Context { get; }
    public 현재사용자Snapshot 현재사용자
        => 현재사용자Context?.현재사용자 ?? 현재사용자Snapshot.익명;

    public IReadOnlyList<CommunityVoteResponse> 공동구매목록
    {
        get => _공동구매목록;
        private set => SetProperty(ref _공동구매목록, value);
    }

    public CommunityVoteResponse? 선택된공동구매
    {
        get => _선택된공동구매;
        private set => SetProperty(ref _선택된공동구매, value);
    }

    public IReadOnlyList<PlatformCommunityPostCommentResponse> 의견목록
    {
        get => _의견목록;
        private set => SetProperty(ref _의견목록, value);
    }

    public string 현재단계코드
    {
        get => _현재단계코드;
        private set => SetProperty(ref _현재단계코드, value);
    }

    /// <summary>
    /// 현재 화면에서 선택한 단계와 별개로 실제 업무가 도달한 가장 앞선 단계입니다.
    /// </summary>
    public string 진행단계코드
    {
        get => _진행단계코드;
        private set => SetProperty(ref _진행단계코드, value);
    }

    public CommunityGroupPurchaseLedgerProgressResponse? 원장절차
    {
        get => _원장절차;
        private set
        {
            if (!SetProperty(ref _원장절차, value))
            {
                return;
            }

            OnPropertyChanged(nameof(원장연결됨));
            OnPropertyChanged(nameof(공동구매원장Id));
            OnPropertyChanged(nameof(원장Revision));
        }
    }

    public string? 원장동기화오류
    {
        get => _원장동기화오류;
        private set => SetProperty(ref _원장동기화오류, value);
    }

    public Guid? 선택된공동구매Id => 선택된공동구매?.Id;
    public bool 공동구매선택됨 => 선택된공동구매 is not null;
    public bool 원장연결됨 => 원장절차 is not null && !string.IsNullOrWhiteSpace(원장절차.CommunityLedgerId);
    public string? 공동구매원장Id => 원장절차?.CommunityLedgerId ?? 선택된공동구매?.CommunityLedgerId;
    public long? 원장Revision => 원장절차?.Revision;

    public void 목록적용(IEnumerable<CommunityVoteResponse> campaigns)
    {
        ArgumentNullException.ThrowIfNull(campaigns);
        공동구매목록 = campaigns.ToArray();
    }

    public void 선택적용(
        CommunityVoteResponse campaign,
        IEnumerable<PlatformCommunityPostCommentResponse>? comments = null)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        _선택Context.선택(campaign.Id.ToString("D"));
        ReplaceCampaign(campaign);
        선택된공동구매 = campaign;
        의견목록 = comments?.ToArray() ?? [];
        원장절차 = null;
        원장동기화오류 = null;
        var inferredStageCode = 진행단계추론(campaign);
        진행단계코드 = inferredStageCode;
        현재단계코드 = inferredStageCode;
        OnPropertyChanged(nameof(선택된공동구매Id));
        OnPropertyChanged(nameof(공동구매선택됨));
    }

    public async Task 선택적용Async(
        CommunityVoteResponse campaign,
        IEnumerable<PlatformCommunityPostCommentResponse>? comments = null,
        CancellationToken cancellationToken = default)
    {
        선택적용(campaign, comments);
        await 원장절차복원Async(campaign, cancellationToken);
    }

    public void 선택해제()
    {
        _선택Context.선택(null);
        선택된공동구매 = null;
        의견목록 = [];
        현재단계코드 = 공동구매절차코드.제안;
        진행단계코드 = 공동구매절차코드.제안;
        원장절차 = null;
        원장동기화오류 = null;
        OnPropertyChanged(nameof(선택된공동구매Id));
        OnPropertyChanged(nameof(공동구매선택됨));
    }

    public void 새공동구매적용(CommunityVoteResponse campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        _선택Context.선택(campaign.Id.ToString("D"));
        공동구매목록 = 공동구매목록
            .Where(item => item.Id != campaign.Id)
            .Prepend(campaign)
            .ToArray();
        선택된공동구매 = campaign;
        의견목록 = [];
        원장절차 = null;
        원장동기화오류 = null;
        var initialStageCode = 진행단계추론(campaign);
        현재단계코드 = initialStageCode;
        진행단계코드 = initialStageCode;
        OnPropertyChanged(nameof(선택된공동구매Id));
        OnPropertyChanged(nameof(공동구매선택됨));
    }

    public async Task 새공동구매적용Async(
        CommunityVoteResponse campaign,
        CancellationToken cancellationToken = default)
    {
        새공동구매적용(campaign);
        await 원장절차복원Async(campaign, cancellationToken);
    }

    public void 공동구매갱신(CommunityVoteResponse campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ReplaceCampaign(campaign);

        if (선택된공동구매?.Id == campaign.Id)
        {
            _선택된공동구매 = campaign;
            OnPropertyChanged(nameof(선택된공동구매));
            OnPropertyChanged(nameof(선택된공동구매Id));
            OnPropertyChanged(nameof(공동구매선택됨));
        }
    }

    public void 의견추가(PlatformCommunityPostCommentResponse comment)
    {
        ArgumentNullException.ThrowIfNull(comment);
        의견목록 = 의견목록.Append(comment).ToArray();
    }

    public void 단계선택(string stageCode)
    {
        if (공동구매절차카탈로그.찾기(stageCode) is null)
        {
            throw new ArgumentException($"알 수 없는 공동구매 절차 코드입니다: {stageCode}", nameof(stageCode));
        }

        현재단계코드 = stageCode;
    }

    public void 단계진행(string stageCode)
    {
        var target = 공동구매절차카탈로그.찾기(stageCode)
            ?? throw new ArgumentException(
                $"알 수 없는 공동구매 절차 코드입니다: {stageCode}",
                nameof(stageCode));
        var current = 공동구매절차카탈로그.찾기(진행단계코드);
        if (current is not null && target.순서 < current.순서)
        {
            throw new InvalidOperationException(
                $"공동구매 진행 단계는 이전 단계로 되돌릴 수 없습니다: {진행단계코드} -> {stageCode}");
        }

        진행단계코드 = stageCode;
        현재단계코드 = stageCode;
    }

    public async Task 단계진행Async(
        string stageCode,
        string? memo = null,
        CancellationToken cancellationToken = default)
    {
        단계전진검증(stageCode);
        var campaignId = 선택된공동구매Id
            ?? throw new InvalidOperationException("원장 절차를 진행할 공동구매가 선택되지 않았습니다.");
        var progress = await _원장절차Client.진행Async(
            campaignId,
            new CommunityGroupPurchaseLedgerProgressRequest
            {
                StageCode = stageCode,
                Memo = memo ?? string.Empty,
                ExpectedRevision = 원장Revision
            },
            cancellationToken)
            ?? throw new InvalidOperationException("공동구매 원장 절차 진행 응답이 비어 있습니다.");
        원장절차적용(progress);
    }

    public bool 단계도달(string stageCode)
    {
        var target = 공동구매절차카탈로그.찾기(stageCode)
            ?? throw new ArgumentException(
                $"알 수 없는 공동구매 절차 코드입니다: {stageCode}",
                nameof(stageCode));
        var current = 공동구매절차카탈로그.찾기(진행단계코드);
        if (current is not null && target.순서 <= current.순서)
        {
            return false;
        }

        진행단계코드 = stageCode;
        현재단계코드 = stageCode;
        return true;
    }

    public async Task<bool> 단계도달Async(
        string stageCode,
        string? memo = null,
        CancellationToken cancellationToken = default)
    {
        var target = 공동구매절차카탈로그.찾기(stageCode)
            ?? throw new ArgumentException(
                $"알 수 없는 공동구매 절차 코드입니다: {stageCode}",
                nameof(stageCode));
        var current = 공동구매절차카탈로그.찾기(진행단계코드);
        if (current is not null && target.순서 <= current.순서)
        {
            return false;
        }

        await 단계진행Async(stageCode, memo, cancellationToken);
        return true;
    }

    private async Task 원장절차복원Async(
        CommunityVoteResponse campaign,
        CancellationToken cancellationToken)
    {
        using var request = _선택Context.요청시작(cancellationToken);
        try
        {
            var progress = await _원장절차Client.조회Async(campaign.Id, request.취소Token);
            if (!request.현재요청)
            {
                return;
            }

            if (progress is null)
            {
                원장동기화오류 = "공동구매 원장을 자동 연결하지 못했습니다.";
                return;
            }

            campaign.CommunityLedgerId = progress.CommunityLedgerId;
            공동구매갱신(campaign);
            원장절차적용(progress);
        }
        catch (OperationCanceledException) when (request.취소Token.IsCancellationRequested)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }
        catch (Exception exception)
        {
            if (request.현재요청)
            {
                원장동기화오류 = $"공동구매 원장 절차를 복원하지 못했습니다. {exception.Message}";
            }
        }
    }

    public void Dispose()
    {
        _선택Context.Dispose();
        GC.SuppressFinalize(this);
    }

    private void 원장절차적용(CommunityGroupPurchaseLedgerProgressResponse progress)
    {
        if (!CommunityGroupPurchaseLedgerStageCodes.IsSupported(progress.CurrentStageCode))
        {
            throw new InvalidOperationException($"원장에 알 수 없는 공동구매 절차 단계가 저장돼 있습니다: {progress.CurrentStageCode}");
        }

        원장절차 = progress;
        원장동기화오류 = null;
        진행단계코드 = progress.CurrentStageCode;
        현재단계코드 = progress.CurrentStageCode;
    }

    private void 단계전진검증(string stageCode)
    {
        var target = 공동구매절차카탈로그.찾기(stageCode)
            ?? throw new ArgumentException(
                $"알 수 없는 공동구매 절차 코드입니다: {stageCode}",
                nameof(stageCode));
        var current = 공동구매절차카탈로그.찾기(진행단계코드);
        if (current is not null && target.순서 < current.순서)
        {
            throw new InvalidOperationException(
                $"공동구매 진행 단계는 이전 단계로 되돌릴 수 없습니다: {진행단계코드} -> {stageCode}");
        }
    }

    private static string 진행단계추론(CommunityVoteResponse campaign)
    {
        var explicitRouteCode = campaign.GroupPurchase?.TradeRouteCode;
        if (string.Equals(
                explicitRouteCode,
                CommunityGroupPurchaseTradeRouteCodes.ReviewRequired,
                StringComparison.OrdinalIgnoreCase))
        {
            return 공동구매절차코드.거래경로;
        }

        if (campaign.ResolutionDocument?.Status == CommunityVoteResolutionStatusCodes.Signed)
        {
            return 공동구매절차코드.이행계획;
        }

        if (campaign.ResolutionDocument?.Status is CommunityVoteResolutionStatusCodes.ReadyToSign
            or CommunityVoteResolutionStatusCodes.PartiallySigned)
        {
            return 공동구매절차코드.전자서명;
        }

        if (campaign.ResolutionDocument is not null)
        {
            return 공동구매절차코드.확정안;
        }

        return campaign.Status == CommunityVoteStatusCodes.Open
            ? 공동구매절차코드.수요모집
            : 공동구매절차코드.거래상대연결;
    }

    private void ReplaceCampaign(CommunityVoteResponse campaign)
    {
        var replaced = false;
        var items = 공동구매목록
            .Select(item =>
            {
                if (item.Id != campaign.Id)
                {
                    return item;
                }

                replaced = true;
                return campaign;
            })
            .ToList();

        if (!replaced)
        {
            items.Insert(0, campaign);
        }

        공동구매목록 = items;
    }
}
