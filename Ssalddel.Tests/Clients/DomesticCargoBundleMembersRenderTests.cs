using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Admin.Dispatch;
using SsalddelAdmin.Components.DispatchTraining;

namespace Ssalddel.Tests.Clients;

/// <summary>제품 구성 의뢰 컴포넌트의 실제 Razor 출력입니다. 배차 실행·브라우저 조작·배치 검증과 구분합니다.</summary>
public sealed class DomesticCargoBundleMembersRenderTests
{
    private static readonly DateTime EndUtc = new(2026, 11, 7, 3, 15, 0, DateTimeKind.Utc);

    [Fact]
    public async Task 단건제안은현재조회에서연결된화물과상차종료만읽기전용으로표시한다()
    {
        var request = Request("REQ-A", "냉장 식자재 12상자", EndUtc);
        var hidden = Request("PRIVATE-UNSELECTED-ID", "비선택 화물 원문", EndUtc.AddHours(1));

        var html = await RenderAsync(Bundle(1, "REQ-A"), [request, hidden]);

        var row = Assert.Single(MemberRows(html));
        Assert.Contains("REQ-A", row); Assert.Contains("냉장 식자재 12상자", row);
        Assert.Contains("상차 시간창 종료", row); Assert.Contains("2026-11-07 12:15 (한국)", row);
        Assert.Contains("AI 제안의 구성 의뢰", html); Assert.Contains("방문 순서와 배차 결과는 별도로 확인", html);
        Assert.DoesNotContain("PRIVATE-UNSELECTED-ID", html); Assert.DoesNotContain("비선택 화물 원문", html);
        Assert.DoesNotContain(request.PickupAddress, html); Assert.DoesNotContain(request.DropoffAddress, html);
        Assert.DoesNotContain("제안 표시 건수와 구성 의뢰 참조를 다시 확인", html);
        Assert.DoesNotContain("<button", html); Assert.DoesNotContain("<input", html); Assert.DoesNotContain("<form", html);
        Assert.DoesNotContain("배차 확정", html); Assert.DoesNotContain("상차 시간창 시작", html);
    }

    [Fact]
    public async Task 두건제안은현재조회순서가달라도명시한의뢰순서에맞춰화물과시각을연결한다()
    {
        var requests = new[] { Request("REQ-A", "가상 쌀 8포", EndUtc), Request("REQ-B", "가상 상자 3개", EndUtc.AddHours(2)) };

        var html = await RenderAsync(Bundle(2, "REQ-B", "REQ-A"), requests);

        var rows = MemberRows(html); Assert.Equal(2, rows.Count);
        Assert.Contains("REQ-B", rows[0]); Assert.Contains("가상 상자 3개", rows[0]); Assert.Contains("2026-11-07 14:15 (한국)", rows[0]);
        Assert.DoesNotContain("가상 쌀 8포", rows[0]); Assert.DoesNotContain("2026-11-07 12:15", rows[0]);
        Assert.Contains("REQ-A", rows[1]); Assert.Contains("가상 쌀 8포", rows[1]); Assert.Contains("2026-11-07 12:15 (한국)", rows[1]);
        Assert.DoesNotContain("가상 상자 3개", rows[1]); Assert.DoesNotContain("2026-11-07 14:15", rows[1]);
        Assert.DoesNotContain("방문 1", html); Assert.DoesNotContain("방문 2", html);
    }

    [Fact]
    public async Task 현재조회에없는참조는기존정상구성을유지하며원시번호를노출하지않는다()
    {
        const string unknown = "PRIVATE-UNKNOWN-DO-NOT-RENDER";
        var html = await RenderAsync(Bundle(2, "REQ-A", unknown),
            [Request("REQ-A", "확인된 가상 화물", EndUtc), Request("PRIVATE-OTHER", "다른 화물 원문", EndUtc)]);

        var rows = MemberRows(html); Assert.Equal(2, rows.Count);
        Assert.Contains("확인된 가상 화물", rows[0]); Assert.Contains("의뢰 연결 미확인", rows[1]);
        Assert.Contains("현재 조회한 의뢰와 연결을 확인", rows[1]);
        Assert.DoesNotContain(unknown, html); Assert.DoesNotContain("PRIVATE-OTHER", html); Assert.DoesNotContain("다른 화물 원문", html);
    }

    [Fact]
    public async Task 같은참조가중복되면정상두건으로표시하거나현재의뢰내용을임의로반복하지않는다()
    {
        var html = await RenderAsync(Bundle(2, "PRIVATE-DUPLICATE", "PRIVATE-DUPLICATE"),
            [Request("PRIVATE-DUPLICATE", "중복 연결 후보 원문", EndUtc)]);

        var rows = MemberRows(html); Assert.Equal(2, rows.Count);
        Assert.All(rows, row => { Assert.Contains("의뢰 연결 미확인", row); Assert.Contains("참조가 중복", row); });
        Assert.DoesNotContain("PRIVATE-DUPLICATE", html); Assert.DoesNotContain("중복 연결 후보 원문", html);
        Assert.DoesNotContain("2026-11-07 12:15", html);
    }

