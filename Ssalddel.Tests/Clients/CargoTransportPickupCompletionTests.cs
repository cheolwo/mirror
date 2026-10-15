using System.Reflection;
using DriverApp.Services;
using DriverApp.ViewModels.Driver.Transport;
using Microsoft.AspNetCore.Components;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Driver.Transport;

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
