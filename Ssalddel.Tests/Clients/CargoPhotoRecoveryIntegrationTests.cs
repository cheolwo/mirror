using System.Net;
using System.Reflection;
using DriverApp.Services;
using DriverApp.ViewModels.Driver.Transport;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Driver.Transport;

namespace Ssalddel.Tests.Clients;

public sealed class CargoPhotoRecoveryIntegrationTests
{
    [Fact]
    public async Task 완료전_사진보관과업로드영수증보관을차례로확인한다()
    {
        using var f = await Fixture.CreateAsync();
        var result = await f.Service.CompleteWithPhotoAsync(Photo());
        Assert.True(result.Uploaded); Assert.True(result.CompletionRecorded);
        Assert.Equal(new[] { "GET", "SAVE", "UPLOAD", "SAVE", "POST" }, f.Trace);
        Assert.Null(f.Store.Saved[0].Photo.ExistingObjectName);
        Assert.Equal("private/proof", f.Store.Saved[1].Photo.ExistingObjectName);
        Assert.All(f.Store.Saved, saved => Assert.Equal("driver-one", saved.Owner));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    public async Task 보호저장실패는_완료POST전에멈춘다(int failedSave, int uploads)
    {
        using var f = await Fixture.CreateAsync();
        f.Store.SaveHook = (_, _) => f.Store.Saved.Count == failedSave ? Task.FromException(new IOException("protected store failed")) : Task.CompletedTask;
        var result = await f.Service.CompleteWithPhotoAsync(Photo());
        Assert.True(result.CompletionNotSubmitted); Assert.False(result.CompletionRecorded);
        Assert.Equal(uploads, f.Handler.Uploads);
        Assert.Equal(0, f.Api.Completions);
    }

    [Fact]
    public async Task 현재계정운송을확인한뒤_보관된업로드영수증으로재개한다()
    {
        using var f = await Fixture.CreateAsync();
        var result = await f.Service.CompleteWithPhotoAsync(Photo() with { ExistingObjectName = "private/proof" });
        Assert.True(result.CompletionRecorded);
        Assert.Equal(0, f.Handler.Uploads);
        Assert.Equal(new[] { "GET", "SAVE", "SAVE", "POST" }, f.Trace);
    }

    [Fact]
    public async Task GET으로이미완료된단계를확인하면_POST나사진업로드를반복하지않는다()
    {
        using var f = await Fixture.CreateAsync(); f.Api.State = "인수완료";
        var result = await f.Service.CompleteWithPhotoAsync(Photo());
        Assert.True(result.CompletionRecorded);
        Assert.False(result.Uploaded);
        Assert.Equal(new[] { "GET" }, f.Trace);
    }

    [Theory]
    [InlineData("상차지도착")]
    [InlineData("확정")]
    public async Task 다른현재단계는_사진보관업로드완료명령을보내지않는다(string state)
    {
        using var f = await Fixture.CreateAsync(); f.Api.State = state;
        var result = await f.Service.CompleteWithPhotoAsync(Photo());
        Assert.True(result.CompletionNotSubmitted); Assert.False(result.CompletionRecorded);
        Assert.Equal(new[] { "GET" }, f.Trace);
    }

    [Fact]
    public async Task 상세조회중계정이바뀌면_이전사진을새계정으로제출하지않는다()
    {
        using var f = await Fixture.CreateAsync();
        var response = new TaskCompletionSource<기사운송상세응답?>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Api.DetailQuery = _ => response.Task;
        var operation = f.Service.CompleteWithPhotoAsync(Photo());
        await f.Auth.ApplyAsync(Snapshot("driver-two"));
        response.SetResult(new() { Id = 11, 상태 = "하차지도착" });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.Empty(f.Store.Saved);
        Assert.Equal(0, f.Handler.Uploads); Assert.Equal(0, f.Api.Completions);
    }

    [Fact]
    public async Task 사진보관중로그아웃되면_업로드나완료명령을전송하지않는다()
    {
        using var f = await Fixture.CreateAsync();
        f.Store.SaveHook = async (_, _) => await f.Auth.ClearAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service.CompleteWithPhotoAsync(Photo()));
        Assert.Equal(0, f.Handler.Uploads); Assert.Equal(0, f.Api.Completions);
    }

