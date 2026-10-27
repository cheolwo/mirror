using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Ssalddel.Client.Infrastructure.Transport;
using SsalddelAdmin.Services;
using SharedRequest = Ssalddel.Contracts.Shipper.Request.화주운송의뢰응답;

namespace Ssalddel.Tests.Ui.BackOffice;

/// <summary>현재 공유 숫자 enum JSON을 실제 백오피스 소비자 세 경로로 읽는 회귀입니다. 실제 서버·환불 실행은 하지 않습니다.</summary>
public sealed class AdminShipperRequestContractR70Tests
{
    private const string RequestId = "admin-r70-fixture";
    private const string Body = """
        {"의뢰Id":"admin-r70-fixture","의뢰상태":"생성됨","결제상태":"결제완료",
         "정산상태":"입금대기","배차상태":"확정","운송방식":"퀵","결제수단":"별도정산",
         "정산시점":2,"증빙방식":0,"수납주체":0,"생성일시":"2026-10-19T00:00:00Z",
         "픽업지":"합성 픽업","하차지":"합성 전달","최종운임":18000,
         "요금옵션":{"기사지급예정운임":6200},"요약":{"화물종류":"R70 합성 상자","픽업지":"합성 픽업","하차지":"합성 전달"}}
        """;

    [Theory]
    [InlineData("detail")]
    [InlineData("list")]
    [InlineData("cancel")]
    public async Task 실제서비스는_공유숫자열거를_표시이름으로읽고_고객금액을보존한다(string mode)
    {
        using var fixture = new Fixture(mode, Body);
        var item = await fixture.ReadAsync();
        if (mode != "cancel")
        {
            Assert.NotNull(item);
            Assert.Equal(RequestId, item.의뢰Id);
            Assert.Equal("운송완료후정산", item.정산시점);
            Assert.Equal("없음", item.증빙방식);
            Assert.Equal("플랫폼", item.수납주체);
            Assert.Equal(18000m, item.최종운임);
            Assert.NotEqual(6200m, item.최종운임);
            Assert.Equal("합성 픽업", item.픽업지);
            Assert.Equal("R70 합성 상자", item.요약?.화물종류);
        }
        var snapshot = fixture.Observer.GetSnapshot(RequestId);
        Assert.NotNull(snapshot);
        Assert.Equal("입금대기", snapshot.SettlementStatus);
        var call = Assert.Single(fixture.Handler.Calls);
        Assert.Equal(mode == "cancel" ? HttpMethod.Post : HttpMethod.Get, call.Method);
        Assert.Equal("fixture-admin", call.Bearer);
        if (mode == "cancel") Assert.EndsWith("/admin/cancel-refund", call.Path);
        if (mode == "detail") Assert.Equal("/api/v1/shipper/requests/" + RequestId, call.Path);
        if (mode == "list") Assert.Equal("/api/v1/shipper/requests", call.Path);
    }

    [Theory]
    [InlineData("detail")]
    [InlineData("list")]
    public async Task 누락된선택열거는_정의된0값으로추정하지않고_미확인으로표시한다(string mode)
    {
        var body = Body.Replace("\"정산시점\":2", "\"정산시점\":null")
            .Replace("\"증빙방식\":0", "\"증빙방식\":null").Replace("\"수납주체\":0", "\"수납주체\":null");
        using var fixture = new Fixture(mode, body);
        var item = Assert.IsType<화주운송의뢰응답>(await fixture.ReadAsync());
        Assert.Equal("미확인", item.정산시점);
        Assert.Equal("미확인", item.증빙방식);
        Assert.Equal("미확인", item.수납주체);
        Assert.Equal(18000m, item.최종운임);
    }

