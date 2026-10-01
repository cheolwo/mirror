using Hongdal.Ui.Common.Areas.App.ViewModels;
using WarehouseManagerApp.Services;

namespace WarehouseManagerApp.ViewModels.Warehouse;

public sealed class 마트재고보충PageViewModel : 창고PageViewModelBase
{
    public 마트재고보충PageViewModel(
        창고작업세션상태ViewModel 세션,
        입고재고조회ViewModel 재고조회,
        입고적재ViewModel 적재)
        : base(
            세션,
            창고PageCodes.마트재고보충,
            "마트 재고 보충",
            창고운영ProfileCodes.마트도심)
    {
        this.재고조회 = 구성요소등록(재고조회);
        this.적재 = 구성요소등록(적재);
    }

    public 입고재고조회ViewModel 재고조회 { get; }
    public 입고적재ViewModel 적재 { get; }
    public bool 처리중 => 재고조회.처리중 || 적재.처리중;

    public Task<bool> 초기화Async(CancellationToken cancellationToken = default)
        => 재고조회.조회Async(cancellationToken);
}

public sealed class 마트주문처리PageViewModel : 창고PageViewModelBase
{
    private readonly IWarehousePickingBatchWorkspaceService _피킹Service;
    private IReadOnlyList<WarehousePickingTaskItem> _작업목록 = [];
    private bool _조회중;
    private string? _오류메시지;

    public 마트주문처리PageViewModel(
        창고작업세션상태ViewModel 세션,
        IWarehousePickingBatchWorkspaceService 피킹Service)
        : base(
            세션,
            창고PageCodes.마트주문처리,
            "마트 주문 처리",
            창고운영ProfileCodes.마트도심)
    {
        _피킹Service = 피킹Service;
    }

    public IReadOnlyList<WarehousePickingTaskItem> 작업목록
    {
        get => _작업목록;
        private set => SetProperty(ref _작업목록, value);
    }

    public bool 조회중
    {
        get => _조회중;
        private set => SetProperty(ref _조회중, value);
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
            오류메시지 = "마트 주문을 조회할 창고를 먼저 선택해 주세요.";
            return false;
        }

        조회중 = true;
        오류메시지 = null;
        try
        {
            작업목록 = await _피킹Service.GetAssignedTasksAsync(warehouse.Id, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            오류메시지 = ex.Message;
            return false;
        }
        finally
        {
            조회중 = false;
        }
    }
}

public sealed class 마트피킹포장PageViewModel : 창고PageViewModelBase
{
    public 마트피킹포장PageViewModel(
        창고작업세션상태ViewModel 세션,
        출고재고조회ViewModel 재고조회,
        출고포장ViewModel 포장,
        IWarehousePickingBatchWorkspaceService 피킹Service)
        : base(
            세션,
            창고PageCodes.마트피킹포장,
            "마트 피킹·포장",
            창고운영ProfileCodes.마트도심)
    {
        this.재고조회 = 구성요소등록(재고조회);
        this.포장 = 구성요소등록(포장);
        this.피킹Service = 피킹Service;
    }

    public 출고재고조회ViewModel 재고조회 { get; }
    public 출고포장ViewModel 포장 { get; }
    public IWarehousePickingBatchWorkspaceService 피킹Service { get; }
    public bool 처리중 => 재고조회.처리중 || 포장.처리중;

    public Task<bool> 초기화Async(CancellationToken cancellationToken = default)
        => 재고조회.조회Async(cancellationToken);
}

public sealed class 마트기사픽업PageViewModel : 창고PageViewModelBase
{
    public 마트기사픽업PageViewModel(
        창고작업세션상태ViewModel 세션,
        출고재고조회ViewModel 재고조회,
        출고운송인계ViewModel 운송인계)
        : base(
            세션,
            창고PageCodes.마트기사픽업,
            "마트 기사 픽업",
            창고운영ProfileCodes.마트도심)
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
