using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Settings;
using Ssalddel.Tests.Driver;
using Ssalddel.Tests.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Queue;

namespace Ssalddel.Tests.Services.Dispatch.Queue;

public sealed partial class 음식배달배차업무정책Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 실제음식후보선정은업무참여설정을확인하고미선택사유를남긴다(bool selected)
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([
            Snapshot("shared-driver", 기사앱식별자.FoodDeliveryDriverApp, 37.501m, 127m, now, 0)]);
        var spaces = await CreateFoodSpaceStoreAsync(("shared-driver", 37.501m, 127m));
        var admission = new SharedDriverAdapterAdmissionProbe { Deny = !selected };
        var policy = new 음식배달배차업무정책(states, new FakeRejectedRequestStore(), new StraightLineRouteService(),
            spaces, new FakeFoodOrderStore(), Options.Create(new 배차큐정책Options()), new TestDispatchAvailability(),
            sharedAdmission: admission);

        var trace = await policy.후보검토Async(Queue());
        Assert.Equal(selected, trace.선정후보 is not null);
        Assert.Contains("Participation:" + DriverWorkKinds.FoodDelivery, admission.Calls);
        if (!selected)
            Assert.Contains(trace.후보목록, x => x.판정Code == "SharedDriverWorkProbeDenied");
    }
}
