using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Ui.Common.Areas.App.Components.Transport;

namespace Ssalddel.Tests.Ui.Common;

public sealed class ShipperLocalDateTimeFieldTests
{
    [Theory]
    [InlineData("2026-10-20T10:00")]
    [InlineData("2026-10-20T10:00:23")]
    [InlineData("2026-10-20T10:00:23.1")]
    [InlineData("2026-10-20T10:00:23.1234567")]
    public void 브라우저_분초소수초를_한국현지시각으로_엄격히읽고_왕복한다(string input)
    {
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ko-KR");
            Assert.True(ShipperLocalDateTimeField.TryParseLocalInput(input, out var value));
            Assert.NotNull(value);
            Assert.Equal(DateTimeKind.Unspecified, value.Value.Kind);
            Assert.Equal(input, ShipperLocalDateTimeField.FormatLocalInput(value));
        }
        finally { CultureInfo.CurrentCulture = prior; }
    }

    [Theory]
    [InlineData("2026-02-30T10:00")]
    [InlineData("2026-10-20 10:00")]
    [InlineData("2026-10-20T10:00Z")]
    [InlineData("2026-10-20T10:00+09:00")]
    [InlineData("2026-10-20T24:00")]
    [InlineData("not-a-date")]
    public void 잘못된날짜나시차가붙은시각은_이전값을유효하게남기지않는다(string input)
    {
        Assert.False(ShipperLocalDateTimeField.TryParseLocalInput(input, out var value));
        Assert.Null(value);
    }

    [Fact]
    public void 빈입력은_nullable로유지하고_시차변환을암묵적으로하지않는다()
    {
        Assert.True(ShipperLocalDateTimeField.TryParseLocalInput(null, out var missing));
        Assert.Null(missing);
        Assert.True(ShipperLocalDateTimeField.TryParseLocalInput(string.Empty, out missing));
        Assert.Null(missing);
        Assert.Equal(string.Empty, ShipperLocalDateTimeField.FormatLocalInput(null));
        Assert.Equal("2026-10-20T10:00", ShipperLocalDateTimeField.FormatLocalInput(
            new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task 날짜필드는_외부값변경과초기화를반영하고_한국어검증문구를유지한다()
    {
        var services = new ServiceCollection();
        services.AddLogging(); services.AddMudServices(); services.AddSingleton<IJSRuntime, NoopJs>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            FieldHarness? harness = null;
            var view = await renderer.RenderComponentAsync<FieldHarness>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(FieldHarness.Capture)] = (Action<FieldHarness>)(value => harness = value) }));
            Assert.NotNull(harness);
            Assert.Contains("type=\"datetime-local\"", view.ToHtmlString());
            Assert.Contains("step=\"any\"", view.ToHtmlString());
            await harness.UpdateAsync(new DateTime(2026, 10, 20, 10, 0, 23).AddTicks(1234567));
            Assert.Contains("2026-10-20T10:00:23.1234567", view.ToHtmlString());
            await harness.UpdateAsync(new DateTime(2026, 10, 21, 11, 30, 0));
            Assert.Contains("2026-10-21T11:30", view.ToHtmlString());
            Assert.DoesNotContain("2026-10-20T10:00", view.ToHtmlString());
            await harness.UpdateAsync(null);
            Assert.DoesNotContain("2026-10-21T11:30", view.ToHtmlString());
            Assert.Contains("시간을 다시 확인해 주세요.", WebUtility.HtmlDecode(view.ToHtmlString()));
            Assert.Contains("role=\"alert\"", view.ToHtmlString());
            Assert.DoesNotContain("Not a valid date time", view.ToHtmlString());
        });
    }

    public sealed class FieldHarness : ComponentBase
    {
        [Parameter] public Action<FieldHarness>? Capture { get; set; }
        private DateTime? value;
        protected override void OnInitialized() => Capture?.Invoke(this);
        public Task UpdateAsync(DateTime? input) => InvokeAsync(() => { value = input; StateHasChanged(); });
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ShipperLocalDateTimeField>(0);
            builder.AddAttribute(1, nameof(ShipperLocalDateTimeField.Value), value);
            builder.AddAttribute(2, nameof(ShipperLocalDateTimeField.Label), "상차 시작 (한국시간)");
            builder.AddAttribute(3, nameof(ShipperLocalDateTimeField.Error), true);
            builder.AddAttribute(4, nameof(ShipperLocalDateTimeField.ErrorText), "시간을 다시 확인해 주세요.");
            builder.CloseComponent();
        }
    }
    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => ValueTask.FromResult(default(T)!);
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(T)!);
    }
}