    [Fact]
    public async Task 하차페이지재진입은_사진만복원하고인계확인을자동완료하지않는다()
    {
        using var f = await Fixture.CreateAsync(); f.Store.Photo = Photo() with { ExistingObjectName = "private/proof" };
        using var page = new 기사하차PageViewModel(new(f.ApiService, f.Auth), new(f.Service), new Exceptions(), f.Store);
        await page.InitializeAsync(11);
        Assert.True(page.사진.사진있음); Assert.False(page.인계.완료); Assert.False(page.완료가능);
        var resumed = page.사진.완료요청생성(DriverTransportCompletionPhotoKind.Dropoff, 11);
        Assert.Equal("private/proof", resumed.ExistingObjectName);
        Assert.Null(resumed.ReceiptEvidence);
        Assert.Equal(0, f.Api.Completions);
    }

    [Fact]
    public async Task 현재운송GET이하차완료를확인하면_그대상의복구사진만정리한다()
    {
        using var f = await Fixture.CreateAsync(); f.Api.State = "인수완료"; f.Store.Photo = Photo();
        using var page = new 기사하차PageViewModel(new(f.ApiService, f.Auth), new(f.Service), new Exceptions(), f.Store);
        await page.InitializeAsync(11);
        Assert.True(page.하차완료확인); Assert.False(page.사진.사진있음);
        Assert.Equal(("driver-one", DriverTransportCompletionPhotoKind.Dropoff, 11L), Assert.Single(f.Store.Removed));
        Assert.Equal(0, f.Api.Completions);
    }

    [Fact]
    public async Task 완료뒤GETnull은_사진과확인을보존하고동일조건재조회뒤에만다시요청할수있다()
    {
        using var f = await Fixture.CreateAsync(); f.Store.Photo = Photo() with { ExistingObjectName = "private/proof" };
        using var page = new 기사하차PageViewModel(new(f.ApiService, f.Auth), new(f.Service), new Exceptions(), f.Store);
        await page.InitializeAsync(11);
        page.인계.하차지확인 = page.인계.수령자확인 = page.인계.결제증빙확인 = true;
        f.Api.DetailQuery = id => Task.FromResult<기사운송상세응답?>(f.Api.Completions == 0
            ? new() { Id = id, 운송번호 = $"cargo-{id}", 상태 = "하차지도착" } : null);
        await page.완료Async();
        Assert.Null(page.운송); Assert.True(page.완료결과확인필요); Assert.False(page.완료가능);
        Assert.True(page.사진.사진있음); Assert.True(page.인계.완료);
        Assert.Empty(f.Store.Removed);
        await page.완료Async(); Assert.Equal(1, f.Api.Completions);
        f.Api.DetailQuery = id => Task.FromResult<기사운송상세응답?>(new() { Id = id, 운송번호 = $"cargo-{id}", 상태 = "하차지도착" });
        await page.새로고침Async();
        Assert.False(page.완료결과확인필요); Assert.True(page.완료가능);
        Assert.True(page.인계.완료); Assert.True(page.사진.사진있음);
    }

    [Fact]
    public async Task 복원실패시사진이없어도_해당대상암호문을명시폐기할수있다()
    {
        var store = new StoreStub([]) { LoadHook = _ => Task.FromException<DriverTransportCompletionPhoto?>(new InvalidDataException()) };
        var status = new 기사운송작업상태ViewModel();
        var photo = new 기사운송사진ViewModel("하차", "하차 사진", status, store);
        photo.복구설정("driver-one", DriverTransportCompletionPhotoKind.Dropoff, 11, 1);
        await photo.복원Async();
        Assert.False(photo.사진있음); Assert.True(photo.복구자료폐기가능);
        await photo.제거Async();
        Assert.Equal(("driver-one", DriverTransportCompletionPhotoKind.Dropoff, 11L), Assert.Single(store.Removed));
        Assert.False(photo.복구자료폐기가능);
    }

