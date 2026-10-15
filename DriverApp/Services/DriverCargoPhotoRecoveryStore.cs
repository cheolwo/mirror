using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DriverApp.Services;

/// <summary>사진·서명을 전용 암호화 캐시에 보관합니다. 일반 업무 복구 저장소와 공유하지 않습니다.</summary>
public interface IDriverCargoPhotoRecoveryStore
{
    Task SaveAsync(string owner, DriverTransportCompletionPhoto photo, CancellationToken cancellationToken = default);
    Task<DriverTransportCompletionPhoto?> LoadAsync(string owner, DriverTransportCompletionPhotoKind kind, long id,
        CancellationToken cancellationToken = default);
    Task RemoveAsync(string owner, DriverTransportCompletionPhotoKind kind, long id, CancellationToken cancellationToken = default);
}

/// <summary>
/// 키 보관은 호스트의 보호 저장소가 담당합니다. 이 core는 키를 생성하거나 재설정하지 않고,
/// 계정·운송·상하차 단계에 결속한 AES-GCM 파일만 읽고 씁니다.
/// </summary>
public sealed class DriverCargoPhotoRecoveryStore : IDriverCargoPhotoRecoveryStore
{
    public const int MaxPhotoBytes = 10 * 1024 * 1024;
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(3);
    private const string Schema = "cargo-photo-recovery.v1";
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int MaxPlaintextBytes = 15 * 1024 * 1024;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("MCR1");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _root;
    private readonly Func<Task<byte[]>> _keyFactory;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _cleanupOffset;

