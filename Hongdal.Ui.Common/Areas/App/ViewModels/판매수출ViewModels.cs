using System.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Sales;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public enum 판매실행단계상태
{
    대기,
    진행중,
    완료
}

public sealed record 판매실행단계ViewModel(
    string 코드,
    string 표시명,
    판매실행단계상태 상태,
    string 안내);

/// <summary>
/// 국내 판매채널 계정, 판매상품과 출품을 한 화면 흐름으로 관리합니다.
/// 공동구매에서 확보한 상품을 실제 국내 판매로 넘기는 하위 ViewModel입니다.
/// </summary>
public sealed class 국내판매ViewModel : 공동구매판매실행업무ViewModelBase, IDisposable
{
    private readonly I판매채널Client _client;
    private readonly 판매ViewModel _기본판매;
    private readonly 공동구매화면상태ViewModel _화면상태;
    private readonly 공동구매거래경로분기ViewModel _분기;
    private 공동구매실행기능ViewModel? _실행;
    private Guid? _대상공동구매Id;

    public 국내판매ViewModel(
        I판매채널Client client,
        판매ViewModel 기본판매,
        공동구매화면상태ViewModel 화면상태,
        공동구매거래경로분기ViewModel 분기)
        : base(화면상태, CommunityGroupPurchaseTradeRouteCodes.Domestic)
    {
        _client = client;
        _기본판매 = 기본판매;
        _화면상태 = 화면상태;
        _분기 = 분기;
        _기본판매.상태.PropertyChanged += 판매상태변경;
        _화면상태.PropertyChanged += 화면상태변경;
        _분기.PropertyChanged += 분기변경;
        공동구매변경동기화();
    }

    public override bool 활성 => _분기.국내판매활성;
    public override IReadOnlyList<string> 지원채널목록 => CommerceChannelOrderSyncScopes.DomesticChannelTypes;
    public 판매ViewModel 기본판매 => _기본판매;

    public IReadOnlyList<판매채널계정항목응답> 계정목록
        => _기본판매.상태.계정목록
            .Where(item => 지원채널목록.Contains(item.채널종류, StringComparer.OrdinalIgnoreCase))
            .ToArray();

    public IReadOnlyList<판매상품항목응답> 상품목록 => _기본판매.상태.상품목록;

    public IReadOnlyList<채널출품항목응답> 출품목록
    {
        get
        {
            var accountIds = 계정목록.Select(account => account.Id).ToHashSet();
            return _기본판매.상태.출품목록
                .Where(listing => accountIds.Contains(listing.판매채널계정Id))
                .ToArray();
        }
    }

    public 판매채널계정저장요청 계정초안 { get; private set; } = 새국내계정초안();
    public 판매상품저장요청 상품초안 { get; private set; } = new();
    public 채널출품저장요청 출품초안 { get; private set; } = new();
    public bool 입고재고연결됨 => 상품초안.입고상품Id > 0;
    public bool 판매채널선택됨 => 출품초안.판매채널계정Id > 0;
    public bool 판매상품선택됨 => 출품초안.판매상품Id > 0;
    public bool 출품완료 => 출품목록.Any(listing =>
        listing.판매상품Id == 출품초안.판매상품Id
        && listing.판매채널계정Id == 출품초안.판매채널계정Id);
    public IReadOnlyList<판매실행단계ViewModel> 진행단계 => 단계생성();
    public string 다음작업안내
        => !활성
            ? "국내 도착 거래에서 국내 판매 흐름을 사용할 수 있습니다."
            : !입고재고연결됨
                ? "판매할 입고 재고를 선택해 주세요."
                : !판매채널선택됨
                    ? "국내 판매채널 계정을 연결하거나 선택해 주세요."
                    : !판매상품선택됨
                        ? "입고 재고를 판매상품으로 등록해 주세요."
                        : !출품완료
                            ? "판매상품을 선택한 국내 채널에 출품해 주세요."
                            : "국내 판매 출품이 준비되었습니다. 채널 주문과 출고 상태를 확인해 주세요.";

