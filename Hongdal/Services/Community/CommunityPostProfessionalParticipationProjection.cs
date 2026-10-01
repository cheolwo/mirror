using System.Text.Json;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Operations;

namespace Hongdal.Services.Community;

internal static class CommunityPostProfessionalParticipationProjection
{
    public static void EnsureProvisionalLedger(커뮤니티원장Dto ledger)
    {
        if (!string.Equals(
                ledger.원장템플릿Key,
                CommunityLedgerTemplateKeys.GroupPurchase,
                StringComparison.OrdinalIgnoreCase)
            || !ledger.확장속성.TryGetValue(
                CommunityPostProvisionalLedgerPolicy.LedgerMaturityAttributeKey,
                out var maturityCode)
            || !string.Equals(
                maturityCode,
                CommunityPostProvisionalLedgerPolicy.LedgerMaturityCode,
                StringComparison.OrdinalIgnoreCase)
            || !ledger.확장속성.TryGetValue(
                CommunityPostProvisionalLedgerPolicy.BindingEffectAttributeKey,
                out var bindingCode)
            || !string.Equals(
                bindingCode,
                CommunityPostProvisionalLedgerPolicy.NonBindingEffectCode,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("전문가 참여는 비구속적 가원장 단계에서만 가능합니다.");
        }
    }

    public static IReadOnlyList<string> ResolveRequiredRoles(커뮤니티원장Dto ledger)
    {
        var plannedRoles = CommunityPostPartyRoleCodes
            .ForPlan(
                ReadTradeDirectionCode(ledger),
                ReadTransportModeCodes(ledger),
                ledger.확장속성.GetValueOrDefault(
                    CommunityPostProvisionalLedgerPolicy.DestinationCountryAttributeKey))
            .Where(CommunityPostPartyRoleCodes.IsSpecialist)
            .ToArray();
        if (ledger.확장속성.TryGetValue(
                CommunityPostProvisionalLedgerPolicy.RequiredProfessionalRolesAttributeKey,
                out var serialized))
        {
            try
            {
                var stored = JsonSerializer.Deserialize<string[]>(serialized);
                if (stored is { Length: > 0 })
                {
                    return stored
                        .Where(CommunityPostPartyRoleCodes.IsSpecialist)
                        .Select(role => CommunityPostPartyRoleCodes.SpecialistRoles.First(candidate => string.Equals(
                            candidate,
                            role,
                            StringComparison.OrdinalIgnoreCase)))
                        .Concat(plannedRoles)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                }
            }
            catch (JsonException)
            {
                // Fall back to the intent policy for ledgers written before this metadata existed.
            }
        }

        return plannedRoles;
    }

    public static string ReadTradeDirectionCode(커뮤니티원장Dto ledger)
    {
        if (ledger.확장속성.TryGetValue(
                CommunityPostProvisionalLedgerPolicy.TradeDirectionAttributeKey,
                out var stored)
            && CommunityTradeDirectionCodes.IsSupported(stored))
        {
            return CommunityTradeDirectionCodes.All.First(code => string.Equals(
                code,
                stored,
                StringComparison.OrdinalIgnoreCase));
        }

        ledger.확장속성.TryGetValue(
            CommunityPostProvisionalLedgerPolicy.CollectiveIntentTypeAttributeKey,
            out var intentTypeCode);
        return CommunityTradeDirectionCodes.ExpectedForIntent(intentTypeCode ?? string.Empty);
    }

    public static IReadOnlyList<string> ReadTransportModeCodes(커뮤니티원장Dto ledger)
    {
        if (!ledger.확장속성.TryGetValue(
                CommunityPostProvisionalLedgerPolicy.TransportModesAttributeKey,
                out var serialized)
            || string.IsNullOrWhiteSpace(serialized))
        {
            return [];
        }

        try
        {
            return CommunityTransportModeCodes.NormalizeMany(
                JsonSerializer.Deserialize<string[]>(serialized));
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static IReadOnlyDictionary<string, int> ReadInterestRoleCounts(커뮤니티원장Dto ledger)
    {
        if (!ledger.확장속성.TryGetValue("InterestRoleCountsJson", out var serialized)
            || string.IsNullOrWhiteSpace(serialized))
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var stored = JsonSerializer.Deserialize<Dictionary<string, int>>(serialized);
            return stored is null
                ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, int>(stored, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static IReadOnlyList<CommunityPartyRoleAssignment> ReadAssignments(커뮤니티원장Dto ledger)
    {
        if (!ledger.확장속성.TryGetValue(
                CommunityPostProvisionalLedgerPolicy.ConfirmedPartyRoleAssignmentsAttributeKey,
                out var serialized)
            || string.IsNullOrWhiteSpace(serialized))
        {
            return [];
        }

        try
        {
            return (JsonSerializer.Deserialize<CommunityPartyRoleAssignment[]>(serialized) ?? [])
                .Where(assignment => !string.IsNullOrWhiteSpace(assignment.UserId)
                                     && CommunityPostPartyRoleCodes.IsSupported(assignment.RoleCode))
                .GroupBy(
                    assignment => $"{assignment.UserId.Trim()}:{assignment.RoleCode.Trim()}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string SerializeAssignments(IReadOnlyList<CommunityPartyRoleAssignment> assignments)
        => JsonSerializer.Serialize(assignments);

    public static 커뮤니티원장블록Dto BuildProfessionalBlock(
        IReadOnlyList<string> requiredRoles,
        IReadOnlyList<CommunityPartyRoleAssignment> assignments)
    {
        var joinedRoleCounts = assignments
            .GroupBy(assignment => assignment.RoleCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        return new 커뮤니티원장블록Dto
        {
            BlockId = CommunityPostProvisionalLedgerPolicy.ProfessionalParticipationBlockId,
            BlockType = CommunityLedgerBlockTypes.Generic,
            Title = "거래 참여팀 역할 구성",
            State = assignments.Count == 0 ? "역할 참여 요청" : "참여팀 구성중",
            Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["RequiredProfessionalRolesJson"] = JsonSerializer.Serialize(requiredRoles),
                ["ConfirmedPartyRoleCountsJson"] = JsonSerializer.Serialize(joinedRoleCounts),
                ["ConfirmedPartyRoleParticipantCount"] = assignments
                    .Select(assignment => assignment.UserId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count()
                    .ToString(),
                ["PlatformConfirmedProfessionalParticipantCount"] = assignments
                    .Where(assignment => CommunityPostPartyRoleCodes.IsSpecialist(assignment.RoleCode))
                    .Select(assignment => assignment.UserId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count()
                    .ToString(),
                ["VerificationScopeCode"] = "RoleDependent",
                ["ParticipationNotice"] = "역할 참여는 자발적이고 비구속적이며 주문, 계약, 업무 배정 또는 운송 주선을 확정하지 않습니다. 플랫폼 역할 확인은 관할기관 면허·등록 확인을 대신하지 않습니다."
            }
        };
    }

    public static CommunityPostProfessionalParticipationResponse BuildResponse(
        커뮤니티원장Dto? ledger,
        long postId,
        string language)
    {
        if (ledger is null)
        {
            return new CommunityPostProfessionalParticipationResponse();
        }

        try
        {
            EnsureProvisionalLedger(ledger);
        }
        catch (InvalidOperationException)
        {
            return new CommunityPostProfessionalParticipationResponse();
        }

        var assignments = ReadAssignments(ledger);
        var professionalAssignments = assignments
            .Where(assignment => CommunityPostPartyRoleCodes.IsSpecialist(assignment.RoleCode))
            .ToArray();
        var requiredRoles = ResolveRequiredRoles(ledger);
        var momentumCode = ResolveMomentumCode(ledger, assignments);

        return new CommunityPostProfessionalParticipationResponse
        {
            IsAvailable = true,
            PlatformPromotionActive = true,
            MomentumCode = momentumCode,
            MomentumMessage = MomentumMessage(momentumCode, language),
            PlatformConfirmedRoleParticipantCount = professionalAssignments
                .Select(assignment => assignment.UserId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            JoinEndpoint = $"/api/v1/community/posts/{postId}/opportunities/participation/professionals",
            RoleOpenings = requiredRoles.Select(roleCode => new CommunityPostProfessionalRoleOpeningResponse
            {
                RoleCode = roleCode,
                Label = RoleLabel(roleCode, language == CommunityDisplayLanguageCodes.English),
                Summary = RoleSummary(roleCode, language == CommunityDisplayLanguageCodes.English),
                VerificationRequirementCode = VerificationRequirementCode(roleCode),
                ExternalCredentialVerificationRequired = RequiresExternalCredential(roleCode),
                ExternalCredentialVerified = false,
                PlatformConfirmedParticipantCount = professionalAssignments.Count(assignment => string.Equals(
                    assignment.RoleCode,
                    roleCode,
                    StringComparison.OrdinalIgnoreCase)),
                CandidateDirectoryEndpoint = CandidateDirectoryEndpoint(roleCode),
                CandidateDirectoryIsResearchOnly = !string.IsNullOrWhiteSpace(
                    CandidateDirectoryEndpoint(roleCode)),
                RequiresSeparateAuthorityAndContractVerification = true
            }).ToArray()
        };
    }

    public static CommunityPostPartyFormationResponse BuildPartyFormationResponse(
        커뮤니티원장Dto? ledger,
        string language)
        => ledger is null
            ? new CommunityPostPartyFormationResponse()
            : BuildPartyFormationResponse(ledger, language, ReadAssignments(ledger));

    public static string ResolveMomentumCode(
        커뮤니티원장Dto ledger,
        IReadOnlyList<CommunityPartyRoleAssignment> assignments)
    {
        var formation = BuildPartyFormationResponse(
            ledger,
            CommunityDisplayLanguageCodes.Korean,
            assignments);
        if (formation.IsReadyForRealLedgerReview)
        {
            return CommunityPostMomentumCodes.ReadyForRealLedgerReview;
        }

        return assignments.Count > 0
            ? CommunityPostMomentumCodes.PartyForming
            : CommunityPostMomentumCodes.SeekingParty;
    }

    public static string ReadinessMessage(커뮤니티원장Dto ledger, string language)
        => BuildPartyFormationResponse(ledger, language).ReadinessMessage;

    private static CommunityPostPartyFormationResponse BuildPartyFormationResponse(
        커뮤니티원장Dto ledger,
        string language,
        IReadOnlyList<CommunityPartyRoleAssignment> assignments)
    {
        try
        {
            EnsureProvisionalLedger(ledger);
        }
        catch (InvalidOperationException)
        {
            return new CommunityPostPartyFormationResponse();
        }

        var english = language == CommunityDisplayLanguageCodes.English;
        var tradeDirectionCode = ReadTradeDirectionCode(ledger);
        var transportModeCodes = ReadTransportModeCodes(ledger);
        var originCountryCode = ledger.확장속성.GetValueOrDefault(
            CommunityPostProvisionalLedgerPolicy.OriginCountryAttributeKey,
            string.Empty);
        var destinationCountryCode = ledger.확장속성.GetValueOrDefault(
            CommunityPostProvisionalLedgerPolicy.DestinationCountryAttributeKey,
            string.Empty);
        var interestCounts = ReadInterestRoleCounts(ledger);
        var definitions = BuildPartyRoleDefinitions(
            tradeDirectionCode,
            transportModeCodes,
            destinationCountryCode);
        var slots = definitions.Select(definition =>
        {
            var interestCount = ResolveInterestCount(
                definition.RoleCode,
                interestCounts,
                transportModeCodes);
            var confirmedCount = assignments.Count(assignment => string.Equals(
                assignment.RoleCode,
                definition.RoleCode,
                StringComparison.OrdinalIgnoreCase));
            return new CommunityPostPartyRoleSlotResponse
            {
                RoleCode = definition.RoleCode,
                CategoryCode = definition.CategoryCode,
                Label = RoleLabel(definition.RoleCode, english),
                Summary = RoleSummary(definition.RoleCode, english),
                IsRequired = definition.IsRequired,
                IsRecommended = definition.IsRecommended,
                TransportModeCode = definition.TransportModeCode,
                VerificationRequirementCode = VerificationRequirementCode(definition.RoleCode),
                ExternalCredentialVerificationRequired = RequiresExternalCredential(definition.RoleCode),
                ExternalCredentialVerified = false,
                InterestCount = interestCount,
                ConfirmedParticipantCount = confirmedCount,
                StateCode = confirmedCount > 0
                    ? CommunityPartyRoleSlotStateCodes.RoleAccepted
                    : interestCount > 0
                        ? CommunityPartyRoleSlotStateCodes.InterestExpressed
                        : CommunityPartyRoleSlotStateCodes.Open,
                CandidateDirectoryEndpoint = CandidateDirectoryEndpoint(
                    definition.RoleCode),
                CandidateDirectoryIsResearchOnly = !string.IsNullOrWhiteSpace(
                    CandidateDirectoryEndpoint(definition.RoleCode)),
                RequiresSeparateAuthorityAndContractVerification =
                    RequiresExternalCredential(definition.RoleCode)
            };
        }).ToArray();
        var requiredSlots = slots.Where(slot => slot.IsRequired).ToArray();
        var representedCount = requiredSlots.Count(slot => slot.IsRepresented);
        var routeNeedsConfirmation = TradeRouteNeedsConfirmation(
            tradeDirectionCode,
            originCountryCode,
            destinationCountryCode,
            transportModeCodes);
        var ready = requiredSlots.Length > 0
                    && representedCount == requiredSlots.Length
                    && !routeNeedsConfirmation;

        return new CommunityPostPartyFormationResponse
        {
            IsAvailable = true,
            TradeDirectionCode = tradeDirectionCode,
            OriginCountryCode = originCountryCode,
            DestinationCountryCode = destinationCountryCode,
            TransportModeCodes = transportModeCodes,
            TradeRouteNeedsConfirmation = routeNeedsConfirmation,
            RequiredRoleSlotCount = requiredSlots.Length,
            RepresentedRequiredRoleSlotCount = representedCount,
            IsReadyForRealLedgerReview = ready,
            ReadinessMessage = BuildReadinessMessage(
                ready,
                routeNeedsConfirmation,
                representedCount,
                requiredSlots.Length,
                english),
            RoleSlots = slots
        };
    }

    public static string RoleLabel(string roleCode, bool english)
        => (roleCode, english) switch
        {
            (CommunityPostPartyRoleCodes.Buyer, true) => "Buyer",
            (CommunityPostPartyRoleCodes.Buyer, false) => "구매자",
            (CommunityPostPartyRoleCodes.Seller, true) => "Seller",
            (CommunityPostPartyRoleCodes.Seller, false) => "판매자",
            (CommunityPostPartyRoleCodes.Importer, true) => "Responsible importer",
            (CommunityPostPartyRoleCodes.Importer, false) => "수입 책임 당사자",
            (CommunityPostPartyRoleCodes.Exporter, true) => "Responsible exporter",
            (CommunityPostPartyRoleCodes.Exporter, false) => "수출 책임 당사자",
            (CommunityPostPartyRoleCodes.ImportCustomsBroker, true) => "Import customs professional",
            (CommunityPostPartyRoleCodes.ImportCustomsBroker, false) => "수입 통관 관세사",
            (CommunityPostPartyRoleCodes.ExportCustomsBroker, true) => "Export customs professional",
            (CommunityPostPartyRoleCodes.ExportCustomsBroker, false) => "수출 통관 관세사",
            (CommunityPostPartyRoleCodes.OceanFreightForwarder, true) => "Ocean freight forwarder",
            (CommunityPostPartyRoleCodes.OceanFreightForwarder, false) => "해상 운송 주선업자",
            (CommunityPostPartyRoleCodes.AirFreightForwarder, true) => "Air freight forwarder",
            (CommunityPostPartyRoleCodes.AirFreightForwarder, false) => "항공 화물 주선업자",
            (CommunityPostPartyRoleCodes.RoadFreightBroker, true) => "Road freight broker",
            (CommunityPostPartyRoleCodes.RoadFreightBroker, false) => "육상 화물 운송 주선업자",
            (CommunityPostPartyRoleCodes.MultimodalCoordinator, true) => "Multimodal logistics coordinator",
            (CommunityPostPartyRoleCodes.MultimodalCoordinator, false) => "복합운송 물류 주선업자",
            (CommunityPostPartyRoleCodes.OceanCarrier, true) => "Ocean carrier",
            (CommunityPostPartyRoleCodes.OceanCarrier, false) => "해상 운송사",
            (CommunityPostPartyRoleCodes.AirCarrier, true) => "Air carrier",
            (CommunityPostPartyRoleCodes.AirCarrier, false) => "항공 운송사",
            (CommunityPostPartyRoleCodes.RoadCarrier, true) => "Road carrier",
            (CommunityPostPartyRoleCodes.RoadCarrier, false) => "육상 운송사·기사",
            (CommunityPostPartyRoleCodes.RailCarrier, true) => "Rail carrier",
            (CommunityPostPartyRoleCodes.RailCarrier, false) => "철도 운송사",
            (CommunityPostPartyRoleCodes.WarehouseOperator, true) => "Warehouse operator",
            (CommunityPostPartyRoleCodes.WarehouseOperator, false) => "창고 운영자",
            (CommunityPostPartyRoleCodes.CustomsControlledFacilityOperator, true) =>
                "Customs-controlled facility operator",
            (CommunityPostPartyRoleCodes.CustomsControlledFacilityOperator, false) =>
                "보세창고·FTZ 운영자",
            (CommunityPostPartyRoleCodes.InBondCarrier, true) => "In-bond carrier",
            (CommunityPostPartyRoleCodes.InBondCarrier, false) => "통관 전 보세운송사",
            (CommunityPostPartyRoleCodes.DomesticFulfillmentOperator, true) =>
                "Domestic fulfillment operator",
            (CommunityPostPartyRoleCodes.DomesticFulfillmentOperator, false) =>
                "미국 내 풀필먼트 운영자",
            (CommunityPostPartyRoleCodes.ParticipantAddressDeliveryProvider, true) =>
                "Participant-address delivery provider",
            (CommunityPostPartyRoleCodes.ParticipantAddressDeliveryProvider, false) =>
                "참여자 주소 배송 사업자",
            _ => roleCode
        };

    private static string RoleSummary(string roleCode, bool english)
        => (roleCode, english) switch
        {
            (CommunityPostPartyRoleCodes.Buyer, true) => "Express purchase interest without creating an order or payment obligation.",
            (CommunityPostPartyRoleCodes.Buyer, false) => "주문·결제 의무 없이 구매 관심과 필요한 수량을 검토합니다.",
            (CommunityPostPartyRoleCodes.Seller, true) => "Review supply quantity, price range, and lead time without accepting an order.",
            (CommunityPostPartyRoleCodes.Seller, false) => "주문 수락 전 공급 수량·가격 범위·납기를 검토합니다.",
            (CommunityPostPartyRoleCodes.Importer, true) => "A transaction party must later accept the importer responsibility required by the destination jurisdiction.",
            (CommunityPostPartyRoleCodes.Importer, false) => "도착국 법령상 수입 책임을 맡을 당사자를 별도 계약 단계에서 확정합니다.",
            (CommunityPostPartyRoleCodes.Exporter, true) => "A transaction party must later accept the exporter responsibility required by the origin jurisdiction.",
            (CommunityPostPartyRoleCodes.Exporter, false) => "출발국 법령상 수출 책임을 맡을 당사자를 별도 계약 단계에서 확정합니다.",
            (CommunityPostPartyRoleCodes.ImportCustomsBroker, true) => "Review import customs questions before a separate engagement; platform profile approval is not proof of every jurisdictional license.",
            (CommunityPostPartyRoleCodes.ImportCustomsBroker, false) => "별도 수임 전 수입 통관 쟁점을 검토하며, 플랫폼 프로필 확인만으로 모든 관할 면허를 증명하지 않습니다.",
            (CommunityPostPartyRoleCodes.ExportCustomsBroker, true) => "Review export filing questions before a separate engagement; platform profile approval is not proof of every jurisdictional license.",
            (CommunityPostPartyRoleCodes.ExportCustomsBroker, false) => "별도 수임 전 수출 신고 쟁점을 검토하며, 플랫폼 프로필 확인만으로 모든 관할 면허를 증명하지 않습니다.",
            (CommunityPostPartyRoleCodes.OceanFreightForwarder, true) => "Review ocean booking and documents without the platform arranging carriage; required authority depends on jurisdiction.",
            (CommunityPostPartyRoleCodes.OceanFreightForwarder, false) => "플랫폼이 운송을 주선하지 않는 상태에서 해상 예약·서류 조건을 검토하며 관할 등록은 별도로 확인합니다.",
            (CommunityPostPartyRoleCodes.AirFreightForwarder, true) => "Review air cargo handling and booking conditions under separately verified authority.",
            (CommunityPostPartyRoleCodes.AirFreightForwarder, false) => "별도로 확인된 권한 범위에서 항공 화물 취급·예약 조건을 검토합니다.",
            (CommunityPostPartyRoleCodes.RoadFreightBroker, true) => "A separately authorized broker may offer to arrange road carriage; the platform does not select a carrier or set a dispatch.",
            (CommunityPostPartyRoleCodes.RoadFreightBroker, false) => "별도 허가·등록 주선업자가 육상 운송 조건을 제안하며 플랫폼은 운송사 선택이나 배차를 결정하지 않습니다.",
            (CommunityPostPartyRoleCodes.MultimodalCoordinator, true) => "Review handoffs across modes under the registrations required by each jurisdiction.",
            (CommunityPostPartyRoleCodes.MultimodalCoordinator, false) => "관할별 등록 범위 안에서 복수 운송수단의 인계 조건을 검토합니다.",
            (CommunityPostPartyRoleCodes.OceanCarrier, true) => "Review ocean carriage capacity without accepting a booking.",
            (CommunityPostPartyRoleCodes.OceanCarrier, false) => "예약 수락 전 해상 운송 가능 용량과 조건을 검토합니다.",
            (CommunityPostPartyRoleCodes.AirCarrier, true) => "Review air carriage capacity without accepting a booking.",
            (CommunityPostPartyRoleCodes.AirCarrier, false) => "예약 수락 전 항공 운송 가능 용량과 조건을 검토합니다.",
            (CommunityPostPartyRoleCodes.RoadCarrier, true) => "Review feasible road carriage without accepting a dispatch.",
            (CommunityPostPartyRoleCodes.RoadCarrier, false) => "배차 수락 전 육상 운송 가능 조건을 검토합니다.",
            (CommunityPostPartyRoleCodes.RailCarrier, true) => "Review rail carriage capacity without accepting a booking.",
            (CommunityPostPartyRoleCodes.RailCarrier, false) => "예약 수락 전 철도 운송 가능 용량과 조건을 검토합니다.",
            (CommunityPostPartyRoleCodes.WarehouseOperator, true) => "Review receiving, storage, and outbound feasibility without accepting a service order.",
            (CommunityPostPartyRoleCodes.WarehouseOperator, false) => "서비스 주문 수락 전 입고·보관·출고 가능 조건을 검토합니다.",
            (CommunityPostPartyRoleCodes.CustomsControlledFacilityOperator, true) =>
                "Review bonded warehouse or FTZ storage without confirming current facility authorization, availability, or a service contract.",
            (CommunityPostPartyRoleCodes.CustomsControlledFacilityOperator, false) =>
                "현재 시설 승인·가용 공간·계약을 확정하지 않은 상태에서 보세창고 또는 FTZ 보관 가능성을 검토합니다.",
            (CommunityPostPartyRoleCodes.InBondCarrier, true) =>
                "Review pre-release in-bond movement subject to ACE filing, carrier bond, route, and a separate carriage contract.",
            (CommunityPostPartyRoleCodes.InBondCarrier, false) =>
                "ACE 신고·carrier bond·이동 경로와 별도 운송계약 확인을 전제로 통관 전 보세운송 가능성을 검토합니다.",
            (CommunityPostPartyRoleCodes.DomesticFulfillmentOperator, true) =>
                "Review released-cargo receiving, break-pack, kitting, storage, and parcel tender without accepting a fulfillment order.",
            (CommunityPostPartyRoleCodes.DomesticFulfillmentOperator, false) =>
                "서비스 주문 수락 전 반출 완료 화물의 입고·소분·kitting·보관·parcel 인계 가능성을 검토합니다.",
            (CommunityPostPartyRoleCodes.ParticipantAddressDeliveryProvider, true) =>
                "Review delivery from fulfillment to participant addresses without accepting shipments or confirming coverage.",
            (CommunityPostPartyRoleCodes.ParticipantAddressDeliveryProvider, false) =>
                "배송 접수나 권역을 확정하지 않은 상태에서 풀필먼트 창고부터 참여자 주소까지의 배송 가능성을 검토합니다.",
            _ => string.Empty
        };

    private static string VerificationRequirementCode(string roleCode)
        => roleCode switch
        {
            CommunityPostPartyRoleCodes.Buyer
                or CommunityPostPartyRoleCodes.Seller
                or CommunityPostPartyRoleCodes.Importer
                or CommunityPostPartyRoleCodes.Exporter
                => CommunityPartyRoleVerificationRequirementCodes.ExplicitPartyAcceptance,
            CommunityPostPartyRoleCodes.ImportCustomsBroker
                or CommunityPostPartyRoleCodes.ExportCustomsBroker
                or CommunityPostPartyRoleCodes.OceanFreightForwarder
                or CommunityPostPartyRoleCodes.AirFreightForwarder
                or CommunityPostPartyRoleCodes.RoadFreightBroker
                or CommunityPostPartyRoleCodes.MultimodalCoordinator
                => CommunityPartyRoleVerificationRequirementCodes.JurisdictionLicenseOrRegistration,
            CommunityPostPartyRoleCodes.OceanCarrier
                or CommunityPostPartyRoleCodes.AirCarrier
                or CommunityPostPartyRoleCodes.RoadCarrier
                or CommunityPostPartyRoleCodes.RailCarrier
                => CommunityPartyRoleVerificationRequirementCodes.CarrierOperatingAuthority,
            CommunityPostPartyRoleCodes.CustomsControlledFacilityOperator
                => CommunityPartyRoleVerificationRequirementCodes
                    .CustomsFacilityAuthorization,
            CommunityPostPartyRoleCodes.InBondCarrier
                => CommunityPartyRoleVerificationRequirementCodes
                    .BondedCarrierOperatingAuthority,
            CommunityPostPartyRoleCodes.DomesticFulfillmentOperator
                => CommunityPartyRoleVerificationRequirementCodes
                    .FacilityCapabilityAndContract,
            CommunityPostPartyRoleCodes.ParticipantAddressDeliveryProvider
                => CommunityPartyRoleVerificationRequirementCodes
                    .CarrierOperatingAuthority,
            _ => CommunityPartyRoleVerificationRequirementCodes.PlatformProfile
        };

    private static bool RequiresExternalCredential(string roleCode)
        => VerificationRequirementCode(roleCode) is
            CommunityPartyRoleVerificationRequirementCodes.JurisdictionLicenseOrRegistration
            or CommunityPartyRoleVerificationRequirementCodes.CarrierOperatingAuthority
            or CommunityPartyRoleVerificationRequirementCodes.CustomsFacilityAuthorization
            or CommunityPartyRoleVerificationRequirementCodes.BondedCarrierOperatingAuthority
            or CommunityPartyRoleVerificationRequirementCodes.FacilityCapabilityAndContract;

    private static IReadOnlyList<PartyRoleDefinition> BuildPartyRoleDefinitions(
        string tradeDirectionCode,
        IReadOnlyList<string> transportModeCodes,
        string destinationCountryCode)
    {
        var roles = CommunityPostPartyRoleCodes.ForPlan(
            tradeDirectionCode,
            transportModeCodes,
            destinationCountryCode);
        return roles
            .Select(roleCode => new PartyRoleDefinition(
                roleCode,
                CategoryCode(roleCode),
                IsRequiredRole(roleCode, tradeDirectionCode),
                IsRecommendedRole(roleCode),
                TransportModeCode(roleCode)))
            .OrderBy(definition => CategoryOrder(definition.CategoryCode))
            .ThenBy(definition => definition.RoleCode, StringComparer.Ordinal)
            .ToArray();
    }

    private static string CategoryCode(string roleCode)
        => roleCode switch
        {
            CommunityPostPartyRoleCodes.Buyer
                or CommunityPostPartyRoleCodes.Seller
                or CommunityPostPartyRoleCodes.Importer
                or CommunityPostPartyRoleCodes.Exporter
                => CommunityPartyRoleCategoryCodes.CommercialParty,
            CommunityPostPartyRoleCodes.ImportCustomsBroker
                or CommunityPostPartyRoleCodes.ExportCustomsBroker
                => CommunityPartyRoleCategoryCodes.CustomsAndDocumentation,
            CommunityPostPartyRoleCodes.OceanFreightForwarder
                or CommunityPostPartyRoleCodes.AirFreightForwarder
                or CommunityPostPartyRoleCodes.RoadFreightBroker
                or CommunityPostPartyRoleCodes.MultimodalCoordinator
                => CommunityPartyRoleCategoryCodes.TransportationIntermediary,
            CommunityPostPartyRoleCodes.OceanCarrier
                or CommunityPostPartyRoleCodes.AirCarrier
                or CommunityPostPartyRoleCodes.RoadCarrier
                or CommunityPostPartyRoleCodes.RailCarrier
                or CommunityPostPartyRoleCodes.InBondCarrier
                or CommunityPostPartyRoleCodes.ParticipantAddressDeliveryProvider
                => CommunityPartyRoleCategoryCodes.Carrier,
            _ => CommunityPartyRoleCategoryCodes.Fulfillment
        };

    private static bool IsRequiredRole(string roleCode, string tradeDirectionCode)
        => roleCode is CommunityPostPartyRoleCodes.Buyer or CommunityPostPartyRoleCodes.Seller
           || !string.Equals(
                  tradeDirectionCode,
                  CommunityTradeDirectionCodes.Domestic,
                  StringComparison.OrdinalIgnoreCase)
              && roleCode is CommunityPostPartyRoleCodes.Importer or CommunityPostPartyRoleCodes.Exporter
           || roleCode is CommunityPostPartyRoleCodes.OceanCarrier
               or CommunityPostPartyRoleCodes.AirCarrier
               or CommunityPostPartyRoleCodes.RoadCarrier
               or CommunityPostPartyRoleCodes.RailCarrier
               or CommunityPostPartyRoleCodes.MultimodalCoordinator;

    private static bool IsRecommendedRole(string roleCode)
        => roleCode is CommunityPostPartyRoleCodes.ImportCustomsBroker
            or CommunityPostPartyRoleCodes.ExportCustomsBroker
            or CommunityPostPartyRoleCodes.OceanFreightForwarder
            or CommunityPostPartyRoleCodes.AirFreightForwarder
            or CommunityPostPartyRoleCodes.RoadFreightBroker
            or CommunityPostPartyRoleCodes.WarehouseOperator
            or CommunityPostPartyRoleCodes.CustomsControlledFacilityOperator
            or CommunityPostPartyRoleCodes.InBondCarrier
            or CommunityPostPartyRoleCodes.DomesticFulfillmentOperator
            or CommunityPostPartyRoleCodes.ParticipantAddressDeliveryProvider;

    private static string? TransportModeCode(string roleCode)
        => roleCode switch
        {
            CommunityPostPartyRoleCodes.OceanFreightForwarder or CommunityPostPartyRoleCodes.OceanCarrier
                => CommunityTransportModeCodes.Ocean,
            CommunityPostPartyRoleCodes.AirFreightForwarder or CommunityPostPartyRoleCodes.AirCarrier
                => CommunityTransportModeCodes.Air,
            CommunityPostPartyRoleCodes.RoadFreightBroker or CommunityPostPartyRoleCodes.RoadCarrier
                => CommunityTransportModeCodes.Road,
            CommunityPostPartyRoleCodes.ParticipantAddressDeliveryProvider
                => CommunityTransportModeCodes.Road,
            CommunityPostPartyRoleCodes.RailCarrier => CommunityTransportModeCodes.Rail,
            CommunityPostPartyRoleCodes.MultimodalCoordinator => CommunityTransportModeCodes.Multimodal,
            _ => null
        };

    private static int ResolveInterestCount(
        string roleCode,
        IReadOnlyDictionary<string, int> interestCounts,
        IReadOnlyList<string> transportModeCodes)
    {
        var sourceRoleCode = roleCode switch
        {
            CommunityPostPartyRoleCodes.Buyer => CommunityPostParticipationRoleCodes.Buyer,
            CommunityPostPartyRoleCodes.Seller => CommunityPostParticipationRoleCodes.Supplier,
            CommunityPostPartyRoleCodes.ImportCustomsBroker or CommunityPostPartyRoleCodes.ExportCustomsBroker
                => CommunityPostParticipationRoleCodes.CustomsBroker,
            CommunityPostPartyRoleCodes.OceanFreightForwarder
                or CommunityPostPartyRoleCodes.AirFreightForwarder
                or CommunityPostPartyRoleCodes.RoadFreightBroker
                or CommunityPostPartyRoleCodes.MultimodalCoordinator
                => transportModeCodes.Count == 1 ? CommunityPostParticipationRoleCodes.FreightBroker : string.Empty,
            CommunityPostPartyRoleCodes.OceanCarrier
                or CommunityPostPartyRoleCodes.AirCarrier
                or CommunityPostPartyRoleCodes.RoadCarrier
                or CommunityPostPartyRoleCodes.RailCarrier
                => transportModeCodes.Count == 1 ? CommunityPostParticipationRoleCodes.Carrier : string.Empty,
            CommunityPostPartyRoleCodes.WarehouseOperator => CommunityPostParticipationRoleCodes.WarehouseOperator,
            CommunityPostPartyRoleCodes.CustomsControlledFacilityOperator
                or CommunityPostPartyRoleCodes.DomesticFulfillmentOperator
                => CommunityPostParticipationRoleCodes.WarehouseOperator,
            CommunityPostPartyRoleCodes.InBondCarrier
                or CommunityPostPartyRoleCodes.ParticipantAddressDeliveryProvider
                => CommunityPostParticipationRoleCodes.Carrier,
            _ => string.Empty
        };
        return string.IsNullOrWhiteSpace(sourceRoleCode)
            ? 0
            : Math.Max(0, interestCounts.GetValueOrDefault(sourceRoleCode));
    }

    private static string CandidateDirectoryEndpoint(string roleCode)
    {
        var stageCode = roleCode switch
        {
            CommunityPostPartyRoleCodes.CustomsControlledFacilityOperator
                => BondedToDoorLogisticsStageCodes.CustomsControlledStorage,
            CommunityPostPartyRoleCodes.InBondCarrier
                => BondedToDoorLogisticsStageCodes.InBondTransportation,
            CommunityPostPartyRoleCodes.DomesticFulfillmentOperator
                => BondedToDoorLogisticsStageCodes.FulfillmentWarehouseInbound,
            CommunityPostPartyRoleCodes.ParticipantAddressDeliveryProvider
                => BondedToDoorLogisticsStageCodes
                    .ParticipantAddressFinalMileDelivery,
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(stageCode)
            ? string.Empty
            : $"/api/v1/operations/third-party-logistics/providers/bonded-to-door?stageCode={stageCode}";
    }

    private static bool TradeRouteNeedsConfirmation(
        string tradeDirectionCode,
        string originCountryCode,
        string destinationCountryCode,
        IReadOnlyList<string> transportModeCodes)
    {
        var crossBorder = !string.Equals(
            tradeDirectionCode,
            CommunityTradeDirectionCodes.Domestic,
            StringComparison.OrdinalIgnoreCase);
        if (crossBorder)
        {
            return string.IsNullOrWhiteSpace(originCountryCode)
                   || string.IsNullOrWhiteSpace(destinationCountryCode)
                   || string.Equals(originCountryCode, destinationCountryCode, StringComparison.OrdinalIgnoreCase)
                   || transportModeCodes.Count == 0;
        }

        var oneCountryMissing = string.IsNullOrWhiteSpace(originCountryCode)
                                != string.IsNullOrWhiteSpace(destinationCountryCode);
        return oneCountryMissing
               || !string.IsNullOrWhiteSpace(originCountryCode)
               && !string.Equals(originCountryCode, destinationCountryCode, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildReadinessMessage(
        bool ready,
        bool routeNeedsConfirmation,
        int representedCount,
        int requiredCount,
        bool english)
    {
        if (ready)
        {
            return english
                ? $"All {requiredCount} required role slots have explicit participants. A real-ledger review may begin, but no order, contract, assignment, or brokerage is confirmed."
                : $"필수 역할 {requiredCount}/{requiredCount}에 참여자가 명시적으로 역할을 수락했습니다. 실원장 전환 검토를 시작할 수 있지만 주문·계약·업무 배정·운송 주선은 확정되지 않았습니다.";
        }

        if (routeNeedsConfirmation)
        {
            return english
                ? $"Required roles explicitly accepted: {representedCount}/{requiredCount}. Confirm origin, destination, and transport modes before real-ledger review."
                : $"필수 역할 {representedCount}/{requiredCount}이 명시적으로 수락되었습니다. 실원장 전환 검토 전에 출발국·도착국·운송수단을 확인해야 합니다.";
        }

        return english
            ? $"Required roles explicitly accepted: {representedCount}/{requiredCount}. Open roles still need voluntary participants."
            : $"필수 역할 {representedCount}/{requiredCount}이 명시적으로 수락되었습니다. 빈 역할에는 자발적 참여가 더 필요합니다.";
    }

    private static int CategoryOrder(string categoryCode)
        => categoryCode switch
        {
            CommunityPartyRoleCategoryCodes.CommercialParty => 0,
            CommunityPartyRoleCategoryCodes.CustomsAndDocumentation => 1,
            CommunityPartyRoleCategoryCodes.TransportationIntermediary => 2,
            CommunityPartyRoleCategoryCodes.Carrier => 3,
            _ => 4
        };

    public static string MomentumMessage(string momentumCode, string language)
    {
        var english = language == CommunityDisplayLanguageCodes.English;
        return momentumCode switch
        {
            CommunityPostMomentumCodes.ReadyForRealLedgerReview => english
                ? "The required party roles were explicitly accepted and the trade route is specified. A real-ledger review may begin, but no transaction is confirmed."
                : "필수 거래 역할이 명시적으로 수락되고 경로가 구체화되어 실원장 전환 검토를 시작할 수 있습니다. 아직 거래는 확정되지 않았습니다.",
            CommunityPostMomentumCodes.PartyForming => english
                ? "A platform-confirmed role participant joined the provisional ledger. External licenses and final authority still require separate verification."
                : "플랫폼에서 역할이 확인된 참여자가 가원장에 합류했습니다. 외부 면허·등록과 최종 권한은 별도로 확인해야 합니다.",
            _ => english
                ? "Community interest formed a provisional ledger. Transaction parties and qualified specialists may join voluntarily."
                : "사용자 관심이 가원장으로 모였습니다. 거래 당사자와 자격을 갖춘 업무 참여자의 자발적 참여를 기다립니다."
        };
    }

    private sealed record PartyRoleDefinition(
        string RoleCode,
        string CategoryCode,
        bool IsRequired,
        bool IsRecommended,
        string? TransportModeCode);
}

internal sealed class CommunityPartyRoleAssignment
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public string SourceCode { get; set; } = CommunityPartyRoleAssignmentSourceCodes.Joined;
    public string VerificationScopeCode { get; set; } = CommunityPartyRoleConfirmationScopeCodes.PlatformProfileOnly;
}

internal static class CommunityPartyRoleAssignmentSourceCodes
{
    public const string Author = "Author";
    public const string Joined = "Joined";
}

internal static class CommunityPartyRoleConfirmationScopeCodes
{
    public const string PlatformProfileOnly = "PlatformProfileOnly";
    public const string ExplicitSelfAcceptance = "ExplicitSelfAcceptance";
}
