using Ssalddel.Contracts.Admin.Progress;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CargoOperatorContextR20Tests
{
    [Fact]
    public async Task 선택운송의사건만표시하고다른운송직접검토를막는다()
    {
        var api = new Api();
        using var model = new CargoIncidentReviewViewModel(api, new Access());
        await model.LoadAsync(transportId: 7);
        Assert.True(model.Loaded);
        Assert.Equal(7, model.TransportId);
        Assert.Equal("incident-a", Assert.Single(model.Items).사건StableId);

        await model.LoadAsync("incident-b", 7);
        Assert.False(model.Loaded);
        Assert.True(model.Lifetime.HasError);
        Assert.Null(model.Source);
        model.Confirmed = true;
        await model.SubmitAsync();
        Assert.Equal(0, api.Mutations);
    }

    [Fact]
    public async Task 무선택전체조회와선택변경에서이전입력을구분한다()
    {
        using var model = new CargoIncidentReviewViewModel(new Api(), new Access());
        await model.LoadAsync();
        Assert.Equal(2, model.Items.Count);
        await model.LoadAsync("incident-a", 7);
        model.Decision = "IncidentClosed";
        model.Reason = "검토 완료";
        model.Prepare();
        Assert.True(model.IsConfirming);
        await model.LoadAsync("incident-b", 8);
        Assert.True(model.Loaded);
        Assert.Equal(8, model.Source!.운송Id);
        Assert.False(model.IsConfirming);
        Assert.Empty(model.Reason);
        Assert.Empty(model.Decision);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task 잘못된운송문맥은조회도실행하지않는다(long id)
    {
        var api = new Api();
        using var model = new CargoIncidentReviewViewModel(api, new Access());
        await model.LoadAsync(transportId: id);
        Assert.True(model.Lifetime.HasError);
        Assert.Empty(model.Items);
        Assert.Equal(0, api.Reads);
    }

    [Fact]
    public async Task 조회된목록에선택운송이없으면다른사건을대신보이지않는다()
    {
        using var model = new CargoIncidentReviewViewModel(new Api(), new Access());
        await model.LoadAsync(transportId: 9);
        Assert.True(model.Loaded);
        Assert.Empty(model.Items);
        Assert.Null(model.Source);
    }

    [Fact]
    public async Task 이전운송의늦은응답이새선택과검토대상을덮지않는다()
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new Api { FirstReadRelease = gate };
        using var model = new CargoIncidentReviewViewModel(api, new Access());
        var old = model.LoadAsync("incident-a", 7);
        Assert.Equal(1, api.Reads);
        await model.LoadAsync("incident-b", 8);
        gate.SetResult(true);
        await old;
        Assert.True(model.Loaded);
        Assert.Equal(8, model.TransportId);
        Assert.Equal("incident-b", model.Source!.사건StableId);
        Assert.Equal("incident-b", Assert.Single(model.Items).사건StableId);
    }

    [Fact]
    public async Task 검토준비뒤계정이바뀌면이전운송의결정을전송하지않는다()
    {
        var api = new Api();
        var access = new Access();
        using var model = new CargoIncidentReviewViewModel(api, access);
        await model.LoadAsync("incident-a", 7);
        model.Decision = "IncidentClosed"; model.Reason = "검토 완료"; model.Prepare();
        model.Confirmed = true;
        access.Switch();
        await model.SubmitAsync();
        Assert.Null(model.Source);
        Assert.Null(model.TransportId);
        Assert.False(model.IsConfirming);
        Assert.Equal(0, api.Mutations);
    }

    private sealed class Api : IRoleWorkspaceApi
    {
        public int Reads { get; private set; }
        public int Mutations { get; private set; }
        public TaskCompletionSource<bool>? FirstReadRelease { get; init; }
        public async Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken)
        {
            Reads++;
            if (Reads == 1 && FirstReadRelease is not null) await FirstReadRelease.Task;
            Assert.Equal(RoleWorkspaceCatalog.Operator, roleKey);
            Assert.Equal(CargoIncidentReviewViewModel.Root, path);
            IReadOnlyList<비정상운송사건운영응답> items =
            [new() { 사건StableId = "incident-a", 운송Id = 7, 운송의뢰Id = "request-a", Revision = 1, 상태Code = "Reviewing" },
             new() { 사건StableId = "incident-b", 운송Id = 8, 운송의뢰Id = "request-b", Revision = 1, 상태Code = "Reviewing" }];
            return (T)items;
        }
        public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
        { Mutations++; throw new InvalidOperationException("No mutation expected"); }
        public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
            => PostAsync<T>(roleKey, path, body, cancellationToken);
        public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken cancellationToken)
            => throw new InvalidOperationException("No upload expected");
    }

    private sealed class Access : IRoleWorkspaceAccess
    {
        private RoleWorkspaceIdentity identity = new("admin", 1, true);
        public event Action? Changed;
        public void Switch() { identity = new("other-admin", identity.Revision + 1, true); Changed?.Invoke(); }
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => identity;
        public Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