    public void 실행연결(공동구매실행기능ViewModel 실행)
    {
        ArgumentNullException.ThrowIfNull(실행);
        if (ReferenceEquals(_실행, 실행))
        {
            return;
        }

        if (_실행 is not null)
        {
            _실행.PropertyChanged -= 실행변경;
        }

        _실행 = 실행;
        _실행.PropertyChanged += 실행변경;
        OnPropertyChanged(string.Empty);
    }

    public bool 입고재고선택(long inboundProductId)
    {
        if (_실행 is null)
        {
            return 유효성실패("공동구매 실행·창고 ViewModel을 먼저 연결해 주세요.");
        }

        var inventory = _실행.창고.상태.재고목록.FirstOrDefault(item => item.입고상품Id == inboundProductId);
        if (inventory is null)
        {
            return 유효성실패("판매할 입고 재고를 목록에서 찾아 주세요.");
        }

        if (_실행.창고.상태.창고목록.Any(warehouse => warehouse.Id == inventory.창고Id))
        {
            _실행.창고.상태.창고선택(inventory.창고Id);
        }

        _실행.창고.상태.재고선택(inboundProductId);
        상품초안.입고상품Id = inventory.입고상품Id;
        상품초안.대표상품명 = string.IsNullOrWhiteSpace(inventory.상품명)
            ? 상품초안.대표상품명
            : inventory.상품명;
        if (string.IsNullOrWhiteSpace(상품초안.판매SKU))
        {
            상품초안.판매SKU = inventory.SKU;
        }

        입력변경알림();
        return true;
    }

    public bool 계정선택(long accountId)
    {
        var account = 계정목록.FirstOrDefault(item => item.Id == accountId);
        if (account is null)
        {
            return 유효성실패("국내 판매채널 계정을 목록에서 선택해 주세요.");
        }

        출품초안.판매채널계정Id = account.Id;
        입력변경알림();
        return true;
    }

    public bool 상품선택(long salesProductId)
    {
        var product = 상품목록.FirstOrDefault(item => item.Id == salesProductId);
        if (product is null)
        {
            return 유효성실패("국내 판매상품을 목록에서 선택해 주세요.");
        }

        출품초안.판매상품Id = product.Id;
        입력변경알림();
        return true;
    }

    public void 입력변경알림() => OnPropertyChanged(string.Empty);

    public async Task<bool> 목록조회Async(CancellationToken cancellationToken = default)
        => await 작업실행Async(
            async token =>
            {
                var accounts = await _client.계정목록조회Async(token);
                var products = await _client.상품목록조회Async(token);
                var listings = await _client.출품목록조회Async(token);
                _기본판매.상태.목록적용(accounts, products, listings);
            },
            "국내 판매채널, 상품과 출품 상태를 조회했습니다.",
            cancellationToken);

    public async Task<bool> 계정생성Async(CancellationToken cancellationToken = default)
    {
        if (!활성)
        {
            return 유효성실패("국내 판매 분기가 활성화된 경우에만 국내 판매채널 계정을 연결할 수 있습니다.");
        }

        if (!CommerceChannelOrderSyncScopes.DomesticChannelTypes.Contains(
                계정초안.채널종류,
                StringComparer.OrdinalIgnoreCase))
        {
            return 유효성실패("국내 판매 계정은 스마트스토어, 쿠팡 또는 11번가 채널을 선택해 주세요.");
        }
        if (string.IsNullOrWhiteSpace(계정초안.상점명))
        {
            return 유효성실패("국내 판매채널 상점명을 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var created = await _client.계정생성Async(계정초안, token)
                    ?? throw new InvalidOperationException("판매채널 계정 생성 응답이 비어 있습니다.");
                _기본판매.상태.계정저장적용(created);
                출품초안.판매채널계정Id = created.Id;
                계정초안 = 새국내계정초안();
                OnPropertyChanged(string.Empty);
            },
            "국내 판매채널 계정을 연결했습니다.",
            cancellationToken);
    }

