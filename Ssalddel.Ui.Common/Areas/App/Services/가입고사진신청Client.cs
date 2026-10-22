using System.Net.Http.Headers;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.Services;

/// <summary>사진 준비 접수만 다루며 일반 입고·재고 완료 명령을 호출하지 않습니다.</summary>
public interface I가입고사진신청Client
{
    Task<가입고사진신청응답?> CreateAsync(Guid requestId, string? name, string contentType, byte[] bytes, CancellationToken ct);
    Task<가입고사진신청응답?> GetAsync(Guid id, CancellationToken ct);
    Task<가입고사진신청목록응답> ListAsync(int page, int pageSize, CancellationToken ct);
    Task<가입고사진내용응답> PhotoAsync(Guid id, CancellationToken ct);
    Task<가입고사진신청응답?> RenameAsync(Guid id, 가입고사진이름변경요청 request, CancellationToken ct);
}

public sealed class 가입고사진신청Client(IRoleWorkspaceApi api) : I가입고사진신청Client
{
    public async Task<가입고사진신청응답?> CreateAsync(Guid requestId, string? name, string contentType, byte[] bytes, CancellationToken ct)
    {
        ValidateId(requestId); ValidatePhoto(contentType, bytes);
        if (name?.Trim().Length > 가입고사진신청Policy.MaximumNameLength) throw new ArgumentException("이름은 100자 이내로 입력해 주세요.", nameof(name));
        using var body = new MultipartFormDataContent();
        using var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        body.Add(file, "file", contentType == "image/png" ? "inbound-photo.png" : "inbound-photo.jpg");
        body.Add(new StringContent(requestId.ToString("D")), "ClientRequestId");
        body.Add(new StringContent(name?.Trim() ?? string.Empty), "name");
        return await api.UploadAsync<가입고사진신청응답>(RoleWorkspaceCatalog.Shipper, 가입고사진신청Policy.ApiPath, body, ct);
    }
    public async Task<가입고사진신청응답?> GetAsync(Guid id, CancellationToken ct)
    {
        ValidateId(id);
        try { return await api.GetAsync<가입고사진신청응답>(RoleWorkspaceCatalog.Shipper, Path(id), ct); }
        catch (RoleWorkspaceAccessException ex) when (ex.StatusCode == 404) { return null; }
        catch (SsalddelApiException ex) when (ex.StatusCode == 404) { return null; }
    }
    public Task<가입고사진신청목록응답> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        if (page < 0 || pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(page));
        return api.GetAsync<가입고사진신청목록응답>(RoleWorkspaceCatalog.Shipper,
            $"{가입고사진신청Policy.ApiPath}?page={page}&pageSize={pageSize}", ct);
    }
    public Task<가입고사진내용응답> PhotoAsync(Guid id, CancellationToken ct)
    { ValidateId(id); return api.GetAsync<가입고사진내용응답>(RoleWorkspaceCatalog.Shipper, Path(id) + "/photo", ct); }
    public Task<가입고사진신청응답?> RenameAsync(Guid id, 가입고사진이름변경요청 request, CancellationToken ct)
    {
        ValidateId(id); ArgumentNullException.ThrowIfNull(request);
        if (request.Revision <= 0 || request.이름.Trim().Length > 가입고사진신청Policy.MaximumNameLength)
            throw new ArgumentException("이름과 현재 신청 상태를 확인해 주세요.", nameof(request));
        return api.PutAsync<가입고사진신청응답>(RoleWorkspaceCatalog.Shipper, Path(id) + "/name", request, ct);
    }
    private static string Path(Guid id) => 가입고사진신청Policy.ApiPath + "/" + id.ToString("D");
    private static void ValidateId(Guid id) { if (id == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(id)); }
    public static void ValidatePhoto(string contentType, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var signature = contentType switch
        {
            "image/jpeg" => bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255,
            "image/png" => bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            _ => false
        };
        if (!signature || bytes.LongLength > 가입고사진신청Policy.MaximumPhotoBytes)
            throw new ArgumentException("5MB 이하의 JPG·PNG 사진을 선택해 주세요.", nameof(bytes));
    }
}
