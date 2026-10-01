using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.ContractManagement;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed partial class 공동구매모집마감ViewModel(
    I공동구매업무Service service,
    공동구매화면상태ViewModel 화면상태) : 공동구매합의업무ViewModelBase(화면상태)
{
    [ObservableProperty]
    public partial string 운영자표시명 { get; set; } = "공동구매 운영자";

    [ObservableProperty]
    public partial bool 이의검토완료 { get; set; }

    public async Task<bool> 마감Async(CancellationToken cancellationToken = default)
    {
        var campaign = 화면상태.선택된공동구매;
        if (campaign is null)
        {
            return 유효성실패("마감할 공동구매를 선택해 주세요.");
        }

        if (campaign.Status != CommunityVoteStatusCodes.Open)
        {
            return 유효성실패("이미 모집이 마감된 공동구매입니다.");
        }

        if (!이의검토완료)
        {
            return 유효성실패("접수된 이의와 운영 조건을 검토했는지 확인해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(운영자표시명))
        {
            return 유효성실패("모집을 마감하는 운영자 표시명을 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var updated = await service.모집마감Async(
                    campaign.Id,
                    new CommunityVoteCloseRequest
                    {
                        ClosedByDisplayName = 운영자표시명.Trim()
                    },
                    token)
                    ?? throw new InvalidOperationException("공동구매 모집 마감 응답이 비어 있습니다.");

                화면상태.공동구매갱신(updated);
                await 화면상태.단계진행Async(
                    공동구매절차코드.거래상대연결,
                    "수요 모집을 마감하고 거래 상대 연결 단계로 진행했습니다.",
                    token);
                이의검토완료 = false;
            },
            "수요 모집을 마감했습니다. 이제 거래 상대를 연결하고 공급 조건을 협의할 수 있습니다.",
            cancellationToken);
    }
}

public sealed partial class 공동구매결의ViewModel : 공동구매합의업무ViewModelBase, IDisposable
{
    private readonly I공동구매업무Service _service;
    private readonly 공동구매화면상태ViewModel _화면상태;
    private Guid? _편집대상Id;

    public 공동구매결의ViewModel(
        I공동구매업무Service service,
        공동구매화면상태ViewModel 화면상태)
        : base(화면상태)
    {
        _service = service;
        _화면상태 = 화면상태;
        _화면상태.PropertyChanged += 화면상태변경;
        결의문기본값동기화();
    }

    [ObservableProperty]
    public partial string 결의문제목 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string 결의문본문 { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool 법률검토요청 { get; set; } = true;

    [ObservableProperty]
    public partial string 검토자표시명 { get; set; } = "공동구매 운영자";

    [ObservableProperty]
    public partial string 검토메모 { get; set; } = string.Empty;

    public string 결의상태문구 => _화면상태.선택된공동구매?.ResolutionDocument?.Status switch
    {
        CommunityVoteResolutionStatusCodes.LegalReviewRequired => "운영 검토 필요",
        CommunityVoteResolutionStatusCodes.ReadyToSign => "서명 대기",
        CommunityVoteResolutionStatusCodes.PartiallySigned => "일부 서명 완료",
        CommunityVoteResolutionStatusCodes.Signed => "전원 서명 완료",
        _ => "초안"
    };

    public async Task<bool> 결의문작성Async(CancellationToken cancellationToken = default)
    {
        var campaign = _화면상태.선택된공동구매;
        if (campaign is null)
        {
            return 유효성실패("결의문을 작성할 공동구매를 선택해 주세요.");
        }

        if (campaign.Status == CommunityVoteStatusCodes.Open)
        {
            return 유효성실패("수요 모집을 마감한 다음 확정안 결의문을 작성해 주세요.");
        }

        if (campaign.ResolutionDocument is not null)
        {
            return 유효성실패("이미 결의문이 작성된 공동구매입니다.");
        }

        if (string.IsNullOrWhiteSpace(결의문제목) || string.IsNullOrWhiteSpace(결의문본문))
        {
            return 유효성실패("결의문 제목과 확정 내용을 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var document = await _service.결의문생성Async(
                    campaign.Id,
                    new CommunityVoteResolutionDraftRequest
                    {
                        DocumentTitle = 결의문제목.Trim(),
                        ResolutionText = 결의문본문.Trim(),
                        RequiredSigners = [],
                        LegalReviewRequested = 법률검토요청
                    },
                    token)
                    ?? throw new InvalidOperationException("공동구매 결의문 생성 응답이 비어 있습니다.");

                campaign.ResolutionDocument = document;
                campaign.Status = CommunityVoteStatusCodes.ResolutionDrafted;
                _화면상태.공동구매갱신(campaign);
                await _화면상태.단계진행Async(
                    공동구매절차코드.확정안,
                    "공동구매 확정안 결의문을 작성했습니다.",
                    token);
            },
            "현재 참여자를 서명 대상으로 포함한 결의문 초안을 만들었습니다.",
            cancellationToken);
    }

    public async Task<bool> 서명준비Async(CancellationToken cancellationToken = default)
    {
        var campaign = _화면상태.선택된공동구매;
        if (campaign?.ResolutionDocument is null)
        {
            return 유효성실패("검토할 공동구매 결의문이 없습니다.");
        }

        if (campaign.ResolutionDocument.Status is CommunityVoteResolutionStatusCodes.ReadyToSign
            or CommunityVoteResolutionStatusCodes.PartiallySigned
            or CommunityVoteResolutionStatusCodes.Signed)
        {
            return 유효성실패("이미 전자서명 단계로 전환된 결의문입니다.");
        }

        if (string.IsNullOrWhiteSpace(검토자표시명))
        {
            return 유효성실패("결의문을 검토한 운영자 표시명을 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var document = await _service.서명준비Async(
                    campaign.Id,
                    new CommunityVoteResolutionReadyToSignRequest
                    {
                        ReviewedByDisplayName = 검토자표시명.Trim(),
                        ReviewMemo = 검토메모.Trim()
                    },
                    token)
                    ?? throw new InvalidOperationException("공동구매 결의문 검토 완료 응답이 비어 있습니다.");

                campaign.ResolutionDocument = document;
                _화면상태.공동구매갱신(campaign);
                await _화면상태.단계진행Async(
                    공동구매절차코드.전자서명,
                    "결의문 검토를 마치고 전자서명 단계로 진행했습니다.",
                    token);
            },
            "검토를 완료하고 전자서명을 받을 수 있는 상태로 전환했습니다.",
            cancellationToken);
    }

    public void Dispose()
    {
        _화면상태.PropertyChanged -= 화면상태변경;
        GC.SuppressFinalize(this);
    }

    private void 화면상태변경(object? sender, PropertyChangedEventArgs e)
    {
        결의문기본값동기화();
        OnPropertyChanged(nameof(결의상태문구));
    }

    private void 결의문기본값동기화()
    {
        var campaign = _화면상태.선택된공동구매;
        if (campaign?.Id == _편집대상Id)
        {
            return;
        }

        _편집대상Id = campaign?.Id;
        결의문제목 = campaign is null ? string.Empty : $"{campaign.Title} 공동구매 확정안";
        결의문본문 = campaign is null ? string.Empty : 기본결의문본문(campaign);
        검토메모 = string.Empty;
    }

    private static string 기본결의문본문(CommunityVoteResponse campaign)
        => string.Join(
            Environment.NewLine,
            $"'{campaign.Title}' 공동구매 수요 모집 결과를 다음과 같이 확정합니다.",
            $"참여자: {campaign.TotalVoteCount}명",
            $"총 요청 수량: {campaign.GroupPurchase?.TotalRequestedQuantity ?? 0}{campaign.GroupPurchase?.QuantityUnit}",
            $"수령 범위: {campaign.GroupPurchase?.ServiceAreaLabel}",
            string.IsNullOrWhiteSpace(campaign.GroupPurchase?.ProposalOriginLegalEffectNotice)
                ? CommunityGroupPurchaseAgreementPolicy.FullLegalEffectNotice
                : campaign.GroupPurchase!.ProposalOriginLegalEffectNotice,
            "접수된 이의와 운영 조건을 검토했으며, 필수 구성원 전자서명 완료 후 구매와 물류 절차를 시작합니다.");
}