    public async Task<bool> 상품생성Async(CancellationToken cancellationToken = default)
    {
        if (!활성)
        {
            return 유효성실패("국내 판매 분기가 활성화된 경우에만 판매상품을 만들 수 있습니다.");
        }

        if (상품초안.입고상품Id <= 0)
        {
            return 유효성실패("국내 판매상품은 입고 완료된 상품 ID와 연결해야 합니다.");
        }
        if (string.IsNullOrWhiteSpace(상품초안.대표상품명)
            || string.IsNullOrWhiteSpace(상품초안.판매SKU)
            || 상품초안.판매가 <= 0)
        {
            return 유효성실패("대표상품명, 판매 SKU와 0원보다 큰 판매가를 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var created = await _client.상품생성Async(상품초안, token)
                    ?? throw new InvalidOperationException("판매상품 생성 응답이 비어 있습니다.");
                _기본판매.상태.상품저장적용(created);
                출품초안.판매상품Id = created.Id;
                상품초안 = new 판매상품저장요청
                {
                    대표상품명 = created.대표상품명,
                    판매SKU = created.판매SKU,
                    입고상품Id = created.입고상품Id,
                    판매가 = created.판매가
                };
                OnPropertyChanged(string.Empty);
            },
            "국내 판매상품을 등록했습니다.",
            cancellationToken);
    }

    public async Task<bool> 출품생성Async(CancellationToken cancellationToken = default)
    {
        if (!활성)
        {
            return 유효성실패("국내 판매 분기가 활성화된 경우에만 국내 판매채널에 출품할 수 있습니다.");
        }

        if (출품초안.판매상품Id <= 0 || 출품초안.판매채널계정Id <= 0)
        {
            return 유효성실패("출품할 판매상품과 국내 판매채널 계정을 선택해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var created = await _client.출품생성Async(출품초안, token)
                    ?? throw new InvalidOperationException("국내 판매채널 출품 응답이 비어 있습니다.");
                _기본판매.상태.출품저장적용(created);
                OnPropertyChanged(string.Empty);
            },
            "국내 판매채널 출품을 생성했습니다.",
            cancellationToken);
    }

    public void Dispose()
    {
        if (_실행 is not null)
        {
            _실행.PropertyChanged -= 실행변경;
        }

        _화면상태.PropertyChanged -= 화면상태변경;
        _분기.PropertyChanged -= 분기변경;
        _기본판매.상태.PropertyChanged -= 판매상태변경;
        GC.SuppressFinalize(this);
    }

    private void 화면상태변경(object? sender, PropertyChangedEventArgs e)
        => 공동구매변경동기화();

    private void 분기변경(object? sender, PropertyChangedEventArgs e)
        => OnPropertyChanged(string.Empty);

    private void 실행변경(object? sender, PropertyChangedEventArgs e)
        => OnPropertyChanged(string.Empty);

    private void 판매상태변경(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(계정목록));
        OnPropertyChanged(nameof(상품목록));
        OnPropertyChanged(nameof(출품목록));
        OnPropertyChanged(nameof(출품완료));
        OnPropertyChanged(nameof(진행단계));
        OnPropertyChanged(nameof(다음작업안내));
    }

    private void 공동구매변경동기화()
    {
        var campaign = _화면상태.선택된공동구매;
        if (_대상공동구매Id == campaign?.Id)
        {
            return;
        }

        _대상공동구매Id = campaign?.Id;
        계정초안 = 새국내계정초안();
        상품초안 = new 판매상품저장요청
        {
            대표상품명 = campaign?.Title ?? string.Empty,
            판매SKU = campaign is null ? string.Empty : $"GP-{campaign.Id:N}"[..11].ToUpperInvariant()
        };
        출품초안 = new 채널출품저장요청();
        OnPropertyChanged(nameof(활성));
        OnPropertyChanged(nameof(계정초안));
        OnPropertyChanged(nameof(상품초안));
        OnPropertyChanged(nameof(출품초안));
    }

    private IReadOnlyList<판매실행단계ViewModel> 단계생성()
    {
        var completed = new[] { 입고재고연결됨, 판매채널선택됨, 판매상품선택됨, 출품완료 };
        var labels = new[]
        {
            ("inventory", "입고 재고", "판매할 재고와 입고상품을 연결합니다."),
            ("channel", "국내 채널", "스마트스토어·쿠팡·11번가 계정을 연결합니다."),
            ("product", "판매상품", "입고상품을 판매 SKU와 가격으로 등록합니다."),
            ("listing", "채널 출품", "판매상품을 선택한 국내 채널에 출품합니다.")
        };
        var firstPending = Array.FindIndex(completed, value => !value);
        return labels.Select((stage, index) => new 판매실행단계ViewModel(
            stage.Item1,
            stage.Item2,
            completed[index]
                ? 판매실행단계상태.완료
                : index == firstPending && 활성
                    ? 판매실행단계상태.진행중
                    : 판매실행단계상태.대기,
            stage.Item3)).ToArray();
    }

    private static 판매채널계정저장요청 새국내계정초안()
        => new() { 채널종류 = CommerceChannelKeys.SmartStore };
}

