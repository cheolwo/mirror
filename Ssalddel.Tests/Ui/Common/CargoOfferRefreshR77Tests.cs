using Ssalddel.Contracts.Driver.Action;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CargoOfferRefreshR77Tests
{
    [Fact]
    public async Task 갱신된라운드에는과거확인을쓰지않고_다시확인후한번만수락한다()
    {
        var api = new Api();
        using var vm = new CargoOfferActionViewModel(new(api), new Access());
        await vm.LoadAsync("r77");
        vm.ConditionsConfirmed = vm.AcceptConfirmed = true;
        Assert.True(vm.CanSubmit);
        api.Round = 2;
        await vm.SubmitAsync();
        Assert.Equal(0, api.Writes); Assert.True(vm.RecommendationChanged);
        Assert.False(vm.ConditionsConfirmed); Assert.False(vm.AcceptConfirmed);
        Assert.False(vm.Lifetime.HasError); Assert.False(vm.Saved);
        vm.ConditionsConfirmed = vm.AcceptConfirmed = true;
        await vm.SubmitAsync();
        Assert.True(vm.Saved); Assert.Equal(1, api.Writes);
        Assert.Equal(2, api.LastBody!.ExpectedRecommendationRound);
        await vm.SubmitAsync(); Assert.Equal(1, api.Writes);
    }

    private sealed class Api : IRoleWorkspaceApi
    {
        public int Round { get; set; } = 1;
        public int Writes { get; private set; }
        public 기사화물배차수락요청? LastBody { get; private set; }
        private readonly DateTime start = DateTime.UtcNow;
        public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken ct)
        {
            object result = path.EndsWith("recommendations", StringComparison.Ordinal)
                ? (IReadOnlyList<기사배차추천항목응답>)[new() { 의뢰Id = "r77", 차량적합여부 = true,
                    추천라운드 = Round, 추천시작시각 = start, 추천만료시각 = start.AddHours(1),
                    기사지급예정운임 = 6000m, 기사지급예정운임근거 = "운임구성.기사지급예정운임" }]
                : path.EndsWith("workspace", StringComparison.Ordinal)
                    ? new 기사화물운송작업공간응답 { 활성운송목록 = [new() { Id = 77, 운송번호 = "r77", 상태 = "배차확정" }] }
                    : new 기사운송의뢰상세응답 { 의뢰Id = "r77", 차량적합여부 = true, 배차상태 = "대기" };
            return Task.FromResult((T)result);
        }
        public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken ct)
        { Writes++; LastBody = Assert.IsType<기사화물배차수락요청>(body); return Task.FromResult((T?)(object)new 기사배차처리응답 { RequestId = "r77" }); }
        public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken ct) => throw new NotSupportedException();
        public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Access : IRoleWorkspaceAccess
    {
        public event Action? Changed { add { } remove { } }
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => new("r77-driver", 1, true);
        public Task EnsureInitializedAsync(string roleKey, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignOutAsync(string roleKey, CancellationToken ct = default) => Task.CompletedTask;
    }
}
