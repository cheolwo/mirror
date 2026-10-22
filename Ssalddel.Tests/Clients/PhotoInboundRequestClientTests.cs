using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Clients;

public sealed class PhotoInboundRequestClientTests
{
    [Fact]
    public async Task 사진접수는_화주역할의정해진경로에_고정파일명과원요청번호를전송한다()
    {
        var api = new Api(); var client = new 가입고사진신청Client(api); var id = Guid.NewGuid();
        await client.CreateAsync(id, null, "image/jpeg", [255, 216, 255, 1], default);
        Assert.Equal(RoleWorkspaceCatalog.Shipper, api.Role); Assert.Equal(가입고사진신청Policy.ApiPath, api.Path);
        Assert.Contains("name=ClientRequestId", api.Body); Assert.Contains(id.ToString("D"), api.Body);
        Assert.Contains("inbound-photo.jpg", api.Body); Assert.DoesNotContain("private-original", api.Body);
    }
    [Fact]
    public async Task 결과조회404는_미확인으로돌려주며새접수를보내지않는다()
    {
        var api = new Api { ReadFailure = new RoleWorkspaceAccessException(404, "not found") };
        var client = new 가입고사진신청Client(api); var id = Guid.NewGuid();
        Assert.Null(await client.GetAsync(id, default)); Assert.Equal(0, api.UploadCount);
        Assert.Equal(가입고사진신청Policy.ApiPath + "/" + id.ToString("D"), api.Path);
    }
    [Fact]
    public async Task 권한실패는_없는신청으로숨기지않는다()
    {
        var api = new Api { ReadFailure = new RoleWorkspaceAccessException(403, "forbidden") };
        var client = new 가입고사진신청Client(api);
        await Assert.ThrowsAsync<RoleWorkspaceAccessException>(() => client.GetAsync(Guid.NewGuid(), default));
    }
    [Theory]
    [InlineData("image/gif")]
    [InlineData("text/plain")]
    [InlineData("image/png")]
    public async Task 유형과실제파일서명이맞지않으면_전송하지않는다(string type)
    {
        var api = new Api(); var client = new 가입고사진신청Client(api);
        await Assert.ThrowsAsync<ArgumentException>(() => client.CreateAsync(Guid.NewGuid(), null, type, [255, 216, 255, 1], default));
        Assert.Equal(0, api.UploadCount);
    }
    [Fact]
    public async Task 빈번호와허용크기초과사진은_전송하지않는다()
    {
        var api = new Api(); var client = new 가입고사진신청Client(api);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.CreateAsync(Guid.Empty, null, "image/jpeg", [255, 216, 255], default));
        var bytes = new byte[가입고사진신청Policy.MaximumPhotoBytes + 1]; bytes[0] = 255; bytes[1] = 216; bytes[2] = 255;
        await Assert.ThrowsAsync<ArgumentException>(() => client.CreateAsync(Guid.NewGuid(), null, "image/jpeg", bytes, default));
        Assert.Equal(0, api.UploadCount);
    }
    private sealed class Api : IRoleWorkspaceApi
    {
        public string? Role, Path, Body; public int UploadCount; public Exception? ReadFailure;
        public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken)
        { Role = roleKey; Path = path; if (ReadFailure is not null) throw ReadFailure; return Task.FromResult(default(T)!); }
        public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken) => throw new InvalidOperationException("사진 신청은 일반 JSON POST를 쓰지 않습니다.");
        public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken) => Task.FromResult(default(T));
        public async Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken cancellationToken)
        { Role = roleKey; Path = path; UploadCount++; Body = await body.ReadAsStringAsync(cancellationToken); return default; }
    }
}