    public DriverCargoPhotoRecoveryStore(string privateRoot, Func<Task<byte[]>> keyFactory, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateRoot);
        _root = Path.GetFullPath(privateRoot);
        _keyFactory = keyFactory ?? throw new ArgumentNullException(nameof(keyFactory));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task SaveAsync(string owner, DriverTransportCompletionPhoto photo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(photo);
        var associatedData = Scope(owner, photo.Kind, photo.TransportId);
        if (!ValidPhoto(photo, photo.Kind, photo.TransportId))
            throw new ArgumentException("복구할 사진의 형식과 크기를 확인해 주세요.", nameof(photo));

        await _gate.WaitAsync(cancellationToken);
        byte[]? key = null;
        byte[]? plaintext = null;
        PhotoEnvelope? existing = null;
        string? temporary = null;
        try
        {
            // Acquire/validate before changing an existing file. Key failures never create a new key.
            key = await KeyAsync(cancellationToken);
            var path = CachePath(associatedData);
            var now = _timeProvider.GetUtcNow();
            var savedAt = now;
            if (File.Exists(path))
            {
                try
                {
                    existing = await ReadAsync(path, key, associatedData, cancellationToken);
                    if (!ValidEnvelope(existing, owner, photo.Kind, photo.TransportId))
                        throw new InvalidDataException();
                }
                catch (Exception ex) when (ex is CryptographicException or JsonException or InvalidDataException or EndOfStreamException)
                {
                    // A wrong/replaced key cannot silently overwrite the only recoverable ciphertext.
                    throw new InvalidOperationException("기존 사진 복구 자료를 확인하지 못했습니다. 명시적으로 폐기한 뒤 다시 촬영해 주세요.");
                }
                if (CryptographicOperations.FixedTimeEquals(SHA256.HashData(existing!.Photo.Bytes), SHA256.HashData(photo.Bytes)))
                    savedAt = existing.SavedAtUtc;
            }
            var envelope = new PhotoEnvelope(Schema, owner, photo.Kind, photo.TransportId, savedAt, savedAt + Lifetime, photo);
            plaintext = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
            if (plaintext.Length > MaxPlaintextBytes)
                throw new ArgumentException("사진 복구 자료의 크기가 너무 큽니다.", nameof(photo));
            cancellationToken.ThrowIfCancellationRequested();
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var tag = new byte[TagSize];
            var ciphertext = new byte[plaintext.Length];
            using (var cipher = new AesGcm(key, TagSize)) cipher.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
            Directory.CreateDirectory(_root);
            temporary = Path.Combine(_root, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await output.WriteAsync(Magic, cancellationToken);
                await output.WriteAsync(nonce, cancellationToken);
                await output.WriteAsync(tag, cancellationToken);
                await output.WriteAsync(ciphertext, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
            temporary = null;
            await CleanupExpiredAsync(key, cancellationToken);
        }
        finally
        {
            if (plaintext is not null) CryptographicOperations.ZeroMemory(plaintext);
            if (key is not null) CryptographicOperations.ZeroMemory(key);
            if (existing?.Photo.Bytes is { } bytes) CryptographicOperations.ZeroMemory(bytes);
            try { if (temporary is not null) File.Delete(temporary); }
            finally { _gate.Release(); }
        }
    }

    public async Task<DriverTransportCompletionPhoto?> LoadAsync(string owner, DriverTransportCompletionPhotoKind kind, long id,
        CancellationToken cancellationToken = default)
    {
        var associatedData = Scope(owner, kind, id);
        await _gate.WaitAsync(cancellationToken);
        byte[]? key = null;
        PhotoEnvelope? envelope = null;
        var accepted = false;
        try
        {
            var path = CachePath(associatedData);
            if (!Directory.Exists(_root)) return null;
            // Key lookup exceptions propagate: callers must not replace a missing/broken key.
            key = await KeyAsync(cancellationToken);
            await CleanupExpiredAsync(key, cancellationToken);
            if (!File.Exists(path)) return null;
            try { envelope = await ReadAsync(path, key, associatedData, cancellationToken); }
            catch (FileNotFoundException) { return null; }
            catch (Exception ex) when (ex is CryptographicException or JsonException or InvalidDataException or EndOfStreamException)
            { throw new InvalidDataException("사진 복구 자료를 확인하지 못했습니다. 해당 사진을 폐기한 뒤 다시 촬영해 주세요."); }
            if (!ValidEnvelope(envelope, owner, kind, id))
                throw new InvalidDataException("사진 복구 자료의 연결을 확인하지 못했습니다.");
            var now = _timeProvider.GetUtcNow();
            if (envelope!.SavedAtUtc > now) return null;
            if (envelope.ExpiresAtUtc <= now)
            {
                File.Delete(path);
                return null;
            }
            accepted = true;
            return envelope.Photo;
        }
        finally
        {
            if (key is not null) CryptographicOperations.ZeroMemory(key);
            if (!accepted && envelope?.Photo.Bytes is { } bytes) CryptographicOperations.ZeroMemory(bytes);
            _gate.Release();
        }
    }

    public async Task RemoveAsync(string owner, DriverTransportCompletionPhotoKind kind, long id,
        CancellationToken cancellationToken = default)
    {
        var associatedData = Scope(owner, kind, id);
        await _gate.WaitAsync(cancellationToken);
        try { cancellationToken.ThrowIfCancellationRequested(); File.Delete(CachePath(associatedData)); }
        finally { _gate.Release(); }
    }

    private async Task<byte[]> KeyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = await _keyFactory();
        cancellationToken.ThrowIfCancellationRequested();
        if (key is null || key.Length != 32)
            throw new InvalidOperationException("사진 복구 키를 확인하지 못했습니다.");
        return key.ToArray();
    }

    private string CachePath(byte[] associatedData)
        => Path.Combine(_root, Convert.ToHexString(associatedData).ToLowerInvariant() + ".cache");

    private static byte[] Scope(string owner, DriverTransportCompletionPhotoKind kind, long id)
    {
        if (string.IsNullOrWhiteSpace(owner) || owner.Length > 1024 || owner.Any(char.IsControl) || id <= 0 || !Enum.IsDefined(kind))
            throw new ArgumentException("사진 복구의 계정과 운송·단계를 확인해 주세요.");
        return SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new object[] { Schema, owner, kind.ToString(), id }, JsonOptions));
    }

    // At most 32 ciphertexts are authenticated per active read/write. The cursor rotates
    // across calls; valid expired records from other completed transports are not orphaned forever.
    private async Task CleanupExpiredAsync(byte[] key, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_root)) return;
        var paths = Directory.EnumerateFiles(_root, "*.cache", SearchOption.TopDirectoryOnly).Skip(_cleanupOffset).Take(32).ToArray();
        _cleanupOffset = paths.Length < 32 ? 0 : _cleanupOffset + paths.Length;
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PhotoEnvelope? record = null;
            try
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (name.Length != 64) continue;
                byte[] scope;
                try { scope = Convert.FromHexString(name); }
                catch (FormatException) { continue; }
                record = await ReadAsync(path, key, scope, cancellationToken);
                if (record is null || !ValidEnvelope(record, record.Owner, record.Kind, record.TransportId)
                    || !CryptographicOperations.FixedTimeEquals(scope, Scope(record.Owner, record.Kind, record.TransportId))) continue;
                if (record.ExpiresAtUtc <= _timeProvider.GetUtcNow()) File.Delete(path);
            }
            catch (Exception ex) when (ex is CryptographicException or JsonException or InvalidDataException
                or EndOfStreamException or FileNotFoundException or ArgumentException)
            { /* Unknown, damaged, or replaced-key ciphertext is preserved for explicit discard. */ }
            finally { if (record?.Photo.Bytes is { } bytes) CryptographicOperations.ZeroMemory(bytes); }
        }
    }

    private static bool ValidPhoto(DriverTransportCompletionPhoto? photo, DriverTransportCompletionPhotoKind kind, long id)
        => photo is not null && photo.Kind == kind && photo.TransportId == id && photo.Bytes is { Length: > 0 and <= MaxPhotoBytes }
            && !string.IsNullOrWhiteSpace(photo.FileName) && photo.FileName.Length <= 255
            && !string.IsNullOrWhiteSpace(photo.ContentType) && photo.ContentType.Length <= 128
            && photo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    private static bool ValidEnvelope(PhotoEnvelope? envelope, string owner, DriverTransportCompletionPhotoKind kind, long id)
        => envelope is not null && envelope.Schema == Schema && envelope.Owner == owner && envelope.Kind == kind && envelope.TransportId == id
            && envelope.ExpiresAtUtc - envelope.SavedAtUtc == Lifetime && ValidPhoto(envelope.Photo, kind, id);

    private static async Task<PhotoEnvelope?> ReadAsync(string path, byte[] key, byte[] associatedData, CancellationToken cancellationToken)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        var length = input.Length;
        var prefixLength = Magic.Length + NonceSize + TagSize;
        if (length <= prefixLength || length > prefixLength + MaxPlaintextBytes) throw new InvalidDataException();
        var buffer = new byte[(int)length];
        await input.ReadExactlyAsync(buffer, cancellationToken);
        if (input.ReadByte() != -1 || !buffer.AsSpan(0, Magic.Length).SequenceEqual(Magic)) throw new InvalidDataException();
        var plaintext = new byte[buffer.Length - prefixLength];
        try
        {
            using (var cipher = new AesGcm(key, TagSize)) cipher.Decrypt(
                buffer.AsSpan(Magic.Length, NonceSize), buffer.AsSpan(prefixLength),
                buffer.AsSpan(Magic.Length + NonceSize, TagSize), plaintext, associatedData);
            return JsonSerializer.Deserialize<PhotoEnvelope>(plaintext, JsonOptions);
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    private sealed record PhotoEnvelope(string Schema, string Owner, DriverTransportCompletionPhotoKind Kind, long TransportId,
        DateTimeOffset SavedAtUtc, DateTimeOffset ExpiresAtUtc, DriverTransportCompletionPhoto Photo);
}
