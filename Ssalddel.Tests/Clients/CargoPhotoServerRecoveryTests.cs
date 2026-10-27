using System.Security.Cryptography;
using System.Text;
using DriverApp.Services;
using DriverApp.ViewModels.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Clients;

/// <summary>사진 암호화 파일과 현재 계정·서버의 결속입니다. 실제 카메라 또는 운영 업로드 시험이 아닙니다.</summary>
public sealed class CargoPhotoServerRecoveryTests
{
    [Theory]
    [InlineData("server")]
    [InlineData("account")]
    [InlineData("logout")]
    public async Task 카메라를연후문맥이바뀌면_반환된이전사진을새서버에저장하거나표시하지않는다(string change)
    {
        using var f = new Fixture();
        var status = new 기사운송작업상태ViewModel();
        var photo = new 기사운송사진ViewModel("상차", "상차 사진", status, f.NewStore());
        photo.복구설정("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11, f.Revision);
        var camera = new DelayedPicker();
        using var adapter = Microsoft.Maui.Media.MediaPicker.UseForCurrentTest(camera);
        var capturing = photo.촬영Async();
        await camera.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (change == "server") f.Endpoint = new("https://second.test/");
        else if (change == "account") { f.Owner = "driver-two"; f.Revision++; }
        else f.Authenticated = false;
        camera.Result.SetResult(new("camera-a.jpg", "image/jpeg", [1, 2, 3]));
        await capturing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(photo.사진있음); Assert.Null(photo.미리보기Url); Assert.False(photo.촬영중);
        Assert.False(Directory.Exists(f.Root)); Assert.Contains("이전 사진을 적용하지", status.메시지);
    }

    [Fact]
    public async Task 고정한사진문맥은_나중현재서버를새범위로채택하지않는다()
    {
        using var f = new Fixture(); var store = f.NewStore();
        var original = store.Capture("driver-one");
        await store.SaveAsync(original, Photo());
        f.Endpoint = new("https://second.test/");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(original, Photo()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.LoadAsync(original, DriverTransportCompletionPhotoKind.Pickup, 11));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.RemoveAsync(original, DriverTransportCompletionPhotoKind.Pickup, 11));
        Assert.Single(Directory.GetFiles(f.Root));
        Assert.Null(await store.LoadAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11));
        f.Endpoint = new("https://first.test/");
        Assert.NotNull(await store.LoadAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11));
    }

    [Fact]
    public async Task 화면의서버가바뀌면_이미복원한사진도완료요청으로재사용하지않는다()
    {
        using var f = new Fixture(); var store = f.NewStore(); await store.SaveAsync("driver-one", Photo());
        var photo = new 기사운송사진ViewModel("상차", "상차 사진", new(), store);
        photo.복구설정("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11, f.Revision);
        await photo.복원Async(); Assert.True(photo.사진있음);
        f.Endpoint = new("https://second.test/");
        Assert.Throws<InvalidOperationException>(() => photo.완료요청생성(DriverTransportCompletionPhotoKind.Pickup, 11));
        Assert.False(photo.사진있음); Assert.Null(photo.미리보기Url); Assert.Single(Directory.GetFiles(f.Root));
    }

    [Fact]
    public async Task 새저장소인스턴스가_동일계정서버의사진만복원한다()
    {
        using var f = new Fixture();
        await f.NewStore().SaveAsync("driver-one", Photo());
        f.Revision++;
        var restored = await f.NewStore().LoadAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11);
        Assert.Equal(Photo().Bytes, restored!.Bytes);
        Assert.Equal("private-upload", restored.ExistingObjectName);
        Assert.Single(Directory.GetFiles(f.Root));
    }

    [Fact]
    public async Task 같은계정운송번호라도_다른서버의사진은복원하거나삭제하지않는다()
    {
        using var f = new Fixture();
        await f.NewStore().SaveAsync("driver-one", Photo());
        f.Endpoint = new("https://second.test/");
        Assert.Null(await f.NewStore().LoadAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11));
        await f.NewStore().RemoveAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11);
        f.Endpoint = new("https://first.test/");
        Assert.NotNull(await f.NewStore().LoadAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11));
        Assert.Single(Directory.GetFiles(f.Root));
    }

    [Fact]
    public async Task 다른계정의사진요청과로그아웃상태는_보호캐시에접근하지않는다()
    {
        using var f = new Fixture(); await f.NewStore().SaveAsync("driver-one", Photo());
        f.Owner = "driver-two"; f.Revision++;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.NewStore().LoadAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11));
        Assert.Null(await f.NewStore().LoadAsync("driver-two", DriverTransportCompletionPhotoKind.Pickup, 11));
        f.Authenticated = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.NewStore().LoadAsync("driver-two", DriverTransportCompletionPhotoKind.Pickup, 11));
        Assert.Single(Directory.GetFiles(f.Root));
    }

    [Fact]
    public async Task 서버정보없는과거사진은_자동이동하거나복원하거나삭제하지않는다()
    {
        using var f = new Fixture(); var raw = f.NewRawStore();
        await raw.SaveAsync("driver-one", Photo());
        var original = await File.ReadAllBytesAsync(Assert.Single(Directory.GetFiles(f.Root)));
        var store = f.NewStore();
        Assert.Null(await store.LoadAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11));
        await store.RemoveAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11);
        Assert.Equal(original, await File.ReadAllBytesAsync(Assert.Single(Directory.GetFiles(f.Root))));
        Assert.NotNull(await raw.LoadAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11));
    }

    [Theory]
    [InlineData("account")]
    [InlineData("server")]
    [InlineData("logout")]
    public async Task 늦은사진읽기뒤문맥이바뀌면_사진을폐기하고새화면에반영하지않는다(string change)
    {
        using var f = new Fixture(); var delayed = new DelayedStore();
        var store = new DriverCargoServerPhotoRecoveryStore(delayed, f.Context());
        var reading = store.LoadAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11);
        if (change == "account") { f.Owner = "driver-two"; f.Revision++; }
        else if (change == "server") f.Endpoint = new("https://second.test/");
        else f.Authenticated = false;
        var photo = Photo(); delayed.Result.SetResult(photo);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading);
        Assert.All(photo.Bytes, value => Assert.Equal((byte)0, value));
    }

    private static DriverTransportCompletionPhoto Photo()
        => new(DriverTransportCompletionPhotoKind.Pickup, 11, "proof.jpg", "image/jpeg", Encoding.UTF8.GetBytes("synthetic-photo"),
            ExistingObjectName: "private-upload", ExistingUrl: "private-url");

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "cargo-photo-server-recovery-tests", Guid.NewGuid().ToString("N"));
        public byte[] Key { get; } = RandomNumberGenerator.GetBytes(32);
        public string Owner { get; set; } = "driver-one";
        public long Revision { get; set; } = 1;
        public bool Authenticated { get; set; } = true;
        public Uri Endpoint { get; set; } = new("https://first.test/");
        public WorkflowRecoveryContext Context() => new(() => new RoleWorkspaceIdentity(Owner, Revision, Authenticated), () => Endpoint);
        public DriverCargoPhotoRecoveryStore NewRawStore() => new(Root, () => Task.FromResult(Key));
        public DriverCargoServerPhotoRecoveryStore NewStore() => new(NewRawStore(), Context());
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }

    private sealed class DelayedStore : IDriverCargoPhotoRecoveryStore
    {
        public TaskCompletionSource<DriverTransportCompletionPhoto?> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<DriverTransportCompletionPhoto?> LoadAsync(string owner, DriverTransportCompletionPhotoKind kind, long id, CancellationToken cancellationToken = default)
            => Result.Task;
        public Task SaveAsync(string owner, DriverTransportCompletionPhoto photo, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RemoveAsync(string owner, DriverTransportCompletionPhotoKind kind, long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class DelayedPicker : Microsoft.Maui.Media.IMediaPicker
    {
        public bool IsCaptureSupported => true;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<Microsoft.Maui.Storage.FileResult?> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<Microsoft.Maui.Storage.FileResult?> CapturePhotoAsync(Microsoft.Maui.Media.MediaPickerOptions? options = null)
        { Started.TrySetResult(); return Result.Task; }
    }
}
