using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Ssalddel.Contracts.Admin.Management;
using SsalddelAdmin.Services;

namespace Ssalddel.Tests.Clients.Admin;

public sealed class ActualVehicleLoadingViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task 미등록실차는_자기선택차량만읽고_임의실측값이나유효기간을채우지않는다()
    {
        var client = new Client(); using var model = Create(client);
        await model.LoadAsync();
        Assert.Equal("미등록", model.StoredState); Assert.True(model.CanEdit); Assert.False(model.CanSave);
        Assert.Null(model.Draft.LengthMm); Assert.Null(model.Draft.MaxKg); Assert.Null(model.Draft.MaxCbm);
        Assert.Null(model.Draft.Pallets); Assert.Null(model.Draft.RainProtected);
        Assert.Equal("Unknown", model.Draft.EntranceKind);
        Assert.Empty(model.Draft.ConfirmedLocal); Assert.Empty(model.Draft.ValidUntilLocal);
    }

    [Fact]
    public async Task 저장은_현재선택실차와판본을보내고_정본재조회후에만확인한다()
    {
        var client = new Client { Response = Response(7) }; using var model = Create(client);
        await model.LoadAsync(); Fill(model);
        model.Draft.MaxKg = 150; model.Draft.MaxCbm = 0.0000001m; model.Draft.Pallets = 0;
        model.Draft.RainProtected = false; model.Draft.Refrigerated = null;
        await model.SaveAsync();
        var request = Assert.Single(client.Commands);
        Assert.Equal(7L, request.ExpectedRevision);
        Assert.Equal("vehicle:r77", request.제원.ActualVehicleReference);
        Assert.Equal("damas", request.제원.VehicleProfileValue);
        Assert.Equal(150, request.제원.최대적재중량Kg); Assert.Equal(0.0000001m, request.제원.최대부피Cbm);
        Assert.Equal(0, request.제원.팔레트적재개수); Assert.Equal(false, request.제원.비눈보호가능);
        Assert.Null(request.제원.냉장가능); Assert.Null(request.제원.적재함길이Mm);
        Assert.Equal(DateTimeKind.Utc, request.제원.ConfirmedAtUtc.Kind);
        Assert.Equal(new DateTime(2026, 10, 19, 11, 0, 0, DateTimeKind.Utc), request.제원.ConfirmedAtUtc);
        Assert.Equal(2, client.LoadCalls); Assert.Equal(8L, model.Current!.판본);
        Assert.False(model.NeedsResultCheck); Assert.Null(model.Error);
        Assert.Contains("최신 기록", model.Message);
    }

    [Fact]
    public async Task 동시변경은_입력보존후재조회전재저장을막고_비교후새판본으로저장한다()
    {
        var client = new Client { Response = Response(3), Save = (_, _) => throw new HttpRequestException("private server detail", null, HttpStatusCode.Conflict) };
        using var model = Create(client); await model.LoadAsync(); Fill(model); model.Draft.MaxKg = 91;
        await model.SaveAsync(); await model.SaveAsync();
        Assert.Single(client.Commands); Assert.Equal(1, client.LoadCalls); Assert.True(model.NeedsResultCheck);
        Assert.Equal(91, model.Draft.MaxKg); Assert.False(model.CanSave); Assert.DoesNotContain("private", model.Error);
        client.Response = Response(4, Record() with { 최대적재중량Kg = 92 });
        await model.LoadAsync();
        Assert.Equal(91, model.Draft.MaxKg); Assert.Equal(92, model.Current!.제원!.최대적재중량Kg);
        Assert.False(model.NeedsResultCheck); Assert.True(model.CanSave);
        client.Save = null; await model.SaveAsync();
        Assert.Equal(4L, client.Commands[1].ExpectedRevision); Assert.Equal(91, model.Current!.제원!.최대적재중량Kg);
    }

    [Fact]
    public async Task 응답유실은_자동PUT재전송없이_같은저장자료재조회로회복한다()
    {
        var client = new Client(); using var model = Create(client); await model.LoadAsync(); Fill(model);
        client.Save = (request, _) =>
        {
            client.Response = Response(1, request.제원 with { ReviewerId = "synthetic-admin" });
            throw new HttpRequestException("connection lost");
        };
        await model.SaveAsync();
        Assert.True(model.NeedsResultCheck); Assert.Single(client.Commands); Assert.Equal(1, client.LoadCalls);
        await model.LoadAsync();
        Assert.False(model.NeedsResultCheck); Assert.Null(model.Error); Assert.Single(client.Commands);
        Assert.Contains("일치", model.Message); Assert.Equal("evidence:r77", model.Draft.EvidenceReference);
    }

    [Fact]
    public async Task PUT성공이어도_재조회실패이면_성공표시하지않고_재저장을막는다()
    {
        var client = new Client(); using var model = Create(client); await model.LoadAsync(); Fill(model);
        client.Load = (_, _) => throw new HttpRequestException("network detail");
        await model.SaveAsync();
        Assert.True(model.NeedsResultCheck); Assert.False(model.CanSave); Assert.Null(model.Message);
        Assert.Equal("evidence:r77", model.Draft.EvidenceReference); Assert.Contains("최신 기록", model.Error);
    }

    [Fact]
    public async Task 다른현재차량으로바뀌면_이전차량작성값을새차량에복사하지않는다()
    {
        var client = new Client { Save = (_, _) => throw new HttpRequestException("conflict", null, HttpStatusCode.Conflict) };
        using var model = Create(client); await model.LoadAsync(); Fill(model); model.Draft.MaxKg = 91; await model.SaveAsync();
        client.Response = Response(1, Record()) with { 현재실차참조 = "vehicle:new", 현재차량종류 = "1ton" };
        await model.LoadAsync();
        Assert.Empty(model.Draft.EvidenceReference); Assert.Null(model.Draft.MaxKg); Assert.False(model.CanSave);
        Assert.Equal("이전 차량 기록", model.StoredState); Assert.Contains("이전 입력", model.Message);
    }

    [Fact]
    public async Task 구판응답에현재선택정보가없으면_과거기록으로추정하지않는다()
    {
        var client = new Client { Response = new("driver:r77", 1, Record()) };
        using var model = Create(client); await model.LoadAsync(); Fill(model);
        Assert.False(model.CurrentVehicleSelected); Assert.False(model.CanSave);
        await model.SaveAsync(); Assert.Empty(client.Commands);
    }

    [Fact]
    public async Task 다른기사응답과이전판본응답은_현재자료에덮어쓰지않는다()
    {
        var client = new Client { Response = Response(5) }; using var model = Create(client); await model.LoadAsync();
        client.Response = new("other-driver", 6, Record()); await model.LoadAsync();
        Assert.Equal("driver:r77", model.Current!.기사Id); Assert.Equal(5L, model.Current.판본); Assert.NotNull(model.Error);
        client.Response = Response(4, Record()); await model.LoadAsync(); Assert.Equal(5L, model.Current!.판본);
    }

    [Fact]
    public async Task 세션변경과해제뒤늦은조회결과는_노출하거나저장하지않는다()
    {
        var pending = new TaskCompletionSource<기사실차적재제원응답>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Client { Load = (_, _) => pending.Task }; var session = "admin:first";
        using var model = new 기사실차적재확인ViewModel(client, "driver:r77", () => session, () => true, () => Now);
        var load = model.LoadAsync(); session = "admin:second"; pending.SetResult(Response(9, Record())); await load;
        Assert.Null(model.Current); Assert.False(model.CanSave); Assert.False(model.SessionCurrent);
        model.Dispose(); Assert.Empty(model.Draft.EvidenceReference); await model.SaveAsync(); Assert.Empty(client.Commands);
    }

    [Fact]
    public async Task 저장중세션변경은_늦은PUT응답뒤추가조회를하지않는다()
    {
        var pending = new TaskCompletionSource<기사실차적재제원응답>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Client { Save = (_, _) => pending.Task }; var session = "first";
        using var model = new 기사실차적재확인ViewModel(client, "driver:r77", () => session, () => true, () => Now);
        await model.LoadAsync(); Fill(model); var saving = model.SaveAsync(); session = "second";
        pending.SetResult(Response(1, client.Commands.Single().제원)); await saving;
        Assert.Null(model.Current); Assert.Equal(1, client.LoadCalls); Assert.False(model.CanSave);
    }

    [Fact]
    public async Task 권한거부는_이전실측입력과기록을숨긴다()
    {
        var client = new Client { Response = Response(1, Record()) }; using var model = Create(client); await model.LoadAsync();
        client.Load = (_, _) => throw new HttpRequestException("private detail", null, HttpStatusCode.Forbidden);
        await model.LoadAsync();
        Assert.Null(model.Current); Assert.Empty(model.Draft.EvidenceReference); Assert.False(model.CanSave);
        Assert.Contains("관리자 권한", model.Error); Assert.DoesNotContain("private", model.Error);
    }

    [Fact]
    public async Task 불가와미확인은보존하고_양수가아닌한도와근거없는특수장비는거절한다()
    {
        var client = new Client(); using var model = Create(client); await model.LoadAsync(); Fill(model);
        model.Draft.MaxKg = 0; Assert.False(model.CanSave); await model.SaveAsync(); Assert.Empty(client.Commands);
        model.Draft.MaxKg = null; model.Draft.Pallets = -1; Assert.False(model.CanSave);
        model.Draft.Pallets = 0; model.Draft.Lift = true; Assert.False(model.CanSave);
        model.Draft.EquipmentReferences = "equipment:lift"; Assert.True(model.CanSave);
        model.Draft.ValidUntilLocal = "2026-10-19T20:00"; Assert.False(model.CanSave);
    }

    [Fact]
    public async Task API는_관리자Bearer와기사경로_기존PUT판본을전달한다()
    {
        기사실차적재제원수정요청? written = null; var methods = new List<HttpMethod>();
        using var handler = new Handler(async request =>
        {
            methods.Add(request.Method);
            Assert.Equal("/api/v1/admin/vehicle-recommendations/drivers/driver%20r77/actual-loading", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme); Assert.Equal("synthetic-token", request.Headers.Authorization.Parameter);
            if (request.Method == HttpMethod.Put) written = await request.Content!.ReadFromJsonAsync<기사실차적재제원수정요청>();
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(Response(3, Record()) with { 기사Id = "driver r77" }) };
        });
        var service = Service(handler, true); await service.실차조회Async("driver r77");
        await service.실차수정Async("driver r77", new(2, Record()));
        Assert.Equal(new[] { HttpMethod.Get, HttpMethod.Put }, methods); Assert.Equal(2L, written!.ExpectedRevision);
    }

    [Theory]
    [InlineData(401)] [InlineData(409)] [InlineData(503)]
    public async Task API실패를빈실차나성공으로바꾸지않는다(int code)
    {
        using var handler = new Handler(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)code)));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Service(handler, true).실차수정Async("driver:r77", new(1, Record())));
        Assert.Equal((HttpStatusCode)code, error.StatusCode);
    }

    [Fact]
    public async Task 비관리자는요청하지않고_null응답도성공으로바꾸지않는다()
    {
        var calls = 0;
        using var handler = new Handler(_ => { calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create<기사실차적재제원응답?>(null) }); });
        await Assert.ThrowsAsync<HttpRequestException>(() => Service(handler, false).실차조회Async("driver:r77")); Assert.Equal(0, calls);
        await Assert.ThrowsAsync<InvalidDataException>(() => Service(handler, true).실차조회Async("driver:r77")); Assert.Equal(1, calls);
    }

    private static 기사실차적재확인ViewModel Create(Client client) => new(client, "driver:r77", now: () => Now);
    private static void Fill(기사실차적재확인ViewModel model)
    {
        model.Draft.EvidenceReference = "evidence:r77"; model.Draft.ConfirmedLocal = "2026-10-19T20:00:00";
        model.Draft.ValidUntilLocal = "2026-10-20T21:00:00";
    }
    private static 기사실차적재제원 Record() => new()
    {
        ActualVehicleReference = "vehicle:r77", VehicleProfileValue = "damas", PrivateEvidenceReference = "evidence:r77",
        ConfirmedAtUtc = new(2026, 10, 19, 11, 0, 0, DateTimeKind.Utc), ValidUntilUtc = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc)
    };
    private static 기사실차적재제원응답 Response(long? revision = null, 기사실차적재제원? value = null)
        => new("driver:r77", revision, value) { 현재실차참조 = "vehicle:r77", 현재차량종류 = "damas" };
    private sealed class Client : I기사실차적재확인Client
    {
        public 기사실차적재제원응답 Response = ActualVehicleLoadingViewModelTests.Response();
        public Func<int, CancellationToken, Task<기사실차적재제원응답>>? Load;
        public Func<기사실차적재제원수정요청, CancellationToken, Task<기사실차적재제원응답>>? Save;
        public int LoadCalls; public List<기사실차적재제원수정요청> Commands = [];
        public Task<기사실차적재제원응답> 실차조회Async(string id, CancellationToken ct = default)
        { LoadCalls++; return Load is null ? Task.FromResult(Response) : Load(LoadCalls, ct); }
        public Task<기사실차적재제원응답> 실차수정Async(string id, 기사실차적재제원수정요청 value, CancellationToken ct = default)
        {
            Commands.Add(value); if (Save is not null) return Save(value, ct);
            Response = Response with { 판본 = (value.ExpectedRevision ?? 0) + 1, 제원 = value.제원 with { ReviewerId = "synthetic-admin" } };
            return Task.FromResult(Response);
        }
    }
    private static 차량관리Service Service(HttpMessageHandler handler, bool admin)
    {
        var session = new 관리자인증세션Service(new ConfigurationBuilder().Build(), new EnvironmentStub());
        session.로그인적용(new() { AccessToken = "synthetic-token", UserId = "synthetic-admin", Roles = admin ? ["서버관리자"] : ["기사"] });
        return new(new HttpClient(handler) { BaseAddress = new("https://example.invalid/") }, session);
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => respond(request); }
    private sealed class EnvironmentStub : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Tests";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
