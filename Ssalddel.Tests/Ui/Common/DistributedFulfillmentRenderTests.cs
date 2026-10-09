using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

namespace Ssalddel.Tests.Ui.Common;

public sealed class DistributedFulfillmentRenderTests
{
    [Theory]
    [InlineData("warehouse", "sites", "내 보관 거점")]
    [InlineData("warehouse", "inventory", "허용된 거점 재고")]
    [InlineData("warehouse", "inbound", "입고 계획·실물 확인")]
    [InlineData("warehouse", "waves", "시간대별 출고 묶음")]
    [InlineData("shipper", "inventory", "내 판매 재고")]
    [InlineData("shipper", "inbound", "입고 계획·실물 확인")]
    [InlineData("shipper", "orders", "내 판매 주문")]
    [InlineData("cargo-driver", "round", "내 배송 순회")]
    [InlineData("orderer", "orders", "내 주문 배송 상태")]
    [InlineData("operator", "waves", "시간대별 출고 묶음")]
    public async Task 실제역할의_독립업무화면은_조회와_복귀를_제공한다(string role, string screen, string title)
    {
        var t = new DistributedUiTransport(); var html = await Render(t, role, screen, "//evil.example");
        Assert.Contains(title, html); Assert.Contains("최신 업무 다시 조회", html); Assert.Contains("업무로 돌아가기", html); Assert.DoesNotContain("evil.example", html); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 익명화면에는_상품과_동의양식이_없다()
    {
        var t = new DistributedUiTransport { CurrentIdentity = new(null, 0, false) }; var html = await Render(t, "warehouse", "sites"); Assert.Contains("로그인", html); Assert.DoesNotContain("내 보관 거점", html); Assert.DoesNotContain("적재 위치 이름", html); Assert.Empty(t.Gets);
    }
    [Fact] public async Task 조회실패를_입고와_재고0건으로_오인하지않는다()
    {
        var t = new DistributedUiTransport { BeforeGet = _ => throw new HttpRequestException("offline") }; var html = await Render(t, "warehouse", "inventory"); Assert.Contains("role=\"alert\"", html); Assert.DoesNotContain("조회한 재고가 없습니다", html); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 올바르지않은_선택번호는_안내로_끝나고_API를_보내지않는다()
    {
        var t = new DistributedUiTransport(); var html = await Render(t, "warehouse", "sites", selected: "../other"); Assert.Contains("업무 대상의 번호", html); Assert.Empty(t.Gets);
    }
    [Fact] public async Task 연습표시는_실운송이나_실정산_성공과_구분된다()
    { var html = await Render(new(), "warehouse", "sites"); Assert.Contains("연습 환경", html); Assert.Contains("실제 보관·운송·정산 완료를 뜻하지 않습니다", html); }
    [Fact] public async Task 일반조회profile은_연습완주로_승격하지않는다()
    { var t = new DistributedUiTransport(); t.Workspace.Profile = "ordinary"; var html = await Render(t, "warehouse", "sites"); Assert.Contains("role=\"alert\"", html); Assert.DoesNotContain("조회한 보관 거점이 없습니다", html); }
    [Fact] public async Task 생성후보가_없어도_일반소유자의_첫공간등록을_지원하며_내부ID는_요구하지않는다()
    {
        var t = new DistributedUiTransport(); t.Workspace.AllowedActions = ["create-site"]; var html = await Render(t, "warehouse", "sites"); Assert.Contains("첫 보관 공간", html); Assert.Contains("본인이 확인한 도로명주소", html); Assert.Contains("확인한 위도", html); Assert.Contains("확인한 경도", html);
        Assert.Contains("도로명주소를 직접 확인", html); Assert.Contains("위도·경도를 직접 확인", html); Assert.Contains("등록은 소유자·관리자의 별도 동의를 대신하지 않습니다", html);
        Assert.DoesNotContain("이 창고의 관리자", html); Assert.DoesNotContain("WarehouseId", html); Assert.DoesNotContain("PlaceId", html); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 실제창고_건물_관리자는_조회한_표시명선택이다()
    {
        var t = new DistributedUiTransport(); t.Workspace.AllowedActions = ["create-site"]; t.Workspace.WarehouseCandidates = [new(7, "내 창고 이름", [new("place-a", "candidate-a", "공개 건물 이름")], [new("manager-a", "등록 관리자 표시명")])];
        var html = await Render(t, "warehouse", "sites"); Assert.Contains("내 창고 이름", html); Assert.DoesNotContain("관리자 ID", html); Assert.DoesNotContain("주소 ID 입력", html); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 소유자와_관리자의_동의와_철회는_독립행동이다()
    {
        var t = new DistributedUiTransport(); t.Workspace.Sites = [new() { SiteId = "site-a", PublicLabel = "공간", Revision = 2, OwnerConsented = true, ManagerConsented = false, AllowedActions = ["consent-manager", "revoke-owner"] }];
        var html = await Render(t, "warehouse", "sites", selected: "site-a"); Assert.Contains("관리자로서 동의", html); Assert.Contains("소유자 동의 철회", html); Assert.DoesNotContain(">소유자로서 동의<", html); Assert.Contains("동의·철회 근거", html); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 다른역할은_운영묶음양식을_열지못한다()
    {
        var t = new DistributedUiTransport(); t.Workspace.AllowedActions = ["create-wave"]; var html = await Render(t, "shipper", "waves"); Assert.Contains("역할의 업무 화면", html); Assert.DoesNotContain("거점당 작업 시간", html); Assert.Empty(t.Gets);
    }
    [Fact] public async Task 기사순회는_현재거점의_위치_상품_수량과_다음방문순서를_표시한다()
    {
        var t = DistributedUiTransport.Round(); var html = await Render(t, "cargo-driver", "round", selected: "wave-a"); Assert.Contains("두 거점·두 기업·두 고객", html); Assert.Contains("현재 거점에서 직접 피킹", html); Assert.Contains("1. 적재 위치 바코드", html); Assert.Contains("같은 적재 위치·같은 상품 소유자", html); Assert.Contains("사진 촬영", html); Assert.Contains("사진 보내고 피킹 완료", html); Assert.Contains("서버가 저장한 방문 순서", html); Assert.DoesNotContain("owner-a</", html); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 고객화면은_전달기록과_본인수령버튼만_표시하고_거점스캔을_노출하지않는다()
    {
        var html = await Render(DistributedUiTransport.Customer(), "orderer", "orders", selected: "wave-a"); Assert.Contains("기사 전달 기록", html); Assert.Contains("본인 수령 확인", html); Assert.DoesNotContain("적재 위치 바코드", html); Assert.DoesNotContain("예약별", html); Assert.Contains("결제·정산은 별도 상태", html);
    }
    [Theory]
    [InlineData("Connected")]
    [InlineData("Pending")]
    [InlineData("Blocked")]
    public async Task 주문_연결상태는_배송성공과_권한거절을_구분하고_허용된_재확인만_제공한다(string status)
    {
        var t = DistributedUiTransport.Customer();
        t.Workspace.Waves[0].Orders = [t.Workspace.Waves[0].Orders[0] with { OrderLedgerConnectionStatusCode = status,
            RootOrderLedgerId = status == "Blocked" ? null : "root-a", RootOrderLedgerRevision = status == "Blocked" ? null : 7,
            AllowedActions = status == "Pending" ? ["retry-order-ledger"] : [] }];
        var html = await Render(t, "orderer", "orders", selected: "wave-a");
        if (status == "Connected") Assert.Contains("/orders/root-a", html);
        if (status == "Pending") Assert.Contains("원주문 연결 다시 확인", html);
        if (status == "Blocked")
        {
            Assert.Contains("참여 권한", html);
            Assert.DoesNotContain("원주문 연결 다시 확인", html);
            Assert.DoesNotContain("/orders/root-a", html);
        }
        Assert.Empty(t.Posts);
    }

    [Fact] public async Task 입고실물확인은_계획수량과_부분확인수량을_구분한다()
    {
        var t = DistributedUiTransport.Round(); t.Workspace.Waves = []; t.Workspace.InboundPlans = [new("plan-a", "실제 예정 입고", "Partial", 2, [new("line-a", "site-a", "loc-a", "owner-a", "sku-a", "lot-a", 10, 4)], ["receive"])];
        var html = await Render(t, "warehouse", "inbound", selected: "plan-a"); Assert.Contains("예정 10 개 · 확인 4 개", html); Assert.Contains("남은 6 개", html); Assert.Contains("사진 보내고 입고·적재 완료", html); Assert.Contains("JPEG·PNG", html); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 빈원천계정도_본인상품선언후_두거점분할계획을_입력할수있다()
    {
        var t = DistributedUiTransport.Round(); t.Workspace.Waves = []; t.Workspace.AllowedActions = ["plan-inbound"];
        foreach (var site in t.Workspace.Sites) site.StatusCode = "Active";
        var html = await Render(t, "shipper", "inbound"); Assert.Contains("첫 상품", html); Assert.Contains("새 본인 상품을 직접 선언", html); Assert.Contains("상품 코드(SKU)", html);
        Assert.Contains("전체 예정 수량", html); Assert.Contains("본인 소유이며 본인이 판매", html); Assert.Contains("실재고가 늘지 않습니다", html); Assert.Contains("첫 거점", html); Assert.Contains("둘째 거점", html);
        Assert.DoesNotContain("소유자 ID", html); Assert.DoesNotContain("SourceInboundId", html); Assert.Empty(t.Posts);
    }
    [Theory] [InlineData("javascript:alert(1)")] [InlineData("//external.invalid/login")]
    public async Task 로그인복귀는_로컬경로로_제한한다(string login)
    {
        var t = new DistributedUiTransport { CurrentIdentity = new(null, 0, false) }; var html = await Render(t, "orderer", "orders", login: login); Assert.DoesNotContain(login, html); Assert.Contains("/workspace-login/orderer", html);
    }
    [Fact] public async Task 지도에서_고른업무의_복귀번호는_연결업무에도_보존된다()
    {
        var html = await Render(new(), "warehouse", "sites", "/workspace/warehouse?selected=existing-work");
        Assert.Contains("href=\"/workspace/warehouse?selected=existing-work\"", html); Assert.Contains("returnUrl=%2Fworkspace%2Fwarehouse%3Fselected%3Dexisting-work", html);
    }
    [Fact] public async Task 같은위치같은SKU의_두기업상품은_원시소유자ID없이_실제표시명으로_구분한다()
    {
        var t = DistributedUiTransport.Round(); t.Workspace.Waves[0].Reservations = t.Workspace.Waves[0].Reservations.Select(line => line with
            { Sku = "same-sku", OwnerLabel = line.OwnerUserId == "owner-a" ? "첫 기업" : "둘째 기업", ProductLabel = line.OwnerUserId == "owner-a" ? "첫 기업 상품" : "둘째 기업 상품" }).ToArray();
        using var model = new 분산이행PageViewModel(new(t, "cargo-driver"), "round"); await model.LoadAsync("wave-a");
        var services = new ServiceCollection(); services.AddLogging(); await using var provider = services.BuildServiceProvider();
        using var renderer = new InteractionRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.StartRound(model); Assert.Contains("첫 기업", renderer.VisibleText()); Assert.Contains("둘째 기업", renderer.VisibleText());
            await renderer.ChangeLabel("확인할 적재 위치와 상품 소유자", "loc-a|owner-a"); Assert.Contains("첫 기업 상품", renderer.VisibleText()); Assert.DoesNotContain("둘째 기업 상품", renderer.VisibleText());
            await renderer.ChangeLabel("확인할 적재 위치와 상품 소유자", "loc-a|owner-b"); Assert.Contains("둘째 기업 상품", renderer.VisibleText()); Assert.DoesNotContain("첫 기업 상품", renderer.VisibleText());
            Assert.DoesNotContain("owner-a", renderer.VisibleText()); Assert.DoesNotContain("owner-b", renderer.VisibleText());
        });
        Assert.Empty(t.Posts);
    }
    [Theory]
    [InlineData("cargo-driver", "round", true)]
    [InlineData("warehouse", "inbound", false)]
    [InlineData("shipper", "orders", false)]
    [InlineData("orderer", "orders", false)]
    public async Task 로그인복귀는_선택원장_확인중요청_원래지도선택을_함께_보존한다(string role, string screen, bool driverAlias)
    {
        var request = Guid.Parse("4e66d9f0-63cf-4df4-9d69-2c2880f4346f"); var returnHref = $"/workspace/{role}?selected=old-work";
        var resume = 분산이행Navigation.ResumeHref(role, screen, "wave-or-plan", request, returnHref, driverAlias);
        Assert.StartsWith(driverAlias ? "/driver/distributed/" : $"/workspace/{role}/distributed/", resume);
        Assert.Contains("selected=wave-or-plan", resume); Assert.Contains("requestId=" + request, resume); Assert.Contains("returnUrl=" + Uri.EscapeDataString(returnHref), resume);
        var login = "/login?returnUrl=" + Uri.EscapeDataString(resume); Assert.Equal(resume, Uri.UnescapeDataString(login.Split("returnUrl=")[1]));
        var t = new DistributedUiTransport { CurrentIdentity = new(null, 0, false) }; var html = await Render(t, role, screen, login: login);
        Assert.Contains($"href=\"{login}\"", html); Assert.Empty(t.Gets); Assert.Empty(t.Posts);
    }
    [Theory]
    [InlineData("//external.invalid", false)]
    [InlineData("/workspace/cargo-driver/distributed/round", false)]
    [InlineData("/workspace/warehouse/distributed/inbound?selected=plan-a&requestId=4e66d9f0-63cf-4df4-9d69-2c2880f4346f", true)]
    public void 거점로그인은_본인역할의_안전한_선택복귀만_소비한다(string route, bool allowed)
    { Assert.Equal(allowed ? route : "/workspace/warehouse/distributed/sites", 분산이행Navigation.WarehouseLoginReturn(route)); }
    [Theory]
    [InlineData("orderer", "orders", true)] [InlineData("orderer", "orders", false)]
    [InlineData("shipper", "orders", true)] [InlineData("shipper", "orders", false)]
    [InlineData("warehouse", "waves", true)] [InlineData("operator", "waves", true)]
    public async Task 취소입력은_서버가_해당주문에_허용한역할에서만_표시된다(string role, string screen, bool allowed)
    {
        var t = DistributedUiTransport.Customer(); t.Workspace.Waves[0].Orders = t.Workspace.Waves[0].Orders.Select(order => order with { AllowedActions = allowed ? ["cancel"] : [] }).ToArray();
        var html = await Render(t, role, screen, selected: "wave-a");
        Assert.Equal(allowed, html.Contains(role is "warehouse" or "operator" ? "취소할 주문" : "내 주문 취소 사유")); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 현재방문작업을_순회조건보다_먼저표시하고_보조조건은_접혀있다()
    {
        var html = await Render(DistributedUiTransport.Round(), "cargo-driver", "round", selected: "wave-a");
        Assert.True(html.IndexOf("현재 방문 작업", StringComparison.Ordinal) < html.IndexOf("순회 조건", StringComparison.Ordinal));
        Assert.Matches("<details(?![^>]*open)[^>]*><summary[^>]*>방문 순서", html);
        Assert.DoesNotContain("판본 1", html);
    }
    [Fact] public async Task 실제경고설명은_각각확인할수있고_미확인수락버튼은_비활성이다()
    {
        var t = DistributedUiTransport.Round(); t.Workspace.Waves[0].AllowedActions = ["accept"];
        t.Workspace.Waves[0].AcceptanceWarnings = [new("cargo-a", "FreightVehicleAdvisory", "서버가 조회한 실제 차량 조건")];
        var html = await Render(t, "cargo-driver", "round", selected: "wave-a"); Assert.Contains("서버가 조회한 실제 차량 조건", html);
        Assert.Matches("<button[^>]*disabled[^>]*>이 배송 순회 수락", html); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 가용0이라도_본인기존예약의_편입경로는_표시한다()
    {
        var t = DistributedUiTransport.Customer(); var now = DateTime.UtcNow; t.Workspace.Waves[0].AllowedActions = ["add-order"];
        t.Workspace.CargoCandidates = [new("cargo-a", "내 의뢰 표시명", now, now.AddHours(1), now, now.AddHours(2))];
        t.Workspace.ExistingOutboundCandidates = [new(17, "cargo-a", "lot-a", "site-a", "owner", "actor", "sku", 3, "기존 상품", "소유 기업")];
        var html = await Render(t, "shipper", "orders", selected: "wave-a"); Assert.Contains("이미 예약된 출고 물품 편입", html); Assert.Contains("내 의뢰 표시명", html); Assert.DoesNotContain("기존 화물 의뢰 작성과 입고 확인을 먼저", html); Assert.Empty(t.Posts);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task 첫주문의_확인체크로_둘째주문을_취소하거나_ACK하지않는다(bool acknowledge)
    {
        var t = DistributedUiTransport.Customer(); var first = t.Workspace.Waves[0].Orders[0] with { OrderReference = "첫 주문", AllowedActions = ["acknowledge", "cancel"] };
        t.Workspace.Waves[0].Orders = [first, first with { OrderId = "order-b", OrderReference = "둘째 주문" }];
        using var model = new 분산이행PageViewModel(new(t, "orderer"), "orders"); await model.LoadAsync("wave-a");
        var services = new ServiceCollection(); services.AddLogging(); await using var provider = services.BuildServiceProvider();
        using var renderer = new InteractionRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.Start(model);
            await renderer.ChangeLabel(acknowledge ? "첫 주문 수령 확인 근거" : "첫 주문 취소 사유", "확인 근거");
            await renderer.ChangeLabel(acknowledge ? "첫 주문 물품을 본인이 실제로 수령" : "첫 주문 의 아직 피킹하지 않은", true);
            var button = acknowledge ? "본인 수령 확인" : "주문 취소 요청";
            await renderer.ClickOrder("둘째 주문", button); Assert.Empty(t.Posts);
            await renderer.ClickOrder("첫 주문", button); Assert.Single(t.Posts);
            Assert.Equal("order-a", t.Posts[0].Body switch { DistributedAcknowledgementRequest request => request.OrderId, DistributedCancelRequest request => request.OrderId, _ => null });
        });
    }
    [Fact] public async Task 재고후보가없어도_본인기존합의_고정당사자의_조건변경을_저장한다()
    {
        var t = new DistributedUiTransport(); var now = DateTime.UtcNow;
        t.Workspace.StorageAgreements = [new("agreement-a", "site-unlisted", "loc-unlisted", "actor", "owner-unlisted", 1, 1, "Active", true, true,
            new() { AllowedSkus = ["sku"], MaximumStoredQuantity = 10, StartsAtUtc = now.AddDays(-1), EndsAtUtc = now.AddDays(30), AccessHours = "09:00~18:00",
                PriceOrFreeTerms = "무상", CustodianAndCare = "공간 제공자가 관리", LossDamageAndReturnTerms = "실물 확인 후 반환" }, ["change-terms"])];
        using var model = new 분산이행PageViewModel(new(t, "warehouse"), "agreements"); await model.LoadAsync("agreement-a");
        var services = new ServiceCollection(); services.AddLogging(); await using var provider = services.BuildServiceProvider();
        using var renderer = new InteractionRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.StartAgreement(model); await renderer.Click("변경안 작성"); await renderer.ChangeLabel("최대 보관 수량", "12");
            await renderer.ChangeLabel("공간·상품 소유자와 위 조건을 확인", true); await renderer.Click("보관 조건 저장");
        });
        var post = Assert.Single(t.Posts); var body = Assert.IsType<DistributedStorageAgreementRequest>(post.Body);
        Assert.Contains("/storage-agreements/agreement-a/terms?", post.Path); Assert.Equal("owner-unlisted", body.GoodsOwnerUserId); Assert.Equal("loc-unlisted", body.LocationId);
        Assert.Equal(12, body.Terms.MaximumStoredQuantity); Assert.Equal(1, body.ExpectedRevision); Assert.NotNull(model.Message); Assert.Empty(t.PhotoPosts);
    }
    [Fact] public async Task 실제사진버튼_재촬영취소와_수량변경은_이전사진을_완료에쓰지않는다()
    {
        var t = DistributedUiTransport.Round(); var camera = new Test업무사진촬영Adapter(); var firstPhoto = camera.Photo;
        using var model = new 분산이행PageViewModel(new(t, "cargo-driver"), "round", camera); await model.LoadAsync("wave-a");
        var services = new ServiceCollection(); services.AddLogging(); await using var provider = services.BuildServiceProvider();
        using var renderer = new InteractionRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.StartPhoto(model, DistributedPhotoWorkflowTests.PickWork()); await renderer.Click("사진 촬영"); Assert.Single(model.Photo.Photos);
            camera.Photo = null; await renderer.Click("이 사진 다시 촬영"); Assert.False(model.Photo.HasPhotos);
            camera.Photo = firstPhoto; await renderer.Click("사진 촬영"); Assert.Single(model.Photo.Photos);
            await renderer.RenderPhoto(DistributedPhotoWorkflowTests.PickWork(quantity: 2)); Assert.False(model.Photo.HasPhotos); Assert.Null(model.Photo.Work);
        });
        Assert.Empty(t.PhotoPosts); Assert.Empty(t.Posts);
    }
    [Fact] public async Task 실제사진보내기버튼_업로드실패는_업무완료로_표시하지않는다()
    {
        var t = DistributedUiTransport.Round(); t.BeforePhotoPost = _ => throw new HttpRequestException("lost upload response");
        using var model = new 분산이행PageViewModel(new(t, "cargo-driver"), "round", new Test업무사진촬영Adapter()); await model.LoadAsync("wave-a");
        var services = new ServiceCollection(); services.AddLogging(); await using var provider = services.BuildServiceProvider();
        using var renderer = new InteractionRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        { await renderer.StartPhoto(model, DistributedPhotoWorkflowTests.PickWork()); await renderer.Click("사진 촬영"); await renderer.Click("사진 보내고"); });
        Assert.Single(t.PhotoPosts); Assert.Empty(t.Posts); Assert.True(model.HasPendingPhoto); Assert.Null(model.Message);
    }
    [Fact]
    public async Task 원주문양식은_내화물을선택한뒤_조회한주문표시명으로연결한다()
    {
        var t = DistributedUiTransport.Customer(); var now = DateTime.UtcNow; t.Workspace.Waves[0].AllowedActions = ["add-order"];
        t.Workspace.CargoCandidates = [new("cargo-a", "선택한 내 의뢰", now, now.AddHours(1), now, now.AddHours(2))];
        t.Workspace.Lots = [new("lot-a", "site-a", "loc-a", "actor", "actor", "sku-a", "내 물품", 1, 3, 3, 0)];
        using var model = new 분산이행PageViewModel(new(t, "shipper"), "orders"); await model.LoadAsync("wave-a");
        var services = new ServiceCollection(); services.AddLogging(); using var provider = services.BuildServiceProvider();
        using var renderer = new InteractionRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.Start(model); Assert.DoesNotContain("조회한 실제 원주문", renderer.VisibleText());
            await renderer.ChangeLabel("내 화물 의뢰", "cargo-a");
            await renderer.ChangeLabel("기존 원주문에 이 배송 업무 연결", true);
            Assert.Contains("조회한 실제 원주문", renderer.VisibleText()); Assert.DoesNotContain("원장 ID 입력", renderer.VisibleText());
            await renderer.ChangeLabel("연결할 원주문", "root-a"); await renderer.ChangeLabel("주문 표시명", "실제 배송 주문");
            await renderer.ChangeLabel("내 판매 상품", "lot-a"); await renderer.ChangeLabel("새 예약 수량", "1");
            await renderer.Click("주문 물품 추가");
            t.BeforeGet = path => { if (path.Contains("order-ledger-candidates")) throw new IOException("requery failed"); };
            await renderer.Click("연결할 원주문 다시 조회");
            Assert.Contains("sku-a · 1 개", renderer.VisibleText()); Assert.Null(model.SelectedRootOrderId); Assert.Empty(t.Posts);
            t.BeforeGet = null; await renderer.Click("연결할 원주문 다시 조회"); await renderer.ChangeLabel("연결할 원주문", "root-a");
            await renderer.ChangeLabel("실제 의뢰의 배송지·시간창과 내 재고 수량", true);
            await renderer.Click("이 묶음에 내 주문 추가");
        });
        var request = Assert.IsType<DistributedOrderRequest>(Assert.Single(t.Posts).Body);
        Assert.Equal("root-a", request.RootOrderLedgerId); Assert.Equal(7, request.ExpectedRootRevision);
        Assert.Contains(t.Gets, path => path.Contains("/waves/wave-a/order-ledger-candidates?roleKey=shipper&cargoRequestId=cargo-a"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 원주문빈목록과조회실패는_구분하고_재조회로복구한다(bool error)
    {
        var t = DistributedUiTransport.Customer(); var now = DateTime.UtcNow; t.Workspace.Waves[0].AllowedActions = ["add-order"];
        t.Workspace.CargoCandidates = [new("cargo-a", "내 의뢰", now, now.AddHours(1), now, now.AddHours(2))];
        t.Workspace.Lots = [new("lot-a", "site-a", "loc-a", "actor", "actor", "sku-a", "물품", 1, 3, 3, 0)];
        if (!error) t.RootCandidates = t.RootCandidates with { Candidates = [] };
        using var model = new 분산이행PageViewModel(new(t, "shipper"), "orders"); await model.LoadAsync("wave-a");
        if (error) t.BeforeGet = path => { if (path.Contains("order-ledger-candidates")) throw new IOException("offline"); };
        var services = new ServiceCollection(); services.AddLogging(); using var provider = services.BuildServiceProvider();
        using var renderer = new InteractionRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.Start(model); await renderer.ChangeLabel("내 화물 의뢰", "cargo-a");
            await renderer.ChangeLabel("기존 원주문에 이 배송 업무 연결", true);
            Assert.Contains(error ? "연결할 원주문을 확인하지 못했습니다" : "함께 참여한 열린 원주문이 없습니다", renderer.VisibleText());
            t.BeforeGet = null; t.RootCandidates = t.RootCandidates with { Candidates = [new("root-a", 8, "다시 확인한 실제 주문", "진행중")] };
            await renderer.Click("연결할 원주문 다시 조회"); Assert.Contains("다시 확인한 실제 주문", renderer.VisibleText());
            Assert.DoesNotContain("연결할 원주문을 확인하지 못했습니다", renderer.VisibleText());
        });
        Assert.Empty(t.Posts); Assert.Null(model.SelectedRootOrderId);
    }

