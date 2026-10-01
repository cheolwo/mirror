using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Contracts.Common.Community;
using Hongdal.Controllers.Common;
using Hongdal.Extensions;
using Hongdal.Services.AgriculturalFisheries.ImportReadiness;
using Hongdal.Services.Community;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Hongdal.Tests.Services.Community;

public sealed class CommunityPostOpportunityServiceTests
{
    [Fact]
    public void 육류와_국경간거래_신호가_함께있을때만_제안한다()
    {
        var analyzer = new CommunityPostOpportunityAnalyzer();

        var suggested = analyzer.Analyze(
            "미국산 돼지고기 수입을 알아봅니다",
            "해외 작업장과 검역 서류를 어디서 확인하면 좋을까요?");
        var ordinary = analyzer.Analyze(
            "오늘 아파트 장터가 열립니다",
            "이웃들과 반찬을 나눕니다.");

        Assert.True(suggested.SuggestMeatImportReadiness);
        Assert.Contains("돼지고기", suggested.MatchedSignals);
        Assert.Contains("수입", suggested.MatchedSignals);
        Assert.False(ordinary.SuggestMeatImportReadiness);
    }

    [Fact]
    public async Task 같은_커뮤니티기능을_유지하고_표시문구만_사용자언어로_바꾼다()
    {
        var store = new InMemoryPostStore(CreateImportPost());
        var service = CreateService(store);

        var korean = await service.GetAsync(71, "ko-KR");
        var english = await service.GetAsync(71, "en-US");

        var koItem = Assert.Single(korean!.Items);
        var enItem = Assert.Single(english!.Items);
        Assert.Equal(koItem.Code, enItem.Code);
        Assert.Equal(koItem.LedgerTemplateKey, enItem.LedgerTemplateKey);
        Assert.Equal(koItem.StartEndpoint, enItem.StartEndpoint);
        Assert.NotEqual(koItem.Title, enItem.Title);
        Assert.Equal(CommunityExperienceScopeCodes.SharedCommunity, english.ExperiencePolicy.ExperienceScopeCode);
        Assert.True(english.ExperiencePolicy.UsesSameCommunityApp);
        Assert.True(english.ExperiencePolicy.DisplayLanguageAffectsContentOnly);
        Assert.False(english.ExperiencePolicy.OperatingProfileAffectsAvailability);
        Assert.False(english.ExperiencePolicy.InfersLanguageFromCountryOrRole);
        Assert.False(enItem.AutoStartsWorkflow);
        Assert.True(enItem.RequiresExplicitConsent);
        Assert.True(enItem.InformationOnly);
        Assert.False(enItem.IsBrokerageEnabled);
    }

    [Fact]
    public async Task 제안조회만으로는_원장을_만들거나_게시글을_바꾸지않는다()
    {
        var store = new InMemoryPostStore(CreateImportPost());
        var ledgerStore = new InMemoryLedgerStore();
        var service = CreateService(store, ledgerStore);

        var result = await service.GetAsync(71, "en");

        Assert.Single(result!.Items);
        Assert.Null(store.Current.LinkedLedgerId);
        Assert.Equal(0, ledgerStore.Count);
    }

    [Fact]
    public async Task 평범한_게시글도_가벼운_참여진입을_제공한다()
    {
        var store = new InMemoryPostStore(CreateOrdinaryPost());
        var service = CreateService(store);

        var result = await service.GetAsync(72, "ko-KR");

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(CommunityPostParticipationStateCodes.Available, result.Participation.StateCode);
        Assert.True(result.Participation.CanStart);
        Assert.False(result.Participation.CanJoin);
        Assert.False(result.Participation.AutoStartsWorkflow);
        Assert.True(result.Participation.NonBinding);
        Assert.True(result.Participation.RequiresExplicitPromotionToPlanning);
        Assert.Equal(CommunityPostParticipationRoleCodes.All.Count, result.Participation.RoleOptions.Count);
    }

