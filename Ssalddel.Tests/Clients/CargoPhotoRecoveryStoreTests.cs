using System.Security.Cryptography;
using System.Text;
using DriverApp.Services;

namespace Ssalddel.Tests.Clients;

/// <summary>전용 암호화 파일 캐시 검사. Android 보호 저장소·카메라·서버 업로드 검증이 아닙니다.</summary>
public sealed class CargoPhotoRecoveryStoreTests
{
    [Fact]
    public async Task 재시작후_같은계정운송단계의사진과업로드근거를복원한다()
    {
        using var f = new Fixture();
        var photo = Photo() with { ExistingObjectName = "private-upload/object", ExistingUrl = "private-url" };
        await f.Store.SaveAsync("driver-one", photo);
        var restarted = new DriverCargoPhotoRecoveryStore(f.Root, () => Task.FromResult(f.Key), f.Clock);
        var restored = await restarted.LoadAsync("driver-one", photo.Kind, photo.TransportId);
        Assert.NotNull(restored);
        Assert.Equal(photo.Bytes, restored.Bytes);
        Assert.Equal(photo.ExistingObjectName, restored.ExistingObjectName);
        Assert.Equal(photo.ReceiptEvidence, restored.ReceiptEvidence);
        Assert.Equal(photo.ExistingUrl, restored.ExistingUrl);
        Assert.Single(Directory.GetFiles(f.Root));
    }

