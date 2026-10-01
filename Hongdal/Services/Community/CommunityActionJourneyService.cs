using System.Text.Json;
using Hongdal.Contracts.Common.CollectiveProcurement;
using Hongdal.Contracts.Common.Community;
using Hongdal.Services.CollectiveProcurement;

namespace Hongdal.Services.Community;

public interface ICommunityActionJourneyService
{
    Task<CommunityActionJourneyResponse> BuildAsync(
        CommunityPostOpportunitySource source,
        CommunityPostParticipationEntryResponse participation,
        CommunityVoteResponse? interestVote,
        커뮤니티원장Dto? rootLedger,
        string displayLanguageCode,
        CancellationToken cancellationToken = default);
}

public sealed class CommunityActionJourneyService(
    ICollectiveProcurementPlanningStore planningStore,
    I커뮤니티원장저장소 ledgerStore) : ICommunityActionJourneyService
{
    public async Task<CommunityActionJourneyResponse> BuildAsync(
        CommunityPostOpportunitySource source,
        CommunityPostParticipationEntryResponse participation,
        CommunityVoteResponse? interestVote,
        커뮤니티원장Dto? rootLedger,
        string displayLanguageCode,
        CancellationToken cancellationToken = default)
    {
        var journeyRoot = CommunityActionJourneyProjection.IsJourneyRoot(participation, rootLedger)
            ? rootLedger
            : null;
        var children = await LoadChildLedgersAsync(journeyRoot, cancellationToken);
        var plan = await FindLatestPlanAsync(source, participation, journeyRoot, cancellationToken);
        return CommunityActionJourneyProjection.Build(
            source,
            participation,
            interestVote,
            journeyRoot,
            children,
            plan,
            displayLanguageCode);
    }

    private async Task<IReadOnlyList<커뮤니티원장Dto>> LoadChildLedgersAsync(
        커뮤니티원장Dto? rootLedger,
        CancellationToken cancellationToken)
    {
        var childIds = rootLedger?.포함원장목록
            .Select(reference => reference.원장Id?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToArray() ?? [];
        if (childIds.Length == 0)
        {
            return [];
        }

        var loaded = await Task.WhenAll(childIds.Select(id => ledgerStore.원장조회Async(id!, cancellationToken)));
        return loaded.Where(ledger => ledger is not null).Cast<커뮤니티원장Dto>().ToArray();
    }

    private async Task<CollectiveProcurementPlanState?> FindLatestPlanAsync(
        CommunityPostOpportunitySource source,
        CommunityPostParticipationEntryResponse participation,
        커뮤니티원장Dto? rootLedger,
        CancellationToken cancellationToken)
    {
        var references = new List<(string Type, string Reference)>
        {
            ("community-post", source.PostId.ToString())
        };
        if (!string.IsNullOrWhiteSpace(participation.PlanningSourceReferenceId))
        {
            references.Add((participation.PlanningSourceTypeCode, participation.PlanningSourceReferenceId));
        }

        if (rootLedger is not null)
        {
            references.Add(("community-ledger", rootLedger.원장Id));
        }

        var plans = new List<CollectiveProcurementPlanState>();
        foreach (var reference in references.Distinct())
        {
            plans.AddRange(await planningStore.ListBySourceAsync(
                reference.Type,
                reference.Reference,
                cancellationToken));
        }

        return plans
            .GroupBy(plan => plan.PlanId)
            .Select(group => group.OrderByDescending(plan => plan.UpdatedAtUtc).First())
            .OrderByDescending(plan => plan.UpdatedAtUtc)
            .FirstOrDefault();
    }
}

internal static class CommunityActionJourneyProjection
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static CommunityActionJourneyResponse Build(
        CommunityPostOpportunitySource source,
        CommunityPostParticipationEntryResponse participation,
        CommunityVoteResponse? interestVote,
        커뮤니티원장Dto? rootLedger,
        IReadOnlyList<커뮤니티원장Dto>? childLedgers,
        CollectiveProcurementPlanState? plan,
        string displayLanguageCode)
    {
        var english = string.Equals(
            displayLanguageCode,
            CommunityDisplayLanguageCodes.English,
            StringComparison.OrdinalIgnoreCase);
        if (source.IsReportBoardPost)
        {
            return new CommunityActionJourneyResponse
            {
                PostId = source.PostId,
                IsAvailable = false,
                CurrentStageCode = CommunityActionJourneyStageCodes.Unavailable,
                CurrentStageLabel = english ? "Unavailable" : "공동 행동 제외"
            };
        }

        var journeyRoot = IsJourneyRoot(participation, rootLedger) ? rootLedger : null;
        var children = journeyRoot is null ? [] : childLedgers ?? [];
        var roleSlots = participation.PartyFormation.RoleSlots
            .Select(slot => new CommunityActionJourneyRoleSlotResponse
            {
                RoleCode = slot.RoleCode,
                CategoryCode = slot.CategoryCode,
                Label = slot.Label,
                Summary = slot.Summary,
                IsRequired = slot.IsRequired,
                IsRecommended = slot.IsRecommended,
                InterestCount = slot.InterestCount,
                ConfirmedParticipantCount = slot.ConfirmedParticipantCount,
                StateCode = slot.StateCode,
                ExternalCredentialVerificationRequired = slot.ExternalCredentialVerificationRequired,
                ExternalCredentialVerified = slot.ExternalCredentialVerified
            })
            .ToArray();
        var economics = BuildEconomics(plan);
        var ledgers = BuildLedgers(journeyRoot, children);
        var hasExecutionLedger = ledgers.Any(ledger => !ledger.IsProvisional && ledger.RelationCode != "root");
        var stage = ResolveStage(
            interestVote,
            journeyRoot,
            children,
            economics,
            participation.PartyFormation);

        return new CommunityActionJourneyResponse
        {
            PostId = source.PostId,
            CurrentStageCode = stage,
            CurrentStageLabel = StageLabel(stage, english),
            InterestVoteId = participation.InterestVoteId,
            ProvisionalLedgerId = participation.ProvisionalLedgerId,
            ParticipantCount = participation.ParticipantCount,
            RequiredRoleCount = participation.PartyFormation.RequiredRoleSlotCount,
            FilledRequiredRoleCount = participation.PartyFormation.RepresentedRequiredRoleSlotCount,
            IsReadyForExecutionReview = participation.PartyFormation.IsReadyForRealLedgerReview
                                        && economics.ExecutionReady,
            HasExecutionLedger = hasExecutionLedger,
            Sales = BuildSales(source.SalesOfferJson),
            Economics = economics,
            Diagram = BuildDiagram(journeyRoot, children),
            RoleSlots = roleSlots,
            Ledgers = ledgers,
            Timeline = BuildTimeline(source, interestVote, journeyRoot, children, plan)
        };
    }

    internal static bool IsJourneyRoot(
        CommunityPostParticipationEntryResponse participation,
        커뮤니티원장Dto? ledger)
        => ledger is not null
           && (string.Equals(
                   participation.ProvisionalLedgerId,
                   ledger.원장Id,
                   StringComparison.OrdinalIgnoreCase)
               || IsProvisional(ledger));

    private static string ResolveStage(
        CommunityVoteResponse? interestVote,
        커뮤니티원장Dto? rootLedger,
        IReadOnlyList<커뮤니티원장Dto> children,
        CommunityActionJourneyEconomicsSummaryResponse economics,
        CommunityPostPartyFormationResponse party)
    {
        var executionLedgers = children.Where(ledger => !IsProvisional(ledger)).ToArray();
        if (executionLedgers.Any(ledger => IsState(ledger, 커뮤니티원장상태.완료))
            || rootLedger is not null && !IsProvisional(rootLedger) && IsState(rootLedger, 커뮤니티원장상태.완료))
        {
            return CommunityActionJourneyStageCodes.Completed;
        }

        if (executionLedgers.Any(ledger => IsState(ledger, 커뮤니티원장상태.진행중))
            || rootLedger is not null && !IsProvisional(rootLedger) && IsState(rootLedger, 커뮤니티원장상태.진행중))
        {
            return CommunityActionJourneyStageCodes.InProgress;
        }

        if (rootLedger is null)
        {
            return interestVote is null
                ? CommunityActionJourneyStageCodes.Conversation
                : CommunityActionJourneyStageCodes.Gathering;
        }

        if (party.IsReadyForRealLedgerReview && economics.ExecutionReady)
        {
            return CommunityActionJourneyStageCodes.Readiness;
        }

        if (party.RepresentedRequiredRoleSlotCount > 0)
        {
            return CommunityActionJourneyStageCodes.Party;
        }

        if (economics.HasPlan)
        {
            return CommunityActionJourneyStageCodes.Conditions;
        }

        return CommunityActionJourneyStageCodes.ProvisionalLedger;
    }

    private static CommunityActionJourneySalesSummaryResponse BuildSales(string? salesOfferJson)
    {
        if (string.IsNullOrWhiteSpace(salesOfferJson))
        {
            return new CommunityActionJourneySalesSummaryResponse();
        }

        try
        {
            var sales = JsonSerializer.Deserialize<PlatformCommunityPostSalesOfferResponse>(salesOfferJson, JsonOptions);
            return sales is null
                ? new CommunityActionJourneySalesSummaryResponse()
                : new CommunityActionJourneySalesSummaryResponse
                {
                    HasSalesOffer = true,
                    ProductTitle = sales.ProductTitle,
                    AvailableQuantity = sales.AvailableQuantity,
                    QuantityUnit = sales.QuantityUnit,
                    UnitPrice = sales.UnitPrice,
                    CurrencyCode = sales.CurrencyCode,
                    AllowsGroupPurchase = sales.AllowsGroupPurchase,
                    StatusCode = sales.Status
                };
        }
        catch (JsonException)
        {
            return new CommunityActionJourneySalesSummaryResponse();
        }
    }

    private static CommunityActionJourneyEconomicsSummaryResponse BuildEconomics(
        CollectiveProcurementPlanState? plan)
    {
        var revision = plan?.CalculationRevisions
            .OrderByDescending(item => item.CalculationRevision)
            .FirstOrDefault();
        if (plan is null || revision is null)
        {
            return new CommunityActionJourneyEconomicsSummaryResponse();
        }

        var assessment = revision.Assessment;
        var allAccepted = plan.Participants.Count > 0
                          && plan.Participants.All(participant =>
                              participant.AcceptedCalculationRevision == revision.CalculationRevision);
        var executionReady = allAccepted
                             && assessment.BenefitAgreementReady
                             && assessment.CurrentQuantityEconomicallyViable;
        var status = executionReady
            ? CollectiveProcurementPlanStatusCodes.ReadyForExecution
            : allAccepted
                ? CollectiveProcurementPlanStatusCodes.TargetAgreed
                : !assessment.CurrentQuantityEconomicallyViable
                    ? CollectiveProcurementPlanStatusCodes.CollectingDemand
                    : assessment.BenefitAgreementReady
                        ? CollectiveProcurementPlanStatusCodes.AwaitingAcceptance
                        : CollectiveProcurementPlanStatusCodes.ResolvingBenefitTerms;
        var scenario = assessment.RecommendedScenario
                       ?? assessment.CurrentPotentialScenario
                       ?? assessment.CurrentCommittedScenario;

        return new CommunityActionJourneyEconomicsSummaryResponse
        {
            HasPlan = true,
            PlanId = plan.PlanId,
            PlanRevision = plan.PlanRevision,
            StatusCode = status,
            CurrencyCode = assessment.CurrencyCode,
            QuantityUnit = assessment.QuantityUnit,
            CurrentCommittedQuantity = assessment.CurrentCommittedQuantity,
            MinimumOrderQuantity = assessment.MinimumOrderQuantity,
            MinimumViableQuantity = assessment.MinimumViableQuantity,
            RecommendedQuantity = assessment.RecommendedQuantity,
            EstimatedUnitLandedCost = scenario?.EstimatedUnitLandedCost,
            CurrentQuantityEconomicallyViable = assessment.CurrentQuantityEconomicallyViable,
            ExecutionReady = executionReady,
            UpdatedAtUtc = plan.UpdatedAtUtc,
            ContainsParticipantPrivateMinimums = false
        };
    }

    private static CommunityActionJourneyDiagramSummaryResponse BuildDiagram(
        커뮤니티원장Dto? rootLedger,
        IReadOnlyList<커뮤니티원장Dto> children)
    {
        var owner = new[] { rootLedger }
            .Concat(children)
            .FirstOrDefault(ledger => ledger?.다이어그램스냅샷 is not null);
        var diagram = owner?.다이어그램스냅샷;
        return diagram is null
            ? new CommunityActionJourneyDiagramSummaryResponse()
            : new CommunityActionJourneyDiagramSummaryResponse
            {
                IsAvailable = true,
                DiagramId = diagram.DiagramId,
                DiagramName = diagram.DiagramName,
                LedgerId = owner?.원장Id,
                NodeCount = diagram.Nodes.Count,
                EdgeCount = diagram.Edges.Count
            };
    }

    private static IReadOnlyList<CommunityActionJourneyLedgerResponse> BuildLedgers(
        커뮤니티원장Dto? rootLedger,
        IReadOnlyList<커뮤니티원장Dto> children)
    {
        var result = new List<CommunityActionJourneyLedgerResponse>();
        if (rootLedger is not null)
        {
            result.Add(ToLedger(rootLedger, "root"));
        }

        foreach (var child in children)
        {
            var relation = rootLedger?.포함원장목록.FirstOrDefault(reference => string.Equals(
                reference.원장Id,
                child.원장Id,
                StringComparison.OrdinalIgnoreCase));
            result.Add(ToLedger(child, relation?.관계유형 ?? relation?.역할 ?? "child"));
        }

        return result;
    }

    private static CommunityActionJourneyLedgerResponse ToLedger(커뮤니티원장Dto ledger, string relationCode)
        => new()
        {
            LedgerId = ledger.원장Id,
            LedgerTemplateKey = ledger.원장템플릿Key,
            Title = ledger.제목,
            State = ledger.상태,
            CurrentStageCode = ledger.현재단계Key ?? string.Empty,
            RelationCode = relationCode,
            IsProvisional = IsProvisional(ledger),
            UpdatedAtUtc = ToOffset(ledger.수정시각Utc)
        };

    private static IReadOnlyList<CommunityActionJourneyTimelineItemResponse> BuildTimeline(
        CommunityPostOpportunitySource source,
        CommunityVoteResponse? interestVote,
        커뮤니티원장Dto? rootLedger,
        IReadOnlyList<커뮤니티원장Dto> children,
        CollectiveProcurementPlanState? plan)
    {
        var items = new List<CommunityActionJourneyTimelineItemResponse>();
        if (source.CreatedAtUtc != default)
        {
            items.Add(new CommunityActionJourneyTimelineItemResponse
            {
                Code = "post-created",
                Title = "이야기가 시작됐어요",
                Detail = source.Title,
                OccurredAtUtc = ToOffset(source.CreatedAtUtc),
                IsCompleted = true
            });
        }

        if (interestVote is not null)
        {
            items.Add(new CommunityActionJourneyTimelineItemResponse
            {
                Code = "interest-opened",
                Title = "마음 모으기를 시작했어요",
                Detail = $"현재 {interestVote.TotalVoteCount}명이 관심을 표시했습니다.",
                OccurredAtUtc = ToOffset(interestVote.CreatedAtUtc),
                IsCompleted = true
            });
        }

        if (rootLedger is not null)
        {
            items.Add(new CommunityActionJourneyTimelineItemResponse
            {
                Code = "provisional-ledger-created",
                Title = "가원장으로 모인 뜻을 기록했어요",
                Detail = "아직 계약이나 업무 배정을 확정하지 않은 비구속 기록입니다.",
                OccurredAtUtc = ToOffset(rootLedger.생성시각Utc),
                IsCompleted = true,
                LedgerId = rootLedger.원장Id
            });
        }

        if (plan is not null)
        {
            items.Add(new CommunityActionJourneyTimelineItemResponse
            {
                Code = "economics-reviewed",
                Title = "수량과 경제성을 함께 검토했어요",
                Detail = "개인별 비공개 기준은 제외하고 집계 결과만 연결합니다.",
                OccurredAtUtc = plan.UpdatedAtUtc,
                IsCompleted = true
            });
        }

        items.AddRange(children.Select(ledger => new CommunityActionJourneyTimelineItemResponse
        {
            Code = $"ledger-{ledger.원장템플릿Key}",
            Title = ledger.제목,
            Detail = $"{ledger.원장템플릿Key} · {ledger.상태}",
            OccurredAtUtc = ToOffset(ledger.수정시각Utc),
            IsCompleted = IsState(ledger, 커뮤니티원장상태.완료),
            LedgerId = ledger.원장Id
        }));
        return items.OrderBy(item => item.OccurredAtUtc).ToArray();
    }

    private static bool IsProvisional(커뮤니티원장Dto ledger)
        => ledger.확장속성.TryGetValue(
               CommunityPostProvisionalLedgerPolicy.LedgerMaturityAttributeKey,
               out var maturity)
           && string.Equals(
               maturity,
               CommunityPostProvisionalLedgerPolicy.LedgerMaturityCode,
               StringComparison.OrdinalIgnoreCase);

    private static bool IsState(커뮤니티원장Dto ledger, string state)
        => string.Equals(ledger.상태, state, StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset ToOffset(DateTime value)
        => new(value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime());

    private static string StageLabel(string stage, bool english)
        => (stage, english) switch
        {
            (CommunityActionJourneyStageCodes.Conversation, true) => "Conversation",
            (CommunityActionJourneyStageCodes.Gathering, true) => "Gathering interest",
            (CommunityActionJourneyStageCodes.ProvisionalLedger, true) => "Provisional record",
            (CommunityActionJourneyStageCodes.Conditions, true) => "Reviewing conditions",
            (CommunityActionJourneyStageCodes.Party, true) => "Forming the team",
            (CommunityActionJourneyStageCodes.Readiness, true) => "Ready for execution review",
            (CommunityActionJourneyStageCodes.InProgress, true) => "In progress",
            (CommunityActionJourneyStageCodes.Completed, true) => "Completed",
            (CommunityActionJourneyStageCodes.Gathering, false) => "마음 모으는 중",
            (CommunityActionJourneyStageCodes.ProvisionalLedger, false) => "가원장 기록됨",
            (CommunityActionJourneyStageCodes.Conditions, false) => "조건 맞추는 중",
            (CommunityActionJourneyStageCodes.Party, false) => "함께할 사람 구성 중",
            (CommunityActionJourneyStageCodes.Readiness, false) => "실행 검토 준비됨",
            (CommunityActionJourneyStageCodes.InProgress, false) => "같이 하는 중",
            (CommunityActionJourneyStageCodes.Completed, false) => "함께 완료했어요",
            _ => "이야기 나누는 중"
        };
}
