using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Admin.Management;
using Ssalddel.Ui.Common.Areas.App.Components;
using SsalddelAdmin.Components.Shared;
using SsalddelAdmin.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class ActualVehicleLoadingPanelRenderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task 미등록은_확인전빈칸과세가지장비선택을보이고_자격승인을구분한다()
    {
        using var model = Model(new Client()); await model.LoadAsync(); var html = await Render(model);
        Assert.Contains("현재 차량과 저장 기록", html); Assert.Contains("기사 본인 선택 차종", html);
        Assert.Contains("vehicle:r77", html); Assert.Contains("미등록", html);
        Assert.Contains("확인하지 않은 값은 비워", html); Assert.Contains("운송 자격과 별개", html);
        Assert.Contains("실측 기록 저장", html); Assert.Contains("disabled", html);
        // SSR은 빈 value 속성을 축약할 수 있다. 직렬화 모양 대신 실제 선택지 의미를 검사한다.
        var selects = Regex.Matches(html, "(?s)<select\\b[^>]*>(.*?)</select>");
        Assert.Equal(7, selects.Count);
        Assert.All(selects.Cast<Match>(), select =>
        {
            Assert.Equal(3, Regex.Matches(select.Groups[1].Value, "<option\\b").Count);
            Assert.Contains(">미확인</option>", select.Value);
        });
        Assert.Contains(">개방형</option>", selects[0].Value); Assert.Contains(">밀폐형</option>", selects[0].Value);
        foreach (var capability in selects.Cast<Match>().Skip(1))
        { Assert.Contains(">가능</option>", capability.Value); Assert.Contains(">불가</option>", capability.Value); }
        Assert.DoesNotContain("value=\"20\"", html); Assert.Contains("팔레트 0개는 적재 불가", html);
    }

    [Fact]
    public async Task 만료기록은_유효하다고승격하지않고_현재보관값과편집항목을함께보인다()
    {
        var record = Record() with { ConfirmedAtUtc = Now.AddDays(-1).UtcDateTime, ValidUntilUtc = Now.AddHours(-1).UtcDateTime,
            최대적재중량Kg = 80, 팔레트적재개수 = 0, 비눈보호가능 = false };
        using var model = Model(new Client { Response = Response(record) }); await model.LoadAsync();
        model.Draft.MaxKg = 70;
        var html = await Render(model);
        Assert.Contains("유효기간 만료", html); Assert.Contains("현재 저장된 실측값", html);
        Assert.Contains("80 kg", html); Assert.Contains("value=\"70\"", html);
        Assert.Contains("evidence:private-r77", html); Assert.False(model.CanSave);
        Assert.DoesNotContain("자격 승인 완료", html);
    }

    [Fact]
    public async Task 저장응답유실은_보존입력과재조회안내를보이고_성공표시하지않는다()
    {
        var client = new Client { SaveFails = true }; using var model = Model(client); await model.LoadAsync();
        Fill(model); await model.SaveAsync(); var html = await Render(model);
        Assert.Contains("다시 저장하지 말고 최신 기록", html); Assert.Contains("작성값을 보존", html);
        Assert.Contains("evidence:private-r77", html); Assert.DoesNotContain("RAW_SERVER_SECRET", html);
        Assert.DoesNotContain("저장된 실측 자료를 최신 기록에서 확인했습니다", html);
        Assert.Matches("(?s)<button[^>]*disabled[^>]*>.*?실측 기록 저장", html);
    }

    [Fact]
    public async Task 현재선택정보누락이면_과거실차를자동선택하지않고_입력하지못한다()
    {
        using var model = Model(new Client { Response = new("driver:r77", 1, Record()) });
        await model.LoadAsync(); var html = await Render(model);
        Assert.Contains("현재 사용할 차량을 먼저 선택", html); Assert.DoesNotContain("실측 기록 저장", html);
    }

    [Fact]
    public async Task 계정변경후에는_이전기사의비공개참조와입력을렌더하지않는다()
    {
        var owner = "first"; using var model = new 기사실차적재확인ViewModel(new Client { Response = Response(Record()) },
            "driver:r77", () => owner, () => true, () => Now);
        await model.LoadAsync(); owner = "second"; var html = await Render(model);
        Assert.Contains("관리자 계정이 바뀌었습니다", html); Assert.DoesNotContain("evidence:private-r77", html);
        Assert.DoesNotContain("vehicle:r77", html); Assert.DoesNotContain("실측 기록 저장", html);
    }

    [Fact]
    public async Task 조회실패는_재시도만보이고_실측입력이나성공자료를가정하지않는다()
    {
        using var model = Model(new Client { LoadFails = true }); await model.LoadAsync(); var html = await Render(model);
        Assert.Contains("최신 기록 확인", html); Assert.Contains("실차 기록을 확인하지 못했습니다", html);
        Assert.DoesNotContain("RAW_SERVER_SECRET", html); Assert.DoesNotContain("실측 기록 저장", html);
    }

    private static 기사실차적재확인ViewModel Model(Client client) => new(client, "driver:r77", now: () => Now);
    private static void Fill(기사실차적재확인ViewModel model)
    {
        model.Draft.EvidenceReference = "evidence:private-r77";
        model.Draft.ConfirmedLocal = "2026-10-19T20:00:00"; model.Draft.ValidUntilLocal = "2026-10-20T21:00:00";
    }
    private static 기사실차적재제원 Record() => new()
    {
        ActualVehicleReference = "vehicle:r77", VehicleProfileValue = "damas", PrivateEvidenceReference = "evidence:private-r77",
        ConfirmedAtUtc = Now.AddHours(-1).UtcDateTime, ValidUntilUtc = Now.AddDays(1).UtcDateTime
    };
    private static 기사실차적재제원응답 Response(기사실차적재제원? record = null)
        => new("driver:r77", record is null ? null : 2, record) { 현재실차참조 = "vehicle:r77", 현재차량종류 = "damas" };
    private sealed class Client : I기사실차적재확인Client
    {
        public 기사실차적재제원응답 Response = ActualVehicleLoadingPanelRenderTests.Response();
        public bool LoadFails; public bool SaveFails;
        public Task<기사실차적재제원응답> 실차조회Async(string id, CancellationToken ct = default)
            => LoadFails ? throw new HttpRequestException("RAW_SERVER_SECRET") : Task.FromResult(Response);
        public Task<기사실차적재제원응답> 실차수정Async(string id, 기사실차적재제원수정요청 request, CancellationToken ct = default)
            => SaveFails ? throw new HttpRequestException("RAW_SERVER_SECRET") : throw new InvalidOperationException("이 렌더 시험은 저장하지 않습니다.");
    }
    private static async Task<string> Render(기사실차적재확인ViewModel model)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddMudServices();
        services.AddSingleton<IJSRuntime, NoopJs>(); services.AddSingleton<NavigationManager, TestNavigation>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RoleAppProviders>();
            var view = await renderer.RenderComponentAsync<ActualVehicleLoadingPanel>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(ActualVehicleLoadingPanel.Model)] = model }));
            return WebUtility.HtmlDecode(view.ToHtmlString());
        });
    }
    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => ValueTask.FromResult(default(T)!);
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken ct, object?[]? args) => ValueTask.FromResult(default(T)!);
    }
    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/vehicle-management?driverId=driver:r77");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
