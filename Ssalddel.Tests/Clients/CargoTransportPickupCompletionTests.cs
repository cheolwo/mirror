using System.Reflection;
using DriverApp.Services;
using DriverApp.ViewModels.Driver.Transport;
using Microsoft.AspNetCore.Components;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Clients;

public sealed class CargoTransportPickupCompletionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 완료응답유실도_같은운송조회가상차완료이면_다음업무로이동한다(bool lost)
    {
        using var f = new Fixture(); await f.Prepare();
        f.Completion.Send = _ => { f.Api.State = "상차완료"; return lost
            ? Task.FromException<DriverTransportCompletionPhotoResult>(new IOException("lost response"))
            : Task.FromResult(new DriverTransportCompletionPhotoResult(true, true, "recorded")); };
        await f.Page.완료Async();
        Assert.Equal([11L, 11L], f.Api.DetailIds); Assert.Equal(1, f.Completion.Calls);
        Assert.True(f.Page.상차완료확인); Assert.False(f.Page.완료가능);
        Assert.False(f.Page.사진.사진있음); Assert.False(f.Page.완료결과확인필요);
        Assert.EndsWith(DriverRoutes.TransportDropoff(11), f.Navigation.Uri);
        await f.Page.완료Async(); Assert.Equal(1, f.Completion.Calls);
    }

    [Fact]
    public async Task 완료뒤조회실패는_증빙을보존하고_재조회전POST반복을막는다()
    {
        using var f = new Fixture(); await f.Prepare();
        f.Completion.Send = _ => { f.Api.FailRead = true; throw new IOException("lost response"); };
        await f.Page.완료Async();
        Assert.True(f.Page.완료결과확인필요); Assert.False(f.Page.완료가능);
        Assert.True(f.Page.사진.사진있음); Assert.True(f.Page.현장.완료); Assert.True(f.Page.인수증.문서사진확인);
        await f.Page.완료Async(); Assert.Equal(1, f.Completion.Calls);
        f.Api.FailRead = false; f.Api.State = "상차완료";
        await f.Page.InitializeAsync(11);
        Assert.True(f.Page.상차완료확인); Assert.False(f.Page.완료결과확인필요);
        Assert.False(f.Page.사진.사진있음);
    }

    [Fact]
    public async Task 완료뒤빈조회도_기존확인을보존하고_같은현재화물을다시확인해야재시도한다()
    {
        using var f = new Fixture(); await f.Prepare();
        f.Completion.Send = _ => { f.Api.NullRead = true; throw new IOException("lost response"); };
        await f.Page.완료Async();
        Assert.Null(f.Page.운송); Assert.True(f.Page.완료결과확인필요); Assert.False(f.Page.완료가능);
        Assert.True(f.Page.사진.사진있음); Assert.True(f.Page.현장.완료); Assert.True(f.Page.인수증.문서사진확인);
        await f.Page.완료Async(); Assert.Equal(1, f.Completion.Calls);
        f.Api.NullRead = false; await f.Page.InitializeAsync(11);
        Assert.True(f.Page.완료가능); Assert.True(f.Page.현장.완료); Assert.True(f.Page.인수증.문서사진확인);
    }

    [Fact]
    public async Task 확인중서명조건이바뀌면_과거현장확인을재사용하지않는다()
    {
        using var f = new Fixture(); await f.Prepare();
        f.Completion.Send = _ => { f.Api.SignatureRequired = true; throw new IOException("lost response"); };
        await f.Page.완료Async();
        Assert.False(f.Page.완료가능); Assert.False(f.Page.현장.완료);
        Assert.False(f.Page.인수증.문서사진확인); Assert.True(f.Page.사진.사진있음);
    }

    [Fact]
    public async Task POST성공응답만있고조회는미완료이면_이동하지않고_기존사진으로명시재시도한다()
    {
        using var f = new Fixture(); await f.Prepare();
        f.Completion.Send = _ => Task.FromResult(new DriverTransportCompletionPhotoResult(true, true, "response", ObjectName: "private/proof-one"));
        await f.Page.완료Async();
        Assert.False(f.Page.상차완료확인); Assert.True(f.Page.완료가능);
        Assert.True(f.Page.현장.완료); Assert.True(f.Page.인수증.문서사진확인);
        Assert.Equal("http://localhost/", f.Navigation.Uri);
        f.Completion.Send = photo => { Assert.Equal("private/proof-one", photo.ExistingObjectName);
            Assert.True(photo.ReceiptEvidence?.Signed); f.Api.State = "상차완료";
            return Task.FromResult(new DriverTransportCompletionPhotoResult(true, true, "recorded")); };
        await f.Page.완료Async(); Assert.Equal(2, f.Completion.Calls); Assert.True(f.Page.상차완료확인);
    }

    [Theory]
    [InlineData("logout")]
    [InlineData("dispose")]
    [InlineData("transport")]
    public async Task 계정페이지운송전환뒤_늦은완료응답을적용하지않는다(string transition)
    {
        using var f = new Fixture(); await f.Prepare();
        var pending = new TaskCompletionSource<DriverTransportCompletionPhotoResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Completion.Send = _ => pending.Task;
        var completing = f.Page.완료Async();
        if (transition == "logout") await f.Auth.ClearAsync();
        else if (transition == "dispose") f.Page.Dispose();
        else await f.Page.InitializeAsync(22);
        Assert.True(f.Completion.Token.IsCancellationRequested);
        pending.SetResult(new(true, true, "late", ObjectName: "private/old-proof")); await completing;
        Assert.Equal(transition == "transport" ? new long[] {11, 22} : [11L], f.Api.DetailIds);
        Assert.False(f.Page.완료가능); Assert.Null(f.Page.상차완료.결과);
        Assert.Equal("http://localhost/", f.Navigation.Uri); Assert.False(f.Page.사진.사진있음);
    }

    [Theory]
    [InlineData("하차지도착")]
    [InlineData("인수완료")]
    public async Task 재조회상태가이미진전했으면_이전단계로완료를반복하지않는다(string state)
    {
        using var f = new Fixture(); await f.Prepare();
        f.Completion.Send = _ => { f.Api.State = state; throw new IOException("lost"); };
        await f.Page.완료Async();
        Assert.True(f.Page.상차완료확인); Assert.False(f.Page.완료가능);
        Assert.EndsWith(state == "인수완료" ? DriverRoutes.CurrentTransport : DriverRoutes.TransportDropoff(11), f.Navigation.Uri);
    }

    [Theory]
    [InlineData("CargoPickupPhotoRequired", "cargo.pickup", "상차 사진 필요")]
    [InlineData("CargoWarehouseHandoffPending", "cargo.pickup", "창고 인계 확인 필요")]
    [InlineData("CargoTransportHeld", "cargo.interruption-recovery", "운송 보류")]
    public async Task 완료거절사유는_사진업로드결과와함께보존하고_미완료GET뒤_업무별안내로표시한다(string code, string stage, string title)
    {
        using var f = new Fixture(); await f.Prepare();
        f.Completion.Send = _ => Task.FromResult(new DriverTransportCompletionPhotoResult(true, false, "확인 필요",
            ObjectName: "private/proof", CompletionFailure: Failure(code, stage)));
        await f.Page.완료Async();
        Assert.Equal(code, f.Page.완료실패?.코드);
        Assert.False(f.Page.완료결과확인필요); Assert.True(f.Page.사진.사진있음);
        Assert.True(f.Page.완료실패상태안내인가);
        Assert.Equal(title, 업무오류안내Presentation.생성(f.Page.완료실패!).제목);
        f.Page.작업상태.설정("새 수량 문제를 확인해 주세요.", MudBlazor.Severity.Warning);
        Assert.False(f.Page.완료실패상태안내인가);
        f.Api.State = "상차완료";
        await f.Page.InitializeAsync(11);
        Assert.Null(f.Page.완료실패); Assert.True(f.Page.상차완료확인);
        Assert.False(f.Page.사진.사진있음); Assert.Equal(1, f.Completion.Calls);
    }

    [Fact]
    public async Task 서버가명시한미확정은_미완료GET만으로해제하거나_POST를반복하지않는다()
    {
        using var f = new Fixture(); await f.Prepare();
        f.Completion.Send = _ => Task.FromException<DriverTransportCompletionPhotoResult>(Failure("UnexpectedFailure", "cargo.pickup", "Unknown"));
        await f.Page.완료Async();
        Assert.True(f.Page.완료결과확인필요); Assert.False(f.Page.완료가능);
        Assert.Equal("Unknown", f.Page.완료실패?.WorkflowContext?.OutcomeCode);
        Assert.True(f.Page.사진.사진있음);
        await f.Page.InitializeAsync(11);
        Assert.True(f.Page.완료결과확인필요); Assert.NotNull(f.Page.완료실패);
        await f.Page.완료Async(); Assert.Equal(1, f.Completion.Calls);
        f.Api.State = "상차완료"; await f.Page.InitializeAsync(11);
        Assert.Null(f.Page.완료실패); Assert.False(f.Page.완료결과확인필요);
    }

    [Theory]
    [InlineData("logout")]
    [InlineData("dispose")]
    [InlineData("transport")]
    public async Task 이전계정이나운송의늦은실패는_새업무오류로남기지않는다(string transition)
    {
        using var f = new Fixture(); await f.Prepare();
        var pending = new TaskCompletionSource<DriverTransportCompletionPhotoResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Completion.Send = _ => pending.Task;
        var completion = f.Page.완료Async();
        if (transition == "logout") await f.Auth.ClearAsync();
        else if (transition == "dispose") f.Page.Dispose();
        else await f.Page.InitializeAsync(22);
        pending.SetException(Failure("CargoPickupPhotoRequired", "cargo.pickup"));
        await completion;
        Assert.Null(f.Page.완료실패); Assert.False(f.Page.완료실패상태안내인가);
        Assert.Equal("http://localhost/", f.Navigation.Uri);
    }

    private static DriverApiException Failure(string code, string stage, string outcome = "NotApplied")
    {
        var metadata = SsalddelApiProblemParser.Parse(System.Text.Json.JsonSerializer.Serialize(new
        {
            errorCode = code, availableRecoveryActions = new[] { "RefreshState" },
            workflowContext = new { operatingSystemId = "DomesticCargoTransportOS", stageId = stage, outcomeCode = outcome }
        }));
        return new(System.Net.HttpStatusCode.BadRequest, "상차 완료", code, null, metadata);
    }

    private sealed class Fixture : IDisposable
    {
        public AuthSession Auth { get; } = new(new TokenStore(), new ClientSessionGuard());
        public ApiStub Api { get; }
        public CompletionService Completion { get; } = new();
        public NavigationStub Navigation { get; } = new();
        public 기사상차PageViewModel Page { get; }
        public Fixture()
        {
            var api = DispatchProxy.Create<IDriverTransportApiService, ApiStub>(); Api = (ApiStub)(object)api;
            Page = new(new 기사운송상세조회Service(api, Auth), new(Completion), new Exceptions(), Navigation);
        }
        public async Task Prepare()
        {
            await Page.InitializeAsync(11);
            using var camera = MauiCargoPhotoTestSupport.UseSyntheticCapture(); await Page.사진.촬영Async();
            Page.현장.상차지확인 = Page.현장.화물상태확인 = Page.현장.화주연락확인 = true;
            Page.인수증.문서사진확인 = true;
            Assert.True(Page.완료가능);
        }
        public void Dispose() => Page.Dispose();
    }
    public class ApiStub : DispatchProxy
    {
        public string State = "상차지도착";
        public bool FailRead;
        public bool NullRead;
        public bool SignatureRequired;
        public List<long> DetailIds { get; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var id = (long)args![0]!;
            if (method?.Name == nameof(IDriverTransportApiService.상세조회Async))
            { DetailIds.Add(id); return FailRead ? Task.FromException<기사운송상세응답?>(new IOException("GET unavailable"))
                : Task.FromResult<기사운송상세응답?>(NullRead ? null : new() { Id = id, 운송번호 = $"cargo-{id}", 상태 = State, 인수증필요 = true, 인수증서명필수 = SignatureRequired }); }
            throw new NotSupportedException();
        }
    }
    private sealed class CompletionService : IDriverTransportCompletionPhotoService
    {
        public int Calls; public CancellationToken Token;
        public Func<DriverTransportCompletionPhoto, Task<DriverTransportCompletionPhotoResult>> Send = _ => throw new NotSupportedException();
        public Task<DriverTransportCompletionPhotoResult> CompleteWithPhotoAsync(DriverTransportCompletionPhoto photo, CancellationToken token = default)
        { Calls++; Token = token; return Send(photo); }
    }
    private sealed class NavigationStub : NavigationManager
    {
        public NavigationStub() => Initialize("http://localhost/", "http://localhost/");
        protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).ToString();
    }
    private sealed class Exceptions : IDriverTransportExceptionService
    { public Task<DriverTransportExceptionReportResult> ReportAsync(DriverTransportExceptionReport report, CancellationToken token = default) => throw new NotSupportedException(); }
    private sealed class TokenStore : IClientSecureTokenStore
    {
        private ClientAuthTokenSnapshot? snapshot = new("test-access", DateTime.UtcNow.AddHours(1), "test-refresh", DateTime.UtcNow.AddDays(1), "driver-a", "기사", ["Driver"]);
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken token = default) => Task.FromResult(snapshot);
        public Task SaveAsync(ClientAuthTokenSnapshot value, CancellationToken token = default) { snapshot = value; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken token = default) { snapshot = null; return Task.CompletedTask; }
    }
}