public sealed record 공동구매전자서명입력(
    string 서명자이름,
    string 서명증빙Payload,
    string? 접속IpHash = null);

public sealed partial class 공동구매전자서명ViewModel : 공동구매합의업무ViewModelBase, IDisposable
{
    private readonly I공동구매업무Service _service;
    private readonly 공동구매화면상태ViewModel _화면상태;
    private Guid? _선택대상공동구매Id;

    public 공동구매전자서명ViewModel(
        I공동구매업무Service service,
        공동구매화면상태ViewModel 화면상태)
        : base(화면상태)
    {
        _service = service;
        _화면상태 = 화면상태;
        _화면상태.PropertyChanged += 화면상태변경;
    }

    [ObservableProperty]
    public partial string 선택된서명자PartyId { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string 선택된서명자표시명 { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool 결의문동의 { get; set; }

    [ObservableProperty]
    public partial string 동의문 { get; set; } = "공동구매 결의문을 확인했으며 확정안에 동의합니다.";

    public IReadOnlyList<ContractSignatureRequest> 미서명자
    {
        get
        {
            var plan = _화면상태.선택된공동구매?.ResolutionDocument?.SignaturePlan;
            if (plan is null)
            {
                return [];
            }

            var missing = plan.MissingRequiredPartyIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return plan.Bundle.SignatureRequests
                .Where(request => missing.Contains(request.PartyId))
                .ToArray();
        }
    }

    public string 서명진행률
    {
        get
        {
            var plan = _화면상태.선택된공동구매?.ResolutionDocument?.SignaturePlan;
            return plan is null ? "준비 전" : $"{plan.SignedRequiredSignerCount}/{plan.RequiredSignerCount}";
        }
    }

    public bool 전원서명완료
        => _화면상태.선택된공동구매?.ResolutionDocument?.SignaturePlan?.IsFullySigned == true;

    public bool 서명자선택(string partyId)
    {
        var signer = 미서명자.FirstOrDefault(request =>
            string.Equals(request.PartyId, partyId, StringComparison.OrdinalIgnoreCase));
        if (signer is null)
        {
            return 유효성실패("서명이 필요한 구성원을 선택해 주세요.");
        }

        선택된서명자PartyId = signer.PartyId;
        선택된서명자표시명 = signer.SignerDisplayName;
        return true;
    }

    public async Task<bool> 서명제출Async(
        공동구매전자서명입력 signature,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signature);

        var campaign = _화면상태.선택된공동구매;
        if (campaign?.ResolutionDocument is null
            || string.IsNullOrWhiteSpace(선택된서명자PartyId)
            || !결의문동의)
        {
            return 유효성실패("서명 요청을 선택하고 결의문 동의 여부를 확인해 주세요.");
        }

        if (campaign.ResolutionDocument.Status is not (
            CommunityVoteResolutionStatusCodes.ReadyToSign
            or CommunityVoteResolutionStatusCodes.PartiallySigned))
        {
            return 유효성실패("현재 결의문은 전자서명을 받을 수 있는 상태가 아닙니다.");
        }

        if (string.IsNullOrWhiteSpace(signature.서명자이름)
            || string.IsNullOrWhiteSpace(signature.서명증빙Payload)
            || string.IsNullOrWhiteSpace(동의문))
        {
            return 유효성실패("서명자 이름, 동의문과 전자서명 증적이 필요합니다.");
        }

        return await 작업실행Async(
            async token =>
            {
                var document = await _service.전자서명Async(
                    campaign.Id,
                    new CommunityVoteResolutionSignRequest
                    {
                        PartyId = 선택된서명자PartyId,
                        SignerDisplayName = signature.서명자이름.Trim(),
                        SignatureMethodCode = ContractSignatureMethodCode.PlatformClickSign,
                        ConsentText = 동의문.Trim(),
                        SignatureEvidencePayload = signature.서명증빙Payload,
                        ClientIpHash = signature.접속IpHash
                    },
                    token)
                    ?? throw new InvalidOperationException("공동구매 전자서명 응답이 비어 있습니다.");

                campaign.ResolutionDocument = document;
                _화면상태.공동구매갱신(campaign);
                await _화면상태.단계진행Async(
                    document.Status == CommunityVoteResolutionStatusCodes.Signed
                        ? 공동구매절차코드.이행계획
                        : 공동구매절차코드.전자서명,
                    document.Status == CommunityVoteResolutionStatusCodes.Signed
                        ? "필수 전자서명이 완료되어 이행 계획 단계로 진행했습니다."
                        : "공동구매 결의문에 전자서명 증적을 추가했습니다.",
                    token);
                입력초기화();
            },
            "전자서명이 기록됐습니다.",
            cancellationToken);
    }

    public void Dispose()
    {
        _화면상태.PropertyChanged -= 화면상태변경;
        GC.SuppressFinalize(this);
    }

    private void 화면상태변경(object? sender, PropertyChangedEventArgs e)
    {
        var campaignId = _화면상태.선택된공동구매Id;
        if (_선택대상공동구매Id != campaignId
            || (!string.IsNullOrWhiteSpace(선택된서명자PartyId)
                && 미서명자.All(request => !string.Equals(
                    request.PartyId,
                    선택된서명자PartyId,
                    StringComparison.OrdinalIgnoreCase))))
        {
            _선택대상공동구매Id = campaignId;
            입력초기화();
        }

        OnPropertyChanged(nameof(미서명자));
        OnPropertyChanged(nameof(서명진행률));
        OnPropertyChanged(nameof(전원서명완료));
    }

    private void 입력초기화()
    {
        선택된서명자PartyId = string.Empty;
        선택된서명자표시명 = string.Empty;
        결의문동의 = false;
    }
}

/// <summary>
/// 서버 상태와 실제 진행 단계를 사용자에게 보여 줄 업무 절차로 투영합니다.
/// 화면에서 단계를 선택해도 진행 단계는 바뀌지 않습니다.
/// </summary>
public sealed class 공동구매절차상태ViewModel : ObservableObject, IDisposable
{
    private readonly 공동구매화면상태ViewModel _화면상태;