/// <summary>
/// 국내 출발·해외 도착 거래에서 판매상품, 해외 채널 출품,
/// 수출신고와 국제물류 준비 상태를 함께 관리합니다.
/// </summary>
public sealed class 해외수출ViewModel : 공동구매판매실행업무ViewModelBase, IDisposable
{
    private readonly I판매채널Client _client;
    private readonly 판매ViewModel _기본판매;
    private readonly 공동구매화면상태ViewModel _화면상태;
    private readonly 공동구매거래경로분기ViewModel _분기;
    private 공동구매실행기능ViewModel? _실행;
    private Guid? _대상공동구매Id;
    private AmazonExportReadinessDraftRequest _초안 = new();
    private AmazonExportReadinessResult _계획 = new();
    private 판매채널계정저장요청 _Amazon계정초안 = 새Amazon계정초안();
    private 판매상품저장요청 _수출상품초안 = new();

    public 해외수출ViewModel(
        I판매채널Client client,
        판매ViewModel 기본판매,
        공동구매화면상태ViewModel 화면상태,
        공동구매거래경로분기ViewModel 분기)
        : base(화면상태, CommunityGroupPurchaseTradeRouteCodes.OtherCrossBorder)
    {
        _client = client;
        _기본판매 = 기본판매;
        _화면상태 = 화면상태;
        _분기 = 분기;
        _기본판매.상태.PropertyChanged += 판매상태변경;
        _화면상태.PropertyChanged += 화면상태변경;
        _분기.PropertyChanged += 분기변경;
        공동구매변경동기화();
    }

    public override bool 활성 => _분기.해외수출활성;
    public override IReadOnlyList<string> 지원채널목록 => CommerceChannelOrderSyncScopes.OverseasChannelTypes;
    public 판매ViewModel 기본판매 => _기본판매;

    public IReadOnlyList<판매채널계정항목응답> 해외계정목록
        => _기본판매.상태.계정목록
            .Where(item => 지원채널목록.Contains(item.채널종류, StringComparer.OrdinalIgnoreCase))
            .ToArray();

    public IReadOnlyList<판매상품항목응답> 상품목록 => _기본판매.상태.상품목록;

    public IReadOnlyList<채널출품항목응답> 출품목록
    {
        get
        {
            var accountIds = 해외계정목록.Select(account => account.Id).ToHashSet();
            return _기본판매.상태.출품목록
                .Where(listing => accountIds.Contains(listing.판매채널계정Id))
                .ToArray();
        }
    }

    public AmazonExportReadinessDraftRequest 초안
    {
        get => _초안;
        private set => SetProperty(ref _초안, value);
    }

    public AmazonExportReadinessResult 계획
    {
        get => _계획;
        private set
        {
            if (SetProperty(ref _계획, value))
            {
                OnPropertyChanged(nameof(출품준비완료));
                OnPropertyChanged(nameof(수출이행준비완료));
                OnPropertyChanged(nameof(필수작업안내));
            }
        }
    }

