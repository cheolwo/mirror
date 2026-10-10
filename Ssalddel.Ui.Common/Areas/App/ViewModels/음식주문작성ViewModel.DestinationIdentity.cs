using Ssalddel.Contracts.Common.PublicData;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed partial class 음식주문작성ViewModel
{
    private FoodDeliveryDestinationIdentityRequest? _선택주소식별;
    private long _주소검색Generation;
    public string 주소검색어 { get; set; } = string.Empty;
    public bool 주소검색중 { get; private set; }
    public bool 주소검색완료 { get; private set; }
    public string? 주소검색오류 { get; private set; }
    public IReadOnlyList<RoadAddressItem> 주소검색결과 { get; private set; } = [];
    public bool 검색주소선택됨 => _선택주소식별 is not null;

    public async Task 주소검색Async(CancellationToken ct = default)
    {
        if (처리중 || 입력잠금 || 주소검색중) return;
        var keyword = 주소검색어.Trim();
        if (keyword.Length < 2)
        { 주소검색오류 = "도로명이나 건물명을 두 글자 이상 입력해 주세요."; OnPropertyChanged(string.Empty); return; }
        var generation = ++_주소검색Generation;
        주소검색중 = true; 주소검색완료 = false; 주소검색오류 = null; 주소검색결과 = []; OnPropertyChanged(string.Empty);
        try
        {
            var result = await service.주소검색Async(keyword, ct);
            ct.ThrowIfCancellationRequested();
            if (generation != _주소검색Generation) return;
            if (!result.Success) throw new InvalidOperationException("공공 주소 검색 실패");
            주소검색결과 = result.Items.Where(value => !string.IsNullOrWhiteSpace(value.RoadAddress)).ToArray();
            주소검색완료 = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        { if (generation == _주소검색Generation) 주소검색오류 = "주소 검색을 사용할 수 없습니다. 배달 주소를 직접 입력해 주문할 수 있습니다."; }
        finally { if (generation == _주소검색Generation) { 주소검색중 = false; OnPropertyChanged(string.Empty); } }
    }

    public void 검색주소선택(RoadAddressItem item)
    {
        if (처리중 || 입력잠금 || !주소검색결과.Contains(item)) return;
        주소 = item.RoadAddress;
        _선택주소식별 = string.IsNullOrWhiteSpace(item.BuildingManagementNo) || string.IsNullOrWhiteSpace(item.RoadNameManagementNo)
            ? null : new() { BuildingManagementNumber = item.BuildingManagementNo, RoadNameCode = item.RoadNameManagementNo };
        주소검색결과 = []; 주소검색완료 = false;
        요청내용변경됨(); OnPropertyChanged(string.Empty);
    }

    private void 주소식별초기화()
    {
        _선택주소식별 = null; ++_주소검색Generation; 주소검색중 = false; 주소검색완료 = false;
        주소검색결과 = []; 주소검색오류 = null; OnPropertyChanged(nameof(검색주소선택됨));
    }

    private FoodDeliveryDestinationIdentityRequest? 선택주소식별복사() => _선택주소식별 is null ? null : new()
    { BuildingManagementNumber = _선택주소식별.BuildingManagementNumber, RoadNameCode = _선택주소식별.RoadNameCode };
}