    [Fact]
    public async Task 현재조회에같은번호가여러개면첫후보로연결하지않고어느후보내용도노출하지않는다()
    {
        var html = await RenderAsync(Bundle(1, "PRIVATE-AMBIGUOUS"),
            [Request("PRIVATE-AMBIGUOUS", "모호한 첫 화물 원문", EndUtc), Request("PRIVATE-AMBIGUOUS", "모호한 둘째 화물 원문", EndUtc.AddHours(2))]);

        var row = Assert.Single(MemberRows(html));
        Assert.Contains("의뢰 연결 미확인", row); Assert.Contains("현재 의뢰가 여러 건", row);
        Assert.DoesNotContain("PRIVATE-AMBIGUOUS", html); Assert.DoesNotContain("모호한 첫 화물 원문", html); Assert.DoesNotContain("모호한 둘째 화물 원문", html);
        Assert.DoesNotContain("2026-11-07 12:15", html); Assert.DoesNotContain("2026-11-07 14:15", html);
    }

    [Theory]
    [InlineData("req-a")]
    [InlineData("REQ-A ")]
    public async Task 의뢰번호의대소문자나공백을임의로바꿔다른원문과연결하지않는다(string reference)
    {
        var html = await RenderAsync(Bundle(1, reference), [Request("REQ-A", "정확히연결되지않은화물", EndUtc)]);
        Assert.Contains("의뢰 연결 미확인", html); Assert.DoesNotContain("정확히연결되지않은화물", html);
        Assert.DoesNotContain("2026-11-07 12:15", html);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task 빈참조를현재조회의빈번호항목과도연결하지않는다(string? reference)
    {
        var html = await RenderAsync(Bundle(1, reference!), [Request(reference!, "빈번호후보원문", EndUtc)]);
        Assert.Contains("의뢰 연결 미확인", html); Assert.Contains("구성 의뢰의 연결 정보를 확인", html);
        Assert.DoesNotContain("빈번호후보원문", html);
    }

    [Theory]
    [InlineData("null-bundle")]
    [InlineData("null-ids")]
    [InlineData("empty-ids")]
    public async Task 구성참조자체가없으면정상단건을만들지않고확인필요로남긴다(string variant)
    {
        var bundle = variant == "null-bundle" ? null : Bundle(1);
        if (variant == "null-ids") bundle!.RequestIds = null!;

        var html = await RenderAsync(bundle, [Request("REQ-A", "조회에는있는화물", EndUtc)]);

        Assert.Empty(MemberRows(html)); Assert.Contains("구성 의뢰 연결을 확인하지 못했습니다", html);
        Assert.Contains("제안 표시 건수와 구성 의뢰 참조를 다시 확인", html); Assert.DoesNotContain("조회에는있는화물", html);
    }

    [Fact]
    public async Task 현재조회가없어도구성원시번호를드러내지않고조회미확인을표시한다()
    {
        var html = await RenderAsync(Bundle(1, "PRIVATE-MISSING-CURRENT"), null);
        Assert.Contains("의뢰 연결 미확인", html); Assert.Contains("현재 조회한 의뢰와 연결을 확인", html);
        Assert.DoesNotContain("PRIVATE-MISSING-CURRENT", html);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(7)]
    public async Task 제안표시건수가실제구성참조와다르면연결된내용과별개로확인필요를표시한다(int advertisedSize)
    {
        var html = await RenderAsync(Bundle(advertisedSize, "REQ-A", "REQ-B"),
            [Request("REQ-A", "가상 식자재", EndUtc), Request("REQ-B", "가상 생활용품", EndUtc.AddHours(1))]);

        Assert.Equal(2, MemberRows(html).Count); Assert.Contains("가상 식자재", html); Assert.Contains("가상 생활용품", html);
        Assert.Contains("제안 표시 건수와 구성 의뢰 참조를 다시 확인", html);
    }

    [Theory]
    [MemberData(nameof(TimeCases))]
    public async Task 원천시각만한국시각으로읽고없는값과변환불가값은미확인으로남긴다(DateTime? value, string expected)
    {
        var html = await RenderAsync(Bundle(1, "REQ-A"), [Request("REQ-A", "가상 화물", value)]);
        var row = Assert.Single(MemberRows(html));
        Assert.Contains("상차 시간창 종료", row); Assert.Contains(expected, row);
        Assert.DoesNotContain("0001-01-01", row); Assert.DoesNotContain("조회 시각", row);
        if (expected != "미확인") Assert.DoesNotContain("미확인", row);
    }

    public static IEnumerable<object?[]> TimeCases()
    {
        yield return [EndUtc, "2026-11-07 12:15 (한국)"];
        yield return [DateTime.SpecifyKind(EndUtc, DateTimeKind.Unspecified), "2026-11-07 12:15 (한국)"];
        yield return [new DateTimeOffset(EndUtc).LocalDateTime, "2026-11-07 12:15 (한국)"];
        yield return [new DateTime(2026, 12, 31, 14, 59, 0, DateTimeKind.Utc), "2026-12-31 23:59 (한국)"];
        yield return [new DateTime(2026, 12, 31, 15, 30, 0, DateTimeKind.Utc), "2027-01-01 00:30 (한국)"];
        yield return [null, "미확인"];
        yield return [default(DateTime), "미확인"];
        yield return [DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc), "미확인"];
        yield return [DateTime.SpecifyKind(DateTime.MaxValue.AddHours(-9), DateTimeKind.Utc), "9999-12-31 23:59 (한국)"];
    }

