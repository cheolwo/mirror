using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

/// <summary>
/// 공동구매 수요를 상품·배송권 기준 자동집단으로 묶는 API를 담당합니다.
/// </summary>
public sealed partial class 공동구매자동집단ViewModel : 공동구매실행업무ViewModelBase, IDisposable
{
    private readonly I공동구매실행Service _service;
    private readonly 공동구매화면상태ViewModel _화면상태;
    private readonly 공동구매실행상태ViewModel _실행상태;
    private readonly 공동구매창고상태ViewModel _창고상태;
    private Guid? _대상공동구매Id;

    public 공동구매자동집단ViewModel(
        I공동구매실행Service service,
        공동구매화면상태ViewModel 화면상태,
        공동구매실행상태ViewModel 실행상태,
        공동구매창고상태ViewModel 창고상태)
        : base(화면상태)
    {
        _service = service;
        _화면상태 = 화면상태;
        _실행상태 = 실행상태;
        _창고상태 = 창고상태;
        _화면상태.PropertyChanged += 화면상태변경;
        _실행상태.PropertyChanged += 실행상태변경;
        _창고상태.PropertyChanged += 창고상태변경;
        공동구매변경동기화();
    }

    [ObservableProperty]
    public partial 공동구매자동집단조회조건 조회조건 { get; private set; } = new();

    [ObservableProperty]
    public partial IReadOnlyList<공동구매자동집단응답> 자동집단목록 { get; private set; } = [];

    [ObservableProperty]
    public partial 공동구매자동수요등록Command 수요초안 { get; private set; } = new();

    public 공동구매자동집단응답? 선택된자동집단 => _실행상태.선택된자동집단;
    public string? 실행공동구매Id => _실행상태.실행공동구매Id;
    public string? 공동구매주문집계원장Id => _실행상태.공동구매주문집계원장Id;
    public string? 내개별주문원장Id => _실행상태.선택된주문원장Id;
    public 창고요약응답? 선택된도착창고 => _창고상태.선택된창고;
    public bool 가상창고사용여부
        => string.Equals(수요초안.도착창고유형, 창고유형코드.가상창고, StringComparison.OrdinalIgnoreCase)
           || (수요초안.도착창고Id is null && !string.IsNullOrWhiteSpace(수요초안.수령도로명주소));

    public async Task<bool> 목록조회Async(CancellationToken cancellationToken = default)
        => await 작업실행Async(
            async token =>
            {
                자동집단목록 = await _service.자동집단목록조회Async(조회조건, token);
                var selectedId = _실행상태.선택된자동집단?.자동집단Id;
                if (!string.IsNullOrWhiteSpace(selectedId))
                {
                    var refreshed = 자동집단목록.FirstOrDefault(item =>
                        string.Equals(item.자동집단Id, selectedId, StringComparison.Ordinal));
                    if (refreshed is not null)
                    {
                        _실행상태.자동집단적용(refreshed);
                    }
                }
            },
            "자동집단 목록을 불러왔습니다.",
            cancellationToken,
            ex => $"자동집단 목록을 불러오지 못했습니다. {ex.Message}");

    public bool 자동집단선택(string automaticGroupId)
    {
        var group = 자동집단목록.FirstOrDefault(item =>
            string.Equals(item.자동집단Id, automaticGroupId, StringComparison.Ordinal));
        if (group is null)
        {
            return 유효성실패("선택할 자동집단을 목록에서 찾아 주세요.");
        }

        _실행상태.자동집단적용(group);
        return true;
    }

