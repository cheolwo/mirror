using System.Reflection;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.WebApp.Services;
using SsalddelApp.Models.Shipper;

namespace Ssalddel.Tests.Ui.Common;

public sealed class ShipperHandlingTimeR77Tests
{
    [Fact]
    public void 초안과접수고정복사_App_Web은명시0과작업시간을같이보낸다()
    {
        var vm = new 운송의뢰작성ViewModel { 상차작업시간분 = 0m, 하차작업시간분 = 6.5m };
        var restored = new 운송의뢰작성ViewModel(); restored.ApplyDraft(vm.ToDraft());
        var frozen = restored.접수입력고정복사();
        vm.하차작업시간분 = 99m;
        Assert.Equal(6.5m, frozen.하차작업시간분);
        var pickup = ShipperRequestHandoffMapper.CreatePickup(frozen.ToDraft());
        var dropoff = ShipperRequestHandoffMapper.CreateDropoff(frozen.ToDraft());
        var web = (화주운송의뢰생성요청)typeof(화주운송의뢰등록Service)
            .GetMethod("ToCreateRequest", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [frozen, "r77-synthetic", null, ""])!;
        Assert.Equal(0m, pickup.작업시간분);
        Assert.Equal(6.5m, dropoff.작업시간분);
        Assert.Equal(pickup.작업시간분, web.픽업!.작업시간분);
        Assert.Equal(dropoff.작업시간분, web.하차!.작업시간분);
        Assert.Equal(6.5m, ShipperRequestHandoffMapper.Copy(dropoff)!.작업시간분);
        restored.Reset(); Assert.Null(restored.상차작업시간분); Assert.Null(restored.하차작업시간분);
    }

    [Fact]
    public void 누락과음수와명시0을구분한다()
    {
        var vm = new 운송의뢰작성ViewModel();
        Assert.False(vm.작업시간확인됨);
        Assert.Null(vm.작업시간오류);
        Assert.Null(ShipperRequestHandoffMapper.CreatePickup(vm.ToDraft()).작업시간분);
        vm.상차작업시간분 = -1m; Assert.NotNull(vm.작업시간오류);
        vm.상차작업시간분 = vm.하차작업시간분 = 0m;
        Assert.True(vm.작업시간확인됨); Assert.Null(vm.작업시간오류);
    }
}
