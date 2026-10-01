using Hongdal.Contracts.Common.Warehouse;
using WarehouseManagerApp.Services;

namespace WarehouseManagerApp.ViewModels.Warehouse;

public enum 창고페이지메시지수준
{
    안내,
    성공,
    경고,
    오류
}

public sealed class 창고피킹배치PageViewModel : 창고PageViewModelBase
{
    private readonly IWarehousePickingBatchWorkspaceService _피킹Service;
    private IReadOnlyList<WarehousePickingWarehouseOption> _창고옵션목록 = [];
    private IReadOnlyList<WarehousePickingTaskItem> _배정작업목록 = [];
    private IReadOnlyList<WarehousePickingTaskItem> _적재대작업목록 = [];
    private WarehousePickingTaskItem? _선택작업;
    private long _선택창고Id = 10;
    private 피킹포장처리방식 _선택처리방식 = 피킹포장처리방식.피킹포장분리;
    private bool _상품Barcode검증필수 = true;
    private string _적재대Barcode = "LOC-A-01-02";
    private string _상품Barcode = string.Empty;
    private int _피킹수량 = 1;
    private string? _옵션메시지;
    private string? _스캔메시지;
    private 창고페이지메시지수준 _옵션메시지수준;
    private 창고페이지메시지수준 _스캔메시지수준;
    private bool _처리중;

    public 창고피킹배치PageViewModel(
        창고작업세션상태ViewModel 세션,
        IWarehousePickingBatchWorkspaceService 피킹Service)
        : base(
            세션,
            창고PageCodes.일반출고,
            "피킹 배치",
            창고운영ProfileCodes.일반입출고)
    {
        _피킹Service = 피킹Service;
    }

    public IReadOnlyList<WarehousePickingWarehouseOption> 창고옵션목록
    {
        get => _창고옵션목록;
        private set => SetProperty(ref _창고옵션목록, value);
    }

    public IReadOnlyList<WarehousePickingTaskItem> 배정작업목록
    {
        get => _배정작업목록;
        private set => SetProperty(ref _배정작업목록, value);
    }

    public IReadOnlyList<WarehousePickingTaskItem> 적재대작업목록
    {
        get => _적재대작업목록;
        private set => SetProperty(ref _적재대작업목록, value);
    }

    public WarehousePickingTaskItem? 선택작업
    {
        get => _선택작업;
        private set => SetProperty(ref _선택작업, value);
    }

    public long 선택창고Id
    {
        get => _선택창고Id;
        private set
        {
            if (!SetProperty(ref _선택창고Id, value))
            {
                return;
            }

            OnPropertyChanged(nameof(선택창고명));
            OnPropertyChanged(nameof(선택창고옵션));
        }
    }

    public 피킹포장처리방식 선택처리방식
    {
        get => _선택처리방식;
        set => SetProperty(ref _선택처리방식, value);
    }

    public bool 상품Barcode검증필수
    {
        get => _상품Barcode검증필수;
        set => SetProperty(ref _상품Barcode검증필수, value);
    }

    public string 적재대Barcode
    {
        get => _적재대Barcode;
        set => SetProperty(ref _적재대Barcode, value ?? string.Empty);
    }

    public string 상품Barcode
    {
        get => _상품Barcode;
        set => SetProperty(ref _상품Barcode, value ?? string.Empty);
    }

    public int 피킹수량
    {
        get => _피킹수량;
        set => SetProperty(ref _피킹수량, Math.Max(0, value));
    }

    public string? 옵션메시지
    {
        get => _옵션메시지;
        private set => SetProperty(ref _옵션메시지, value);
    }

    public string? 스캔메시지
    {
        get => _스캔메시지;
        private set => SetProperty(ref _스캔메시지, value);
    }

    public 창고페이지메시지수준 옵션메시지수준
    {
        get => _옵션메시지수준;
        private set => SetProperty(ref _옵션메시지수준, value);
    }

    public 창고페이지메시지수준 스캔메시지수준
    {
        get => _스캔메시지수준;
        private set => SetProperty(ref _스캔메시지수준, value);
    }

    public bool 처리중
    {
        get => _처리중;
        private set => SetProperty(ref _처리중, value);
    }

    public string 선택창고명
        => 선택창고옵션?.WarehouseName ?? "창고";

    public WarehousePickingWarehouseOption? 선택창고옵션
        => 창고옵션목록.FirstOrDefault(option => option.WarehouseId == 선택창고Id);

