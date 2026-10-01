using Hongdal.Ui.Common.Areas.App.ViewModels;
using WarehouseManagerApp.Services;

namespace WarehouseManagerApp.ViewModels.Warehouse;

public sealed class 일반입고작업PageViewModel : 창고PageViewModelBase
{
    public 일반입고작업PageViewModel(
        창고작업세션상태ViewModel 세션,
        입고ViewModel 입고,
        IInboundReceivingWorkflowService 입고Workflow)
        : base(
            세션,
            창고PageCodes.일반입고,
            "일반 입고 작업",
            창고운영ProfileCodes.일반입출고)
    {
        this.입고 = 구성요소등록(입고);
        this.입고Workflow = 입고Workflow;
    }

    public 입고ViewModel 입고 { get; }
    public IInboundReceivingWorkflowService 입고Workflow { get; }
    public bool 처리중 => 입고.처리중;

    public async Task<bool> 초기화Async(CancellationToken cancellationToken = default)
    {
        var inboundLoaded = await 입고.목록조회Async(cancellationToken);
        var inventoryLoaded = await 입고.재고목록조회Async(cancellationToken);
        return inboundLoaded && inventoryLoaded;
    }
}

public sealed class 일반재고현황PageViewModel : 창고PageViewModelBase
{
    public 일반재고현황PageViewModel(
        창고작업세션상태ViewModel 세션,
        입고재고조회ViewModel 입고재고조회,
        출고재고조회ViewModel 출고재고조회)
        : base(
            세션,
            창고PageCodes.일반재고,
            "일반 재고 현황",
            창고운영ProfileCodes.일반입출고)
    {
        this.입고재고조회 = 구성요소등록(입고재고조회);
        this.출고재고조회 = 구성요소등록(출고재고조회);
    }

    public 입고재고조회ViewModel 입고재고조회 { get; }
    public 출고재고조회ViewModel 출고재고조회 { get; }
    public bool 처리중 => 입고재고조회.처리중 || 출고재고조회.처리중;

    public Task<bool> 초기화Async(CancellationToken cancellationToken = default)
        => 출고재고조회.조회Async(cancellationToken);
}

public sealed class 일반출고작업PageViewModel : 창고PageViewModelBase
{
    private readonly IWarehousePickingBatchWorkspaceService _피킹Service;
    private IReadOnlyList<WarehousePickingTaskItem> _피킹작업목록 = [];
    private string? _오류메시지;

    public 일반출고작업PageViewModel(
        창고작업세션상태ViewModel 세션,
        출고ViewModel 출고,
        IWarehousePickingBatchWorkspaceService 피킹Service)
        : base(
            세션,
            창고PageCodes.일반출고,
            "일반 출고 작업",
            창고운영ProfileCodes.일반입출고)
    {
        _피킹Service = 피킹Service;
        this.출고 = 구성요소등록(출고);
    }

    public 출고ViewModel 출고 { get; }

    public IReadOnlyList<WarehousePickingTaskItem> 피킹작업목록
    {
        get => _피킹작업목록;
        private set => SetProperty(ref _피킹작업목록, value);
    }

    public string? 오류메시지
    {
        get => _오류메시지;
        private set => SetProperty(ref _오류메시지, value);
    }

    public async Task<bool> 초기화Async(CancellationToken cancellationToken = default)
    {
        var warehouse = 세션.선택된창고;
        if (warehouse is null)
        {
            오류메시지 = "출고 작업을 조회할 창고를 먼저 선택해 주세요.";
            return false;
        }

        오류메시지 = null;
        try
        {
            var inventoryLoaded = await 출고.재고목록조회Async(cancellationToken);
            피킹작업목록 = await _피킹Service.GetAssignedTasksAsync(warehouse.Id, cancellationToken);
            return inventoryLoaded;
        }
        catch (Exception ex)
        {
            오류메시지 = ex.Message;
            return false;
        }
    }
}

public sealed class 일반운송인계PageViewModel : 창고PageViewModelBase
{
    public 일반운송인계PageViewModel(
        창고작업세션상태ViewModel 세션,
        출고재고조회ViewModel 재고조회,
        출고운송인계ViewModel 운송인계)
        : base(
            세션,
            창고PageCodes.일반운송인계,
            "일반 운송 인계",
            창고운영ProfileCodes.일반입출고)
    {
        this.재고조회 = 구성요소등록(재고조회);
        this.운송인계 = 구성요소등록(운송인계);
    }

    public 출고재고조회ViewModel 재고조회 { get; }
    public 출고운송인계ViewModel 운송인계 { get; }
    public bool 처리중 => 재고조회.처리중 || 운송인계.처리중;

    public Task<bool> 초기화Async(CancellationToken cancellationToken = default)
        => 재고조회.조회Async(cancellationToken);
}
