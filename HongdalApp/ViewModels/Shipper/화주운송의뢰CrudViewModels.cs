using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using HongdalApp.Models.Shipper;
using HongdalApp.Services;

namespace HongdalApp.ViewModels.Shipper;

public sealed class 화주운송의뢰상태ViewModel : ObservableObject
{
    private IReadOnlyList<ShipperRequestItem> _의뢰목록 = [];
    private ShipperRequestItem? _선택된의뢰;

    public IReadOnlyList<ShipperRequestItem> 의뢰목록
    {
        get => _의뢰목록;
        private set => SetProperty(ref _의뢰목록, value);
    }

    public ShipperRequestItem? 선택된의뢰
    {
        get => _선택된의뢰;
        private set => SetProperty(ref _선택된의뢰, value);
    }

    public void 목록적용(IReadOnlyList<ShipperRequestItem> items)
    {
        var selectedId = 선택된의뢰?.의뢰Id;
        의뢰목록 = items ?? [];
        선택된의뢰 = 의뢰목록.FirstOrDefault(item => string.Equals(
            item.의뢰Id,
            selectedId,
            StringComparison.OrdinalIgnoreCase)) ?? 의뢰목록.FirstOrDefault();
    }

    public void 저장적용(ShipperRequestItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        의뢰목록 = 의뢰목록
            .Where(value => !string.Equals(value.의뢰Id, item.의뢰Id, StringComparison.OrdinalIgnoreCase))
            .Prepend(item)
            .ToArray();
        선택된의뢰 = item;
    }

