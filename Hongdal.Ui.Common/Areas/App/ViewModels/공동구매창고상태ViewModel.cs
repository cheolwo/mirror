using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Inventory;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Contracts.Shipper.Request;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

/// <summary>
/// 입출고 화면의 기준 창고, 입고 요청과 재고 선택을 하위 ViewModel 사이에서 공유합니다.
/// </summary>
public class 입출고화면상태ViewModel : ObservableObject
{
    public 입출고화면상태ViewModel()
    {
    }

    public 입출고화면상태ViewModel(IHongdal현재사용자Context 현재사용자Context)
    {
        this.현재사용자Context = 현재사용자Context;
    }

    public IHongdal현재사용자Context? 현재사용자Context { get; }
    public 현재사용자Snapshot 현재사용자
        => 현재사용자Context?.현재사용자 ?? 현재사용자Snapshot.익명;
    private IReadOnlyList<창고요약응답> _창고목록 = [];
    private 창고요약응답? _선택된창고;
    private IReadOnlyList<창고사용자항목응답> _창고사용자목록 = [];
    private 창고사용자항목응답? _선택된창고사용자;
    private IReadOnlyList<입고요청항목응답> _입고요청목록 = [];
    private 입고요청항목응답? _선택된입고요청;
    private IReadOnlyList<입고상품항목응답> _최근입고상품목록 = [];
    private IReadOnlyList<재고항목응답> _재고목록 = [];
    private 재고항목응답? _선택된재고;
    private 창고작업결과응답? _최근입고작업결과;
    private 창고작업결과응답? _최근출고작업결과;
    private 화주운송의뢰응답? _최근운송의뢰;

    public IReadOnlyList<창고요약응답> 창고목록
    {
        get => _창고목록;
        private set => SetProperty(ref _창고목록, value);
    }

    public 창고요약응답? 선택된창고
    {
        get => _선택된창고;
        private set
        {
            if (!SetProperty(ref _선택된창고, value))
            {
                return;
            }

            창고사용자목록 = [];
            선택된창고사용자 = null;
            OnPropertyChanged(nameof(선택창고입고요청목록));
            OnPropertyChanged(nameof(선택창고재고목록));
        }
    }

    public IReadOnlyList<창고사용자항목응답> 창고사용자목록
    {
        get => _창고사용자목록;
        private set => SetProperty(ref _창고사용자목록, value);
    }

    public 창고사용자항목응답? 선택된창고사용자
    {
        get => _선택된창고사용자;
        private set => SetProperty(ref _선택된창고사용자, value);
    }

    public IReadOnlyList<입고요청항목응답> 입고요청목록
    {
        get => _입고요청목록;
        private set
        {
            if (SetProperty(ref _입고요청목록, value))
            {
                OnPropertyChanged(nameof(선택창고입고요청목록));
            }
        }
    }

    public 입고요청항목응답? 선택된입고요청
    {
        get => _선택된입고요청;
        private set => SetProperty(ref _선택된입고요청, value);
    }

    public IReadOnlyList<입고상품항목응답> 최근입고상품목록
    {
        get => _최근입고상품목록;
        private set => SetProperty(ref _최근입고상품목록, value);
    }

    public IReadOnlyList<재고항목응답> 재고목록
    {
        get => _재고목록;
        private set
        {
            if (SetProperty(ref _재고목록, value))
            {
                OnPropertyChanged(nameof(선택창고재고목록));
            }
        }
    }

    public 재고항목응답? 선택된재고
    {
        get => _선택된재고;
        private set => SetProperty(ref _선택된재고, value);
    }

    public 창고작업결과응답? 최근입고작업결과
    {
        get => _최근입고작업결과;
        private set => SetProperty(ref _최근입고작업결과, value);
    }

    public 창고작업결과응답? 최근출고작업결과
    {
        get => _최근출고작업결과;
        private set => SetProperty(ref _최근출고작업결과, value);
    }

    public 화주운송의뢰응답? 최근운송의뢰
    {
        get => _최근운송의뢰;
        private set => SetProperty(ref _최근운송의뢰, value);
    }

    public IReadOnlyList<입고요청항목응답> 선택창고입고요청목록
        => 선택된창고 is null
            ? 입고요청목록
            : 입고요청목록.Where(x => x.창고Id == 선택된창고.Id).ToArray();

    public IReadOnlyList<재고항목응답> 선택창고재고목록
        => 선택된창고 is null
            ? 재고목록
            : 재고목록.Where(x => x.창고Id == 선택된창고.Id).ToArray();

    public void 창고목록적용(IReadOnlyList<창고요약응답> warehouses)
    {
        ArgumentNullException.ThrowIfNull(warehouses);
        var selectedId = 선택된창고?.Id;
        창고목록 = warehouses;
        선택된창고 = warehouses.FirstOrDefault(x => x.Id == selectedId)
            ?? warehouses.FirstOrDefault(x => x.기본창고여부)
            ?? warehouses.FirstOrDefault();
        선택값정합성확인();
    }

    public void 창고저장적용(창고요약응답 warehouse)
    {
        ArgumentNullException.ThrowIfNull(warehouse);
        창고목록 = 창고목록.Where(x => x.Id != warehouse.Id).Append(warehouse).ToArray();
        선택된창고 = warehouse;
        선택값정합성확인();
    }