    [Fact]
    public async Task 사진서명계정과업로드주소가_파일명이나디스크평문에남지않는다()
    {
        using var f = new Fixture();
        var photo = Photo() with { ExistingObjectName = "synthetic-private-object", ExistingUrl = "synthetic-private-url" };
        var originalKey = f.Key.ToArray();
        await f.Store.SaveAsync("synthetic-private-driver", photo);
        var path = Assert.Single(Directory.GetFiles(f.Root));
        Assert.DoesNotContain("driver", Path.GetFileName(path));
        Assert.DoesNotContain(photo.FileName, Path.GetFileName(path));
        var raw = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(path));
        Assert.DoesNotContain("synthetic-private-driver", raw);
        Assert.DoesNotContain("synthetic-photo-content", raw);
        Assert.DoesNotContain("synthetic-recipient-signature", raw);
        Assert.DoesNotContain("synthetic-private-object", raw);
        Assert.DoesNotContain("synthetic-private-url", raw);
        Assert.Equal(originalKey, f.Key);
    }

    [Theory]
    [InlineData("driver-two", DriverTransportCompletionPhotoKind.Pickup, 11)]
    [InlineData("driver-one", DriverTransportCompletionPhotoKind.Dropoff, 11)]
    [InlineData("driver-one", DriverTransportCompletionPhotoKind.Pickup, 12)]
    public async Task 다른계정운송단계는_기존사진을조회하거나삭제하지못한다(string owner, DriverTransportCompletionPhotoKind kind, long id)
    {
        using var f = new Fixture();
        await f.Store.SaveAsync("driver-one", Photo());
        Assert.Null(await f.Store.LoadAsync(owner, kind, id));
        await f.Store.RemoveAsync(owner, kind, id);
        Assert.NotNull(await f.Store.LoadAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11));
    }

    [Theory]
    [InlineData("driver-two", DriverTransportCompletionPhotoKind.Pickup, 11)]
    [InlineData("driver-one", DriverTransportCompletionPhotoKind.Dropoff, 11)]
    [InlineData("driver-one", DriverTransportCompletionPhotoKind.Pickup, 12)]
    public async Task 다른대상의파일명으로암호문을옮겨도_인증결속을통과하지못한다(string owner, DriverTransportCompletionPhotoKind kind, long id)
    {
        using var f = new Fixture();
        await f.Store.SaveAsync("driver-one", Photo());
        var source = Assert.Single(Directory.GetFiles(f.Root));
        await f.Store.SaveAsync(owner, Photo() with { Kind = kind, TransportId = id });
        var target = Assert.Single(Directory.GetFiles(f.Root).Where(path => path != source));
        File.Copy(source, target, overwrite: true);
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Store.LoadAsync(owner, kind, id));
        Assert.NotNull(await f.Store.LoadAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11));
    }

    [Fact]
    public async Task 변조된암호문은_완료로복원하지않고자동덮어쓰지않는다()
    {
        using var f = new Fixture();
        await f.Store.SaveAsync("driver-one", Photo());
        var path = Assert.Single(Directory.GetFiles(f.Root));
        var corrupted = await File.ReadAllBytesAsync(path);
        corrupted[^1] ^= 1;
        await File.WriteAllBytesAsync(path, corrupted);
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Store.LoadAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Store.SaveAsync("driver-one", Photo()));
        Assert.Equal(corrupted, await File.ReadAllBytesAsync(path));
        await f.Store.RemoveAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11);
        await f.Store.SaveAsync("driver-one", Photo());
        Assert.NotNull(await f.Store.LoadAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11));
    }

    [Fact]
    public async Task 다른키로열기실패가_새키로덮어쓰는결과가되지않는다()
    {
        using var f = new Fixture();
        await f.Store.SaveAsync("driver-one", Photo());
        var path = Assert.Single(Directory.GetFiles(f.Root));
        var before = await File.ReadAllBytesAsync(path);
        var otherKey = RandomNumberGenerator.GetBytes(32);
        var changedKeyStore = new DriverCargoPhotoRecoveryStore(f.Root, () => Task.FromResult(otherKey), f.Clock);
        await Assert.ThrowsAsync<InvalidDataException>(() => changedKeyStore.LoadAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11));
        await Assert.ThrowsAsync<InvalidOperationException>(() => changedKeyStore.SaveAsync("driver-one", Photo()));
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        Assert.NotNull(await f.Store.LoadAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11));
    }

    [Fact]
    public async Task 키보호저장소실패는_예외를유지하고기존사진을변경하지않는다()
    {
        using var f = new Fixture();
        await f.Store.SaveAsync("driver-one", Photo());
        var path = Assert.Single(Directory.GetFiles(f.Root));
        var before = await File.ReadAllBytesAsync(path);
        var unavailable = new DriverCargoPhotoRecoveryStore(f.Root,
            () => Task.FromException<byte[]>(new InvalidOperationException("보호 저장소 접근 실패")), f.Clock);
        await Assert.ThrowsAsync<InvalidOperationException>(() => unavailable.LoadAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11));
        await Assert.ThrowsAsync<InvalidOperationException>(() => unavailable.SaveAsync("driver-one", Photo()));
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        Assert.Single(Directory.GetFiles(f.Root));
    }

    [Fact]
    public async Task 크기가잘못된키는_새파일을만들지않는다()
    {
        using var f = new Fixture();
        var invalid = new DriverCargoPhotoRecoveryStore(f.Root, () => Task.FromResult(new byte[16]), f.Clock);
        await Assert.ThrowsAsync<InvalidOperationException>(() => invalid.SaveAsync("driver-one", Photo()));
        Assert.False(Directory.Exists(f.Root));
    }

    [Fact]
    public async Task 삼일이된사진은_복원하지않고해당대상의암호문만제거한다()
    {
        using var f = new Fixture();
        await f.Store.SaveAsync("driver-one", Photo());
        f.Clock.Advance(TimeSpan.FromDays(1));
        await f.Store.SaveAsync("driver-two", Photo());
        f.Clock.Advance(TimeSpan.FromDays(2));
        Assert.Null(await f.Store.LoadAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11));
        Assert.NotNull(await f.Store.LoadAsync("driver-two", DriverTransportCompletionPhotoKind.Pickup, 11));
        Assert.Single(Directory.GetFiles(f.Root));
    }

    [Fact]
    public async Task 같은사진의업로드근거를추가해도_원래보관기한을늘리지않는다()
    {
        using var f = new Fixture();
        var photo = Photo();
        await f.Store.SaveAsync("driver-one", photo);
        f.Clock.Advance(TimeSpan.FromDays(2));
        await f.Store.SaveAsync("driver-one", photo with { ExistingObjectName = "uploaded-later" });
        Assert.Equal("uploaded-later", (await f.Store.LoadAsync("driver-one", photo.Kind, photo.TransportId))!.ExistingObjectName);
        f.Clock.Advance(TimeSpan.FromDays(1));
        Assert.Null(await f.Store.LoadAsync("driver-one", photo.Kind, photo.TransportId));
        Assert.Empty(Directory.GetFiles(f.Root));
    }

    [Fact]
    public async Task 다른운송을다시열지않아도_만료파일을청소하고변조자료는보존한다()
    {
        using var f = new Fixture();
        await f.Store.SaveAsync("expired-driver", Photo());
        var expiredPath = Assert.Single(Directory.GetFiles(f.Root));
        await f.Store.SaveAsync("damaged-driver", Photo());
        var damagedPath = Assert.Single(Directory.GetFiles(f.Root).Where(path => path != expiredPath));
        var damaged = await File.ReadAllBytesAsync(damagedPath); damaged[^1] ^= 1;
        await File.WriteAllBytesAsync(damagedPath, damaged);
        f.Clock.Advance(TimeSpan.FromDays(4));
        await f.Store.SaveAsync("current-driver", Photo());
        Assert.False(File.Exists(expiredPath));
        Assert.Equal(damaged, await File.ReadAllBytesAsync(damagedPath));
        Assert.NotNull(await f.Store.LoadAsync("current-driver", DriverTransportCompletionPhotoKind.Pickup, 11));
    }

    [Fact]
    public async Task 정리한도를넘는과거운송도_여러번의활성조회에서순환정리한다()
    {
        using var f = new Fixture();
        for (var id = 1; id <= 40; id++)
            await f.Store.SaveAsync("driver-one", Photo() with { TransportId = id });
        f.Clock.Advance(TimeSpan.FromDays(4));
        await f.Store.SaveAsync("driver-one", Photo() with { TransportId = 99 });
        for (var turn = 0; turn < 4; turn++)
            Assert.NotNull(await f.Store.LoadAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 99));
        Assert.Single(Directory.GetFiles(f.Root));
    }

    [Fact]
    public async Task 새로촬영한사진은_새보관기한으로교체하고임시파일을남기지않는다()
    {
        using var f = new Fixture();
        await f.Store.SaveAsync("driver-one", Photo());
        f.Clock.Advance(TimeSpan.FromDays(2));
        var newPhoto = Photo() with { Bytes = Encoding.UTF8.GetBytes("synthetic-new-photo-content") };
        await f.Store.SaveAsync("driver-one", newPhoto);
        f.Clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(newPhoto.Bytes, (await f.Store.LoadAsync("driver-one", newPhoto.Kind, newPhoto.TransportId))!.Bytes);
        Assert.Single(Directory.GetFiles(f.Root));
        Assert.Empty(Directory.GetFiles(f.Root, "*.tmp"));
    }

    [Fact]
    public async Task 용량제한초과는_기존사진을보존한채거절한다()
    {
        using var f = new Fixture();
        await f.Store.SaveAsync("driver-one", Photo());
        await Assert.ThrowsAsync<ArgumentException>(() => f.Store.SaveAsync("driver-one", Photo() with { Bytes = new byte[DriverCargoPhotoRecoveryStore.MaxPhotoBytes + 1] }));
        Assert.Equal(Photo().Bytes, (await f.Store.LoadAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11))!.Bytes);
    }

    [Fact]
    public async Task 저장취소는_기존파일과호출자의키를보존한다()
    {
        using var f = new Fixture();
        await f.Store.SaveAsync("driver-one", Photo());
        var path = Assert.Single(Directory.GetFiles(f.Root));
        var before = await File.ReadAllBytesAsync(path);
        using var cancellation = new CancellationTokenSource();
        var key = f.Key.ToArray();
        var cancelDuringKey = new DriverCargoPhotoRecoveryStore(f.Root, () =>
        { cancellation.Cancel(); return Task.FromResult(key); }, f.Clock);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelDuringKey.SaveAsync("driver-one", Photo(), cancellation.Token));
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        Assert.Equal(f.Key, key);
        Assert.Empty(Directory.GetFiles(f.Root, "*.tmp"));
    }

    [Fact]
    public async Task 동시저장은_단일완전한암호문만남긴다()
    {
        using var f = new Fixture();
        await Task.WhenAll(f.Store.SaveAsync("driver-one", Photo() with { ExistingObjectName = "upload-one" }),
            f.Store.SaveAsync("driver-one", Photo() with { ExistingObjectName = "upload-two" }));
        var restored = await f.Store.LoadAsync("driver-one", DriverTransportCompletionPhotoKind.Pickup, 11);
        Assert.Contains(restored!.ExistingObjectName, new[] { "upload-one", "upload-two" });
        Assert.Single(Directory.GetFiles(f.Root));
        Assert.Empty(Directory.GetFiles(f.Root, "*.tmp"));
    }

    [Theory]
    [InlineData("", DriverTransportCompletionPhotoKind.Pickup, 11)]
    [InlineData("driver-one", (DriverTransportCompletionPhotoKind)42, 11)]
    [InlineData("driver-one", DriverTransportCompletionPhotoKind.Pickup, 0)]
    public async Task 불명확한계정운송단계는_새캐시를만들지않는다(string owner, DriverTransportCompletionPhotoKind kind, long id)
    {
        using var f = new Fixture();
        await Assert.ThrowsAsync<ArgumentException>(() => f.Store.SaveAsync(owner, Photo() with { Kind = kind, TransportId = id }));
        Assert.False(Directory.Exists(f.Root));
    }

    private static DriverTransportCompletionPhoto Photo() => new(DriverTransportCompletionPhotoKind.Pickup, 11,
        "private-photo.png", "image/png", Encoding.UTF8.GetBytes("synthetic-photo-content"),
        new("인수증", true, false, null, "synthetic-recipient", "synthetic-organization", "synthetic-recipient-signature", "synthetic-driver-signature"));

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 14, 1, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan interval) => _now += interval;
    }
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "mirror-cargo-photo-tests", Guid.NewGuid().ToString("N")));
        public byte[] Key { get; } = RandomNumberGenerator.GetBytes(32);
        public Clock Clock { get; } = new();
        public DriverCargoPhotoRecoveryStore Store { get; }
        public Fixture() => Store = new(Root, () => Task.FromResult(Key), Clock);
        public void Dispose()
        {
            var boundary = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "mirror-cargo-photo-tests")) + Path.DirectorySeparatorChar;
            if (Directory.Exists(Root) && Root.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)) Directory.Delete(Root, recursive: true);
            CryptographicOperations.ZeroMemory(Key);
        }
    }
}