    public bool 선택(string requestId)
    {
        var item = 의뢰목록.FirstOrDefault(value => string.Equals(
            value.의뢰Id,
            requestId,
            StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return false;
        }

        선택된의뢰 = item;
        return true;
    }

    public void 삭제적용(string requestId)
    {
        의뢰목록 = 의뢰목록
            .Where(value => !string.Equals(value.의뢰Id, requestId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (string.Equals(선택된의뢰?.의뢰Id, requestId, StringComparison.OrdinalIgnoreCase))
        {
            선택된의뢰 = 의뢰목록.FirstOrDefault();
        }
    }
}

public abstract class 화주운송의뢰Crud업무ViewModelBase(
    IShipperOperationsService service,
    화주운송의뢰상태ViewModel 상태,
    string 업무코드,
    string 업무명,
    업무조각유형 업무유형) : 업무조각ViewModelBase(업무코드, 업무명, 업무유형)
{
    protected IShipperOperationsService Service { get; } = service;
    protected 화주운송의뢰상태ViewModel 의뢰상태 { get; } = 상태;

    protected static ShipperRequestItem 복사(ShipperRequestItem source)
        => new()
        {
            의뢰Id = source.의뢰Id,
            화물종류 = source.화물종류,
            화물적재형태 = source.화물적재형태,
            의뢰상태 = source.의뢰상태,
            결제상태 = source.결제상태,
            배차상태 = source.배차상태,
            정산상태 = source.정산상태,
            운송방식 = source.운송방식,
            차량종류 = source.차량종류,
            결제수단 = source.결제수단,
            결제예정금액 = source.결제예정금액,
            예상거리Km = source.예상거리Km,
            기준운임 = source.기준운임,
            기사지급예정운임 = source.기사지급예정운임,
            알선단계 = source.알선단계,
            재알선금지 = source.재알선금지,
            정책위반 = source.정책위반,
            재알선의심 = source.재알선의심,
            정책경고목록 = source.정책경고목록.ToArray(),
            생성일시 = source.생성일시,
            픽업지 = source.픽업지,
            하차지 = source.하차지
        };

    protected bool 유효한초안(ShipperRequestItem draft)
        => !string.IsNullOrWhiteSpace(draft.화물종류)
           && !string.IsNullOrWhiteSpace(draft.픽업지)
           && !string.IsNullOrWhiteSpace(draft.하차지);
}

public sealed class 화주운송의뢰목록조회ViewModel(
    IShipperOperationsService service,
    화주운송의뢰상태ViewModel 상태)
    : 화주운송의뢰Crud업무ViewModelBase(
        service,
        상태,
        "shipper-request-query",
        "운송의뢰 조회",
        업무조각유형.목록조회), I목록조회ViewModel<ShipperRequestItem>
{
    public IReadOnlyList<ShipperRequestItem> 항목목록 => 의뢰상태.의뢰목록;
    public ShipperRequestItem? 선택된항목 => 의뢰상태.선택된의뢰;

    public Task<bool> 조회Async(CancellationToken cancellationToken = default)
        => 작업실행Async(
            async token => 의뢰상태.목록적용(await Service.GetRequestsAsync(token)),
            "운송의뢰를 조회했습니다.",
            cancellationToken);

    public bool 선택(string requestId)
        => 의뢰상태.선택(requestId)
           || 유효성실패("목록에 있는 운송의뢰를 선택해 주세요.");
}

public sealed class 화주운송의뢰등록ViewModel(
    IShipperOperationsService service,
    화주운송의뢰상태ViewModel 상태)
    : 화주운송의뢰Crud업무ViewModelBase(
        service,
        상태,
        "shipper-request-create",
        "운송의뢰 등록",
        업무조각유형.등록), I등록ViewModel<ShipperRequestItem>
{
    private ShipperRequestItem _초안 = new();

    public ShipperRequestItem 초안
    {
        get => _초안;
        private set => SetProperty(ref _초안, value);
    }

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        if (!유효한초안(초안))
        {
            return 유효성실패("화물 종류와 상·하차지를 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                if (string.IsNullOrWhiteSpace(초안.의뢰Id))
                {
                    초안.의뢰Id = $"shipper-app-{Guid.NewGuid():N}";
                }
                if (초안.생성일시 == default)
                {
                    초안.생성일시 = DateTime.UtcNow;
                }

                var created = await Service.AddRequestAsync(초안, token);
                의뢰상태.저장적용(복사(created));
                초안 = new ShipperRequestItem();
            },
            "운송의뢰를 등록했습니다.",
            cancellationToken);
    }

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

public sealed class 화주운송의뢰수정ViewModel(
    IShipperOperationsService service,
    화주운송의뢰상태ViewModel 상태)
    : 화주운송의뢰Crud업무ViewModelBase(
        service,
        상태,
        "shipper-request-update",
        "운송의뢰 수정",
        업무조각유형.수정), I수정ViewModel<ShipperRequestItem>
{
    private ShipperRequestItem _초안 = new();

    public ShipperRequestItem 초안
    {
        get => _초안;
        private set => SetProperty(ref _초안, value);
    }

    public bool 선택항목적용()
    {
        if (의뢰상태.선택된의뢰 is not { } selected)
        {
            return 유효성실패("수정할 운송의뢰를 먼저 선택해 주세요.");
        }

        초안 = 복사(selected);
        return true;
    }

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        if (의뢰상태.선택된의뢰 is not { } selected || string.IsNullOrWhiteSpace(초안.의뢰Id))
        {
            return 유효성실패("수정할 운송의뢰를 먼저 선택해 주세요.");
        }
        if (!string.Equals(selected.의뢰Id, 초안.의뢰Id, StringComparison.OrdinalIgnoreCase))
        {
            return 유효성실패("수정 중에는 운송의뢰 ID를 변경할 수 없습니다.");
        }
        if (!유효한초안(초안))
        {
            return 유효성실패("화물 종류와 상·하차지를 입력해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                var updated = await Service.UpdateRequestAsync(초안, token);
                의뢰상태.저장적용(복사(updated));
                선택항목적용();
            },
            "운송의뢰를 수정했습니다.",
            cancellationToken);
    }

    public void 입력변경알림() => OnPropertyChanged(nameof(초안));
}

public sealed class 화주운송의뢰삭제ViewModel(
    IShipperOperationsService service,
    화주운송의뢰상태ViewModel 상태)
    : 화주운송의뢰Crud업무ViewModelBase(
        service,
        상태,
        "shipper-request-delete",
        "운송의뢰 삭제",
        업무조각유형.삭제), I삭제ViewModel<string>
{
    public string 초안 => 의뢰상태.선택된의뢰?.의뢰Id ?? string.Empty;

    public async Task<bool> 실행Async(CancellationToken cancellationToken = default)
    {
        var requestId = 초안;
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return 유효성실패("삭제할 운송의뢰를 먼저 선택해 주세요.");
        }

        return await 작업실행Async(
            async token =>
            {
                await Service.DeleteRequestAsync(requestId, token);
                의뢰상태.삭제적용(requestId);
            },
            "운송의뢰를 삭제했습니다.",
            cancellationToken);
    }
}

public sealed class 화주운송의뢰CrudViewModel(
    화주운송의뢰목록조회ViewModel 조회,
    화주운송의뢰등록ViewModel 등록,
    화주운송의뢰수정ViewModel 수정,
    화주운송의뢰삭제ViewModel 삭제)
    : 업무단위CrudViewModelBase<화주운송의뢰목록조회ViewModel, 화주운송의뢰등록ViewModel, 화주운송의뢰수정ViewModel, 화주운송의뢰삭제ViewModel>(
        "shipper-request",
        "화주 운송의뢰",
        조회,
        등록,
        수정,
        삭제);