    [Fact]
    public void 정의된0값과다른정의값은_숫자없이_원래이름으로표시한다()
    {
        var source = JsonSerializer.Deserialize<SharedRequest>(Body.Replace("\"정산시점\":2", "\"정산시점\":0"))!;
        var zero = AdminShipperRequestProjection.ToView(source);
        Assert.Equal("선결제", zero.정산시점);
        Assert.Equal("없음", zero.증빙방식);
        Assert.Equal("플랫폼", zero.수납주체);
        source.증빙방식 = Ssalddel.Contracts.Shipper.Request.증빙방식.현금영수증;
        source.수납주체 = Ssalddel.Contracts.Shipper.Request.수납주체.화주직접;
        var named = AdminShipperRequestProjection.ToView(source);
        Assert.Equal("현금영수증", named.증빙방식);
        Assert.Equal("화주직접", named.수납주체);
    }

    [Theory]
    [InlineData("detail", "정산시점", 2)]
    [InlineData("list", "정산시점", 2)]
    [InlineData("cancel", "정산시점", 2)]
    [InlineData("detail", "증빙방식", 0)]
    [InlineData("list", "증빙방식", 0)]
    [InlineData("cancel", "증빙방식", 0)]
    [InlineData("detail", "수납주체", 0)]
    [InlineData("list", "수납주체", 0)]
    [InlineData("cancel", "수납주체", 0)]
    public async Task 정의밖숫자는_표시성공으로대신하지않고_같은명령을재전송하지않는다(string mode, string property, int original)
    {
        var body = Body.Replace($"\"{property}\":{original}", $"\"{property}\":999");
        using var fixture = new Fixture(mode, body);
        var failure = await Assert.ThrowsAsync<JsonException>(async () => await fixture.ReadAsync());
        Assert.Equal("$." + property, failure.Path);
        Assert.Null(fixture.Observer.GetSnapshot(RequestId));
        Assert.Empty(fixture.RefreshRequests);
        var call = Assert.Single(fixture.Handler.Calls);
        Assert.Equal(mode == "cancel" ? HttpMethod.Post : HttpMethod.Get, call.Method);
    }

    [Fact]
    public async Task 향후알수없는열거문자열도_원래JSON계약실패로남긴다()
    {
        using var fixture = new Fixture("detail", Body.Replace("\"정산시점\":2", "\"정산시점\":\"미래정책\""));
        var failure = await Assert.ThrowsAsync<JsonException>(async () => await fixture.ReadAsync());
        Assert.Equal("$.정산시점", failure.Path);
        Assert.Null(fixture.Observer.GetSnapshot(RequestId));
        Assert.Single(fixture.Handler.Calls);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string mode;
        private readonly HttpClient http;
        public Handler Handler { get; }
        public TransportRequestLedgerObserver Observer { get; } = new();
        public List<TransportRequestLedgerRefreshRequest> RefreshRequests { get; } = [];
        private readonly 백오피스조회Service service;
        public Fixture(string mode, string response)
        {
            this.mode = mode; Handler = new(mode == "list" ? "[" + response + "]" : response);
            http = new(Handler) { BaseAddress = new Uri("https://admin-r70.example.invalid/") };
            var session = new 관리자인증세션Service(new ConfigurationBuilder().Build(), new EnvironmentStub());
            session.로그인적용(new 토큰응답 { AccessToken = "fixture-admin", UserId = "fixture-admin", Roles = ["서버관리자"] });
            Observer.RefreshRequested += RefreshRequests.Add;
            service = new(http, session, Observer);
        }
        public async Task<화주운송의뢰응답?> ReadAsync()
        {
            if (mode == "list") return Assert.Single(await service.의뢰목록조회Async());
            if (mode == "cancel")
            {
                await service.의뢰취소환불처리Async(RequestId, RequestId, "합성 시험");
                return null;
            }
            return await service.의뢰상세조회Async(RequestId);
        }
        public void Dispose() => http.Dispose();
    }
    private sealed class Handler(string response) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, string? Bearer)> Calls { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add((request.Method, request.RequestUri!.AbsolutePath, request.Headers.Authorization?.Parameter));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(response, Encoding.UTF8, "application/json") });
        }
    }
    private sealed class EnvironmentStub : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "AdminR70ContractTests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
