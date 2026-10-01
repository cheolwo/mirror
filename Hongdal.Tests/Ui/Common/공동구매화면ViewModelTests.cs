using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.ContractManagement;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Inventory;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Contracts.Common.Operations;
using Hongdal.Contracts.Common.Sales;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Contracts.Shipper.Request;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Hongdal.Tests.Ui.Common;

public sealed class 공동구매화면ViewModelTests
{
    [Fact]
    public void 루트화면_하위ViewModel을기본업무영역별로제공한다()
    {
        using var fixture = CreateFixture(new Fake공동구매업무Service());

        Assert.Equal(21, fixture.ViewModel.업무목록.Count);
        Assert.Contains(fixture.ViewModel.모집.제안, fixture.ViewModel.업무영역조회(공동구매업무영역코드.모집));
        Assert.Contains(fixture.ViewModel.합의.결의, fixture.ViewModel.업무영역조회(공동구매업무영역코드.합의));
        Assert.Contains(fixture.ViewModel.공급.협상, fixture.ViewModel.업무영역조회(공동구매업무영역코드.공급));
        Assert.Contains(fixture.ViewModel.물류.이행계획, fixture.ViewModel.업무영역조회(공동구매업무영역코드.물류));
        Assert.Contains(fixture.ViewModel.실행.커머스이행, fixture.ViewModel.업무영역조회(공동구매업무영역코드.실행));
        Assert.Contains(fixture.ViewModel.국내판매, fixture.ViewModel.업무영역조회(공동구매업무영역코드.실행));
        Assert.Contains(fixture.ViewModel.해외수출, fixture.ViewModel.업무영역조회(공동구매업무영역코드.실행));

        Assert.Equal(5, fixture.ViewModel.모집.세부업무목록.Count);
        Assert.Equal(4, fixture.ViewModel.합의.세부업무목록.Count);
        Assert.Equal(10, fixture.ViewModel.공급.세부업무목록.Count);
        Assert.Equal(2, fixture.ViewModel.물류.세부업무목록.Count);
        Assert.Equal(8, fixture.ViewModel.공동수입.세부업무목록.Count);
        Assert.Equal(22, fixture.ViewModel.실행.세부업무목록.Count);
        Assert.Equal(51, fixture.ViewModel.절차세부업무목록.Count);
        Assert.Equal(
            fixture.ViewModel.절차세부업무목록.Count,
            fixture.ViewModel.절차세부업무목록.Select(item => item.업무코드).Distinct().Count());
    }

    [Fact]
    public void 실행_창고기능을입고원장과출고원장으로조립한다()
    {
        using var fixture = CreateFixture(new Fake공동구매업무Service());

        Assert.Same(fixture.ViewModel.실행.창고.입고원장, fixture.ViewModel.실행.입고원장);
        Assert.Same(fixture.ViewModel.실행.창고.출고원장, fixture.ViewModel.실행.출고원장);
    }

