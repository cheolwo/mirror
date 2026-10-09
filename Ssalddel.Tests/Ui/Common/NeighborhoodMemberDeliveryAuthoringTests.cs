using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Tests.Ui.Common;

public sealed partial class NeighborhoodDeliveryViewModelTests
{
    [Fact]
    public async Task 회원배송_정보제공확인이없으면_확정할수없고_확인후에도_기존견적은유효하다()
    {
        var client = new FakeClient(); using var vm = await Ready(client);
        vm.MemberDeliveryRequested = true; await vm.QuoteAsync(); vm.DispatchRequested = true; vm.DirectPaymentAgreed = true;
        Assert.True(vm.HasCurrentQuote); Assert.False(vm.CanConfirmCurrentQuote); Assert.Null(await vm.SubmitAsync()); Assert.Empty(client.Created);
        vm.Draft.MemberDeliveryDisclosureAgreed = true;
        Assert.True(vm.CanConfirmCurrentQuote); Assert.True(vm.HasCurrentQuote); Assert.Equal("request", await vm.SubmitAsync());
        var body = Assert.Single(client.Created);
        Assert.True(body.MemberDeliveryRequested); Assert.True(body.MemberDeliveryDisclosureAgreed);
        Assert.Equal(NeighborhoodMemberDeliveryRoutes.NoticeVersion, body.MemberDeliveryNoticeVersion);
        Assert.Equal(4000, body.AgreedFareKrw); Assert.Null(body.AutoFulfillment); Assert.Null(body.FulfillmentSelection);
    }

    [Fact]
    public async Task 회원배송_조건을변경하면_기존정보제공확인을_다시받는다()
    {
        var client = new FakeClient(); using var vm = await Ready(client);
        vm.MemberDeliveryRequested = true; vm.Draft.MemberDeliveryDisclosureAgreed = true;
        vm.Draft.WeightKg = 2; vm.InputChanged(); Assert.False(vm.Draft.MemberDeliveryDisclosureAgreed);
        vm.MemberDeliveryRequested = false; Assert.Null(vm.Draft.MemberDeliveryNoticeVersion);
    }
}
