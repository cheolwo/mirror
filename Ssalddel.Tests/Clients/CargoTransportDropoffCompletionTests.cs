using System.Reflection;
using DriverApp.Services;
using DriverApp.ViewModels.Driver.Transport;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Clients;

public sealed class CargoTransportDropoffCompletionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 완료응답이유실되어도_같은운송GET이완료이면_업무카드를종료한다(bool lost)
    {
        using var f = new Fixture(); await f.Prepare();
        f.Completion.Send = _ => { f.Api.State = "인수완료"; return lost
            ? Task.FromException<DriverTransportCompletionPhotoResult>(new IOException("lost completion response"))
            : Task.FromResult(new DriverTransportCompletionPhotoResult(true, true, "recorded")); };
        await f.Page.완료Async();
        Assert.Equal([11L, 11L], f.Api.DetailIds); Assert.Equal(1, f.Completion.Calls);
        Assert.True(f.Page.하차완료확인); Assert.False(f.Page.완료가능); Assert.False(f.Page.사진.사진있음);
        Assert.False(f.Page.완료결과확인필요); Assert.Contains("완료를 확인", f.Page.작업상태.메시지);
        var notice = Assert.Single(f.Page.CompletionFeedback.History);
        Assert.Equal("하차와 인계가 완료되었습니다", notice.Title); Assert.True(notice.IsFinal);
        Assert.Contains("정산과 실제 지급은 별도로", notice.Message);
        Assert.Same(notice, f.Page.CompletionFeedback.Current);
        Assert.Equal(DriverRoutes.CurrentTransport, f.Page.CompletionReturnHref);
        f.Page.CompletionFeedback.Dismiss();
        Assert.Null(f.Page.CompletionFeedback.Current);
        await f.Page.InitializeAsync(11);
        Assert.Single(f.Page.CompletionFeedback.History); Assert.Null(f.Page.CompletionFeedback.Current);
        await f.Page.완료Async(); Assert.Equal(1, f.Completion.Calls);
    }

    [Fact]
    public async Task 완료뒤GET실패는_사진과확인을유지하고_재조회전에는POST를반복하지않는다()
    {
        using var f = new Fixture(); await f.Prepare();
        f.Completion.Send = _ => { f.Api.FailRead = true; throw new IOException("lost completion response"); };
        await f.Page.완료Async();
        Assert.True(f.Page.완료결과확인필요); Assert.False(f.Page.완료가능);
        Assert.True(f.Page.사진.사진있음); Assert.True(f.Page.인계.완료);
        Assert.Empty(f.Page.CompletionFeedback.History); Assert.Null(f.Page.CompletionReturnHref);
        await f.Page.완료Async(); Assert.Equal(1, f.Completion.Calls);
        f.Api.FailRead = false; f.Api.State = "인수완료";
        await f.Page.InitializeAsync(11);
        Assert.True(f.Page.하차완료확인); Assert.False(f.Page.완료결과확인필요); Assert.False(f.Page.완료가능);
        Assert.Single(f.Page.CompletionFeedback.History);
    }

    [Fact]
    public async Task GET에서아직하차도착이면_기존사진과확인으로_명시재시도할수있다()
    {
        using var f = new Fixture(); await f.Prepare();
        f.Completion.Send = _ => Task.FromResult(new DriverTransportCompletionPhotoResult(true, false, "unknown", ObjectName: "private/proof-one"));
        await f.Page.완료Async();
        Assert.True(f.Page.완료가능); Assert.True(f.Page.인계.완료); Assert.True(f.Page.사진.사진있음);
        Assert.False(f.Page.완료결과확인필요); Assert.Equal(1, f.Completion.Calls);
        Assert.Empty(f.Page.CompletionFeedback.History);
        f.Completion.Send = photo => { Assert.Equal("private/proof-one", photo.ExistingObjectName); f.Api.State = "인수완료";
            return Task.FromResult(new DriverTransportCompletionPhotoResult(true, true, "recorded")); };
        await f.Page.완료Async(); Assert.Equal(2, f.Completion.Calls); Assert.True(f.Page.하차완료확인);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 계정변경과페이지이탈은_늦은완료응답을적용하거나재조회하지않는다(bool dispose)
    {
        using var f = new Fixture(); await f.Prepare();
        var pending = new TaskCompletionSource<DriverTransportCompletionPhotoResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Completion.Send = _ => pending.Task;
        var completing = f.Page.완료Async();
        if (dispose) f.Page.Dispose(); else await f.Auth.ClearAsync();
        Assert.True(f.Completion.Token.IsCancellationRequested);
        pending.SetResult(new(true, true, "late result", ObjectName: "private/old-proof")); await completing;
        Assert.Single(f.Api.DetailIds); Assert.False(f.Page.완료가능); Assert.Null(f.Page.하차완료.결과);
        Assert.Empty(f.Page.CompletionFeedback.History); Assert.Null(f.Page.CompletionReturnHref);
        if (!dispose) { Assert.Null(f.Page.운송); Assert.False(f.Page.사진.사진있음); }
    }

    [Fact]
    public async Task 이미완료된운송을열거나_다른운송으로전환하면_이전완료알림을만들지않는다()
    {
        using var f = new Fixture(); f.Api.State = "인수완료";
        await f.Page.InitializeAsync(11);
        Assert.True(f.Page.하차완료확인); Assert.Empty(f.Page.CompletionFeedback.History);
        Assert.Null(f.Page.CompletionReturnHref);
        f.Api.State = "하차지도착"; await f.Prepare();
        f.Completion.Send = _ => { f.Api.State = "인수완료"; return Task.FromResult(new DriverTransportCompletionPhotoResult(true, true, "recorded")); };
        await f.Page.완료Async(); Assert.Single(f.Page.CompletionFeedback.History);
        await f.Page.InitializeAsync(22);
        Assert.Empty(f.Page.CompletionFeedback.History); Assert.Null(f.Page.CompletionReturnHref);
    }

    [Theory]
    [InlineData("transport", false)]
    [InlineData("account", false)]
    [InlineData("dispose", false)]
    [InlineData("transport", true)]
    [InlineData("account", true)]
    [InlineData("dispose", true)]
    public async Task 운송전환과계정변경과이탈뒤_늦은촬영결과나오류를적용하지않는다(string transition, bool failed)
    {
        using var f = new Fixture(); await f.Page.InitializeAsync(11);
        var delayed = new DelayedPicker();
        using var camera = Microsoft.Maui.Media.MediaPicker.UseForCurrentTest(delayed);
        var capturing = f.Page.사진.촬영Async();
        Assert.True(f.Page.사진.촬영중);

        if (transition == "transport") await f.Page.InitializeAsync(22);
        else if (transition == "account") await f.Auth.ClearAsync();
        else f.Page.Dispose();
        f.Page.작업상태.설정("전환 뒤 현재 업무 안내", MudBlazor.Severity.Info);

        if (failed) delayed.Result.SetException(new IOException("late camera failure"));
        else delayed.Result.SetResult(new("previous-transport.jpg", "image/jpeg", [9, 8]));
        await capturing;

        Assert.False(f.Page.사진.사진있음); Assert.False(f.Page.사진.촬영중);
        Assert.Null(f.Page.사진.파일명); Assert.Null(f.Page.사진.미리보기Url);
        Assert.Equal("전환 뒤 현재 업무 안내", f.Page.작업상태.메시지);
        Assert.False(f.Page.완료가능); Assert.Equal(0, f.Completion.Calls);
        if (transition == "transport") Assert.Equal(22, f.Page.운송Id);
        if (transition == "account") Assert.Null(f.Page.운송);
    }

    [Fact]
    public async Task 이전운송의촬영종료는_현재운송의새촬영을종료시키거나사진을덮어쓰지않는다()
    {
        using var f = new Fixture(); await f.Page.InitializeAsync(11);
        var previous = new DelayedPicker();
        using var oldCamera = Microsoft.Maui.Media.MediaPicker.UseForCurrentTest(previous);
        var oldCapture = f.Page.사진.촬영Async();
        await f.Page.InitializeAsync(22);
        var current = new DelayedPicker();
        using var newCamera = Microsoft.Maui.Media.MediaPicker.UseForCurrentTest(current);
        var newCapture = f.Page.사진.촬영Async();
        previous.Result.SetResult(new("previous-transport.jpg", "image/jpeg", [9, 8]));
        await oldCapture;
        Assert.True(f.Page.사진.촬영중); Assert.False(f.Page.사진.사진있음);

        current.Result.SetResult(new("current-transport.jpg", "image/jpeg", [1, 2]));
        await newCapture;
        Assert.False(f.Page.사진.촬영중); Assert.Equal("current-transport.jpg", f.Page.사진.파일명);
        var photo = f.Page.사진.완료요청생성(DriverTransportCompletionPhotoKind.Dropoff, 22);
        Assert.Equal(22, photo.TransportId); Assert.Equal("current-transport.jpg", photo.FileName);
    }

    [Theory]
    [InlineData("lost")]
    [InlineData("null")]
    [InlineData("wrong-id")]
    public async Task 사진업로드완료와완료결과미확인은_미저장확정으로안내하지않는다(string response)
    {
        using var f = new Fixture(); await f.Auth.RestoreAsync();
        f.Api.Complete = id => response switch
        {
            "lost" => Task.FromException<기사운송상태변경응답?>(new HttpRequestException("lost response")),
            "null" => Task.FromResult<기사운송상태변경응답?>(null),
            _ => Task.FromResult<기사운송상태변경응답?>(new() { Id = id + 1, 상태 = "인수완료" })
        };
        using var client = new HttpClient();
        var service = new HttpDriverTransportCompletionPhotoService(client, f.Auth, null!, f.ApiService);
        var result = await service.CompleteWithPhotoAsync(new(DriverTransportCompletionPhotoKind.Dropoff, 11,
            "proof.jpg", "image/jpeg", [1, 2], ExistingObjectName: "private/uploaded-proof"));
        Assert.True(result.Uploaded); Assert.False(result.CompletionRecorded);
        Assert.Contains("결과를 확인하지 못", result.Message); Assert.DoesNotContain("저장되지 않았", result.Message);
        Assert.Equal("private/uploaded-proof", result.ObjectName);
    }

    [Fact]
    public async Task 사진완료서비스는_업로드후거절된업무사유를_안전한typed예외로보존한다()
    {
        using var f = new Fixture(); await f.Auth.RestoreAsync();
        f.Api.Complete = _ => Task.FromException<기사운송상태변경응답?>(Failure("CargoDropoffPhotoRequired", "NotApplied"));
        using var client = new HttpClient();
        var service = new HttpDriverTransportCompletionPhotoService(client, f.Auth, null!, f.ApiService);
        var result = await service.CompleteWithPhotoAsync(new(DriverTransportCompletionPhotoKind.Dropoff, 11,
            "proof.jpg", "image/jpeg", [1, 2], ExistingObjectName: "private/uploaded-proof"));
        Assert.True(result.Uploaded); Assert.False(result.CompletionRecorded);
        Assert.Equal("CargoDropoffPhotoRequired", result.CompletionFailure?.ErrorCode);
        Assert.Equal("NotApplied", result.CompletionFailure?.RecoveryProblem?.WorkflowContext?.OutcomeCode);
    }

    [Fact]
    public async Task 하차거절은_GET미완료후에도사유를유지하고_실제완료확인시에해소한다()
    {
        using var f = new Fixture(); await f.Prepare();
        f.Completion.Send = _ => Task.FromException<DriverTransportCompletionPhotoResult>(Failure("CargoDropoffPhotoRequired", "NotApplied"));
        await f.Page.완료Async();
        Assert.Equal("하차 사진 필요", 업무오류안내Presentation.생성(f.Page.완료실패!).제목);
        Assert.True(f.Page.완료실패상태안내인가); Assert.True(f.Page.사진.사진있음);
        Assert.False(f.Page.완료결과확인필요);
        f.Api.State = "인수완료"; await f.Page.InitializeAsync(11);
        Assert.Null(f.Page.완료실패); Assert.False(f.Page.사진.사진있음);
    }

    [Fact]
    public async Task 서버미확정하차는_미완료GET뒤에도_결과조회만허용한다()
    {
        using var f = new Fixture(); await f.Prepare();
        f.Completion.Send = _ => Task.FromResult(new DriverTransportCompletionPhotoResult(true, false, "확인 필요",
            CompletionFailure: Failure("UnexpectedFailure", "Unknown")));
        await f.Page.완료Async();
        Assert.True(f.Page.완료결과확인필요); Assert.False(f.Page.완료가능);
        Assert.Equal("하차 결과 확인 필요", 업무오류안내Presentation.생성(f.Page.완료실패!).제목);
        await f.Page.완료Async(); Assert.Equal(1, f.Completion.Calls);
        f.Api.State = "인수완료"; await f.Page.InitializeAsync(11);
        Assert.Null(f.Page.완료실패); Assert.False(f.Page.완료결과확인필요);
    }

    private static DriverApiException Failure(string code, string outcome)
    {
        var metadata = SsalddelApiProblemParser.Parse(System.Text.Json.JsonSerializer.Serialize(new
        {
            errorCode = code, availableRecoveryActions = new[] { "RefreshState" },
            workflowContext = new { operatingSystemId = "DomesticCargoTransportOS", stageId = "cargo.dropoff", outcomeCode = outcome }
        }));
        return new(System.Net.HttpStatusCode.BadRequest, "하차 완료", code, null, metadata);
    }

    private sealed class Fixture : IDisposable
    {
        public AuthSession Auth { get; } = new(new TokenStore(), new ClientSessionGuard());
        public IDriverTransportApiService ApiService { get; }
        public ApiStub Api { get; }
        public CompletionService Completion { get; } = new();
        public 기사하차PageViewModel Page { get; }
        public Fixture()
        {
            ApiService = DispatchProxy.Create<IDriverTransportApiService, ApiStub>(); Api = (ApiStub)(object)ApiService;
            Page = new(new 기사운송상세조회Service(ApiService, Auth), new(Completion), new Exceptions());
        }
        public async Task Prepare()
        {
            await Page.InitializeAsync(11);
            using var camera = MauiCargoPhotoTestSupport.UseSyntheticCapture(); await Page.사진.촬영Async();
            Page.인계.하차지확인 = Page.인계.수령자확인 = Page.인계.결제증빙확인 = true;
            Assert.True(Page.완료가능);
        }
        public void Dispose() => Page.Dispose();
    }
    public class ApiStub : DispatchProxy
    {
        public string State = "하차지도착";
        public bool FailRead;
        public List<long> DetailIds { get; } = [];
        public Func<long, Task<기사운송상태변경응답?>> Complete = id => Task.FromResult<기사운송상태변경응답?>(new() { Id = id, 상태 = "인수완료" });
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var id = (long)args![0]!;
            if (method?.Name == nameof(IDriverTransportApiService.상세조회Async))
            { DetailIds.Add(id); return FailRead ? Task.FromException<기사운송상세응답?>(new IOException("GET unavailable"))
                : Task.FromResult<기사운송상세응답?>(new() { Id = id, 운송번호 = $"cargo-{id}", 상태 = State }); }
            if (method?.Name == nameof(IDriverTransportApiService.하차완료Async)) return Complete(id);
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
    private sealed class DelayedPicker : Microsoft.Maui.Media.IMediaPicker
    {
        public bool IsCaptureSupported => true;
        public TaskCompletionSource<Microsoft.Maui.Storage.FileResult?> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<Microsoft.Maui.Storage.FileResult?> CapturePhotoAsync(Microsoft.Maui.Media.MediaPickerOptions? options = null) => Result.Task;
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