    public bool 출품준비완료 => 계획.ReadyForAmazonListingDraft;
    public bool 수출이행준비완료 => 계획.ReadyForExportFulfillment;
    public IReadOnlyList<string> 필수작업안내 => 계획.RequiredActionCodes.Select(필수작업표시).ToArray();
    public 판매채널계정저장요청 Amazon계정초안
    {
        get => _Amazon계정초안;
        private set => SetProperty(ref _Amazon계정초안, value);
    }
    public 판매상품저장요청 수출상품초안
    {
        get => _수출상품초안;
        private set => SetProperty(ref _수출상품초안, value);
    }
    public bool 입고재고연결됨 => 수출상품초안.입고상품Id > 0;
    public bool 공동주문출고배분완료 => _실행?.재고배분.서버배분완료 == true;
    public IReadOnlyList<판매실행단계ViewModel> 진행단계 => 수출단계생성();
    public string 다음작업안내
        => !활성
            ? "국내 출발·해외 도착 거래에서 해외 수출 흐름을 사용할 수 있습니다."
            : !입고재고연결됨
                ? "수출할 국내 입고 재고를 선택해 주세요."
                : 초안.SalesChannelAccountId <= 0
                    ? "Amazon 판매자 계정을 연결하거나 선택해 주세요."
                    : 초안.SalesProductId <= 0
                        ? "입고 재고를 수출 판매상품으로 등록해 주세요."
                        : !출품준비완료
                            ? "Amazon 상품·마켓플레이스·콘텐츠 정보를 확인해 주세요."
                            : !수출이행준비완료
                                ? "수출신고, 재고 배분, 국제운송과 반품·정산 조건을 확인해 주세요."
                                : "Amazon 출품과 수출 이행 준비가 완료되었습니다.";

    public void 실행연결(공동구매실행기능ViewModel 실행)
    {
        ArgumentNullException.ThrowIfNull(실행);
        if (ReferenceEquals(_실행, 실행))
        {
            return;
        }

        if (_실행 is not null)
        {
            _실행.PropertyChanged -= 실행변경;
        }

        _실행 = 실행;
        _실행.PropertyChanged += 실행변경;
        OnPropertyChanged(string.Empty);
    }

    public bool 수출재고선택(long inboundProductId)
    {
        if (_실행 is null)
        {
            return 유효성실패("공동구매 실행·창고 ViewModel을 먼저 연결해 주세요.");
        }

        var inventory = _실행.창고.상태.재고목록.FirstOrDefault(item => item.입고상품Id == inboundProductId);
        if (inventory is null)
        {
            return 유효성실패("수출할 입고 재고를 목록에서 찾아 주세요.");
        }

        if (_실행.창고.상태.창고목록.Any(warehouse => warehouse.Id == inventory.창고Id))
        {
            _실행.창고.상태.창고선택(inventory.창고Id);
        }

        _실행.창고.상태.재고선택(inboundProductId);
        수출상품초안.입고상품Id = inventory.입고상품Id;
        수출상품초안.대표상품명 = string.IsNullOrWhiteSpace(inventory.상품명)
            ? 수출상품초안.대표상품명
            : inventory.상품명;
        if (string.IsNullOrWhiteSpace(수출상품초안.판매SKU))
        {
            수출상품초안.판매SKU = inventory.SKU;
        }

        입력변경알림();
        return true;
    }

    public void 공동주문출고상태확인(bool inventoryReserved)
    {
        초안.InventoryReserved = inventoryReserved;
        초안.OutboundBatchReady = 공동주문출고배분완료;
        입력변경알림();
    }

    public async Task<bool> 목록조회Async(CancellationToken cancellationToken = default)
        => await 작업실행Async(
            async token =>
            {
                var accounts = await _client.계정목록조회Async(token);
                var products = await _client.상품목록조회Async(token);
                var listings = await _client.출품목록조회Async(token);
                _기본판매.상태.목록적용(accounts, products, listings);
            },
            "해외 판매채널, 수출상품과 출품 상태를 조회했습니다.",
            cancellationToken);

