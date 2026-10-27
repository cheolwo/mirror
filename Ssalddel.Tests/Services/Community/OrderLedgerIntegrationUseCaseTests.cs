using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.ContractManagement;
using Ssalddel.Services.Community;

namespace Ssalddel.Tests.Services.Community;

public sealed class OrderLedgerIntegrationUseCaseTests
{
    [Theory]
    [InlineData(CommunityLedgerTemplateKeys.FoodOrder)]
    [InlineData(CommunityLedgerTemplateKeys.SsalddelMart)]
    public async Task Status_search_and_counts_include_matching_orders_after_the_first_200(string template)
    {
        var rows = Enumerable.Range(0, 201).Select(index => Ledger($"order-{index}", template, 커뮤니티원장상태.완료)).ToArray();
        if (template == CommunityLedgerTemplateKeys.FoodOrder)
            foreach (var row in rows) row.블록목록 = [new() { BlockId = "food-order", BlockType = CommunityLedgerBlockTypes.Order,
                State = row == rows[^1] ? "전달완료" : "수령확인" }];
        else rows[^1].상태 = 커뮤니티원장상태.진행중;
        var service = new 주문원장통합UseCase(new FakeLedgerStore(rows));

        var result = await service.주문자목록조회Async("user-1", new() { 상태 = 커뮤니티원장상태.진행중 });

        Assert.Equal(rows[^1].원장Id, Assert.Single(result.Value.Items).원장Id);
        Assert.Equal(1, result.Value.TotalCount);
        Assert.Equal(1, result.Value.원장종류목록.Single(x => x.원장템플릿Key == template).내원장수);
    }

    [Theory]
    [InlineData("전달완료", 커뮤니티원장상태.진행중)]
    [InlineData("수령확인", 커뮤니티원장상태.완료)]
    [InlineData("취소", 커뮤니티원장상태.닫힘)]
    public async Task Food_list_filter_count_and_detail_use_the_same_revision_block(string stage, string expected)
    {
        var root = Ledger("food-order:read-1", CommunityLedgerTemplateKeys.FoodOrder, 커뮤니티원장상태.완료, 7);
        root.현재단계Key = "픽업대기";
        root.블록목록 = [new() { BlockId = "food-order", BlockType = CommunityLedgerBlockTypes.Order, State = stage }];
        var stored = System.Text.Json.JsonSerializer.Serialize(root);
        var service = new 주문원장통합UseCase(new FakeLedgerStore(root));

        var list = await service.주문자목록조회Async("user-1", new() { 상태 = expected });
        var detail = await service.조회Async(root.원장Id);
        var excluded = await service.주문자목록조회Async("user-1", new() { 상태 = expected == 커뮤니티원장상태.완료
            ? 커뮤니티원장상태.진행중 : 커뮤니티원장상태.완료 });

        Assert.True(list.IsSuccess);
        var item = Assert.Single(list.Value.Items);
        Assert.Equal(expected, item.상태);
        Assert.Equal(stage, item.현재단계Key);
        Assert.Equal(1, list.Value.TotalCount);
        Assert.Equal(1, list.Value.원장종류목록.Single(x => x.원장템플릿Key == CommunityLedgerTemplateKeys.FoodOrder).내원장수);
        Assert.Equal(expected, detail.Value.주문원장.상태);
        Assert.Equal(item.Revision, detail.Value.주문원장.Revision);
        Assert.Empty(excluded.Value.Items);
        Assert.Equal(stored, System.Text.Json.JsonSerializer.Serialize(root));
    }

    [Fact]
    public async Task Link_child_ledger_adds_reference_to_order_root()
    {
        var store = new FakeLedgerStore(
            Ledger("order-1", CommunityLedgerTemplateKeys.Order, revision: 3),
            Ledger("transport-1", CommunityLedgerTemplateKeys.CargoTransport, state: 커뮤니티원장상태.진행중));
        var useCase = new 주문원장통합UseCase(store);

        var result = await useCase.하위원장연결Async(
            "order-1",
            new 주문하위원장연결요청
            {
                하위원장Id = "transport-1",
                역할 = 주문원장포함역할.운송,
                필수여부 = true,
                기대Revision = 3
            },
            "user-1");

        Assert.True(result.IsSuccess);
        var reference = Assert.Single(result.Value.주문원장.포함원장목록);
        Assert.Equal("transport-1", reference.원장Id);
        Assert.Equal(CommunityLedgerTemplateKeys.CargoTransport, reference.원장템플릿Key);
        Assert.Equal(4, result.Value.주문원장.Revision);
        Assert.Equal("user-1", store.LastUpdatedBy);
    }

