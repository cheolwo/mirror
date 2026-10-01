using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.ContractManagement;
using Hongdal.Services.Community;

namespace Hongdal.Tests.Services.Community;

public sealed class OrderLedgerIntegrationUseCaseTests
{
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
        var store = new FakeLedgerStore(
            root,
            Ledger("delivery-1", CommunityLedgerTemplateKeys.FoodDelivery, state: 커뮤니티원장상태.완료));
        var useCase = new 주문원장통합UseCase(store);

        var result = await useCase.조회Async("order-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.완료하위원장수);
        Assert.True(result.Value.필수하위원장완료여부);
        Assert.Equal(커뮤니티원장상태.완료, result.Value.포함원장목록.Single().원장!.상태);
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

        Assert.Contains("공동구매 주문집계에서만", exception.Message);
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
            => Task.FromResult<IReadOnlyList<커뮤니티원장Dto>>(_ledgers.Values.ToArray());

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
