using Hongdal.Contracts.Common.Sales;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed class 판매채널계정수정ViewModel(
    I판매채널계정Service service,
    판매업무상태ViewModel 상태)
    : 판매업무조각ViewModelBase(
        상태,
        "sales-channel-account-update",
        "판매채널 계정 수정",
        업무조각유형.수정), I수정ViewModel<판매채널계정저장요청>
{
    private 판매채널계정저장요청 _초안 = new();

    public 판매채널계정저장요청 초안
    {
        get => _초안;
        private set => SetProperty(ref _초안, value);
    }

    public bool 선택항목적용()
    {
        var selected = 판매상태.선택된계정;
        if (selected is null)
        {
            return 유효성실패("수정할 판매채널 계정을 먼저 선택해 주세요.");
        }

        초안 = new 판매채널계정저장요청
        {
            채널종류 = selected.채널종류,
            상점명 = selected.상점명
        };
        return true;
    }

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        var selected = 판매상태.선택된계정;
        if (selected is null)
        {
            return 유효성실패("수정할 판매채널 계정을 먼저 선택해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(초안.채널종류) || string.IsNullOrWhiteSpace(초안.상점명))
        {
            return 유효성실패("판매채널 종류와 상점명을 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var result = await service.계정수정Async(selected.Id, 초안, token)
                    ?? throw new InvalidOperationException("판매채널 계정 수정 응답이 비어 있습니다.");
                판매상태.계정저장적용(result);
                선택항목적용();
            },
            "판매채널 계정을 수정했습니다.",
            cancellationToken);
    }

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

public sealed class 판매채널계정삭제ViewModel(
    I판매채널계정Service service,
    판매업무상태ViewModel 상태)
    : 판매업무조각ViewModelBase(
        상태,
        "sales-channel-account-delete",
        "판매채널 계정 삭제",
        업무조각유형.삭제), I삭제ViewModel<long>
{
    public long 초안 => 판매상태.선택된계정?.Id ?? 0;

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        var accountId = 초안;
        if (accountId <= 0)
        {
            return 유효성실패("삭제할 판매채널 계정을 먼저 선택해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                await service.계정삭제Async(accountId, token);
                판매상태.계정삭제적용(accountId);
            },
            "판매채널 계정을 삭제했습니다.",
            cancellationToken);
    }
}

public sealed class 판매상품수정ViewModel(
    I상품등록Service service,
    판매업무상태ViewModel 상태)
    : 판매업무조각ViewModelBase(
        상태,
        "sales-product-update",
        "판매상품 수정",
        업무조각유형.수정), I수정ViewModel<판매상품저장요청>
{
    private 판매상품저장요청 _초안 = new();

    public 판매상품저장요청 초안
    {
        get => _초안;
        private set => SetProperty(ref _초안, value);
    }

    public bool 선택항목적용()
    {
        var selected = 판매상태.선택된상품;
        if (selected is null)
        {
            return 유효성실패("수정할 판매상품을 먼저 선택해 주세요.");
        }

        초안 = new 판매상품저장요청
        {
            입고상품Id = selected.입고상품Id,
            대표상품명 = selected.대표상품명,
            판매SKU = selected.판매SKU,
            판매가 = selected.판매가,
            샘플데이터여부 = selected.샘플데이터여부,
            샘플데이터코드 = selected.샘플데이터코드
        };
        return true;
    }

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        var selected = 판매상태.선택된상품;
        if (selected is null)
        {
            return 유효성실패("수정할 판매상품을 먼저 선택해 주세요.");
        }

        if (초안.입고상품Id <= 0
            || string.IsNullOrWhiteSpace(초안.대표상품명)
            || string.IsNullOrWhiteSpace(초안.판매SKU)
            || 초안.판매가 <= 0)
        {
            return 유효성실패("입고상품, 대표상품명, 판매 SKU와 0원보다 큰 판매가를 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var result = await service.상품수정Async(selected.Id, 초안, token)
                    ?? throw new InvalidOperationException("판매상품 수정 응답이 비어 있습니다.");
                판매상태.상품저장적용(result);
                선택항목적용();
            },
            "판매상품을 수정했습니다.",
            cancellationToken);
    }

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

public sealed class 판매상품삭제ViewModel(
    I상품등록Service service,
    판매업무상태ViewModel 상태)
    : 판매업무조각ViewModelBase(
        상태,
        "sales-product-delete",
        "판매상품 삭제",
        업무조각유형.삭제), I삭제ViewModel<long>
{
    public long 초안 => 판매상태.선택된상품?.Id ?? 0;

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        var productId = 초안;
        if (productId <= 0)
        {
            return 유효성실패("삭제할 판매상품을 먼저 선택해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                await service.상품삭제Async(productId, token);
                판매상태.상품삭제적용(productId);
            },
            "판매상품을 삭제했습니다.",
            cancellationToken);
    }
}