    public async Task<bool> 수요등록Async(CancellationToken cancellationToken = default)
    {
        var campaign = _화면상태.선택된공동구매;
        if (campaign is null)
        {
            return 유효성실패("자동집단에 연결할 공동구매를 선택해 주세요.");
        }

        if (현재사용자.인증됨)
        {
            수요초안.주문자키 = 현재사용자.UserId!;
            if (string.IsNullOrWhiteSpace(수요초안.주문자표시명))
            {
                수요초안.주문자표시명 = 현재사용자.UserName ?? 현재사용자.UserId!;
            }

            OnPropertyChanged(nameof(수요초안));
        }

        if (string.IsNullOrWhiteSpace(수요초안.주문자키)
            || string.IsNullOrWhiteSpace(수요초안.주문자표시명))
        {
            return 유효성실패("로그인 사용자와 주문자 표시명을 확인해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(수요초안.상품키)
            || string.IsNullOrWhiteSpace(수요초안.상품명)
            || string.IsNullOrWhiteSpace(수요초안.배송권키))
        {
            return 유효성실패("상품과 배송권 정보를 확인해 주세요.");
        }

        if (수요초안.희망수량 <= 0 || string.IsNullOrWhiteSpace(수요초안.수량단위))
        {
            return 유효성실패("0보다 큰 희망 수량과 수량 단위를 입력해 주세요.");
        }

        if (주문확정수요인가(수요초안)
            && 수요초안.도착창고Id is not > 0
            && string.IsNullOrWhiteSpace(수요초안.수령도로명주소))
        {
            return 유효성실패("예약 결제 주문은 보유 창고를 선택하거나 자택 등 수령 주소를 입력해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(수요초안.수요출처키))
        {
            수요초안.수요출처키 = $"community-vote:{campaign.Id:N}:{수요초안.주문자키.Trim()}";
            OnPropertyChanged(nameof(수요초안));
        }

        return await 작업실행Async(
            async token =>
            {
                var group = await _service.자동수요등록Async(수요초안, token)
                    ?? throw new InvalidOperationException("자동집단 수요 등록 응답이 비어 있습니다.");
                자동집단목록 = 자동집단목록
                    .Where(item => !string.Equals(item.자동집단Id, group.자동집단Id, StringComparison.Ordinal))
                    .Prepend(group)
                    .ToArray();
                _실행상태.자동집단적용(group);
                var registeredDemand = group.수요목록.FirstOrDefault(item =>
                    string.Equals(item.수요출처키, 수요초안.수요출처키, StringComparison.Ordinal))
                    ?? group.수요목록.FirstOrDefault(item =>
                        string.Equals(item.주문자키, 수요초안.주문자키, StringComparison.Ordinal));
                if (registeredDemand is not null)
                {
                    _실행상태.주문집계선택(registeredDemand.공동구매주문집계원장Id);
                    if (!string.IsNullOrWhiteSpace(registeredDemand.개별주문원장Id))
                    {
                        _실행상태.주문원장선택(registeredDemand.개별주문원장Id);
                    }
                }
            },
            "수요를 공동구매에 등록하고 주문집계와 내 개별 주문을 연결했습니다.",
            cancellationToken);
    }

    public void 입력변경알림()
    {
        OnPropertyChanged(nameof(수요초안));
        OnPropertyChanged(nameof(가상창고사용여부));
    }

    public bool 도착창고선택(long warehouseId)
    {
        if (!_창고상태.창고선택(warehouseId) || _창고상태.선택된창고 is null)
        {
            return 유효성실패("선택할 도착 창고를 목록에서 찾아 주세요.");
        }

        선택창고초안적용(_창고상태.선택된창고);
        return true;
    }

    public bool 자택가상창고사용(string roadAddress, string? detailAddress = null, string? receivingLabel = null)
    {
        if (string.IsNullOrWhiteSpace(roadAddress))
        {
            return 유효성실패("가상 창고로 사용할 자택 또는 수령 도로명주소를 입력해 주세요.");
        }

        수요초안.도착창고Id = null;
        수요초안.도착창고유형 = 창고유형코드.가상창고;
        수요초안.도착창고명 = string.IsNullOrWhiteSpace(receivingLabel) ? "자택 수령지 가상 창고" : receivingLabel.Trim();
        수요초안.수령지주소참조키 = string.Empty;
        수요초안.수령지표시명 = string.IsNullOrWhiteSpace(receivingLabel) ? "자택 수령지" : receivingLabel.Trim();
        수요초안.수령도로명주소 = roadAddress.Trim();
        수요초안.수령상세주소 = detailAddress?.Trim() ?? string.Empty;
        입력변경알림();
        return true;
    }

    public void Dispose()
    {
        _화면상태.PropertyChanged -= 화면상태변경;
        _실행상태.PropertyChanged -= 실행상태변경;
        _창고상태.PropertyChanged -= 창고상태변경;
        GC.SuppressFinalize(this);
    }

    private void 화면상태변경(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)
            || e.PropertyName is nameof(공동구매화면상태ViewModel.선택된공동구매)
                or nameof(공동구매화면상태ViewModel.선택된공동구매Id))
        {
            공동구매변경동기화();
        }
    }

    private void 실행상태변경(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)
            || e.PropertyName is nameof(공동구매실행상태ViewModel.선택된자동집단)
                or nameof(공동구매실행상태ViewModel.실행공동구매Id)
                or nameof(공동구매실행상태ViewModel.공동구매주문집계원장Id)
                or nameof(공동구매실행상태ViewModel.선택된주문원장Id))
        {
            OnPropertyChanged(nameof(선택된자동집단));
            OnPropertyChanged(nameof(실행공동구매Id));
            OnPropertyChanged(nameof(공동구매주문집계원장Id));
            OnPropertyChanged(nameof(내개별주문원장Id));
        }
    }

    private void 창고상태변경(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)
            || e.PropertyName == nameof(공동구매창고상태ViewModel.선택된창고))
        {
            OnPropertyChanged(nameof(선택된도착창고));
            if (_창고상태.선택된창고 is not null)
            {
                선택창고초안적용(_창고상태.선택된창고);
            }
        }
    }

    private void 공동구매변경동기화()
    {
        var campaign = _화면상태.선택된공동구매;
        if (_대상공동구매Id == campaign?.Id)
        {
            return;
        }

        _대상공동구매Id = campaign?.Id;
        자동집단목록 = [];
        수요초안 = campaign is null ? new() : 수요초안생성(campaign, _창고상태.선택된창고);
        조회조건 = new 공동구매자동집단조회조건
        {
            상품키 = string.IsNullOrWhiteSpace(수요초안.상품키) ? null : 수요초안.상품키,
            배송권키 = string.IsNullOrWhiteSpace(수요초안.배송권키) ? null : 수요초안.배송권키
        };
        작업상태초기화();
    }

    private static 공동구매자동수요등록Command 수요초안생성(
        CommunityVoteResponse campaign,
        창고요약응답? warehouse)
    {
        var option = campaign.Options.FirstOrDefault(item => item.IsWinningOption)
            ?? campaign.Options.FirstOrDefault();
        var groupPurchase = campaign.GroupPurchase;
        // 자동집단 키는 여러 수요가 공유하는 카탈로그 키여야 하므로 옵션 ID를 임의 대체값으로 쓰지 않습니다.
        var productKey = First(option?.ProductKey);
        var productName = First(option?.Text, campaign.Title, productKey);
        var deliveryKey = First(groupPurchase?.ServiceAreaKey);
        return new 공동구매자동수요등록Command
        {
            커뮤니티게시글Id = campaign.SourcePostId,
            커뮤니티원장Id = campaign.CommunityLedgerId ?? string.Empty,
            상품키 = productKey,
            상품명 = productName,
            HS코드 = First(option?.HsCode, groupPurchase?.HsCode),
            온도코드 = First(option?.TemperatureCode, groupPurchase?.TemperatureCode, "상온"),
            물류방식 = First(option?.LogisticsMode, groupPurchase?.LogisticsMode, "LCL"),
            배송권키 = deliveryKey,
            배송권명 = First(groupPurchase?.ServiceAreaLabel, deliveryKey),
            도착창고Id = warehouse?.Id,
            도착창고유형 = warehouse?.창고유형 ?? string.Empty,
            도착창고명 = warehouse?.창고명 ?? string.Empty,
            수령지주소참조키 = warehouse is null ? string.Empty : $"warehouse:{warehouse.Id}:receiving-address",
            수령지표시명 = warehouse?.창고명 ?? string.Empty,
            수령도로명주소 = warehouse?.주소 ?? string.Empty,
            // 응답의 RequestedQuantity는 전체 참여자 합계이므로 개인 수요 초안에 복사하지 않습니다.
            희망수량 = 1,
            수량단위 = First(option?.QuantityUnit, groupPurchase?.QuantityUnit, "개"),
            수요유형 = 공동구매자동수요유형코드.관심표시,
            결제상태 = 공동구매자동결제상태코드.미결제,
            메모 = $"'{campaign.Title}' 공동구매 확정 흐름에서 전달한 수요입니다.",
            목표참여자수 = groupPurchase?.MinimumParticipantCount > 0
                ? groupPurchase.MinimumParticipantCount
                : null,
            목표수량 = groupPurchase?.MinimumTotalQuantity > 0
                ? groupPurchase.MinimumTotalQuantity
                : null
        };
    }

    private void 선택창고초안적용(창고요약응답 warehouse)
    {
        수요초안.도착창고Id = warehouse.Id;
        수요초안.도착창고유형 = warehouse.창고유형;
        수요초안.도착창고명 = warehouse.창고명;
        수요초안.수령지주소참조키 = $"warehouse:{warehouse.Id}:receiving-address";
        수요초안.수령지표시명 = warehouse.창고명;
        수요초안.수령도로명주소 = warehouse.주소;
        수요초안.수령상세주소 = string.Empty;
        입력변경알림();
    }

    private static bool 주문확정수요인가(공동구매자동수요등록Command command)
        => command.수요유형 == 공동구매자동수요유형코드.예약결제
           || command.결제상태 is 공동구매자동결제상태코드.예약됨
               or 공동구매자동결제상태코드.결제확정;

    private static string First(params string?[] candidates)
        => candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
