using Ssalddel.Contracts.Common.PublicData;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class FoodDestinationIdentityUiTests
{
    [Fact]
    public async Task OfficialSelectionCarriesIndependentIdentityAndDetailedUnitDoesNotChangePlace()
    {
        var service = new Service(); var vm = Ready(service);
        vm.주소검색어 = "도로 1"; await vm.주소검색Async(); vm.검색주소선택(Assert.Single(vm.주소검색결과));
        vm.상세주소 = "비공개 상세";
        Assert.True(await vm.등록Async());
        var request = service.Request!;
        Assert.Equal("building-1", request.DestinationIdentity!.BuildingManagementNumber);
        Assert.Equal("road-1", request.DestinationIdentity.RoadNameCode);
        Assert.Null(request.DestinationIdentity.ApartmentComplexCode);
        Assert.Equal("비공개 상세", request.수령인정보.상세주소);
        vm.주소 = "다른 배달 주소";
        Assert.True(await vm.등록Async()); Assert.Null(service.Request!.DestinationIdentity);
        Assert.Equal("building-1", request.DestinationIdentity.BuildingManagementNumber);
    }

    [Fact]
    public async Task ResetAndUnselectedOrIncompleteSearchResultsCannotClaimConfirmedIdentity()
    {
        var service = new Service(); var vm = Ready(service); vm.주소검색어 = "도로 1";
        vm.검색주소선택(new() { RoadAddress = "임의 주소", BuildingManagementNo = "fake", RoadNameManagementNo = "fake" });
        Assert.Equal("직접 입력 주소", vm.주소);
        await vm.주소검색Async(); vm.검색주소선택(Assert.Single(vm.주소검색결과));
        vm.새요청준비(); Assert.False(vm.검색주소선택됨); Assert.Empty(vm.주소검색결과); Assert.Empty(vm.주소검색어);
        vm = Ready(service); service.Response = new() { Success = true, Items = [new() { RoadAddress = "도로 1" }] };
        vm.주소검색어 = "도로 1"; await vm.주소검색Async(); vm.검색주소선택(Assert.Single(vm.주소검색결과));
        Assert.True(await vm.등록Async()); Assert.Null(service.Request!.DestinationIdentity);
    }

    [Fact]
    public async Task SearchFailureKeepsManualOrderingAndNeverTreatsMissingIdentityAsZero()
    {
        var service = new Service { Search = (_, _) => throw new HttpRequestException("no key") };
        var vm = Ready(service); vm.주소검색어 = "도로 1"; await vm.주소검색Async();
        Assert.Contains("직접 입력", vm.주소검색오류); Assert.False(vm.검색주소선택됨);
        Assert.True(await vm.등록Async()); Assert.Null(service.Request!.DestinationIdentity);
    }

    [Fact]
    public async Task LateSearchAfterAccountResetDoesNotRestoreOldAddresses()
    {
        var completion = new TaskCompletionSource<PublicDataLookupResponse<RoadAddressItem>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new Service { Search = (_, _) => completion.Task }; var vm = Ready(service); vm.주소검색어 = "도로 1";
        var pending = vm.주소검색Async(); vm.새요청준비(); completion.SetResult(service.Response); await pending;
        Assert.Empty(vm.주소검색결과); Assert.False(vm.주소검색중); Assert.False(vm.검색주소선택됨);
    }

    private static 음식주문작성ViewModel Ready(Service service)
    {
        var vm = new 음식주문작성ViewModel(service);
        vm.음식점설정(new() { 음식점 = new() { Id = 1, 주문가능여부 = true }, 메뉴목록 = [new() { Id = 1, 메뉴명 = "메뉴 1", 판매가 = 4000 }] });
        vm.메뉴수량변경(1, 1); vm.수령인명 = "수령인 1"; vm.연락처 = "연락처 1"; vm.주소 = "직접 입력 주소"; return vm;
    }
    private sealed class Service : I주문자음식주문쓰기Service
    {
        public 음식주문등록요청? Request { get; private set; }
        public PublicDataLookupResponse<RoadAddressItem> Response { get; set; } = new() { Success = true,
            Items = [new() { RoadAddress = "도로 1", BuildingManagementNo = "building-1", RoadNameManagementNo = "road-1" }] };
        public Func<string, CancellationToken, Task<PublicDataLookupResponse<RoadAddressItem>>>? Search { get; set; }
        public Task<PublicDataLookupResponse<RoadAddressItem>> 주소검색Async(string keyword, CancellationToken ct = default)
            => Search is null ? Task.FromResult(Response) : Search(keyword, ct);
        public Task<음식주문응답> 등록Async(음식주문등록요청 request, CancellationToken ct = default)
        { Request = request; return Task.FromResult(new 음식주문응답 { 주문번호 = "order-a" }); }
    }
}
