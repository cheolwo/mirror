using Microsoft.Extensions.Options;
using Ssalddel.Application.Driver.Qualification;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Driver.Profile;
using Ssalddel.Services.Dispatch.Compliance;
using Ssalddel.Services.Operations;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.Dispatch;

public sealed class 신규화물업무자격GuardTests
{
    [Fact]
    public async Task 검증연결누락은_허용으로대체하지않는다()
    {
        var result = await 신규화물업무자격검사.평가Async(null, "driver");
        Assert.False(result.CanReceiveNewWork);
        Assert.Equal("Qualification.GuardNotConfigured", result.ReasonCode);
    }

    [Fact]
    public async Task 자격미확인은_플랫폼준비와관계없이_신규업무차단()
    {
        var platform = new Platform(new() { IsPreparationComplete = true, ActualRegistrationConfirmed = true });
        var guard = Guard(new(false, "Qualification.Suspended", "정지", 운송자격Codes.Suspended), platform);
        var result = await guard.평가Async("driver");
        Assert.False(result.CanReceiveNewWork);
        Assert.Equal("Qualification.Suspended", result.ReasonCode);
        Assert.Equal(0, platform.Reads);
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public async Task 내부출시관문은_준비와실제등록과기관신고와무료설정을_각각확인한다(
        bool prepared, bool registered, bool authority, bool free)
    {
        var guard = Guard(Valid(), new Platform(new()
        {
            IsPreparationComplete = prepared, ActualRegistrationConfirmed = registered,
            RequiredAuthorityProceduresConfirmed = authority, FreeConnectionConfigured = free
        }));
        var result = await guard.평가Async("driver");
        Assert.False(result.CanReceiveNewWork);
        Assert.Equal("FreightPlatform.ReadinessRequired", result.ReasonCode);
    }

    [Fact]
    public async Task 모든내부검토근거가있어도_기능활성화와기관인증을생성하지않는다()
    {
        var platform = new Platform(new() { IsPreparationComplete = true, ActualRegistrationConfirmed = true,
            RequiredAuthorityProceduresConfirmed = true, FreeConnectionConfigured = true, CanActivateOperations = false });
        var result = await Guard(Valid(), platform).평가Async("driver");
        Assert.True(result.CanReceiveNewWork);
        Assert.False(platform.Value.CanActivateOperations);
        Assert.False(result.IsSimulationBypass);
    }

    [Theory]
    [InlineData(SsalddelExecutionMode.Operational, false)]
    [InlineData(SsalddelExecutionMode.Simulation, true)]
    public async Task 시험우회표시는_Operational에서는_재사용불가(SsalddelExecutionMode mode, bool expected)
    {
        var platform = new Platform(new());
        var result = await Guard(Valid() with { IsSimulationBypass = true }, platform, mode).평가Async("driver");
        Assert.Equal(expected, result.CanReceiveNewWork);
        Assert.Equal(0, platform.Reads);
    }

    [Fact]
    public void 운송정보누락은_Operational에서차단하고_명시Simulation은분리한다()
    {
        var request = new 살뜰.도메인.화주.화주운송의뢰();
        Assert.False(신규화물업무자격검사.화물정보완료(Guard(Valid(), new Platform(new())), request));
        Assert.True(신규화물업무자격검사.화물정보완료(Guard(Valid(), new Platform(new()), SsalddelExecutionMode.Simulation), request));
        Assert.False(신규화물업무자격검사.화물정보완료(null, request));
    }

    private static 운송자격평가결과 Valid() => new(true, "Qualification.Valid", "내부 검토", 운송자격Codes.Valid, 3);
    private static 신규화물업무자격Guard Guard(운송자격평가결과 result, Platform platform,
        SsalddelExecutionMode mode = SsalddelExecutionMode.Operational)
        => new(new Qualification(result), platform, new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = mode })));
    private sealed class Qualification(운송자격평가결과 result) : I기사운송자격Service
    {
        public Task<운송자격평가결과> 평가Async(string driverId, string? vehicleKind, CancellationToken ct = default) => Task.FromResult(result);
    }
    private sealed class Platform(FreightPlatformReadinessDto value) : I화물플랫폼등록준비Service
    {
        public FreightPlatformReadinessDto Value { get; } = value;
        public int Reads { get; private set; }
        public FreightPlatformReadinessDto 조회() { Reads++; return Value; }
    }
}