public sealed class 채널출품수정ViewModel(
    I채널출품Service service,
    판매업무상태ViewModel 상태)
    : 판매업무조각ViewModelBase(
        상태,
        "sales-listing-update",
        "채널 출품 수정",
        업무조각유형.수정), I수정ViewModel<채널출품저장요청>
{
    private 채널출품저장요청 _초안 = new();

    public 채널출품저장요청 초안
    {
        get => _초안;
        private set => SetProperty(ref _초안, value);
    }

    public bool 선택항목적용()
    {
        var selected = 판매상태.선택된출품;
        if (selected is null)
        {
            return 유효성실패("수정할 채널 출품을 먼저 선택해 주세요.");
        }

        초안 = new 채널출품저장요청
        {
            판매상품Id = selected.판매상품Id,
            판매채널계정Id = selected.판매채널계정Id
        };
        return true;
    }

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        var selected = 판매상태.선택된출품;
        if (selected is null)
        {
            return 유효성실패("수정할 채널 출품을 먼저 선택해 주세요.");
        }

        if (초안.판매상품Id <= 0 || 초안.판매채널계정Id <= 0)
        {
            return 유효성실패("출품할 판매상품과 판매채널 계정을 선택해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var result = await service.출품수정Async(selected.Id, 초안, token)
                    ?? throw new InvalidOperationException("채널 출품 수정 응답이 비어 있습니다.");
                판매상태.출품저장적용(result);
                선택항목적용();
            },
            "채널 출품을 수정했습니다.",
            cancellationToken);
    }

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

public sealed class 채널출품삭제ViewModel(
    I채널출품Service service,
    판매업무상태ViewModel 상태)
    : 판매업무조각ViewModelBase(
        상태,
        "sales-listing-delete",
        "채널 출품 삭제",
        업무조각유형.삭제), I삭제ViewModel<long>
{
    public long 초안 => 판매상태.선택된출품?.Id ?? 0;

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        var listingId = 초안;
        if (listingId <= 0)
        {
            return 유효성실패("삭제할 채널 출품을 먼저 선택해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                await service.출품삭제Async(listingId, token);
                판매상태.출품삭제적용(listingId);
            },
            "채널 출품을 삭제했습니다.",
            cancellationToken);
    }
}

public sealed class 판매채널계정CrudViewModel(
    판매채널계정조회ViewModel 조회,
    판매채널계정등록ViewModel 등록,
    판매채널계정수정ViewModel 수정,
    판매채널계정삭제ViewModel 삭제)
    : 업무단위CrudViewModelBase<판매채널계정조회ViewModel, 판매채널계정등록ViewModel, 판매채널계정수정ViewModel, 판매채널계정삭제ViewModel>(
        "sales-channel-account",
        "판매채널 계정",
        조회,
        등록,
        수정,
        삭제);

public sealed class 판매상품CrudViewModel(
    판매상품조회ViewModel 조회,
    판매상품등록ViewModel 등록,
    판매상품수정ViewModel 수정,
    판매상품삭제ViewModel 삭제)
    : 업무단위CrudViewModelBase<판매상품조회ViewModel, 판매상품등록ViewModel, 판매상품수정ViewModel, 판매상품삭제ViewModel>(
        "sales-product",
        "판매상품",
        조회,
        등록,
        수정,
        삭제);

public sealed class 채널출품CrudViewModel(
    채널출품조회ViewModel 조회,
    채널출품등록ViewModel 등록,
    채널출품수정ViewModel 수정,
    채널출품삭제ViewModel 삭제)
    : 업무단위CrudViewModelBase<채널출품조회ViewModel, 채널출품등록ViewModel, 채널출품수정ViewModel, 채널출품삭제ViewModel>(
        "sales-listing",
        "채널 출품",
        조회,
        등록,
        수정,
        삭제);
