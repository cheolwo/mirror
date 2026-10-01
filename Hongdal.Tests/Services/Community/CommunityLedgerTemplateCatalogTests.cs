using Hongdal.Contracts.Common.Community;

namespace Hongdal.Tests.Services.Community;

public sealed class CommunityLedgerTemplateCatalogTests
{
    [Fact]
    public void All_IncludesCommunityLedgerWorkTypes()
    {
        var keys = CommunityLedgerTemplateCatalog.All.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains(CommunityLedgerTemplateKeys.CargoTransport, keys);
        Assert.Contains(CommunityLedgerTemplateKeys.Order, keys);
        Assert.Contains(CommunityLedgerTemplateKeys.FoodOrder, keys);
        Assert.Contains(CommunityLedgerTemplateKeys.FoodDelivery, keys);
        Assert.Contains(CommunityLedgerTemplateKeys.HongdalMart, keys);
        Assert.Contains(CommunityLedgerTemplateKeys.WarehouseOutbound, keys);
        Assert.Contains(CommunityLedgerTemplateKeys.WarehouseInbound, keys);
        Assert.Contains(CommunityLedgerTemplateKeys.LocalSale, keys);
        Assert.Contains(CommunityLedgerTemplateKeys.GroupPurchase, keys);
        Assert.Contains(CommunityLedgerTemplateKeys.GroupOrder, keys);
        Assert.Contains(CommunityLedgerTemplateKeys.GroupImport, keys);
        Assert.Contains(CommunityLedgerTemplateKeys.MeatImportReadiness, keys);
        Assert.Contains(CommunityLedgerTemplateKeys.Errand, keys);
    }

    [Fact]
    public void Templates_TreatRoleNamesAsLabelsAndPermissionsAsActionHints()
    {
        var template = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.GroupPurchase);
        var settlementRole = template.Roles.Single(x => x.RoleName == "정산 확인자");

