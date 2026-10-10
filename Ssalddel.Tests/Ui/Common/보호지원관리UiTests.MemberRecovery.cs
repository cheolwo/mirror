using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed partial class 보호지원관리UiTests
{
    [Theory]
    [InlineData("reviewing", 보호지원출처Codes.생활배송)]
    [InlineData("closed", 보호지원출처Codes.음식주문)]
    public async Task 종결된생활배송사건에서만별도복구를선택한다(string status, string source)
    {
        var client = RecoveryClient(); client.DetailFactory = id => Task.FromResult(new 보호지원CaseResponse
        { CaseId = id, Kind = 보호지원종류Codes.분쟁, StatusCode = status, SourceKind = source, SourceId = "delivery-a" });
        using var model = new 보호지원관리ViewModel(client, () => "admin"); await model.LoadAsync("case-a");
        await model.LoadRecoveryAsync(); await model.SubmitRecoveryAsync();
        Assert.False(model.CanLoadRecovery); Assert.Equal(0, client.RecoveryReads); Assert.Empty(client.RecoveryCommands);
    }

    [Fact]
    public async Task 사건종결은배송을자동변경하지않고명시확인후별도복구를조회한다()
    {
        var client = RecoveryClient(); NeighborhoodMemberRecoveryRequest? receipt = null;
        client.RecoveryFactory = (_, request) => Task.FromResult(RecoveryContext(request, receipt));
        client.RecoveryCommand = (_, request) => { receipt = request; return Task.FromResult(new NeighborhoodMemberRecoveryResponse
            { RequestId = "delivery-a", Revision = 8, StateCode = "Assigned" }); };
        using var model = new 보호지원관리ViewModel(client, () => "admin"); await model.LoadAsync("case-a");
        Assert.Empty(client.RecoveryCommands); await model.LoadRecoveryAsync();
        await model.SubmitRecoveryAsync(); Assert.Empty(client.RecoveryCommands);
        model.RecoveryConfirmed = true; await model.SubmitRecoveryAsync();
        var command = Assert.Single(client.RecoveryCommands); Assert.Equal(7, command.ExpectedRevision);
        Assert.Equal("case-a", command.SupportCaseId); Assert.Equal("Resume", command.ResolutionCode);
        Assert.False(model.HasPending); Assert.Equal("Assigned", model.Recovery!.StateCode);
        Assert.Contains("지급 완료는 별개", model.Notice); Assert.Empty(client.Commands);
    }

    [Fact]
    public async Task 복구저장후응답유실은원요청영수증조회만으로완료한다()
    {
        var client = RecoveryClient(); NeighborhoodMemberRecoveryRequest? receipt = null;
        client.RecoveryFactory = (_, request) => Task.FromResult(RecoveryContext(request, receipt));
        client.RecoveryCommand = (_, request) => { receipt = request; throw new HttpRequestException("stored then lost"); };
        using var model = new 보호지원관리ViewModel(client, () => "admin"); await model.LoadAsync("case-a");
        await model.LoadRecoveryAsync(); model.RecoveryConfirmed = true; await model.SubmitRecoveryAsync();
        Assert.True(model.HasRecoveryPending); await model.CheckRecoveryAsync();
        Assert.False(model.HasRecoveryPending); Assert.Single(client.RecoveryCommands); Assert.NotNull(model.Notice);
    }

    [Fact]
    public async Task 영수증없이같은상태가된배송은원복구성공으로표시하지않는다()
    {
        var client = RecoveryClient(); var changed = false;
        client.RecoveryFactory = (_, request) => Task.FromResult(changed
            ? new NeighborhoodMemberRecoveryContextResponse { RequestId = "delivery-a", SupportCaseId = "case-a", Revision = 8, StateCode = "Assigned" }
            : RecoveryContext(request));
        client.RecoveryCommand = (_, _) => { changed = true; throw new HttpRequestException(); };
        using var model = new 보호지원관리ViewModel(client, () => "admin"); await model.LoadAsync("case-a");
        await model.LoadRecoveryAsync(); model.RecoveryConfirmed = true; await model.SubmitRecoveryAsync();
        await model.CheckRecoveryAsync(); Assert.False(model.HasRecoveryPending);
        Assert.Contains("성공은 확인되지", model.Error); Assert.Null(model.Notice); Assert.Single(client.RecoveryCommands);
    }

    [Fact]
    public async Task 응답유실후새제출호출은원복구요청을덮어쓰지않는다()
    {
        var client = RecoveryClient();
        client.RecoveryCommand = (_, _) => throw new HttpRequestException("response lost");
        using var model = new 보호지원관리ViewModel(client, () => "admin"); await model.LoadAsync("case-a");
        await model.LoadRecoveryAsync(); model.RecoveryConfirmed = true; await model.SubmitRecoveryAsync();
        var original = Assert.Single(client.RecoveryCommands); var reads = client.RecoveryReads;
        model.RecoveryResolution = "CancelBeforePickup"; model.RecoveryConfirmed = true;
        Assert.False(model.CanSubmitRecovery); await model.SubmitRecoveryAsync();
        Assert.True(model.HasRecoveryPending); Assert.Single(client.RecoveryCommands); Assert.Equal(reads, client.RecoveryReads);
        client.RecoveryFactory = (_, request) =>
        {
            Assert.Same(original, request); Assert.Equal("Resume", request.ResolutionCode);
            return Task.FromResult(RecoveryContext(request));
        };
        await model.CheckRecoveryAsync(); Assert.True(model.HasRecoveryPending); Assert.Single(client.RecoveryCommands);
    }

    [Fact]
    public async Task 미확정복구는조회실패시재전송하지않고명시재확인도원결정을보존한다()
    {
        var client = RecoveryClient(); var readFails = false; NeighborhoodMemberRecoveryRequest? receipt = null;
        client.RecoveryFactory = (_, request) => readFails ? throw new HttpRequestException()
            : Task.FromResult(RecoveryContext(request, receipt));
        client.RecoveryCommand = (_, request) =>
        {
            if (client.RecoveryCommands.Count == 1) throw new HttpRequestException();
            receipt = request; return Task.FromResult(new NeighborhoodMemberRecoveryResponse { RequestId = "delivery-a", Revision = 8, StateCode = "Assigned" });
        };
        using var model = new 보호지원관리ViewModel(client, () => "admin"); await model.LoadAsync("case-a");
        await model.LoadRecoveryAsync(); model.RecoveryConfirmed = true; await model.SubmitRecoveryAsync();
        readFails = true; await model.CheckRecoveryAsync(); await model.RetryRecoveryAsync();
        Assert.True(model.HasRecoveryPending); Assert.False(model.CanRetryRecovery); Assert.Single(client.RecoveryCommands);
        readFails = false; await model.CheckRecoveryAsync(); Assert.True(model.CanRetryRecovery);
        model.RecoveryResolution = "CancelBeforePickup"; await model.RetryRecoveryAsync();
        Assert.False(model.HasRecoveryPending); Assert.Equal(2, client.RecoveryCommands.Count);
        Assert.Same(client.RecoveryCommands[0], client.RecoveryCommands[1]); Assert.Equal("Resume", client.RecoveryCommands[1].ResolutionCode);
    }

    [Fact]
    public async Task 다른계정의늦은배송복구조회는적용하지않는다()
    {
        var client = RecoveryClient(); string? owner = "admin";
        using var model = new 보호지원관리ViewModel(client, () => owner); await model.LoadAsync("case-a");
        var wait = new TaskCompletionSource<NeighborhoodMemberRecoveryContextResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.RecoveryFactory = (_, _) => wait.Task;
        var load = model.LoadRecoveryAsync(); owner = "other"; model.SynchronizeOwner();
        wait.SetResult(RecoveryContext(new() { SupportCaseId = "case-a" })); await load;
        Assert.Null(model.Recovery); Assert.Null(model.Detail); Assert.False(model.HasRecoveryPending);
        Assert.Empty(client.RecoveryCommands);
    }

    private static Client RecoveryClient() => new()
    {
        DetailFactory = id => Task.FromResult(new 보호지원CaseResponse { CaseId = id, Kind = 보호지원종류Codes.분쟁,
            StatusCode = "closed", SourceKind = 보호지원출처Codes.생활배송, SourceId = "delivery-a" }),
        RecoveryFactory = (_, request) => Task.FromResult(RecoveryContext(request))
    };
    private static NeighborhoodMemberRecoveryContextResponse RecoveryContext(NeighborhoodMemberRecoveryRequest request,
        NeighborhoodMemberRecoveryRequest? receipt = null) => new()
    {
        RequestId = "delivery-a", SupportCaseId = "case-a", Revision = receipt is null ? 7 : 8,
        StateCode = receipt is null ? "Issue" : "Assigned", IssueCode = "BeforePickupReviewRequired",
        CanResume = receipt is null, CanCancelBeforePickup = receipt is null,
        AppliedClientRequestId = receipt is not null && request.ClientRequestId == receipt.ClientRequestId ? receipt.ClientRequestId : null,
        AppliedResolutionCode = receipt is not null && request.ClientRequestId == receipt.ClientRequestId ? receipt.ResolutionCode : null
    };
}