    public bool 판매상품선택(long salesProductId)
    {
        var product = 상품목록.FirstOrDefault(item => item.Id == salesProductId);
        if (product is null)
        {
            return 유효성실패("목록에 있는 수출 판매상품을 선택해 주세요.");
        }

        초안.SalesProductId = product.Id;
        초안.SalesSku = product.판매SKU;
        초안.ProductName = product.대표상품명;
        입력변경알림();
        return true;
    }

    public bool Amazon계정선택(long accountId)
    {
        var account = 해외계정목록.FirstOrDefault(item =>
            item.Id == accountId
            && string.Equals(item.채널종류, CommerceChannelKeys.Amazon, StringComparison.OrdinalIgnoreCase));
        if (account is null)
        {
            return 유효성실패("연결된 Amazon 판매채널 계정을 선택해 주세요.");
        }

        초안.SalesChannelAccountId = account.Id;
        초안.AmazonSellerAccountConnected = true;
        입력변경알림();
        return true;
    }

    public async Task<bool> Amazon계정생성Async(CancellationToken cancellationToken = default)
    {
        if (!활성)
        {
            return 유효성실패("해외 수출 분기가 활성화된 경우에만 Amazon 계정을 연결할 수 있습니다.");
        }
        if (string.IsNullOrWhiteSpace(Amazon계정초안.상점명))
        {
            return 유효성실패("Amazon 판매자 상점명을 입력해 주세요.");
        }

        Amazon계정초안.채널종류 = CommerceChannelKeys.Amazon;
        return await 작업실행Async(
            async token =>
            {
                var created = await _client.계정생성Async(Amazon계정초안, token)
                    ?? throw new InvalidOperationException("Amazon 판매채널 계정 생성 응답이 비어 있습니다.");
                _기본판매.상태.계정저장적용(created);
                초안.SalesChannelAccountId = created.Id;
                초안.AmazonSellerAccountConnected = true;
                Amazon계정초안 = 새Amazon계정초안();
                로컬미리보기();
            },
            "Amazon 판매자 계정을 연결했습니다.",
            cancellationToken);
    }

    public async Task<bool> 수출상품생성Async(CancellationToken cancellationToken = default)
    {
        if (!활성)
        {
            return 유효성실패("해외 수출 분기가 활성화된 경우에만 수출 판매상품을 만들 수 있습니다.");
        }
        if (수출상품초안.입고상품Id <= 0
            || string.IsNullOrWhiteSpace(수출상품초안.대표상품명)
            || string.IsNullOrWhiteSpace(수출상품초안.판매SKU)
            || 수출상품초안.판매가 <= 0)
        {
            return 유효성실패("입고상품, 대표상품명, 판매 SKU와 0원보다 큰 판매가를 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var created = await _client.상품생성Async(수출상품초안, token)
                    ?? throw new InvalidOperationException("수출 판매상품 생성 응답이 비어 있습니다.");
                _기본판매.상태.상품저장적용(created);
                초안.SalesProductId = created.Id;
                초안.SalesSku = created.판매SKU;
                초안.ProductName = created.대표상품명;
                수출상품초안 = new 판매상품저장요청
                {
                    입고상품Id = created.입고상품Id,
                    대표상품명 = created.대표상품명,
                    판매SKU = created.판매SKU,
                    판매가 = created.판매가
                };
                로컬미리보기();
            },
            "수출 판매상품을 등록했습니다.",
            cancellationToken);
    }

    public bool 수출이행경로선택(string fulfillmentModeCode)
    {
        if (fulfillmentModeCode is not (
            AmazonExportFulfillmentModeCode.FbmInternationalShipping
            or AmazonExportFulfillmentModeCode.FbaInbound
            or AmazonExportFulfillmentModeCode.ExportForwarderHandover
            or AmazonExportFulfillmentModeCode.Manual))
        {
            return 유효성실패("FBM 국제배송, FBA 입고, 수출 포워더 인계 또는 수동 경로를 선택해 주세요.");
        }

        초안.FulfillmentModeCode = fulfillmentModeCode;
        입력변경알림();
        return true;
    }

