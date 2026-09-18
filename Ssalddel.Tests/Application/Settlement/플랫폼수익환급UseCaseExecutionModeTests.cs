using Ssalddel.Application.Settlement;
using Ssalddel.Contracts.Common.PlatformProfit;
using 살뜰.Services.Options;
using 살뜰.Services.Settlement;

namespace Ssalddel.Tests.Application.Settlement;

public sealed class 플랫폼수익환급UseCaseExecutionModeTests
{
    [Fact]
    public async Task 운영모드에서는_수동수익기록을거절한다()
    {
        var service = new RecordingProfitReturnService();
        var useCase = new 플랫폼수익환급UseCase(
            service,
            new TestExecutionModePolicy(SsalddelExecutionMode.Operational));

        var result = await useCase.수익기록Async(new PlatformRevenueEntryRequest(), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.False(service.RecordRevenueCalled);
    }

    [Fact]
    public async Task Simulation모드에서는_수동수익기록을허용한다()
    {
        var service = new RecordingProfitReturnService();
        var useCase = new 플랫폼수익환급UseCase(
            service,
            new TestExecutionModePolicy(SsalddelExecutionMode.Simulation));

        var result = await useCase.수익기록Async(new PlatformRevenueEntryRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(service.RecordRevenueCalled);
    }

    private sealed record TestExecutionModePolicy(SsalddelExecutionMode Mode)
        : ISsalddelExecutionModePolicy
    {
        public bool IsSimulation => Mode == SsalddelExecutionMode.Simulation;
        public bool IsOperational => Mode == SsalddelExecutionMode.Operational;
    }

    private sealed class RecordingProfitReturnService : IPlatformProfitReturnService
    {
        public bool RecordRevenueCalled { get; private set; }

        public Task<PlatformRevenueEntryResponse> RecordRevenueAsync(
            PlatformRevenueEntryRequest request,
            CancellationToken cancellationToken)
        {
            RecordRevenueCalled = true;
            return Task.FromResult(new PlatformRevenueEntryResponse());
        }

        public Task<PlatformProfitReturnPolicyResponse> CreatePolicyAsync(
            PlatformProfitReturnPolicyRequest request,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PlatformProfitReturnPlanResponse> CreateReturnSchedulesAsync(
            PlatformProfitReturnScheduleCreateRequest request,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<PlatformProfitReturnScheduleResponse>> ListSchedulesAsync(
            string? participantUserId,
            DateOnly? from,
            DateOnly? to,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
