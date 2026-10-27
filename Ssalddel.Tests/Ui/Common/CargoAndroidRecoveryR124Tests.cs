using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CargoAndroidRecoveryR124Tests
{
    [Fact]
    public async Task 만료된_추천으로_복귀하면_선택을_해제하고_정상_대기를_표시한다()
    {
        var api = new Api();
        var adapter = new CargoDriverWorkspaceAdapter(new(api));

        var result = await adapter.LoadAsync("offer:expired", CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Null(result.SelectedId);
        Assert.Contains("선택한 추천을 더 이상 확인할 수 없습니다", result.Message);
        Assert.Equal(0, api.Writes);
    }

    [Fact]
    public async Task 과거_추천이_없으면_현재_계정에_반환된_추천만_선택한다()
    {
        var api = new Api { Offers = [new() { 의뢰Id = "new-current", 차량적합여부 = true }] };
        var adapter = new CargoDriverWorkspaceAdapter(new(api));

        var result = await adapter.LoadAsync("offer:old-other-account", CancellationToken.None);

        Assert.Equal("offer:new-current", result.SelectedId);
        Assert.Single(result.Items);
        Assert.DoesNotContain("old-other-account", result.Message);
        Assert.Equal(0, api.Writes);
    }

    [Fact]
    public async Task 현재_추천이_있으면_기존_선택을_유지한다()
    {
        var api = new Api { Offers = [new() { 의뢰Id = "first" }, new() { 의뢰Id = "chosen" }] };
        var adapter = new CargoDriverWorkspaceAdapter(new(api));

        var result = await adapter.LoadAsync("offer:chosen", CancellationToken.None);

        Assert.Equal("offer:chosen", result.SelectedId);
        Assert.DoesNotContain("선택한 추천", result.Message ?? "");
        Assert.Equal(0, api.Writes);
    }

    [Fact]
    public async Task 만료후_새추천이_있으면_완료이력보다_현재추천을_우선한다()
    {
        var api = new Api
        {
            Active = [new() { Id = 21, 상태 = "배송완료" }],
            Offers = [new() { 의뢰Id = "new-current" }]
        };
        var result = await new CargoDriverWorkspaceAdapter(new(api)).LoadAsync("offer:old", CancellationToken.None);

        Assert.Equal("offer:new-current", result.SelectedId);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(0, api.Writes);
    }

    [Fact]
    public async Task 이미_수락한_현재_운송이_있으면_과거_추천대신_그_운송을_조회한다()
    {
        var api = new Api { Active = [new() { Id = 42, 상태 = "배차확정", 출발지 = "검증 상차", 도착지 = "검증 하차" }] };
        var adapter = new CargoDriverWorkspaceAdapter(new(api));

        var result = await adapter.LoadAsync("offer:accepted-before", CancellationToken.None);

        Assert.Equal("42", result.SelectedId);
        Assert.Single(result.Items);
        Assert.Equal(0, api.OfferReads);
        Assert.Equal(0, api.Writes);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task 추천_권한조회실패를_만료복구로_숨기지_않는다(int status)
    {
        var api = new Api { OfferFailure = new RoleWorkspaceAccessException(status, "본인 권한 확인 필요") };
        var adapter = new CargoDriverWorkspaceAdapter(new(api));

        var error = await Assert.ThrowsAsync<RoleWorkspaceAccessException>(() => adapter.LoadAsync("offer:old", CancellationToken.None));

        Assert.Equal(status, error.StatusCode);
        Assert.Equal(0, api.Writes);
    }

    [Fact]
    public async Task 추천_서버조회실패를_빈_업무_성공으로_바꾸지_않는다()
    {
        var api = new Api { OfferFailure = new HttpRequestException("synthetic server failure") };
        var adapter = new CargoDriverWorkspaceAdapter(new(api));

        await Assert.ThrowsAsync<HttpRequestException>(() => adapter.LoadAsync("offer:old", CancellationToken.None));

        Assert.Equal(0, api.Writes);
    }

    private sealed class Api : IRoleWorkspaceApi
    {
        public IReadOnlyList<기사배차추천항목응답> Offers { get; init; } = [];
        public IReadOnlyList<기사운송요약응답> Active { get; init; } = [];
        public Exception? OfferFailure { get; init; }
        public int OfferReads { get; private set; }
        public int Writes { get; private set; }

        public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken)
        {
            Assert.Equal(RoleWorkspaceCatalog.CargoDriver, roleKey);
            cancellationToken.ThrowIfCancellationRequested();
            object result;
            if (path.EndsWith("work/status", StringComparison.Ordinal))
                result = new 기사운행상태응답 { DriverId = "driver-r124", Status = "대기" };
            else if (path.EndsWith("operational-dispatch/availability", StringComparison.Ordinal))
                result = new 운영배차수신상태Dto { 주체Id = "driver-r124" };
            else if (path.EndsWith("transports/workspace", StringComparison.Ordinal))
                result = new 기사화물운송작업공간응답 { 활성운송목록 = Active };
            else if (path.EndsWith("recommendations", StringComparison.Ordinal))
            {
                OfferReads++;
                if (OfferFailure is { } failure) return Task.FromException<T>(failure);
                result = Offers;
            }
            else if (path.EndsWith("transports/42", StringComparison.Ordinal))
                result = new 기사운송상세응답 { Id = 42, 상태 = "배차확정", 출발지 = "검증 상차", 도착지 = "검증 하차" };
            else throw new InvalidOperationException("Unexpected test read: " + path);
            return Task.FromResult((T)result);
        }

        public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken ct)
        { Writes++; throw new InvalidOperationException("Recovery must not issue a command."); }
        public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken ct)
        { Writes++; throw new InvalidOperationException("Recovery must not issue a command."); }
        public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken ct)
        { Writes++; throw new InvalidOperationException("Recovery must not upload."); }
    }
}
