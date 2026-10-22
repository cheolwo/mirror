using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Admin.BusinessPackages;
using Ssalddel.Contracts.Common.Community;
using SsalddelAdmin.Services;

namespace Ssalddel.Tests.Ui.Admin;

public sealed class AdminTransportOverviewNavigationR12Tests
{
    [Theory]
    [InlineData("/transports", null)]
    [InlineData(업무패키지관리Routes.FreightDeliveryTransports, BusinessPackageCatalog.FreightDelivery)]
    [InlineData(업무패키지관리Routes.OrderWarehouseOutboundTransports, null)]
    public void 목록의업무별칭과조회조건이상세및전문조회왕복에서유지된다(string path, string? business)
    {
        var context = 운송관제NavigationContext.ForList(path, " 운송중 ", "의뢰 A&B+1", true);
        Assert.Equal(business, context.BusinessCode);
        var listQuery = Query(context.ListHref);
        Assert.Equal("운송중", listQuery["status"]);
        Assert.Equal("의뢰 A&B+1", listQuery["eventRequest"]);
        Assert.Equal("true", listQuery["adminOnly"]);

        var detail = context.DetailHref("REQ:1");
        Assert.StartsWith("/transports/REQ%3A1?", detail);
        Assert.Equal(context.ListHref, Query(detail)[PageNavigationQueryNames.ReturnPath]);
        foreach (var segment in new[] { "events", "proofs", "settlement" })
        {
            var child = 운송관제NavigationContext.WorkflowHref("REQ:1", segment,
                Query(detail)[PageNavigationQueryNames.ReturnPath]);
            Assert.StartsWith("/transports/REQ%3A1/" + segment + "?", child);
            Assert.Equal(context.ListHref, Query(child)[PageNavigationQueryNames.ReturnPath]);
            var overview = 운송관제NavigationContext.WorkflowHref("REQ:1", null,
                Query(child)[PageNavigationQueryNames.ReturnPath]);
            Assert.Equal(detail, overview);
            Assert.Equal(context.ListHref, 운송관제NavigationContext.ResolveListHref(
                Query(overview)[PageNavigationQueryNames.ReturnPath]));
        }
    }

    [Theory]
    [InlineData("https://outside.example/transports")]
    [InlineData("//outside.example/transports")]
    [InlineData("/\\outside.example/transports")]
    [InlineData("/%2foutside.example/transports")]
    [InlineData("/transports/../login")]
    [InlineData("/admin/freight-delivery/transports/../requests")]
    [InlineData("/admin/food-delivery/operations")]
    [InlineData("/transports?access_token=private")]
    [InlineData("/transports?from=%2Flogin")]
    [InlineData("/transports?status=one&status=two")]
    [InlineData("/transports?adminOnly=invalid")]
    [InlineData("/transports?eventRequest=%0a")]
    [InlineData("/transports#outside")]
    public void 허용목록밖의복귀와중복또는알수없는조건은안전한기존목록으로돌아간다(string unsafeReturn)
    {
        Assert.Equal("/transports", 운송관제NavigationContext.ResolveListHref(unsafeReturn));
        var link = 운송관제NavigationContext.WorkflowHref("REQ:1", "events", unsafeReturn);
        Assert.Equal("/transports", Query(link)[PageNavigationQueryNames.ReturnPath]);
        Assert.DoesNotContain("private", link);
    }

    [Fact]
    public void 오래된상세링크와빈조건은기존목록으로호환한다()
    {
        Assert.Equal("/transports", 운송관제NavigationContext.ResolveListHref(null));
        Assert.Equal("/transports", 운송관제NavigationContext.ForList("/transports", " ", null).ListHref);
        Assert.Equal(업무패키지관리Routes.FreightDeliveryTransports,
            운송관제NavigationContext.ResolveListHref(업무패키지관리Routes.FreightDeliveryTransports + "?adminOnly=false"));
        Assert.Throws<ArgumentException>(() => 운송관제NavigationContext.WorkflowHref("REQ:1", "../payments", null));
    }

    [Theory]
    [InlineData("/ADMIN/FREIGHT-DELIVERY/TRANSPORTS", 업무패키지관리Routes.FreightDeliveryTransports, BusinessPackageCatalog.FreightDelivery)]
    [InlineData("/admin/freight-delivery/transports/", 업무패키지관리Routes.FreightDeliveryTransports, BusinessPackageCatalog.FreightDelivery)]
    [InlineData("/TRANSPORTS", "/transports", null)]
    [InlineData("/transports/", "/transports", null)]
    [InlineData("/ADMIN/ORDER-WAREHOUSE/OUTBOUND-TRANSPORTS", 업무패키지관리Routes.OrderWarehouseOutboundTransports, null)]
    [InlineData("/admin/order-warehouse/outbound-transports/", 업무패키지관리Routes.OrderWarehouseOutboundTransports, null)]
    public void 라우터가허용한대소문자와끝슬래시가업무필터를해제하지않는다(string path, string canonical, string? business)
    {
        var context = 운송관제NavigationContext.ForList(path, "운송중");
        Assert.Equal(canonical, context.ListPath);
        Assert.Equal(business, context.BusinessCode);
        Assert.Equal(context.ListHref, 운송관제NavigationContext.ResolveListHref(path + "?status=" + Uri.EscapeDataString("운송중")));
    }