    public 공동구매절차상태ViewModel(공동구매화면상태ViewModel 화면상태)
    {
        _화면상태 = 화면상태;
        _화면상태.PropertyChanged += 화면상태변경;
    }

    public IReadOnlyList<공동구매절차단계표시> 단계목록
        => 공동구매절차카탈로그.전체
            .Select(stage => new 공동구매절차단계표시(
                stage.순서,
                stage.코드,
                stage.제목,
                stage.설명,
                단계상태(stage.코드),
                string.Equals(stage.코드, _화면상태.현재단계코드, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

    public 공동구매절차단계표시? 현재단계
        => 단계목록.FirstOrDefault(stage => stage.선택됨);

    public bool 실행준비완료
        => _화면상태.선택된공동구매?.ResolutionDocument?.Status
            == CommunityVoteResolutionStatusCodes.Signed;

    public int 수요전달실패수
        => _화면상태.선택된공동구매?.GroupPurchase?.DemandHandoffFailedCount ?? 0;

    public string 실행안내
    {
        get
        {
            if (!실행준비완료)
            {
                return "필수 구성원의 전자서명이 완료되면 공동구매 실행 단계가 열립니다.";
            }

            return 수요전달실패수 > 0
                ? "일부 수요 전달이 실패했습니다. 운영자가 전달 상태를 확인해야 합니다."
                : "확정된 수요가 공동구매 자동 집단화와 후속 물류 절차로 전달됐습니다.";
        }
    }

    public void 단계선택(string stageCode) => _화면상태.단계선택(stageCode);

    public 공동구매절차단계상태 단계상태(string stageCode)
    {
        var campaign = _화면상태.선택된공동구매;
        if (campaign is null)
        {
            return 공동구매절차단계상태.대기;
        }

        var target = 공동구매절차카탈로그.찾기(stageCode)
            ?? throw new ArgumentException(
                $"알 수 없는 공동구매 절차 코드입니다: {stageCode}",
                nameof(stageCode));
        var progress = 공동구매절차카탈로그.찾기(_화면상태.진행단계코드)
            ?? 공동구매절차카탈로그.전체[0];

        if (stageCode == 공동구매절차코드.제안)
        {
            return 공동구매절차단계상태.완료;
        }

        return target.순서 < progress.순서
            ? 공동구매절차단계상태.완료
            : target.순서 == progress.순서
                ? 공동구매절차단계상태.진행중
                : 공동구매절차단계상태.대기;
    }

    public void Dispose()
    {
        _화면상태.PropertyChanged -= 화면상태변경;
        GC.SuppressFinalize(this);
    }

    private void 화면상태변경(object? sender, PropertyChangedEventArgs e)
        => OnPropertyChanged(string.Empty);
}

public sealed class 공동구매합의기능ViewModel : 조립ViewModelBase
{
    public 공동구매합의기능ViewModel(
        공동구매모집마감ViewModel 모집마감,
        공동구매결의ViewModel 결의,
        공동구매전자서명ViewModel 전자서명,
        공동구매절차상태ViewModel 절차상태,
        공동구매모집마감처리ViewModel 모집마감처리,
        공동구매결의문등록ViewModel 결의문등록,
        공동구매결의서명준비ViewModel 결의서명준비,
        공동구매전자서명등록ViewModel 전자서명등록)
    {
        this.모집마감 = 하위ViewModel등록(모집마감, 수명소유: false);
        this.결의 = 하위ViewModel등록(결의, 수명소유: false);
        this.전자서명 = 하위ViewModel등록(전자서명, 수명소유: false);
        this.절차상태 = 하위ViewModel등록(절차상태);
        세부업무목록 =
        [
            하위ViewModel등록(모집마감처리, 수명소유: false),
            하위ViewModel등록(결의문등록, 수명소유: false),
            하위ViewModel등록(결의서명준비, 수명소유: false),
            하위ViewModel등록(전자서명등록, 수명소유: false)
        ];
    }

    public 공동구매모집마감ViewModel 모집마감 { get; }
    public 공동구매결의ViewModel 결의 { get; }
    public 공동구매전자서명ViewModel 전자서명 { get; }
    public 공동구매절차상태ViewModel 절차상태 { get; }
    public IReadOnlyList<I업무조각ViewModel> 세부업무목록 { get; }

    public bool 처리중 => 모집마감.처리중 || 결의.처리중 || 전자서명.처리중;
}
