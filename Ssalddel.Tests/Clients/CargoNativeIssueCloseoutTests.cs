using System.Reflection;
using DriverApp.Services;
using DriverApp.ViewModels.Driver.Transport;
using MudBlazor;
using Ssalddel.Contracts.Driver.Transport;

namespace Ssalddel.Tests.Clients;

public sealed class CargoNativeIssueCloseoutTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 운송전환뒤_이전신고의늦은결과를표시하지않는다(bool failure)
    {
        var source = new Reporter();
        var status = new 기사운송작업상태ViewModel();
        using var vm = Create(source, status);
        vm.운송설정(11);
        var pending = vm.신고Async();
        vm.운송설정(22);
        status.설정("새 운송 안내", Severity.Info);
        Assert.True(source.Token.IsCancellationRequested);
        if (failure) source.Result.SetException(new HttpRequestException("private-response"));
        else source.Result.SetResult(Success(11));
        await pending;
        Assert.Equal("새 운송 안내", status.메시지);
        Assert.False(vm.신고중);
        Assert.False(vm.결과확인필요);
    }

    [Fact]
    public async Task 같은ID의새인증문맥도_이전응답을버린다()
    {
        var source = new Reporter();
        var status = new 기사운송작업상태ViewModel();
        using var vm = Create(source, status);
        vm.운송설정(11);
        var pending = vm.신고Async();
        vm.운송설정(11);
        status.초기화();
        source.Result.SetResult(Success(11));
        await pending;
        Assert.Null(status.메시지);
    }

    [Fact]
    public async Task 화면종료뒤_신고취소와늦은응답표시를차단한다()
    {
        var source = new Reporter();
        var status = new 기사운송작업상태ViewModel();
        var vm = Create(source, status);
        vm.운송설정(11);
        var pending = vm.신고Async();
        vm.Dispose();
        status.초기화();
        source.Result.SetResult(Success(11));
        await pending;
        Assert.True(source.Token.IsCancellationRequested);
        Assert.Null(status.메시지);
        Assert.False(vm.신고가능);
    }

    [Fact]
    public async Task 수신중이거나결과미확정이면_같은화면에서재전송하지않는다()
    {
        var source = new Reporter();
        var status = new 기사운송작업상태ViewModel();
        using var vm = Create(source, status);
        vm.운송설정(11);
        var pending = vm.신고Async();
        await vm.신고Async();
        Assert.Equal(1, source.Calls);
        source.Result.SetResult(new(false, "확인 필요", OutcomeUnknown: true));
        await pending;
        await vm.신고Async();
        Assert.Equal(1, source.Calls);
        Assert.True(vm.결과확인필요);
        Assert.False(vm.신고가능);
        Assert.Equal(Severity.Warning, status.심각도);
        Assert.DoesNotContain("저장되지", status.메시지);
    }

    [Fact]
    public async Task 전송후입력변경을_기존신고결과의내용으로쓰지않는다()
    {
        var source = new Reporter();
        var status = new 기사운송작업상태ViewModel();
        using var vm = Create(source, status);
        vm.운송설정(11);
        vm.메모 = "원래 메모";
        var pending = vm.신고Async();
        vm.유형 = "다른 유형";
        vm.메모 = "다른 메모";
        source.Result.SetResult(Success(11));
        await pending;
        Assert.Contains("수량 부족 · 원래 메모", status.메시지);
        Assert.DoesNotContain("다른 유형", status.메시지);
        Assert.Equal("수량 부족", source.Report!.Reason);
    }

    [Fact]
    public async Task 잘못된운송의성공응답을_완료로표시하지않는다()
    {
        var source = new Reporter();
        var status = new 기사운송작업상태ViewModel();
        using var vm = Create(source, status);
        vm.운송설정(11);
        var pending = vm.신고Async();
        source.Result.SetResult(Success(22));
        await pending;
        Assert.True(vm.결과확인필요);
        Assert.DoesNotContain("신고되었습니다", status.메시지);
    }

    [Fact]
    public async Task 현재문맥이아니면_신고명령을시작하지않는다()
    {
        var source = new Reporter();
        using var vm = Create(source, new(), () => false);
        vm.운송설정(11);
        await vm.신고Async();
        Assert.Equal(0, source.Calls);
    }

    [Fact]
    public async Task 응답도착전에인증문맥이바뀌면_성공표시를버린다()
    {
        var source = new Reporter();
        var status = new 기사운송작업상태ViewModel();
        var current = true;
        using var vm = Create(source, status, () => current);
        vm.운송설정(11);
        var pending = vm.신고Async();
        current = false;
        status.초기화();
        source.Result.SetResult(Success(11));
        await pending;
        Assert.Null(status.메시지);
        Assert.False(vm.신고가능);
    }

    [Theory]
    [InlineData("network")]
    [InlineData("timeout")]
    [InlineData("invalid")]
    [InlineData("null")]
    [InlineData("wrong-id")]
    [InlineData("not-recorded")]
    [InlineData("private")]
    [InlineData("other-stage")]
    [InlineData("other-reason")]
    public async Task 서버미확정응답을_미저장또는성공으로단정하지않는다(string mode)
    {
        var api = DispatchProxy.Create<IDriverTransportApiService, Api>();
        var observed = Detail(11);
        if (mode == "private") observed.개인정보제공보류 = true;
        if (mode == "other-stage") observed.최근예외단계 = "하차";
        if (mode == "other-reason") observed.최근예외메시지 = "이전 사유";
        ((Api)(object)api).Send = mode switch
        {
            "network" => _ => throw new HttpRequestException("private-url traceId=secret"),
            "timeout" => _ => throw new TaskCanceledException("private-timeout"),
            "invalid" => _ => throw new InvalidOperationException("private-json"),
            "null" => _ => Task.FromResult<기사운송요약응답?>(null),
            "wrong-id" => _ => Task.FromResult<기사운송요약응답?>(Detail(22)),
            "not-recorded" => _ => Task.FromResult<기사운송요약응답?>(new() { Id = 11 }),
            _ => _ => Task.FromResult<기사운송요약응답?>(observed)
        };
        var result = await new HttpDriverTransportExceptionService(api).ReportAsync(new(11, "상차", "수량불일치", "수량 부족", null));
        Assert.False(result.Reported);
        Assert.True(result.OutcomeUnknown);
        Assert.DoesNotContain("저장되지", result.Message);
        Assert.DoesNotContain("private-", result.Message);
        Assert.Null(result.Transport);
    }

    [Fact]
    public async Task 같은운송에신고된서버결과만_성공으로표시한다()
    {
        var api = DispatchProxy.Create<IDriverTransportApiService, Api>();
        ((Api)(object)api).Send = _ => Task.FromResult<기사운송요약응답?>(Detail(11));
        var result = await new HttpDriverTransportExceptionService(api).ReportAsync(new(11, "상차", "수량불일치", "수량 부족", null));
        Assert.True(result.Reported);
        Assert.False(result.OutcomeUnknown);
        Assert.Equal(11, result.Transport!.Id);
    }

    private static 기사운송예외신고ViewModel Create(Reporter source, 기사운송작업상태ViewModel status, Func<bool>? current = null)
        => new(source, status, "상차", ["수량 부족", "다른 유형"], "수량 부족", _ => "수량불일치", current);
    private static DriverTransportExceptionReportResult Success(long id) => new(true, "신고되었습니다.", Detail(id));
    private static 기사운송요약응답 Detail(long id) => new() { Id = id, 예외신고됨 = true, 관리자확인필요 = true,
        최근예외단계 = "상차", 최근예외코드 = "수량불일치", 최근예외메시지 = "수량 부족" };
    private sealed class Reporter : IDriverTransportExceptionService
    {
        public TaskCompletionSource<DriverTransportExceptionReportResult> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public CancellationToken Token { get; private set; }
        public DriverTransportExceptionReport? Report { get; private set; }
        public Task<DriverTransportExceptionReportResult> ReportAsync(DriverTransportExceptionReport report, CancellationToken cancellationToken = default)
        { Calls++; Token = cancellationToken; Report = report; return Result.Task; }
    }
    public class Api : DispatchProxy
    {
        public Func<CancellationToken, Task<기사운송요약응답?>> Send { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method?.Name == nameof(IDriverTransportApiService.예외신고Async)
                ? Send((CancellationToken)args![2]!) : throw new NotSupportedException();
    }
}