    [Fact]
    public async Task 실제관리자소비자는명시한업무와상태를서버GET에보내고기존관리자자격을사용한다()
    {
        using var fixture = new Fixture(true);
        Assert.Empty(await fixture.Service.운송진행목록조회Async("운송중", BusinessPackageCatalog.FreightDelivery));
        var call = Assert.Single(fixture.Handler.Calls);
        Assert.Equal(HttpMethod.Get, call.Method);
        Assert.Equal("/api/v1/admin/transports", call.Uri.AbsolutePath);
        Assert.Equal("freight-delivery", Query(call.Uri.PathAndQuery)["business"]);
        Assert.Equal("운송중", Query(call.Uri.PathAndQuery)["상태"]);
        Assert.Equal("fixture-admin-r12", call.Bearer);
    }

    [Fact]
    public async Task 기존필터없는호출은API계약을유지한다()
    {
        using var fixture = new Fixture(true);
        await fixture.Service.운송진행목록조회Async("상차완료", CancellationToken.None);
        var call = Assert.Single(fixture.Handler.Calls);
        Assert.Equal("상차완료", Query(call.Uri.PathAndQuery)["상태"]);
        Assert.False(Query(call.Uri.PathAndQuery).ContainsKey("business"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("order-warehouse")]
    [InlineData("unknown")]
    public async Task 명시한잘못된업무는전체조회로바꾸지않는다(string business)
    {
        using var fixture = new Fixture(true);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.운송진행목록조회Async(null, business));
        Assert.Empty(fixture.Handler.Calls);
    }

    [Fact]
    public async Task 관리자로그인없이새필터조회가실행되지않는다()
    {
        using var fixture = new Fixture(false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.운송진행목록조회Async(null, "freight-delivery"));
        Assert.Empty(fixture.Handler.Calls);
    }

    [Fact]
    public async Task 업무분류없는기존서비스는필터요청을전체목록으로대체하지않는다()
    {
        I백오피스Service service = new 백오피스메모리Service(new 문서관리메모리Service());
        var original = await service.운송진행목록조회Async();
        Assert.NotEmpty(original);
        await Assert.ThrowsAsync<NotSupportedException>(() => service.운송진행목록조회Async(null, "freight-delivery"));
        Assert.Equal(original.Count, (await service.운송진행목록조회Async(null, (string?)null)).Count);
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("business")]
    [InlineData("events")]
    public async Task null본문을운송또는이벤트가없는정상목록으로표시하지않는다(string mode)
    {
        using var fixture = new Fixture(true, "null");
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ReadAsync(mode));
        Assert.Single(fixture.Handler.Calls);
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("business")]
    [InlineData("events")]
    public async Task null항목이들어온목록은화면정상값으로전달하지않는다(string mode)
    {
        using var fixture = new Fixture(true, "[null]");
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.ReadAsync(mode));
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("business")]
    [InlineData("events")]
    public async Task 정상빈배열은실제조회결과0건으로유지한다(string mode)
    {
        using var fixture = new Fixture(true, "[]");
        Assert.Equal(0, await fixture.ReadAsync(mode));
        Assert.Single(fixture.Handler.Calls);
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("business")]
    [InlineData("events")]
    public async Task 서버오류를정상빈목록으로치환하지않는다(string mode)
    {
        using var fixture = new Fixture(true, "[]", HttpStatusCode.InternalServerError);
        await Assert.ThrowsAsync<HttpRequestException>(() => fixture.ReadAsync(mode));
    }

    private static Dictionary<string, Microsoft.Extensions.Primitives.StringValues> Query(string route)
        => QueryHelpers.ParseQuery(new Uri(new Uri("https://admin-r12.invalid"), route).Query);

    private sealed class Fixture : IDisposable
    {
        private readonly HttpClient http;
        public Handler Handler { get; }
        public 백오피스조회Service Service { get; }
        public Fixture(bool authenticated, string body = "[]", HttpStatusCode status = HttpStatusCode.OK)
        {
            Handler = new(body, status);
            http = new(Handler) { BaseAddress = new Uri("https://admin-r12.invalid/") };
            var session = new 관리자인증세션Service(new ConfigurationBuilder().Build(), new EnvironmentStub());
            if (authenticated) session.로그인적용(new 토큰응답
            { AccessToken = "fixture-admin-r12", UserId = "fixture-admin", Roles = ["서버관리자"] });
            Service = new(http, session, new TransportRequestLedgerObserver());
        }
        public async Task<int> ReadAsync(string mode)
            => mode switch
            {
                "plain" => (await Service.운송진행목록조회Async()).Count,
                "business" => (await Service.운송진행목록조회Async(null, "freight-delivery")).Count,
                "events" => (await Service.운송이벤트조회Async()).Count,
                _ => throw new ArgumentException("시험 조회 경로를 확인해 주세요.", nameof(mode))
            };
        public void Dispose() => http.Dispose();
    }

    private sealed class Handler(string body, HttpStatusCode status) : HttpMessageHandler
    {
        public List<(HttpMethod Method, Uri Uri, string? Bearer)> Calls { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add((request.Method, request.RequestUri!, request.Headers.Authorization?.Parameter));
            return Task.FromResult(new HttpResponseMessage(status)
            { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class EnvironmentStub : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "AdminTransportR12Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
