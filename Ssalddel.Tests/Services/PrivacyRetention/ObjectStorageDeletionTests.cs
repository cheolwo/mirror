using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Ssalddel.Services.Storage;

namespace Ssalddel.Tests.Services.PrivacyRetention;

public sealed class ObjectStorageDeletionTests
{
    [Fact]
    public async Task 서버소유접두사의정확한객체만삭제하고_재시도는멱등이다()
    {
        using var env = new Env(); var storage = new DevelopmentLocalStorageService(env, new HttpContextAccessor());
        await using var bytes = new MemoryStream([1, 2, 3]);
        var obj = await storage.UploadImmutableAsync(bytes, "cases/owned/evidence.png", "image/png", ObjectStorageAccess.Private);
        var first = await storage.DeleteAsync(obj.ContainerName, obj.ObjectName, "cases/owned");
        var second = await storage.DeleteAsync(obj.ContainerName, obj.ObjectName, "cases/owned");
        Assert.True(first.ActiveObjectAbsent); Assert.True(first.HistoricalCopiesVerifiedAbsent); Assert.Equal(first, second);
        await Assert.ThrowsAsync<FileNotFoundException>(() => storage.DownloadAsync(obj.ContainerName, obj.ObjectName));
    }

    [Theory]
    [InlineData("cases/other/evidence.png", "cases/owned")]
    [InlineData("cases/owned/../other.png", "cases/owned")]
    [InlineData("/cases/owned/evidence.png", "cases/owned")]
    [InlineData("C:/cases/owned/evidence.png", "cases/owned")]
    [InlineData("cases/owned/evidence.png", "cases/../owned")]
    public void 임의경로나다른사건의객체를삭제대상으로허용하지않는다(string objectName, string prefix)
        => Assert.Throws<InvalidOperationException>(() => ObjectStorageObjectName.RequireOwnedDeletionName(objectName, prefix));

    private sealed class Env : IHostEnvironment, IDisposable
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "privacy-deletion-test", Guid.NewGuid().ToString("N"));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public void Dispose() { if (Directory.Exists(ContentRootPath)) Directory.Delete(ContentRootPath, true); }
    }
}
