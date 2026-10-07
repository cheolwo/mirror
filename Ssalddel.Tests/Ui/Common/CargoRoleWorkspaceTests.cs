using Microsoft.AspNetCore.Components.Forms;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CargoRoleWorkspaceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 서버의_명시적_빈_행동목록은_구판_응답과_구별해_신고도_막는다(bool legacy)
    {
        var source = new 기사운송상세응답 { Id = 1, 상태 = "상차지도착", 가능한행동 = legacy ? null : [] };
        var card = CargoDriverWorkspaceAdapter.Map(source);
        Assert.Equal(legacy, Assert.Single(card.Actions!, action => action.Key == "issue").Enabled);
        Assert.Equal(legacy, Assert.Single(card.Actions!, action => action.IsPrimary).Enabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 상차_성공_후_새로운_업무보류는_이미저장한_완료를_실패로_바꾸지_않는다(bool legacy)
    {
        var api = new Api();
        api.Get = (_, _, _) => Task.FromResult<object>(new 기사운송상세응답
        {
            Id = 1, 상태 = api.GetCalls.Count < 3 ? "상차지도착" : "상차완료",
            관리자확인필요 = api.GetCalls.Count >= 3, 운송진행보류 = api.GetCalls.Count >= 3,
            가능한행동 = legacy ? null : api.GetCalls.Count < 3 ? ["pickup", "issue"] : ["issue"]
        });
        api.Post = (_, _, _, _) => Task.FromResult<object?>(new 기사운송상태변경응답 { Id = 1, 상태 = "상차완료" });
        using var model = new CargoTransportActionViewModel(new(api), api, new Access());
        await model.LoadAsync(1, "pickup");
        model.SetPhoto(new Photo()); model.CargoConfirmed = model.LocationConfirmed = model.RecipientConfirmed = model.SaveConfirmed = true;
        await model.SubmitAsync();
        Assert.True(model.Saved); Assert.False(model.Lifetime.HasError);
        Assert.True(model.Source!.운송진행보류); Assert.False(model.CanSubmit);
        Assert.Single(api.PostCalls);
    }

    [Fact]
    public void 좌표가_없는_화주와_기사_주소는_임의_핀으로_바꾸지_않는다()
    {
        var shipper = ShipperWorkspaceAdapter.Map(new() { 의뢰Id = "request-1", 픽업지 = "상차 주소", 하차지 = "하차 주소" });
        var driver = CargoDriverWorkspaceAdapter.Map(new() { Id = 1, 출발지 = "상차 주소", 도착지 = "하차 주소", 상태 = "상차지도착" });
        Assert.Empty(shipper.Markers!); Assert.Empty(shipper.Routes!);
        Assert.Null(driver.Markers); Assert.Contains(driver.Sections!, section => section.Title == "지도");
    }

    [Fact]
    public void 확인된_두_좌표만_직선_후보선으로_표시한다()
    {
        var item = ShipperWorkspaceAdapter.Map(new()
        {
            의뢰Id = "request-1", 픽업위도 = 37.57m, 픽업경도 = 127.01m, 하차위도 = 37.58m, 하차경도 = 127.02m
        });
        Assert.Equal(2, item.Markers!.Count);
        var route = Assert.Single(item.Routes!);
        Assert.Contains("직선 후보선", route.Notice); Assert.Contains("실제 주행 경로 아님", route.Notice);
        Assert.Equal("cargo-straight-candidate", route.SourceCode);
        Assert.Null(CargoWorkspacePresentation.Point(91m, 127m));
        Assert.Empty(CargoWorkspacePresentation.CandidateRoutes("r", CargoWorkspacePresentation.LocationMarkers("r", 37m, null, 38m, 127m)));
    }

    [Fact]
    public void 종료되거나_오래된_기사_위치는_화주_지도에_표시하지_않는다()
    {
        var source = new 화주운송의뢰응답
        {
            의뢰Id = "r", 확정기사Id = "driver", 기사최근위도 = 37m, 기사최근경도 = 127m,
            기사최근위치시각Utc = DateTime.UtcNow.AddHours(-1), 운송상태 = "운송중"
        };
        Assert.Empty(ShipperWorkspaceAdapter.Map(source).Markers!);
        source.기사최근위치시각Utc = DateTime.UtcNow; source.운송상태 = "인수완료";
        Assert.Empty(ShipperWorkspaceAdapter.Map(source).Markers!);
    }

    [Theory]
    [InlineData("배차확정", "arrive-pickup", null)]
    [InlineData("상차지도착", "pickup", "/pickup")]
    [InlineData("운송중", "arrive-dropoff", null)]
    [InlineData("하차지도착", "dropoff", "/dropoff")]
    public void 기사_현재_단계에_맞는_한_주행동을_표시한다(string state, string key, string? suffix)
    {
        var item = CargoDriverWorkspaceAdapter.Map(new() { Id = 3, 상태 = state });
        var action = Assert.Single(item.Actions!.Where(action => action.IsPrimary));
        Assert.Equal(key, action.Key);
        if (suffix is null) Assert.Null(action.Route); else Assert.EndsWith(suffix, action.Route);
    }

    [Fact]
    public void 개인정보_제공이_보류되면_기사_목적지와_연락처와_행동을_제거한다()
    {
        var item = CargoDriverWorkspaceAdapter.Map(new()
        { Id = 7, 개인정보제공보류 = true, 출발지 = "비공개 주소", 상차연락처 = "010-0000-0000", 상태 = "상차지도착" });
        Assert.Null(item.Actions); Assert.Null(item.Markers);
        Assert.DoesNotContain(item.Sections!.SelectMany(section => section.Fields), field => field.Value.Contains("비공개") || field.Value.Contains("010"));
    }

    [Fact]
    public async Task 현재_목록에_없는_선택_의뢰도_서버_권한으로_새로_조회한다()
    {
        var api = new Api { Get = (_, path, _) => Task.FromResult<object>(path.Contains("?", StringComparison.Ordinal)
            ? Array.Empty<화주운송의뢰응답>() : new 화주운송의뢰응답 { 의뢰Id = "r-old", 픽업지 = "허용된 주소" }) };
        var result = await new ShipperWorkspaceAdapter(new(api)).LoadAsync("r-old", default);
        Assert.Equal("r-old", result.SelectedId);
        Assert.Single(result.Items);
        Assert.All(api.GetCalls, call => Assert.Equal(RoleWorkspaceCatalog.Shipper, call.Role));
        Assert.Contains(api.GetCalls, call => call.Path.EndsWith("/r-old", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 도착_명령_직전_현재_운송_상태를_다시_검사한다()
    {
        var api = new Api { Get = (_, _, _) => Task.FromResult<object>(new 기사운송상세응답 { Id = 1, 상태 = "상차완료" }) };
        var adapter = new CargoDriverWorkspaceAdapter(new(api));
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.PerformAsync("1", "arrive-pickup", Guid.NewGuid(), default));
        Assert.Empty(api.PostCalls);
    }

    [Fact]
    public async Task 운송_조회_대상이_다르면_도착_명령을_보내지_않는다()
    {
        var api = new Api { Get = (_, _, _) => Task.FromResult<object>(new 기사운송상세응답 { Id = 2, 상태 = "배차확정" }) };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CargoDriverWorkspaceAdapter(new(api)).PerformAsync("1", "arrive-pickup", Guid.NewGuid(), default));
        Assert.Empty(api.PostCalls);
    }

    [Fact]
    public async Task 허용된_도착_명령만_화물기사_역할로_보낸다()
    {
        var api = new Api
        {
            Get = (_, _, _) => Task.FromResult<object>(new 기사운송상세응답 { Id = 1, 상태 = "배차확정" }),
            Post = (_, _, _, _) => Task.FromResult<object?>(new 기사운송상태변경응답 { Id = 1, 상태 = "상차지도착" })
        };
        await new CargoDriverWorkspaceAdapter(new(api)).PerformAsync("1", "arrive-pickup", Guid.NewGuid(), default);
        var command = Assert.Single(api.PostCalls);
        Assert.Equal(RoleWorkspaceCatalog.CargoDriver, command.Role); Assert.EndsWith("/1/arrive-pickup", command.Path);
    }

    [Fact]
    public async Task 창고_입고권한만_있으면_다른_공정은_조회하지_않는다()
    {
        var api = new Api
        {
            Post = (_, _, body, _) => Task.FromResult<object?>(new 창고작업진입확인응답 { IsAllowed = ((창고작업진입확인요청)body).ProcessCode == "inbound" }),
            Get = (_, path, _) => Task.FromResult<object>(path.EndsWith("warehouses", StringComparison.Ordinal) ? new 창고목록응답()
                : path.Contains("inspection-targets?", StringComparison.Ordinal) ? new 입고검수대상페이지응답()
                : path.EndsWith("/3", StringComparison.Ordinal) ? new 입고요청항목응답 { Id = 3, 창고Id = 9, 상태 = 입고상태코드.예정 }
                : new 입고요청목록응답 { Items = [new() { Id = 3, 창고Id = 9, 상태 = 입고상태코드.예정 }] })
        };
        var result = await new WarehouseWorkspaceAdapter(new(api)).LoadAsync("inbound:3", default);
        Assert.Single(result.Items); Assert.Contains("허용된", result.Message);
        Assert.Empty(result.Items[0].Markers!);
        Assert.DoesNotContain(api.GetCalls, call => call.Path.Contains("put-away") || call.Path.Contains("picking") || call.Path.Contains("packing") || call.Path.Contains("handoff"));
        Assert.All(api.GetCalls, call => Assert.Equal(RoleWorkspaceCatalog.Warehouse, call.Role));
    }

    [Fact]
    public async Task 창고_작업_권한_부족을_빈_목록으로_숨기지_않는다()
    {
        var api = new Api { Post = (_, _, _, _) => Task.FromResult<object?>(new 창고작업진입확인응답 { IsAllowed = false }) };
        var error = await Assert.ThrowsAsync<RoleWorkspaceAccessException>(() => new WarehouseWorkspaceAdapter(new(api)).LoadAsync(null, default));
        Assert.Equal(403, error.StatusCode); Assert.Empty(api.GetCalls);
    }

    [Fact]
    public async Task 다른_목록_페이지의_창고_업무는_명시한_상세를_조회한다()
    {
        var api = new Api
        {
            Post = (_, _, body, _) => Task.FromResult<object?>(new 창고작업진입확인응답 { IsAllowed = ((창고작업진입확인요청)body).ProcessCode == "inbound" }),
            Get = (_, path, _) => Task.FromResult<object>(path.EndsWith("warehouses", StringComparison.Ordinal) ? new 창고목록응답()
                : path.Contains("inspection-targets?", StringComparison.Ordinal) ? new 입고검수대상페이지응답()
                : path.EndsWith("/92", StringComparison.Ordinal) ? new 입고요청항목응답 { Id = 92, 창고Id = 9, 상태 = 입고상태코드.완료 }
                : new 입고요청목록응답())
        };
        var result = await new WarehouseWorkspaceAdapter(new(api)).LoadAsync("inbound:92", default);
        var item = Assert.Single(result.Items);
        Assert.Equal("inbound:92", item.Id); Assert.False(Assert.Single(item.Actions!).Enabled);
    }

    [Fact]
    public async Task 입력_도중_계정을_바꾸면_늦은_응답이_개인정보를_복원하지_않는다()
    {
        var response = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new Api { Get = (_, _, _) => response.Task };
        var access = new Access();
        using var model = new CargoTransportActionViewModel(new(api), api, access);
        var load = model.LoadAsync(1, "pickup");
        access.Switch("another-owner");
        response.SetResult(new 기사운송상세응답 { Id = 1, 상태 = "상차지도착", 출발지 = "이전 계정 주소" });
        await load;
        Assert.Null(model.Source); Assert.Null(model.PhotoName); Assert.False(model.CanSubmit);
    }

    [Fact]
    public async Task 상차_직전에_운송_단계가_바뀌면_사진_업로드와_완료를_실행하지_않는다()
    {
        var stage = "상차지도착";
        var api = new Api { Get = (_, _, _) => Task.FromResult<object>(new 기사운송상세응답 { Id = 1, 상태 = stage }) };
        using var model = new CargoTransportActionViewModel(new(api), api, new Access());
        await model.LoadAsync(1, "pickup");
        model.SetPhoto(new Photo()); model.CargoConfirmed = model.LocationConfirmed = model.RecipientConfirmed = model.SaveConfirmed = true;
        Assert.True(model.CanSubmit);
        stage = "운송중";
        await model.SubmitAsync();
        Assert.Empty(api.PostCalls); Assert.Equal(0, api.UploadCount); Assert.False(model.Saved); Assert.True(model.Lifetime.HasError);
    }

    [Fact]
    public async Task 상차_완료는_실제_업로드_결과를_첨부한_뒤_서버_상태를_재조회한다()
    {
        var stage = "상차지도착";
        var api = new Api
        {
            Get = (_, _, _) => Task.FromResult<object>(new 기사운송상세응답 { Id = 1, 상태 = stage }),
            Post = (_, _, body, _) =>
            {
                var completion = Assert.IsType<기사운송상차완료요청>(body);
                Assert.Equal("uploaded-object", completion.상차사진ObjectName);
                stage = "상차완료";
                return Task.FromResult<object?>(new 기사운송상태변경응답 { Id = 1, 상태 = stage });
            }
        };
        using var model = new CargoTransportActionViewModel(new(api), api, new Access());
        await model.LoadAsync(1, "pickup");
        model.SetPhoto(new Photo()); model.CargoConfirmed = model.LocationConfirmed = model.RecipientConfirmed = model.SaveConfirmed = true;
        await model.SubmitAsync();
        Assert.True(model.Saved); Assert.Equal("상차완료", model.Source!.상태); Assert.Null(model.PhotoName);
        Assert.Equal(1, api.UploadCount); Assert.Single(api.PostCalls);
        Assert.Equal(3, api.GetCalls.Count);
    }

    [Fact]
    public async Task 포장_직전에_서버의_허용_상태가_바뀌면_입력을_보내지_않는다()
    {
        var allowed = true;
        var api = new Api { Get = (_, _, _) => Task.FromResult<object>(new 포장작업상세응답 { InboundItemId = 8, ProductName = "물품", AvailableQuantity = 2, CanPack = allowed }) };
        using var model = new WarehouseTaskActionViewModel(new(api), new Access());
        await model.LoadAsync("packing", "8");
        model.FirstConfirmed = model.SecondConfirmed = model.SaveConfirmed = true;
        Assert.True(model.CanSubmit);
        allowed = false;
        await model.SubmitAsync();
        Assert.Empty(api.PostCalls); Assert.False(model.Saved); Assert.False(model.ServerAllows);
    }

    [Fact]
    public async Task 창고_입력_계정이_바뀌면_물품명과_위치와_확인상태를_삭제한다()
    {
        var access = new Access();
        var api = new Api { Get = (_, _, _) => Task.FromResult<object>(new 입고요청항목응답 { Id = 5, 예정상품명 = "이전 계정 물품", 예정수량 = 1, 상태 = 입고상태코드.예정 }) };
        using var model = new WarehouseTaskActionViewModel(new(api), access);
        await model.LoadAsync("inbound", "5");
        model.StorageLocation = "이전 계정 보관 위치"; model.FirstConfirmed = model.SaveConfirmed = true;
        access.Switch("another-owner");
        Assert.False(model.Loaded); Assert.Equal("", model.ProductName); Assert.Equal("", model.StorageLocation);
        Assert.False(model.FirstConfirmed); Assert.False(model.SaveConfirmed);
    }

    [Fact]
    public void 입력_복귀_URL에는_업무_ID만_포함한다()
    {
        var route = CargoWorkspaceRoutes.ReturnTo(RoleWorkspaceCatalog.Warehouse, "picking:batch-1");
        Assert.Equal("/workspace/warehouse?selected=picking%3Abatch-1", route);
        Assert.Equal(("picking", "batch:1"), WarehouseWorkspaceAdapter.ParseId("picking:batch:1"));
        Assert.Throws<ArgumentException>(() => WarehouseWorkspaceAdapter.ParseId("inbound:abc"));
    }

    [Fact]
    public void 입력_로그인은_실제_로그인_경로와_안전한_복귀_경로를_사용한다()
    {
        Assert.Equal("/workspace-login/cargo-driver?returnUrl=%2Fworkspace%2Fcargo-driver%2Ftransports%2F7%2Fpickup", CargoWorkspaceRoutes.DriverLogin(7, "pickup"));
        Assert.Equal("/workspace-login/warehouse?returnUrl=%2Fworkspace%2Fwarehouse%2Ftasks%2Fpicking%2Fbatch%253A1", CargoWorkspaceRoutes.WarehouseLogin("picking", "batch:1"));
        Assert.Equal("/workspace-login/cargo-driver?returnUrl=%2Fworkspace%2Fcargo-driver", CargoWorkspaceRoutes.DriverLogin(-1, "invalid"));
        Assert.Equal("/workspace-login/warehouse?returnUrl=%2Fworkspace%2Fwarehouse", CargoWorkspaceRoutes.WarehouseLogin("packing", "bad-id"));
        Assert.Equal("/workspace-login/cargo-driver?returnUrl=%2Fworkspace%2Fcargo-driver", CargoWorkspaceRoutes.OfferLogin(""));
    }

    [Fact]
    public void 화주_작성_검토와_최종_상세는_지도_선택_복귀_문맥을_보존한다()
    {
        var home = CargoWorkspaceRoutes.ReturnTo(RoleWorkspaceCatalog.Shipper, "request-8");
        var context = ShipperRequestNavigationContext.Parse("/shipper/request?from=" + Uri.EscapeDataString(home));
        foreach (var step in Enum.GetValues<ShipperRequestAuthoringStep>())
            context = ShipperRequestNavigationContext.Parse(context.PathFor(step));
        var detail = PageNavigationContext.WithReturnPath(ShipperRequestDetailPageRoutes.SummaryFor("created-9"), context.ReturnPath);
        Assert.Equal(home, ShipperRequestNavigationContext.Parse(detail).ReturnPath);
        Assert.Equal(home, ShipperRequestNavigationContext.Parse(PageNavigationContext.WithReturnPath(ShipperRequestPageRoutes.Bulk, context.ReturnPath)).ReturnPath);
        Assert.DoesNotContain("returnUrl", detail);
    }

    [Fact]
    public async Task 잘못된_업무_입력_URL은_조회하지_않고_오류로_표시한다()
    {
        var api = new Api();
        using var transport = new CargoTransportActionViewModel(new(api), api, new Access());
        using var warehouse = new WarehouseTaskActionViewModel(new(api), new Access());
        using var offer = new CargoOfferActionViewModel(new(api), new Access());
        await transport.LoadAsync(1, "invalid");
        await warehouse.LoadAsync("invalid", "8");
        await offer.LoadAsync("");
        Assert.True(transport.Lifetime.HasError); Assert.True(warehouse.Lifetime.HasError); Assert.True(offer.Lifetime.HasError);
        Assert.Empty(api.GetCalls); Assert.False(transport.CanSubmit); Assert.False(warehouse.CanSubmit); Assert.False(offer.CanSubmit);
    }

    [Theory]
    [InlineData("상차지도착", "상차지도착", false, false)]
    [InlineData("상차완료", "상차지도착", false, false)]
    [InlineData("상차완료", "상차완료", false, true)]
    public async Task 상차_응답과_재조회_완료상태가_다르면_성공으로_표시하지_않는다(string responseState, string storedState, bool reviewRequired, bool privacyHold)
    {
        var api = new Api();
        api.Get = (_, _, _) => Task.FromResult<object>(new 기사운송상세응답
        {
            Id = 1, 상태 = api.GetCalls.Count < 3 ? "상차지도착" : storedState,
            관리자확인필요 = api.GetCalls.Count >= 3 && reviewRequired,
            개인정보제공보류 = api.GetCalls.Count >= 3 && privacyHold
        });
        api.Post = (_, _, _, _) => Task.FromResult<object?>(new 기사운송상태변경응답 { Id = 1, 상태 = responseState });
        using var model = new CargoTransportActionViewModel(new(api), api, new Access());
        await model.LoadAsync(1, "pickup");
        model.SetPhoto(new Photo()); model.CargoConfirmed = model.LocationConfirmed = model.RecipientConfirmed = model.SaveConfirmed = true;
        await model.SubmitAsync();
        Assert.False(model.Saved); Assert.True(model.Lifetime.HasError); Assert.False(model.CanSubmit);
        Assert.Single(api.PostCalls);
        if (privacyHold) Assert.Null(model.Source);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 운송_문제신고는_예외와_관리자_확인_상태의_재조회까지_확인한다(bool stored)
    {
        var api = new Api();
        api.Get = (_, _, _) => Task.FromResult<object>(new 기사운송상세응답
        {
            Id = 1, 상태 = "상차지도착", 예외신고됨 = api.GetCalls.Count >= 3 && stored,
            관리자확인필요 = api.GetCalls.Count >= 3 && stored, 최근예외메시지 = api.GetCalls.Count >= 3 && stored ? "현장 물품 없음" : ""
        });
        api.Post = (_, _, _, _) => Task.FromResult<object?>(new 기사운송요약응답
        { Id = 1, 상태 = "상차지도착", 예외신고됨 = true, 관리자확인필요 = true, 최근예외메시지 = "현장 물품 없음" });
        using var model = new CargoTransportActionViewModel(new(api), api, new Access());
        await model.LoadAsync(1, "issue"); model.IssueReason = "현장 물품 없음"; model.SaveConfirmed = true;
        await model.SubmitAsync();
        Assert.Equal(stored, model.Saved); Assert.Equal(!stored, model.Lifetime.HasError); Assert.Single(api.PostCalls);
    }

    [Theory]
    [InlineData("inbound")]
    [InlineData("put-away")]
    [InlineData("picking")]
    [InlineData("packing")]
    [InlineData("handoff")]
    public async Task 창고_완료_응답이_있어도_재조회한_업무가_그대로면_성공으로_표시하지_않는다(string kind)
    {
        var time = DateTime.UtcNow;
        var api = new Api
        {
            Get = (_, path, _) => Task.FromResult<object>(path.EndsWith("/inspection-target", StringComparison.Ordinal)
                ? new 입고검수대상상세응답 { InboundItemId = 81, InboundId = 8, WarehouseId = 9, CanInspect = true }
                : path.Contains("outbound-plan-reviews/", StringComparison.Ordinal)
                    ? new 출고예정검토상세응답 { OutboundPlanId = 10, InboundItemId = 8 }
                : kind switch
            {
                "inbound" => new 입고요청항목응답 { Id = 8, 상태 = 입고상태코드.예정, 예정상품명 = "물품", 예정수량 = 2 },
                "put-away" => new 적재작업상세응답 { InboundItemId = 8, CanPutAway = true, ProductName = "물품", StorageLocation = "rack" },
                "picking" => new 피킹작업상세응답 { TaskKey = "8", Status = 피킹작업조회상태코드.진행중, CanComplete = true, RackCode = "rack" },
                "packing" => new 포장작업상세응답 { InboundItemId = 8, CanPack = true, ProductName = "물품", AvailableQuantity = 2 },
                _ => new 출고인계준비상세응답 { InboundItemId = 8, CanConfirmHandoff = true, AvailableQuantity = 2 }
            }),
            Post = (_, _, _, _) => Task.FromResult<object?>(kind switch
            {
                "inbound" => new 입고상품목록응답 { Items = [new() { Id = 81, 입고요청Id = 8, 창고Id = 9, 입고완료일시 = time }] },
                "put-away" => new 적재작업결과응답 { InboundItemId = 8, InventoryStatus = "적재완료", StorageLocation = "rack", PutAwayAtUtc = time },
                "picking" => new 피킹작업결과응답 { TaskKey = "8", Status = 피킹작업조회상태코드.완료, CompletedAtUtc = time },
                "packing" => new 포장작업결과응답 { InboundItemId = 8, InventoryStatus = "포장완료-일반포장", PackagingQuantity = 2, PackagingType = 포장유형코드.일반포장, PackedAtUtc = time },
                _ => new 출고인계준비결과응답 { InboundItemId = 8, OutboundPlanId = 10, OutboundStatus = "준비중", HandoffQuantity = 2, HandoffReadyAtUtc = time }
            })
        };
        using var model = new WarehouseTaskActionViewModel(new(api), new Access());
        await model.LoadAsync(kind, "8");
        model.StorageLocation = "rack"; model.FirstConfirmed = model.SecondConfirmed = model.SaveConfirmed = true;
        Assert.True(model.CanSubmit);
        await model.SubmitAsync();
        Assert.Single(api.PostCalls); Assert.False(model.Saved); Assert.True(model.Lifetime.HasError); Assert.False(model.CanSubmit);
        Assert.Equal(kind is "inbound" or "handoff" ? 4 : 3, api.GetCalls.Count);
    }

    [Fact]
    public async Task 창고_포장_완료는_재조회한_포장_일시와_방식이_확인된_후_성공으로_표시한다()
    {
        var packed = false; var time = DateTime.UtcNow;
        var api = new Api
        {
            Get = (_, _, _) => Task.FromResult<object>(new 포장작업상세응답
            { InboundItemId = 8, CanPack = !packed, ProductName = "물품", AvailableQuantity = 2, PackedAtUtc = packed ? time : null, PackingType = packed ? 포장유형코드.일반포장 : "" }),
            Post = (_, _, _, _) =>
            {
                packed = true;
                return Task.FromResult<object?>(new 포장작업결과응답
                { InboundItemId = 8, InventoryStatus = "포장완료-일반포장", PackagingQuantity = 2, PackagingType = 포장유형코드.일반포장, PackedAtUtc = time });
            }
        };
        using var model = new WarehouseTaskActionViewModel(new(api), new Access());
        await model.LoadAsync("packing", "8"); model.FirstConfirmed = model.SecondConfirmed = model.SaveConfirmed = true;
        await model.SubmitAsync();
        Assert.True(model.Saved); Assert.False(model.Lifetime.HasError); Assert.False(model.ServerAllows);
    }

    private sealed class Photo : IBrowserFile
    {
        public string Name => "현장.jpg";
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => 3;
        public string ContentType => "image/jpeg";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) => new MemoryStream([1, 2, 3]);
    }
    private sealed class Api : IRoleWorkspaceApi
    {
        public Func<string, string, CancellationToken, Task<object>> Get { get; set; } = (_, _, _) => throw new InvalidOperationException("Unexpected read");
        public Func<string, string, object, CancellationToken, Task<object?>> Post { get; set; } = (_, _, _, _) => throw new InvalidOperationException("Unexpected command");
        public List<(string Role, string Path)> GetCalls { get; } = [];
        public List<(string Role, string Path, object Body)> PostCalls { get; } = [];
        public int UploadCount { get; private set; }
        public async Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken)
        { GetCalls.Add((roleKey, path)); return (T)await Get(roleKey, path, cancellationToken); }
        public async Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
        { PostCalls.Add((roleKey, path, body)); var result = await Post(roleKey, path, body, cancellationToken); return result is null ? default : (T)result; }
        public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken) => PostAsync<T>(roleKey, path, body, cancellationToken);
        public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken cancellationToken)
        { UploadCount++; return Task.FromResult((T?)(object)new CargoEvidenceUploadResponse("uploaded-object", null)); }
    }
    private sealed class Access : IRoleWorkspaceAccess
    {
        private RoleWorkspaceIdentity _identity = new("owner", 1, true);
        public event Action? Changed;
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => _identity;
        public Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default) { Switch(null); return Task.CompletedTask; }
        public void Switch(string? owner) { _identity = new(owner, _identity.Revision + 1, owner is not null); Changed?.Invoke(); }
    }
}