    [Fact]
    public async Task 화물본문이비어있어도다른업무값으로추정해채우지않는다()
    {
        var request = Request("REQ-A", " ", EndUtc); request.SourceType = "PRIVATE-SOURCE-TYPE";
        var html = await RenderAsync(Bundle(1, "REQ-A"), [request]);
        Assert.Contains("화물 내용 미확인", html); Assert.DoesNotContain("PRIVATE-SOURCE-TYPE", html);
    }

    [Fact]
    public async Task 연결된원의뢰번호와화물본문도실행되는HTML이아닌원문으로인코딩한다()
    {
        const string id = "<script>request_marker()</script>";
        const string cargo = "<script>cargo_marker()</script>";
        var raw = await RenderRawAsync(Bundle(1, id), [Request(id, cargo, EndUtc)]);
        Assert.DoesNotContain("<script>", raw); Assert.Contains("&lt;script&gt;request_marker()&lt;/script&gt;", raw);
        Assert.Contains("&lt;script&gt;cargo_marker()&lt;/script&gt;", raw);
        Assert.Contains(id, WebUtility.HtmlDecode(raw)); Assert.Contains(cargo, WebUtility.HtmlDecode(raw));
    }

    [Fact]
    public async Task 렌더가제안의크기참조와현재의뢰원문및UTC시각을수정하지않는다()
    {
        var bundle = Bundle(7, "REQ-B", "REQ-A", "REQ-A", "PRIVATE-UNKNOWN");
        bundle.BundleKey = "PRIVATE-BUNDLE-KEY"; bundle.SuggestedDriverId = "PRIVATE-DRIVER-ID";
        bundle.ExpectedFare = 107000; bundle.ExpectedProfit = 43000;
        var requests = new[] { Request("REQ-A", "가상 화물 A", EndUtc), Request("REQ-B", "가상 화물 B", null) };
        var before = JsonSerializer.Serialize(new { bundle, requests });

        var html = await RenderAsync(bundle, requests);

        Assert.Equal(before, JsonSerializer.Serialize(new { bundle, requests }));
        Assert.DoesNotContain("PRIVATE-BUNDLE-KEY", html); Assert.DoesNotContain("PRIVATE-DRIVER-ID", html);
        Assert.DoesNotContain("107000", html); Assert.DoesNotContain("43000", html);
    }

    private static DomesticCargoDispatchAIReviewBundleDto Bundle(int size, params string[] ids)
        => new() { BundleKey = "가상 제안", BundleSize = size, RequestIds = ids.ToList() };

    private static DomesticCargoDispatchAIReviewRequestDto Request(string id, string cargo, DateTime? end)
        => new()
        {
            RequestId = id, CargoType = cargo, PickupWindowEndUtc = end,
            PickupAddress = "PRIVATE-PICKUP-ADDRESS", DropoffAddress = "PRIVATE-DROPOFF-ADDRESS"
        };

    private static IReadOnlyList<string> MemberRows(string html)
        => Regex.Matches(html, "<li\\b[^>]*>(?:(?!</li>).)*</li>", RegexOptions.Singleline)
            .Cast<Match>().Select(match => match.Value).ToArray();

    private static async Task<string> RenderAsync(DomesticCargoDispatchAIReviewBundleDto? bundle,
        IReadOnlyList<DomesticCargoDispatchAIReviewRequestDto>? requests)
        => WebUtility.HtmlDecode(await RenderRawAsync(bundle, requests));

    private static async Task<string> RenderRawAsync(DomesticCargoDispatchAIReviewBundleDto? bundle,
        IReadOnlyList<DomesticCargoDispatchAIReviewRequestDto>? requests)
    {
        var services = new ServiceCollection(); services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<DomesticCargoBundleMembers>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(DomesticCargoBundleMembers.Bundle)] = bundle,
                [nameof(DomesticCargoBundleMembers.Requests)] = requests
            }))).ToHtmlString());
    }
}
