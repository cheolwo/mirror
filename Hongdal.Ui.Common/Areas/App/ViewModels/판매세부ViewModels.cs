using Hongdal.Contracts.Common.Sales;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public abstract class 판매업무조각ViewModelBase(
    판매업무상태ViewModel 상태,
    string 업무코드,
    string 업무명,
    업무조각유형 업무유형) : 판매업무ViewModelBase(상태), I업무조각ViewModel
{
    public string 업무코드 { get; } = 업무코드;
    public string 업무명 { get; } = 업무명;
    public 업무조각유형 업무유형 { get; } = 업무유형;
}

public sealed class 판매채널계정조회ViewModel(판매채널계정ViewModel 원본)
    : 위임업무조각ViewModelBase<판매채널계정ViewModel>(
        원본,
        "sales-channel-account-query",
        "판매채널 계정 조회",
        업무조각유형.목록조회), I목록조회ViewModel<판매채널계정항목응답>
{
    public IReadOnlyList<판매채널계정항목응답> 항목목록 => 원본.계정목록;
    public 판매채널계정항목응답? 선택된항목 => 원본.상태공유.선택된계정;

    public void 지원채널설정(IEnumerable<string>? channelTypes)
        => 원본.지원채널설정(channelTypes);

    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 원본.목록조회Async(cancellationToken);

    public bool 선택(long accountId) => 원본.선택(accountId);
}

public sealed class 판매채널계정등록ViewModel(판매채널계정ViewModel 원본)
    : 위임업무조각ViewModelBase<판매채널계정ViewModel>(
        원본,
        "sales-channel-account-create",
        "판매채널 계정 등록",
        업무조각유형.등록), I등록ViewModel<판매채널계정저장요청>
{
    public 판매채널계정저장요청 초안 => 원본.초안;

    public void 지원채널설정(IEnumerable<string>? channelTypes)
        => 원본.지원채널설정(channelTypes);

    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.생성Async(cancellationToken);

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

public sealed class 판매상품조회ViewModel(상품등록ViewModel 원본)
    : 위임업무조각ViewModelBase<상품등록ViewModel>(
        원본,
        "sales-product-query",
        "판매상품 조회",
        업무조각유형.목록조회),
        I목록조회ViewModel<판매상품항목응답>,
        I비동기검색ViewModel<판매상품항목응답>
{
    private readonly object _검색초기화동기화 = new();
    private Task<bool>? _검색초기조회;

    public IReadOnlyList<판매상품항목응답> 항목목록 => 원본.상품목록;
    public 판매상품항목응답? 선택된항목 => 원본.상태공유.선택된상품;

    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 원본.목록조회Async(cancellationToken);

    public async Task<IReadOnlyList<판매상품항목응답>> 검색Async(
        string? 검색어,
        CancellationToken cancellationToken = default)
    {
        if (항목목록.Count == 0)
        {
            Task<bool> initialLoad;
            lock (_검색초기화동기화)
            {
                _검색초기조회 ??= 조회Async(CancellationToken.None);
                initialLoad = _검색초기조회;
            }

            var loaded = await initialLoad.WaitAsync(cancellationToken);
            if (!loaded)
            {
                lock (_검색초기화동기화)
                {
                    if (ReferenceEquals(_검색초기조회, initialLoad))
                    {
                        _검색초기조회 = null;
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(검색어))
        {
            return 항목목록.Take(20).ToArray();
        }

        var search = 검색어.Trim();
        return 항목목록
            .Where(item => item.대표상품명.Contains(search, StringComparison.OrdinalIgnoreCase)
                           || item.판매SKU.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .ToArray();
    }

    public bool 선택(long productId) => 원본.선택(productId);
}

public sealed class 판매상품등록ViewModel(상품등록ViewModel 원본)
    : 위임업무조각ViewModelBase<상품등록ViewModel>(
        원본,
        "sales-product-create",
        "판매상품 등록",
        업무조각유형.등록), I등록ViewModel<판매상품저장요청>
{
    public 판매상품저장요청 초안 => 원본.초안;

    public void 입고상품연결(long inboundProductId, string? productName = null, string? sku = null)
        => 원본.입고상품연결(inboundProductId, productName, sku);

    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.등록Async(cancellationToken);

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

public sealed class 채널출품조회ViewModel(채널출품ViewModel 원본)
    : 위임업무조각ViewModelBase<채널출품ViewModel>(
        원본,
        "sales-listing-query",
        "채널 출품 조회",
        업무조각유형.목록조회), I목록조회ViewModel<채널출품항목응답>
{
    public IReadOnlyList<채널출품항목응답> 항목목록 => 원본.출품목록;
    public 채널출품항목응답? 선택된항목 => 원본.상태공유.선택된출품;

    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 원본.목록조회Async(cancellationToken);

    public bool 선택(long listingId) => 원본.상태공유.출품선택(listingId);
}

public sealed class 채널출품등록ViewModel(채널출품ViewModel 원본)
    : 위임업무조각ViewModelBase<채널출품ViewModel>(
        원본,
        "sales-listing-create",
        "채널 출품 등록",
        업무조각유형.등록), I등록ViewModel<채널출품저장요청>
{
    public 채널출품저장요청 초안 => 원본.초안;

    public bool 계정선택(long accountId) => 원본.계정선택(accountId);
    public bool 상품선택(long productId) => 원본.상품선택(productId);

    public Task<bool> 실행Async(CancellationToken cancellationToken = default)
        => 원본.생성Async(cancellationToken);

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}
