using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

namespace Ssalddel.Tests.Ui.Common;

public sealed class DistributedPhotoWorkflowTests
{
    [Fact] public async Task 사진없는_완료와_텍스트근거만의_피킹은_전송하지않는다()
    {
        var t = DistributedUiTransport.Round(); using var m = Model(t); await m.LoadAsync("wave-a");
        await m.CompletePhotoAsync(true); await m.PickAsync("res-a", "barcode-a", "sku-a", 1, "사진처럼 보이는 URL", true);
        Assert.Empty(t.PhotoPosts); Assert.Empty(t.Posts); Assert.NotNull(m.ValidationError);
    }
    [Fact] public async Task 촬영취소는_완료나_빈사진전송으로_바꾸지않는다()
    {
        var t = DistributedUiTransport.Round(); using var m = Model(t, new() { Photo = null }); await m.LoadAsync("wave-a"); await Capture(m); await m.CompletePhotoAsync(true);
        Assert.False(m.Photo.HasPhotos); Assert.Empty(t.PhotoPosts); Assert.Empty(t.Posts); Assert.True(m.IsLoaded);
    }
    [Fact] public async Task 촬영권한오류는_현재업무를_지우지않고_안내한다()
    {
        var t = DistributedUiTransport.Round(); using var m = Model(t, new() { Error = new InvalidOperationException("카메라 권한이 필요합니다.") }); await m.LoadAsync("wave-a"); await Capture(m);
        Assert.True(m.IsLoaded); Assert.Contains("카메라 권한", m.ValidationError); Assert.False(m.Photo.HasPhotos); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 사진전송실패는_완료하지않고_원래업로드와_완료번호로_재확인한다()
    {
        var t = DistributedUiTransport.Round(); var calls = 0; t.BeforePhotoPost = _ => { if (++calls == 1) throw new HttpRequestException("lost upload response"); };
        using var m = Model(t); await m.LoadAsync("wave-a"); await Capture(m); var completionId = m.Photo.CompletionRequestId; await m.CompletePhotoAsync(true);
        Assert.True(m.HasPendingPhoto); Assert.Empty(t.Posts); Assert.Null(m.Message); await m.RetryPhotoAsync();
        Assert.Equal(2, t.PhotoPosts.Count); Assert.Single(t.PhotoPosts.Select(photo => photo.UploadRequestId).Distinct()); Assert.All(t.PhotoPosts, photo => Assert.Equal(completionId, photo.Work.Pick!.RequestId));
        var command = Assert.IsType<DistributedPickBatchRequest>(Assert.Single(t.Posts).Body); Assert.Equal(completionId, command.RequestId); Assert.Single(command.PhotoEvidenceIds); Assert.NotNull(m.Message);
    }
    [Fact] public async Task 사진저장뒤_완료실패는_사진을_재전송하지않고_같은명령을_사용한다()
    {
        var t = DistributedUiTransport.Round(); var calls = 0; t.BeforePost = _ => { if (++calls == 1) throw new HttpRequestException("lost command"); };
        using var m = Model(t); await m.LoadAsync("wave-a"); await Capture(m); await m.CompletePhotoAsync(true); var id = m.RequestId;
        Assert.True(m.Photo.FullyUploaded); Assert.True(m.HasPendingCommand); await m.RetryPendingAsync();
        Assert.Single(t.PhotoPosts); Assert.Equal(2, t.Posts.Count); Assert.All(t.Posts, post => Assert.Equal(id, post.Body.RequestId));
        Assert.Equal(Assert.IsType<DistributedPickBatchRequest>(t.Posts[0].Body).PhotoEvidenceIds, Assert.IsType<DistributedPickBatchRequest>(t.Posts[1].Body).PhotoEvidenceIds);
        Assert.NotNull(m.Message); Assert.False(m.Photo.HasPhotos);
    }
    [Fact] public async Task 완료가_저장되고_응답만유실되면_원영수증을먼저조회하고_재실행하지않는다()
    {
        var t = DistributedUiTransport.Round(); var lost = false; t.AfterPost = _ => { if (!lost) { lost = true; throw new HttpRequestException("response lost after commit"); } };
        using var m = Model(t); await m.LoadAsync("wave-a"); await Capture(m); await m.CompletePhotoAsync(true); Assert.True(m.HasPendingCommand); await m.RetryPendingAsync();
        Assert.Single(t.PhotoPosts); Assert.Single(t.Posts); Assert.Contains(t.Gets, path => path.Contains("/receipts/")); Assert.NotNull(m.Message); Assert.False(m.HasPendingCommand);
    }
    [Fact] public async Task 사진저장응답의_다른완료번호는_완료명령으로_사용하지않는다()
    {
        var t = DistributedUiTransport.Round(); t.AlterPhoto = photo => photo with { CompletionRequestId = Guid.NewGuid() }; using var m = Model(t); await m.LoadAsync("wave-a"); await Capture(m); await m.CompletePhotoAsync(true);
        Assert.True(m.HasPendingPhoto); Assert.Empty(t.Posts); Assert.Null(m.Message);
    }
    [Theory]
    [InlineData("Uploading", false)]
    [InlineData("Deleted", false)]
    [InlineData("Uploaded", true)]
    public async Task 미완성사진상태나_다른입력digest는_완료근거로_사용하지않는다(string status, bool wrongDigest)
    {
        var t = DistributedUiTransport.Round(); t.AlterPhoto = photo => photo with { StatusCode = status, InputDigest = wrongDigest ? new string('b', 64) : photo.InputDigest };
        using var m = Model(t); await m.LoadAsync("wave-a"); await Capture(m); await m.CompletePhotoAsync(true); Assert.Empty(t.Posts); Assert.Null(m.Message); Assert.True(m.HasPendingPhoto);
    }
    [Fact] public async Task 사진전송은_성공해도_사용자가완료를누르기전에는_피킹이_바뀌지않는다()
    {
        var t = DistributedUiTransport.Round(); using var m = Model(t); await m.LoadAsync("wave-a"); await Capture(m);
        await m.Photo.UploadAsync(new(t, "cargo-driver"), CancellationToken.None, () => { });
        Assert.Single(t.PhotoPosts); Assert.Empty(t.Posts); Assert.Equal(0, t.Workspace.Waves[0].Reservations[0].PickedQuantity); Assert.True(m.Photo.FullyUploaded);
    }
    [Fact] public async Task 촬영뒤_수량변경은_이전사진과_완료번호를_폐기한다()
    {
        var t = DistributedUiTransport.Round(); using var m = Model(t); await m.LoadAsync("wave-a"); await Capture(m); var old = m.Photo.CompletionRequestId;
        await m.CapturePhotoAsync(PickWork(1, 2), "피킹", "pick", "waves/wave-a/pick-batches", ["pick"], 2);
        Assert.Single(m.Photo.Photos); Assert.NotEqual(old, m.Photo.CompletionRequestId); Assert.Equal(2, Assert.Single(m.Photo.Work!.Pick!.Lines).Quantity); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 업로드응답대기중_수량이나_업무대상을_바꿀수없다()
    {
        var t = DistributedUiTransport.Round(); t.BeforePhotoPost = _ => throw new HttpRequestException("upload response unknown");
        using var m = Model(t); await m.LoadAsync("wave-a"); await Capture(m); var id = m.Photo.CompletionRequestId; await m.CompletePhotoAsync(true);
        await m.CapturePhotoAsync(PickWork(1, 2), "피킹", "pick", "waves/wave-a/pick-batches", ["pick"], 2); await m.ChooseAsync("another-wave"); await m.LoadAsync("another-wave"); m.RemovePhoto(0);
        Assert.Equal(id, m.Photo.CompletionRequestId); Assert.Equal(1, Assert.Single(m.Photo.Work!.Pick!.Lines).Quantity); Assert.Single(m.Photo.Photos); Assert.Equal("wave-a", m.SelectedId); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 계정변경은_사진과완료입력을_다른계정에_넘기지않는다()
    {
        var t = DistributedUiTransport.Round(); using var m = Model(t); await m.LoadAsync("wave-a"); await Capture(m); Assert.True(m.Photo.HasPhotos);
        t.SetIdentity(new("other", 2, true)); Assert.False(m.Photo.HasPhotos); Assert.Null(m.Photo.Work); await m.CompletePhotoAsync(true); Assert.Empty(t.PhotoPosts); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 다른위치나_다른소유자의_물품을_한사진으로_묶지않는다()
    {
        var t = DistributedUiTransport.Round(); using var m = Model(t); await m.LoadAsync("wave-a"); var work = PickWork();
        work.Pick!.Lines = [work.Pick.Lines[0], new() { ReservationId = "res-b", OwnerUserId = "owner-b", Sku = "sku-b", Quantity = 1 }];
        await m.CapturePhotoAsync(work, "피킹", "pick", "waves/wave-a/pick-batches", ["pick"], 2); Assert.False(m.Photo.HasPhotos); Assert.NotNull(m.ValidationError); Assert.Empty(t.PhotoPosts);
    }
    [Fact] public async Task 같은위치같은소유자의_여러SKU와_부분수량을_같은사진으로_묶는다()
    {
        var t = DistributedUiTransport.Round(); var wave = t.Workspace.Waves[0]; wave.Reservations = wave.Reservations.Select(line => line.ReservationId == "res-b" ? line with { OwnerUserId = "owner-a" } : line).ToArray();
        using var m = Model(t); await m.LoadAsync("wave-a"); var work = PickWork(); work.Pick!.Lines = [work.Pick.Lines[0], new() { ReservationId = "res-b", OwnerUserId = "owner-a", Sku = "sku-b", Quantity = 1 }];
        await m.CapturePhotoAsync(work, "피킹", "pick", "waves/wave-a/pick-batches", ["pick"], 2); await m.CompletePhotoAsync(true);
        var command = Assert.IsType<DistributedPickBatchRequest>(Assert.Single(t.Posts).Body); Assert.Equal(2, command.Lines.Count); Assert.All(command.Lines, line => Assert.Equal(1, line.Quantity)); Assert.Single(command.PhotoEvidenceIds); Assert.NotNull(m.Message);
    }
    [Fact] public async Task 웹의_미지원촬영은_가짜사진이나_완료를_만들지않는다()
    {
        var t = DistributedUiTransport.Round(); using var m = new 분산이행PageViewModel(new(t, "cargo-driver"), "round"); await m.LoadAsync("wave-a"); await Capture(m);
        Assert.False(m.Photo.CaptureSupported); Assert.False(m.Photo.HasPhotos); Assert.Contains("모바일 앱", m.ValidationError); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 사진은최대세장이고_재촬영취소는_지운사진을_완료에쓰지않는다()
    {
        var capture = new Test업무사진촬영Adapter(); var t = DistributedUiTransport.Round(); using var m = Model(t, capture); await m.LoadAsync("wave-a");
        await Capture(m); await Capture(m); await Capture(m); await Capture(m); Assert.Equal(3, m.Photo.Photos.Count);
        m.RemovePhoto(0); capture.Photo = null; await Capture(m); Assert.Equal(2, m.Photo.Photos.Count); Assert.Empty(t.Posts);
    }
    public static DistributedEvidenceWorkRequest PickWork(long revision = 1, int quantity = 1) => new() { Kind = "Pick", AggregateId = "wave-a", Pick = new() { ExpectedRevision = revision, StopId = "stop-a", LocationBarcode = "barcode-a", Lines = [new() { ReservationId = "res-a", OwnerUserId = "owner-a", Sku = "sku-a", Quantity = quantity }] } };
    private static 분산이행PageViewModel Model(DistributedUiTransport transport, Test업무사진촬영Adapter? capture = null) => new(new(transport, "cargo-driver"), "round", capture ?? new());
    private static Task Capture(분산이행PageViewModel model) => model.CapturePhotoAsync(PickWork(), "피킹", "pick", "waves/wave-a/pick-batches", ["pick"], 1);
}
internal sealed class Test업무사진촬영Adapter : I업무사진촬영Adapter
{
    public bool IsSupported => true;
    public 업무촬영사진? Photo { get; set; } = new("camera.jpg", "image/jpeg", [255, 216, 255, 224, 0, 1]);
    public Exception? Error { get; set; }
    public Task<업무촬영사진?> CaptureAsync(string title, CancellationToken ct)
        => Error is not null ? Task.FromException<업무촬영사진?>(Error) : Task.FromResult(Photo);
}
