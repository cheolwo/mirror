using System.Text.Json;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
namespace Ssalddel.Tests.Ui.Common;
public sealed partial class NeighborhoodDeliveryViewModelTests
{
    [Fact]
    public void 원장의인수완료는_배차확정대신_전달완료로보이며_수금은따로표시한다()
    {
        var response = new NeighborhoodDeliveryResponse { ProposalStateCode = NeighborhoodDeliveryProposalStates.Assigned,
            DispatchStatusCode = "인수완료", SettlementStatusCode = "현장수금예정" };
        Assert.Equal("전달 완료", NeighborhoodDeliveryPresentation.Stage(response));
        Assert.Equal("전달 완료", NeighborhoodDeliveryPresentation.Progress(response.DispatchStatusCode));
        Assert.Equal("기사에게 직접 현장 지급 예정", NeighborhoodDeliveryPresentation.Collection(response));
    }
    [Fact]
    public async Task 새화면에서_미확정배송을복구할때_견적없이_원입력을그대로재송신한다()
    {
        var client = new FakeClient(); using var first = await Ready(client);
        await first.QuoteAsync(); first.DispatchRequested = true; first.DirectPaymentAgreed = true;
        client.Create = _ => Task.FromException<NeighborhoodDeliveryResponse?>(new HttpRequestException("lost"));
        await first.SubmitAsync();
        var pending = JsonSerializer.Deserialize<NeighborhoodDeliveryRequest>(JsonSerializer.Serialize(Assert.Single(client.Created)))!;
        pending.CollaborationId = "work-1"; pending.DispatchMode = "hybrid"; pending.ExpectedTermsRevision = 1;
        // Optional dropoff window must remain absent through recovery.
        pending.Dropoff.시간창 = null;
        var collaborations = new CollaborationClient { Detail = new() { StableId = "work-1", DeliveryRegistrationPending = true,
            MyPendingDeliveryRequest = pending, TermsRevision = 1, Terms = new() { TransferMethod = "driver-delivery" } } };
        using var recovered = new NeighborhoodDeliveryAuthoringViewModel(client, new User("owner"), collaborations);
        await recovered.LoadCollaborationAsync("work-1"); Assert.True(recovered.NeedsSubmissionReview); Assert.Null(recovered.Quote);
        client.Created.Clear(); client.Create = _ => Task.FromResult<NeighborhoodDeliveryResponse?>(new() { RequestId = "original-1", CollaborationId = "work-1" });
        Assert.Equal("original-1", await recovered.SubmitAsync());
        Assert.Equal(JsonSerializer.Serialize(pending), JsonSerializer.Serialize(Assert.Single(client.Created)));
        Assert.Empty(collaborations.Commands);
    }
}
