using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Services.Community;
using Hongdal.Services.Orderer;

namespace Hongdal.Tests.Services.Community;

public sealed class CommunityGroupPurchaseLedgerWorkflowTests
{
    [Fact]
    public async Task 조회와진행_공동구매원장을자동연결하고절차이력을저장한다()
    {
        var campaignId = Guid.NewGuid();
        var campaignStore = new FakeCampaignStore(CreateCampaign(campaignId));
        var ledgerStore = new FakeLedgerStore();
        var service = new 공동구매원장절차Service(campaignStore, ledgerStore);

        var created = await service.조회Async(campaignId);

        Assert.NotNull(created);
        Assert.Equal(공동구매원장절차Service.원장Id생성(campaignId), created.CommunityLedgerId);
        Assert.Equal(created.CommunityLedgerId, campaignStore.Campaign.CommunityLedgerId);
        Assert.True(created.AutomaticallyLinked);
        Assert.Equal(CommunityGroupPurchaseLedgerStageCodes.Recruitment, created.CurrentStageCode);
        Assert.Contains(created.History, x => x.StageCode == CommunityGroupPurchaseLedgerStageCodes.Recruitment);

        var progressed = await service.진행Async(
            campaignId,
            new CommunityGroupPurchaseLedgerProgressRequest
            {
                StageCode = CommunityGroupPurchaseLedgerStageCodes.SupplyNegotiation,
                Memo = "공급 조건 협상을 시작했습니다.",
                ExpectedRevision = created.Revision
            },
            "representative-1");

        Assert.NotNull(progressed);
        Assert.Equal(CommunityGroupPurchaseLedgerStageCodes.SupplyNegotiation, progressed.CurrentStageCode);
        Assert.Contains(
            progressed.History,
            x => x.StageCode == CommunityGroupPurchaseLedgerStageCodes.SupplyNegotiation
                 && x.Memo == "공급 조건 협상을 시작했습니다."
                 && x.ChangedBy == "representative-1");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.진행Async(
            campaignId,
            new CommunityGroupPurchaseLedgerProgressRequest
            {
                StageCode = CommunityGroupPurchaseLedgerStageCodes.Counterparty
            },
            "representative-1"));
    }

    [Fact]
    public async Task 발주초안_주문과판매입고출고운송원장을생성하고공동구매원장에연결한다()
    {
        var campaignId = Guid.NewGuid();
        var campaignStore = new FakeCampaignStore(CreateCampaign(campaignId));
        var ledgerStore = new FakeLedgerStore();
        var workflow = new 공동구매원장절차Service(campaignStore, ledgerStore);
        var service = new DomesticGroupPurchaseFulfillmentPlanService(
            new InMemoryDomesticGroupPurchaseFulfillmentOrderDraftStore(),
            ledgerStore,
            workflow,
            new 주문원장통합UseCase(ledgerStore));

        var request = CreateFulfillmentRequest(campaignId);
        var created = await service.CreateOrderDraftAsync("representative-1", request);

        Assert.True(created.Plan.LedgersPersisted);
        Assert.False(created.Plan.OrderPlaced);
        Assert.False(string.IsNullOrWhiteSpace(created.Plan.OrderLedgerId));
        Assert.All(created.Plan.LedgerNodes, x => Assert.False(string.IsNullOrWhiteSpace(x.LedgerId)));

        var groupPurchaseLedger = await ledgerStore.원장조회Async(
            공동구매원장절차Service.원장Id생성(campaignId));
        Assert.NotNull(groupPurchaseLedger);
        var orderReference = Assert.Single(groupPurchaseLedger.포함원장목록);
        Assert.Equal(created.Plan.OrderLedgerId, orderReference.원장Id);
        Assert.Equal(주문원장포함역할.개별주문, orderReference.역할);

        var orderLedger = await ledgerStore.원장조회Async(created.Plan.OrderLedgerId);
        Assert.NotNull(orderLedger);
        Assert.Equal(CommunityLedgerTemplateKeys.Order, orderLedger.원장템플릿Key);
        Assert.Equal(created.Plan.LedgerNodes.Count - 1, orderLedger.포함원장목록.Count);
        Assert.Contains(orderLedger.포함원장목록, x => x.역할 == 주문원장포함역할.판매);
        Assert.Contains(orderLedger.포함원장목록, x => x.역할 == 주문원장포함역할.창고입고);
        Assert.Contains(orderLedger.포함원장목록, x => x.역할 == 주문원장포함역할.창고출고);
        Assert.Contains(orderLedger.포함원장목록, x => x.역할 == 주문원장포함역할.운송);

        var progress = await workflow.조회Async(campaignId);
        Assert.NotNull(progress);
        Assert.Equal(CommunityGroupPurchaseLedgerStageCodes.Execution, progress.CurrentStageCode);
        Assert.Contains(
            progress.History,
            x => x.StageCode == CommunityGroupPurchaseLedgerStageCodes.Execution
                 && x.Memo.Contains("자동 연결", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 생산자연락과공급제안_공동구매원장블록과절차단계로저장한다()
    {
        var campaignId = Guid.NewGuid();
        var campaignStore = new FakeCampaignStore(CreateCampaign(campaignId));
        var ledgerStore = new FakeLedgerStore();
        var workflow = new 공동구매원장절차Service(campaignStore, ledgerStore);
        var service = new DomesticGroupPurchaseProducerConnectionService(
            new UnconnectedCommunityProducerMemberDirectory(),
            new UnconnectedCommunityGroupPurchaseRepresentativeDirectory(),
            new InMemoryDomesticProducerContactRequestDraftStore(),
            new InMemoryDomesticProducerSupplyOfferDraftStore(),
            ledgerStore,
            workflow);

        var contact = await service.CreateDraftAsync(
            "representative-1",
            new DomesticProducerContactRequestDraftRequest
            {
                GroupPurchaseCampaignId = campaignId,
                CampaignTitle = "고구마 공동구매",
                ProducerCandidateKey = "producer-1",
                ProducerMaskedDisplayName = "김○○",
                ProductSummary = "고구마",
                RequestedQuantitySummary = "100kg",
                RequiredPackagingFormCode = DomesticProducePackagingFormCodes.CorrugatedBox,
                PackagingUnitSummary = "10kg 상자",
                QualityGradeSummary = "혼합 크기 허용",
                RequestedQuantity = 100,
                MaximumAbsorptionQuantity = 150,
                QuantityUnit = "kg",
                CanReceiveSplitShipments = true,
                Message = "공급 가능 여부를 협의하고 싶습니다."
            });
        var offer = await service.CreateSupplyOfferDraftAsync(
            "producer-1",
            new DomesticProducerSupplyOfferDraftRequest
            {
                GroupPurchaseCampaignId = campaignId,
                CampaignTitle = "고구마 공동구매",
                RepresentativeCandidateKey = "representative-1",
                RepresentativeMaskedDisplayName = "대표 최○○",
                ProducerMaskedDisplayName = "농가 김○○",
                ProductSummary = "고구마",
                AvailableQuantitySummary = "10kg 상자 50개",
                SupportedPackagingFormCodes = [DomesticProducePackagingFormCodes.CorrugatedBox],
                AvailableQuantity = 500,
                MinimumTakeQuantity = 200,
                QuantityUnit = "kg",
                CanSplitShipments = true,
                ExpectedPriceSummary = "상자당 18,000원",
                SupplyDeadlineSummary = "이번 주 금요일",
                OfferReasonCode = DomesticProducerSupplyOfferReasonCodes.OffGrade,
                QualityDisclosure = "크기가 고르지 않지만 파손과 부패는 없습니다.",
                FoodSafetyConfirmed = true,
                Message = "공동구매 가능 여부를 검토해 주세요."
            });

        var ledger = await ledgerStore.원장조회Async(공동구매원장절차Service.원장Id생성(campaignId));
        Assert.NotNull(ledger);
        Assert.Contains(ledger.블록목록, x => x.BlockId == $"producer-contact-{contact.DraftId:N}");
        Assert.Contains(ledger.블록목록, x => x.BlockId == $"producer-supply-offer-{offer.DraftId:N}");

        var progress = await workflow.조회Async(campaignId);
        Assert.NotNull(progress);
        Assert.Equal(CommunityGroupPurchaseLedgerStageCodes.SupplyNegotiation, progress.CurrentStageCode);
        Assert.Contains(progress.History, x => x.StageCode == CommunityGroupPurchaseLedgerStageCodes.Counterparty);
        Assert.Contains(progress.History, x => x.StageCode == CommunityGroupPurchaseLedgerStageCodes.SupplyNegotiation);
    }

    [Fact]
    public async Task 공개협상_협상이력저장소와공동구매원장요약블록을함께갱신한다()
    {
        var campaignId = Guid.NewGuid();
        var campaignStore = new FakeCampaignStore(CreateCampaign(campaignId));
        var ledgerStore = new FakeLedgerStore();
        var workflow = new 공동구매원장절차Service(campaignStore, ledgerStore);
        var service = new DomesticGroupPurchaseNegotiationService(
            new InMemoryDomesticGroupPurchaseNegotiationStore(),
            new TestNegotiationClock(),
            ledgerStore,
            workflow);

        await service.AppendEventAsync(
            campaignId,
            "representative-1",
            new DomesticGroupPurchaseNegotiationEventRequest
            {
                EventTypeCode = DomesticGroupPurchaseNegotiationEventTypeCodes.Proposal,
                MaskedActorDisplayName = "대표 최○○",
                ActorRoleLabel = "공동구매 대표",
                PublicSummary = "500kg 공급 조건 협의를 시작합니다."
            });

        var ledger = await ledgerStore.원장조회Async(공동구매원장절차Service.원장Id생성(campaignId));
        Assert.NotNull(ledger);
        var negotiationBlock = Assert.Single(
            ledger.블록목록,
            x => x.BlockId == $"supply-negotiation-{campaignId:N}");
        Assert.Equal("1", negotiationBlock.Data["EventCount"]);

        var progress = await workflow.조회Async(campaignId);
        Assert.NotNull(progress);
        Assert.Equal(CommunityGroupPurchaseLedgerStageCodes.SupplyNegotiation, progress.CurrentStageCode);
    }

    [Fact]
    public async Task 공동수입전환_원천공동구매와전용창고입고물류원장을연결한다()
    {
        var campaignId = Guid.NewGuid();
        var campaignStore = new FakeCampaignStore(CreateImportCampaign(campaignId));
        var ledgerStore = new FakeLedgerStore();
        var workflow = new 공동구매원장절차Service(campaignStore, ledgerStore);
        var service = new 공동수입원장전환Service(campaignStore, workflow, ledgerStore);
        var request = new CommunityGroupImportLedgerConversionRequest
        {
            GroupPurchaseCampaignId = campaignId,
            LogisticsRouteCode = CommunityGroupImportLogisticsRouteCodes.DedicatedWarehouse,
            ProductSummary = "태국산 망고",
            PlannedQuantity = 1_000,
            QuantityUnit = "kg",
            InternationalTransportMode = "LCL",
            FinalDestinationLabel = "서울 공동 수령지",
            WarehouseReferenceKey = "dedicated-warehouse:verified",
            WarehouseDisplayName = "공동구매 전용 창고",
            WarehouseOperatorConsentConfirmed = true,
            WarehouseSiteVerified = true,
            WarehouseBulkReceivingSupported = true,
            WarehouseStorageSupported = true,
            WarehouseOutboundSupported = false,
            RequiresWarehouseOutbound = false,
            RequiresFinalDestinationDelivery = false
        };

        var created = await service.전환Async(request, "representative-1");

        Assert.True(created.Created);
        Assert.Equal("전용 창고 입고·보관", created.LogisticsRouteLabel);
        Assert.Contains(created.Nodes, x =>
            x.IsSourceReference
            && x.RelationType == CommunityLedgerRelationTypes.Reference);
        Assert.Contains(created.Nodes, x =>
            x.LedgerTemplateKey == CommunityLedgerTemplateKeys.WarehouseInbound
            && x.RelationRole == 공동수입원장관계역할.물류거점입고);
        Assert.DoesNotContain(created.Nodes, x =>
            x.LedgerTemplateKey == CommunityLedgerTemplateKeys.WarehouseOutbound);

        var root = await ledgerStore.원장조회Async(created.GroupImportLedgerId);
        Assert.NotNull(root);
        Assert.Equal(CommunityLedgerTemplateKeys.GroupImport, root.원장템플릿Key);
        Assert.Equal(
            공동구매원장절차Service.원장Id생성(campaignId),
            root.외부참조["SourceGroupPurchaseLedgerId"]);
        foreach (var node in created.Nodes.Where(x => !x.IsSourceReference))
        {
            var child = await ledgerStore.원장조회Async(node.LedgerId);
            Assert.NotNull(child);
            Assert.Equal(node.LedgerTemplateKey, child.원장템플릿Key);
        }

        var sourceProgress = await workflow.조회Async(campaignId);
        Assert.NotNull(sourceProgress);
        Assert.Equal(CommunityGroupPurchaseLedgerStageCodes.Execution, sourceProgress.CurrentStageCode);
    }

    private static 공동구매원장캠페인Snapshot CreateCampaign(Guid campaignId)
        => new(
            campaignId,
            CommunityVoteKindCodes.GroupPurchaseDemand,
            "platform",
            "고구마 공동구매",
            "생산자 직거래 공동구매",
            1001,
            "공동구매 대표",
            CommunityVoteStatusCodes.Open,
            null,
            null);

    private static 공동구매원장캠페인Snapshot CreateImportCampaign(Guid campaignId)
        => new(
            campaignId,
            CommunityVoteKindCodes.GroupPurchaseDemand,
            "platform",
            "태국산 망고 공동수입",
            "해외 판매자와 합의한 망고 공동수입",
            2001,
            "공동구매 대표",
            CommunityVoteStatusCodes.Closed,
            CommunityVoteResolutionStatusCodes.Signed,
            null,
            CommunityGroupPurchaseTradeRouteCodes.InboundGroupImportCandidate,
            "0804502000",
            "TH",
            "TH",
            "KR",
            CommunityGroupPurchaseCustomsClearanceStatusCodes.NotCleared,
            1_000,
            "kg");

    private static DomesticGroupPurchaseFulfillmentPlanRequest CreateFulfillmentRequest(Guid campaignId)
        => new()
        {
            GroupPurchaseCampaignId = campaignId,
            CampaignTitle = "고구마 공동구매",
            RouteCode = DomesticGroupPurchaseFulfillmentRouteCodes.ThirdPartyLogistics,
            ProducerDisplayName = "해남 생산자",
            ProductSummary = "고구마",
            QuantitySummary = "500kg",
            PlannedQuantity = 500,
            QuantityUnit = "kg",
            DestinationLabel = "서울 공동 수령지",
            HubReferenceKey = "third-party-logistics:sample",
            HubDisplayName = "검증된 3PL",
            RequiresLastMileDelivery = true,
            ProducerTermsAccepted = true,
            BuyerRepresentativeTermsAccepted = true,
            SupplyCompatibilityConfirmed = true,
            HubCapabilities = new DomesticGroupPurchaseFulfillmentHubCapabilitySnapshot
            {
                HasOperatorConsent = true,
                SiteVerified = true,
                SupportsBulkReceiving = true,
                SupportsSorting = true,
                SupportsStorage = true,
                SupportsLastMileHandoff = true,
                HandlingCapacity = 1_000,
                CapacityUnit = "kg"
            }
        };

    private sealed class FakeCampaignStore(공동구매원장캠페인Snapshot campaign)
        : I공동구매원장캠페인Store
    {
        public 공동구매원장캠페인Snapshot Campaign { get; private set; } = campaign;

        public Task<공동구매원장캠페인Snapshot?> 조회Async(
            Guid campaignId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<공동구매원장캠페인Snapshot?>(
                Campaign.CampaignId == campaignId ? Campaign : null);

        public Task 원장연결Async(
            Guid campaignId,
            string ledgerId,
            CancellationToken cancellationToken = default)
        {
            if (Campaign.CampaignId == campaignId)
            {
                Campaign = Campaign with { CommunityLedgerId = ledgerId };
            }

            return Task.CompletedTask;
        }
    }

    private sealed class TestNegotiationClock : IDomesticGroupPurchaseNegotiationClock
    {
        public DateTimeOffset UtcNow => new(2026, 7, 16, 0, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeLedgerStore : I커뮤니티원장저장소
    {
        private readonly Dictionary<string, 커뮤니티원장Dto> ledgers =
            new(StringComparer.OrdinalIgnoreCase);

        public Task<커뮤니티원장Dto> 원장저장Async(
            커뮤니티원장저장요청 request,
            string updatedBy,
            CancellationToken cancellationToken = default)
        {
            주문원장구성정책.저장요청검증(request);
            var id = request.원장Id ?? throw new InvalidOperationException("원장 ID가 필요합니다.");
            ledgers.TryGetValue(id, out var existing);
            if (request.기대Revision.HasValue && request.기대Revision != existing?.Revision)
            {
                throw new InvalidOperationException("revision conflict");
            }

            var now = DateTime.UtcNow;
            var history = existing?.상태이력.ToList() ?? [];
            if (existing is null)
            {
                history.Add(new 커뮤니티원장상태이력Dto
                {
                    상태 = request.상태 ?? 커뮤니티원장상태.초안,
                    현재단계Key = request.현재단계Key,
                    메모 = "원장을 생성했습니다.",
                    변경자 = updatedBy,
                    변경시각Utc = now
                });
            }

            var saved = new 커뮤니티원장Dto
            {
                원장Id = id,
                Revision = (existing?.Revision ?? 0) + 1,
                커뮤니티Id = request.커뮤니티Id,
                원장템플릿Key = request.원장템플릿Key,
                제목 = request.제목,
                원함 = request.원함,
                상태 = request.상태 ?? existing?.상태 ?? 커뮤니티원장상태.초안,
                현재단계Key = request.현재단계Key,
                대상OsCode = request.대상OsCode,
                대상OsName = request.대상OsName,
                생성자UserId = request.생성자UserId,
                생성자표시명 = request.생성자표시명 ?? existing?.생성자표시명 ?? "익명 참여자",
                블록목록 = request.블록목록,
                참여자목록 = request.참여자목록,
                포함원장목록 = request.포함원장목록 ?? existing?.포함원장목록 ?? [],
                다이어그램스냅샷 = request.다이어그램스냅샷,
                외부참조 = request.외부참조,
                확장속성 = request.확장속성,
                상태이력 = history,
                생성시각Utc = existing?.생성시각Utc ?? now,
                수정시각Utc = now
            };
            ledgers[id] = saved;
            return Task.FromResult(saved);
        }

        public Task<커뮤니티원장Dto?> 원장조회Async(
            string 원장Id,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ledgers.GetValueOrDefault(원장Id));

        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(
            커뮤니티원장조회조건 query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<커뮤니티원장Dto>>(ledgers.Values.ToArray());

        public Task<커뮤니티원장Dto?> 원장상태변경Async(
            커뮤니티원장상태변경요청 request,
            string updatedBy,
            CancellationToken cancellationToken = default)
        {
            if (!ledgers.TryGetValue(request.원장Id, out var existing))
            {
                return Task.FromResult<커뮤니티원장Dto?>(null);
            }

            if (request.기대Revision.HasValue && request.기대Revision != existing.Revision)
            {
                throw new InvalidOperationException("revision conflict");
            }

            var now = DateTime.UtcNow;
            existing.Revision++;
            existing.상태 = request.상태;
            existing.현재단계Key = request.현재단계Key;
            existing.수정시각Utc = now;
            existing.상태이력 = existing.상태이력
                .Append(new 커뮤니티원장상태이력Dto
                {
                    상태 = request.상태,
                    이전상태 = request.이전상태,
                    현재단계Key = request.현재단계Key,
                    메모 = request.메모,
                    변경자 = updatedBy,
                    변경시각Utc = now
                })
                .ToArray();
            return Task.FromResult<커뮤니티원장Dto?>(existing);
        }
    }
}
