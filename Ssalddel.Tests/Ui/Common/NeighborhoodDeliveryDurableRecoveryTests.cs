using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed partial class NeighborhoodDeliveryViewModelTests
{
    [Fact]
    public async Task 작성페이지종료후_미확정원문과번호를복원하고_GET후에만_명시재전송한다()
    {
        var store = new VolatileWorkflowRecoveryStore(); var client = new FakeClient { Create = _ => throw new IOException("lost response") };
        var first = await DurableReady(client, store); var id = first.Draft.ClientRequestId;
        await first.SubmitAsync(); first.Dispose();
        Assert.Equal("", first.Draft.Pickup.연락처.전화번호);
        using var restored = new NeighborhoodDeliveryAuthoringViewModel(client, new User("owner"), recoveryStore: store);
        Assert.True(await restored.RestoreAsync()); Assert.Equal(id, restored.Draft.ClientRequestId);
        Assert.Equal("시험로 10", restored.Draft.Pickup.주소.도로명주소); Assert.True(restored.NeedsSubmissionReview);
        Assert.Single(client.Created); Assert.Equal(id, Assert.Single(client.SubmissionReads));
        restored.Draft.Pickup.주소.도로명주소 = "프로그램으로 바뀐 주소";
        client.Create = _ => Task.FromResult<NeighborhoodDeliveryResponse?>(new() { RequestId = "request" });
        Assert.Equal("request", await restored.SubmitAsync()); Assert.Equal(2, client.Created.Count);
        Assert.All(client.Created, request => Assert.Equal(id, request.ClientRequestId));
        Assert.All(client.Created, request => Assert.Equal("시험로 10", request.Pickup.주소.도로명주소));
        Assert.Null(await store.LoadAsync("owner", "neighborhood-delivery-create.v1", default));
    }
    [Fact]
    public async Task 원장저장뒤_배차접수실패는_복원GET만으로완료하지않고_동일명시POST로이어간다()
    {
        var store = new VolatileWorkflowRecoveryStore(); var client = new FakeClient { Create = _ => throw new IOException("queue registration failed after source saved") };
        var first = await DurableReady(client, store); var id = first.Draft.ClientRequestId; await first.SubmitAsync(); first.Dispose();
        client.SubmissionRead = _ => Task.FromResult<NeighborhoodDeliveryResponse?>(new()
            { RequestId = "saved-source", ProposalStateCode = NeighborhoodDeliveryProposalStates.RegistrationPending });
        using var restored = new NeighborhoodDeliveryAuthoringViewModel(client, new User("owner"), recoveryStore: store);
        Assert.True(await restored.RestoreAsync()); Assert.True(restored.RegistrationRecoveryPending);
        Assert.Null(restored.Submitted); Assert.True(restored.NeedsSubmissionReview); Assert.False(restored.RecoveryCheckedMissing);
        Assert.Single(client.Created); Assert.NotNull(await store.LoadAsync("owner", "neighborhood-delivery-create.v1", default));
        client.Create = _ => Task.FromResult<NeighborhoodDeliveryResponse?>(new()
            { RequestId = "saved-source", ProposalStateCode = NeighborhoodDeliveryProposalStates.Queued });
        Assert.Equal("saved-source", await restored.SubmitAsync()); Assert.Equal(2, client.Created.Count);
        Assert.All(client.Created, request => Assert.Equal(id, request.ClientRequestId));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(client.Created[0]), System.Text.Json.JsonSerializer.Serialize(client.Created[1]));
        Assert.False(restored.RegistrationRecoveryPending); Assert.Null(await store.LoadAsync("owner", "neighborhood-delivery-create.v1", default));
    }

    [Fact]
    public async Task 원문만료후_저장됐지만배차미완료인의뢰는_미접수나완료로승격하지않는다()
    {
        var clock = new RecoveryClock(); var store = new VolatileWorkflowRecoveryStore(clock);
        var client = new FakeClient { Create = _ => throw new IOException("source saved but queue pending") };
        var first = await DurableReady(client, store); var id = first.Draft.ClientRequestId; await first.SubmitAsync(); first.Dispose();
        clock.Now = DateTimeOffset.UtcNow.AddDays(4);
        client.SubmissionRead = _ => Task.FromResult<NeighborhoodDeliveryResponse?>(new()
            { RequestId = "saved-source", ProposalStateCode = NeighborhoodDeliveryProposalStates.RegistrationPending });
        using var restored = new NeighborhoodDeliveryAuthoringViewModel(client, new User("owner"), recoveryStore: store);
        await restored.RestoreAsync(); await restored.DiscardExpiredRecoveryAsync();
        Assert.True(restored.RecoveryExpired); Assert.True(restored.RegistrationRecoveryPending); Assert.False(restored.RecoveryCheckedMissing);
        Assert.Null(restored.Submitted); Assert.Equal("", restored.Draft.Pickup.주소.도로명주소);
        Assert.Null(await restored.SubmitAsync()); Assert.Single(client.Created);
        var record = await store.LoadAsync("owner", "neighborhood-delivery-create.v1", default);
        Assert.Equal(id, record!.RequestId); Assert.Null(record.PayloadJson);
    }

    [Fact]
    public async Task 저장된원접수_GET확인은_페이지복원이나명시확인에서_새POST를보내지않는다()
    {
        var store = new VolatileWorkflowRecoveryStore(); var client = new FakeClient { Create = _ => throw new IOException("lost response") };
        var first = await DurableReady(client, store); await first.SubmitAsync(); first.Dispose();
        client.SubmissionRead = _ => Task.FromResult<NeighborhoodDeliveryResponse?>(new() { RequestId = "already-saved" });
        using var restored = new NeighborhoodDeliveryAuthoringViewModel(client, new User("owner"), recoveryStore: store);
        await restored.RestoreAsync(); Assert.Equal("already-saved", restored.Submitted!.RequestId); Assert.Single(client.Created);
        Assert.Equal("already-saved", await restored.SubmitAsync()); Assert.Single(client.Created);
    }
    [Fact]
    public async Task 원결과조회실패는_새접수로우회하지않고_이전원문을유지한다()
    {
        var store = new VolatileWorkflowRecoveryStore(); var client = new FakeClient { Create = _ => throw new IOException("lost") };
        var first = await DurableReady(client, store); await first.SubmitAsync(); first.Dispose();
        client.SubmissionRead = _ => throw new IOException("offline");
        using var restored = new NeighborhoodDeliveryAuthoringViewModel(client, new User("owner"), recoveryStore: store);
        Assert.False(await restored.RestoreAsync()); Assert.Null(await restored.SubmitAsync());
        Assert.Single(client.Created); Assert.True(restored.NeedsSubmissionReview); Assert.False(restored.CanEditInput);
    }
    [Fact]
    public async Task 보호저장실패시_원배송POST를보내지않는다()
    {
        var client = new FakeClient(); using var model = await DurableReady(client, new FailingRecoveryStore());
        Assert.Null(await model.SubmitAsync()); Assert.Empty(client.Created); Assert.NotNull(model.Error);
    }
    [Fact]
    public async Task 다른계정은_이전계정의주소와미확정요청을복원하지않는다()
    {
        var store = new VolatileWorkflowRecoveryStore(); var client = new FakeClient { Create = _ => throw new IOException("lost") };
        var first = await DurableReady(client, store); await first.SubmitAsync(); first.Dispose();
        using var other = new NeighborhoodDeliveryAuthoringViewModel(client, new User("other"), recoveryStore: store);
        await other.RestoreAsync(); Assert.False(other.NeedsSubmissionReview); Assert.Equal("", other.Draft.Pickup.주소.도로명주소);
        Assert.Empty(client.SubmissionReads);
    }
    [Fact]
    public async Task 만료원문은삭제하고_원번호GET이실패하면_새작성으로넘기지않는다()
    {
        var clock = new RecoveryClock(); var store = new VolatileWorkflowRecoveryStore(clock);
        var client = new FakeClient { Create = _ => throw new IOException("lost") };
        var first = await DurableReady(client, store); await first.SubmitAsync(); var id = first.Draft.ClientRequestId; first.Dispose();
        clock.Now = DateTimeOffset.UtcNow.AddDays(4); client.SubmissionRead = _ => throw new IOException("offline");
        using var restored = new NeighborhoodDeliveryAuthoringViewModel(client, new User("owner"), recoveryStore: store);
        await restored.RestoreAsync(); Assert.True(restored.RecoveryExpired); Assert.Equal("", restored.Draft.Pickup.주소.도로명주소);
        await restored.DiscardExpiredRecoveryAsync(); Assert.True(restored.RecoveryExpired); Assert.Single(client.Created);
        var record = await store.LoadAsync("owner", "neighborhood-delivery-create.v1", default); Assert.Equal(id, record!.RequestId); Assert.Null(record.PayloadJson);
        client.SubmissionRead = _ => Task.FromResult<NeighborhoodDeliveryResponse?>(null);
        await restored.DiscardExpiredRecoveryAsync(); Assert.False(restored.RecoveryExpired); Assert.NotEqual(id, restored.Draft.ClientRequestId);
    }
    private static async Task<NeighborhoodDeliveryAuthoringViewModel> DurableReady(FakeClient client, IWorkflowRecoveryStore store)
    {
        var model = new NeighborhoodDeliveryAuthoringViewModel(client, new User("owner"), recoveryStore: store);
        model.Initialize(null); await model.RestoreAsync(); model.CollectionConsent = model.AgeConfirmed = true; await model.AcceptConsentAsync();
        model.Draft.CargoName = "시험 물건"; model.Draft.WeightKg = 1;
        foreach (var location in new[] { model.Draft.Pickup, model.Draft.Dropoff })
        { location.주소.도로명주소 = "시험로 10"; location.연락처.이름 = "시험 이용자"; location.연락처.전화번호 = "01000000000"; }
        await model.QuoteAsync(); model.DispatchRequested = model.DirectPaymentAgreed = true; return model;
    }
}

internal sealed class RecoveryClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Now;
}
internal sealed class FailingRecoveryStore : IWorkflowRecoveryStore
{
    public Task<WorkflowRecoveryRecord?> LoadAsync(string owner, string scope, CancellationToken ct) => Task.FromResult<WorkflowRecoveryRecord?>(null);
    public Task SaveAsync(WorkflowRecoveryRecord record, CancellationToken ct) => throw new IOException("protected storage unavailable");
    public Task RemoveAsync(string owner, string scope, Guid expectedRequestId, CancellationToken ct) => Task.CompletedTask;
}
