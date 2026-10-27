using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CommunityPersonalIdentityTests
{
    [Fact]
    public async Task 복원중에는_방문자로단정하거나_개인표시를노출하지않는다()
    {
        var source = new IdentitySource("owner", "사용자");
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var model = new CommunityPersonalIdentityViewModel(source, source, _ => pending.Task);
        var task = model.RestoreAsync();
        Assert.Equal(CommunityPersonalIdentityState.Restoring, model.Snapshot.State);
        Assert.Empty(model.Snapshot.DisplayName);
        pending.SetResult(); await task;
        Assert.Equal(new CommunityPersonalIdentitySnapshot(CommunityPersonalIdentityState.Authenticated, "사용자"), model.Snapshot);
    }

    [Fact]
    public async Task 익명과복원실패를구분하고_재시도후현재계정을표시한다()
    {
        var source = new IdentitySource(); var fail = true;
        using var model = new CommunityPersonalIdentityViewModel(source, source, _ => fail ? Task.FromException(new InvalidOperationException("private detail")) : Task.CompletedTask);
        await model.RestoreAsync();
        Assert.Equal(new CommunityPersonalIdentitySnapshot(CommunityPersonalIdentityState.Failed), model.Snapshot);
        fail = false; await model.RestoreAsync();
        Assert.Equal(CommunityPersonalIdentityState.Anonymous, model.Snapshot.State);
        source.Set("owner", "새 계정");
        Assert.Equal("새 계정", model.Snapshot.DisplayName);
        source.Set(null, null);
        Assert.Equal(new CommunityPersonalIdentitySnapshot(CommunityPersonalIdentityState.Anonymous), model.Snapshot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 계정이바뀌면_이전복원의늦은성공이나실패가_현재표시를덮지않는다(bool fails)
    {
        var source = new IdentitySource("old", "이전 이름");
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var model = new CommunityPersonalIdentityViewModel(source, source, _ => pending.Task);
        var task = model.RestoreAsync();
        source.Set("new", "현재 이름");
        if (fails) pending.SetException(new InvalidOperationException("private detail")); else pending.SetResult();
        await task;
        Assert.Equal(new CommunityPersonalIdentitySnapshot(CommunityPersonalIdentityState.Authenticated, "현재 이름"), model.Snapshot);
    }

    [Fact]
    public async Task 화면을닫으면_개인표시와구독을지우고_늦은응답을무시한다()
    {
        var source = new IdentitySource("owner", "이름");
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new CommunityPersonalIdentityViewModel(source, source, _ => pending.Task);
        var task = model.RestoreAsync();
        model.Dispose(); pending.SetResult(); await task;
        source.Set("other", "다른 이름");
        Assert.Equal(new CommunityPersonalIdentitySnapshot(CommunityPersonalIdentityState.Anonymous), model.Snapshot);
    }

    private sealed class IdentitySource : ISsalddel현재사용자Context, ISsalddel현재사용자변경알림
    {
        public IdentitySource(string? id = null, string? name = null) => 현재사용자 = new(id, name, []);
        public 현재사용자Snapshot 현재사용자 { get; private set; }
        public event Action? Changed;
        public void Set(string? id, string? name) { 현재사용자 = new(id, name, []); Changed?.Invoke(); }
    }
}
