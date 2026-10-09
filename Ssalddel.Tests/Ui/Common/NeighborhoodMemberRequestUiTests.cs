using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed partial class NeighborhoodDeliveryViewModelTests
{
    [Fact]
    public async Task 회원배송의뢰_취소는_서버권한과_명시확인을_모두요구한다()
    {
        var source = MemberSource(); var clients = new FakeClient { Read = _ => Task.FromResult<NeighborhoodDeliveryResponse?>(source) };
        var member = new NeighborhoodMemberDeliveryUiTests.Fixture();
        using var vm = new NeighborhoodMemberRequestViewModel(member, clients, new User("member"), member.Session);
        await vm.LoadAsync(source.RequestId); Assert.False(vm.CanCancel); Assert.False(await vm.CancelAsync());
        vm.CancellationAgreed = true; source.CanCancelMemberDelivery = false;
        Assert.False(vm.CanCancel); Assert.False(await vm.CancelAsync()); Assert.Empty(member.Cancellations);
    }
    [Fact]
    public async Task 회원배송의뢰_취소명령후_원의뢰의취소판본을_재조회한다()
    {
        var source = MemberSource(); var reads = 0;
        var clients = new FakeClient { Read = _ => { reads++; return Task.FromResult<NeighborhoodDeliveryResponse?>(source); } };
        var member = new NeighborhoodMemberDeliveryUiTests.Fixture { Cancellation = body => { source.MemberWorkStateCode = "Cancelled"; source.MemberWorkRevision = body.ExpectedRevision + 1; source.CanCancelMemberDelivery = false; return Task.CompletedTask; } };
        using var vm = new NeighborhoodMemberRequestViewModel(member, clients, new User("member"), member.Session);
        await vm.LoadAsync(source.RequestId); vm.CancellationAgreed = true; Assert.True(await vm.CancelAsync());
        Assert.Equal(2, reads); Assert.Equal(2, Assert.Single(member.Cancellations).ExpectedRevision); Assert.Null(vm.Pending);
        Assert.Contains("지급 확인은 별도", vm.Notice);
    }
    [Fact]
    public async Task 회원배송의뢰_취소응답유실은_재시작후_원의뢰의취소결과로_복구한다()
    {
        var source = MemberSource(); var clients = new FakeClient { Read = _ => Task.FromResult<NeighborhoodDeliveryResponse?>(source) };
        var member = new NeighborhoodMemberDeliveryUiTests.Fixture { Cancellation = body => { source.MemberWorkStateCode = "Cancelled"; source.MemberWorkRevision++; source.CanCancelMemberDelivery = false; return Task.FromException(new HttpRequestException()); } };
        using (var vm = new NeighborhoodMemberRequestViewModel(member, clients, new User("member"), member.Session))
        { await vm.LoadAsync(source.RequestId); vm.CancellationAgreed = true; Assert.False(await vm.CancelAsync()); Assert.Equal("request-cancel", vm.Pending?.Kind); }
        using var recovered = new NeighborhoodMemberRequestViewModel(member, clients, new User("member"), member.Session);
        await recovered.LoadAsync(source.RequestId); Assert.Null(recovered.Pending); Assert.Single(member.Cancellations); Assert.Equal("Cancelled", recovered.Source!.MemberWorkStateCode);
    }
    private static NeighborhoodDeliveryResponse MemberSource() => new()
    { RequestId = "member-source", MemberDeliveryRequested = true, MemberWorkRevision = 2, MemberWorkStateCode = "Assigned", CanCancelMemberDelivery = true };
}