    public AmazonExportReadinessResult 로컬미리보기()
    {
        계획 = AmazonExportReadinessPlanner.Plan(초안);
        return 계획;
    }

    public void 입력변경알림()
    {
        OnPropertyChanged(nameof(초안));
        로컬미리보기();
    }

    public async Task<bool> Amazon출품생성Async(CancellationToken cancellationToken = default)
    {
        var current = 로컬미리보기();
        if (!활성)
        {
            return 유효성실패("국내 출발·해외 도착으로 판정된 거래에서만 해외 수출 출품을 만들 수 있습니다.");
        }
        if (!current.ReadyForAmazonListingDraft)
        {
            return 유효성실패(string.Join(" ", 필수작업안내));
        }

        return await 작업실행Async(
            async token =>
            {
                var created = await _client.출품생성Async(
                    new 채널출품저장요청
                    {
                        판매상품Id = 초안.SalesProductId,
                        판매채널계정Id = 초안.SalesChannelAccountId
                    },
                    token)
                    ?? throw new InvalidOperationException("해외 판매채널 출품 응답이 비어 있습니다.");
                _기본판매.상태.출품저장적용(created);
            },
            "Amazon 수출 출품을 생성했습니다.",
            cancellationToken);
    }

    public void Dispose()
    {
        if (_실행 is not null)
        {
            _실행.PropertyChanged -= 실행변경;
        }

        _화면상태.PropertyChanged -= 화면상태변경;
        _분기.PropertyChanged -= 분기변경;
        _기본판매.상태.PropertyChanged -= 판매상태변경;
        GC.SuppressFinalize(this);
    }

    private void 화면상태변경(object? sender, PropertyChangedEventArgs e)
        => 공동구매변경동기화();

    private void 분기변경(object? sender, PropertyChangedEventArgs e)
        => OnPropertyChanged(string.Empty);

    private void 실행변경(object? sender, PropertyChangedEventArgs e)
        => OnPropertyChanged(string.Empty);