    [Fact]
    public void 국내판매와해외수출_거래방향에따라각각의ViewModel을활성화한다()
    {
        using var fixture = CreateFixture(new Fake공동구매업무Service());
        var domestic = Campaign("국내 감자 판매", DateTime.UtcNow);

        fixture.ViewModel.상태.선택적용(domestic);

        Assert.True(fixture.ViewModel.국내판매.활성);
        Assert.False(fixture.ViewModel.해외수출.활성);
        Assert.Equal("국내 감자 판매", fixture.ViewModel.국내판매.상품초안.대표상품명);

        var export = Campaign("한국 배 수출", DateTime.UtcNow, hsCode: "0808.30");
        export.GroupPurchase!.TradeRouteCode = CommunityGroupPurchaseTradeRouteCodes.OtherCrossBorder;
        export.GroupPurchase.SellerCountryCode = "KR";
        export.GroupPurchase.ShipFromCountryCode = "KR";
        export.GroupPurchase.DeliveryCountryCode = "US";

        fixture.ViewModel.상태.선택적용(export);

        Assert.False(fixture.ViewModel.국내판매.활성);
        Assert.True(fixture.ViewModel.해외수출.활성);
        Assert.Equal(공동구매업무분기코드.해외수출, fixture.ViewModel.거래경로분기.활성분기코드);
        Assert.Equal("해외 수출", fixture.ViewModel.거래경로분기.활성분기명);
        Assert.Equal("한국 배 수출", fixture.ViewModel.해외수출.초안.ProductName);
        Assert.Equal("0808.30", fixture.ViewModel.해외수출.초안.HsCode);
        Assert.Contains(
            fixture.ViewModel.해외수출.필수작업안내,
            item => item.Contains("HS 코드", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 국내판매_입고재고부터채널출품까지_단계상태로연결한다()
    {
        using var fixture = CreateFixture(new Fake공동구매업무Service());
        var campaign = Campaign("국내 감자 판매", DateTime.UtcNow);
        fixture.ViewModel.상태.선택적용(campaign);
        fixture.ViewModel.실행.창고.상태.재고목록적용(
        [
            new 재고항목응답
            {
                입고상품Id = 71,
                창고Id = 1,
                상품명 = "감자 10kg",
                SKU = "POTATO-10",
                가용수량 = 10
            }
        ]);
        var sales = fixture.ViewModel.국내판매;

        Assert.True(sales.입고재고선택(71));
        sales.계정초안.상점명 = "홍달 산지마켓";
        Assert.True(await sales.계정생성Async());
        sales.상품초안.판매가 = 25_000;
        Assert.True(await sales.상품생성Async());
        Assert.True(await sales.출품생성Async());

        Assert.True(sales.출품완료);
        Assert.All(sales.진행단계, stage => Assert.Equal(판매실행단계상태.완료, stage.상태));
        Assert.Contains("준비", sales.다음작업안내);
    }

    [Fact]
    public async Task 해외수출_Amazon계정과수출상품을직접생성하고_출품준비단계를관리한다()
    {
        using var fixture = CreateFixture(new Fake공동구매업무Service());
        var campaign = Campaign("한국 배 수출", DateTime.UtcNow, hsCode: "0808.30");
        campaign.GroupPurchase!.TradeRouteCode = CommunityGroupPurchaseTradeRouteCodes.OtherCrossBorder;
        campaign.GroupPurchase.ShipFromCountryCode = "KR";
        campaign.GroupPurchase.DeliveryCountryCode = "US";
        fixture.ViewModel.상태.선택적용(campaign);
        fixture.ViewModel.실행.창고.상태.재고목록적용(
        [
            new 재고항목응답
            {
                입고상품Id = 81,
                창고Id = 1,
                상품명 = "한국 배 5kg",
                SKU = "PEAR-5",
                가용수량 = 20
            }
        ]);
        var export = fixture.ViewModel.해외수출;

        Assert.True(export.수출재고선택(81));
        export.Amazon계정초안.상점명 = "Hongdal Korea";
        Assert.True(await export.Amazon계정생성Async());
        export.수출상품초안.판매가 = 45_000;
        Assert.True(await export.수출상품생성Async());

        export.초안.MarketplaceId = "ATVPDKIKX0DER";
        export.초안.SellerId = "SELLER-1";
        export.초안.ProductType = "FRESH_FRUIT";
        export.초안.ProductTypeDefinitionConfirmed = true;
        export.초안.ListingPayloadMapped = true;
        export.초안.ImageAndDescriptionReady = true;
        export.초안.KoreanLogisticsHistoryRecorded = true;
        export.초안.ProductJourneyEvidenceReady = true;
        export.초안.UserReviewUsageConsentConfirmed = true;
        export.초안.EligibleUserReviewCount = 1;
        export.초안.DetailPageImageAssetGenerated = true;
        export.초안.DetailPageImageAssetApproved = true;
        export.초안.AdvertisingCreativeReady = true;
        export.입력변경알림();

        Assert.True(export.출품준비완료);
        Assert.True(await export.Amazon출품생성Async());
        Assert.Contains(export.진행단계, stage => stage.코드 == "listing" && stage.상태 == 판매실행단계상태.완료);
        Assert.False(export.수출이행준비완료);
        Assert.Contains(export.필수작업안내, item => item.Contains("출고 배치", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 초기화_국내공동구매와공동수입을함께조회하고최신공동수입분기를선택한다()
    {
        var service = new Fake공동구매업무Service();
        var oldCampaign = Campaign("이전 국내 공동구매", DateTime.UtcNow.AddDays(-2), sourcePostId: 11);
        var latestCampaign = Campaign("최신 국내 공동구매", DateTime.UtcNow.AddDays(-1), sourcePostId: 22);
        var importCampaign = Campaign("공동수입", DateTime.UtcNow, sourcePostId: 33, hsCode: "0202.30");
        service.목록응답.Items = [oldCampaign, importCampaign, latestCampaign];
        service.상세응답[importCampaign.Id] = importCampaign;
        service.의견응답[33] =
        [
            new PlatformCommunityPostCommentResponse
            {
                Id = 1,
                Nickname = "참여자",
                Body = "[이의제기:recruitment] 수령 시간을 조정해 주세요."
            }
        ];

        using var fixture = CreateFixture(service);

        var succeeded = await fixture.ViewModel.초기화Async();

        Assert.True(succeeded);
        Assert.Equal(
            [importCampaign.Id, latestCampaign.Id, oldCampaign.Id],
            fixture.ViewModel.상태.공동구매목록.Select(x => x.Id));
        Assert.Same(importCampaign, fixture.ViewModel.상태.선택된공동구매);
        Assert.True(fixture.ViewModel.거래경로분기.공동수입활성);
        Assert.True(fixture.ViewModel.공동수입.활성);
        Assert.False(fixture.ViewModel.국내공동구매.활성);
        Assert.Single(fixture.ViewModel.상태.의견목록);
        Assert.Equal(1, fixture.ViewModel.모집.이의검토.전체이의수);
    }

    [Fact]
    public void 공동수입물류_전용창고를선택하면입고보관원장만계획한다()
    {
        using var fixture = CreateFixture(new Fake공동구매업무Service());
        var campaign = Campaign("태국산 망고 공동수입", DateTime.UtcNow, hsCode: "0804.50");
        campaign.GroupPurchase!.TradeRouteCode = CommunityGroupPurchaseTradeRouteCodes.InboundGroupImportCandidate;
        campaign.GroupPurchase.TotalRequestedQuantity = 1_000;
        campaign.GroupPurchase.QuantityUnit = "kg";
        campaign.GroupPurchase.ServiceAreaLabel = "서울 공동 수령지";
        fixture.ViewModel.상태.선택적용(campaign);
        var logistics = fixture.ViewModel.공동수입.원장물류;

        Assert.True(logistics.물류경로선택(CommunityGroupImportLogisticsRouteCodes.DedicatedWarehouse));
        logistics.초안.WarehouseReferenceKey = "dedicated-warehouse:verified";
        logistics.초안.WarehouseOperatorConsentConfirmed = true;
        logistics.초안.WarehouseSiteVerified = true;
        logistics.초안.WarehouseBulkReceivingSupported = true;
        logistics.초안.WarehouseStorageSupported = true;
        logistics.입력변경알림();

        Assert.True(logistics.계획?.Ready);
        Assert.Equal("전용 창고 입고·보관", logistics.계획?.LogisticsRouteLabel);
        Assert.Contains(logistics.계획!.Nodes, x =>
            x.LedgerTemplateKey == CommunityLedgerTemplateKeys.WarehouseInbound);
        Assert.DoesNotContain(logistics.계획.Nodes, x =>
            x.LedgerTemplateKey == CommunityLedgerTemplateKeys.WarehouseOutbound);
    }

    [Fact]
    public async Task 공동수입선적통관_원장생성뒤_선적이벤트와통관상태를차례로실행한다()
    {
        var shipmentClient = new Fake공동수입선적통관Client();
        using var fixture = CreateFixture(
            new Fake공동구매업무Service(),
            shipmentCustomsClient: shipmentClient);
        var campaign = Campaign("태국산 망고 공동수입", DateTime.UtcNow, hsCode: "0804.50");
        campaign.GroupPurchase!.TradeRouteCode = CommunityGroupPurchaseTradeRouteCodes.InboundGroupImportCandidate;
        campaign.GroupPurchase.ShipFromCountryCode = "TH";
        campaign.GroupPurchase.DeliveryCountryCode = "KR";
        campaign.GroupPurchase.TotalRequestedQuantity = 1_000;
        campaign.GroupPurchase.QuantityUnit = "kg";
        campaign.GroupPurchase.ServiceAreaKey = "seoul-group";
        campaign.GroupPurchase.ServiceAreaLabel = "서울 공동 수령지";
        fixture.ViewModel.상태.선택적용(campaign);
        var import = fixture.ViewModel.공동수입;

        Assert.False(await import.선적통관.관리자선적저장Async());
        Assert.Contains("공동수입 원장", import.선적통관.오류메시지);

        Assert.True(await import.원장물류.공동수입원장전환Async());
        Assert.True(import.선적통관.원장준비완료);

        import.선적통관.선적초안.문서관리번호 = "DOC-2026-001";
        import.선적통관.선적초안.운송문서번호 = "BL-001";
        Assert.True(await import.선적통관.관리자선적저장Async());
        Assert.Equal(campaign.Id.ToString("D"), shipmentClient.LastSaveRequest?.공동구매Id);
        Assert.Equal("seoul-group", shipmentClient.LastSaveRequest?.주문자집단배송권키);

        import.선적통관.이벤트초안.이벤트코드 = 공동구매선적상태코드.운송중;
        import.선적통관.이벤트초안.표시명 = "국제 운송 중";
        import.선적통관.이벤트초안.출처주체코드 = "forwarder";
        Assert.True(await import.선적통관.관리자이벤트추가Async());
        Assert.Equal(공동구매선적상태코드.운송중, import.선적통관.현재선적?.현재상태코드);

        import.선적통관.통관초안.통관화물관리번호 = "CARGO-001";
        Assert.True(await import.선적통관.관리자통관동기화Async());
        Assert.True(import.선적통관.통관결과?.동기화됨);
        Assert.Equal(공동구매선적상태코드.통관완료, import.선적통관.현재선적?.현재상태코드);
        Assert.Contains("국내 입고", import.선적통관.다음작업안내);
    }

    [Fact]
    public async Task 거래경로필터_국내공동구매만조회하고국내분기를활성화한다()
    {
        var service = new Fake공동구매업무Service();
        var oldCampaign = Campaign("이전 국내 공동구매", DateTime.UtcNow.AddDays(-2));
        var latestCampaign = Campaign("최신 국내 공동구매", DateTime.UtcNow.AddDays(-1));
        var importCampaign = Campaign("공동수입", DateTime.UtcNow, hsCode: "0202.30");
        service.목록응답.Items = [oldCampaign, importCampaign, latestCampaign];
        service.상세응답[latestCampaign.Id] = latestCampaign;
        using var fixture = CreateFixture(service);

        var succeeded = await fixture.ViewModel.모집.목록.거래경로별목록조회Async(
            공동구매거래경로필터코드.국내공동구매);

        Assert.True(succeeded);
        Assert.Equal(
            [latestCampaign.Id, oldCampaign.Id],
            fixture.ViewModel.상태.공동구매목록.Select(x => x.Id));
        Assert.Same(latestCampaign, fixture.ViewModel.상태.선택된공동구매);
        Assert.True(fixture.ViewModel.국내공동구매.활성);
        Assert.False(fixture.ViewModel.공동수입.활성);
    }

    [Fact]
    public async Task 거래경로필터_한국출발해외도착거래만수출목록으로조회한다()
    {
        var service = new Fake공동구매업무Service();
        var domestic = Campaign("국내 감자 판매", DateTime.UtcNow.AddMinutes(-1));
        var export = Campaign("한국 배 수출", DateTime.UtcNow, hsCode: "0808.30");
        export.GroupPurchase!.TradeRouteCode = CommunityGroupPurchaseTradeRouteCodes.OtherCrossBorder;
        export.GroupPurchase.ShipFromCountryCode = "KR";
        export.GroupPurchase.DeliveryCountryCode = "US";
        service.목록응답.Items = [domestic, export];
        service.상세응답[export.Id] = export;
        using var fixture = CreateFixture(service);

        var succeeded = await fixture.ViewModel.거래경로별초기화Async(
            공동구매거래경로필터코드.해외수출);

        Assert.True(succeeded);
        Assert.Equal(export.Id, Assert.Single(fixture.ViewModel.상태.공동구매목록).Id);
        Assert.True(fixture.ViewModel.해외수출.활성);
        Assert.False(fixture.ViewModel.국내판매.활성);
    }

    [Fact]
    public async Task HS코드별초기화_정규화된코드로조회하고해당공동구매를선택한다()
    {
        var service = new Fake공동구매업무Service();
        var matchingCampaign = Campaign(
            "냉동 소고기 공동구매",
            DateTime.UtcNow,
            sourcePostId: 31,
            hsCode: "0202.30");
        var otherCampaign = Campaign(
            "세제 공동구매",
            DateTime.UtcNow.AddMinutes(-1),
            hsCode: "3402.50");
        service.목록응답.Items = [matchingCampaign, otherCampaign];
        service.상세응답[matchingCampaign.Id] = matchingCampaign;

        using var fixture = CreateFixture(service);

        var succeeded = await fixture.ViewModel.HS코드별초기화Async(
            "0202.30",
            "orderer-group:seoul");

        Assert.True(succeeded);
        Assert.Equal("orderer-group:seoul", service.마지막목록커뮤니티범위);
        Assert.Equal("020230", service.마지막목록HS코드);
        Assert.True(fixture.ViewModel.모집.목록.HS코드조회적용중);
        Assert.Equal(matchingCampaign.Id, Assert.Single(fixture.ViewModel.상태.공동구매목록).Id);
        Assert.Same(matchingCampaign, fixture.ViewModel.상태.선택된공동구매);
    }

    [Fact]
    public async Task 제안등록_투표생성실패시게시글번호를복구정보로남긴다()
    {
        var service = new Fake공동구매업무Service
        {
            생성게시글응답 = new PlatformCommunityPostResponse { Id = 77 },
            공동구매생성예외 = new InvalidOperationException("vote endpoint unavailable")
        };
        using var fixture = CreateFixture(service);
        var proposal = fixture.ViewModel.모집.제안;
        FillProposal(proposal);

        var succeeded = await proposal.등록Async();

        Assert.False(succeeded);
        Assert.Equal(Api작업상태.실패, proposal.상태);
        Assert.True(proposal.제안글만생성됨);
        Assert.Equal(77, proposal.복구할게시글Id);
        Assert.Contains("게시글 번호 77", proposal.오류메시지);
        Assert.NotNull(service.마지막제안글요청);
        Assert.Contains("최소 참여: 3명", service.마지막제안글요청!.Body);
        Assert.Equal(77, service.마지막공동구매생성요청?.SourcePostId);
        Assert.Null(fixture.ViewModel.상태.선택된공동구매);
    }

    [Fact]
    public async Task 제안등록_성공하면목록선택과수요모집단계를한번에적용한다()
    {
        var created = Campaign("여름 채소 공동구매", DateTime.UtcNow, sourcePostId: 77);
        var service = new Fake공동구매업무Service
        {
            생성게시글응답 = new PlatformCommunityPostResponse { Id = 77 },
            공동구매생성응답 = created
        };
        using var fixture = CreateFixture(service);
        var proposal = fixture.ViewModel.모집.제안;
        FillProposal(proposal);
        proposal.가격의사결정.조회HS코드 = "0701.90";
        proposal.가격의사결정.제안가격Krw = 85_000m;
        proposal.가격의사결정.가격기준중량Kg = 10m;
        proposal.수령소명 = "중앙 관리실";
        proposal.수령소주소 = "서울시 테스트구 1";

        var succeeded = await proposal.등록Async();

        Assert.True(succeeded);
        Assert.Same(created, fixture.ViewModel.상태.선택된공동구매);
        Assert.Equal(공동구매절차코드.수요모집, fixture.ViewModel.상태.현재단계코드);
        Assert.Equal(created.Id, Assert.Single(fixture.ViewModel.상태.공동구매목록).Id);
        Assert.False(proposal.제안글만생성됨);
        Assert.Null(proposal.복구할게시글Id);
        var voteRequest = Assert.IsType<CommunityVoteCreateRequest>(service.마지막공동구매생성요청);
        Assert.Equal(
            CommunityGroupPurchaseProposerRoleCodes.GroupPurchaseRepresentative,
            voteRequest.GroupPurchase?.ProposerRoleCode);
        Assert.Equal("공동구매 대표", service.마지막제안글요청?.RoleTag);
        Assert.Contains("목표 단가: 8,500원/kg", service.마지막제안글요청?.Body);
        Assert.Equal("platform", voteRequest.GroupPurchase?.ServiceAreaKey);
        Assert.Equal("070190", voteRequest.GroupPurchase?.HsCode);
        Assert.Equal("070190", Assert.Single(voteRequest.StructuredOptions).HsCode);
        Assert.Equal(8_500m, voteRequest.GroupPurchase?.TargetUnitPriceKrwPerKg);
        Assert.Single(voteRequest.GroupPurchase?.PickupPoints ?? []);
        Assert.True(voteRequest.ResolutionDocumentEnabled);
        Assert.True(voteRequest.SignatureRequired);
    }

    [Fact]
    public async Task 생산자제안_역할코드를투표설정과제안글에함께남긴다()
    {
        var created = Campaign("못난이 감자 생산자 제안", DateTime.UtcNow, sourcePostId: 88);
        var service = new Fake공동구매업무Service
        {
            생성게시글응답 = new PlatformCommunityPostResponse { Id = 88 },
            공동구매생성응답 = created
        };
        using var fixture = CreateFixture(service);
        var proposal = fixture.ViewModel.모집.제안;
        FillProposal(proposal);
        proposal.제안주체코드 = CommunityGroupPurchaseProposerRoleCodes.Producer;

        var succeeded = await proposal.등록Async();

        Assert.True(succeeded);
        Assert.Equal("생산자", proposal.선택된제안주체명);
        Assert.Equal(2, proposal.제안주체목록.Count);
        Assert.Equal("생산자", service.마지막제안글요청?.RoleTag);
        Assert.Contains(
            "제안 주체: 생산자 (Producer)",
            service.마지막제안글요청?.Body);
        Assert.Contains(
            CommunityGroupPurchaseAgreementPolicy.PolicyCode,
            service.마지막제안글요청?.Body);
        Assert.Contains("제안의 선후만으로", service.마지막제안글요청?.Body);
        Assert.Equal(
            CommunityGroupPurchaseProposerRoleCodes.Producer,
            service.마지막공동구매생성요청?.GroupPurchase?.ProposerRoleCode);
    }

    [Fact]
    public async Task 해외판매자제안_거래경로하위ViewModel이_공동수입후보요청을조립한다()
    {
        var created = Campaign("중국 산지 상품 공동수입", DateTime.UtcNow, sourcePostId: 89);
        var service = new Fake공동구매업무Service
        {
            생성게시글응답 = new PlatformCommunityPostResponse { Id = 89 },
            공동구매생성응답 = created
        };
        using var fixture = CreateFixture(service);
        var proposal = fixture.ViewModel.모집.제안;
        FillProposal(proposal);

        proposal.거래경로.판매자국가코드 = "cn";

        Assert.Equal("cn", proposal.거래경로.상품출발국가코드);
        Assert.True(proposal.거래경로.공동수입후보);
        Assert.False(proposal.공동수입전환.계약확정준비완료);
        Assert.Same(
            proposal.공동수입전환,
            fixture.ViewModel.모집.공동수입전환);

        proposal.공동수입전환.HS코드 = "0202.30";
        Assert.True(proposal.공동수입전환.계약확정준비완료);

        var succeeded = await proposal.등록Async();

        Assert.True(succeeded);
        var request = Assert.IsType<CommunityVoteCreateRequest>(
            service.마지막공동구매생성요청);
        Assert.Equal("CN", request.GroupPurchase?.SellerCountryCode);
        Assert.Equal("CN", request.GroupPurchase?.ShipFromCountryCode);
        Assert.Equal("KR", request.GroupPurchase?.DeliveryCountryCode);
        Assert.Equal(
            CommunityGroupPurchaseCustomsClearanceStatusCodes.NotCleared,
            request.GroupPurchase?.CustomsClearanceStatusCode);
        Assert.Equal("0202.30", request.GroupPurchase?.HsCode);
        Assert.Equal("0202.30", Assert.Single(request.StructuredOptions).HsCode);
        Assert.Contains("거래 경로: 공동수입 후보", service.마지막제안글요청?.Body);
        Assert.Contains("계약 확정 전", service.마지막제안글요청?.Body);
    }

    [Fact]
    public void 해외판매자라도_국내출발통관재고이면_국내공동구매로유지한다()
    {
        using var fixture = CreateFixture(new Fake공동구매업무Service());
        var route = fixture.ViewModel.모집.제안.거래경로;
        route.판매자국가코드 = "US";
        route.상품출발국가코드 = "KR";
        route.국내통관상태코드 = CommunityGroupPurchaseCustomsClearanceStatusCodes.Cleared;

        Assert.Equal(CommunityGroupPurchaseTradeRouteCodes.Domestic, route.거래경로코드);
        Assert.Equal("국내 공동구매", route.판정명);
        Assert.False(route.공동수입후보);
    }

    [Fact]
    public async Task 미국운영시장_기본국가를적용하고_한국출발미국배송을공동수입으로판정한다()
    {
        using var fixture = CreateFixture(
            new Fake공동구매업무Service(),
            operatingMarketProfileClient: new FakeOperatingMarketProfileClient("US"));

        Assert.True(await fixture.ViewModel.초기화Async());

        var route = fixture.ViewModel.모집.제안.거래경로;
        Assert.Equal("US", route.운영국가코드);
        Assert.Equal("US", route.판매자국가코드);
        Assert.Equal("US", route.상품출발국가코드);
        Assert.Equal("US", route.최종배송국가코드);
        Assert.Equal("미국 내 공동구매", route.판정명);

        route.판매자국가코드 = "KR";

        Assert.Equal("KR", route.상품출발국가코드);
        Assert.True(route.공동수입후보);
        Assert.Equal("공동수입 후보", route.판정명);
    }

    [Fact]
    public async Task 공동수입선택_수입분기만활성화하고국내공급API를차단한다()
    {
        var campaign = Campaign(
            "중국 냉동육 공동수입",
            DateTime.UtcNow,
            hsCode: "0202.30",
            status: CommunityVoteStatusCodes.Closed);
        campaign.GroupPurchase!.SellerCountryCode = "CN";
        campaign.GroupPurchase.ShipFromCountryCode = "CN";
        campaign.GroupPurchase.DeliveryCountryCode = "KR";
        campaign.GroupPurchase.CustomsClearanceStatusCode =
            CommunityGroupPurchaseCustomsClearanceStatusCodes.NotCleared;
        campaign.GroupPurchase.TradeRouteCode =
            CommunityGroupPurchaseTradeRouteCodes.InboundGroupImportCandidate;
        campaign.GroupPurchase.IsGroupImportCandidate = true;
        campaign.GroupPurchase.HsCode = "0202.30";
        using var fixture = CreateFixture(new Fake공동구매업무Service());
        fixture.ViewModel.상태.선택적용(campaign);

        Assert.True(fixture.ViewModel.공동수입.활성);
        Assert.False(fixture.ViewModel.국내공동구매.활성);
        Assert.Equal(공동구매절차코드.거래상대연결, fixture.ViewModel.상태.진행단계코드);
        Assert.False(await fixture.ViewModel.공급.생산자연결.후보조회Async());
        Assert.Contains("국내 공동구매 분기", fixture.ViewModel.공급.생산자연결.오류메시지);
        Assert.True(fixture.ViewModel.공동수입.계약확정준비완료);

        Assert.True(await fixture.ViewModel.공동수입.해외판매자연결완료Async());
        Assert.Equal(공동구매절차코드.공급조건협상, fixture.ViewModel.상태.진행단계코드);
        Assert.True(await fixture.ViewModel.공동수입.수입조건확정Async());
        Assert.Equal(공동구매절차코드.이의검토, fixture.ViewModel.상태.진행단계코드);
        Assert.True(await fixture.ViewModel.공동수입.최종이의검토완료Async());
        Assert.Equal(공동구매절차코드.확정안, fixture.ViewModel.상태.진행단계코드);
    }

    [Fact]
    public void 절차카탈로그_거래경로분기부터커머스까지업무순서대로배치한다()
    {
        Assert.Equal(
            [
                공동구매절차코드.제안,
                공동구매절차코드.거래경로,
                공동구매절차코드.수요모집,
                공동구매절차코드.거래상대연결,
                공동구매절차코드.공급조건협상,
                공동구매절차코드.이의검토,
                공동구매절차코드.확정안,
                공동구매절차코드.전자서명,
                공동구매절차코드.이행계획,
                공동구매절차코드.실행,
                공동구매절차코드.커머스
            ],
            공동구매절차카탈로그.전체.Select(stage => stage.코드));
    }

    [Fact]
    public async Task 가격의사결정_선택된거래경로와저장된목표단가로분기별자료를조회한다()
    {
        var priceService = new Fake공동구매가격의사결정Service
        {
            응답 = new 공동구매가격의사결정결과
            {
                자료있음 = true,
                상태코드 = "Complete",
                기준비교목록 =
                [
                    new 공동구매가격기준비교(
                        "domestic-retail",
                        "국내 소매 평균가격",
                        "aT",
                        12_000m,
                        9_000m,
                        3_000m,
                        0.25m,
                        공동구매가격판단신호코드.제안가격경쟁력,
                        "제안가격이 낮음",
                        "국내 소매가격보다 낮습니다.")
                ]
            }
        };
        using var fixture = CreateFixture(
            new Fake공동구매업무Service(),
            priceService: priceService);
        var campaign = Campaign("감자 공동구매", DateTime.UtcNow, hsCode: "070190");
        campaign.GroupPurchase!.TradeRouteCode = CommunityGroupPurchaseTradeRouteCodes.Domestic;
        campaign.GroupPurchase.HsCode = "070190";
        campaign.GroupPurchase.TargetUnitPriceKrwPerKg = 9_000m;
        fixture.ViewModel.상태.선택적용(campaign);

        Assert.Same(
            fixture.ViewModel.가격의사결정,
            fixture.ViewModel.국내공동구매.가격의사결정);
        Assert.Same(
            fixture.ViewModel.가격의사결정,
            fixture.ViewModel.공동수입.가격의사결정);
        Assert.True(await fixture.ViewModel.가격의사결정.조회Async());

        Assert.Equal(
            공동구매가격의사결정유형코드.국내공동구매,
            priceService.마지막요청?.유형코드);
        Assert.Equal(9_000m, priceService.마지막요청?.제안단가KrwPerKg);
        Assert.True(fixture.ViewModel.가격의사결정.가격정보최신);
        Assert.True(fixture.ViewModel.가격의사결정.의사결정근거충분);

        var result = fixture.ViewModel.가격의사결정.결과;
        fixture.ViewModel.상태.단계선택(공동구매절차코드.공급조건협상);
        Assert.Same(result, fixture.ViewModel.가격의사결정.결과);
        Assert.True(fixture.ViewModel.가격의사결정.가격정보최신);

        fixture.ViewModel.가격의사결정.제안가격Krw = 9_500m;
        Assert.False(fixture.ViewModel.가격의사결정.가격정보최신);
    }

    [Fact]
    public void 절차단계선택_화면탭만바꾸고실제진행단계는변경하지않는다()
    {
        var campaign = Campaign("감자 공동구매", DateTime.UtcNow);
        using var fixture = CreateFixture(new Fake공동구매업무Service());
        fixture.ViewModel.상태.선택적용(campaign);

        fixture.ViewModel.상태.단계선택(공동구매절차코드.커머스);

        Assert.Equal(공동구매절차코드.커머스, fixture.ViewModel.상태.현재단계코드);
        Assert.Equal(공동구매절차코드.수요모집, fixture.ViewModel.상태.진행단계코드);
        Assert.Equal(
            공동구매절차단계상태.진행중,
            fixture.ViewModel.합의.절차상태.단계상태(공동구매절차코드.수요모집));
        Assert.Equal(
            공동구매절차단계상태.대기,
            fixture.ViewModel.합의.절차상태.단계상태(공동구매절차코드.커머스));
    }

    [Fact]
    public async Task 합의흐름_모집마감부터전원서명까지단계상태를전이한다()
    {
        var openCampaign = Campaign("감자 공동구매", DateTime.UtcNow, sourcePostId: 33);
        var closedCampaign = Campaign(
            openCampaign.Title,
            openCampaign.CreatedAtUtc,
            openCampaign.SourcePostId,
            id: openCampaign.Id,
            status: CommunityVoteStatusCodes.Closed);
        var draftDocument = ResolutionDocument(
            openCampaign.Id,
            CommunityVoteResolutionStatusCodes.LegalReviewRequired);
        var readyDocument = ResolutionDocument(
            openCampaign.Id,
            CommunityVoteResolutionStatusCodes.ReadyToSign,
            signed: false);
        var signedDocument = ResolutionDocument(
            openCampaign.Id,
            CommunityVoteResolutionStatusCodes.Signed,
            signed: true);
        var service = new Fake공동구매업무Service
        {
            모집마감응답 = closedCampaign,
            결의문응답 = draftDocument,
            서명준비응답 = readyDocument,
            전자서명응답 = signedDocument
        };
        using var fixture = CreateFixture(service);
        var viewModel = fixture.ViewModel;
        viewModel.상태.목록적용([openCampaign]);
        viewModel.상태.선택적용(openCampaign);

        Assert.Equal(
            공동구매절차단계상태.진행중,
            viewModel.합의.절차상태.단계상태(공동구매절차코드.수요모집));
        Assert.Equal(
            공동구매절차단계상태.대기,
            viewModel.합의.절차상태.단계상태(공동구매절차코드.확정안));

        viewModel.합의.모집마감.이의검토완료 = true;
        Assert.True(await viewModel.합의.모집마감.마감Async());
        Assert.Equal(CommunityVoteStatusCodes.Closed, viewModel.상태.선택된공동구매?.Status);
        Assert.Equal(공동구매절차코드.거래상대연결, viewModel.상태.진행단계코드);
        Assert.Contains("제안의 선후만으로", viewModel.합의.결의.결의문본문);

        viewModel.상태.단계진행(공동구매절차코드.공급조건협상);
        viewModel.상태.단계진행(공동구매절차코드.이의검토);
        viewModel.상태.단계진행(공동구매절차코드.확정안);
        Assert.True(await viewModel.합의.결의.결의문작성Async());
        Assert.Same(draftDocument, viewModel.상태.선택된공동구매?.ResolutionDocument);
        Assert.True(await viewModel.합의.결의.서명준비Async());
        Assert.Equal(공동구매절차코드.전자서명, viewModel.상태.현재단계코드);
        Assert.Single(viewModel.합의.전자서명.미서명자);
        Assert.Equal(
            "resolution-1",
            viewModel.실행.주문원장.서명.참고공동구매결의문번호);
        Assert.Equal(
            "document-hash",
            viewModel.실행.주문원장.서명.참고공동구매결의문Hash);
        Assert.Empty(viewModel.실행.주문원장.서명.서명준비초안.계약문서번호);
        Assert.Contains("합의한 최종 계약문", viewModel.실행.주문원장.서명.계약문서안내);

        Assert.True(viewModel.합의.전자서명.서명자선택("party-1"));
        viewModel.합의.전자서명.결의문동의 = true;
        Assert.True(await viewModel.합의.전자서명.서명제출Async(
            new 공동구매전자서명입력("참여자 1", "data:image/png;base64,test")));

        Assert.Equal(공동구매절차코드.이행계획, viewModel.상태.진행단계코드);
        Assert.True(viewModel.합의.절차상태.실행준비완료);
        Assert.Equal(
            공동구매절차단계상태.완료,
            viewModel.합의.절차상태.단계상태(공동구매절차코드.전자서명));
        Assert.Equal(
            공동구매절차단계상태.진행중,
            viewModel.합의.절차상태.단계상태(공동구매절차코드.이행계획));
        Assert.Equal(
            공동구매절차단계상태.대기,
            viewModel.합의.절차상태.단계상태(공동구매절차코드.실행));
        Assert.Equal("party-1", service.마지막전자서명요청?.PartyId);
        Assert.True(viewModel.합의.전자서명.전원서명완료);
    }

    [Fact]
    public async Task 수요참여_공동수령방식은수령소선택을요구한다()
    {
        var campaign = Campaign("쌀 공동구매", DateTime.UtcNow);
        using var fixture = CreateFixture(new Fake공동구매업무Service());
        fixture.ViewModel.상태.목록적용([campaign]);
        fixture.ViewModel.상태.선택적용(campaign);
        var demand = fixture.ViewModel.모집.수요참여;
        demand.참여자표시명 = "주문자";
        demand.참여방식코드 = CommunityVoteParticipationMethodCodes.PickupPoint;
        demand.수령소Id = null;

        var succeeded = await demand.참여Async();

        Assert.False(succeeded);
        Assert.Contains("공동 수령소", demand.오류메시지);
    }

    [Fact]
    public async Task 생산자연결_동의된후보를연락요청초안으로변환한다()
    {
        var campaign = Campaign(
            "양파 공동구매",
            DateTime.UtcNow,
            status: CommunityVoteStatusCodes.Closed);
        var supplyService = new Fake공동구매공급Service
        {
            생산자후보응답 = new DomesticProducerCandidateQueryResponse
            {
                IntegrationStatusCode = DomesticProducerDirectoryIntegrationStatuses.Connected,
                Items =
                [
                    new DomesticProducerCandidateResponse
                    {
                        CandidateKey = "producer-1",
                        MaskedDisplayName = "무안 양파 농가 김○○",
                        ProductTags = ["양파"],
                        ThirdPartySharingConsentConfirmed = true,
                        ContactRequestConsentConfirmed = true
                    }
                ]
            },
            연락요청응답 = new DomesticProducerContactRequestDraftResponse
            {
                DraftId = Guid.NewGuid(),
                ProducerCandidateKey = "producer-1"
            },
            적합성응답 = new DomesticGroupPurchaseSupplyCompatibilityPreviewResponse
            {
                IsMutuallyFeasible = true
            }
        };
        using var fixture = CreateFixture(
            new Fake공동구매업무Service(),
            supplyService);
        fixture.ViewModel.상태.목록적용([campaign]);
        fixture.ViewModel.상태.선택적용(campaign);
        var connection = fixture.ViewModel.공급.생산자연결;

        Assert.True(await connection.후보조회Async());
        Assert.True(connection.생산자선택("producer-1"));
        Assert.True(await connection.연락요청저장Async());

        Assert.Equal("producer-1", connection.저장된연락요청?.ProducerCandidateKey);
        Assert.Equal(campaign.Id, supplyService.마지막연락요청?.GroupPurchaseCampaignId);
        Assert.Equal("양파", supplyService.마지막연락요청?.ProductSummary);
        Assert.True(await fixture.ViewModel.국내공동구매.거래상대연결완료Async());
        Assert.Equal(공동구매절차코드.공급조건협상, fixture.ViewModel.상태.진행단계코드);
        Assert.True(await fixture.ViewModel.공급.공급적합성.미리보기Async());
        Assert.True(await fixture.ViewModel.국내공동구매.공급조건확정Async());
        Assert.Equal(공동구매절차코드.이의검토, fixture.ViewModel.상태.진행단계코드);
        Assert.True(await fixture.ViewModel.국내공동구매.최종이의검토완료Async());
        Assert.Equal(공동구매절차코드.확정안, fixture.ViewModel.상태.진행단계코드);
    }

    [Fact]
    public async Task 이행계획_확정조건을발주원장초안으로변환한다()
    {
        var campaign = Campaign("쌀 공동구매", DateTime.UtcNow);
        campaign.GroupPurchase!.TotalRequestedQuantity = 100;
        campaign.GroupPurchase.QuantityUnit = "포대";
        var logisticsService = new Fake공동구매물류Service();
        using var fixture = CreateFixture(
            new Fake공동구매업무Service(),
            logisticsService: logisticsService);
        fixture.ViewModel.상태.목록적용([campaign]);
        fixture.ViewModel.상태.선택적용(campaign);
        var fulfillment = fixture.ViewModel.물류.이행계획;
        fulfillment.초안.ProducerTermsAccepted = true;
        fulfillment.초안.BuyerRepresentativeTermsAccepted = true;
        fulfillment.초안.SupplyCompatibilityConfirmed = true;
        fulfillment.입력변경알림();
        logisticsService.발주초안응답 = new DomesticGroupPurchaseFulfillmentOrderDraftResponse
        {
            DraftId = Guid.NewGuid(),
            Plan = fulfillment.계획!
        };

        Assert.True(fulfillment.발주초안생성가능);
        Assert.True(await fulfillment.발주초안저장Async());
        Assert.NotNull(fulfillment.저장된발주초안);
        Assert.Equal(campaign.Id, logisticsService.마지막발주요청?.GroupPurchaseCampaignId);
        Assert.Equal(100, logisticsService.마지막발주요청?.PlannedQuantity);
    }

    [Fact]
    public async Task 자동집단_선택된공동구매를수요초안으로만들고실행Id를공유한다()
    {
        var campaign = Campaign("햇감자 공동구매", DateTime.UtcNow, sourcePostId: 51);
        campaign.CommunityLedgerId = "community-ledger-51";
        campaign.CreatedByDisplayName = "주문자";
        campaign.Options[0].ProductKey = "potato-10kg";
        campaign.Options[0].RequestedQuantity = 4;
        campaign.GroupPurchase!.ServiceAreaKey = "seoul-east";
        campaign.GroupPurchase.ServiceAreaLabel = "서울 동부권";
        campaign.GroupPurchase.MinimumParticipantCount = 5;
        var executionService = new Fake공동구매실행Service
        {
            자동수요응답 = new 공동구매자동집단응답
            {
                자동집단Id = "auto-potato-seoul",
                공동구매주문집계원장Id = "aggregation-potato-seoul",
                상품키 = "potato-10kg",
                배송권키 = "seoul-east",
                현재상태 = 공동구매자동집단상태코드.수요수집중,
                수요목록 =
                [
                    new 공동구매자동수요응답
                    {
                        수요Id = "demand-51",
                        수요출처키 = $"community-vote:{campaign.Id:N}:orderer-51",
                        주문자키 = "orderer-51",
                        공동구매주문집계원장Id = "aggregation-potato-seoul",
                        개별주문원장Id = "individual-order-51"
                    }
                ]
            }
        };
        using var fixture = CreateFixture(
            new Fake공동구매업무Service(),
            executionService: executionService);
        fixture.ViewModel.상태.선택적용(campaign);
        var automaticGroup = fixture.ViewModel.실행.자동집단;

        Assert.Equal("potato-10kg", automaticGroup.수요초안.상품키);
        Assert.Equal("seoul-east", automaticGroup.수요초안.배송권키);
        Assert.Equal(1, automaticGroup.수요초안.희망수량);
        automaticGroup.수요초안.주문자키 = "orderer-51";
        automaticGroup.수요초안.주문자표시명 = "주문자";

        Assert.True(await automaticGroup.수요등록Async());

        Assert.Equal("auto-potato-seoul", fixture.ViewModel.실행.상태.실행공동구매Id);
        Assert.Equal("aggregation-potato-seoul", fixture.ViewModel.실행.상태.공동구매주문집계원장Id);
        Assert.Equal("individual-order-51", fixture.ViewModel.실행.상태.선택된주문원장Id);
        Assert.Same(executionService.자동수요응답, automaticGroup.선택된자동집단);
        Assert.Equal(
            $"community-vote:{campaign.Id:N}:orderer-51",
            executionService.마지막자동수요요청?.수요출처키);
    }

    [Fact]
    public async Task 주문원장_보호조회하위원장연결과서명을독립하위ViewModel로처리한다()
    {
        var campaign = Campaign("사과 공동구매", DateTime.UtcNow);
        campaign.ResolutionDocument = ResolutionDocument(
            campaign.Id,
            CommunityVoteResolutionStatusCodes.ReadyToSign);
        var executionService = new Fake공동구매실행Service
        {
            주문원장보호응답 = ProtectedOrderLedger("order-root-1", revision: 1),
            하위원장연결응답 = OrderLedger("order-root-1", revision: 2, childLedgerId: "sales-ledger-1"),
            서명준비응답 = new 주문원장서명상태공개Dto
            {
                주문원장Id = "order-root-1",
                Revision = 3,
                상태Code = ContractSignatureStatusCode.WaitingForSignature,
                필수서명자수 = 1
            },
            서명등록응답 = new 주문원장서명상태공개Dto
            {
                주문원장Id = "order-root-1",
                Revision = 4,
                상태Code = ContractSignatureStatusCode.Signed,
                필수서명자수 = 1,
                서명완료자수 = 1,
                전체서명완료여부 = true
            }
        };
        using var fixture = CreateFixture(
            new Fake공동구매업무Service(),
            executionService: executionService);
        fixture.ViewModel.상태.선택적용(campaign);
        var orderLedger = fixture.ViewModel.실행.주문원장;
        orderLedger.조회.주문원장선택("order-root-1");

        Assert.True(await orderLedger.조회.조회Async());
        Assert.Equal(1, orderLedger.하위원장.현재Revision);

        orderLedger.하위원장.연결초안.하위원장Id = "sales-ledger-1";
        orderLedger.하위원장.연결초안.역할 = 주문원장포함역할.판매;
        Assert.True(await orderLedger.하위원장.연결Async());
        Assert.Equal(2, orderLedger.하위원장.현재Revision);
        Assert.Equal("sales-ledger-1", executionService.마지막하위원장연결요청?.하위원장Id);

        Assert.Equal("resolution-1", orderLedger.서명.참고공동구매결의문번호);
        orderLedger.서명.서명준비초안.계약문서번호 = "ORDER-2026-0001";
        orderLedger.서명.서명준비초안.문서Hash = "order-document-hash";
        Assert.True(await orderLedger.서명.서명준비Async());
        orderLedger.서명.서명등록초안.동의문Hash = "consent-hash";
        orderLedger.서명.서명등록초안.서명증적Hash = "evidence-hash";
        Assert.True(await orderLedger.서명.서명등록Async());
        Assert.True(orderLedger.서명.전체서명완료);
        Assert.Equal(4, orderLedger.서명.서명상태?.Revision);
    }

    [Fact]
    public async Task 커머스이행_자동집단Id로조회하고현재단계의다음작업을제시한다()
    {
        var executionService = new Fake공동구매실행Service
        {
            공동구매커머스응답 =
            [
                new 공동구매커머스이행계획공개Dto
                {
                    공동구매Id = "auto-group-77",
                    문서관리번호 = "commerce-77",
                    주문자집단배송권키 = "seoul",
                    현재상태코드 = 공동구매커머스이행상태코드.입고완료,
                    판매가능수량 = 80
                }
            ]
        };
        using var fixture = CreateFixture(
            new Fake공동구매업무Service(),
            executionService: executionService);
        fixture.ViewModel.상태.선택적용(Campaign("커머스 이행 공동구매", DateTime.UtcNow));
        fixture.ViewModel.실행.상태.실행공동구매선택("auto-group-77");
        var commerce = fixture.ViewModel.실행.커머스이행;

        Assert.True(await commerce.공동구매별조회Async());

        Assert.Equal("commerce-77", commerce.선택된계획?.문서관리번호);
        Assert.Equal(
            공동구매커머스단계상태.진행중,
            commerce.진행단계.Single(stage =>
                stage.코드 == 공동구매커머스이행상태코드.입고완료).상태);
        Assert.Contains("판매 채널", commerce.다음작업안내);
        Assert.False(commerce.변경가능);
    }

    private static TestFixture CreateFixture(
        Fake공동구매업무Service service,
        Fake공동구매공급Service? supplyService = null,
        Fake공동구매물류Service? logisticsService = null,
        Fake공동구매실행Service? executionService = null,
        Fake공동구매가격의사결정Service? priceService = null,
        Fake공동구매원장절차Client? ledgerProgressClient = null,
        Fake공동수입선적통관Client? shipmentCustomsClient = null,
        IOperatingMarketProfileClient? operatingMarketProfileClient = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<I공동구매업무Service>(service);
        services.AddSingleton<I공동구매공급Service>(supplyService ?? new Fake공동구매공급Service());
        services.AddSingleton<I공동구매물류Service>(logisticsService ?? new Fake공동구매물류Service());
        services.AddSingleton<I공동구매실행Service>(executionService ?? new Fake공동구매실행Service());
        services.AddSingleton<I공동구매창고Service>(new Fake공동구매창고Service());
        services.AddSingleton<I공동구매원장절차Client>(
            ledgerProgressClient ?? new Fake공동구매원장절차Client());
        services.AddSingleton<I공동수입원장전환Client>(new Fake공동수입원장전환Client());
        services.AddSingleton<I공동수입선적통관Client>(
            shipmentCustomsClient ?? new Fake공동수입선적통관Client());
        services.AddSingleton<I판매채널Client>(new Fake판매채널Client());
        services.AddSingleton<I공동구매가격의사결정Service>(
            priceService ?? new Fake공동구매가격의사결정Service());
        services.AddSingleton<IOperatingMarketProfileClient>(
            operatingMarketProfileClient ?? new FakeOperatingMarketProfileClient("KR"));
        services.AddHongdalUiCommonAppServices();
        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        return new TestFixture(
            provider,
            scope,
            scope.ServiceProvider.GetRequiredService<공동구매화면ViewModel>());
    }

    private static void FillProposal(공동구매제안ViewModel proposal)
    {
        proposal.제목 = "여름 채소 공동구매";
        proposal.설명 = "산지 채소를 함께 구매합니다.";
        proposal.상품명 = "감자 10kg";
        proposal.제안자표시명 = "제안자";
        proposal.게시글비밀번호 = "test-password";
    }

    private static CommunityVoteResponse Campaign(
        string title,
        DateTime createdAtUtc,
        long? sourcePostId = null,
        string hsCode = "",
        Guid? id = null,
        string status = CommunityVoteStatusCodes.Open)
        => new()
        {
            Id = id ?? Guid.NewGuid(),
            Title = title,
            CreatedAtUtc = createdAtUtc,
            SourcePostId = sourcePostId,
            Status = status,
            Options =
            [
                new CommunityVoteOptionResponse
                {
                    OptionId = "option-1",
                    Text = title,
                    HsCode = hsCode,
                    QuantityUnit = "개"
                }
            ],
            GroupPurchase = new CommunityGroupPurchaseVoteResponse
            {
                QuantityUnit = "개",
                ServiceAreaLabel = "platform"
            }
        };

    private static CommunityVoteResolutionDocumentResponse ResolutionDocument(
        Guid voteId,
        string status,
        bool? signed = null)
    {
        ContractElectronicSignaturePlan? plan = null;
        if (signed is not null)
        {
            var request = new ContractSignatureRequest(
                "party-1",
                "CommunityParticipant",
                "참여자 1");
            var bundle = new ContractElectronicSignatureBundle(
                "resolution-1",
                "document-hash",
                [request],
                [],
                DateTimeOffset.UtcNow);
            plan = new ContractElectronicSignaturePlan(
                bundle,
                signed.Value ? ContractSignatureStatusCode.Signed : ContractSignatureStatusCode.WaitingForSignature,
                1,
                signed.Value ? 1 : 0,
                signed.Value ? [] : ["party-1"],
                [],
                signed.Value,
                signed.Value ? "서명 완료" : "서명 대기");
        }

        return new CommunityVoteResolutionDocumentResponse
        {
            Id = Guid.NewGuid(),
            VoteId = voteId,
            DocumentNumber = "resolution-1",
            DocumentTitle = "공동구매 확정안",
            ResolutionText = "확정 내용",
            DocumentHash = "document-hash",
            Status = status,
            SignaturePlan = plan
        };
    }

    private static 주문원장통합공개Dto OrderLedger(
        string orderLedgerId,
        long revision,
        string? childLedgerId = null)
        => new()
        {
            주문원장 = new 주문원장원장요약Dto
            {
                원장Id = orderLedgerId,
                Revision = revision,
                상태 = "진행중"
            },
            포함원장목록 = childLedgerId is null
                ? []
                :
                [
                    new 주문포함원장공개Dto
                    {
                        원장Id = childLedgerId,
                        역할 = 주문원장포함역할.판매,
                        필수여부 = true
                    }
                ],
            전체하위원장수 = childLedgerId is null ? 0 : 1
        };

    private static 주문원장역할별조회공개Dto ProtectedOrderLedger(
        string orderLedgerId,
        long revision)
        => new()
        {
            주문원장Id = orderLedgerId,
            조회역할 = "주문자",
            주문원장상태 = "진행중",
            주문원장조회근거 = "소유자",
            주문원장상세 = new 주문원장원장요약Dto
            {
                원장Id = orderLedgerId,
                Revision = revision,
                상태 = "진행중"
            }
        };

    private sealed class TestFixture(
        ServiceProvider provider,
        IServiceScope scope,
        공동구매화면ViewModel viewModel) : IDisposable
    {
        public 공동구매화면ViewModel ViewModel { get; } = viewModel;

        public void Dispose()
        {
            scope.Dispose();
            provider.Dispose();
        }
    }

    private sealed class FakeOperatingMarketProfileClient(string countryCode)
        : IOperatingMarketProfileClient
    {
        public Task<OperatingMarketRuntimeProfileResponse?> GetCurrentAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<OperatingMarketRuntimeProfileResponse?>(new()
            {
                MarketCode = countryCode,
                CountryCode = countryCode
            });
    }

    private sealed class Fake공동구매업무Service : I공동구매업무Service
    {
        public CommunityVoteListResponse 목록응답 { get; } = new();
        public Dictionary<Guid, CommunityVoteResponse> 상세응답 { get; } = [];
        public Dictionary<long, IReadOnlyList<PlatformCommunityPostCommentResponse>> 의견응답 { get; } = [];
        public PlatformCommunityPostResponse? 생성게시글응답 { get; set; }
        public CommunityVoteResponse? 공동구매생성응답 { get; set; }
        public Exception? 공동구매생성예외 { get; set; }
        public CommunityVoteResponse? 수요참여응답 { get; set; }
        public PlatformCommunityPostCommentResponse? 이의등록응답 { get; set; }
        public CommunityVoteResponse? 모집마감응답 { get; set; }
        public CommunityVoteResolutionDocumentResponse? 결의문응답 { get; set; }
        public CommunityVoteResolutionDocumentResponse? 서명준비응답 { get; set; }
        public CommunityVoteResolutionDocumentResponse? 전자서명응답 { get; set; }
        public PlatformCommunityPostCreateRequest? 마지막제안글요청 { get; private set; }
        public CommunityVoteCreateRequest? 마지막공동구매생성요청 { get; private set; }
        public CommunityVoteResolutionSignRequest? 마지막전자서명요청 { get; private set; }
        public string? 마지막목록커뮤니티범위 { get; private set; }
        public string? 마지막목록HS코드 { get; private set; }

        public Task<CommunityVoteListResponse> 목록조회Async(
            string? communityScope = null,
            string? hsCode = null,
            CancellationToken cancellationToken = default)
        {
            마지막목록커뮤니티범위 = communityScope;
            마지막목록HS코드 = hsCode;
            return Task.FromResult(목록응답);
        }

        public Task<CommunityVoteResponse?> 상세조회Async(
            Guid voteId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(상세응답.GetValueOrDefault(voteId));

        public Task<PlatformCommunityPostResponse?> 제안글생성Async(
            PlatformCommunityPostCreateRequest request,
            CancellationToken cancellationToken = default)
        {
            마지막제안글요청 = request;
            return Task.FromResult(생성게시글응답);
        }

        public Task<CommunityVoteResponse?> 공동구매생성Async(
            CommunityVoteCreateRequest request,
            CancellationToken cancellationToken = default)
        {
            마지막공동구매생성요청 = request;
            return 공동구매생성예외 is null
                ? Task.FromResult(공동구매생성응답)
                : Task.FromException<CommunityVoteResponse?>(공동구매생성예외);
        }

        public Task<IReadOnlyList<PlatformCommunityPostCommentResponse>> 의견조회Async(
            long postId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(의견응답.GetValueOrDefault(postId) ?? []);

        public Task<CommunityVoteResponse?> 수요참여Async(
            Guid voteId,
            CommunityVoteCastRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(수요참여응답);

        public Task<PlatformCommunityPostCommentResponse?> 이의등록Async(
            long postId,
            PlatformCommunityPostCommentCreateRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(이의등록응답);

        public Task<CommunityVoteResponse?> 모집마감Async(
            Guid voteId,
            CommunityVoteCloseRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(모집마감응답);

        public Task<CommunityVoteResolutionDocumentResponse?> 결의문생성Async(
            Guid voteId,
            CommunityVoteResolutionDraftRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(결의문응답);

        public Task<CommunityVoteResolutionDocumentResponse?> 서명준비Async(
            Guid voteId,
            CommunityVoteResolutionReadyToSignRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(서명준비응답);

        public Task<CommunityVoteResolutionDocumentResponse?> 전자서명Async(
            Guid voteId,
            CommunityVoteResolutionSignRequest request,
            CancellationToken cancellationToken = default)
        {
            마지막전자서명요청 = request;
            return Task.FromResult(전자서명응답);
        }
    }

    private sealed class Fake공동구매공급Service : I공동구매공급Service
    {
        public DomesticProducerCandidateQueryResponse 생산자후보응답 { get; set; } = new();
        public DomesticProducerContactRequestDraftResponse? 연락요청응답 { get; set; }
        public DomesticGroupPurchaseRepresentativeCandidateQueryResponse 대표후보응답 { get; set; } = new();
        public DomesticProducerSupplyOfferDraftResponse? 공급제안응답 { get; set; }
        public DomesticGroupPurchaseSupplyCompatibilityPreviewResponse? 적합성응답 { get; set; }
        public DomesticGroupPurchaseNegotiationTimelineResponse 협상이력응답 { get; set; } = new();
        public DomesticProducerContactRequestDraftRequest? 마지막연락요청 { get; private set; }

        public Task<DomesticProducerCandidateQueryResponse> 생산자후보조회Async(
            Guid campaignId,
            string? search = null,
            string? regionCode = null,
            string? product = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(생산자후보응답);

        public Task<DomesticProducerContactRequestDraftResponse?> 연락요청초안생성Async(
            Guid campaignId,
            DomesticProducerContactRequestDraftRequest request,
            CancellationToken cancellationToken = default)
        {
            마지막연락요청 = request;
            return Task.FromResult(연락요청응답);
        }

        public Task<DomesticGroupPurchaseRepresentativeCandidateQueryResponse> 대표후보조회Async(
            Guid campaignId,
            string? search = null,
            string? operatingAreaCode = null,
            string? product = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(대표후보응답);

        public Task<DomesticProducerSupplyOfferDraftResponse?> 공급제안초안생성Async(
            Guid campaignId,
            DomesticProducerSupplyOfferDraftRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(공급제안응답);

        public Task<DomesticGroupPurchaseSupplyCompatibilityPreviewResponse?> 공급적합성미리보기Async(
            Guid campaignId,
            DomesticGroupPurchaseSupplyCompatibilityPreviewRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(적합성응답);

        public Task<DomesticGroupPurchaseNegotiationTimelineResponse> 협상이력조회Async(
            Guid campaignId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(협상이력응답);

        public Task<DomesticGroupPurchaseNegotiationEventResponse?> 협상이벤트등록Async(
            Guid campaignId,
            DomesticGroupPurchaseNegotiationEventRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<DomesticGroupPurchaseNegotiationEventResponse?>(new());

        public Task<DomesticGroupPurchaseNegotiationIssueResponse?> 협상쟁점등록Async(
            Guid campaignId,
            DomesticGroupPurchaseNegotiationIssueRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<DomesticGroupPurchaseNegotiationIssueResponse?>(new() { IssueId = Guid.NewGuid() });

        public Task<DomesticGroupPurchaseNegotiationIssueResponse?> 숙고의견등록Async(
            Guid campaignId,
            Guid issueId,
            DomesticGroupPurchaseDeliberationPositionRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<DomesticGroupPurchaseNegotiationIssueResponse?>(new() { IssueId = issueId });

        public Task<DomesticGroupPurchaseNegotiationIssueResponse?> 협상쟁점합의Async(
            Guid campaignId,
            Guid issueId,
            DomesticGroupPurchaseNegotiationResolutionRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<DomesticGroupPurchaseNegotiationIssueResponse?>(new()
            {
                IssueId = issueId,
                StatusCode = DomesticGroupPurchaseNegotiationIssueStatusCodes.Resolved
            });
    }

    private sealed class Fake공동구매물류Service : I공동구매물류Service
    {
        public DomesticGroupPurchaseFulfillmentPlanResponse? 미리보기응답 { get; set; }
        public DomesticGroupPurchaseFulfillmentOrderDraftResponse? 발주초안응답 { get; set; }
        public DomesticGroupPurchaseFulfillmentPlanRequest? 마지막발주요청 { get; private set; }

        public Task<DomesticGroupPurchaseFulfillmentPlanResponse?> 이행계획미리보기Async(
            Guid campaignId,
            DomesticGroupPurchaseFulfillmentPlanRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(미리보기응답);

        public Task<DomesticGroupPurchaseFulfillmentOrderDraftResponse?> 발주초안생성Async(
            Guid campaignId,
            DomesticGroupPurchaseFulfillmentPlanRequest request,
            CancellationToken cancellationToken = default)
        {
            마지막발주요청 = request;
            return Task.FromResult(발주초안응답);
        }
    }

    private sealed class Fake공동구매원장절차Client : I공동구매원장절차Client
    {
        private readonly Dictionary<Guid, CommunityGroupPurchaseLedgerProgressResponse> _progress = [];

        public Task<CommunityGroupPurchaseLedgerProgressResponse?> 조회Async(
            Guid campaignId,
            CancellationToken cancellationToken = default)
        {
            if (!_progress.TryGetValue(campaignId, out var progress))
            {
                progress = Create(campaignId, 공동구매절차코드.수요모집, revision: 1);
                _progress[campaignId] = progress;
            }

            return Task.FromResult<CommunityGroupPurchaseLedgerProgressResponse?>(progress);
        }

        public Task<CommunityGroupPurchaseLedgerProgressResponse?> 진행Async(
            Guid campaignId,
            CommunityGroupPurchaseLedgerProgressRequest request,
            CancellationToken cancellationToken = default)
        {
            var revision = _progress.TryGetValue(campaignId, out var current)
                ? current.Revision + 1
                : 1;
            var progress = Create(campaignId, request.StageCode, revision);
            _progress[campaignId] = progress;
            return Task.FromResult<CommunityGroupPurchaseLedgerProgressResponse?>(progress);
        }

        private static CommunityGroupPurchaseLedgerProgressResponse Create(
            Guid campaignId,
            string stageCode,
            long revision)
            => new()
            {
                GroupPurchaseCampaignId = campaignId,
                CommunityLedgerId = $"group-purchase-{campaignId:N}",
                Revision = revision,
                LedgerStatus = "진행중",
                CurrentStageCode = stageCode,
                AutomaticallyLinked = true
            };
    }

    private sealed class Fake공동수입원장전환Client : I공동수입원장전환Client
    {
        private readonly Dictionary<Guid, CommunityGroupImportLedgerPlanResponse> _ledgers = [];

        public Task<CommunityGroupImportLedgerPlanResponse?> 조회Async(
            Guid campaignId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<CommunityGroupImportLedgerPlanResponse?>(_ledgers.GetValueOrDefault(campaignId));

        public Task<CommunityGroupImportLedgerPlanResponse?> 미리보기Async(
            Guid campaignId,
            CommunityGroupImportLedgerConversionRequest request,
            CancellationToken cancellationToken = default)
        {
            request.GroupPurchaseCampaignId = campaignId;
            return Task.FromResult<CommunityGroupImportLedgerPlanResponse?>(
                CommunityGroupImportLedgerPlanBuilder.Preview(request));
        }

        public Task<CommunityGroupImportLedgerPlanResponse?> 전환Async(
            Guid campaignId,
            CommunityGroupImportLedgerConversionRequest request,
            CancellationToken cancellationToken = default)
        {
            request.GroupPurchaseCampaignId = campaignId;
            var response = CommunityGroupImportLedgerPlanBuilder.Preview(request);
            response.Created = true;
            response.GroupImportLedgerId = $"group-import-{campaignId:N}";
            response.Revision = 1;
            _ledgers[campaignId] = response;
            return Task.FromResult<CommunityGroupImportLedgerPlanResponse?>(response);
        }
    }

    private sealed class Fake공동수입선적통관Client : I공동수입선적통관Client
    {
        public 공동구매해외선적추적저장요청? LastSaveRequest { get; private set; }
        private 공동구매해외선적추적Dto? _shipment;

        public Task<공동구매해외선적공개Dto?> 공개조회Async(
            string documentManagementNumber,
            CancellationToken cancellationToken = default)
            => Task.FromResult<공동구매해외선적공개Dto?>(null);

        public Task<IReadOnlyList<공동구매해외선적추적Dto>?> 관리자목록Async(
            공동구매해외선적추적조회조건 condition,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<공동구매해외선적추적Dto>?>([]);

        public Task<공동구매해외선적추적Dto?> 관리자저장Async(
            공동구매해외선적추적저장요청 request,
            CancellationToken cancellationToken = default)
        {
            LastSaveRequest = request;
            _shipment = new 공동구매해외선적추적Dto
            {
                추적Id = request.추적Id ?? "shipment-1",
                공동구매Id = request.공동구매Id,
                주문자집단배송권키 = request.주문자집단배송권키,
                주문자집단배송권명 = request.주문자집단배송권명,
                상품요약 = request.상품요약,
                문서관리번호 = request.문서관리번호,
                운송문서유형 = request.운송문서유형,
                운송문서번호 = request.운송문서번호,
                운송수단 = request.운송수단,
                출발국가코드 = request.출발국가코드,
                현재상태코드 = request.현재상태코드
            };
            return Task.FromResult<공동구매해외선적추적Dto?>(_shipment);
        }

        public Task<공동구매해외선적추적Dto?> 관리자이벤트추가Async(
            string documentManagementNumber,
            공동구매해외선적추적이벤트추가요청 request,
            CancellationToken cancellationToken = default)
        {
            _shipment ??= new 공동구매해외선적추적Dto { 문서관리번호 = documentManagementNumber };
            _shipment.현재상태코드 = request.이벤트코드;
            _shipment.현재위치요약 = request.위치요약;
            return Task.FromResult<공동구매해외선적추적Dto?>(_shipment);
        }

        public Task<공동구매해외선적통관동기화결과?> 관리자통관동기화Async(
            공동구매해외선적통관동기화요청 request,
            CancellationToken cancellationToken = default)
        {
            _shipment ??= new 공동구매해외선적추적Dto { 문서관리번호 = request.문서관리번호 };
            _shipment.현재상태코드 = 공동구매선적상태코드.통관완료;
            return Task.FromResult<공동구매해외선적통관동기화결과?>(new 공동구매해외선적통관동기화결과
            {
                동기화됨 = true,
                메시지 = "통관 완료",
                통관단계명 = "수입신고수리",
                선적 = _shipment
            });
        }
    }

    private sealed class Fake판매채널Client : I판매채널Client
    {
        public Task<IReadOnlyList<판매채널계정항목응답>> 계정목록조회Async(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<판매채널계정항목응답>>([]);

        public Task<판매채널계정항목응답?> 계정생성Async(
            판매채널계정저장요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<판매채널계정항목응답?>(new 판매채널계정항목응답
            {
                Id = 1,
                채널종류 = request.채널종류,
                상점명 = request.상점명,
                연결상태 = "Connected"
            });

        public Task<판매채널계정항목응답?> 계정수정Async(
            long accountId,
            판매채널계정저장요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<판매채널계정항목응답?>(new 판매채널계정항목응답
            {
                Id = accountId,
                채널종류 = request.채널종류,
                상점명 = request.상점명,
                연결상태 = "Connected"
            });

        public Task 계정삭제Async(long accountId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<판매상품항목응답>> 상품목록조회Async(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<판매상품항목응답>>([]);

        public Task<판매상품항목응답?> 상품생성Async(
            판매상품저장요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<판매상품항목응답?>(new 판매상품항목응답
            {
                Id = 1,
                입고상품Id = request.입고상품Id,
                대표상품명 = request.대표상품명,
                판매SKU = request.판매SKU,
                판매가 = request.판매가
            });

        public Task<판매상품항목응답?> 상품수정Async(
            long productId,
            판매상품저장요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<판매상품항목응답?>(new 판매상품항목응답
            {
                Id = productId,
                입고상품Id = request.입고상품Id,
                대표상품명 = request.대표상품명,
                판매SKU = request.판매SKU,
                판매가 = request.판매가
            });

        public Task 상품삭제Async(long productId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<채널출품항목응답>> 출품목록조회Async(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<채널출품항목응답>>([]);

        public Task<채널출품항목응답?> 출품생성Async(
            채널출품저장요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<채널출품항목응답?>(new 채널출품항목응답
            {
                Id = 1,
                판매상품Id = request.판매상품Id,
                판매채널계정Id = request.판매채널계정Id,
                출품상태 = "Draft"
            });

        public Task<채널출품항목응답?> 출품수정Async(
            long listingId,
            채널출품저장요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<채널출품항목응답?>(new 채널출품항목응답
            {
                Id = listingId,
                판매상품Id = request.판매상품Id,
                판매채널계정Id = request.판매채널계정Id,
                출품상태 = "Draft"
            });

        public Task 출품삭제Async(long listingId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class Fake공동구매창고Service : I공동구매창고Service
    {
        public Task<IReadOnlyList<창고요약응답>> 창고목록조회Async(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<창고요약응답>>([]);

        public Task<창고요약응답?> 창고생성Async(
            창고저장요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<창고요약응답?>(null);

        public Task<창고요약응답?> 창고수정Async(
            long warehouseId,
            창고저장요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<창고요약응답?>(null);

        public Task 창고삭제Async(long warehouseId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<창고사용자항목응답>> 창고사용자목록조회Async(
            long warehouseId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<창고사용자항목응답>>([]);

        public Task<창고사용자항목응답?> 창고사용자추가Async(
            long warehouseId,
            창고사용자저장요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<창고사용자항목응답?>(null);

        public Task<창고사용자항목응답?> 창고사용자수정Async(
            long warehouseId,
            long warehouseUserId,
            창고사용자저장요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<창고사용자항목응답?>(null);

        public Task 창고사용자삭제Async(
            long warehouseId,
            long warehouseUserId,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<입고요청항목응답>> 입고목록조회Async(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<입고요청항목응답>>([]);

        public Task<입고요청페이지응답> 입고예정관점목록조회Async(
            string perspectiveCode,
            string? communityLedgerId,
            입고요청목록조회요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new 입고요청페이지응답());

        public Task<출고예정페이지응답> 출고예정관점목록조회Async(
            string perspectiveCode,
            string? communityLedgerId,
            출고예정목록조회요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new 출고예정페이지응답());

        public Task<입고요청항목응답?> 입고요청생성Async(
            입고요청저장요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<입고요청항목응답?>(null);

        public Task<입고요청항목응답?> 입고요청수정Async(
            long inboundId,
            입고요청저장요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<입고요청항목응답?>(null);

        public Task 입고요청취소Async(long inboundId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<입고상품항목응답>> 입고완료Async(
            long inboundId,
            입고완료요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<입고상품항목응답>>([]);

        public Task<IReadOnlyList<재고항목응답>> 재고목록조회Async(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<재고항목응답>>([]);

        public Task<창고작업결과응답?> 입고검수Async(
            long inboundItemId,
            입고검수요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<창고작업결과응답?>(null);

        public Task<창고작업결과응답?> 적재위치배정Async(
            long inboundItemId,
            적재위치배정요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<창고작업결과응답?>(null);

        public Task<창고작업결과응답?> 포장작업Async(
            long inboundItemId,
            포장작업요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<창고작업결과응답?>(null);

        public Task<화주운송의뢰응답?> 운송인계Async(
            재고운송의뢰생성요청 request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<화주운송의뢰응답?>(null);
    }

    private sealed class Fake공동구매실행Service : I공동구매실행Service
    {
        public IReadOnlyList<공동구매자동집단응답> 자동집단목록응답 { get; set; } = [];
        public 공동구매자동집단응답? 자동수요응답 { get; set; }
        public 공동구매자동수요등록Command? 마지막자동수요요청 { get; private set; }
        public 주문원장역할별조회공개Dto? 주문원장보호응답 { get; set; }
        public 주문원장역할별조회공개Dto? 주문원장역할응답 { get; set; }
        public 주문원장통합공개Dto? 하위원장연결응답 { get; set; }
        public 주문원장통합공개Dto? 하위원장분리응답 { get; set; }
        public 주문하위원장연결ClientRequest? 마지막하위원장연결요청 { get; private set; }
        public 주문원장서명상태공개Dto? 서명상태응답 { get; set; }
        public 주문원장서명상태공개Dto? 서명준비응답 { get; set; }
        public 주문원장서명상태공개Dto? 서명등록응답 { get; set; }
        public IReadOnlyList<공동구매커머스이행계획공개Dto> 공동구매커머스응답 { get; set; } = [];
        public IReadOnlyList<공동구매커머스이행계획공개Dto> 문서커머스응답 { get; set; } = [];

        public Task<IReadOnlyList<공동구매자동집단응답>> 자동집단목록조회Async(
            공동구매자동집단조회조건 condition,
            CancellationToken cancellationToken = default)
            => Task.FromResult(자동집단목록응답);

        public Task<공동구매자동집단응답?> 자동수요등록Async(
            공동구매자동수요등록Command request,
            CancellationToken cancellationToken = default)
        {
            마지막자동수요요청 = request;
            return Task.FromResult(자동수요응답);
        }

        public Task<주문원장역할별조회공개Dto?> 주문원장보호조회Async(
            string orderLedgerId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(주문원장보호응답);

        public Task<주문원장역할별조회공개Dto?> 주문원장역할조회Async(
            string orderLedgerId,
            string viewCode,
            CancellationToken cancellationToken = default)
            => Task.FromResult(주문원장역할응답);

        public Task<주문원장통합공개Dto?> 하위원장연결Async(
            string orderLedgerId,
            주문하위원장연결ClientRequest request,
            CancellationToken cancellationToken = default)
        {
            마지막하위원장연결요청 = request;
            return Task.FromResult(하위원장연결응답);
        }

        public Task<주문원장통합공개Dto?> 하위원장분리Async(
            string orderLedgerId,
            string childLedgerId,
            long? expectedRevision = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(하위원장분리응답);

        public Task<주문원장서명상태공개Dto?> 주문원장서명상태조회Async(
            string orderLedgerId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(서명상태응답);

        public Task<주문원장서명상태공개Dto?> 주문원장서명준비Async(
            string orderLedgerId,
            주문원장서명준비ClientRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(서명준비응답);

        public Task<주문원장서명상태공개Dto?> 주문원장서명등록Async(
            string orderLedgerId,
            주문원장서명등록ClientRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(서명등록응답);

        public Task<IReadOnlyList<공동구매커머스이행계획공개Dto>> 공동구매별커머스이행조회Async(
            string groupPurchaseId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(공동구매커머스응답);

        public Task<IReadOnlyList<공동구매커머스이행계획공개Dto>> 문서번호로커머스이행조회Async(
            string documentManagementNumber,
            CancellationToken cancellationToken = default)
            => Task.FromResult(문서커머스응답);
    }

    private sealed class Fake공동구매가격의사결정Service : I공동구매가격의사결정Service
    {
        public 공동구매가격의사결정결과 응답 { get; set; } = new();
        public 공동구매가격의사결정요청? 마지막요청 { get; private set; }

        public Task<공동구매가격의사결정결과> 조회Async(
            공동구매가격의사결정요청 request,
            CancellationToken cancellationToken = default)
        {
            마지막요청 = request;
            return Task.FromResult(응답);
        }
    }
}