    [Fact]
    public async Task 신고게시글은_거래참여와_가원장흐름에서_분리한다()
    {
        var source = CreateOrdinaryPost() with { IsReportBoardPost = true };
        var store = new InMemoryPostStore(source);
        var service = CreateService(store, voteService: new InMemoryCommunityVoteService());

        var result = await service.GetAsync(source.PostId, "ko-KR");

        Assert.NotNull(result);
        Assert.Equal(CommunityPostParticipationStateCodes.Closed, result.Participation.StateCode);
        Assert.False(result.Participation.CanStart);
        Assert.Empty(result.Participation.RoleOptions);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartParticipationAsync(
            source.PostId,
            CreateParticipationStartRequest(),
            "reader-1",
            "읽던 사람"));
    }

    [Fact]
    public async Task 독자가_명시적으로_시작하면_비구속적_역할관심투표만_만든다()
    {
        var store = new InMemoryPostStore(CreateOrdinaryPost());
        var ledgerStore = new InMemoryLedgerStore();
        var voteService = new InMemoryCommunityVoteService();
        var service = CreateService(store, ledgerStore, voteService);
        var request = CreateParticipationStartRequest();

        var started = await service.StartParticipationAsync(
            72,
            request,
            "reader-1",
            "읽던 사람");
        var retried = await service.StartParticipationAsync(
            72,
            request,
            "reader-2",
            "다른 독자");
        var votes = await voteService.ListBySourcePostAsync(72, CancellationToken.None);

        Assert.False(started.ReusedExistingInterestVote);
        Assert.True(retried.ReusedExistingInterestVote);
        Assert.Equal(started.InterestVote.Id, retried.InterestVote.Id);
        Assert.Equal(CommunityVoteKindCodes.CollectiveActionInterest, started.InterestVote.VoteKind);
        Assert.Equal(72, started.InterestVote.SourcePostId);
        Assert.Null(started.InterestVote.CommunityLedgerId);
        Assert.True(started.InterestVote.AllowMultipleSelection);
        Assert.False(started.InterestVote.ResolutionDocumentEnabled);
        Assert.False(started.InterestVote.SignatureRequired);
        Assert.Equal(CommunityPostParticipationStateCodes.Gathering, started.Participation.StateCode);
        Assert.All(started.Participation.RoleOptions, option => Assert.False(string.IsNullOrWhiteSpace(option.OptionId)));
        Assert.Single(votes.Items);
        Assert.Null(store.Current.LinkedLedgerId);
        Assert.Equal(0, ledgerStore.Count);
    }

    [Fact]
    public async Task 참여관심모집은_두가지_명시적확인이_필요하다()
    {
        var store = new InMemoryPostStore(CreateOrdinaryPost());
        var voteService = new InMemoryCommunityVoteService();
        var service = CreateService(store, voteService: voteService);
        var request = CreateParticipationStartRequest();
        request.ConfirmNonBindingParticipation = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartParticipationAsync(
            72,
            request,
            "reader-1",
            "읽던 사람"));

        var votes = await voteService.ListBySourcePostAsync(72, CancellationToken.None);
        Assert.Empty(votes.Items);
    }

    [Fact]
    public async Task 한사람이_여러_가능역할을_표시해도_참여자수는_한명으로_집계한다()
    {
        var store = new InMemoryPostStore(CreateOrdinaryPost());
        var voteService = new InMemoryCommunityVoteService();
        var service = CreateService(store, voteService: voteService);
        var started = await service.StartParticipationAsync(
            72,
            CreateParticipationStartRequest(),
            "reader-1",
            "읽던 사람");
        var selected = started.InterestVote.Options
            .Where(option => option.ProductKey is "community-role:Carrier" or "community-role:FollowOnly")
            .Select(option => option.OptionId)
            .ToArray();

        await voteService.CastVoteAsync(
            started.InterestVote.Id,
            new CommunityVoteCastRequest
            {
                VoterKey = "reader-1",
                VoterDisplayName = "읽던 사람",
                OptionIds = selected
            },
            CancellationToken.None);
        var result = await service.GetAsync(72, "ko-KR");

        Assert.Equal(2, selected.Length);
        Assert.Equal(1, result!.Participation.ParticipantCount);
        Assert.Equal(1, result.Participation.RoleOptions.Single(x => x.RoleCode == CommunityPostParticipationRoleCodes.Carrier).InterestCount);
        Assert.Equal(1, result.Participation.RoleOptions.Single(x => x.RoleCode == CommunityPostParticipationRoleCodes.FollowOnly).InterestCount);
        Assert.Null(store.Current.LinkedLedgerId);
    }

    [Fact]
    public async Task 두사람이_모이기전에는_가원장을_만들수없다()
    {
        var store = new InMemoryPostStore(CreateOrdinaryPost());
        var ledgerStore = new InMemoryLedgerStore();
        var voteService = new InMemoryCommunityVoteService();
        var service = CreateService(store, ledgerStore, voteService);
        var started = await service.StartParticipationAsync(
            72,
            CreateParticipationStartRequest(),
            "reader-1",
            "읽던 사람");
        var buyerOption = started.InterestVote.Options.Single(option =>
            option.ProductKey == "community-role:Buyer");
        await voteService.CastVoteAsync(
            started.InterestVote.Id,
            new CommunityVoteCastRequest
            {
                AuthenticatedUserId = "reader-1",
                VoterKey = "session-1",
                VoterDisplayName = "읽던 사람",
                OptionIds = [buyerOption.OptionId]
            },
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PromoteParticipationAsync(
            72,
            CreatePromotionRequest(started.InterestVote.Id),
            "author-2",
            "작성자"));

        Assert.Contains("2명 이상", exception.Message, StringComparison.Ordinal);
        Assert.Null(store.Current.LinkedLedgerId);
        Assert.Equal(0, ledgerStore.Count);
    }

    [Fact]
    public async Task 작성자가_두사람의_관심을_비구속적_가원장으로_승격하고_재시도해도_한번만저장한다()
    {
        var store = new InMemoryPostStore(CreateOrdinaryPost());
        var ledgerStore = new InMemoryLedgerStore();
        var voteService = new InMemoryCommunityVoteService();
        var service = CreateService(store, ledgerStore, voteService);
        var started = await service.StartParticipationAsync(
            72,
            CreateParticipationStartRequest(),
            "reader-1",
            "읽던 사람");
        var buyerOption = started.InterestVote.Options.Single(option =>
            option.ProductKey == "community-role:Buyer");
        var warehouseOption = started.InterestVote.Options.Single(option =>
            option.ProductKey == "community-role:WarehouseOperator");
        await voteService.CastVoteAsync(
            started.InterestVote.Id,
            new CommunityVoteCastRequest
            {
                AuthenticatedUserId = "reader-1",
                VoterKey = "session-1",
                VoterDisplayName = "구매 관심자",
                OptionIds = [buyerOption.OptionId]
            },
            CancellationToken.None);
        await voteService.CastVoteAsync(
            started.InterestVote.Id,
            new CommunityVoteCastRequest
            {
                AuthenticatedUserId = "warehouse-1",
                VoterKey = "session-2",
                VoterDisplayName = "창고 담당자",
                OptionIds = [warehouseOption.OptionId]
            },
            CancellationToken.None);

        var request = CreatePromotionRequest(started.InterestVote.Id);
        var promoted = await service.PromoteParticipationAsync(72, request, "author-2", "작성자");
        var retried = await service.PromoteParticipationAsync(72, request, "author-2", "작성자");
        var ledger = ledgerStore.Get(promoted.ProvisionalLedger.LedgerId);
        var vote = await voteService.GetAsync(started.InterestVote.Id, CancellationToken.None);

        Assert.False(promoted.ReusedExistingProvisionalLedger);
        Assert.True(retried.ReusedExistingProvisionalLedger);
        Assert.Equal(1, ledgerStore.Count);
        Assert.NotNull(ledger);
        Assert.Equal(1, ledger!.Revision);
        Assert.Equal(커뮤니티원장상태.초안, ledger.상태);
        Assert.Equal(CommunityLedgerTemplateKeys.GroupPurchase, ledger.원장템플릿Key);
        Assert.Equal(
            CommunityPostProvisionalLedgerPolicy.LedgerMaturityCode,
            ledger.확장속성[CommunityPostProvisionalLedgerPolicy.LedgerMaturityAttributeKey]);
        Assert.Equal("NonBinding", ledger.확장속성[CommunityPostProvisionalLedgerPolicy.BindingEffectAttributeKey]);
        Assert.Contains(ledger.참여자목록, participant => participant.UserId == "reader-1");
        Assert.Contains(ledger.참여자목록, participant => participant.UserId == "warehouse-1");
        Assert.Equal(CommunityVoteStatusCodes.Closed, vote!.Status);
        Assert.Equal(ledger.원장Id, vote.CommunityLedgerId);
        Assert.Equal(CommunityPostParticipationStateCodes.ProvisionalLedgerCreated, promoted.Participation.StateCode);
        Assert.False(promoted.Participation.CanJoin);
        Assert.Equal(CommunityTradeDirectionCodes.Import, promoted.TradeDirectionCode);
        Assert.Equal("US", promoted.OriginCountryCode);
        Assert.Equal("KR", promoted.DestinationCountryCode);
        Assert.Equal([CommunityTransportModeCodes.Ocean], promoted.TransportModeCodes);
        Assert.True(promoted.Participation.PartyFormation.IsAvailable);
        Assert.True(promoted.Participation.PartyFormation.NonBinding);
        Assert.True(promoted.Participation.PartyFormation.PlatformDoesNotAssignWork);
        Assert.True(promoted.Participation.PartyFormation.PlatformDoesNotCreateContracts);
        Assert.Contains(promoted.Participation.PartyFormation.RoleSlots, slot =>
            slot.RoleCode == CommunityPostPartyRoleCodes.Importer && slot.IsRequired);
        Assert.Contains(promoted.Participation.PartyFormation.RoleSlots, slot =>
            slot.RoleCode == CommunityPostPartyRoleCodes.Exporter && slot.IsRequired);
        Assert.Contains(promoted.Participation.PartyFormation.RoleSlots, slot =>
            slot.RoleCode == CommunityPostPartyRoleCodes.ImportCustomsBroker
            && slot.IsRecommended
            && slot.VerificationRequirementCode == CommunityPartyRoleVerificationRequirementCodes.JurisdictionLicenseOrRegistration);
        Assert.Contains(promoted.Participation.PartyFormation.RoleSlots, slot =>
            slot.RoleCode == CommunityPostPartyRoleCodes.OceanCarrier
            && slot.IsRequired
            && slot.VerificationRequirementCode == CommunityPartyRoleVerificationRequirementCodes.CarrierOperatingAuthority);
        Assert.Contains(promoted.Participation.PartyFormation.RoleSlots, slot =>
            slot.RoleCode == CommunityPostPartyRoleCodes.OceanFreightForwarder && slot.IsRecommended);
        Assert.Equal(0, promoted.Participation.PartyFormation.RepresentedRequiredRoleSlotCount);
        Assert.False(promoted.Participation.PartyFormation.IsReadyForRealLedgerReview);
    }

    [Fact]
    public async Task 관심표시와_역할수락을_분리하고_필수당사자와_운송사가_수락해야_실원장검토가가능하다()
    {
        var store = new InMemoryPostStore(CreateOrdinaryPost());
        var ledgerStore = new InMemoryLedgerStore();
        var voteService = new InMemoryCommunityVoteService();
        var service = CreateService(store, ledgerStore, voteService);
        var started = await service.StartParticipationAsync(
            72,
            CreateParticipationStartRequest(),
            "reader-1",
            "읽던 사람");
        var buyerOption = started.InterestVote.Options.Single(option =>
            option.ProductKey == "community-role:Buyer");
        var warehouseOption = started.InterestVote.Options.Single(option =>
            option.ProductKey == "community-role:WarehouseOperator");
        await voteService.CastVoteAsync(
            started.InterestVote.Id,
            new CommunityVoteCastRequest
            {
                AuthenticatedUserId = "buyer-interest-1",
                VoterKey = "session-buyer",
                VoterDisplayName = "구매 관심자",
                OptionIds = [buyerOption.OptionId]
            },
            CancellationToken.None);
        await voteService.CastVoteAsync(
            started.InterestVote.Id,
            new CommunityVoteCastRequest
            {
                AuthenticatedUserId = "warehouse-interest-1",
                VoterKey = "session-warehouse",
                VoterDisplayName = "창고 관심자",
                OptionIds = [warehouseOption.OptionId]
            },
            CancellationToken.None);
        var promoted = await service.PromoteParticipationAsync(
            72,
            CreatePromotionRequest(started.InterestVote.Id),
            "author-2",
            "작성자");
        var participationService = new CommunityPostProfessionalParticipationService(
            store,
            ledgerStore,
            new InMemoryProfessionalEligibilityService(CommunityPostPartyRoleCodes.OceanCarrier));

        JoinCommunityPostPartyRoleResponse? partyResult = null;
        foreach (var role in new[]
                 {
                     CommunityPostPartyRoleCodes.Buyer,
                     CommunityPostPartyRoleCodes.Seller,
                     CommunityPostPartyRoleCodes.Importer,
                     CommunityPostPartyRoleCodes.Exporter
                 })
        {
            partyResult = await participationService.JoinPartyRoleAsync(
                72,
                new JoinCommunityPostPartyRoleRequest
                {
                    ProvisionalLedgerId = promoted.ProvisionalLedger.LedgerId,
                    PartyRoleCode = role,
                    ConfirmRoleCapacity = true,
                    ConfirmVoluntaryNonBindingParticipation = true,
                    ConfirmParticipantNotification = true
                },
                $"party-{role}",
                $"{role} 참여자");
        }

        Assert.NotNull(partyResult);
        Assert.Equal(4, partyResult.Participation.PartyFormation.RepresentedRequiredRoleSlotCount);
        Assert.False(partyResult.Participation.PartyFormation.IsReadyForRealLedgerReview);

        var carrierResult = await participationService.JoinAsync(
            72,
            new JoinCommunityPostProfessionalRequest
            {
                ProvisionalLedgerId = promoted.ProvisionalLedger.LedgerId,
                ProfessionalRoleCode = CommunityPostPartyRoleCodes.OceanCarrier,
                ConfirmProfessionalCapacity = true,
                ConfirmVoluntaryNonBindingParticipation = true,
                ConfirmParticipantNotification = true
            },
            "ocean-carrier-1",
            "해상 운송사");
        var retriedBuyer = await participationService.JoinPartyRoleAsync(
            72,
            new JoinCommunityPostPartyRoleRequest
            {
                ProvisionalLedgerId = promoted.ProvisionalLedger.LedgerId,
                PartyRoleCode = CommunityPostPartyRoleCodes.Buyer,
                ConfirmRoleCapacity = true,
                ConfirmVoluntaryNonBindingParticipation = true,
                ConfirmParticipantNotification = true
            },
            $"party-{CommunityPostPartyRoleCodes.Buyer}",
            "Buyer 참여자");

        Assert.True(carrierResult.Participation.PartyFormation.IsReadyForRealLedgerReview);
        Assert.Equal(5, carrierResult.Participation.PartyFormation.RepresentedRequiredRoleSlotCount);
        Assert.Contains("명시적으로 역할을 수락", carrierResult.Participation.PartyFormation.ReadinessMessage, StringComparison.Ordinal);
        Assert.True(retriedBuyer.ReusedExistingParticipation);
        Assert.True(retriedBuyer.Participation.PartyFormation.IsReadyForRealLedgerReview);
        Assert.Contains(carrierResult.Participation.PartyFormation.RoleSlots, slot =>
            slot.RoleCode == CommunityPostPartyRoleCodes.OceanCarrier
            && slot.ConfirmedParticipantCount == 1
            && slot.ExternalCredentialVerificationRequired
            && !slot.ExternalCredentialVerified);
    }

    [Fact]
    public async Task 미국공동수입은_보세시설부터참여자주소배송까지_역할슬롯과원장참여를연결한다()
    {
        var store = new InMemoryPostStore(CreateOrdinaryPost());
        var ledgerStore = new InMemoryLedgerStore();
        var voteService = new InMemoryCommunityVoteService();
        var service = CreateService(
            store,
            ledgerStore,
            voteService,
            new InMemoryProfessionalEligibilityService(
                CommunityPostPartyRoleCodes.CustomsControlledFacilityOperator));
        var started = await service.StartParticipationAsync(
            72,
            CreateParticipationStartRequest(),
            "buyer-interest",
            "구매 관심자");
        var buyerOption = started.InterestVote.Options.Single(option =>
            option.ProductKey == "community-role:Buyer");
        var warehouseOption = started.InterestVote.Options.Single(option =>
            option.ProductKey == "community-role:WarehouseOperator");
        await voteService.CastVoteAsync(
            started.InterestVote.Id,
            new CommunityVoteCastRequest
            {
                AuthenticatedUserId = "buyer-interest",
                VoterKey = "session-us-buyer",
                VoterDisplayName = "미국 공동구매 관심자",
                OptionIds = [buyerOption.OptionId]
            },
            CancellationToken.None);
        await voteService.CastVoteAsync(
            started.InterestVote.Id,
            new CommunityVoteCastRequest
            {
                AuthenticatedUserId = "warehouse-interest",
                VoterKey = "session-us-warehouse",
                VoterDisplayName = "물류 관심자",
                OptionIds = [warehouseOption.OptionId]
            },
            CancellationToken.None);

        var promotionRequest = CreatePromotionRequest(started.InterestVote.Id);
        promotionRequest.OriginCountryCode = "KR";
        promotionRequest.DestinationCountryCode = "US";
        var promoted = await service.PromoteParticipationAsync(
            72,
            promotionRequest,
            "author-2",
            "작성자");

        var expectedRoleDirectories = new Dictionary<string, string>
        {
            [CommunityPostPartyRoleCodes.CustomsControlledFacilityOperator] =
                "stageCode=CustomsControlledStorage",
            [CommunityPostPartyRoleCodes.InBondCarrier] =
                "stageCode=InBondTransportation",
            [CommunityPostPartyRoleCodes.DomesticFulfillmentOperator] =
                "stageCode=FulfillmentWarehouseInbound",
            [CommunityPostPartyRoleCodes.ParticipantAddressDeliveryProvider] =
                "stageCode=ParticipantAddressFinalMileDelivery"
        };
        foreach (var expected in expectedRoleDirectories)
        {
            var slot = promoted.Participation.PartyFormation.RoleSlots.Single(item =>
                item.RoleCode == expected.Key);
            Assert.True(slot.IsRecommended);
            Assert.False(slot.IsRequired);
            Assert.True(slot.ExternalCredentialVerificationRequired);
            Assert.False(slot.ExternalCredentialVerified);
            Assert.True(slot.CandidateDirectoryIsResearchOnly);
            Assert.Contains(expected.Value, slot.CandidateDirectoryEndpoint, StringComparison.Ordinal);

            var opening = promoted.Participation.ProfessionalParticipation.RoleOpenings.Single(
                item => item.RoleCode == expected.Key);
            Assert.Equal(slot.CandidateDirectoryEndpoint, opening.CandidateDirectoryEndpoint);
            Assert.True(opening.RequiresSeparateAuthorityAndContractVerification);
        }

        Assert.DoesNotContain(
            promoted.Participation.PartyFormation.RoleSlots,
            slot => slot.RoleCode == CommunityPostPartyRoleCodes.WarehouseOperator);
        Assert.Contains(
            promoted.Participation.PartyFormation.RoleSlots,
            slot => slot.RoleCode == CommunityPostPartyRoleCodes.CustomsControlledFacilityOperator
                    && slot.ConfirmedParticipantCount == 1
                    && slot.StateCode == CommunityPartyRoleSlotStateCodes.RoleAccepted);

        var participationService = new CommunityPostProfessionalParticipationService(
            store,
            ledgerStore,
            new InMemoryProfessionalEligibilityService(
                expectedRoleDirectories.Keys.ToArray()));
        JoinCommunityPostProfessionalResponse? joined = null;
        var participantIndex = 0;
        foreach (var roleCode in expectedRoleDirectories.Keys.Where(roleCode => !string.Equals(
                     roleCode,
                     CommunityPostPartyRoleCodes.CustomsControlledFacilityOperator,
                     StringComparison.OrdinalIgnoreCase)))
        {
            participantIndex++;
            joined = await participationService.JoinAsync(
                72,
                new JoinCommunityPostProfessionalRequest
                {
                    ProvisionalLedgerId = promoted.ProvisionalLedger.LedgerId,
                    ProfessionalRoleCode = roleCode,
                    ConfirmProfessionalCapacity = true,
                    ConfirmVoluntaryNonBindingParticipation = true,
                    ConfirmParticipantNotification = true
                },
                $"us-logistics-{participantIndex}",
                $"미국 물류 참여자 {participantIndex}");
        }

        Assert.NotNull(joined);
        var ledger = ledgerStore.Get(promoted.ProvisionalLedger.LedgerId);
        Assert.NotNull(ledger);
        Assert.All(
            expectedRoleDirectories.Keys,
            roleCode => Assert.Contains(
                joined!.Participation.PartyFormation.RoleSlots,
                slot => slot.RoleCode == roleCode
                        && slot.ConfirmedParticipantCount == 1
                        && slot.StateCode == CommunityPartyRoleSlotStateCodes.RoleAccepted));
        Assert.Equal(
            3,
            ledger!.참여자목록.Count(participant =>
                participant.ParticipationState == "가원장 역할 참여"));
        Assert.Contains(
            ledger.참여자목록,
            participant => participant.UserId == "author-2"
                           && participant.ParticipationState == "가원장 발의·역할 참여"
                           && participant.RoleLabel.Contains(
                               "보세창고·FTZ 운영자",
                               StringComparison.Ordinal));
        Assert.All(
            ledger.참여자목록.Where(participant =>
                participant.ParticipationState == "가원장 역할 참여"),
            participant => Assert.Contains(
                "플랫폼 역할 확인",
                participant.RoleLabel,
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task 게시글작성자가아니면_모인관심을_가원장으로_승격할수없다()
    {
        var store = new InMemoryPostStore(CreateOrdinaryPost());
        var service = CreateService(store, voteService: new InMemoryCommunityVoteService());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.PromoteParticipationAsync(
            72,
            CreatePromotionRequest(Guid.NewGuid()),
            "reader-1",
            "읽던 사람"));
    }

    [Fact]
    public async Task 해외작성자도_명시적으로_확인하면_같은게시글과_원장을_시작한다()
    {
        var store = new InMemoryPostStore(CreateImportPost(authorUserId: "exporter-1"));
        var service = CreateService(store);
        var request = CreateStartRequest(MeatImportReadinessPartySideCodes.Overseas);

        var started = await service.StartMeatImportReadinessAsync(
            71,
            request,
            "exporter-1",
            "Overseas operator");
        var retried = await service.StartMeatImportReadinessAsync(
            71,
            request,
            "exporter-1",
            "Overseas operator");

        Assert.True(started.LinkedToCommunityPost);
        Assert.Equal(MeatImportReadinessCaseIds.FromCommunityPost(71), started.Case.CaseId);
        Assert.Equal(started.Case.CaseId, store.Current.LinkedLedgerId);
        Assert.Equal(MeatImportReadinessPartySideCodes.Overseas, started.Case.InitiatorSideCode);
        Assert.Equal(71, started.Case.SourceCommunityPostId);
        Assert.Contains(started.Case.Participants, participant =>
            participant.UserId == "exporter-1"
            && participant.SideCode == MeatImportReadinessPartySideCodes.Overseas);
        Assert.Contains(started.Case.Participants, participant =>
            participant.UserId == "importer-1"
            && participant.SideCode == MeatImportReadinessPartySideCodes.Korean);
        Assert.Equal(started.Case.CaseId, retried.Case.CaseId);
        Assert.Equal(1, retried.Case.Revision);
    }

    [Fact]
    public async Task 작성자의_두가지_명시적확인없이는_시작하지않는다()
    {
        var store = new InMemoryPostStore(CreateImportPost());
        var ledgerStore = new InMemoryLedgerStore();
        var service = CreateService(store, ledgerStore);
        var request = CreateStartRequest(MeatImportReadinessPartySideCodes.Korean);
        request.ConfirmInformationOnly = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartMeatImportReadinessAsync(
            71,
            request,
            "author-1",
            "작성자"));

        Assert.Null(store.Current.LinkedLedgerId);
        Assert.Equal(0, ledgerStore.Count);
    }

    [Fact]
    public void 조회는_공개이고_시작은_로그인이_필요하다()
    {
        var controller = typeof(CommunityPostOpportunitiesController);
        Assert.NotNull(controller.GetMethod(nameof(CommunityPostOpportunitiesController.Get))!
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(controller.GetMethod(nameof(CommunityPostOpportunitiesController.StartMeatImportReadiness))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(controller.GetMethod(nameof(CommunityPostOpportunitiesController.StartParticipation))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(controller.GetMethod(nameof(CommunityPostOpportunitiesController.PromoteParticipation))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(controller.GetMethod(nameof(CommunityPostOpportunitiesController.JoinProfessional))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(controller.GetMethod(nameof(CommunityPostOpportunitiesController.JoinPartyRole))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).SingleOrDefault());
    }

    [Fact]
    public void 컨트롤러는_기능별_좁은_UseCase에_의존한다()
    {
        var parameterTypes = typeof(CommunityPostOpportunitiesController)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.DoesNotContain(typeof(ICommunityPostOpportunityService), parameterTypes);
        Assert.Contains(typeof(ICommunityPostOpportunityQueryUseCase), parameterTypes);
        Assert.Contains(typeof(ICommunityPostParticipationUseCase), parameterTypes);
        Assert.Contains(typeof(ICommunityPostProfessionalParticipationService), parameterTypes);
        Assert.Contains(typeof(ICommunityPostMeatImportReadinessUseCase), parameterTypes);
    }

    [Fact]
    public void 기능별_UseCase가_DI에_등록된다()
    {
        var services = new ServiceCollection();

        services.AddHongdalDomainServices();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ICommunityPostOpportunityQueryUseCase)
            && descriptor.ImplementationType == typeof(CommunityPostOpportunityQueryUseCase));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ICommunityPostParticipationUseCase)
            && descriptor.ImplementationType == typeof(CommunityPostParticipationUseCase));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ICommunityPostMeatImportReadinessUseCase)
            && descriptor.ImplementationType == typeof(CommunityPostMeatImportReadinessUseCase));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ICommunityPostOpportunityService)
            && descriptor.ImplementationType == typeof(CommunityPostOpportunityService));
    }

    private static CommunityPostOpportunityService CreateService(
        InMemoryPostStore postStore,
        InMemoryLedgerStore? ledgerStore = null,
        ICommunityVoteService? voteService = null,
        ICommunityProfessionalEligibilityService? professionalEligibilityService = null)
    {
        var effectiveLedgerStore = ledgerStore ?? new InMemoryLedgerStore();
        var effectiveVoteService = voteService ?? new InMemoryCommunityVoteService();
        var effectiveEligibilityService = professionalEligibilityService
                                          ?? new InMemoryProfessionalEligibilityService();
        var analyzer = new CommunityPostOpportunityAnalyzer();
        var queryUseCase = new CommunityPostOpportunityQueryUseCase(
            postStore,
            analyzer,
            effectiveVoteService,
            effectiveLedgerStore,
            new ProjectionOnlyCommunityActionJourneyService(),
            new EmptyCommunityDynamicDiscoveryService());
        var readinessUseCase = new CommunityPostMeatImportReadinessUseCase(
            postStore,
            analyzer,
            new MeatImportReadinessService(effectiveLedgerStore));
        var professionalParticipationService = new CommunityPostProfessionalParticipationService(
            postStore,
            effectiveLedgerStore,
            effectiveEligibilityService);
        var participationUseCase = new CommunityPostParticipationUseCase(
            postStore,
            effectiveVoteService,
            effectiveLedgerStore,
            effectiveEligibilityService);
        return new CommunityPostOpportunityService(
            queryUseCase,
            participationUseCase,
            professionalParticipationService,
            readinessUseCase);
    }

    private static CommunityPostOpportunitySource CreateImportPost(string authorUserId = "author-1")
        => new(
            71,
            "platform",
            "미국산 돼지고기 수입을 함께 검토합니다",
            "해외 작업장과 한국 수입업자가 검역·통관 준비 정보를 함께 확인하고 싶습니다.",
            authorUserId,
            null);

    private static CommunityPostOpportunitySource CreateOrdinaryPost()
        => new(
            72,
            "platform",
            "아파트 장터에서 같이 나눌 사람 있나요",
            "일단 편하게 의견부터 나눠봅니다.",
            "author-2",
            null);

    private static StartCommunityPostParticipationRequest CreateParticipationStartRequest()
        => new()
        {
            DisplayLanguageCode = CommunityDisplayLanguageCodes.Korean,
            ConfirmExplicitStart = true,
            ConfirmNonBindingParticipation = true
        };

    private static PromoteCommunityPostParticipationRequest CreatePromotionRequest(Guid voteId)
        => new()
        {
            InterestVoteId = voteId,
            CollectiveIntentTypeCode = CommunityCollectiveIntentTypeCodes.GroupImportCandidate,
            TradeDirectionCode = CommunityTradeDirectionCodes.Import,
            OriginCountryCode = "US",
            DestinationCountryCode = "KR",
            TransportModeCodes = [CommunityTransportModeCodes.Ocean],
            ConfirmProvisionalLedger = true,
            ConfirmNonBindingEvidence = true,
            ConfirmParticipantNotifications = true
        };

    private static StartCommunityMeatImportReadinessRequest CreateStartRequest(string initiatorSideCode)
        => new()
        {
            DisplayLanguageCode = CommunityDisplayLanguageCodes.English,
            ConfirmExplicitStart = true,
            ConfirmInformationOnly = true,
            Case = new CreateMeatImportReadinessCaseRequest
            {
                InitiatorSideCode = initiatorSideCode,
                Title = "US frozen pork readiness",
                ProductTypeCode = MeatImportReadinessProductTypeCodes.Pork,
                ProductName = "Frozen pork",
                HsCode = "0203299000",
                OriginCountryCode = "US",
                OriginCountryName = "United States",
                KoreanImporterUserId = "importer-1",
                KoreanImporterDisplayName = "Korean importer",
                KoreanImporterOrganizationName = "Korean Importer Co.",
                OverseasCounterparty = new CreateMeatImportReadinessCounterpartyRequest
                {
                    UserId = initiatorSideCode == MeatImportReadinessPartySideCodes.Overseas ? "exporter-1" : "exporter-2",
                    DisplayName = "Overseas operator",
                    OrganizationName = "US Exporter",
                    RoleCode = MeatImportReadinessParticipantRoleCodes.OverseasEstablishment,
                    EstablishmentNumber = "EST-1234"
                }
            }
        };

    private sealed class InMemoryPostStore : ICommunityPostOpportunityStore
    {
        public InMemoryPostStore(CommunityPostOpportunitySource source)
        {
            Current = source;
        }

        public CommunityPostOpportunitySource Current { get; private set; }

        public Task<CommunityPostOpportunitySource?> GetAsync(long postId, CancellationToken cancellationToken = default)
            => Task.FromResult<CommunityPostOpportunitySource?>(Current.PostId == postId ? Current : null);

        public Task<CommunityPostLedgerLinkResult> LinkLedgerAsync(
            long postId,
            string actorUserId,
            string ledgerId,
            CancellationToken cancellationToken = default)
        {
            if (postId != Current.PostId)
            {
                return Task.FromResult(CommunityPostLedgerLinkResult.NotFound);
            }

            if (!string.Equals(actorUserId, Current.AuthorUserId, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(CommunityPostLedgerLinkResult.NotOwner);
            }

            if (Current.LinkedLedgerId is not null)
            {
                return Task.FromResult(string.Equals(Current.LinkedLedgerId, ledgerId, StringComparison.OrdinalIgnoreCase)
                    ? CommunityPostLedgerLinkResult.AlreadyLinked
                    : CommunityPostLedgerLinkResult.ConflictingLedger);
            }

            Current = Current with { LinkedLedgerId = ledgerId };
            return Task.FromResult(CommunityPostLedgerLinkResult.Linked);
        }

        public Task<CommunityPostMomentumUpdateResult> SetMomentumPromotionAsync(
            long postId,
            string ledgerId,
            string momentumCode,
            string momentumMessage,
            int roleParticipantCount,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                postId == Current.PostId
                && string.Equals(Current.LinkedLedgerId, ledgerId, StringComparison.OrdinalIgnoreCase)
                    ? CommunityPostMomentumUpdateResult.Updated
                    : CommunityPostMomentumUpdateResult.ConflictingLedger);
    }

    private sealed class InMemoryLedgerStore : I커뮤니티원장저장소
    {
        private readonly Dictionary<string, 커뮤니티원장Dto> _items = new(StringComparer.OrdinalIgnoreCase);

        public int Count => _items.Count;

        public 커뮤니티원장Dto? Get(string ledgerId) => _items.GetValueOrDefault(ledgerId);

        public Task<커뮤니티원장Dto> 원장저장Async(
            커뮤니티원장저장요청 request,
            string updatedBy,
            CancellationToken cancellationToken = default)
        {
            var id = request.원장Id ?? $"ledger-{Guid.NewGuid():N}";
            _items.TryGetValue(id, out var existing);
            if (request.기대Revision.HasValue && request.기대Revision.Value != (existing?.Revision ?? 0))
            {
                throw new InvalidOperationException("원장의 현재 상태가 다른 요청에서 먼저 변경되었습니다.");
            }

            var now = DateTime.UtcNow;
            var saved = new 커뮤니티원장Dto
            {
                원장Id = id,
                Revision = (existing?.Revision ?? 0) + 1,
                커뮤니티Id = request.커뮤니티Id,
                원장템플릿Key = request.원장템플릿Key,
                제목 = request.제목,
                원함 = request.원함,
                상태 = request.상태 ?? 커뮤니티원장상태.초안,
                현재단계Key = request.현재단계Key,
                대상OsCode = request.대상OsCode,
                대상OsName = request.대상OsName,
                생성자UserId = request.생성자UserId,
                생성자표시명 = request.생성자표시명 ?? "참여자",
                블록목록 = request.블록목록,
                참여자목록 = request.참여자목록,
                포함원장목록 = request.포함원장목록 ?? [],
                다이어그램스냅샷 = request.다이어그램스냅샷,
                외부참조 = request.외부참조,
                확장속성 = request.확장속성,
                생성시각Utc = existing?.생성시각Utc ?? now,
                수정시각Utc = now
            };
            _items[id] = saved;
            return Task.FromResult(saved);
        }

        public Task<커뮤니티원장Dto?> 원장조회Async(string 원장Id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.GetValueOrDefault(원장Id));

        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(
            커뮤니티원장조회조건 query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<커뮤니티원장Dto>>(_items.Values.ToArray());

        public Task<커뮤니티원장Dto?> 원장상태변경Async(
            커뮤니티원장상태변경요청 request,
            string updatedBy,
            CancellationToken cancellationToken = default)
            => Task.FromResult<커뮤니티원장Dto?>(null);
    }

    private sealed class InMemoryProfessionalEligibilityService(params string[] roleCodes)
        : ICommunityProfessionalEligibilityService
    {
        public Task<IReadOnlyList<string>> GetVerifiedRoleCodesAsync(
            string userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(roleCodes);
    }

    private sealed class ProjectionOnlyCommunityActionJourneyService : ICommunityActionJourneyService
    {
        public Task<CommunityActionJourneyResponse> BuildAsync(
            CommunityPostOpportunitySource source,
            CommunityPostParticipationEntryResponse participation,
            CommunityVoteResponse? interestVote,
            커뮤니티원장Dto? rootLedger,
            string displayLanguageCode,
            CancellationToken cancellationToken = default)
            => Task.FromResult(CommunityActionJourneyProjection.Build(
                source,
                participation,
                interestVote,
                rootLedger,
                [],
                null,
                displayLanguageCode));
    }

    private sealed class EmptyCommunityDynamicDiscoveryService : ICommunityDynamicDiscoveryService
    {
        public CommunityDynamicTopicCatalogResponse GetCatalog() => new();

        public Task<CommunityPostContextDiscoveryResponse> DiscoverAsync(
            CommunityPostOpportunitySource source,
            CommunityPostContextDiscoveryRequest? request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CommunityPostContextDiscoveryResponse { PostId = source.PostId });

        public Task<CommunityDynamicTopicFeedResponse?> GetFeedAsync(
            string topicKey,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
            => Task.FromResult<CommunityDynamicTopicFeedResponse?>(null);
    }
}