    [Fact]
    public async Task Individual_import_is_linked_as_an_extension_of_the_individual_order()
    {
        var store = new FakeLedgerStore(
            Ledger("order-1", CommunityLedgerTemplateKeys.Order, revision: 2),
            Ledger("individual-import-1", CommunityLedgerTemplateKeys.IndividualImport));
        var useCase = new 주문원장통합UseCase(store);

        var result = await useCase.하위원장연결Async(
            "order-1",
            new 주문하위원장연결요청
            {
                하위원장Id = "individual-import-1",
                역할 = 주문원장포함역할.개별수입,
                필수여부 = true,
                기대Revision = 2
            },
            "user-1");

        Assert.True(result.IsSuccess);
        var reference = Assert.Single(result.Value.주문원장.포함원장목록);
        Assert.Equal(CommunityLedgerTemplateKeys.IndividualImport, reference.원장템플릿Key);
        Assert.Equal(주문원장포함역할.개별수입, reference.역할);
        Assert.Equal(CommunityLedgerRelationTypes.Contains, reference.관계유형);
    }

    [Theory]
    [InlineData(CommunityLedgerTemplateKeys.FoodOrder, CommunityLedgerTemplateKeys.IndividualImport, 주문원장포함역할.개별수입)]
    [InlineData(CommunityLedgerTemplateKeys.Order, CommunityLedgerTemplateKeys.IndividualImport, 주문원장포함역할.운송)]
    [InlineData(CommunityLedgerTemplateKeys.Order, CommunityLedgerTemplateKeys.CargoTransport, 주문원장포함역할.개별수입)]
    public void Individual_import_extension_rejects_another_root_template_or_role(
        string rootTemplateKey,
        string childTemplateKey,
        string role)
    {
        var request = new 커뮤니티원장저장요청
        {
            원장Id = "root-1",
            원장템플릿Key = rootTemplateKey,
            제목 = "주문",
            포함원장목록 =
            [
                new()
                {
                    원장Id = "child-1",
                    원장템플릿Key = childTemplateKey,
                    역할 = role,
                    관계유형 = CommunityLedgerRelationTypes.Contains
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => 주문원장구성정책.저장요청검증(request));

        Assert.Contains("개별주문 원장(order)의 수입 이행 확장", exception.Message);
    }

    [Fact]
    public async Task Individual_export_is_linked_as_an_extension_of_the_individual_order()
    {
        var store = new FakeLedgerStore(
            Ledger("order-1", CommunityLedgerTemplateKeys.Order, revision: 2),
            Ledger("individual-export-1", CommunityLedgerTemplateKeys.IndividualExport));
        var useCase = new 주문원장통합UseCase(store);

        var result = await useCase.하위원장연결Async(
            "order-1",
            new 주문하위원장연결요청
            {
                하위원장Id = "individual-export-1",
                역할 = 주문원장포함역할.개별수출,
                필수여부 = true,
                기대Revision = 2
            },
            "user-1");

        Assert.True(result.IsSuccess);
        var reference = Assert.Single(result.Value.주문원장.포함원장목록);
        Assert.Equal(CommunityLedgerTemplateKeys.IndividualExport, reference.원장템플릿Key);
        Assert.Equal(주문원장포함역할.개별수출, reference.역할);
        Assert.Equal(CommunityLedgerRelationTypes.Contains, reference.관계유형);
    }

    [Fact]
    public async Task Group_export_aggregates_individual_exports_without_replacing_their_ledgers()
    {
        var store = new FakeLedgerStore(
            Ledger("group-export-1", CommunityLedgerTemplateKeys.GroupExport),
            Ledger("individual-export-1", CommunityLedgerTemplateKeys.IndividualExport),
            Ledger("individual-export-2", CommunityLedgerTemplateKeys.IndividualExport));
        var useCase = new 주문원장통합UseCase(store);

        var first = await useCase.하위원장연결Async(
            "group-export-1",
            new 주문하위원장연결요청
            {
                하위원장Id = "individual-export-1",
                역할 = 주문원장포함역할.개별수출,
                필수여부 = true
            },
            "user-1");
        var second = await useCase.하위원장연결Async(
            "group-export-1",
            new 주문하위원장연결요청
            {
                하위원장Id = "individual-export-2",
                역할 = 주문원장포함역할.개별수출,
                필수여부 = true
            },
            "user-1");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, second.Value.포함원장목록.Count);
        Assert.All(second.Value.포함원장목록, item =>
        {
            Assert.Equal(CommunityLedgerTemplateKeys.IndividualExport, item.원장템플릿Key);
            Assert.Equal(주문원장포함역할.개별수출, item.역할);
            Assert.NotNull(item.원장);
        });
    }

    [Theory]
    [InlineData(CommunityLedgerTemplateKeys.FoodOrder, CommunityLedgerTemplateKeys.IndividualExport, 주문원장포함역할.개별수출)]
    [InlineData(CommunityLedgerTemplateKeys.Order, CommunityLedgerTemplateKeys.IndividualExport, 주문원장포함역할.운송)]
    [InlineData(CommunityLedgerTemplateKeys.Order, CommunityLedgerTemplateKeys.CargoTransport, 주문원장포함역할.개별수출)]
    [InlineData(CommunityLedgerTemplateKeys.GroupExport, CommunityLedgerTemplateKeys.Order, 주문원장포함역할.개별수출)]
    [InlineData(CommunityLedgerTemplateKeys.GroupExport, CommunityLedgerTemplateKeys.IndividualExport, 주문원장포함역할.운송)]
    public void Individual_export_extension_rejects_another_root_child_or_role(
        string rootTemplateKey,
        string childTemplateKey,
        string role)
    {
        var request = new 커뮤니티원장저장요청
        {
            원장Id = "root-1",
            원장템플릿Key = rootTemplateKey,
            제목 = "수출",
            포함원장목록 =
            [
                new()
                {
                    원장Id = "child-1",
                    원장템플릿Key = childTemplateKey,
                    역할 = role,
                    관계유형 = CommunityLedgerRelationTypes.Contains
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => 주문원장구성정책.저장요청검증(request));

        Assert.Contains("개별주문 원장의 수출 이행 확장", exception.Message);
        Assert.Contains("공동수출 원장", exception.Message);
    }

    [Fact]
    public void Group_export_rejects_non_export_child_ledger()
    {
        var request = new 커뮤니티원장저장요청
        {
            원장Id = "group-export-1",
            원장템플릿Key = CommunityLedgerTemplateKeys.GroupExport,
            제목 = "공동수출",
            포함원장목록 =
            [
                new()
                {
                    원장Id = "transport-1",
                    원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport,
                    역할 = 주문원장포함역할.운송,
                    관계유형 = CommunityLedgerRelationTypes.Contains
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => 주문원장구성정책.저장요청검증(request));

        Assert.Contains("개별수출 원장만", exception.Message);
    }

    [Fact]
    public async Task Integrated_query_reads_current_child_state_instead_of_copied_state()
    {
        var root = Ledger("order-1", CommunityLedgerTemplateKeys.Order);
        root.포함원장목록 =
        [
            new()
            {
                원장Id = "delivery-1",
                원장템플릿Key = CommunityLedgerTemplateKeys.FoodDelivery,
                역할 = 주문원장포함역할.배송,
                필수여부 = true,
                표시순서 = 0
            }
        ];
        var child = Ledger("delivery-1", CommunityLedgerTemplateKeys.FoodDelivery, state: 커뮤니티원장상태.완료);
        var rootState = root.상태;
        var rootRevision = root.Revision;
        var store = new FakeLedgerStore(root, child);
        var useCase = new 주문원장통합UseCase(store);

        var result = await useCase.조회Async("order-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.완료하위원장수);
        Assert.True(result.Value.필수하위원장완료여부);
        Assert.Equal(커뮤니티원장상태.완료, result.Value.포함원장목록.Single().원장!.상태);

        // A query must not promote the root or retain a copied child completion.
        child.상태 = "진행중";
        var current = await useCase.조회Async("order-1");
        Assert.True(current.IsSuccess);
        Assert.Equal(0, current.Value.완료하위원장수);
        Assert.False(current.Value.필수하위원장완료여부);
        Assert.Equal(rootState, root.상태);
        Assert.Equal(rootRevision, root.Revision);
        Assert.Null(store.LastUpdatedBy);
    }

    [Fact]
    public async Task 주문자목록은_기존원장에서_네종류만순서대로분류하고실행경계를표시한다()
    {
        var store = new FakeLedgerStore(
            Ledger("food-1", CommunityLedgerTemplateKeys.FoodOrder),
            Ledger("mart-1", CommunityLedgerTemplateKeys.SsalddelMart),
            Ledger("together-1", CommunityLedgerTemplateKeys.GroupOrder),
            Ledger("import-1", CommunityLedgerTemplateKeys.GroupImport),
            Ledger("transport-1", CommunityLedgerTemplateKeys.CargoTransport));
        var useCase = new 주문원장통합UseCase(store);

        var result = await useCase.주문자목록조회Async(
            "user-1",
            new 주문자원장목록조회요청 { PageSize = 20 });

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ["음식 주문 원장", "살뜰 마트 주문 원장", "같이 주문 원장", "같이 수입 원장"],
            result.Value.원장종류목록.Select(item => item.원장종류명));
        Assert.Equal(4, result.Value.TotalCount);
        Assert.DoesNotContain(result.Value.Items, item => item.원장Id == "transport-1");
        Assert.Equal(
            주문자원장실행경계코드.실제주문,
            result.Value.Items.Single(item => item.원장Id == "food-1").실행경계코드);
        Assert.Equal(
            주문자원장실행경계코드.집계원장,
            result.Value.Items.Single(item => item.원장Id == "together-1").실행경계코드);
        Assert.Equal(
            주문자원장실행경계코드.수입준비,
            result.Value.Items.Single(item => item.원장Id == "import-1").실행경계코드);
    }

    [Fact]
    public async Task Link_rejects_child_from_another_community()
    {
        var root = Ledger("order-1", CommunityLedgerTemplateKeys.Order);
        var child = Ledger("transport-1", CommunityLedgerTemplateKeys.CargoTransport);
        child.커뮤니티Id = "another-community";
        var useCase = new 주문원장통합UseCase(new FakeLedgerStore(root, child));

        var result = await useCase.하위원장연결Async(
            "order-1",
            new 주문하위원장연결요청
            {
                하위원장Id = "transport-1",
                역할 = 주문원장포함역할.운송
            },
            "user-1");

        Assert.True(result.IsFailed);
        Assert.Contains("같은 커뮤니티", result.Errors.Single().Message);
    }

    [Fact]
    public void Save_policy_rejects_duplicate_child_references()
    {
        var request = new 커뮤니티원장저장요청
        {
            원장Id = "order-1",
            원장템플릿Key = CommunityLedgerTemplateKeys.Order,
            제목 = "주문",
            포함원장목록 =
            [
                Reference("transport-1", 0),
                Reference("transport-1", 1)
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => 주문원장구성정책.저장요청검증(request));

        Assert.Contains("중복", exception.Message);
    }

    [Fact]
    public async Task Legacy_group_purchase_can_still_read_direct_individual_order_links()
    {
        var store = new FakeLedgerStore(
            Ledger("group-1", CommunityLedgerTemplateKeys.GroupPurchase),
            Ledger("order-1", CommunityLedgerTemplateKeys.Order));
        var useCase = new 주문원장통합UseCase(store);

        var result = await useCase.하위원장연결Async(
            "group-1",
            new 주문하위원장연결요청
            {
                하위원장Id = "order-1",
                역할 = 주문원장포함역할.개별주문,
                필수여부 = true
            },
            "user-1");

        Assert.True(result.IsSuccess);
        var reference = Assert.Single(result.Value.포함원장목록);
        Assert.Equal(주문원장포함역할.개별주문, reference.역할);
        Assert.Equal(CommunityLedgerTemplateKeys.Order, reference.원장템플릿Key);
    }

    [Fact]
    public async Task Group_purchase_contains_order_aggregation_and_aggregation_contains_individual_orders()
    {
        var store = new FakeLedgerStore(
            Ledger("group-1", CommunityLedgerTemplateKeys.GroupPurchase),
            Ledger("aggregation-1", CommunityLedgerTemplateKeys.GroupOrder),
            Ledger("order-1", CommunityLedgerTemplateKeys.Order));
        var useCase = new 주문원장통합UseCase(store);

        var groupResult = await useCase.하위원장연결Async(
            "group-1",
            new 주문하위원장연결요청
            {
                하위원장Id = "aggregation-1",
                역할 = 주문원장포함역할.주문집계,
                필수여부 = true
            },
            "user-1");
        var aggregationResult = await useCase.하위원장연결Async(
            "aggregation-1",
            new 주문하위원장연결요청
            {
                하위원장Id = "order-1",
                역할 = 주문원장포함역할.개별주문,
                필수여부 = true
            },
            "user-1");

        Assert.True(groupResult.IsSuccess);
        Assert.True(aggregationResult.IsSuccess);
        Assert.Equal(주문원장포함역할.주문집계, Assert.Single(groupResult.Value.포함원장목록).역할);
        Assert.Equal(주문원장포함역할.개별주문, Assert.Single(aggregationResult.Value.포함원장목록).역할);
    }

    [Fact]
    public async Task Group_purchase_rejects_direct_transport_ledger_link()
    {
        var store = new FakeLedgerStore(
            Ledger("group-1", CommunityLedgerTemplateKeys.GroupPurchase),
            Ledger("transport-1", CommunityLedgerTemplateKeys.CargoTransport));
        var useCase = new 주문원장통합UseCase(store);

        var result = await useCase.하위원장연결Async(
            "group-1",
            new 주문하위원장연결요청
            {
                하위원장Id = "transport-1",
                역할 = 주문원장포함역할.운송
            },
            "user-1");

        Assert.True(result.IsFailed);
        Assert.Contains("개별 주문 원장만", result.Errors.Single().Message);
    }

    [Fact]
    public void Individual_order_rejects_individual_order_role()
    {
        var request = new 커뮤니티원장저장요청
        {
            원장Id = "order-1",
            원장템플릿Key = CommunityLedgerTemplateKeys.Order,
            제목 = "개별 주문",
            포함원장목록 =
            [
                new()
                {
                    원장Id = "order-2",
                    원장템플릿Key = CommunityLedgerTemplateKeys.Order,
                    역할 = 주문원장포함역할.개별주문
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => 주문원장구성정책.저장요청검증(request));

        Assert.Contains("같이 주문 원장에서만", exception.Message);
    }

    [Fact]
    public async Task Individual_order_signature_is_stored_on_the_order_ledger_boundary()
    {
        var ledgerStore = new FakeLedgerStore(Ledger("order-1", CommunityLedgerTemplateKeys.Order));
        var signatureStore = new FakeSignatureStore();
        var useCase = new 주문원장서명UseCase(ledgerStore, signatureStore);

        var prepared = await useCase.서명요청준비Async(
            "order-1",
            new 주문원장서명요청준비요청
            {
                계약문서번호 = "ORDER-2026-0001",
                문서Hash = "sha256:order-document",
                만료시각Utc = DateTimeOffset.UtcNow.AddDays(1)
            },
            "user-1");
        var signed = await useCase.서명등록Async(
            "order-1",
            new 주문원장서명등록요청
            {
                문서Hash = "sha256:order-document",
                동의문Hash = "sha256:consent",
                서명증적Hash = "sha256:evidence",
                기대Revision = prepared.Value.Revision
            },
            "user-1");

        Assert.True(prepared.IsSuccess);
        Assert.Equal(ContractSignatureStatusCode.WaitingForSignature, prepared.Value.상태Code);
        Assert.True(signed.IsSuccess);
        Assert.True(signed.Value.전체서명완료여부);
        Assert.Equal(ContractSignatureStatusCode.Signed, signed.Value.상태Code);
        Assert.Single(signatureStore.Records["order-1"].서명묶음.Evidences);
    }

    [Fact]
    public async Task Group_purchase_query_aggregates_each_individual_order_signature()
    {
        var group = Ledger("group-1", CommunityLedgerTemplateKeys.GroupPurchase);
        group.포함원장목록 =
        [
            IndividualOrderReference("order-1", 0),
            IndividualOrderReference("order-2", 1)
        ];
        var ledgerStore = new FakeLedgerStore(
            group,
            Ledger("order-1", CommunityLedgerTemplateKeys.Order),
            Ledger("order-2", CommunityLedgerTemplateKeys.Order));
        var now = DateTimeOffset.UtcNow;
        var signedBundle = SignatureBundle("order-1", "user-1", now);
        signedBundle = ContractElectronicSignaturePlanner.AddEvidence(
            signedBundle,
            new ContractSignatureEvidence(
                "user-1",
                "익명 참여자",
                ContractSignatureMethodCode.PlatformClickSign,
                signedBundle.DocumentHash,
                "sha256:consent",
                "sha256:evidence",
                now));
        var signatureStore = new FakeSignatureStore(
            new 주문원장서명기록("order-1", "platform", 2, signedBundle, "user-1", now),
            new 주문원장서명기록("order-2", "platform", 1, SignatureBundle("order-2", "user-2", now), "user-2", now));
        var useCase = new 주문원장통합UseCase(ledgerStore, signatureStore);

        var result = await useCase.조회Async("group-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.서명대상주문수);
        Assert.Equal(1, result.Value.서명완료주문수);
        Assert.Equal(["order-2"], result.Value.미서명주문Ids);
        Assert.False(result.Value.전체주문서명완료여부);
        Assert.True(result.Value.포함원장목록.Single(x => x.원장Id == "order-1").주문자서명상태?.전체서명완료여부);
    }

    [Fact]
    public async Task Group_purchase_bundle_cannot_be_signed_instead_of_individual_orders()
    {
        var useCase = new 주문원장서명UseCase(
            new FakeLedgerStore(Ledger("group-1", CommunityLedgerTemplateKeys.GroupPurchase)),
            new FakeSignatureStore());

        var result = await useCase.서명요청준비Async(
            "group-1",
            new 주문원장서명요청준비요청
            {
                계약문서번호 = "GROUP-2026-0001",
                문서Hash = "sha256:group-document"
            },
            "user-1");

        Assert.True(result.IsFailed);
        Assert.Contains("개별 주문 원장에만", result.Errors.Single().Message);
    }

    [Theory]
    [InlineData(커뮤니티원장상태.진행중)]
    [InlineData(커뮤니티원장상태.보류)]
    [InlineData(커뮤니티원장상태.완료)]
    [InlineData(커뮤니티원장상태.닫힘)]
    [InlineData("알수없는상태")]
    public async Task Started_child_cannot_be_detached_or_made_optional(string state)
    {
        var root = Ledger("root", CommunityLedgerTemplateKeys.Order, revision: 7);
        root.포함원장목록 = [new() { 원장Id = "child", 원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport,
            역할 = 주문원장포함역할.운송, 필수여부 = true }];
        var store = new FakeLedgerStore(root, Ledger("child", CommunityLedgerTemplateKeys.CargoTransport, state));
        var useCase = new 주문원장통합UseCase(store);
        var detach = await useCase.하위원장분리Async("root", "child", 7, "user-1");
        var downgrade = await useCase.하위원장연결Async("root", new()
            { 하위원장Id = "child", 역할 = 주문원장포함역할.운송, 필수여부 = false, 기대Revision = 7 }, "user-1");
        Assert.True(detach.IsFailed);
        Assert.True(downgrade.IsFailed);
        Assert.Equal(409, detach.Errors.Single().Metadata["StatusCode"]);
        Assert.Equal(7, root.Revision);
        Assert.True(Assert.Single(root.포함원장목록).필수여부);
        Assert.Null(store.LastUpdatedBy);
    }

    [Fact]
    public async Task Same_connection_replay_does_not_save_or_increment_revision()
    {
        var root = Ledger("root", CommunityLedgerTemplateKeys.Order, 커뮤니티원장상태.진행중, 7);
        root.포함원장목록 = [new() { 원장Id = "child", 원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport,
            역할 = 주문원장포함역할.운송, 필수여부 = true, 표시순서 = 3 }];
        var store = new FakeLedgerStore(root, Ledger("child", CommunityLedgerTemplateKeys.CargoTransport, 커뮤니티원장상태.진행중));
        var result = await new 주문원장통합UseCase(store).하위원장연결Async("root", new()
            { 하위원장Id = "child", 역할 = 주문원장포함역할.운송, 필수여부 = true, 기대Revision = 7 }, "user-1");
        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value.주문원장.Revision);
        Assert.Null(store.LastUpdatedBy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Absent_required_work_cannot_be_reported_as_complete(bool missingChild)
    {
        var root = Ledger("root", CommunityLedgerTemplateKeys.Order);
        if (missingChild) root.포함원장목록 = [new() { 원장Id = "missing", 원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport,
            역할 = 주문원장포함역할.운송, 필수여부 = true }];
        var result = await new 주문원장통합UseCase(new FakeLedgerStore(root)).조회Async("root");
        Assert.True(result.IsSuccess);
        Assert.False(result.Value.필수하위원장완료여부);
        Assert.Equal(missingChild ? "원장누락" : "필수업무미지정", result.Value.필수하위원장판정);
        Assert.Equal(커뮤니티원장상태.초안, root.상태);
    }

    [Fact]
    public async Task Draft_child_can_be_removed_but_missing_child_requires_reconciliation()
    {
        var root = Ledger("root", CommunityLedgerTemplateKeys.Order);
        root.포함원장목록 = [new() { 원장Id = "child", 원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport, 역할 = 주문원장포함역할.운송 }];
        var store = new FakeLedgerStore(root, Ledger("child", CommunityLedgerTemplateKeys.CargoTransport));
        var result = await new 주문원장통합UseCase(store).하위원장분리Async("root", "child", 1, "user-1");
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.주문원장.포함원장목록);
        var missing = new 주문원장통합UseCase(new FakeLedgerStore(root));
        var rejected = await missing.하위원장분리Async("root", "child", 1, "user-1");
        Assert.True(rejected.IsFailed);
    }

    private static 커뮤니티포함원장참조Dto Reference(string ledgerId, int order)
        => new()
        {
            원장Id = ledgerId,
            원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport,
            역할 = 주문원장포함역할.운송,
            표시순서 = order
        };

    private static 커뮤니티포함원장참조Dto IndividualOrderReference(string ledgerId, int order)
        => new()
        {
            원장Id = ledgerId,
            원장템플릿Key = CommunityLedgerTemplateKeys.Order,
            역할 = 주문원장포함역할.개별주문,
            필수여부 = true,
            표시순서 = order
        };

    private static ContractElectronicSignatureBundle SignatureBundle(
        string orderLedgerId,
        string userId,
        DateTimeOffset now)
        => ContractElectronicSignaturePlanner.CreateBundle(
            $"SIGN-{orderLedgerId}",
            $"sha256:{orderLedgerId}",
            [new ContractSignatureRequest(userId, "Orderer", "익명 참여자", true, now)],
            now,
            now.AddDays(1));

    private static 커뮤니티원장Dto Ledger(
        string id,
        string templateKey,
        string state = 커뮤니티원장상태.초안,
        long revision = 1)
        => new()
        {
            원장Id = id,
            Revision = revision,
            커뮤니티Id = "platform",
            원장템플릿Key = templateKey,
            제목 = id,
            상태 = state,
            생성자UserId = "user-1",
            생성자표시명 = "익명 참여자"
        };

    private sealed class FakeLedgerStore : I커뮤니티원장저장소
    {
        private readonly Dictionary<string, 커뮤니티원장Dto> _ledgers;

        public FakeLedgerStore(params 커뮤니티원장Dto[] ledgers)
        {
            _ledgers = ledgers.ToDictionary(x => x.원장Id, StringComparer.OrdinalIgnoreCase);
        }

        public string? LastUpdatedBy { get; private set; }

        public Task<커뮤니티원장Dto> 원장저장Async(
            커뮤니티원장저장요청 request,
            string updatedBy,
            CancellationToken cancellationToken = default)
        {
            var existing = _ledgers[request.원장Id!];
            if (request.기대Revision.HasValue && request.기대Revision.Value != existing.Revision)
            {
                throw new InvalidOperationException("revision conflict");
            }

            LastUpdatedBy = updatedBy;
            var saved = new 커뮤니티원장Dto
            {
                원장Id = existing.원장Id,
                Revision = existing.Revision + 1,
                커뮤니티Id = request.커뮤니티Id,
                원장템플릿Key = request.원장템플릿Key,
                제목 = request.제목,
                원함 = request.원함,
                상태 = request.상태 ?? existing.상태,
                현재단계Key = request.현재단계Key,
                대상OsCode = request.대상OsCode,
                대상OsName = request.대상OsName,
                생성자UserId = request.생성자UserId,
                생성자표시명 = request.생성자표시명 ?? existing.생성자표시명,
                블록목록 = request.블록목록,
                참여자목록 = request.참여자목록,
                포함원장목록 = request.포함원장목록 ?? existing.포함원장목록,
                다이어그램스냅샷 = request.다이어그램스냅샷,
                외부참조 = request.외부참조,
                확장속성 = request.확장속성
            };
            _ledgers[saved.원장Id] = saved;
            return Task.FromResult(saved);
        }

        public Task<커뮤니티원장Dto?> 원장조회Async(
            string 원장Id,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_ledgers.GetValueOrDefault(원장Id));

        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(
            커뮤니티원장조회조건 query,
            CancellationToken cancellationToken = default)
        {
            var items = _ledgers.Values.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(query.원장템플릿Key))
            {
                items = items.Where(item => string.Equals(
                    item.원장템플릿Key,
                    query.원장템플릿Key,
                    StringComparison.OrdinalIgnoreCase));
            }
            else if (query.원장템플릿Keys.Count > 0)
            {
                var keys = query.원장템플릿Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
                items = items.Where(item => keys.Contains(item.원장템플릿Key));
            }

            if (!string.IsNullOrWhiteSpace(query.상태))
            {
                items = items.Where(item => string.Equals(
                    item.상태,
                    query.상태,
                    StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.접근UserId))
            {
                items = items.Where(item =>
                    string.Equals(item.생성자UserId, query.접근UserId, StringComparison.OrdinalIgnoreCase)
                    || item.참여자목록.Any(participant => string.Equals(
                        participant.UserId,
                        query.접근UserId,
                        StringComparison.OrdinalIgnoreCase)));
            }

            return Task.FromResult<IReadOnlyList<커뮤니티원장Dto>>(
                items.Skip(Math.Max(0, query.Skip)).Take(query.Limit <= 0 ? 50 : query.Limit).ToArray());
        }

        public Task<커뮤니티원장Dto?> 원장상태변경Async(
            커뮤니티원장상태변경요청 request,
            string updatedBy,
            CancellationToken cancellationToken = default)
            => Task.FromResult<커뮤니티원장Dto?>(null);
    }

    private sealed class FakeSignatureStore : I주문원장서명저장소
    {
        public FakeSignatureStore(params 주문원장서명기록[] records)
        {
            Records = records.ToDictionary(x => x.주문원장Id, StringComparer.OrdinalIgnoreCase);
        }

        public Dictionary<string, 주문원장서명기록> Records { get; }

        public Task<주문원장서명기록?> 조회Async(
            string 주문원장Id,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Records.GetValueOrDefault(주문원장Id));

        public Task<IReadOnlyDictionary<string, 주문원장서명기록>> 목록조회Async(
            IEnumerable<string> 주문원장Ids,
            CancellationToken cancellationToken = default)
        {
            var ids = 주문원장Ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult<IReadOnlyDictionary<string, 주문원장서명기록>>(
                Records.Where(x => ids.Contains(x.Key)).ToDictionary(StringComparer.OrdinalIgnoreCase));
        }

        public Task<주문원장서명기록> 저장Async(
            string 주문원장Id,
            string 커뮤니티Id,
            ContractElectronicSignatureBundle 서명묶음,
            long? 기대Revision,
            string 변경자UserId,
            CancellationToken cancellationToken = default)
        {
            var existingRevision = Records.GetValueOrDefault(주문원장Id)?.Revision ?? 0;
            if (기대Revision.HasValue && 기대Revision.Value != existingRevision)
            {
                throw new InvalidOperationException("revision conflict");
            }

            var record = new 주문원장서명기록(
                주문원장Id,
                커뮤니티Id,
                existingRevision + 1,
                서명묶음,
                변경자UserId,
                DateTimeOffset.UtcNow);
            Records[주문원장Id] = record;
            return Task.FromResult(record);
        }
    }
}