    [Fact]
    public async Task 촬영사진의보호저장실패시_사진을유지하고명시폐기를안내한다()
    {
        var store = new StoreStub([]) { SaveHook = (_, _) => Task.FromException(new IOException()) };
        var status = new 기사운송작업상태ViewModel();
        var photo = new 기사운송사진ViewModel("하차", "하차 사진", status, store);
        photo.복구설정("driver-one", DriverTransportCompletionPhotoKind.Dropoff, 11, 1);
        using var camera = MauiCargoPhotoTestSupport.UseSyntheticCapture();
        await photo.촬영Async();
        Assert.True(photo.사진있음); Assert.True(photo.복구자료폐기가능);
        Assert.Contains("보호 보관", status.메시지);
    }

    [Fact]
    public async Task 늦은사진복원은_운송이나인증문맥초기화뒤표시하지않는다()
    {
        var pending = new TaskCompletionSource<DriverTransportCompletionPhoto?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new StoreStub([]) { LoadHook = _ => pending.Task };
        var photo = new 기사운송사진ViewModel("하차", "하차 사진", new(), store);
        photo.복구설정("driver-one", DriverTransportCompletionPhotoKind.Dropoff, 11, 1);
        var restore = photo.복원Async();
        photo.초기화();
        pending.SetResult(Photo());
        await restore;
        Assert.False(photo.사진있음); Assert.Null(photo.미리보기Url);
    }

    [Fact]
    public async Task 서버와같은사진상한을사용하고_큰사진은재촬영안내로차단한다()
    {
        Assert.Equal(Ssalddel.Application.Driver.Transport.화물운송사진증빙Policy.MaximumBytes,
            DriverTransportCompletionPhotoLimits.MaximumUploadBytes);
        var status = new 기사운송작업상태ViewModel();
        var store = new StoreStub([]);
        var photo = new 기사운송사진ViewModel("하차", "하차 사진", status, store);
        photo.복구설정("driver-one", DriverTransportCompletionPhotoKind.Dropoff, 11, 1);
        using var capture = Microsoft.Maui.Media.MediaPicker.UseForCurrentTest(new LargePicker());
        await photo.촬영Async();
        Assert.False(photo.사진있음); Assert.Empty(store.Saved); Assert.Contains("5MB", status.메시지);
    }

