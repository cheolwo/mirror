using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

public sealed class RoleWorkspaceShipperCurrentR22Tests
{
    [Fact]
    public async Task ClosedSelectedRequestDoesNotBecomeCurrentWhenAllRequestsAreClosed()
    {
        var completed = new 화주운송의뢰응답 { 의뢰Id = "request-closed", 의뢰상태 = "접수", 운송상태 = "운송완료" };
        var cancelled = new 화주운송의뢰응답 { 의뢰Id = "request-cancelled", 의뢰상태 = "의뢰취소", 운송상태 = "배차대기" };
        var api = Api(completed, cancelled);
        var snapshot = await new ShipperWorkspaceAdapter(new(api)).LoadAsync(completed.의뢰Id, default);

        Assert.Equal(completed.의뢰Id, snapshot.SelectedId);
        Assert.All(snapshot.Items, item => Assert.False(item.IsCurrent));
        Assert.False(snapshot.Items.Any(item => item.IsCurrent));
        var start = Assert.Single(snapshot.EmptyActions!);
        Assert.Equal("create", start.Key);
        Assert.True(start.Enabled);
        Assert.False(start.RequiresConfirmation);
        Assert.Contains("/shipper/request", start.Route);
        AssertReadOnly(api);
    }

    [Fact]
    public async Task PendingRequestRemainsCurrentWhileClosedRequestSelectionAndRoutesStayPreserved()
    {
        var completed = new 화주운송의뢰응답
        {
            의뢰Id = "request-closed", 의뢰상태 = "접수", 운송상태 = "운송완료",
            픽업위도 = 37.57m, 픽업경도 = 127.01m, 하차위도 = 37.58m, 하차경도 = 127.02m
        };
        var pending = new 화주운송의뢰응답
        {
            의뢰Id = "request-pending", 의뢰상태 = "접수", 운송상태 = "배차대기",
            픽업위도 = 37.59m, 픽업경도 = 127.03m, 하차위도 = 37.60m, 하차경도 = 127.04m
        };
        var api = Api(completed, pending);
        var snapshot = await new ShipperWorkspaceAdapter(new(api)).LoadAsync(completed.의뢰Id, default);

        Assert.Equal(completed.의뢰Id, snapshot.SelectedId);
        var selected = Assert.Single(snapshot.Items, item => item.Id == snapshot.SelectedId);
        Assert.False(selected.IsCurrent);
        Assert.Equal(pending.의뢰Id, Assert.Single(snapshot.Items, item => item.IsCurrent).Id);
        Assert.True(snapshot.Items.Any(item => item.IsCurrent));
        Assert.All(snapshot.Items, item => Assert.Equal(2, item.Markers!.Count));
        Assert.Contains(completed.의뢰Id, Assert.Single(selected.Actions!, action => action.Key == "timeline").Route);
        Assert.Equal("배차대기", pending.운송상태);
        AssertReadOnly(api);
    }

    private static FoodRoleWorkspaceAdapterTests.Api Api(params 화주운송의뢰응답[] requests)
    {
        var api = new FoodRoleWorkspaceAdapterTests.Api();
        api.Reads["api/v1/shipper/requests?page=1&pageSize=200"] = requests;
        foreach (var request in requests) api.Reads["api/v1/shipper/requests/" + request.의뢰Id] = request;
        return api;
    }

    private static void AssertReadOnly(FoodRoleWorkspaceAdapterTests.Api api)
    {
        Assert.Equal(2, api.ReadRoles.Count);
        Assert.All(api.ReadRoles, role => Assert.Equal(RoleWorkspaceCatalog.Shipper, role));
        Assert.Empty(api.Writes);
    }
}
