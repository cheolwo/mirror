using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 분산입고협의RenderTests
{
    [Fact]
    public async Task 공간소유자의수용조건은_농산물거부와공산품허용을_별개로표시한다()
    {
        var transport = new 분산입고협의TestTransport();
        using var model = await Model(transport, "sites", "site-a");
        var html = await Render<분산공간수용조건Panel>(new() { ["Model"] = model, ["Site"] = transport.Workspace.Sites[0] });
        Assert.Contains("허용 품목", html); Assert.Contains("공산품", html); Assert.Contains("거부 품목", html); Assert.Contains("농산물", html);
        Assert.Contains("10 상자", html); Assert.Contains("받지 않음", html); Assert.Contains("수용 조건 설정", html);
        Assert.DoesNotContain("site-a</", html); Assert.Empty(transport.Posts);
    }

    [Fact]
    public async Task 현장관리자는_공간소유자의조건수정버튼을보지않는다()
    {
        var transport = new 분산입고협의TestTransport(); transport.Workspace.Sites[0].AllowedActions = [];
        using var model = await Model(transport, "sites", "site-a");
        var html = await Render<분산공간수용조건Panel>(new() { ["Model"] = model, ["Site"] = transport.Workspace.Sites[0] });
        Assert.Contains("받을 수 있는 물품", html); Assert.DoesNotContain("수용 조건 설정", html); Assert.Empty(transport.Posts);
    }

    [Fact]
    public async Task 승인전입고는_대기이유와다음담당을보이고_사진완료버튼이없다()
    {
        var transport = new 분산입고협의TestTransport();
        using var model = await Model(transport, "inbound", "plan-a");
        var html = await Render<분산입고Screen>(new() { ["Model"] = model });
        Assert.Contains("이번 입고 협의", html); Assert.Contains("양측 동의 대기", html); Assert.Contains("다음 담당 · 공간 제공자", html);
        Assert.Contains("물품 소유자와 공간 제공자의 입고 동의를 기다립니다", html);
        Assert.DoesNotContain("사진 보내고 입고·적재 완료", html); Assert.DoesNotContain("DistributedInboundApprovalRequired", html);
        Assert.DoesNotContain("supplier-private</", html); Assert.Empty(transport.Posts);
    }

    [Fact]
    public async Task 현장관리자의조회에서숨겨진비용이나협의메모는_빈항목으로노출하지않는다()
    {
        var transport = new 분산입고협의TestTransport(); var collaboration = transport.Workspace.InboundPlans[0].Lines[0].InboundCollaboration!;
        collaboration.Terms.SupplyPriceOrFreeTerms = collaboration.Terms.SupplierResponsibilityTerms = collaboration.Terms.ReturnTerms = "";
        collaboration.History = [];
        transport.SetLine(transport.Line with { AllowedActions = [] });
        using var model = await Model(transport, "inbound", "plan-a");
        var html = await Render<분산입고협의Panel>(new() { ["Model"] = model, ["Plan"] = model.SelectedInbound });
        Assert.Contains("받을 수량", html); Assert.DoesNotContain("공급·무상 조건", html); Assert.DoesNotContain("협의 메모 남기기", html);
        Assert.DoesNotContain("협의 기록", html); Assert.DoesNotContain("수정 조건 제안", html);
    }

    [Fact]
    public async Task 확정예약이어도_실제입고와_협의메모와_동의는_각각구분한다()
    {
        var transport = new 분산입고협의TestTransport(); transport.Line.InboundCollaboration!.StatusCode = "Confirmed";
        transport.Line.InboundCollaboration.GoodsOwnerConsented = transport.Line.InboundCollaboration.SpaceOwnerConsented = true;
        transport.SetLine(transport.Line with { AllowedActions = ["note-inbound", "receive"], WaitingReasonCodes = [] });
        using var model = await Model(transport, "inbound", "plan-a");
        var html = await Render<분산입고Screen>(new() { ["Model"] = model });
        Assert.Contains("입고 예약 승인", html); Assert.Contains("실제 입고 0 개", html); Assert.Contains("별도입니다", html);
        Assert.Contains("사진 보내고 입고·적재 완료", html); Assert.Contains("협의 메모는 동의를 대신하지 않습니다", html);
        Assert.DoesNotContain("이번 입고 조건에 동의", html); Assert.Empty(transport.Posts);
    }

    [Fact]
    public async Task 다른상품의승인으로_미승인상품의사진입고양식을열지않는다()
    {
        var transport = new 분산입고협의TestTransport(); var blocked = transport.Line;
        var ready = blocked with { LineKey = "line-b", Sku = "ready-sku", AllowedActions = ["receive"], WaitingReasonCodes = [] };
        transport.Workspace.InboundPlans = [transport.Workspace.InboundPlans[0] with { Lines = [blocked, ready], AllowedActions = ["receive"] }];
        using var model = await Model(transport, "inbound", "plan-a");
        var html = await Render<분산입고Screen>(new() { ["Model"] = model });
        Assert.Contains("남은 3 개", html); Assert.DoesNotContain("남은 6 개", html);
        Assert.Contains("사진 보내고 입고·적재 완료", html);
    }

    [Fact]
    public async Task 변경협의중에도_이전예약용량과_미승인변경안을구분한다()
    {
        var transport = new 분산입고협의TestTransport(); var c = transport.Line.InboundCollaboration!;
        c.TermsVersion = 2; c.ReservedTermsVersion = 1; c.ReservedTerms = new() { Quantity = 2 };
        using var model = await Model(transport, "inbound", "plan-a");
        var html = await Render<분산입고협의Panel>(new() { ["Model"] = model, ["Plan"] = model.SelectedInbound });
        Assert.Contains("이전 1 차에서 승인한 2 개의 공간은 확보 중", html);
        Assert.Contains("변경안의 승인 전에는 실제 입고를 진행할 수 없습니다", html);
        Assert.DoesNotContain("입고 예약 승인", html);
    }

    [Fact]
    public async Task 유효계약과미확정변경안은_별도로표시하고_변경안에만동의한다()
    {
        var transport = new 분산입고협의TestTransport();
        transport.Workspace.StorageAgreements = [transport.Workspace.StorageAgreements[0] with { AllowedActions = ["revoke", "consent-pending-terms"],
            PendingTermsVersion = 2, PendingTerms = new() { MaximumStoredQuantity = 8, AllowedSkus = ["next-sku"], PriceOrFreeTerms = "변경 비용 조건" } }];
        using var model = await Model(transport, "agreements", "agreement-a");
        var html = await Render<분산보관합의Screen>(new() { ["Model"] = model });
        Assert.Contains("현재 보관 조건은 유지", html); Assert.Contains("보관 변경안 · 2 차", html); Assert.Contains("next-sku", html);
        Assert.Contains("이 변경안에 동의", html); Assert.Contains("본인 보관 동의 철회", html);
        Assert.DoesNotContain(">이 보관 조건에 동의<", html); Assert.Empty(transport.Posts);
    }

    [Theory]
    [InlineData("CapacityExceeded", "수용량이 부족")]
    [InlineData("GoodsCategoryDenied", "받지 않는 품목")]
    [InlineData("TemperatureUnknown", "보관 온도")]
    [InlineData("ExistingCapacityUnknown", "기존 보관 물품")]
    [InlineData("DistributedInboundPolicyChanged", "수용 조건이 바뀌었습니다")]
    [InlineData("DistributedInboundArrivalWindowClosed", "도착 시간이 아닙니다")]
    [InlineData("DistributedInboundAgreementVersionMismatch", "보관 계약의 판본이 바뀌었습니다")]
    public async Task 입고대기이유는_일상적인말로표시한다(string reason, string expected)
    {
        var transport = new 분산입고협의TestTransport(); transport.SetLine(transport.Line with { WaitingReasonCodes = [reason] });
        using var model = await Model(transport, "inbound", "plan-a");
        var html = await Render<분산입고협의Panel>(new() { ["Model"] = model, ["Plan"] = model.SelectedInbound });
        Assert.Contains(expected, html); Assert.DoesNotContain(reason, html);
    }

    [Fact]
    public async Task 명시적null분류가조회되면_미확인으로표시하고_수정양식도열수있다()
    {
        var transport = new 분산입고협의TestTransport();
        transport.Line.InboundCollaboration!.Terms.Goods.CategoryCodes = null!;
        using var model = await Model(transport, "inbound", "plan-a");
        var html = await Render<분산입고협의Panel>(new() { ["Model"] = model, ["Plan"] = model.SelectedInbound });
        Assert.Contains("분류 미확인", html);
        var services = new ServiceCollection().AddLogging(); await using var provider = services.BuildServiceProvider();
        using var renderer = new 분산입고협의InteractionRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.Start<분산입고협의Panel>(new() { ["Model"] = model, ["Plan"] = model.SelectedInbound });
            await renderer.Click("수정 조건 제안");
            Assert.Contains("확인한 물품 분류", renderer.Text());
        });
        Assert.Empty(transport.Posts); Assert.Null(model.Error);
    }

    [Fact]
    public async Task 실제제안버튼은_공급자선택과_확인한조건을_UTC로보낸다()
    {
        var transport = new 분산입고협의TestTransport { ReceiptKind = "InboundProposal" };
        using var model = await Model(transport, "inbound", "plan-a");
        var services = new ServiceCollection().AddLogging(); await using var provider = services.BuildServiceProvider();
        using var renderer = new 분산입고협의InteractionRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.Start<분산입고협의Panel>(new() { ["Model"] = model, ["Plan"] = model.SelectedInbound });
            await renderer.Click("수정 조건 제안");
            await renderer.Change("이번 공급 담당", "owner-private");
            await renderer.Change("공간의 출입 가능 시간과 이번 도착 시간", true);
            await renderer.Change("위 상품 특성과 공급·보관 조건", true);
            await renderer.Click("입고 조건 보내기");
        });
        var request = Assert.IsType<DistributedInboundProposalRequest>(Assert.Single(transport.Posts));
        Assert.Equal("owner-private", request.Terms.SupplierUserId); Assert.Equal("supplier-private", request.Terms.SellerUserId);
        Assert.Equal(1, request.Terms.AgreementTermsVersion); Assert.Equal(DateTimeKind.Utc, request.Terms.ArrivalStartUtc.Kind);
        Assert.Equal(new DateTime(2026,10,12,1,0,0,DateTimeKind.Utc), request.Terms.ArrivalStartUtc);
        Assert.True(request.Terms.AccessHoursConfirmed); Assert.NotEqual(Guid.Empty, request.RequestId); Assert.Null(model.Error);
    }

    [Fact]
    public async Task 부분입고뒤_남은물량을포기하는제안은_받은수량과_고정된물품공급관계를보낸다()
    {
        var transport = new 분산입고협의TestTransport { ReceiptKind = "InboundProposal" };
        transport.SetLine(transport.Line with { ReceivedQuantity = 2 });
        var original = transport.Line.InboundCollaboration!.Terms;
        var goods = System.Text.Json.JsonSerializer.Serialize(original.Goods);
        using var model = await Model(transport, "inbound", "plan-a");
        var services = new ServiceCollection().AddLogging(); await using var provider = services.BuildServiceProvider();
        using var renderer = new 분산입고협의InteractionRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.Start<분산입고협의Panel>(new() { ["Model"] = model, ["Plan"] = model.SelectedInbound });
            await renderer.Click("수정 조건 제안");
            Assert.True(renderer.DisabledFieldset("물품과 공급 관계"));
            await renderer.Change("이번 총 예정 수량", "2");
            await renderer.Change("공간의 출입 가능 시간과 이번 도착 시간", true);
            await renderer.Change("위 상품 특성과 공급·보관 조건", true);
            await renderer.Click("입고 조건 보내기");
        });
        var request = Assert.IsType<DistributedInboundProposalRequest>(Assert.Single(transport.Posts));
        Assert.Equal(2, request.Terms.Quantity); Assert.Equal(2, transport.Line.ReceivedQuantity);
        Assert.Equal(goods, System.Text.Json.JsonSerializer.Serialize(request.Terms.Goods));
        Assert.Equal(original.SupplierUserId, request.Terms.SupplierUserId); Assert.Equal(original.SupplierTypeCode, request.Terms.SupplierTypeCode);
        Assert.Equal(original.GoodsOwnerUserId, request.Terms.GoodsOwnerUserId); Assert.Equal(original.SellerUserId, request.Terms.SellerUserId);
        Assert.Null(model.ValidationError); Assert.Null(model.Error);
    }

    [Fact]
    public async Task 미선택식품여부를_기본0이나수용동의로보내지않는다()
    {
        var transport = new 분산입고협의TestTransport(); transport.Workspace.Sites[0].AcceptancePolicy = null;
        using var model = await Model(transport, "sites", "site-a");
        var services = new ServiceCollection().AddLogging(); await using var provider = services.BuildServiceProvider();
        using var renderer = new 분산입고협의InteractionRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.Start<분산공간수용조건Panel>(new() { ["Model"] = model, ["Site"] = transport.Workspace.Sites[0] });
            await renderer.Click("수용 조건 설정");
            await renderer.Change("공산품", true); await renderer.Change("상자 포장", true); await renderer.Change("상온", true);
            await renderer.Change("공간 전체 용량 단위", "상자"); await renderer.Change("공간 전체 수용량", "10"); await renderer.Change("출입 가능 시간", "평일 10~18시");
            await renderer.Change("공간 제공자로서 위 조건", true); await renderer.Click("수용 조건 저장");
        });
        Assert.Empty(transport.Posts); Assert.Contains("식품 수용 여부", model.ValidationError);
    }

    [Fact]
    public async Task 초안보관합의도_협의에선선택하고_승인전동의필요를안내한다()
    {
        var transport = new 분산입고협의TestTransport(); transport.Workspace.StorageAgreements = [transport.Workspace.StorageAgreements[0] with { StatusCode = "Draft" }];
        using var model = await Model(transport, "inbound", "plan-a");
        var services = new ServiceCollection().AddLogging(); await using var provider = services.BuildServiceProvider();
        using var renderer = new 분산입고협의InteractionRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.Start<분산입고협의Panel>(new() { ["Model"] = model, ["Plan"] = model.SelectedInbound });
            await renderer.Click("수정 조건 제안");
            Assert.Contains("동의 대기", renderer.Text()); Assert.Contains("실제 입고 승인은 보관 합의 동의 뒤 가능합니다", renderer.Text());
        });
        Assert.Empty(transport.Posts);
    }

    private static async Task<분산이행PageViewModel> Model(분산입고협의TestTransport transport, string screen, string id)
    { var model = new 분산이행PageViewModel(new(transport, "warehouse"), screen); await model.LoadAsync(id); Assert.Null(model.Error); return model; }
    internal static async Task<string> Render<T>(Dictionary<string, object?> parameters, [CallerMemberName] string example = "example") where T : IComponent
    {
        var services = new ServiceCollection().AddLogging(); await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var rendered = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString());
        // 실제 Razor 출력을 보존합니다. 브라우저/휴대폰 실행 또는 실제 계약의 증거로 승격하지 않습니다.
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "Ssalddel.Ui.Common"))) root = root.Parent;
        if (root is not null)
        {
            var folder = Path.Combine(root.FullName, "artifacts", "local", "inbound-r64"); Directory.CreateDirectory(folder);
            var uiFolder = Path.Combine(root.FullName, "Ssalddel.Ui.Common", "Areas", "App", "RoleWorkspace", "DistributedFulfillment");
            var foundation = File.ReadAllText(Path.Combine(root.FullName, "Ssalddel.Ui.Common", "wwwroot", "Areas", "App", "css", "role-app-foundation.css"));
            var css = foundation + string.Join('\n', new[] { "분산이행Screen", "분산공간Screen", "분산입고Screen", "분산보관합의Screen", "분산공간수용조건Panel", "분산입고협의Panel", "분산보관변경협의Panel" }
                .Select(name => Path.Combine(uiFolder, name + ".razor.css")).Where(File.Exists).Select(File.ReadAllText)).Replace(" ::deep", "");
            File.WriteAllText(Path.Combine(folder, "ui-" + example + ".html"), "<!doctype html><html lang=\"ko\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>입고 협의 로컬 화면</title><style>body{margin:0;font-family:Arial,sans-serif;font-size:16px}" + css + "</style><main class=\"distributed-work\">" + rendered + "</main></html>");
        }
        return WebUtility.HtmlDecode(rendered);
    }
}

