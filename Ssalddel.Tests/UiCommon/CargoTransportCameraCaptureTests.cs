using Microsoft.AspNetCore.Components.Forms;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

namespace Ssalddel.Tests.Ui.Common;

/// <summary>가짜 촬영 포트로 앱 입력 수명을 검사한다. 실제 권한·카메라 촬영·서버 증빙 검증은 아니다.</summary>
public sealed class CargoTransportCameraCaptureTests
{
    [Theory]
    [InlineData("pickup", "상차지도착", "상차완료")]
    [InlineData("dropoff", "하차지도착", "인수완료")]
    public async Task 촬영은자동완료하지않고기존업로드와서버확인경로를그대로사용한다(string action, string state, string finalState)
    {
        var bytes = new byte[] { 255, 216, 255, 42 };
        var capture = new Capture(() => Task.FromResult<업무촬영사진?>(new("captured.jpg", "image/jpeg", bytes)));
        var api = new Api(state);
        using var model = Model(api, capture);
        await model.LoadAsync(1, action); Confirm(model);
        Assert.False(model.CanSubmit);

        await model.CapturePhotoAsync();
        Assert.Equal(1, capture.Calls); Assert.Equal("captured.jpg", model.PhotoName);
        Assert.Equal(state, model.Source!.상태); Assert.False(model.CompletionConfirmed);
        Assert.Equal(0, api.Uploads); Assert.Equal(0, api.Writes);
        Assert.True(model.CanSubmit);
        bytes[0] = 0; // 포트가 반환한 배열 변경이 보관/업로드할 사진을 바꾸지 않는다.
        await model.SubmitAsync();

        Assert.Equal(new byte[] { 255, 216, 255, 42 }, api.UploadedBytes);
        Assert.Equal(1, api.Uploads); Assert.Equal(1, api.Writes);
        Assert.Equal(finalState, model.Source!.상태); Assert.True(model.Saved);
        Assert.True(model.CompletionConfirmed);
    }

    [Fact]
    public async Task 촬영취소는기존사진을보존하지만완료를막고명시적파일선택으로회복한다()
    {
        var api = new Api("상차지도착");
        var capture = new Capture(() => Task.FromResult<업무촬영사진?>(null));
        using var model = Model(api, capture);
        await model.LoadAsync(1, "pickup"); Confirm(model); model.SetPhoto(new Photo());
        Assert.True(model.CanSubmit);

        await model.CapturePhotoAsync();
        Assert.Equal("selected.jpg", model.PhotoName); Assert.Contains("취소", model.PhotoCaptureMessage);
        Assert.False(model.CanSubmit); Assert.False(model.Lifetime.HasError);
        await model.SubmitAsync();
        Assert.Equal(0, api.Uploads); Assert.Equal(0, api.Writes);
        model.SetPhoto(new Photo());
        Assert.Null(model.PhotoCaptureMessage); Assert.True(model.CanSubmit);
    }

    [Theory]
    [InlineData("permission")]
    [InlineData("camera")]
    [InlineData("unsupported-type")]
    [InlineData("oversize")]
    public async Task 권한거부와재촬영오류는이전사진으로조용히완료하지않고파일선택을남긴다(string failure)
    {
        var api = new Api("하차지도착");
        var capture = new Capture(() => failure switch
        {
            "permission" => throw new UnauthorizedAccessException("Synthetic permission rejection"),
            "camera" => throw new InvalidOperationException("Synthetic camera failure"),
            "unsupported-type" => Task.FromResult<업무촬영사진?>(new("invalid.pdf", "application/pdf", [1, 2, 3])),
            _ => Task.FromResult<업무촬영사진?>(new("large.jpg", "image/jpeg", new byte[CargoTransportActionViewModel.MaxPhotoBytes + 1]))
        });
        using var model = Model(api, capture);
        await model.LoadAsync(1, "dropoff"); Confirm(model); model.SetPhoto(new Photo());
        await model.CapturePhotoAsync();

        Assert.Equal("selected.jpg", model.PhotoName); Assert.NotNull(model.PhotoCaptureMessage);
        Assert.False(model.CanSubmit); Assert.True(model.CanCapturePhoto); Assert.False(model.Lifetime.IsBusy);
        await model.SubmitAsync(); Assert.Equal(0, api.Uploads); Assert.Equal(0, api.Writes);
        model.SetPhoto(new Photo()); Assert.True(model.CanSubmit);
    }

