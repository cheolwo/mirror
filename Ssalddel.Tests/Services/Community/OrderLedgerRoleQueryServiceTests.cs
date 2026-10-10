using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Community;
using Ssalddel.Services.PrivacyRetention;
using System.Text.Json;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Warehouse;

namespace Ssalddel.Tests.Services.Community;

public sealed class OrderLedgerRoleQueryServiceTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task Purged_food_ledger_cannot_create_an_implicit_relation_but_keeps_explicit_history(
        bool rootPurged, bool explicitRelation)
    {
        var root = FoodLedger(음식주문상태코드.픽업완료);
        var child = Ledger("historical-custom-transport", CommunityLedgerTemplateKeys.FoodDelivery, "driver-1", "기사");
        child.외부참조 = new Dictionary<string, string>
        {
            ["원천Id"] = "existing-order", ["원천유형"] = "RestaurantFoodOrder"
        };
        var purged = rootPurged ? root : child;
        purged.확장속성 = new Dictionary<string, string>(purged.확장속성)
        {
            [배송원장개인정보파기Service.Marker] = "FoodOrder:existing-order"
        };
        root.포함원장목록 = explicitRelation
            ? [Reference(child.원장Id, child.원장템플릿Key, 주문원장포함역할.배송, 3)] : [];
        var stored = JsonSerializer.Serialize(root);
        var store = new FakeLedgerStore(root, child);
        var service = new 주문원장역할별조회Service(store, new FakeDisclosureStore());

        var result = await service.조회Async(root.원장Id, "orderer-1", 주문원장조회역할.주문자);

        Assert.True(result.IsSuccess);
        if (explicitRelation)
        {
            var item = Assert.Single(result.Value.관련원장목록);
            Assert.Equal(child.원장Id, item.원장Id);
            Assert.Equal(3, item.표시순서);
            Assert.Null(item.원장상세);
        }
        else Assert.Empty(result.Value.관련원장목록);
        Assert.Equal(rootPurged ? 0 : 1, store.ListReads);
        Assert.Equal(stored, JsonSerializer.Serialize(root));
    }

    [Theory]
    [InlineData("orderer-1", "주문자", false)]
    [InlineData("driver-1", "운송담당자", true)]
    public async Task Legacy_food_source_reference_is_read_only_and_keeps_role_disclosure(
        string userId, string role, bool childVisible)
    {
        var root = FoodLedger(음식주문상태코드.픽업완료);
        root.포함원장목록 = [];
        var child = Ledger("historical-custom-transport", CommunityLedgerTemplateKeys.FoodDelivery, "driver-1", "기사");
        child.외부참조 = new Dictionary<string, string> { ["원천Id"] = "existing-order", ["원천유형"] = "RestaurantFoodOrder" };
        var stored = JsonSerializer.Serialize(root);
        var service = new 주문원장역할별조회Service(new FakeLedgerStore(root, child), new FakeDisclosureStore());

        var result = await service.조회Async(root.원장Id, userId, role);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.관련원장목록);
        Assert.Equal(child.원장Id, item.원장Id);
        Assert.Equal(주문원장포함역할.배송, item.주문안역할);
        Assert.Equal(childVisible, item.상세조회가능여부);
        Assert.Equal(childVisible, item.원장상세 is not null);
        Assert.Equal(stored, JsonSerializer.Serialize(root));
        Assert.Empty(root.포함원장목록);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("type")]
    [InlineData("community")]
    [InlineData("template")]
    public async Task Legacy_food_lookup_rejects_unrelated_transport(string mismatch)
    {
        var root = FoodLedger(음식주문상태코드.픽업완료);
        root.포함원장목록 = [];
        var child = Ledger("historical-custom-transport", CommunityLedgerTemplateKeys.FoodDelivery, "driver-1", "기사");
        child.외부참조 = new Dictionary<string, string> { ["원천Id"] = mismatch == "source" ? "another-order" : "existing-order",
            ["원천유형"] = mismatch == "type" ? "CargoTransport" : "RestaurantFoodOrder" };
        if (mismatch == "community") child.커뮤니티Id = "another-community";
        if (mismatch == "template") child.원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport;
        var service = new 주문원장역할별조회Service(new FakeLedgerStore(root, child), new FakeDisclosureStore());

        var result = await service.조회Async(root.원장Id, "orderer-1", 주문원장조회역할.주문자);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.관련원장목록);
    }

    [Fact]
    public async Task Orderer_view_masks_other_owners_child_ledgers()
    {
        var ledgers = CreateLedgers();
        var service = new 주문원장역할별조회Service(ledgers, new FakeDisclosureStore());

        var result = await service.조회Async("order-1", "orderer-1", 주문원장조회역할.주문자);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.주문원장상세);
        Assert.Equal(2, result.Value.관련원장목록.Count);
        Assert.All(result.Value.관련원장목록, item =>
        {
            Assert.Null(item.원장상세);
            Assert.Equal(원장조회근거.공개요청필요, item.조회근거);
            Assert.True(item.공개요청가능여부);
        });
    }

    [Fact]
    public async Task Seller_view_returns_only_the_sales_ledger_owned_by_the_seller()
    {
        var service = new 주문원장역할별조회Service(CreateLedgers(), new FakeDisclosureStore());

        var result = await service.조회Async("order-1", "seller-1", 주문원장조회역할.판매자);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.관련원장목록);
        Assert.Equal(주문원장포함역할.판매, item.주문안역할);
        Assert.Equal(원장조회근거.소유자, item.조회근거);
        Assert.NotNull(item.원장상세);
        Assert.Null(result.Value.주문원장상세);
    }

    [Fact]
    public async Task Unrelated_user_cannot_enter_a_role_view()
    {
        var service = new 주문원장역할별조회Service(CreateLedgers(), new FakeDisclosureStore());

        var result = await service.조회Async("order-1", "stranger-1", 주문원장조회역할.운송담당자);

        Assert.True(result.IsFailed);
        Assert.Equal(403, result.Errors.Single().Metadata["StatusCode"]);
    }

    [Fact]
    public async Task Approved_disclosure_reveals_the_requested_child_ledger_to_the_orderer()
    {
        var ledgers = CreateLedgers();
        var disclosureStore = new FakeDisclosureStore();
        var disclosureService = new 주문원장공개요청Service(ledgers, disclosureStore);
        var queryService = new 주문원장역할별조회Service(ledgers, disclosureStore);

        var requested = await disclosureService.요청Async(
            "order-1",
            new 원장공개요청입력 { 대상원장Id = "sale-1", 사유 = "주문 이행 상태 확인" },
            "orderer-1");
        var decided = await disclosureService.결정Async(
            "order-1",
            requested.Value.요청Id,
            new 원장공개결정입력 { 승인여부 = true, 공개일수 = 7 },
            "seller-1");
        var viewed = await queryService.조회Async("order-1", "orderer-1", 주문원장조회역할.주문자);

        Assert.True(requested.IsSuccess);
        Assert.True(decided.IsSuccess);
        var sale = viewed.Value.관련원장목록.Single(x => x.원장Id == "sale-1");
        Assert.Equal(원장조회근거.승인공개, sale.조회근거);
        Assert.NotNull(sale.원장상세);
        Assert.False(sale.공개요청가능여부);
    }

    [Fact]
    public async Task User_who_does_not_own_the_target_ledger_cannot_approve_disclosure()
    {
        var ledgers = CreateLedgers();
        var service = new 주문원장공개요청Service(ledgers, new FakeDisclosureStore());
        var requested = await service.요청Async(
            "order-1",
            new 원장공개요청입력 { 대상원장Id = "sale-1", 사유 = "주문 이행 상태 확인" },
            "orderer-1");

        var result = await service.결정Async(
            "order-1",
            requested.Value.요청Id,
            new 원장공개결정입력 { 승인여부 = true },
            "driver-1");

        Assert.True(result.IsFailed);
        Assert.Equal(403, result.Errors.Single().Metadata["StatusCode"]);
    }

    [Theory]
    [InlineData(음식주문상태코드.주문대기, 커뮤니티원장상태.진행중)]
    [InlineData(음식주문상태코드.주문확인, 커뮤니티원장상태.진행중)]
    [InlineData(음식주문상태코드.기사배정, 커뮤니티원장상태.진행중)]
    [InlineData(음식주문상태코드.조리중, 커뮤니티원장상태.진행중)]
    [InlineData(음식주문상태코드.픽업대기, 커뮤니티원장상태.진행중)]
    [InlineData(음식주문상태코드.픽업완료, 커뮤니티원장상태.진행중)]
    [InlineData(음식주문상태코드.전달완료, 커뮤니티원장상태.진행중)]
    [InlineData(음식주문상태코드.수령확인, 커뮤니티원장상태.완료)]
    [InlineData(음식주문상태코드.거절, 커뮤니티원장상태.닫힘)]
    [InlineData(음식주문상태코드.취소, 커뮤니티원장상태.닫힘)]
    public async Task Food_role_read_uses_same_revision_block_without_changing_stored_summary(
        string foodState, string expectedStatus)
    {
        var root = FoodLedger(foodState);
        var storedJson = JsonSerializer.Serialize(root);
        var service = new 주문원장역할별조회Service(new FakeLedgerStore(root), new FakeDisclosureStore());

        var result = await service.조회Async(root.원장Id, "orderer-1", 주문원장조회역할.주문자);

        Assert.True(result.IsSuccess);
        var readRoot = Assert.IsType<커뮤니티원장Dto>(result.Value.주문원장상세);
        Assert.NotSame(root, readRoot);
        Assert.Equal(expectedStatus, result.Value.주문원장상태);
        Assert.Equal(expectedStatus, readRoot.상태);
        Assert.Equal(foodState, readRoot.현재단계Key);
        Assert.Equal(storedJson, JsonSerializer.Serialize(root));
        foreach (var property in typeof(커뮤니티원장Dto).GetProperties())
        {
            if (property.Name is not nameof(커뮤니티원장Dto.상태) and not nameof(커뮤니티원장Dto.현재단계Key))
            {
                Assert.Equal(property.GetValue(root), property.GetValue(readRoot));
            }
        }
        using var stored = JsonDocument.Parse(storedJson);
        using var read = JsonDocument.Parse(JsonSerializer.Serialize(readRoot));
        Assert.Equal(stored.RootElement.EnumerateObject().Select(x => x.Name),
            read.RootElement.EnumerateObject().Select(x => x.Name));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("blank")]
    [InlineData("duplicate")]
    [InlineData("wrong-block-type")]
    [InlineData("other-template")]
    public async Task Ambiguous_or_unrelated_food_summary_remains_unchanged(string caseName)
    {
        var root = FoodLedger(음식주문상태코드.수령확인);
        switch (caseName)
        {
            case "missing": root.블록목록 = []; break;
            case "unknown": root.블록목록[0].State = "미확인 상태"; break;
            case "blank": root.블록목록[0].State = null; break;
            case "duplicate": root.블록목록 = [root.블록목록[0], root.블록목록[0]]; break;
            case "wrong-block-type": root.블록목록[0].BlockType = CommunityLedgerBlockTypes.Generic; break;
            case "other-template": root.원장템플릿Key = CommunityLedgerTemplateKeys.Order; break;
        }
        var storedJson = JsonSerializer.Serialize(root);
        var service = new 주문원장역할별조회Service(new FakeLedgerStore(root), new FakeDisclosureStore());

        var result = await service.조회Async(root.원장Id, "orderer-1", 주문원장조회역할.주문자);

        Assert.True(result.IsSuccess);
        Assert.Same(root, result.Value.주문원장상세);
        Assert.Equal(root.상태, result.Value.주문원장상태);
        Assert.Equal(storedJson, JsonSerializer.Serialize(root));
    }

    [Fact]
    public async Task Food_block_does_not_reveal_state_to_a_child_only_role_or_unrelated_user()
    {
        var root = FoodLedger(음식주문상태코드.수령확인);
        root.상태 = 커뮤니티원장상태.진행중;
        root.포함원장목록 = [Reference("sale-1", CommunityLedgerTemplateKeys.LocalSale, 주문원장포함역할.판매, 0)];
        var store = new FakeLedgerStore(root,
            Ledger("sale-1", CommunityLedgerTemplateKeys.LocalSale, "seller-1", "판매자"));
        var service = new 주문원장역할별조회Service(store, new FakeDisclosureStore());

        var seller = await service.조회Async(root.원장Id, "seller-1", 주문원장조회역할.판매자);
        var stranger = await service.조회Async(root.원장Id, "stranger-1", 주문원장조회역할.주문자);

        Assert.True(seller.IsSuccess);
        Assert.Null(seller.Value.주문원장상세);
        Assert.Equal(커뮤니티원장상태.진행중, seller.Value.주문원장상태);
        Assert.NotNull(Assert.Single(seller.Value.관련원장목록).원장상세);
        Assert.True(stranger.IsFailed);
        Assert.Equal(403, stranger.Errors.Single().Metadata["StatusCode"]);
        Assert.Equal("픽업대기", root.현재단계Key);
    }

    [Theory]
    [InlineData(음식주문상태코드.주문대기, "음식점")]
    [InlineData(음식주문상태코드.조리중, "음식점")]
    [InlineData(음식주문상태코드.픽업완료, "기사")]
    [InlineData(음식주문상태코드.전달완료, "주문자")]
    [InlineData(음식주문상태코드.수령확인, "주문자")]
    public async Task Order_guidance_uses_protected_current_food_stage_without_completing_payment(string state, string actor)
    {
        var root = FoodLedger(state);
        root.외부참조 = new Dictionary<string, string> { ["음식주문번호"] = "FOOD / 1" };
        var original = JsonSerializer.Serialize(root);
        var result = await new 주문원장역할별조회Service(new FakeLedgerStore(root), new FakeDisclosureStore())
            .조회Async(root.원장Id, "orderer-1", 주문원장조회역할.주문자);
        Assert.True(result.IsSuccess);
        var guide = Assert.IsType<주문원장업무안내Dto>(result.Value.업무안내);
        Assert.Equal(actor, guide.담당역할);
        Assert.Equal("FoodOrder", guide.업무종류);
        Assert.Equal("FOOD / 1", guide.원천Id);
        Assert.Contains("각각", guide.완료구분안내);
        Assert.Equal(original, JsonSerializer.Serialize(root));
    }

    [Theory]
    [InlineData("FoodOrderNo")]
    [InlineData("orderNo")]
    [InlineData("block")]
    [InlineData("canonical-id")]
    public async Task Food_navigation_preserves_existing_source_identity_aliases(string source)
    {
        var root = FoodLedger(음식주문상태코드.전달완료);
        root.외부참조 = source is "block" or "canonical-id" ? new Dictionary<string, string>() : new Dictionary<string, string> { [source] = "existing-order" };
        if (source == "block") root.블록목록[0].Data = new Dictionary<string, string> { ["FoodOrderNo"] = "existing-order" };
        var result = await new 주문원장역할별조회Service(new FakeLedgerStore(root), new FakeDisclosureStore())
            .조회Async(root.원장Id, "orderer-1", 주문원장조회역할.주문자);
        Assert.Equal("FoodOrder", result.Value.업무안내!.업무종류);
        Assert.Equal("existing-order", result.Value.업무안내.원천Id);
    }

    [Fact]
    public async Task Hidden_child_state_and_personal_details_are_not_used_in_task_guidance()
    {
        var root = Ledger("root", CommunityLedgerTemplateKeys.Order, "orderer-1", "주문자");
        root.포함원장목록 = [Reference("private-child", CommunityLedgerTemplateKeys.CargoTransport, 주문원장포함역할.운송, 4)];
        var child = Ledger("private-child", CommunityLedgerTemplateKeys.CargoTransport, "private-driver", "개인 기사 이름");
        child.상태 = 커뮤니티원장상태.완료;
        child.현재단계Key = "개인 배송 위치";
        var result = await new 주문원장역할별조회Service(new FakeLedgerStore(root, child), new FakeDisclosureStore())
            .조회Async("root", "orderer-1", 주문원장조회역할.주문자);
        var guide = Assert.IsType<주문원장업무안내Dto>(result.Value.업무안내);
        Assert.Equal("업무 상태 확인 필요", guide.현재단계);
        Assert.Null(guide.원천Id);
        Assert.DoesNotContain("개인", JsonSerializer.Serialize(guide));
        Assert.Null(Assert.Single(result.Value.관련원장목록).원장상세);
        Assert.Equal(4, Assert.Single(result.Value.관련원장목록).표시순서);
    }

    [Fact]
    public async Task No_fulfillment_reference_is_not_shown_as_completed_order()
    {
        var root = Ledger("root", CommunityLedgerTemplateKeys.Order, "orderer-1", "주문자");
        var result = await new 주문원장역할별조회Service(new FakeLedgerStore(root), new FakeDisclosureStore())
            .조회Async("root", "orderer-1", 주문원장조회역할.주문자);
        Assert.Equal("이행 업무 연결 확인", result.Value.업무안내!.현재단계);
        Assert.Equal(root.상태, result.Value.주문원장상태);
        Assert.Null(result.Value.업무안내.업무종류);
    }

    [Theory]
    [InlineData(true, "Connected")]
    [InlineData(true, "Pending")]
    [InlineData(true, "Blocked")]
    [InlineData(false, "Connected")]
    public async Task Distributed_navigation_requires_an_accessible_child_and_verified_connection(bool accessible, string status)
    {
        var root = Ledger("root", CommunityLedgerTemplateKeys.Order, "orderer-1", "주문자");
        root.포함원장목록 = [Reference("transport-1", CommunityLedgerTemplateKeys.CargoTransport, 주문원장포함역할.운송, 0)];
        var child = Ledger("transport-1", CommunityLedgerTemplateKeys.CargoTransport,
            accessible ? "orderer-1" : "driver-1", "담당자");
        var adapter = new QueryOnlyConnectionAdapter(status);
        var result = await new 주문원장역할별조회Service(new FakeLedgerStore(root, child), new FakeDisclosureStore(), adapter)
            .조회Async("root", "orderer-1", 주문원장조회역할.주문자);
        Assert.True(result.IsSuccess);
        Assert.Equal(accessible ? 1 : 0, adapter.Reads);
        var guide = Assert.IsType<주문원장업무안내Dto>(result.Value.업무안내);
        if (accessible && status != "Blocked")
        {
            Assert.Equal("DistributedOrder", guide.업무종류);
            Assert.Equal("wave-1", guide.원천Id);
            if (status == "Pending") Assert.Equal("업무 연결 확인 필요", guide.현재단계);
        }
        else
        {
            Assert.Null(guide.업무종류);
            Assert.Null(guide.원천Id);
            if (accessible) Assert.Equal("업무 연결 권한 확인 필요", guide.현재단계);
        }
    }

    private sealed class QueryOnlyConnectionAdapter(string status) : I분산주문원장연결Adapter
    {
        public int Reads { get; private set; }
        public Task<DistributedOrderLedgerReferenceDto?> FindForChildAsync(string actorUserId, string rootOrderLedgerId, string childLedgerId, CancellationToken ct)
        {
            Assert.Equal("orderer-1", actorUserId); Assert.Equal("root", rootOrderLedgerId); Assert.Equal("transport-1", childLedgerId);
            Reads++;
            return Task.FromResult<DistributedOrderLedgerReferenceDto?>(new("wave-1", "order-1", rootOrderLedgerId, status));
        }
        public Task<DistributedRootOrderSnapshot> ValidateAsync(string actorUserId, string rootId, long expectedRevision, string sellerUserId, string customerUserId, CancellationToken ct)
            => throw new InvalidOperationException("Read must not validate or change the connection.");
        public Task<DistributedOrderLedgerConnectionState> SynchronizeAsync(string actorUserId, DistributedOrderLedgerConnection connection, CancellationToken ct)
            => throw new InvalidOperationException("Read must not synchronize the connection.");
        public Task<DistributedOrderLedgerConnectionState> ReadAsync(string actorUserId, DistributedOrderLedgerConnection connection, CancellationToken ct)
            => throw new InvalidOperationException("Only the narrow child read is allowed.");
    }

    private static 커뮤니티원장Dto FoodLedger(string foodState)
    {
        var root = Ledger("food-order:existing-order", CommunityLedgerTemplateKeys.FoodOrder, "orderer-1", "주문자");
        root.상태 = 커뮤니티원장상태.완료;
        root.현재단계Key = "픽업대기";
        root.Revision = 9;
        root.투영완료Revision = 9;
        root.투영상태 = 커뮤니티원장투영상태.완료;
        root.투영EventId = "projection-event";
        root.투영마지막오류 = "previous diagnostic";
        root.원함 = "주문 이행";
        root.대상OsCode = "Food";
        root.대상OsName = "음식 주문";
        root.생성시각Utc = new DateTime(2026, 10, 7, 6, 30, 23, DateTimeKind.Utc);
        root.수정시각Utc = new DateTime(2026, 10, 7, 15, 13, 18, DateTimeKind.Utc);
        root.블록목록 = [new() { BlockId = "food-order", BlockType = CommunityLedgerBlockTypes.Order, State = foodState }];
        root.참여자목록 = [new() { UserId = "orderer-1", RoleLabel = "주문자" }];
        root.포함원장목록 = [Reference("transport-1", CommunityLedgerTemplateKeys.FoodDelivery, 주문원장포함역할.배송, 0)];
        root.다이어그램스냅샷 = new() { DiagramId = "diagram-existing" };
        root.외부참조 = new Dictionary<string, string> { ["원천종류"] = "FoodOrder", ["원천주문번호"] = "existing-order" };
        root.확장속성 = new Dictionary<string, string> { ["표시설정"] = "keep" };
        root.상태이력 = [new() { 상태 = 커뮤니티원장상태.완료, 현재단계Key = "픽업대기" }];
        return root;
    }

    private static FakeLedgerStore CreateLedgers()
    {
        var root = Ledger("order-1", CommunityLedgerTemplateKeys.Order, "orderer-1", "주문자");
        root.포함원장목록 =
        [
            Reference("sale-1", CommunityLedgerTemplateKeys.LocalSale, 주문원장포함역할.판매, 0),
            Reference("transport-1", CommunityLedgerTemplateKeys.CargoTransport, 주문원장포함역할.운송, 1)
        ];
        return new FakeLedgerStore(
            root,
            Ledger("sale-1", CommunityLedgerTemplateKeys.LocalSale, "seller-1", "판매자"),
            Ledger("transport-1", CommunityLedgerTemplateKeys.CargoTransport, "driver-1", "운송 기사"));
    }

    private static 커뮤니티원장Dto Ledger(string id, string templateKey, string ownerId, string ownerName)
        => new()
        {
            원장Id = id,
            Revision = 1,
            커뮤니티Id = "platform",
            원장템플릿Key = templateKey,
            제목 = $"{id} 상세 정보",
            상태 = 커뮤니티원장상태.진행중,
            생성자UserId = ownerId,
            생성자표시명 = ownerName
        };

    private static 커뮤니티포함원장참조Dto Reference(
        string ledgerId,
        string templateKey,
        string role,
        int order)
        => new()
        {
            원장Id = ledgerId,
            원장템플릿Key = templateKey,
            역할 = role,
            필수여부 = true,
            표시순서 = order
        };

    private sealed class FakeLedgerStore : I커뮤니티원장저장소
    {
        private readonly Dictionary<string, 커뮤니티원장Dto> _ledgers;
        public int ListReads { get; private set; }

        public FakeLedgerStore(params 커뮤니티원장Dto[] ledgers)
        {
            _ledgers = ledgers.ToDictionary(x => x.원장Id, StringComparer.OrdinalIgnoreCase);
        }

        public Task<커뮤니티원장Dto?> 원장조회Async(string 원장Id, CancellationToken cancellationToken = default)
            => Task.FromResult(_ledgers.GetValueOrDefault(원장Id));

        public Task<커뮤니티원장Dto> 원장저장Async(커뮤니티원장저장요청 request, string updatedBy, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(커뮤니티원장조회조건 query, CancellationToken cancellationToken = default)
        {
            ListReads++;
            return Task.FromResult<IReadOnlyList<커뮤니티원장Dto>>(_ledgers.Values.ToArray());
        }

        public Task<커뮤니티원장Dto?> 원장상태변경Async(커뮤니티원장상태변경요청 request, string updatedBy, CancellationToken cancellationToken = default)
            => Task.FromResult<커뮤니티원장Dto?>(null);
    }

    private sealed class FakeDisclosureStore : I주문원장공개요청저장소
    {
        private readonly Dictionary<string, 원장공개요청기록> _records = new(StringComparer.OrdinalIgnoreCase);

        public Task<원장공개요청기록> 요청생성Async(원장공개요청기록 요청, CancellationToken cancellationToken = default)
        {
            _records[요청.요청Id] = 요청;
            return Task.FromResult(요청);
        }

        public Task<원장공개요청기록?> 요청조회Async(string 요청Id, CancellationToken cancellationToken = default)
            => Task.FromResult(_records.GetValueOrDefault(요청Id));

        public Task<IReadOnlyList<원장공개요청기록>> 받은요청목록Async(string 승인자UserId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<원장공개요청기록>>(
                _records.Values.Where(x => x.승인자UserId == 승인자UserId).ToArray());

        public Task<IReadOnlySet<string>> 승인된대상원장Ids조회Async(
            string 주문원장Id,
            string 요청자UserId,
            IEnumerable<string> 대상원장Ids,
            DateTimeOffset 기준시각Utc,
            CancellationToken cancellationToken = default)
        {
            var targetIds = 대상원장Ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
            IReadOnlySet<string> result = _records.Values
                .Where(x => x.주문원장Id == 주문원장Id
                            && x.요청자UserId == 요청자UserId
                            && x.상태 == 원장공개요청상태.승인
                            && x.만료시각Utc > 기준시각Utc
                            && targetIds.Contains(x.대상원장Id))
                .Select(x => x.대상원장Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(result);
        }

        public Task<원장공개요청기록?> 요청결정Async(
            string 요청Id,
            string 승인자UserId,
            bool 승인여부,
            string? 처리메모,
            DateTimeOffset 처리시각Utc,
            DateTimeOffset 승인만료시각Utc,
            CancellationToken cancellationToken = default)
        {
            var existing = _records.GetValueOrDefault(요청Id);
            if (existing is null || existing.승인자UserId != 승인자UserId || existing.상태 != 원장공개요청상태.승인대기)
            {
                return Task.FromResult<원장공개요청기록?>(null);
            }

            var updated = existing with
            {
                상태 = 승인여부 ? 원장공개요청상태.승인 : 원장공개요청상태.거절,
                처리시각Utc = 처리시각Utc,
                만료시각Utc = 승인여부 ? 승인만료시각Utc : existing.만료시각Utc,
                처리메모 = 처리메모
            };
            _records[요청Id] = updated;
            return Task.FromResult<원장공개요청기록?>(updated);
        }
    }
}