    public void 창고삭제적용(long warehouseId)
    {
        창고목록 = 창고목록.Where(x => x.Id != warehouseId).ToArray();
        if (선택된창고?.Id == warehouseId)
        {
            선택된창고 = 창고목록.FirstOrDefault(x => x.기본창고여부) ?? 창고목록.FirstOrDefault();
        }
        선택값정합성확인();
    }

    public bool 창고선택(long warehouseId)
    {
        var warehouse = 창고목록.FirstOrDefault(x => x.Id == warehouseId);
        if (warehouse is null)
        {
            return false;
        }

        선택된창고 = warehouse;
        선택값정합성확인();
        return true;
    }

    public void 창고사용자목록적용(IReadOnlyList<창고사용자항목응답> users)
    {
        ArgumentNullException.ThrowIfNull(users);
        창고사용자목록 = users;
        선택된창고사용자 = users.FirstOrDefault(x => x.Id == 선택된창고사용자?.Id)
            ?? users.FirstOrDefault();
    }

    public void 창고사용자저장적용(창고사용자항목응답 user)
    {
        ArgumentNullException.ThrowIfNull(user);
        창고사용자목록 = 창고사용자목록.Where(x => x.Id != user.Id).Append(user).ToArray();
        선택된창고사용자 = user;
    }

    public bool 창고사용자선택(long warehouseUserId)
    {
        var user = 창고사용자목록.FirstOrDefault(x => x.Id == warehouseUserId);
        if (user is null)
        {
            return false;
        }

        선택된창고사용자 = user;
        return true;
    }

    public void 창고사용자삭제적용(long warehouseUserId)
    {
        창고사용자목록 = 창고사용자목록.Where(x => x.Id != warehouseUserId).ToArray();
        if (선택된창고사용자?.Id == warehouseUserId)
        {
            선택된창고사용자 = 창고사용자목록.FirstOrDefault();
        }
    }

    public void 입고목록적용(IReadOnlyList<입고요청항목응답> inbounds)
    {
        ArgumentNullException.ThrowIfNull(inbounds);
        var selectedId = 선택된입고요청?.Id;
        입고요청목록 = inbounds;
        선택된입고요청 = 선택창고입고요청목록.FirstOrDefault(x => x.Id == selectedId)
            ?? 선택창고입고요청목록.FirstOrDefault();
    }

    public void 입고요청저장적용(입고요청항목응답 inbound)
    {
        ArgumentNullException.ThrowIfNull(inbound);
        입고요청목록 = 입고요청목록.Where(x => x.Id != inbound.Id).Append(inbound).ToArray();
        선택된입고요청 = inbound;
    }

    public void 입고요청삭제적용(long inboundId)
    {
        입고요청목록 = 입고요청목록.Where(x => x.Id != inboundId).ToArray();
        if (선택된입고요청?.Id == inboundId)
        {
            선택된입고요청 = 선택창고입고요청목록.FirstOrDefault();
        }
    }

    public bool 입고요청선택(long inboundId)
    {
        var inbound = 선택창고입고요청목록.FirstOrDefault(x => x.Id == inboundId);
        if (inbound is null)
        {
            return false;
        }

        선택된입고요청 = inbound;
        return true;
    }

    public void 입고완료적용(IReadOnlyList<입고상품항목응답> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        최근입고상품목록 = items;
    }

    public void 재고목록적용(IReadOnlyList<재고항목응답> inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var selectedId = 선택된재고?.입고상품Id;
        재고목록 = inventory;
        선택된재고 = 선택창고재고목록.FirstOrDefault(x => x.입고상품Id == selectedId)
            ?? 선택창고재고목록.FirstOrDefault();
    }

    public bool 재고선택(long inboundItemId)
    {
        var inventory = 선택창고재고목록.FirstOrDefault(x => x.입고상품Id == inboundItemId);
        if (inventory is null)
        {
            return false;
        }

        선택된재고 = inventory;
        return true;
    }

    public void 입고작업결과적용(창고작업결과응답 result)
    {
        ArgumentNullException.ThrowIfNull(result);
        최근입고작업결과 = result;
    }

    public void 출고작업결과적용(창고작업결과응답 result)
    {
        ArgumentNullException.ThrowIfNull(result);
        최근출고작업결과 = result;
    }

    public void 운송의뢰적용(화주운송의뢰응답 result)
    {
        ArgumentNullException.ThrowIfNull(result);
        최근운송의뢰 = result;
    }

    private void 선택값정합성확인()
    {
        var filteredInbounds = 선택창고입고요청목록;
        if (선택된입고요청 is null
            || !filteredInbounds.Any(x => x.Id == 선택된입고요청.Id))
        {
            선택된입고요청 = filteredInbounds.FirstOrDefault();
        }

        var filteredInventory = 선택창고재고목록;
        if (선택된재고 is null
            || !filteredInventory.Any(x => x.입고상품Id == 선택된재고.입고상품Id))
        {
            선택된재고 = filteredInventory.FirstOrDefault();
        }
    }
}

/// <summary>
/// 기존 공동구매 화면이 공통 입출고 상태를 그대로 사용할 수 있게 하는 호환 형식입니다.
/// </summary>
public sealed class 공동구매창고상태ViewModel : 입출고화면상태ViewModel
{
    public 공동구매창고상태ViewModel()
    {
    }

    public 공동구매창고상태ViewModel(IHongdal현재사용자Context 현재사용자Context)
        : base(현재사용자Context)
    {
    }
}
