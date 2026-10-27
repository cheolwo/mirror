using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

namespace Ssalddel.Tests.Ui.Common;

public sealed class DistributedRoundAcceptanceSummaryTests
{
    public static TheoryData<decimal?> InvalidAmounts => new() { null, -1m, -0.01m };

    [Theory]
    [MemberData(nameof(InvalidAmounts))]
    public async Task 운임과계획값의_누락음수는_0원이나무료로바꾸지않는다(decimal? value)
    {
        var wave = Wave("wave-a", value); wave.TotalDistanceKm = value; wave.TotalElapsedMinutes = value;
        var html = await Render(wave);
        Assert.Equal("미확정", 분산순회수락요약표시.운임(value));
        Assert.Equal("미확정", 분산순회수락요약표시.거리(value));
        Assert.Equal("미확정", 분산순회수락요약표시.소요(value));
        Assert.Contains("미확정", html);
        Assert.DoesNotContain("0 원", html); Assert.DoesNotContain("무료", html);
    }

    [Fact]
    public async Task 제안운임0은_무료피킹이나포장동의가아니다()
    {
        var html = await Render(Wave("wave-a", 0m));
        Assert.Contains("0 원", html);
        Assert.Contains("포함·별도 여부 확인 필요", html);
        Assert.Contains("직접 피킹 선택은 포장 동의가 아닙니다", html);
        Assert.Contains("포장 작업·보수는 별도로 확인", html);
        Assert.DoesNotContain("무료", html); Assert.DoesNotContain("포장 없음", html);
        Assert.DoesNotContain("피킹 보수 0", html);
    }

    [Fact]
    public async Task 제안값을합산하지않고_정확히표시하며_수락과지급명령을추가하지않는다()
    {
        var wave = Wave("wave-a", 4321.25m); wave.TotalDistanceKm = 2.314159m; wave.TotalElapsedMinutes = 18.25m;
        var html = await Render(wave);
        Assert.Contains("4,321.25 원", html); Assert.Contains("2.314159 km", html); Assert.Contains("18.25 분", html);
        Assert.Contains("거점 직접 피킹 → 고객 전달", html);
        Assert.Contains("제안 운임은 입금·실지급 완료를 뜻하지 않습니다", html);
        Assert.DoesNotContain("<button", html); Assert.DoesNotContain("<input", html);
        Assert.DoesNotContain("name=\"consent", html); Assert.DoesNotContain("상세 주소", html);
    }