    private static DriverTransportCompletionPhoto Photo() => new(DriverTransportCompletionPhotoKind.Dropoff, 11, "proof.png", "image/png", [1, 2, 3]);
    private static ClientAuthTokenSnapshot Snapshot(string id = "driver-one")
        => new("test-access", DateTime.UtcNow.AddHours(1), "test-refresh", DateTime.UtcNow.AddDays(1), id, "기사", ["Driver"]);
    private sealed class Fixture : IDisposable
    {
        public List<string> Trace { get; } = [];
        public AuthSession Auth { get; } = new(new TokenStore(), new ClientSessionGuard());
        public IDriverTransportApiService ApiService { get; }
        public TransportStub Api { get; }
        public StoreStub Store { get; }
        public UploadHandler Handler { get; }
        public HttpClient Client { get; }
        public HttpDriverTransportCompletionPhotoService Service { get; }
        private Fixture()
        {
            ApiService = DispatchProxy.Create<IDriverTransportApiService, TransportStub>(); Api = (TransportStub)(object)ApiService; Api.Trace = Trace;
            Store = new(Trace); Handler = new(Trace); Client = new(Handler) { BaseAddress = new("https://example.invalid/") };
            Service = new(Client, Auth, new(Client, Auth), ApiService, Store);
        }
        public static async Task<Fixture> CreateAsync() { var result = new Fixture(); await result.Auth.RestoreAsync(); return result; }
        public void Dispose() => Client.Dispose();
    }
    public sealed class StoreStub(List<string> trace) : IDriverCargoPhotoRecoveryStore
    {
        public List<(string Owner, DriverTransportCompletionPhoto Photo)> Saved { get; } = [];
        public List<(string Owner, DriverTransportCompletionPhotoKind Kind, long Id)> Removed { get; } = [];
        public DriverTransportCompletionPhoto? Photo;
        public Func<string, DriverTransportCompletionPhoto, Task>? SaveHook;
        public Func<CancellationToken, Task<DriverTransportCompletionPhoto?>>? LoadHook;
        public async Task SaveAsync(string owner, DriverTransportCompletionPhoto photo, CancellationToken token = default)
        { trace.Add("SAVE"); Saved.Add((owner, photo)); if (SaveHook is not null) await SaveHook(owner, photo); Photo = photo; }
        public Task<DriverTransportCompletionPhoto?> LoadAsync(string owner, DriverTransportCompletionPhotoKind kind, long id, CancellationToken token = default)
            => LoadHook?.Invoke(token) ?? Task.FromResult(Photo is { } p && p.Kind == kind && p.TransportId == id ? p : null);
        public Task RemoveAsync(string owner, DriverTransportCompletionPhotoKind kind, long id, CancellationToken token = default)
        { Removed.Add((owner, kind, id)); Photo = null; return Task.CompletedTask; }
    }
    public class TransportStub : DispatchProxy
    {
        public List<string> Trace = [];
        public string State = "하차지도착";
        public int Completions;
        public Func<long, Task<기사운송상세응답?>>? DetailQuery;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var id = (long)args![0]!;
            if (method?.Name == nameof(IDriverTransportApiService.상세조회Async))
            { Trace.Add("GET"); return DetailQuery?.Invoke(id) ?? Task.FromResult<기사운송상세응답?>(new() { Id = id, 운송번호 = $"cargo-{id}", 상태 = State }); }
            if (method?.Name == nameof(IDriverTransportApiService.하차완료Async))
            { Trace.Add("POST"); Completions++; return Task.FromResult<기사운송상태변경응답?>(new() { Id = id, 상태 = "인수완료" }); }
            throw new InvalidOperationException("예상하지 않은 운송 명령");
        }
    }
    private sealed class UploadHandler(List<string> trace) : HttpMessageHandler
    {
        public int Uploads;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method); Assert.Equal("/api/v1/files/upload", request.RequestUri!.AbsolutePath);
            trace.Add("UPLOAD"); Uploads++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"objectName\":\"private/proof\",\"url\":\"private-url\"}", System.Text.Encoding.UTF8, "application/json") });
        }
    }
    private sealed class LargePicker : Microsoft.Maui.Media.IMediaPicker
    {
        public bool IsCaptureSupported => true;
        public Task<Microsoft.Maui.Storage.FileResult?> CapturePhotoAsync(Microsoft.Maui.Media.MediaPickerOptions? options = null)
            => Task.FromResult<Microsoft.Maui.Storage.FileResult?>(new("proof.jpg", "image/jpeg", new byte[DriverTransportCompletionPhotoLimits.MaximumUploadBytes + 1]));
    }
    private sealed class Exceptions : IDriverTransportExceptionService
    { public Task<DriverTransportExceptionReportResult> ReportAsync(DriverTransportExceptionReport report, CancellationToken token = default) => throw new NotSupportedException(); }
    private sealed class TokenStore : IClientSecureTokenStore
    {
        private ClientAuthTokenSnapshot? _snapshot = Snapshot();
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken token = default) => Task.FromResult(_snapshot);
        public Task SaveAsync(ClientAuthTokenSnapshot value, CancellationToken token = default) { _snapshot = value; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken token = default) { _snapshot = null; return Task.CompletedTask; }
    }
}