    [Fact]
    public async Task 웹미지원포트에서는기존파일선택과8메가제한을그대로유지한다()
    {
        var api = new Api("상차지도착");
        var capture = new Capture(() => throw new InvalidOperationException()) { IsSupported = false };
        using var model = Model(api, capture);
        await model.LoadAsync(1, "pickup"); Confirm(model);
        Assert.False(model.HasCameraSupport); Assert.False(model.CanCapturePhoto);
        await model.CapturePhotoAsync(); Assert.Equal(0, capture.Calls);
        model.SetPhoto(new Photo()); Assert.True(model.CanSubmit);
        Assert.Throws<ArgumentException>(() => model.SetPhoto(new Photo(CargoTransportActionViewModel.MaxPhotoBytes + 1)));
        Assert.Equal("selected.jpg", model.PhotoName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 계정이나입력화면이바뀐뒤돌아온촬영파일을다른업무에적용하지않는다(bool switchScreen)
    {
        var pending = new TaskCompletionSource<업무촬영사진?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new Api("상차지도착"); var access = new Access();
        using var model = Model(api, new Capture(() => pending.Task), access);
        await model.LoadAsync(1, "pickup"); Confirm(model);
        var running = model.CapturePhotoAsync(); Assert.True(model.Lifetime.IsBusy);
        if (switchScreen) { api.State = "하차지도착"; await model.LoadAsync(2, "dropoff"); }
        else access.Switch("another-driver");
        pending.SetResult(new("late.jpg", "image/jpeg", [255, 216, 255]));
        await running;

        Assert.Null(model.PhotoName); Assert.False(model.CanSubmit); Assert.False(model.Saved);
        Assert.Equal(0, api.Uploads); Assert.Equal(0, api.Writes);
        if (switchScreen) { Assert.Equal(2, model.ItemId); Assert.Equal("dropoff", model.ActionKey); }
        else Assert.Null(model.Source);
    }

    [Fact]
    public async Task 사진촬영성공도서버완료거절을완료상태로바꾸지않는다()
    {
        var api = new Api("상차지도착") { RejectCompletion = true };
        using var model = Model(api, new Capture(() => Task.FromResult<업무촬영사진?>(new("camera.jpg", "image/jpeg", [255, 216, 255]))));
        await model.LoadAsync(1, "pickup"); Confirm(model); await model.CapturePhotoAsync();
        await model.SubmitAsync();
        Assert.Equal(1, api.Uploads); Assert.Equal(1, api.Writes);
        Assert.Equal("상차지도착", model.Source!.상태); Assert.False(model.Saved); Assert.False(model.CompletionConfirmed);
    }

    private static CargoTransportActionViewModel Model(Api api, Capture capture, Access? access = null)
        => new(new(api), api, access ?? new(), capture);
    private static void Confirm(CargoTransportActionViewModel model)
        => model.CargoConfirmed = model.LocationConfirmed = model.RecipientConfirmed = model.PaymentConfirmed = model.SaveConfirmed = true;
    private sealed class Capture(Func<Task<업무촬영사진?>> result) : I업무사진촬영Adapter
    {
        public bool IsSupported { get; init; } = true;
        public int Calls { get; private set; }
        public Task<업무촬영사진?> CaptureAsync(string title, CancellationToken ct) { Calls++; return result(); }
    }
    private sealed class Photo(long size = 3) : IBrowserFile
    {
        public string Name => "selected.jpg";
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => size;
        public string ContentType => "image/jpeg";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) => new MemoryStream([1, 2, 3]);
    }
    private sealed class Api(string state) : IRoleWorkspaceApi
    {
        public string State { get; set; } = state;
        public int Uploads { get; private set; }
        public int Writes { get; private set; }
        public byte[]? UploadedBytes { get; private set; }
        public bool RejectCompletion { get; init; }
        public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken ct)
            => Task.FromResult((T)(object)new 기사운송상세응답 { Id = long.Parse(path.Split('/')[^1]), 상태 = State });
        public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken ct)
        {
            Writes++;
            if (RejectCompletion) throw new InvalidOperationException("Synthetic server rejection");
            State = path.EndsWith("/pickup-complete", StringComparison.Ordinal) ? "상차완료" : "인수완료";
            return Task.FromResult((T?)(object)new 기사운송상태변경응답 { Id = 1, 상태 = State });
        }
        public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken ct) => throw new NotSupportedException();
        public async Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken ct)
        {
            Assert.Equal("api/v1/files/upload", path);
            var parts = Assert.IsType<MultipartFormDataContent>(body);
            var file = Assert.Single(parts.Where(x => x.Headers.ContentDisposition?.Name?.Trim('"') == "file"));
            UploadedBytes = await file.ReadAsByteArrayAsync(ct); Uploads++;
            return (T?)(object)new CargoEvidenceUploadResponse("existing-upload-object", null);
        }
    }
    private sealed class Access : IRoleWorkspaceAccess
    {
        private RoleWorkspaceIdentity _identity = new("driver", 1, true);
        public event Action? Changed;
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => _identity;
        public Task EnsureInitializedAsync(string roleKey, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignOutAsync(string roleKey, CancellationToken ct = default) { Switch(null); return Task.CompletedTask; }
        public void Switch(string? owner) { _identity = new(owner, _identity.Revision + 1, owner is not null); Changed?.Invoke(); }
    }
}