    [Fact]
    public void 모르는정차유형은_피킹포장배송확정으로바꾸지않는다()
    {
        var wave = Wave("wave-a", 5000m); wave.Stops = [Stop("unknown-a", 1, "Pack")];
        Assert.Equal("작업 범위 확인 필요", 분산순회수락요약표시.작업범위(wave));
        wave.Stops = [];
        Assert.Equal("작업 범위 확인 필요", 분산순회수락요약표시.작업범위(wave));
        wave.Stops = [Stop("delivery-a", 1, "Delivery")];
        Assert.Equal("고객 전달", 분산순회수락요약표시.작업범위(wave));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../other")]
    [InlineData("wave-a?selected=other")]
    [InlineData("wave-a/other")]
    public async Task 불안전한순회식별자는_약정복귀링크를만들지않는다(string id)
    {
        var wave = Wave(id, 5000m);
        Assert.Null(분산순회수락요약표시.약정확인경로(wave, "/driver/distributed/round?selected=" + id));
        Assert.DoesNotContain("href=", await Render(wave));
    }

    [Theory]
    [InlineData("https://evil.example/path")]
    [InlineData("//evil.example/path")]
    [InlineData("/driver/distributed/round?selected=other")]
    [InlineData("/driver/distributed/round?selected=wave-a&selected=other")]
    [InlineData("/driver/distributed/round?selected=wave-a&returnUrl=https%3A%2F%2Fevil.example")]
    [InlineData("/driver/distributed/round?selected=wave-a&requestId=not-a-guid")]
    [InlineData("/driver/distributed/round?selected=wave-a&unknown=evil")]
    public void 잘못된복귀링크는_현재순회의안전한경로로대체한다(string supplied)
    {
        var href = 분산순회수락요약표시.약정확인경로(Wave("wave-a", 5000m), supplied);
        Assert.Equal("/activity-settlements?returnUrl=" + Uri.EscapeDataString("/workspace/cargo-driver/distributed/round?selected=wave-a"), href);
        Assert.DoesNotContain("evil", href!); Assert.DoesNotContain("other", href!);
    }

    [Theory]
    [InlineData("/driver/distributed/round")]
    [InlineData("/workspace/cargo-driver/distributed/round")]
    public void 원래순회와미확정요청_상위복귀문맥을보존한다(string path)
    {
        var requestId = Guid.Parse("91824507-a999-4e05-8af0-2c4571668def");
        var returnHref = path + "?selected=wave-a&requestId=" + requestId.ToString("D")
            + "&returnUrl=" + Uri.EscapeDataString("/driver/transports/current");
        Assert.Equal("/activity-settlements?returnUrl=" + Uri.EscapeDataString(returnHref),
            분산순회수락요약표시.약정확인경로(Wave("wave-a", 5000m), returnHref));
    }

    [Fact]
    public async Task 같은컴포넌트의순회교체와해제후_이전운임과복귀링크가남지않는다()
    {
        var services = new ServiceCollection(); services.AddLogging(); await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            AcceptanceSummaryHost? host = null;
            var output = await renderer.RenderComponentAsync<AcceptanceSummaryHost>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(AcceptanceSummaryHost.Capture)] = (Action<AcceptanceSummaryHost>)(value => host = value) }));
            await host!.ChangeAsync(Wave("wave-a", 4321m), "/driver/distributed/round?selected=wave-a");
            var first = WebUtility.HtmlDecode(output.ToHtmlString()); Assert.Contains("4,321 원", first); Assert.Contains("wave-a", first);
            await host.ChangeAsync(Wave("wave-b", null), "/driver/distributed/round?selected=wave-b");
            var second = WebUtility.HtmlDecode(output.ToHtmlString()); Assert.DoesNotContain("4,321", second); Assert.DoesNotContain("wave-a", second);
            Assert.Contains("미확정", second); Assert.Contains("wave-b", second);
            await host.ChangeAsync(null, null);
            Assert.DoesNotContain("작업·보수 확인", WebUtility.HtmlDecode(output.ToHtmlString()));
            Assert.DoesNotContain("activity-settlements", output.ToHtmlString());
        });
    }

    [Fact]
    public async Task 실제순회요약을추가해도_기존수락경고와허용행동을유지하며추가조회전송을하지않는다()
    {
        var transport = DistributedUiTransport.Round(); var wave = transport.Workspace.Waves[0];
        wave.OfferedDriverFareKrw = 5000m;
        wave.AcceptanceWarnings = [new("cargo-a", "CheckTime", "방문 시간을 다시 확인해 주세요")];
        using var model = new 분산이행PageViewModel(new(transport, "cargo-driver"), "round");
        await model.LoadAsync("wave-a"); var beforeGets = transport.Gets.ToArray(); var beforeActions = wave.AllowedActions.ToArray();
        var services = new ServiceCollection(); services.AddLogging(); await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<분산순회Screen>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(분산순회Screen.Model)] = model, [nameof(분산순회Screen.ReturnHref)] = "/driver/distributed/round?selected=wave-a" }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
        Assert.Contains("방문 시간을 다시 확인해 주세요", html); Assert.Contains("이 배송 순회 수락", html);
        Assert.Contains("disabled", html); Assert.Contains("포함·별도 여부 확인 필요", html);
        Assert.Equal(beforeActions, wave.AllowedActions); Assert.Equal(beforeGets, transport.Gets);
        Assert.Empty(transport.Posts); Assert.Empty(transport.PhotoPosts);
    }

    private static DistributedWaveDto Wave(string id, decimal? fare) => new()
    {
        WaveId = id, Label = "현재 순회", Revision = 1, OfferedDriverFareKrw = fare,
        Stops = [Stop("pickup-a", 1, "Pickup"), Stop("delivery-a", 2, "Delivery")]
    };

    private static DistributedStopDto Stop(string id, int sequence, string kind)
        => new(id, sequence, kind, null, [], DateTime.UnixEpoch, DateTime.UnixEpoch, DateTime.UnixEpoch.AddHours(1), 0m, "Pending", "상세 주소", null, null);

    private static async Task<string> Render(DistributedWaveDto? wave)
    {
        var services = new ServiceCollection(); services.AddLogging(); await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<분산순회수락요약Panel>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(분산순회수락요약Panel.Wave)] = wave }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    public sealed class AcceptanceSummaryHost : ComponentBase
    {
        [Parameter] public Action<AcceptanceSummaryHost>? Capture { get; set; }
        private DistributedWaveDto? wave;
        private string? returnHref;
        protected override void OnInitialized() => Capture?.Invoke(this);
        public Task ChangeAsync(DistributedWaveDto? next, string? nextReturn) => InvokeAsync(() =>
        { wave = next; returnHref = nextReturn; StateHasChanged(); });
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<분산순회수락요약Panel>(0);
            builder.AddAttribute(1, nameof(분산순회수락요약Panel.Wave), wave);
            builder.AddAttribute(2, nameof(분산순회수락요약Panel.ReturnHref), returnHref);
            builder.CloseComponent();
        }
    }
}