#pragma warning disable BL0006 // 실제 Razor 입력 바인딩과 클릭을 좁은 로컬 시험에서 호출합니다.
internal sealed class 분산입고협의InteractionRenderer(IServiceProvider services, ILoggerFactory logging) : Renderer(services, logging)
{
    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
    private int id;
    protected override void HandleException(Exception exception) => throw exception;
    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
    public Task Start<T>(Dictionary<string, object?> parameters) where T : IComponent
    { id = AssignRootComponentId(InstantiateComponent(typeof(T))); return RenderRootComponentAsync(id, ParameterView.FromDictionary(parameters)); }
    public string Text() { var frames = GetCurrentRenderTreeFrames(id); return Text(frames.Array, 0, frames.Count); }
    private static string Text(RenderTreeFrame[] frames, int start, int count) => string.Concat(frames.Skip(start).Take(count).Where(x => x.FrameType is RenderTreeFrameType.Text or RenderTreeFrameType.Markup).Select(x => x.FrameType == RenderTreeFrameType.Text ? x.TextContent : x.MarkupContent));
    public Task Click(string text) => DispatchEventAsync(Event("button", text, "onclick"), null, new MouseEventArgs());
    public Task Change(string text, object value) => DispatchEventAsync(Event("label", text, "onchange"), null, new ChangeEventArgs { Value = value });
    public bool DisabledFieldset(string text)
    {
        var frames = GetCurrentRenderTreeFrames(id);
        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames.Array[i];
            if (frame.FrameType != RenderTreeFrameType.Element || frame.ElementName != "fieldset" || !Text(frames.Array, i + 1, frame.ElementSubtreeLength - 1).Contains(text, StringComparison.Ordinal)) continue;
            for (var j = i + 1; j < i + frame.ElementSubtreeLength && frames.Array[j].FrameType == RenderTreeFrameType.Attribute; j++)
                if (frames.Array[j].AttributeName == "disabled" && frames.Array[j].AttributeValue is true) return true;
        }
        return false;
    }
    private ulong Event(string element, string text, string attribute)
    {
        var frames = GetCurrentRenderTreeFrames(id);
        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames.Array[i]; if (frame.FrameType != RenderTreeFrameType.Element || frame.ElementName != element || !Text(frames.Array, i + 1, frame.ElementSubtreeLength - 1).Contains(text, StringComparison.Ordinal)) continue;
            for (var j = i + 1; j < i + frame.ElementSubtreeLength; j++)
                if (frames.Array[j].FrameType == RenderTreeFrameType.Attribute && frames.Array[j].AttributeName == attribute && frames.Array[j].AttributeEventHandlerId != 0) return frames.Array[j].AttributeEventHandlerId;
        }
        throw new InvalidOperationException("예상한 화면 조작이 없습니다: " + text);
    }
}
#pragma warning restore BL0006