        Assert.Equal(CommunityLedgerParticipationModes.OpenRoleParticipation, template.ParticipationPolicy.DefaultParticipationMode);
        Assert.Contains("라벨", template.ParticipationPolicy.RoleLabelPolicy);
        Assert.Contains("실명", template.ParticipationPolicy.IdentityDisplayPolicy);
        Assert.Contains("닉네임", template.ParticipationPolicy.IdentityDisplayPolicy);
        Assert.Contains("익명", template.ParticipationPolicy.IdentityDisplayPolicy);
        Assert.Contains("선택적 신뢰 신호", template.ParticipationPolicy.IdentityDisplayPolicy);
        Assert.Contains("행동 힌트", template.ParticipationPolicy.PermissionInterpretation);
        Assert.Contains(CommunityLedgerPermissionCodes.MarkPayment, settlementRole.Permissions);
        Assert.Contains(CommunityLedgerPermissionCodes.CloseLedger, settlementRole.Permissions);
        Assert.DoesNotContain("플랫폼 결제 대행", settlementRole.Permissions);
    }

    [Fact]
    public void Templates_ClassifyLedgerToTargetOperatingSystemAndEngines()
    {
        var cargo = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.CargoTransport);
        var mart = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.HongdalMart);
        var outbound = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.WarehouseOutbound);

        Assert.Equal(CommunityLedgerOperatingSystemCodes.DomesticCargoTransport, cargo.TargetOperatingSystemCode);
        Assert.Contains(CommunityLedgerEngineHints.TransportDispatch, cargo.EngineHints);

        Assert.Equal(CommunityLedgerOperatingSystemCodes.HongdalMartUrbanLogistics, mart.TargetOperatingSystemCode);
        Assert.Contains(CommunityLedgerEngineHints.PickingBatch, mart.EngineHints);
        Assert.Contains(CommunityLedgerEngineHints.FoodDeliveryDispatch, mart.EngineHints);

        Assert.Equal(CommunityLedgerOperatingSystemCodes.WarehouseCommerceFulfillment, outbound.TargetOperatingSystemCode);
        Assert.Contains(CommunityLedgerEngineHints.OutboundBatch, outbound.EngineHints);
        Assert.Contains(CommunityLedgerEngineHints.PickingBatch, outbound.EngineHints);
    }

    [Fact]
    public void Templates_ProvideDynamicUiAndBestLedgerConversationHints()
    {
        foreach (var template in CommunityLedgerTemplateCatalog.All)
        {
            Assert.NotEmpty(template.UiSectionHints);
            Assert.NotEmpty(template.ActionHints);
            Assert.False(string.IsNullOrWhiteSpace(template.원함확인질문));
            Assert.False(string.IsNullOrWhiteSpace(template.원함확인설명));
            Assert.NotEmpty(template.원함확인질문목록);
            Assert.NotEmpty(template.홍달지원범위안내목록);
            Assert.NotEmpty(template.사용자확인책임안내목록);
            Assert.NotEmpty(template.LedgerBlocks);
            Assert.NotEmpty(template.CompositionRules);
            Assert.NotEmpty(template.ProcessingSurfaces);
            Assert.Equal(CommunityLedgerOperatingSystemRoleCodes.Scheduler, template.OperatingSystemRoleCode);
            Assert.Contains("API", template.OperatingSystemRoleSummary);
            Assert.NotEmpty(template.SchedulingHints);
            Assert.Equal(CommunityLedgerParticipationModes.OpenRoleParticipation, template.ParticipationPolicy.DefaultParticipationMode);
            Assert.NotEmpty(template.ParticipationPolicy.RestrictionTriggers);
            Assert.NotEmpty(template.ParticipationPolicy.RestrictableActionCodes);
            Assert.Equal(1, template.ParticipationPolicy.ExperiencePolicy.InitialLevel);
            Assert.NotEmpty(template.ParticipationPolicy.ExperiencePolicy.LevelTiers);
            Assert.NotEmpty(template.ParticipationPolicy.ExperiencePolicy.ExperienceEvents);
            Assert.Equal(CommunityLedgerPrimaryStoreKinds.MongoDocument, template.PersistencePolicy.PrimaryStoreKind);
            Assert.Equal("community_ledgers", template.PersistencePolicy.PrimaryStoreName);
            Assert.DoesNotContain(template.PersistencePolicy.RelationalProjectionTargets, target =>
                target.TargetName.Contains("블록 관계", StringComparison.OrdinalIgnoreCase)
                || target.EntityHint.Contains("CommunityLedgerBlockProjection", StringComparison.OrdinalIgnoreCase));
            Assert.False(string.IsNullOrWhiteSpace(template.BestLedgerPatternTitle));
            Assert.False(string.IsNullOrWhiteSpace(template.BestLedgerPatternSummary));
            Assert.NotEmpty(template.CommunityDiscussionPrompts);
        }
    }

    [Fact]
    public void Templates_StructureLedgersIntoReusableBlocks()
    {
        foreach (var template in CommunityLedgerTemplateCatalog.All)
        {
            Assert.Contains(template.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.Participant);
            Assert.Contains(template.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.State);
            Assert.Contains(template.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.Handoff && block.OpensApiHandoff);
            Assert.NotEmpty(template.BlockRelations);
            Assert.Contains(template.BlockRelations, relation => relation.RelationType == CommunityLedgerRelationTypes.Flow);
            Assert.All(template.LedgerBlocks, block =>
            {
                Assert.False(string.IsNullOrWhiteSpace(block.Code));
                Assert.False(string.IsNullOrWhiteSpace(block.DisplayName));
                Assert.False(string.IsNullOrWhiteSpace(block.Purpose));
                Assert.NotEmpty(block.DataHints);
                Assert.NotEmpty(block.ActionHints);
            });
            Assert.Contains(template.LedgerBlocks, block => block.RequiredForAiJudgment);
        }

        var cargo = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.CargoTransport);
        Assert.Contains(cargo.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.Place && block.UiSectionHint == "상차지");
        Assert.Contains(cargo.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.Place && block.UiSectionHint == "하차지");
        Assert.Contains(cargo.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.Item && block.UiSectionHint == "화물 조건");
        Assert.Contains(cargo.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.Evidence);
        Assert.Contains(cargo.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.Settlement);

        var mart = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.HongdalMart);
        Assert.Contains(mart.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.Order && block.UiSectionHint == "주문");
        Assert.Contains(mart.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.Inventory && block.UiSectionHint == "도심 재고");
        Assert.Contains(mart.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.Handoff && block.UiSectionHint == "기사 픽업");
        Assert.Contains(mart.LedgerBlocks, block => block.CompositionRuleCodes.Contains(CommunityLedgerCompositionRuleCodes.MartOrderBeforePickingPacking));

        var groupPurchase = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.GroupPurchase);
        Assert.Contains(groupPurchase.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.Decision && block.UiSectionHint == "투표/결정");
        Assert.Contains(groupPurchase.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.Order && block.UiSectionHint == "주문 집계");
        Assert.DoesNotContain(groupPurchase.LedgerBlocks, block => block.UiSectionHint == "통관 상태");

        var groupOrder = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.GroupOrder);
        Assert.True(groupOrder.IsInternalAggregationTemplate);
        Assert.Contains(groupOrder.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.Order && block.UiSectionHint == "개별 주문 원장");
        Assert.Contains(groupOrder.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.Quantity && block.UiSectionHint == "주문 수량 합계");
        Assert.Contains(groupOrder.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.Handoff && block.OpensApiHandoff);

        var groupImport = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.GroupImport);
        Assert.Contains(groupImport.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.Order && block.UiSectionHint == "원천 공동구매 원장");
        Assert.Contains(groupImport.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.Decision && block.UiSectionHint == "수입 결정");
        Assert.Contains(groupImport.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.State && block.UiSectionHint == "해외 선적");
        Assert.Contains(groupImport.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.State && block.UiSectionHint == "통관 상태");
        Assert.Contains(groupImport.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.Handoff && block.UiSectionHint == "국내 반출");
        Assert.Contains(groupImport.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.Inventory && block.UiSectionHint == "3PL 입고");
        Assert.Contains(groupImport.LedgerBlocks, block => block.BlockType == CommunityLedgerBlockTypes.Handoff && block.UiSectionHint == "세대 분배");
        Assert.Contains(groupImport.LedgerBlocks, block => block.CompositionRuleCodes.Contains(CommunityLedgerCompositionRuleCodes.GroupPurchaseCustomsBeforeDomesticDistribution));
    }

    [Fact]
    public void PriorityModules_MapLedgersAndInterLedgerRelations()
    {
        var modules = CommunityLedgerTemplateCatalog.PriorityImplementationModules;

        Assert.Equal(19, modules.Count);
        Assert.Equal(Enumerable.Range(1, 19), modules.Select(module => module.Priority).Order());
        Assert.Equal("커뮤니티 대화 원장", modules[0].DisplayName);
        Assert.Contains(modules, module => module.ModuleCode == CommunityLedgerImplementationModuleCodes.WishLedgerAssessment);
        Assert.Contains(modules, module => module.ModuleCode == CommunityLedgerImplementationModuleCodes.OrderRoot);
        Assert.Contains(modules, module => module.DisplayName == "마트 배송 원장");
        Assert.Contains(modules, module => module.ModuleCode == CommunityLedgerImplementationModuleCodes.GroupPurchaseDemand);
        Assert.Contains(modules, module => module.ModuleCode == CommunityLedgerImplementationModuleCodes.GroupOrderAggregation);
        Assert.Contains(modules, module => module.ModuleCode == CommunityLedgerImplementationModuleCodes.GroupPurchaseImportDecision);
        Assert.Contains(modules, module => module.ModuleCode == CommunityLedgerImplementationModuleCodes.GroupPurchaseShipmentCustoms);
        Assert.Contains(modules, module => module.ModuleCode == CommunityLedgerImplementationModuleCodes.GroupPurchaseDistribution);
        Assert.Contains(modules, module => module.ModuleCode == CommunityLedgerImplementationModuleCodes.FoodOrder);
        Assert.Contains(modules, module => module.ModuleCode == CommunityLedgerImplementationModuleCodes.FoodDelivery);
        Assert.Contains(modules, module => module.ModuleCode == CommunityLedgerImplementationModuleCodes.WarehouseInbound);

        var relations = CommunityLedgerTemplateCatalog.LedgerRelations;
        Assert.Contains(relations, relation =>
            relation.FromModuleCode == CommunityLedgerImplementationModuleCodes.OrderRoot
            && relation.ToModuleCode == CommunityLedgerImplementationModuleCodes.CargoTransport
            && relation.RelationType == CommunityLedgerRelationTypes.Contains
            && relation.Cardinality == CommunityLedgerRelationCardinality.OneToMany);
        Assert.Contains(relations, relation =>
            relation.FromModuleCode == CommunityLedgerImplementationModuleCodes.GroupPurchaseDemand
            && relation.ToModuleCode == CommunityLedgerImplementationModuleCodes.GroupOrderAggregation
            && relation.RelationType == CommunityLedgerRelationTypes.Contains
            && relation.Cardinality == CommunityLedgerRelationCardinality.OneToMany
            && relation.Required);
        Assert.Contains(relations, relation =>
            relation.FromModuleCode == CommunityLedgerImplementationModuleCodes.GroupOrderAggregation
            && relation.ToModuleCode == CommunityLedgerImplementationModuleCodes.OrderRoot
            && relation.RelationType == CommunityLedgerRelationTypes.Contains
            && relation.Cardinality == CommunityLedgerRelationCardinality.OneToMany
            && relation.Required);
        Assert.Contains(relations, relation =>
            relation.FromModuleCode == CommunityLedgerImplementationModuleCodes.CommunityConversation
            && relation.ToModuleCode == CommunityLedgerImplementationModuleCodes.WishLedgerAssessment
            && relation.RelationType == CommunityLedgerRelationTypes.Handoff
            && relation.Cardinality == CommunityLedgerRelationCardinality.OneToMany);
        Assert.Contains(relations, relation =>
            relation.FromModuleCode == CommunityLedgerImplementationModuleCodes.PickingPacking
            && relation.ToModuleCode == CommunityLedgerImplementationModuleCodes.HongdalMartDelivery
            && relation.Cardinality == CommunityLedgerRelationCardinality.ManyToOne
            && relation.Required);
        Assert.Contains(relations, relation =>
            relation.FromModuleCode == CommunityLedgerImplementationModuleCodes.TransportProgress
            && relation.ToModuleCode == CommunityLedgerImplementationModuleCodes.ReportDispute);
        Assert.Contains(relations, relation =>
            relation.FromModuleCode == CommunityLedgerImplementationModuleCodes.GroupPurchaseDemand
            && relation.ToModuleCode == CommunityLedgerImplementationModuleCodes.GroupPurchaseImportDecision
            && relation.FromLedgerTemplateKey == CommunityLedgerTemplateKeys.GroupPurchase
            && relation.ToLedgerTemplateKey == CommunityLedgerTemplateKeys.GroupImport
            && relation.RelationType == CommunityLedgerRelationTypes.Handoff
            && !relation.Required);
        Assert.Contains(relations, relation =>
            relation.FromModuleCode == CommunityLedgerImplementationModuleCodes.GroupPurchaseShipmentCustoms
            && relation.ToModuleCode == CommunityLedgerImplementationModuleCodes.GroupPurchaseDistribution
            && relation.RelationType == CommunityLedgerRelationTypes.Handoff
            && relation.Required);
        Assert.Contains(relations, relation =>
            relation.FromModuleCode == CommunityLedgerImplementationModuleCodes.FoodOrder
            && relation.ToModuleCode == CommunityLedgerImplementationModuleCodes.FoodDelivery
            && relation.RelationType == CommunityLedgerRelationTypes.Handoff
            && relation.Cardinality == CommunityLedgerRelationCardinality.OneToMany
            && !relation.Required);
        Assert.Contains(relations, relation =>
            relation.FromModuleCode == CommunityLedgerImplementationModuleCodes.TransportProgress
            && relation.ToModuleCode == CommunityLedgerImplementationModuleCodes.WarehouseInbound
            && relation.RelationType == CommunityLedgerRelationTypes.Handoff
            && relation.Cardinality == CommunityLedgerRelationCardinality.OneToMany
            && !relation.Required);
    }

    [Fact]
    public void WarehouseTransfer_ConnectsOutboundTransportAndInboundLedgers()
    {
        var relations = CommunityLedgerTemplateCatalog.LedgerRelations;

        Assert.Contains(relations, relation =>
            relation.FromModuleCode == CommunityLedgerImplementationModuleCodes.WarehouseOutbound
            && relation.ToModuleCode == CommunityLedgerImplementationModuleCodes.PickingPacking
            && relation.RelationType == CommunityLedgerRelationTypes.Contains);
        Assert.Contains(relations, relation =>
            relation.FromModuleCode == CommunityLedgerImplementationModuleCodes.PickingPacking
            && relation.ToModuleCode == CommunityLedgerImplementationModuleCodes.CargoTransport
            && relation.RelationType == CommunityLedgerRelationTypes.Handoff);
        Assert.Contains(relations, relation =>
            relation.FromModuleCode == CommunityLedgerImplementationModuleCodes.CargoTransport
            && relation.ToModuleCode == CommunityLedgerImplementationModuleCodes.TransportProgress
            && relation.RelationType == CommunityLedgerRelationTypes.Flow);
        Assert.Contains(relations, relation =>
            relation.FromModuleCode == CommunityLedgerImplementationModuleCodes.TransportProgress
            && relation.ToModuleCode == CommunityLedgerImplementationModuleCodes.WarehouseInbound
            && relation.RelationType == CommunityLedgerRelationTypes.Handoff);
    }

    [Fact]
    public void HongdalMartTemplate_UsesDeliveryLedgerNameAndImmediateDeliveryAsAttribute()
    {
        var mart = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.HongdalMart);

        Assert.Equal("알뜰살뜰 마트 배송 원장", mart.DisplayName);
        Assert.Contains("배송유형", mart.Summary);
        Assert.Contains("즉시배송", mart.Summary);
        Assert.DoesNotContain("즉시배송 원장", mart.DisplayName);
        Assert.Contains(mart.PersistencePolicy.RelationalProjectionTargets, target => target.TargetName == "마트 배송 배차");
    }

    [Fact]
    public void BlockRelations_ExposeRequiredRelationsFromCompositionRules()
    {
        var mart = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.HongdalMart);

        Assert.Contains(mart.BlockRelations, relation =>
            relation.RelationType == CommunityLedgerRelationTypes.Requires
            && relation.Required
            && relation.CompositionRuleCode == CommunityLedgerCompositionRuleCodes.MartOrderBeforePickingPacking
            && relation.FromBlockCode.Contains("order", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(mart.BlockRelations, relation =>
            relation.RelationType == CommunityLedgerRelationTypes.Requires
            && relation.Required
            && relation.CompositionRuleCode == CommunityLedgerCompositionRuleCodes.MartPackedBeforeDeliveryPickup
            && relation.FromBlockCode.Contains("packing-complete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Templates_DeclareExistingOrPlannedProcessingSurfaces()
    {
        string[] allowedHandoffModes =
        [
            CommunityLedgerHandoffModes.HttpApi,
            CommunityLedgerHandoffModes.InternalService,
            CommunityLedgerHandoffModes.PlannedApi
        ];

        foreach (var template in CommunityLedgerTemplateCatalog.All)
        {
            Assert.All(template.ProcessingSurfaces, surface =>
            {
                Assert.Contains(surface.HandoffMode, allowedHandoffModes);
                Assert.False(string.IsNullOrWhiteSpace(surface.Purpose));
                Assert.False(string.IsNullOrWhiteSpace(surface.RoutePattern) && string.IsNullOrWhiteSpace(surface.ApiEndpointKey));
            });
        }

        var cargo = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.CargoTransport);
        Assert.Contains(cargo.ProcessingSurfaces, surface => surface.ApiEndpointKey == "화주운송의뢰Controller.의뢰생성");
        Assert.Contains(cargo.ProcessingSurfaces, surface => surface.ApiEndpointKey == "기사운송진행Controller.상차완료");
    }

    [Fact]
    public void Templates_StartMembersAtLevelOneAndGrowByHelpfulLedgerActions()
    {
        foreach (var template in CommunityLedgerTemplateCatalog.All)
        {
            var policy = template.ParticipationPolicy.ExperiencePolicy;

            Assert.Equal(1, policy.InitialLevel);
            Assert.Contains("가입 시 1레벨", policy.InitialLevelSummary);
            Assert.Contains("고정 역할 권한이 아니라", policy.LevelBasis);
            Assert.Contains("신고", policy.RestrictionInteractionPolicy);
            Assert.Contains(policy.LevelTiers, tier => tier.Level == 1 && tier.RequiredExperience == 0);
            Assert.Contains(policy.LevelTiers, tier => tier.Level == 2 && tier.RequiredExperience > 0);
            Assert.Contains(policy.LevelTiers, tier => tier.Label.Contains("신뢰", StringComparison.Ordinal));
            Assert.Contains(policy.ExperienceEvents, item => item.EventCode == CommunityLedgerExperienceEventCodes.LedgerDraftCreated);
            Assert.Contains(policy.ExperienceEvents, item => item.EventCode == CommunityLedgerExperienceEventCodes.CompletionConfirmed);
            Assert.Contains(policy.ExperienceEvents, item => item.EventCode == CommunityLedgerExperienceEventCodes.WorkStateChanged);
            Assert.Contains(policy.ExperienceEvents, item => item.EventCode == CommunityLedgerExperienceEventCodes.TransportPickupArrived);
            Assert.Contains(policy.ExperienceEvents, item => item.EventCode == CommunityLedgerExperienceEventCodes.TransportPickupCompleted);
            Assert.Contains(policy.ExperienceEvents, item => item.EventCode == CommunityLedgerExperienceEventCodes.TransportDropoffArrived);
            Assert.Contains(policy.ExperienceEvents, item => item.EventCode == CommunityLedgerExperienceEventCodes.TransportDropoffCompleted);
            Assert.Contains(policy.ExperienceEvents, item => item.EventCode == CommunityLedgerExperienceEventCodes.TransportIssueReported);
            Assert.Contains(policy.ExperienceEvents, item => item.EventCode == CommunityLedgerExperienceEventCodes.FoodOrderAccepted);
            Assert.Contains(policy.ExperienceEvents, item => item.EventCode == CommunityLedgerExperienceEventCodes.WarehouseInboundCompleted);
            Assert.Contains(policy.ExperienceEvents, item => item.EventCode == CommunityLedgerExperienceEventCodes.WarehouseInboundInspected);
            Assert.Contains(policy.ExperienceEvents, item => item.EventCode == CommunityLedgerExperienceEventCodes.WarehousePutAwayCompleted);
            Assert.Contains(policy.ExperienceEvents, item => item.EventCode == CommunityLedgerExperienceEventCodes.WarehousePickingCompleted);
            Assert.Contains(policy.ExperienceEvents, item => item.EventCode == CommunityLedgerExperienceEventCodes.WarehouseInventoryPacked);
            Assert.Contains(policy.ExperienceEvents, item => item.EventCode == CommunityLedgerExperienceEventCodes.WarehouseReconsignmentCreated);
            Assert.All(policy.ExperienceEvents, item => Assert.True(item.BaseExperience > 0));
        }
    }

    [Fact]
    public void Templates_TreatOperatingSystemAsSchedulerAndApiAsExecutor()
    {
        foreach (var template in CommunityLedgerTemplateCatalog.All)
        {
            Assert.Equal(CommunityLedgerOperatingSystemRoleCodes.Scheduler, template.OperatingSystemRoleCode);
            Assert.Contains("호출 순서", template.OperatingSystemRoleSummary);
            Assert.Contains("실제 처리 API/엔진 호출 순서 결정", template.SchedulingHints);
            Assert.NotEmpty(template.ProcessingSurfaces);
        }
    }

    [Fact]
    public void Templates_RestrictRolesOnlyAfterReportDisputeOrModerationSignals()
    {
        foreach (var template in CommunityLedgerTemplateCatalog.All)
        {
            Assert.Equal(CommunityLedgerParticipationModes.OpenRoleParticipation, template.ParticipationPolicy.DefaultParticipationMode);
            Assert.Contains("신고", template.ParticipationPolicy.RestrictionPolicy);
            Assert.Contains("분쟁", template.ParticipationPolicy.RestrictionPolicy);
            Assert.Contains("신고 접수", template.ParticipationPolicy.RestrictionTriggers);
            Assert.Contains("운영자 검토", template.ParticipationPolicy.RestrictionTriggers);
            Assert.Contains(CommunityLedgerPermissionCodes.ChangeState, template.ParticipationPolicy.RestrictableActionCodes);
            Assert.Contains(CommunityLedgerPermissionCodes.CloseLedger, template.ParticipationPolicy.RestrictableActionCodes);
        }
    }

    [Fact]
    public void Templates_TreatMongoAsFlexibleLedgerSourceAndRelationalDbAsProjection()
    {
        var outbound = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.WarehouseOutbound);

        Assert.Equal(CommunityLedgerPrimaryStoreKinds.MongoDocument, outbound.PersistencePolicy.PrimaryStoreKind);
        Assert.Contains("MongoDB", outbound.PersistencePolicy.FlexibleAttributeStrategy);
        Assert.Contains("관계형 DB", outbound.PersistencePolicy.RelationalProjectionPolicy);
        Assert.Contains(outbound.PersistencePolicy.RelationalProjectionTargets, target => target.TargetName == "창고 재고");
        Assert.Contains(outbound.PersistencePolicy.RelationalProjectionTargets, target => target.LinkFieldHint.Contains("CommunityLedgerId", StringComparison.OrdinalIgnoreCase));

        var groupPurchase = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.GroupPurchase);
        Assert.Contains(groupPurchase.PersistencePolicy.RelationalProjectionTargets, target => target.TargetName == "주문집계 인계");
        Assert.DoesNotContain(groupPurchase.PersistencePolicy.RelationalProjectionTargets, target => target.TargetName == "공동수입 결정");

        var groupOrder = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.GroupOrder);
        Assert.Contains(groupOrder.PersistencePolicy.RelationalProjectionTargets, target => target.TargetName == "공동구매 주문집계");

        var groupImport = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.GroupImport);
        Assert.Contains(groupImport.PersistencePolicy.RelationalProjectionTargets, target => target.TargetName == "공동수입 결정");
        Assert.Contains(groupImport.PersistencePolicy.RelationalProjectionTargets, target => target.TargetName == "국내 운송 의뢰");
        Assert.Contains(groupImport.PersistencePolicy.RelationalProjectionTargets, target => target.TargetName == "국내 3PL 입고");
    }

    [Fact]
    public void Templates_GateActionsBehindCompositionRules()
    {
        var cargo = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.CargoTransport);
        var cargoRule = cargo.CompositionRules.Single(rule =>
            rule.Code == CommunityLedgerCompositionRuleCodes.TransportRequestBeforePickupDropoff);

        Assert.Contains("상차지", cargoRule.RequiredUiSectionHints);
        Assert.Contains("하차지", cargoRule.RequiredUiSectionHints);
        Assert.Contains("상차 확인", cargoRule.GatedActionHints);
        Assert.Contains("하차 완료", cargoRule.GatedActionHints);

        var outbound = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.WarehouseOutbound);
        var inboundRule = outbound.CompositionRules.Single(rule =>
            rule.Code == CommunityLedgerCompositionRuleCodes.InboundOrStockBeforeOutbound);

        Assert.Contains(CommunityLedgerTemplateKeys.WarehouseInbound, inboundRule.RequiredLedgerTemplateKeys);
        Assert.Contains("출고 품목", inboundRule.RequiredUiSectionHints);
        Assert.Contains("피킹 시작", inboundRule.GatedActionHints);

        var mart = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.HongdalMart);
        var martOrderRule = mart.CompositionRules.Single(rule =>
            rule.Code == CommunityLedgerCompositionRuleCodes.MartOrderBeforePickingPacking);
        var martPickupRule = mart.CompositionRules.Single(rule =>
            rule.Code == CommunityLedgerCompositionRuleCodes.MartPackedBeforeDeliveryPickup);

        Assert.Contains("주문", martOrderRule.RequiredUiSectionHints);
        Assert.Contains("도심 재고", martOrderRule.RequiredUiSectionHints);
        Assert.Contains("포장 완료", martOrderRule.GatedActionHints);
        Assert.Contains("포장 완료", martPickupRule.RequiredUiSectionHints);
        Assert.Contains("기사 인계", martPickupRule.GatedActionHints);
        Assert.DoesNotContain(CommunityLedgerTemplateKeys.WarehouseOutbound, martOrderRule.RequiredLedgerTemplateKeys);
        Assert.DoesNotContain(CommunityLedgerTemplateKeys.WarehouseOutbound, martPickupRule.RequiredLedgerTemplateKeys);

        var groupPurchase = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.GroupPurchase);
        var groupOrderHandoffRule = groupPurchase.CompositionRules.Single(rule =>
            rule.Code == CommunityLedgerCompositionRuleCodes.GroupPurchaseAgreementBeforeGroupOrder);
        var groupOrder = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.GroupOrder);
        var individualOrderRule = groupOrder.CompositionRules.Single(rule =>
            rule.Code == CommunityLedgerCompositionRuleCodes.GroupOrderRequiresIndividualOrders);
        var groupImport = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.GroupImport);
        var importDecisionRule = groupImport.CompositionRules.Single(rule =>
            rule.Code == CommunityLedgerCompositionRuleCodes.GroupPurchaseDemandBeforeImportDecision);
        var shipmentRule = groupImport.CompositionRules.Single(rule =>
            rule.Code == CommunityLedgerCompositionRuleCodes.GroupPurchaseImportDecisionBeforeShipment);
        var distributionRule = groupImport.CompositionRules.Single(rule =>
            rule.Code == CommunityLedgerCompositionRuleCodes.GroupPurchaseCustomsBeforeDomesticDistribution);

        Assert.Equal("공동구매 원장", groupPurchase.DisplayName);
        Assert.Equal("공동구매 주문집계 원장", groupOrder.DisplayName);
        Assert.Equal("공동수입 원장", groupImport.DisplayName);
        Assert.Contains(CommunityLedgerTemplateKeys.GroupOrder, groupOrderHandoffRule.RequiredLedgerTemplateKeys);
        Assert.Contains("합의", groupOrderHandoffRule.RequiredUiSectionHints);
        Assert.Contains(CommunityLedgerTemplateKeys.Order, individualOrderRule.RequiredLedgerTemplateKeys);
        Assert.Contains("개별 주문 원장", individualOrderRule.RequiredUiSectionHints);
        Assert.Contains(CommunityLedgerTemplateKeys.GroupPurchase, importDecisionRule.RequiredLedgerTemplateKeys);
        Assert.Contains("원천 공동구매 원장", importDecisionRule.RequiredUiSectionHints);
        Assert.Contains("수입 진행 결정", importDecisionRule.GatedActionHints);
        Assert.Contains("수입 결정", shipmentRule.RequiredUiSectionHints);
        Assert.Contains("통관 상태 동기화", shipmentRule.GatedActionHints);
        Assert.Contains("통관 상태", distributionRule.RequiredUiSectionHints);
        Assert.Contains("국내 반출", distributionRule.RequiredUiSectionHints);
        Assert.Contains("3PL 입고 인계", distributionRule.GatedActionHints);
        Assert.Contains(CommunityLedgerTemplateKeys.WarehouseInbound, distributionRule.RequiredLedgerTemplateKeys);
        Assert.Contains(CommunityLedgerTemplateKeys.CargoTransport, distributionRule.RequiredLedgerTemplateKeys);
    }

    [Fact]
    public void DeliveryLedgers_DeclareTheirSourceLedgers()
    {
        var foodDelivery = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.FoodDelivery);
        var orderRule = foodDelivery.CompositionRules.Single(rule =>
            rule.Code == CommunityLedgerCompositionRuleCodes.FoodOrderBeforeDelivery);

        Assert.Contains(CommunityLedgerTemplateKeys.FoodOrder, orderRule.RequiredLedgerTemplateKeys);
        Assert.Contains("픽업지", orderRule.RequiredUiSectionHints);
        Assert.Contains("도착지", orderRule.RequiredUiSectionHints);
        Assert.Contains("배달 회차", foodDelivery.UiSectionHints);
        Assert.Contains("분할 항목", foodDelivery.UiSectionHints);
        Assert.Contains("재배달 사유", foodDelivery.UiSectionHints);

        var foodOrder = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.FoodOrder);
        Assert.DoesNotContain("픽업", foodOrder.UiSectionHints);
        Assert.DoesNotContain("전달", foodOrder.UiSectionHints);
        Assert.DoesNotContain(foodOrder.ProcessingSurfaces, surface => surface.ControllerName == "배차주소Controller");

        var outbound = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.WarehouseOutbound);
        var transportRule = outbound.CompositionRules.Single(rule =>
            rule.Code == CommunityLedgerCompositionRuleCodes.OutboundBeforeHandoffTransport);

        Assert.Contains(CommunityLedgerTemplateKeys.CargoTransport, transportRule.RequiredLedgerTemplateKeys);
        Assert.Contains("운송 인계", transportRule.GatedActionHints);
    }

    [Fact]
    public void FlowClassifier_IdentifiesHongdalMartFlowFromLedgerShape()
    {
        var result = CommunityLedgerFlowClassifier.Analyze(new()
        {
            Title = "알뜰살뜰 마트 장보기 즉시배송",
            Body = "도심 재고를 피킹하고 포장 완료 뒤 기사 픽업으로 고객에게 전달합니다.",
            UiSectionHints = ["참여자", "주문", "도심 재고", "피킹/포장", "포장 완료", "기사 픽업"],
            ActionHints = ["재고 확인", "피킹 시작", "피킹 완료", "포장 완료", "기사 인계", "전달 완료"]
        });

        Assert.Equal(CommunityLedgerTemplateKeys.HongdalMart, result.PrimaryCandidate.TemplateKey);
        Assert.Equal(CommunityLedgerOperatingSystemCodes.HongdalMartUrbanLogistics, result.PrimaryCandidate.TargetOperatingSystemCode);
        Assert.Equal(CommunityLedgerFlowRelationCodes.StrongFlowMatch, result.PrimaryCandidate.RelationCode);
        Assert.False(result.RequiresHumanReview);
        Assert.Contains(CommunityLedgerEngineHints.FoodDeliveryDispatch, result.PrimaryCandidate.EngineHints);
        Assert.Contains(CommunityLedgerCompositionRuleCodes.MartOrderBeforePickingPacking, result.PrimaryCandidate.RelatedCompositionRuleCodes);
        Assert.Contains(CommunityLedgerCompositionRuleCodes.MartPackedBeforeDeliveryPickup, result.PrimaryCandidate.RelatedCompositionRuleCodes);
        Assert.Contains(result.PrimaryCandidate.RelatedProcessingSurfaceHints, surface => surface == "GET 기사배차추천Controller.조회");
        Assert.Contains(result.PrimaryCandidate.RelatedLedgerBlockCodes, blockCode => blockCode.Contains("urban-inventory", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.PrimaryCandidate.RelatedLedgerBlockCodes, blockCode => blockCode.Contains("driver-pickup", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FlowClassifier_IdentifiesGroupPurchaseImportFlowFromLedgerShape()
    {
        var result = CommunityLedgerFlowClassifier.Analyze(new()
        {
            Title = "중국 생활용품 공동주문 수입",
            Body = "참여자 모집 수량을 보고 수입 결정을 한 뒤 해외 선적, 통관 상태, 국내 반출, 3PL 입고와 세대 분배를 진행합니다.",
            UiSectionHints = ["참여자", "모집 수량", "투표/결정", "수입 결정", "해외 선적", "통관 상태", "국내 반출", "3PL 입고", "세대 분배"],
            ActionHints = ["참여 신청", "수량 확정", "수입 진행 결정", "해외 발주/선적 등록", "통관 상태 동기화", "3PL 입고 인계", "세대 분배 시작"]
        });

        Assert.Equal(CommunityLedgerTemplateKeys.GroupImport, result.PrimaryCandidate.TemplateKey);
        Assert.Equal(CommunityLedgerOperatingSystemCodes.GroupPurchaseImport, result.PrimaryCandidate.TargetOperatingSystemCode);
        Assert.Equal(CommunityLedgerFlowRelationCodes.StrongFlowMatch, result.PrimaryCandidate.RelationCode);
        Assert.False(result.RequiresHumanReview);
        Assert.Contains(CommunityLedgerEngineHints.ImportCustoms, result.PrimaryCandidate.EngineHints);
        Assert.Contains(CommunityLedgerCompositionRuleCodes.GroupPurchaseDemandBeforeImportDecision, result.PrimaryCandidate.RelatedCompositionRuleCodes);
        Assert.Contains(CommunityLedgerCompositionRuleCodes.GroupPurchaseImportDecisionBeforeShipment, result.PrimaryCandidate.RelatedCompositionRuleCodes);
        Assert.Contains(CommunityLedgerCompositionRuleCodes.GroupPurchaseCustomsBeforeDomesticDistribution, result.PrimaryCandidate.RelatedCompositionRuleCodes);
        Assert.Contains(result.PrimaryCandidate.RelatedProcessingSurfaceHints, surface => surface == "POST 공동구매해외선적추적Controller.통관동기화");
        Assert.Contains(result.PrimaryCandidate.RelatedLedgerBlockCodes, blockCode => blockCode.Contains("import-decision", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.PrimaryCandidate.RelatedLedgerBlockCodes, blockCode => blockCode.Contains("customs-state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void 수입준비도는_일반분류기의_자동전환후보가_아닌_커뮤니티선택형템플릿이다()
    {
        var template = CommunityLedgerTemplateCatalog.Find(CommunityLedgerTemplateKeys.MeatImportReadiness);
        var result = CommunityLedgerFlowClassifier.Analyze(new()
        {
            Title = "미국산 돼지고기 수입 준비",
            Body = "해외 작업장, 검역, 통관 절차와 양측 확인을 검토합니다."
        });

        Assert.True(template.IsCommunityOpportunityTemplate);
        Assert.Equal(CommunityLedgerOperatingSystemCodes.CommunityTrust, template.TargetOperatingSystemCode);
        Assert.DoesNotContain(result.Candidates, candidate =>
            candidate.TemplateKey == CommunityLedgerTemplateKeys.MeatImportReadiness);
    }

    [Fact]
    public void FlowClassifier_SeparatesDomesticGroupPurchaseFromGroupImport()
    {
        var result = CommunityLedgerFlowClassifier.Analyze(new()
        {
            Title = "아파트 생활용품 공동구매",
            Body = "참여자별 개별 주문과 수량을 모아 공동 가격과 구매처를 확정하고 수령 거점에서 분배합니다.",
            UiSectionHints = ["개별 주문 원장", "주문 수량 합계", "공동 조건", "투표/결정", "구매 확정", "수령 거점", "분배"],
            ActionHints = ["개별 주문 연결", "묶음 조건 확정", "구매 확정", "수령 거점 확정", "분배 시작"]
        });

        Assert.Equal(CommunityLedgerTemplateKeys.GroupPurchase, result.PrimaryCandidate.TemplateKey);
        Assert.Equal(CommunityLedgerOperatingSystemCodes.CommunityTrust, result.PrimaryCandidate.TargetOperatingSystemCode);
        Assert.DoesNotContain(CommunityLedgerEngineHints.ImportCustoms, result.PrimaryCandidate.EngineHints);
    }

    [Fact]
    public void FlowClassifier_SeparatesGenericWarehouseOutboundFromHongdalMart()
    {
        var result = CommunityLedgerFlowClassifier.Analyze(new()
        {
            Title = "판매채널 창고 출고",
            Body = "출고 품목을 피킹하고 검수와 포장을 마친 뒤 운송 인계합니다.",
            UiSectionHints = ["참여자", "출고 품목", "피킹 작업", "검수", "포장", "운송 인계"],
            ActionHints = ["피킹 시작", "피킹 완료", "검수 요청", "포장 완료", "운송 인계"]
        });

        Assert.Equal(CommunityLedgerTemplateKeys.WarehouseOutbound, result.PrimaryCandidate.TemplateKey);
        Assert.Equal(CommunityLedgerOperatingSystemCodes.WarehouseCommerceFulfillment, result.PrimaryCandidate.TargetOperatingSystemCode);
        Assert.NotEqual(CommunityLedgerTemplateKeys.HongdalMart, result.PrimaryCandidate.TemplateKey);
        Assert.Contains(CommunityLedgerCompositionRuleCodes.InboundOrStockBeforeOutbound, result.PrimaryCandidate.RelatedCompositionRuleCodes);
        Assert.Contains(CommunityLedgerCompositionRuleCodes.OutboundBeforeHandoffTransport, result.PrimaryCandidate.RelatedCompositionRuleCodes);
    }

    [Fact]
    public void BuildDraftBody_UsesOptionalEvidenceAndParticipantConfirmationTone()
    {
        var body = CommunityLedgerTemplateCatalog.BuildDraftBody(
            CommunityLedgerTemplateKeys.LocalSale,
            "Hongdal Community",
            "동네 판매자");

        Assert.Contains("원장 유형: 생활 판매 원장", body);
        Assert.Contains("처리 체계: 창고·커머스 이행 처리 체계", body);
        Assert.Contains("원함 확인:", body);
        Assert.Contains("질문: 무엇을 원하나요?", body);
        Assert.Contains("홍달이 도울 수 있는 범위:", body);
        Assert.Contains("사용자가 직접 확인해야 하는 것:", body);
        Assert.Contains("처리 체계/엔진 인계:", body);
        Assert.Contains("처리 체계는 실행 주체라기보다 스케줄러", body);
        Assert.Contains("실제 처리 API/엔진 호출 순서 결정", body);
        Assert.Contains("실제 처리 표면:", body);
        Assert.Contains("저장/반영 방식:", body);
        Assert.Contains("community_ledgers", body);
        Assert.Contains("동적 UI 힌트:", body);
        Assert.Contains("가능한 행동:", body);
        Assert.Contains("원장 블록:", body);
        Assert.Contains("참여자 블록", body);
        Assert.Contains("판매 물건 블록", body);
        Assert.Contains("AI 판단근거", body);
        Assert.Contains("원장 모듈/관계:", body);
        Assert.Contains("블록 관계:", body);
        Assert.Contains("구성 규칙:", body);
        Assert.Contains("판매 물건과 상대가 먼저 정해져야 합니다.", body);
        Assert.Contains("베스트 원장 공유 포인트:", body);
        Assert.Contains("참여/역할 정책:", body);
        Assert.Contains("기본 참여 방식: OpenRoleParticipation", body);
        Assert.Contains("제한 트리거: 신고 접수", body);
        Assert.Contains("성장/레벨 정책:", body);
        Assert.Contains("시작 레벨: 가입 시 1레벨", body);
        Assert.Contains("경험치 행동: 원장 초안 작성", body);
        Assert.Contains("경험치 행동: 업무 상태 변경", body);
        Assert.Contains("경험치 행동: 운송 상차지 도착", body);
        Assert.Contains("경험치 행동: 운송 상차 완료", body);
        Assert.Contains("경험치 행동: 운송 하차지 도착", body);
        Assert.Contains("경험치 행동: 운송 하차 완료", body);
        Assert.Contains("경험치 행동: 운송 문제 신고", body);
        Assert.Contains("경험치 행동: 음식 주문 수락", body);
        Assert.Contains("경험치 행동: 창고 입고 완료", body);
        Assert.Contains("경험치 행동: 창고 피킹 완료", body);
        Assert.Contains("경험치 행동: 창고 포장 완료", body);
        Assert.Contains("경험치 행동: 창고 재위탁 운송 생성", body);
        Assert.Contains("성장 단계: Lv.3 신뢰 구성원", body);
        Assert.Contains("참여자/역할 라벨:", body);
        Assert.Contains("표시 이름:", body);
        Assert.Contains("실명이 아니라 닉네임", body);
        Assert.Contains("사진, 메모, 링크 증빙은 필요할 때만 첨부합니다.", body);
        Assert.Contains("결제나 입금 표시는 참여자 간 확인용으로 남깁니다.", body);
    }
}
