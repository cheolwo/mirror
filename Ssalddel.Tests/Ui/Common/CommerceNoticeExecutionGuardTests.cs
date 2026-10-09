using System.Text.Json;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Commerce;
using Ssalddel.Services.Commerce;
using Ssalddel.Ui.Common.Areas.App.ViewModels.Commerce;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CommerceNoticeExecutionGuardTests
{
    [Theory]
    [InlineData(SsalddelExecutionMode.Simulation, "Simulation")]
    [InlineData(SsalddelExecutionMode.Operational, "Operational")]
    public void NoticeReportsServerExecutionModeWithoutCertifyingOperationalReadiness(SsalddelExecutionMode mode, string expected)
    {
        var execution = new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = mode }));
        var service = new 통신판매안내Service(Options.Create(new 통신판매운영Options()), execution: execution);

        var value = service.조회();

        Assert.Equal(expected, value.ExecutionModeCode);
        Assert.False(value.IsOperationalReady);
        Assert.NotEmpty(value.MissingRequirements);
    }

    [Fact]
    public void MissingExecutionDependencyAndLegacyResponseRemainUnknown()
    {
        var service = new 통신판매안내Service(Options.Create(new 통신판매운영Options()));
        Assert.Equal("Unknown", service.조회().ExecutionModeCode);
        Assert.Equal("Unknown", JsonSerializer.Deserialize<통신판매안내Response>("{\"Version\":\"legacy\"}")!.ExecutionModeCode);
    }

    [Theory]
    [InlineData("Simulation", true, null, true, true)]
    [InlineData("Operational", true, null, false, false)]
    [InlineData("Unknown", true, null, false, false)]
    [InlineData(null, true, null, false, false)]
    [InlineData("simulation", true, null, false, false)]
    [InlineData("Simulation", true, 7L, true, true)]
    [InlineData("Operational", true, 7L, true, false)]
    [InlineData("Unknown", true, 7L, true, false)]
    [InlineData("Unknown", false, null, true, false)]
    public async Task OnlyExplicitServerSimulationAllowsMissingRequiredSeller(string? mode, bool requireSeller,
        long? sellerRevision, bool allowed, bool simulation)
    {
        var client = new CommerceUiFixture { Notice = _ => Task.FromResult<통신판매안내Response?>(Notice(mode)) };
        using var model = new 통신판매안내ViewModel(client);
        Assert.False(model.CanAcknowledge(requireSeller, sellerRevision));

        await model.LoadAsync();

        Assert.Equal(simulation, model.IsSimulation);
        Assert.Equal(allowed, model.CanAcknowledge(requireSeller, sellerRevision));
        Assert.False(model.CanAcknowledge(requireSeller, sellerRevision, disabled: true));
        Assert.Equal(0, client.PrivateReads);
        Assert.Empty(client.Sellers);
    }

    [Fact]
    public async Task RefreshLocksAcknowledgmentUntilLatestNoticeArrives()
    {
        var client = new CommerceUiFixture { Notice = _ => Task.FromResult<통신판매안내Response?>(Notice("Simulation")) };
        using var model = new 통신판매안내ViewModel(client);
        await model.LoadAsync();
        Assert.True(model.CanAcknowledge(true, null));
        var pending = new TaskCompletionSource<통신판매안내Response?>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Notice = _ => pending.Task;

        var refreshing = model.LoadAsync();
        Assert.True(model.IsLoading);
        Assert.True(model.IsSimulation);
        Assert.False(model.CanAcknowledge(true, null));
        Assert.False(model.CanAcknowledge(true, 7));

        pending.SetResult(Notice("Operational")); await refreshing;
        Assert.False(model.IsLoading); Assert.False(model.IsSimulation);
        Assert.False(model.CanAcknowledge(true, null)); Assert.True(model.CanAcknowledge(true, 7));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrEmptyRefreshRemovesPreviousSimulationExceptionAndCanRecover(bool empty)
    {
        var client = new CommerceUiFixture { Notice = _ => Task.FromResult<통신판매안내Response?>(Notice("Simulation")) };
        using var model = new 통신판매안내ViewModel(client);
        await model.LoadAsync();
        Assert.True(model.CanAcknowledge(true, null));
        client.Notice = _ => empty ? Task.FromResult<통신판매안내Response?>(null)
            : Task.FromException<통신판매안내Response?>(new HttpRequestException("private response payload"));

        await model.LoadAsync();

        Assert.Null(model.Value); Assert.False(model.IsSimulation);
        Assert.False(model.CanAcknowledge(true, null)); Assert.False(model.CanAcknowledge(false, 7));
        Assert.NotNull(model.Error); Assert.DoesNotContain("private response payload", model.Error);
        client.Notice = _ => Task.FromResult<통신판매안내Response?>(Notice("Simulation"));
        await model.LoadAsync();
        Assert.Null(model.Error); Assert.True(model.CanAcknowledge(true, null));
    }

    [Fact]
    public async Task DisposedNoticeRejectsLateResponseAndCannotAcknowledgeOrReload()
    {
        var reads = 0;
        var client = new CommerceUiFixture { Notice = _ => { reads++; return Task.FromResult<통신판매안내Response?>(Notice("Simulation")); } };
        using var model = new 통신판매안내ViewModel(client);
        await model.LoadAsync();
        var previous = model.Value;
        var pending = new TaskCompletionSource<통신판매안내Response?>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken requestToken = default;
        client.Notice = token => { reads++; requestToken = token; return pending.Task; };

        var refreshing = model.LoadAsync();
        model.Dispose();
        Assert.True(requestToken.IsCancellationRequested);
        pending.SetResult(Notice("Operational")); await refreshing;
        Assert.Same(previous, model.Value);
        Assert.False(model.CanAcknowledge(true, null)); Assert.False(model.CanAcknowledge(false, 7));
        await model.LoadAsync(); Assert.Equal(2, reads);
    }

    private static 통신판매안내Response Notice(string? mode)
        => new() { Version = "test-policy", ExecutionModeCode = mode!, IntermediaryNotice = "중개 안내" };
}