    private void 판매상태변경(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(해외계정목록));
        OnPropertyChanged(nameof(상품목록));
        OnPropertyChanged(nameof(출품목록));
        OnPropertyChanged(nameof(진행단계));
        OnPropertyChanged(nameof(다음작업안내));
    }

    private void 공동구매변경동기화()
    {
        var campaign = _화면상태.선택된공동구매;
        if (_대상공동구매Id == campaign?.Id)
        {
            return;
        }

        _대상공동구매Id = campaign?.Id;
        var settings = campaign?.GroupPurchase;
        var hsCode = !string.IsNullOrWhiteSpace(settings?.HsCode)
            ? settings.HsCode
            : campaign?.Options.FirstOrDefault(option =>
                !string.IsNullOrWhiteSpace(option.HsCode))?.HsCode
              ?? string.Empty;
        초안 = new AmazonExportReadinessDraftRequest
        {
            ProductName = campaign?.Title ?? string.Empty,
            SalesSku = campaign is null ? string.Empty : $"EXP-{campaign.Id:N}"[..12].ToUpperInvariant(),
            HsCode = hsCode,
            OriginCountryCode = string.IsNullOrWhiteSpace(settings?.ShipFromCountryCode)
                ? CommunityGroupPurchaseTradeRoutePolicy.KoreaCountryCode
                : settings.ShipFromCountryCode,
            ImportParticipantEligibilityRequired = false,
            SourceImportGroupPurchaseId = campaign?.CommunityLedgerId ?? string.Empty,
            FulfillmentModeCode = AmazonExportFulfillmentModeCode.FbmInternationalShipping
        };
        Amazon계정초안 = 새Amazon계정초안();
        수출상품초안 = new 판매상품저장요청
        {
            대표상품명 = campaign?.Title ?? string.Empty,
            판매SKU = campaign is null ? string.Empty : $"EXP-{campaign.Id:N}"[..12].ToUpperInvariant()
        };
        계획 = AmazonExportReadinessPlanner.Plan(초안);
        OnPropertyChanged(nameof(활성));
    }

    private static string 필수작업표시(string actionCode)
        => actionCode switch
        {
            AmazonExportRequiredActionCode.ConfirmImportParticipantEligibility => "수입 참여자·수출 활용 자격 확인",
            AmazonExportRequiredActionCode.ConfirmAmazonSellerAccount => "Amazon 판매자 계정 연결",
            AmazonExportRequiredActionCode.ConfirmMarketplaceAndSellerId => "마켓플레이스와 Seller ID 확인",
            AmazonExportRequiredActionCode.ConfirmProductTypeDefinition => "Amazon 상품 유형 정의 확인",
            AmazonExportRequiredActionCode.ConfirmListingPayloadMapping => "상품 출품 데이터 연결",
            AmazonExportRequiredActionCode.ConfirmHsExportReview => "HS 코드와 수출 제한 검토",
            AmazonExportRequiredActionCode.ConfirmCustomsBrokerEngagement => "관세사·수출신고 계획 확인",
            AmazonExportRequiredActionCode.ConfirmCustomsBrokerFee => "관세사 비용 확인",
            AmazonExportRequiredActionCode.ConfirmExportDocuments => "상업송장과 포장명세서 준비",
            AmazonExportRequiredActionCode.ConfirmInventoryAndOutboundBatch => "수출 재고와 출고 배치 확보",
            AmazonExportRequiredActionCode.ConfirmFulfillmentRoute => "해외 이행 경로 확정",
            AmazonExportRequiredActionCode.ConfirmInternationalShippingPlan => "국제운송 계획 확정",
            AmazonExportRequiredActionCode.ConfirmReturnsAndSettlementPolicy => "반품·정산 통화·수수료 정책 확정",
            AmazonExportRequiredActionCode.ConfirmAmazonFbaInboundEligibility => "Amazon FBA 입고 자격 확인",
            AmazonExportRequiredActionCode.ConfirmNonFbaColdChainFulfillmentRoute => "냉장·냉동 비FBA 경로 확인",
            AmazonExportRequiredActionCode.ConfirmKoreanLogisticsTrace => "국내 물류 이력 연결",
            AmazonExportRequiredActionCode.ConfirmReviewUsageConsent => "사용자 후기 활용 동의 확인",
            AmazonExportRequiredActionCode.ConfirmAmazonDetailPageImageAsset => "Amazon 상세페이지 이미지 승인",
            _ => actionCode
        };

    private IReadOnlyList<판매실행단계ViewModel> 수출단계생성()
    {
        var listingComplete = 출품목록.Any(listing =>
            listing.판매상품Id == 초안.SalesProductId
            && listing.판매채널계정Id == 초안.SalesChannelAccountId);
        var completed = new[]
        {
            입고재고연결됨,
            초안.SalesChannelAccountId > 0,
            초안.SalesProductId > 0,
            출품준비완료,
            listingComplete,
            수출이행준비완료
        };
        var labels = new[]
        {
            ("inventory", "수출 재고", "국내 입고 재고와 수출 대상 상품을 연결합니다."),
            ("account", "Amazon 계정", "Amazon 판매자 계정과 마켓플레이스를 확인합니다."),
            ("product", "수출 판매상품", "입고상품을 수출 판매 SKU로 등록합니다."),
            ("listing-ready", "출품 준비", "상품 유형·콘텐츠·마켓 스토리를 확인합니다."),
            ("listing", "Amazon 출품", "준비된 상품을 Amazon 채널에 출품합니다."),
            ("fulfillment", "수출 이행", "수출신고·출고배치·국제운송·반품 정산을 확정합니다.")
        };
        var firstPending = Array.FindIndex(completed, value => !value);
        return labels.Select((stage, index) => new 판매실행단계ViewModel(
            stage.Item1,
            stage.Item2,
            completed[index]
                ? 판매실행단계상태.완료
                : index == firstPending && 활성
                    ? 판매실행단계상태.진행중
                    : 판매실행단계상태.대기,
            stage.Item3)).ToArray();
    }

    private static 판매채널계정저장요청 새Amazon계정초안()
        => new() { 채널종류 = CommerceChannelKeys.Amazon };
}
