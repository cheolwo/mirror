using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 분산입고일정요약R102RenderTests
{
    [Fact]
    public async Task 여러상품의조회된일정과예정실입고수량을_행별로구분한다()
    {
        var first = Line("line-a", "상품-A", 5, 2, Terms());
        var secondTerms = Terms();
        secondTerms.ArrivalStartUtc = secondTerms.ArrivalStartUtc.AddDays(2);
        secondTerms.ArrivalEndUtc = secondTerms.ArrivalEndUtc.AddDays(2);
        secondTerms.StorageEndsAtUtc = secondTerms.StorageEndsAtUtc.AddDays(2);
        var second = Line("line-b", "상품-B", 3, 0, secondTerms);
        var html = await Render([first, second]);
        Assert.Contains("상품-A", html); Assert.Contains("상품-B", html);
        Assert.Contains("예정 5 개 · 실제 입고 2 개", html);
        Assert.Contains("예정 3 개 · 실제 입고 0 개", html);
        Assert.Contains(분산이행표시.Time(first.InboundCollaboration!.Terms.ArrivalStartUtc), html);
        Assert.Contains(분산이행표시.Time(secondTerms.ArrivalStartUtc), html);
        Assert.DoesNotContain("입고 완료", html); Assert.DoesNotContain("<button", html);
    }

    [Fact]
    public async Task 일정이없거나기본날짜인경우_임의예정일을표시하지않는다()
    {
        var withoutTerms = new DistributedInboundLineDto("no-terms", "site-a", "location-a", "owner-private", "일정없는상품", "lot-private", 4, 0);
        var defaultDates = Line("default-dates", "날짜미확인상품", 2, 0, new());
        var html = await Render([withoutTerms, defaultDates]);
        Assert.Contains("도착·보관 일정 미확인", html); Assert.Contains("일정 미확인", html);
        Assert.DoesNotContain("01월 01일", html); Assert.DoesNotContain("입고 완료", html);
        Assert.Contains("보관 위치 확인 대기", html);
        Assert.DoesNotContain("owner-private", html); Assert.DoesNotContain("lot-private", html);
    }

    [Fact]
    public async Task 변경안과이전확정예약의일정수량을_별도로보인다()
    {
        var terms = Terms();
        var line = Line("line-a", "상품-A", 5, 1, terms);
        var reserved = Terms();
        reserved.Quantity = 3;
        reserved.ArrivalStartUtc = reserved.ArrivalStartUtc.AddDays(-2);
        reserved.ArrivalEndUtc = reserved.ArrivalEndUtc.AddDays(-2);
        var collaboration = line.InboundCollaboration!;
        collaboration.TermsVersion = 2;
        collaboration.ReservedTermsVersion = 1;
        collaboration.ReservedTerms = reserved;
        var before = JsonSerializer.Serialize(line);
        var html = await Render([line]);
        Assert.Contains("변경안 도착 예정", html); Assert.Contains("변경안 보관 종료", html);
        Assert.Contains("이전 확정 예약 · 1 차", html); Assert.Contains("확보 수량", html); Assert.Contains("3 개", html);
        Assert.Contains(분산이행표시.Time(terms.ArrivalStartUtc), html);
        Assert.Contains(분산이행표시.Time(reserved.ArrivalStartUtc), html);
        Assert.Contains("예정 5 개 · 실제 입고 1 개", html);
        Assert.DoesNotContain("입고 예약 승인", html); Assert.DoesNotContain("입고 완료", html);
        Assert.Equal(before, JsonSerializer.Serialize(line));
    }

    [Fact]
    public async Task 승인된예약도실입고가0이면_완료로바꾸지않고_비공개조건을반복노출하지않는다()
    {
        var line = Line("line-a", "상품-A", 5, 0, Terms());
        line.InboundCollaboration!.StatusCode = "Confirmed";
        line.InboundCollaboration.Terms.SupplierUserId = "supplier-private";
        line.InboundCollaboration.Terms.SupplyPriceOrFreeTerms = "비공개 공급금액";
        line.InboundCollaboration.Terms.ReturnTerms = "비공개 반환약정";
        var html = await Render([line]);
        Assert.Contains("입고 예약 승인", html); Assert.Contains("실제 입고 0 개", html);
        Assert.DoesNotContain("입고 완료", html); Assert.DoesNotContain("supplier-private", html);
        Assert.DoesNotContain("비공개 공급금액", html); Assert.DoesNotContain("비공개 반환약정", html);
    }

    [Fact]
    public async Task 거꾸로된도착창과보관종료는_일정미확인으로남긴다()
    {
        var terms = Terms();
        terms.ArrivalEndUtc = terms.ArrivalStartUtc.AddMinutes(-1);
        terms.StorageEndsAtUtc = terms.ArrivalEndUtc.AddMinutes(-1);
        var html = await Render([Line("line-a", "상품-A", 2, 0, terms)]);
        Assert.Equal(2, html.Split("일정 미확인", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain(분산이행표시.Time(terms.ArrivalStartUtc), html);
    }

    [Fact]
    public async Task 빈계획은일정요약의빈섹션을만들지않는다()
        => Assert.Equal(string.Empty, (await Render([])).Trim());

    [Fact]
    public async Task 조회된거점의같은위치표시명만사용하고_주소바코드는노출하지않는다()
    {
        var site = new DistributedSiteDto
        {
            SiteId = "site-a", PublicLabel = "단지 보관 공간", Floor = "private-floor", Subspace = "private-subspace",
            Locations = [new("location-a", "site-a", "barcode-private", "선반 1")]
        };
        var html = await Render([Line("line-a", "상품-A", 5, 0, Terms())], [site]);
        Assert.Contains("단지 보관 공간 · 선반 1", html);
        Assert.DoesNotContain("private-floor", html); Assert.DoesNotContain("private-subspace", html);
        Assert.DoesNotContain("barcode-private", html); Assert.DoesNotContain("site-a", html);
    }

    private static DistributedInboundLineDto Line(string id, string sku, int expected, int received, DistributedInboundCollaborationTerms terms)
        => new(id, "site-a", "location-a", "owner-private", sku, "lot-private", expected, received)
        {
            InboundCollaboration = new() { TermsVersion = 1, StatusCode = "Proposed", Terms = terms }
        };

    private static DistributedInboundCollaborationTerms Terms() => new()
    {
        Quantity = 5,
        ArrivalStartUtc = new(2026, 10, 20, 1, 0, 0, DateTimeKind.Utc),
        ArrivalEndUtc = new(2026, 10, 20, 3, 0, 0, DateTimeKind.Utc),
        StorageEndsAtUtc = new(2026, 10, 25, 3, 0, 0, DateTimeKind.Utc)
    };

    private static async Task<string> Render(IReadOnlyList<DistributedInboundLineDto> lines, IReadOnlyList<DistributedSiteDto>? sites = null)
    {
        var services = new ServiceCollection().AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => WebUtility.HtmlDecode(
            (await renderer.RenderComponentAsync<분산입고일정요약Panel>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(분산입고일정요약Panel.Lines)] = lines,
                    [nameof(분산입고일정요약Panel.Sites)] = sites ?? []
                }))).ToHtmlString()));
    }
}
