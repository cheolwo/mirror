using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class DurableRecoveryPresentationTests
{
    [Theory]
    [InlineData("cargo.pickup", "상차")]
    [InlineData("cargo.dropoff", "하차")]
    public void 보호보관실패와_완료후정리실패를_구분한다(string stage, string label)
    {
        var unavailable = Notice("CargoCompletionRecoveryUnavailable", stage, 업무처리결과Codes.미확정);
        Assert.Equal($"{label} 요청 확인 필요", unavailable.제목);
        Assert.False(unavailable.복귀허용);
        Assert.True(unavailable.재조회허용);
        var cleanup = Notice("CargoCompletionRecoveryCleanupPending", stage, 업무처리결과Codes.적용);
        Assert.Equal($"{label} 완료 · 확인 기록 정리 필요", cleanup.제목);
        Assert.Contains("다시 완료하지 말고", cleanup.설명);
        Assert.False(cleanup.복귀허용);
        Assert.DoesNotContain("PRIVATE", cleanup.설명);
        var preparation = Notice("CargoCompletionPreparationIncomplete", stage, 업무처리결과Codes.미적용);
        Assert.Equal($"{label} 준비 확인 필요", preparation.제목);
        Assert.Contains("완료 요청을 보내지 않았습니다", preparation.설명);
        Assert.False(preparation.재조회허용);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("null")]
    [InlineData("{broken")]
    public async Task 손상된보호기록을_기록없음으로읽지않는다(string json)
    {
        var store = new ProtectedWorkflowRecoveryStore(_ => new("owner", 1, true),
            _ => Task.FromResult<string?>(json), (_, _) => Task.CompletedTask, _ => { });
        await Assert.ThrowsAnyAsync<Exception>(() => store.LoadAsync("owner", "cargo.v1", default));
    }

    [Fact]
    public async Task 삭제실패하면_다른저장소인스턴스에서도_미확정기록이남는다()
    {
        var storage = new Dictionary<string, string>();
        RoleWorkspaceIdentity Identity(string _) => new("owner", 1, true);
        Task<string?> Read(string key) => Task.FromResult(storage.GetValueOrDefault(key));
        Task Write(string key, string value) { storage[key] = value; return Task.CompletedTask; }
        var record = new WorkflowRecoveryRecord("owner", "cargo.v1", Guid.NewGuid(), null, DateTime.UtcNow, "31");
        var first = new ProtectedWorkflowRecoveryStore(Identity, Read, Write, _ => throw new IOException("storage"));
        await first.SaveAsync(record, default);
        await Assert.ThrowsAsync<IOException>(() => first.RemoveAsync("owner", "cargo.v1", record.RequestId, default));
        var reopened = new ProtectedWorkflowRecoveryStore(Identity, Read, Write, key => storage.Remove(key));
        Assert.Equal(record, await reopened.LoadAsync("owner", "cargo.v1", default));
    }

    private static 업무오류안내Presentation Notice(string code, string stage, string outcome)
        => 업무오류안내Presentation.생성(new Api작업오류(code, "PRIVATE_RAW", WorkflowContext:
            new(OperatingSystemIds.DomesticCargoTransport, stage, outcome)));
}
