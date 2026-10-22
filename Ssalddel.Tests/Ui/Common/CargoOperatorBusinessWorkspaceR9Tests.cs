using Ssalddel.Contracts.Admin.BusinessPackages;
using Ssalddel.Contracts.Admin.Progress;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CargoOperatorBusinessWorkspaceR9Tests
{
    [Fact]
    public async Task 화물관리자는동일관리자자격으로화물조회만하고주소에서핀을추정하지않는다()
    {
        var api = new Api([new()
        {
            Id = 3, 운송번호 = "운송 3", 상태 = "상차완료", 출발지 = "상차지", 도착지 = "하차지", 운임 = 15000,
            기사_운송자 = "기사 1", 관리자확인필요 = true, 예외신고됨 = true,
            최근예외메시지 = "내부 오류 원문", 최근예외코드 = "InternalCode"
        }]);
        var adapter = new CargoOperatorWorkspaceAdapter(api);
        var snapshot = await adapter.LoadAsync("3", default);

        Assert.Equal(RoleWorkspaceCatalog.Operator, adapter.RoleKey);
        Assert.Equal(BusinessPackageCatalog.FreightDelivery, adapter.BusinessCode);
        Assert.Equal(("operator", "api/v1/admin/transports?business=freight-delivery"), Assert.Single(api.Calls));
        var item = Assert.Single(snapshot.Items);
        Assert.Empty(snapshot.EmptyActions ?? []);
        Assert.Equal("3", snapshot.SelectedId);
        Assert.True(item.IsCurrent);
        Assert.Empty(item.Markers ?? []);
        Assert.Empty(item.Routes ?? []);
        Assert.Equal("하차지", item.Summary!.Destination);
        Assert.Contains(item.Summary.Metrics!, field => field.Label == "운임" && field.Value == "15,000원");
        Assert.DoesNotContain(item.Sections!.SelectMany(section => section.Fields), field => field.Value.Contains("내부 오류") || field.Value.Contains("InternalCode"));
        var action = Assert.Single(item.Actions!);
        Assert.False(action.IsPrimary);
        Assert.Equal("화물 사건 목록", action.Label);
        Assert.Equal(RoleWorkspaceActionUsage.OptionalTool, RoleWorkspaceActionPolicy.GetUsage(adapter.RoleKey, action));
        Assert.StartsWith("/workspace/operator/cargo-incidents?business=freight-delivery&", action.Route);
        Assert.Contains("transportId=3&", action.Route);
        Assert.EndsWith("returnUrl=%2Fworkspace%2Foperator%3Fbusiness%3Dfreight-delivery%26selected%3D3", action.Route);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task 과거신고플래그는현재미종결사건이나필수검토행동으로단정하지않는다(bool reported, bool required)
    {
        var api = new Api([new()
        {
            Id = 7, 상태 = "인수완료", 예외신고됨 = reported, 관리자확인필요 = required,
            최근예외단계 = "상차", 최근예외메시지 = "과거 신고"
        }]);
        var adapter = new CargoOperatorWorkspaceAdapter(api);
        var snapshot = await adapter.LoadAsync("7", default);
        var item = Assert.Single(snapshot.Items);
        Assert.Equal("운송 진행", item.Summary!.Request!.Value);
        var record = Assert.Single(item.Sections!, section => section.Title == "신고 기록");
        Assert.Equal(new RoleWorkspaceField("신고 단계", "상차"), Assert.Single(record.Fields));
        Assert.DoesNotContain(item.Sections!, section => section.Title.Contains("확인 필요"));
        Assert.DoesNotContain(item.Sections!.SelectMany(section => section.Fields), field => field.Value.Contains("사건 검토 필요"));
        var action = Assert.Single(item.Actions!);
        Assert.False(action.IsPrimary);
        Assert.Equal(RoleWorkspaceActionUsage.OptionalTool, RoleWorkspaceActionPolicy.GetUsage(adapter.RoleKey, action));
    }

    [Fact]
    public async Task 빈목록도화물사건진입만남기고가상운송이나음식도구를만들지않는다()
    {
        var snapshot = await new CargoOperatorWorkspaceAdapter(new Api([])).LoadAsync("missing", default);
        Assert.Empty(snapshot.Items);
        Assert.Null(snapshot.SelectedId);
        Assert.Equal(CargoOperatorWorkspaceAdapter.ReviewIncidents, Assert.Single(snapshot.EmptyActions!).Key);
    }

    [Fact]
    public async Task 목록이바뀌면사라진선택을되살리지않고현재반환된운송으로복귀한다()
    {
        var api = new Api([new() { Id = 8, 상태 = "인수완료" }]);
        var snapshot = await new CargoOperatorWorkspaceAdapter(api).LoadAsync("removed", default);
        Assert.Equal("8", snapshot.SelectedId);
        Assert.False(Assert.Single(snapshot.Items).IsCurrent);
    }

    [Fact]
    public async Task 관리자권한실패를빈목록이나샘플로숨기지않는다()
    {
        var api = new Api([]) { Failure = new RoleWorkspaceAccessException(403, "권한 확인") };
        var result = await Assert.ThrowsAsync<RoleWorkspaceAccessException>(() => new CargoOperatorWorkspaceAdapter(api).LoadAsync(null, default));
        Assert.Equal(403, result.StatusCode);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    public async Task 잘못된식별자나중복응답은판단카드로표시하지않는다(long first, long second)
    {
        var api = new Api([new() { Id = first }, new() { Id = second }]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CargoOperatorWorkspaceAdapter(api).LoadAsync(null, default));
    }

    [Fact]
    public async Task 화물관리조회카드는확정이나송금명령을직접실행하지않는다()
    {
        var api = new Api([]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CargoOperatorWorkspaceAdapter(api).PerformAsync("1", "complete", Guid.NewGuid(), default));
        Assert.Empty(api.Calls);
    }

    private sealed class Api(IReadOnlyList<운송진행응답> items) : IRoleWorkspaceApi
    {
        public List<(string Role, string Path)> Calls { get; } = [];
        public Exception? Failure { get; init; }
        public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken)
        { Calls.Add((roleKey, path)); return Failure is null ? Task.FromResult((T)(object)items) : Task.FromException<T>(Failure); }
        public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken) => throw new Xunit.Sdk.XunitException("조회 화면은 명령을 보내지 않습니다.");
        public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken) => throw new Xunit.Sdk.XunitException("조회 화면은 명령을 보내지 않습니다.");
        public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken cancellationToken) => throw new Xunit.Sdk.XunitException("조회 화면은 업로드하지 않습니다.");
    }
}
