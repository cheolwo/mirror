using System.Text.Json;
using System.Reflection;
using DriverApp.Services;
using DriverApp.ViewModels.Driver.Transport;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Clients;

public sealed class CargoNativeIssueDurableRecoveryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 실제상하차부모의재진입이_보호를읽고_과거신고GET만으로해제하지않는다(bool pickup)
    {
        var auth = new AuthSession(new TokenStore(), new ClientSessionGuard());
        var backend = new Backend(); var reporter = new Reporter();
        var api = DispatchProxy.Create<IDriverTransportApiService, TransportApi>();
        DriverCargoExceptionRecovery Recovery() => new(new ProtectedWorkflowRecoveryStore(
                _ => new(auth.UserId, auth.SessionRevision, auth.IsAuthenticated), backend.Read, backend.Write, backend.Remove),
            new WorkflowRecoveryContext(() => new RoleWorkspaceIdentity(auth.UserId, auth.SessionRevision, auth.IsAuthenticated), () => new Uri("https://first.test/")));
        using (var firstPickup = pickup ? new 기사상차PageViewModel(new(api, auth), new(new CompletionService()), reporter, new Navigation(), exceptionRecovery: Recovery()) : null)
        using (var firstDropoff = !pickup ? new 기사하차PageViewModel(new(api, auth), new(new CompletionService()), reporter, exceptionRecovery: Recovery()) : null)
        {
            if (pickup) await firstPickup!.InitializeAsync(11); else await firstDropoff!.InitializeAsync(11);
            var issue = firstPickup?.예외 ?? firstDropoff!.예외;
            Assert.True(issue.신고가능); await issue.신고Async(); Assert.True(issue.결과확인필요);
        }
        using var nextPickup = pickup ? new 기사상차PageViewModel(new(api, auth), new(new CompletionService()), reporter, new Navigation(), exceptionRecovery: Recovery()) : null;
        using var nextDropoff = !pickup ? new 기사하차PageViewModel(new(api, auth), new(new CompletionService()), reporter, exceptionRecovery: Recovery()) : null;
        if (pickup) await nextPickup!.InitializeAsync(11); else await nextDropoff!.InitializeAsync(11);
        var restored = nextPickup?.예외 ?? nextDropoff!.예외;
        Assert.True(restored.결과확인필요); Assert.False(restored.신고가능);
        await restored.신고Async(); Assert.Equal(1, reporter.Calls); Assert.Single(backend.Values);
    }

    [Theory]
    [InlineData("상차")]
    [InlineData("하차")]
    public async Task 새저장소와_VM은_미확정보호를복원하고_신고를다시보내지않는다(string stage)
    {
        var f = new Fixture();
        using (var first = f.NewVm(stage))
        {
            first.운송설정(11); await first.복원Async();
            first.메모 = "보관하지 않을 개인 메모";
            await first.신고Async();
            Assert.True(first.결과확인필요); Assert.False(first.신고가능);
        }
        f.Revision++;
        using var restored = f.NewVm(stage);
        restored.운송설정(11); await restored.복원Async();
        Assert.True(restored.결과확인필요); Assert.False(restored.신고가능); Assert.Null(restored.메모);
        await restored.신고Async(); await restored.복원Async();
        Assert.Equal(1, f.Reporter.Calls); Assert.Single(f.Backend.Values);
        var saved = JsonSerializer.Deserialize<WorkflowRecoveryRecord>(f.Backend.Values.Single().Value)!;
        Assert.Null(saved.PayloadJson); Assert.Equal("11", saved.Target);
        Assert.DoesNotContain("보관하지", f.Backend.Values.Single().Value);
        Assert.Contains("운영자", f.Status.메시지);
    }

    [Theory]
    [InlineData("throw")]
    [InlineData("noop")]
    [InlineData("read")]
    public async Task 신고전보호가확인되지않으면_POST없이닫고_명시적재조회뒤에만다시진행한다(string failure)
    {
        var f = new Fixture(); using var vm = f.NewVm(); vm.운송설정(11); await vm.복원Async();
        f.Backend.FailWrite = failure == "throw"; f.Backend.NoOpWrite = failure == "noop";
        f.Backend.FailRead = failure == "read";
        await vm.신고Async();
        Assert.Equal(0, f.Reporter.Calls); Assert.False(vm.신고가능);
        await vm.신고Async(); Assert.Equal(0, f.Reporter.Calls);
        f.Backend.FailWrite = false; f.Backend.NoOpWrite = false; f.Backend.FailRead = false;
        await vm.복원Async(); Assert.True(vm.신고가능);
        await vm.신고Async(); Assert.Equal(1, f.Reporter.Calls);
    }

    [Fact]
    public async Task 복구조회실패뒤에도_복구확인전신고를보내지않는다()
    {
        var f = new Fixture(); f.Backend.FailRead = true; using var vm = f.NewVm(); vm.운송설정(11);
        await vm.복원Async(); await vm.신고Async();
        Assert.True(vm.결과확인필요); Assert.False(vm.신고가능); Assert.Equal(0, f.Reporter.Calls);
        f.Backend.FailRead = false; await vm.복원Async(); Assert.True(vm.신고가능);
    }

    [Theory]
    [InlineData("logout", false)]
    [InlineData("logout", true)]
    [InlineData("account", false)]
    [InlineData("account", true)]
    [InlineData("server", false)]
    [InlineData("server", true)]
    [InlineData("transport", false)]
    [InlineData("transport", true)]
    [InlineData("dispose", false)]
    [InlineData("dispose", true)]
    public async Task 전환뒤늦은신고응답은_새문맥을바꾸거나_이전보호기록을지우지않는다(string transition, bool fault)
    {
        var f = new Fixture(); using var vm = f.NewVm(); vm.운송설정(11); await vm.복원Async();
        var response = new TaskCompletionSource<DriverTransportExceptionReportResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Reporter.Send = _ => response.Task;
        var reporting = vm.신고Async();
        await f.Reporter.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (transition == "logout") { f.Authenticated = false; f.Revision++; vm.운송설정(11); }
        else if (transition == "account") { f.Owner = "driver-two"; f.Revision++; vm.운송설정(11); }
        else if (transition == "server") f.Endpoint = new("https://second.test/");
        else if (transition == "transport") vm.운송설정(22);
        else vm.Dispose();
        f.Status.설정("현재 화면", Severity.Info);
        if (fault) response.SetException(new IOException("비공개 예외"));
        else response.SetResult(new(true, "늦은 성공", new() { Id = 11 }));
        await reporting.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("현재 화면", f.Status.메시지); Assert.Single(f.Backend.Values); Assert.Equal(1, f.Reporter.Calls);
        f.Authenticated = true; f.Owner = "driver-one"; f.Endpoint = new("https://first.test/"); f.Revision++;
        using var next = f.NewVm(); next.운송설정(11); await next.복원Async();
        Assert.True(next.결과확인필요); Assert.False(next.신고가능);
        await next.신고Async(); Assert.Equal(1, f.Reporter.Calls);
    }

    [Fact]
    public async Task 같은운송도_다른계정_다른서버_다른단계에는보호기록을전파하지않는다()
    {
        var f = new Fixture();
        using (var first = f.NewVm()) { first.운송설정(11); await first.복원Async(); await first.신고Async(); }
        foreach (var scope in new[] { "account", "server", "stage", "transport" })
        {
            f.Owner = scope == "account" ? "driver-two" : "driver-one";
            f.Endpoint = new(scope == "server" ? "https://second.test/" : "https://first.test/");
            f.Revision++;
            using var other = f.NewVm(scope == "stage" ? "하차" : "상차");
            other.운송설정(scope == "transport" ? 22 : 11); await other.복원Async();
            Assert.False(other.결과확인필요); Assert.True(other.신고가능);
        }
        Assert.Single(f.Backend.Values); Assert.Equal(1, f.Reporter.Calls);
    }

    [Fact]
    public async Task 명시적응답을받고보호삭제까지확인한경우에만_재실행잠금을해제한다()
    {
        var f = new Fixture(); f.Reporter.Send = _ => Task.FromResult(new DriverTransportExceptionReportResult(true, "이번 신고 응답", new() { Id = 11 }));
        using (var vm = f.NewVm()) { vm.운송설정(11); await vm.복원Async(); await vm.신고Async(); Assert.False(vm.결과확인필요); }
        Assert.Empty(f.Backend.Values);
        using var next = f.NewVm(); next.운송설정(11); await next.복원Async(); Assert.True(next.신고가능);
    }

    [Fact]
    public async Task 보호삭제실패는_성공응답과별개로_재실행중복을계속차단한다()
    {
        var f = new Fixture(); f.Backend.NoOpRemove = true;
        f.Reporter.Send = _ => Task.FromResult(new DriverTransportExceptionReportResult(true, "이번 신고 응답", new() { Id = 11 }));
        using (var vm = f.NewVm()) { vm.운송설정(11); await vm.복원Async(); await vm.신고Async(); Assert.True(vm.결과확인필요); }
        using var next = f.NewVm(); next.운송설정(11); await next.복원Async();
        Assert.True(next.결과확인필요); await next.신고Async(); Assert.Equal(1, f.Reporter.Calls);
    }

    [Fact]
    public async Task 만료한관측이나이전신고표시를_이번요청의미적용근거로사용하지않는다()
    {
        var f = new Fixture(); var recovery = f.NewRecovery(); var snapshot = recovery.Capture();
        var record = await recovery.BeginAsync(snapshot, "상차", 11, Guid.NewGuid());
        var key = f.Backend.Values.Single().Key;
        f.Backend.Values[key] = JsonSerializer.Serialize(record with { CreatedAtUtc = DateTime.UtcNow.AddDays(-5) });
        using var vm = f.NewVm(); vm.운송설정(11); await vm.복원Async();
        Assert.True(vm.결과확인필요); await vm.신고Async(); Assert.Equal(0, f.Reporter.Calls);
        Assert.Single(f.Backend.Values);
    }

    [Fact]
    public async Task 계정변경이없는서버전환도_메모를새서버로전파하지않는다()
    {
        var f = new Fixture(); using var vm = f.NewVm(); vm.운송설정(11); await vm.복원Async();
        vm.메모 = "이전 서버 메모"; vm.열림 = true;
        f.Endpoint = new("https://second.test/"); Assert.False(vm.신고가능);
        await vm.복원Async(); Assert.Null(vm.메모); Assert.False(vm.열림); Assert.True(vm.신고가능);
    }

    private sealed class Fixture
    {
        public string Owner { get; set; } = "driver-one";
        public long Revision { get; set; } = 1;
        public bool Authenticated { get; set; } = true;
        public Uri Endpoint { get; set; } = new("https://first.test/");
        public Backend Backend { get; } = new();
        public Reporter Reporter { get; } = new();
        public 기사운송작업상태ViewModel Status { get; } = new();
        public RoleWorkspaceIdentity Identity() => new(Owner, Revision, Authenticated);
        public DriverCargoExceptionRecovery NewRecovery() => new(new ProtectedWorkflowRecoveryStore(_ => Identity(), Backend.Read, Backend.Write, Backend.Remove),
            new WorkflowRecoveryContext(Identity, () => Endpoint));
        public 기사운송예외신고ViewModel NewVm(string stage = "상차")
            => new(Reporter, Status, stage, ["수량 부족"], "수량 부족", _ => "test.quantity", () => Authenticated, NewRecovery());
    }

    private sealed class Backend
    {
        public Dictionary<string, string> Values { get; } = [];
        public bool FailRead { get; set; }
        public bool FailWrite { get; set; }
        public bool NoOpWrite { get; set; }
        public bool NoOpRemove { get; set; }
        public Task<string?> Read(string key) => FailRead ? throw new IOException("보호 읽기 실패") : Task.FromResult(Values.GetValueOrDefault(key));
        public Task Write(string key, string value)
        {
            if (FailWrite) throw new IOException("보호 쓰기 실패");
            if (!NoOpWrite) Values[key] = value;
            return Task.CompletedTask;
        }
        public void Remove(string key) { if (!NoOpRemove) Values.Remove(key); }
    }

    private sealed class Reporter : IDriverTransportExceptionService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public Func<CancellationToken, Task<DriverTransportExceptionReportResult>> Send { get; set; }
            = _ => Task.FromResult(new DriverTransportExceptionReportResult(false, "응답 유실", OutcomeUnknown: true));
        public Task<DriverTransportExceptionReportResult> ReportAsync(DriverTransportExceptionReport report, CancellationToken cancellationToken = default)
        { Calls++; Started.TrySetResult(); return Send(cancellationToken); }
    }

    public class TransportApi : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
            => method?.Name == nameof(IDriverTransportApiService.상세조회Async)
                ? Task.FromResult<기사운송상세응답?>(new()
                {
                    Id = (long)args![0]!, 상태 = "상차지도착", 기사_운송자 = "driver-one", 출발지 = "상차지", 도착지 = "하차지",
                    예외신고됨 = true, 최근예외단계 = "상차", 최근예외메시지 = "수량 부족", 관리자확인필요 = true
                })
                : throw new NotSupportedException();
    }

    private sealed class TokenStore : IClientSecureTokenStore
    {
        private ClientAuthTokenSnapshot? snapshot = new("test-only-token", DateTime.UtcNow.AddHours(1), "test-only-refresh",
            DateTime.UtcNow.AddDays(1), "driver-one", "기사", ["Driver"]);
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(snapshot);
        public Task SaveAsync(ClientAuthTokenSnapshot value, CancellationToken cancellationToken = default) { snapshot = value; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default) { snapshot = null; return Task.CompletedTask; }
    }

    private sealed class CompletionService : IDriverTransportCompletionPhotoService
    {
        public Task<DriverTransportCompletionPhotoResult> CompleteWithPhotoAsync(DriverTransportCompletionPhoto photo, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("신고 복구가 완료 명령을 실행하면 안 됩니다.");
    }

    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("http://localhost/", "http://localhost/driver/transports/current");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new InvalidOperationException("신고 복구에서 자동 이동하지 않습니다.");
    }
}