    private static async Task<string> Render(DistributedUiTransport t, string role, string screen, string? returnHref = null, string? selected = null, string? login = null)
    {
        var services = new ServiceCollection(); services.AddLogging(); await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<분산이행Screen>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(분산이행Screen.Transport)] = t, [nameof(분산이행Screen.RoleKey)] = role, [nameof(분산이행Screen.Screen)] = screen,
                [nameof(분산이행Screen.ReturnHref)] = returnHref, [nameof(분산이행Screen.SelectedId)] = selected, [nameof(분산이행Screen.LoginHref)] = login }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }
}

#pragma warning disable BL0006 // Test only: dispatch the real generated component bindings without an external UI test package.
internal sealed class InteractionRenderer(IServiceProvider services, ILoggerFactory logging) : Renderer(services, logging)
{
    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
    private int componentId;
    private 분산이행PageViewModel photoModel = default!;
    protected override void HandleException(Exception exception) => throw exception;
    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
    public Task Start(분산이행PageViewModel model)
    {
        componentId = AssignRootComponentId(InstantiateComponent(typeof(분산주문Screen)));
        return RenderRootComponentAsync(componentId, ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(분산주문Screen.Model)] = model }));
    }
    public Task StartAgreement(분산이행PageViewModel model)
    { componentId = AssignRootComponentId(InstantiateComponent(typeof(분산보관합의Screen))); return RenderRootComponentAsync(componentId, ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(분산보관합의Screen.Model)] = model })); }
    public Task StartRound(분산이행PageViewModel model)
    { componentId = AssignRootComponentId(InstantiateComponent(typeof(분산순회Screen))); return RenderRootComponentAsync(componentId, ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(분산순회Screen.Model)] = model })); }
    public string VisibleText()
    { var frames = GetCurrentRenderTreeFrames(componentId); return string.Concat(frames.Array.Take(frames.Count).Where(frame => frame.FrameType is RenderTreeFrameType.Text or RenderTreeFrameType.Markup).Select(frame => frame.FrameType == RenderTreeFrameType.Text ? frame.TextContent : frame.MarkupContent)); }
    public Task StartPhoto(분산이행PageViewModel model, DistributedEvidenceWorkRequest work)
    { photoModel = model; componentId = AssignRootComponentId(InstantiateComponent(typeof(분산사진업무Screen))); return RenderPhoto(work); }
    public Task RenderPhoto(DistributedEvidenceWorkRequest work) => RenderRootComponentAsync(componentId, ParameterView.FromDictionary(new Dictionary<string, object?>
    { [nameof(분산사진업무Screen.Model)] = photoModel, [nameof(분산사진업무Screen.Work)] = work, [nameof(분산사진업무Screen.Title)] = "피킹", [nameof(분산사진업무Screen.Action)] = "pick",
        [nameof(분산사진업무Screen.Suffix)] = "waves/wave-a/pick-batches", [nameof(분산사진업무Screen.AllowedActions)] = new[] { "pick" }, [nameof(분산사진업무Screen.Quantity)] = work.Pick!.Lines.Sum(line => line.Quantity), [nameof(분산사진업무Screen.Confirmed)] = true }));
    public Task Click(string button) => DispatchEventAsync(FindEvent("button", button, "onclick"), null, new MouseEventArgs());
    public Task ChangeLabel(string label, object value) => DispatchEventAsync(FindEvent("label", label, "onchange"), null, new ChangeEventArgs { Value = value });
    public Task ClickOrder(string reference, string button)
    {
        var frames = GetCurrentRenderTreeFrames(componentId); var article = Element(frames.Array, frames.Count, "article", reference, 0);
        var chosen = Element(frames.Array, article + frames.Array[article].ElementSubtreeLength, "button", button, article + 1);
        return DispatchEventAsync(Event(frames.Array, chosen, "onclick"), null, new MouseEventArgs());
    }
    private ulong FindEvent(string element, string text, string attribute)
    { var frames = GetCurrentRenderTreeFrames(componentId); return Event(frames.Array, Element(frames.Array, frames.Count, element, text, 0), attribute); }
    private static int Element(RenderTreeFrame[] frames, int end, string element, string text, int start)
    {
        for (var i = start; i < end; i++)
        {
            if (frames[i].FrameType != RenderTreeFrameType.Element || frames[i].ElementName != element) continue;
            var content = string.Concat(frames.Skip(i + 1).Take(frames[i].ElementSubtreeLength - 1)
                .Where(frame => frame.FrameType is RenderTreeFrameType.Text or RenderTreeFrameType.Markup)
                .Select(frame => frame.FrameType == RenderTreeFrameType.Text ? frame.TextContent : frame.MarkupContent));
            if (content.Contains(text, StringComparison.Ordinal)) return i;
        }
        throw new InvalidOperationException("Expected rendered control was absent: " + text);
    }
    private static ulong Event(RenderTreeFrame[] frames, int element, string attribute)
    {
        for (var i = element + 1; i < element + frames[element].ElementSubtreeLength; i++)
            if (frames[i].FrameType == RenderTreeFrameType.Attribute && frames[i].AttributeName == attribute && frames[i].AttributeEventHandlerId != 0) return frames[i].AttributeEventHandlerId;
        throw new InvalidOperationException("Expected rendered event was absent: " + attribute);
    }
}
#pragma warning restore BL0006