    public async Task<bool> 초기화Async(CancellationToken cancellationToken = default)
    {
        if (처리중)
        {
            return false;
        }

        처리중 = true;
        try
        {
            일반입출고Profile확인();
            창고옵션목록 = await _피킹Service.GetWarehouseOptionsAsync(cancellationToken);
            var first = 창고옵션목록.FirstOrDefault();
            if (first is not null)
            {
                창고옵션적용(first);
            }

            await 배정작업조회Async(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            스캔메시지수준 = 창고페이지메시지수준.오류;
            스캔메시지 = ex.Message;
            return false;
        }
        finally
        {
            처리중 = false;
        }
    }

    public async Task 창고변경Async(long warehouseId, CancellationToken cancellationToken = default)
    {
        선택창고Id = warehouseId;
        var option = 선택창고옵션;
        if (option is not null)
        {
            창고옵션적용(option);
        }

        적재대작업목록 = [];
        선택작업 = null;
        스캔메시지 = null;
        await 배정작업조회Async(cancellationToken);
    }

    public async Task 옵션저장Async(CancellationToken cancellationToken = default)
    {
        try
        {
            var option = await _피킹Service.UpdateWarehouseOptionAsync(
                선택창고Id,
                선택처리방식,
                상품Barcode검증필수,
                cancellationToken);
            창고옵션목록 = 창고옵션목록
                .Select(item => item.WarehouseId == option.WarehouseId ? option : item)
                .ToArray();
            OnPropertyChanged(nameof(선택창고명));
            OnPropertyChanged(nameof(선택창고옵션));
            옵션메시지수준 = 창고페이지메시지수준.성공;
            옵션메시지 = $"{option.WarehouseName} 옵션을 저장했습니다.";
            await 배정작업조회Async(cancellationToken);
        }
        catch (Exception ex)
        {
            옵션메시지수준 = 창고페이지메시지수준.오류;
            옵션메시지 = ex.Message;
        }
    }

    public async Task 적재대조회Async(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _피킹Service.ScanRackAsync(
                선택창고Id,
                적재대Barcode,
                cancellationToken);
            적재대작업목록 = result.Items;
            작업선택적용(적재대작업목록.FirstOrDefault());
            스캔메시지수준 = result.IsSuccess
                ? 창고페이지메시지수준.성공
                : 창고페이지메시지수준.경고;
            스캔메시지 = result.Message;
            await 배정작업조회Async(cancellationToken);
        }
        catch (Exception ex)
        {
            스캔메시지수준 = 창고페이지메시지수준.오류;
            스캔메시지 = ex.Message;
        }
    }

    public void 작업선택(WarehousePickingTaskItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        작업선택적용(item);
    }

    public async Task 피킹완료Async(CancellationToken cancellationToken = default)
    {
        if (선택작업 is null)
        {
            return;
        }

        try
        {
            var result = await _피킹Service.CompletePickAsync(
                선택작업.TaskKey,
                적재대Barcode,
                상품Barcode,
                피킹수량,
                cancellationToken);
            스캔메시지수준 = result.IsSuccess
                ? 창고페이지메시지수준.성공
                : 창고페이지메시지수준.오류;
            스캔메시지 = result.Message;
            await 배정작업조회Async(cancellationToken);

            var rackResult = await _피킹Service.ScanRackAsync(
                선택창고Id,
                적재대Barcode,
                cancellationToken);
            적재대작업목록 = rackResult.Items;
            작업선택적용(적재대작업목록.FirstOrDefault());
        }
        catch (Exception ex)
        {
            스캔메시지수준 = 창고페이지메시지수준.오류;
            스캔메시지 = ex.Message;
        }
    }

    private async Task 배정작업조회Async(CancellationToken cancellationToken)
        => 배정작업목록 = await _피킹Service.GetAssignedTasksAsync(선택창고Id, cancellationToken);

    private void 창고옵션적용(WarehousePickingWarehouseOption option)
    {
        선택창고Id = option.WarehouseId;
        선택처리방식 = option.Mode;
        상품Barcode검증필수 = option.IsBarcodeRequired;
    }

    private void 작업선택적용(WarehousePickingTaskItem? item)
    {
        선택작업 = item;
        상품Barcode = item?.ProductBarcode ?? string.Empty;
        피킹수량 = item is null ? 1 : Math.Min(1, item.RemainingQuantity);
    }

    private void 일반입출고Profile확인()
    {
        if (!string.Equals(
                세션.운영ProfileCode,
                창고운영ProfileCodes.일반입출고,
                StringComparison.OrdinalIgnoreCase))
        {
            세션.운영Profile설정(창고운영ProfileCodes.일반입출고);
        }
    }
}
