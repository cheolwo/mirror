using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hongdal.Contracts.Common.Community;

namespace Hongdal.Services.Community;

public sealed class CommunityPostParticipationUseCase : ICommunityPostParticipationUseCase
{
    private readonly ICommunityPostOpportunityStore _postStore;

    private readonly ICommunityVoteService _voteService;

    private readonly I커뮤니티원장저장소 _ledgerStore;

    private readonly ICommunityProfessionalEligibilityService _professionalEligibilityService;

    public CommunityPostParticipationUseCase(ICommunityPostOpportunityStore postStore, ICommunityVoteService voteService, I커뮤니티원장저장소 ledgerStore, ICommunityProfessionalEligibilityService professionalEligibilityService)
    {
        _postStore = postStore;
        _voteService = voteService;
        _ledgerStore = ledgerStore;
        _professionalEligibilityService = professionalEligibilityService;
    }

    public async Task<StartCommunityPostParticipationResponse> StartParticipationAsync(long postId, StartCommunityPostParticipationRequest request, string actorUserId, string actorDisplayName, CancellationToken cancellationToken = default(CancellationToken))
    {
        ArgumentNullException.ThrowIfNull(request, "request");
        CommunityPostOpportunityGuard.RequireActor(actorUserId);
        if (!request.ConfirmExplicitStart || !request.ConfirmNonBindingParticipation)
        {
            throw new InvalidOperationException("게시글에서 참여 관심 모집을 명시적으로 시작하고, 이 단계가 비구속적이라는 점을 모두 확인해야 합니다.");
        }
        CommunityPostOpportunitySource source = (await _postStore.GetAsync(postId, cancellationToken)) ?? throw new KeyNotFoundException("커뮤니티 게시글을 찾을 수 없습니다.");
        CommunityPostOpportunityGuard.EnsureCollectiveActionAllowed(source);
        string language = CommunityDisplayLanguageCodes.Normalize(request.DisplayLanguageCode);
        CommunityVoteResponse? existingVote = await CommunityPostOpportunityProjection.FindParticipationVoteAsync(_voteService, postId, cancellationToken);
        if (existingVote?.Status == "Open")
        {
            return CommunityPostOpportunityProjection.BuildParticipationStartResponse(source, language, existingVote, reused: true);
        }
        if (!string.IsNullOrWhiteSpace(existingVote?.CommunityLedgerId))
        {
            return CommunityPostOpportunityProjection.BuildParticipationStartResponse(source, language, existingVote, reused: true);
        }
        return CommunityPostOpportunityProjection.BuildParticipationStartResponse(source, language, await _voteService.CreateAsync(new CommunityVoteCreateRequest
        {
            AppKey = source.AppKey,
            CommunityScope = source.AppKey,
            Title = BuildParticipationTitle(source, request.Title, language),
            Description = (string.Equals(language, "en-US", StringComparison.OrdinalIgnoreCase) ? "Choose any roles you may be interested in. This is non-binding and does not create an order, contract, dispatch, brokerage, or ledger." : "관심 있는 역할을 가볍게 선택합니다. 이 선택만으로 주문·계약·배차·주선·원장이 만들어지지 않습니다."),
            VoteKind = "CollectiveActionInterest",
            SourcePostId = postId,
            StructuredOptions = CommunityPostOpportunityProjection.BuildInterestVoteOptions(language),
            AllowMultipleSelection = true,
            ResolutionDocumentEnabled = false,
            SignatureRequired = false,
            ClosesAtUtc = request.ClosesAtUtc,
            CreatedByDisplayName = (string.IsNullOrWhiteSpace(actorDisplayName) ? "참여자" : actorDisplayName.Trim())
        }, cancellationToken), reused: false);
    }

    public async Task<PromoteCommunityPostParticipationResponse> PromoteParticipationAsync(long postId, PromoteCommunityPostParticipationRequest request, string actorUserId, string actorDisplayName, CancellationToken cancellationToken = default(CancellationToken))
    {
        ArgumentNullException.ThrowIfNull(request, "request");
        string actor = CommunityPostOpportunityGuard.RequireActor(actorUserId);
        if (!request.ConfirmProvisionalLedger || !request.ConfirmNonBindingEvidence || !request.ConfirmParticipantNotifications)
        {
            throw new InvalidOperationException("가원장 생성, 비구속적 관심 증빙, 참여자 알림을 모두 명시적으로 확인해야 합니다.");
        }
        if (request.InterestVoteId == Guid.Empty)
        {
            throw new InvalidOperationException("승격할 참여 관심 투표가 필요합니다.");
        }
        if (!CommunityCollectiveIntentTypeCodes.IsSupported(request.CollectiveIntentTypeCode))
        {
            throw new InvalidOperationException("공동구매, 공동수입 또는 공동수출 검토 의도만 가원장으로 기록할 수 있습니다.");
        }
        string intentTypeCode = CommunityCollectiveIntentTypeCodes.All.First((string code) => string.Equals(code, request.CollectiveIntentTypeCode.Trim(), StringComparison.OrdinalIgnoreCase));
        string tradeDirectionCode = NormalizeTradeDirectionCode(intentTypeCode, request.TradeDirectionCode);
        string originCountryCode = NormalizeCountryCode(request.OriginCountryCode, "출발국가");
        string destinationCountryCode = NormalizeCountryCode(request.DestinationCountryCode, "도착국가");
        IReadOnlyList<string> transportModeCodes = NormalizeTransportModeCodes(request.TransportModeCodes);
        CommunityPostOpportunitySource source = (await _postStore.GetAsync(postId, cancellationToken)) ?? throw new KeyNotFoundException("커뮤니티 게시글을 찾을 수 없습니다.");
        CommunityPostOpportunityGuard.EnsureCollectiveActionAllowed(source);
        if (string.IsNullOrWhiteSpace(source.AuthorUserId) || !string.Equals(source.AuthorUserId, actor, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("게시글 작성자만 모인 관심을 가원장으로 승격할 수 있습니다.");
        }
        CommunityInterestVotePromotionSnapshot snapshot = (await _voteService.GetInterestPromotionSnapshotAsync(request.InterestVoteId, postId, cancellationToken)) ?? throw new KeyNotFoundException("참여 관심 투표를 찾을 수 없습니다.");
        if (snapshot.ParticipantCount < 2)
        {
            throw new InvalidOperationException($"가원장은 서로 다른 관심 참여자 {2}명 이상이 모인 뒤 만들 수 있습니다.");
        }
        IReadOnlyList<string> authorProfessionalRoles = await _professionalEligibilityService.GetVerifiedRoleCodesAsync(actor, cancellationToken);
        string[] plannedSpecialistRoles = CommunityPostPartyRoleCodes.ForPlan(tradeDirectionCode, transportModeCodes, destinationCountryCode).Where(CommunityPostPartyRoleCodes.IsSpecialist).ToArray();
        authorProfessionalRoles = authorProfessionalRoles.Where((string role) => plannedSpecialistRoles.Contains<string>(role, StringComparer.OrdinalIgnoreCase)).ToArray();
        string ledgerId = CommunityPostProvisionalLedgerIds.FromInterestVote(postId, request.InterestVoteId);
        if (source.LinkedLedgerId != null && !string.Equals(source.LinkedLedgerId, ledgerId, StringComparison.OrdinalIgnoreCase))
        {
            throw new CommunityPostOpportunityConflictException("게시글에 이미 다른 원장이 연결되어 있습니다.");
        }
        switch (await _postStore.LinkLedgerAsync(postId, actor, ledgerId, cancellationToken))
        {
            case CommunityPostLedgerLinkResult.NotFound:
                throw new KeyNotFoundException("커뮤니티 게시글을 찾을 수 없습니다.");
            case CommunityPostLedgerLinkResult.NotOwner:
                throw new UnauthorizedAccessException("게시글 작성자만 가원장을 연결할 수 있습니다.");
            case CommunityPostLedgerLinkResult.ConflictingLedger:
                throw new CommunityPostOpportunityConflictException("동시에 다른 원장이 게시글에 연결되었습니다. 게시글을 다시 확인해 주세요.");
            default:
                {
                    커뮤니티원장Dto? ledger = await _ledgerStore.원장조회Async(ledgerId, cancellationToken);
                    bool reused = ledger != null;
                    if (ledger == null)
                    {
                        try
                        {
                            ledger = await _ledgerStore.원장저장Async(BuildProvisionalLedgerRequest(source, snapshot, ledgerId, intentTypeCode, tradeDirectionCode, originCountryCode, destinationCountryCode, transportModeCodes, actor, actorDisplayName, authorProfessionalRoles), actor, cancellationToken);
                        }
                        catch (InvalidOperationException)
                        {
                            ledger = await _ledgerStore.원장조회Async(ledgerId, cancellationToken);
                            if (ledger == null)
                            {
                                throw;
                            }
                            reused = true;
                        }
                    }
                    string storedTradeDirectionCode = CommunityPostProfessionalParticipationProjection.ReadTradeDirectionCode(ledger);
                    string storedOriginCountryCode = ledger.확장속성.GetValueOrDefault("OriginCountryCode", string.Empty);
                    string storedDestinationCountryCode = ledger.확장속성.GetValueOrDefault("DestinationCountryCode", string.Empty);
                    IReadOnlyList<string> storedTransportModeCodes = CommunityPostProfessionalParticipationProjection.ReadTransportModeCodes(ledger);
                    if (!string.Equals(storedTradeDirectionCode, tradeDirectionCode, StringComparison.OrdinalIgnoreCase) || !RouteValueMatches(originCountryCode, storedOriginCountryCode) || !RouteValueMatches(destinationCountryCode, storedDestinationCountryCode) || (transportModeCodes.Count > 0 && !transportModeCodes.Order<string>(StringComparer.OrdinalIgnoreCase).SequenceEqual<string>(storedTransportModeCodes.Order<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase)))
                    {
                        throw new CommunityPostOpportunityConflictException("이미 기록된 가원장의 거래 방향·국가·운송수단과 요청이 다릅니다.");
                    }
                    tradeDirectionCode = storedTradeDirectionCode;
                    originCountryCode = storedOriginCountryCode;
                    destinationCountryCode = storedDestinationCountryCode;
                    transportModeCodes = storedTransportModeCodes;
                    CommunityInterestVotePromotionSnapshot attachedSnapshot = (await _voteService.AttachProvisionalLedgerAsync(request.InterestVoteId, postId, ledgerId, 2, actorDisplayName, cancellationToken)) ?? throw new KeyNotFoundException("참여 관심 투표를 찾을 수 없습니다.");
                    CommunityPostOpportunitySource linkedSource = source with
                    {
                        LinkedLedgerId = ledgerId
                    };
                    string language = CommunityDisplayLanguageCodes.Normalize(request.DisplayLanguageCode);
                    CommunityVoteResponse? vote = await _voteService.GetAsync(request.InterestVoteId, cancellationToken);
                    await SetInitialMomentumPromotionAsync(postId, ledger, authorProfessionalRoles, cancellationToken);
                    return new PromoteCommunityPostParticipationResponse
                    {
                        PostId = postId,
                        DisplayLanguageCode = language,
                        ReusedExistingProvisionalLedger = reused,
                        CollectiveIntentTypeCode = intentTypeCode,
                        TradeDirectionCode = tradeDirectionCode,
                        OriginCountryCode = originCountryCode,
                        DestinationCountryCode = destinationCountryCode,
                        TransportModeCodes = transportModeCodes,
                        ProvisionalLedger = new CommunityPostProvisionalLedgerResponse
                        {
                            LedgerId = ledger.원장Id,
                            Revision = ledger.Revision,
                            LedgerTemplateKey = ledger.원장템플릿Key,
                            State = ledger.상태,
                            CurrentStageCode = (ledger.현재단계Key ?? string.Empty),
                            ParticipantCount = attachedSnapshot.ParticipantCount,
                            EvidenceSnapshotHash = attachedSnapshot.EvidenceSnapshotHash,
                            NonBinding = true,
                            ParticipantNotificationsRequested = true,
                            TradeDirectionCode = tradeDirectionCode,
                            OriginCountryCode = originCountryCode,
                            DestinationCountryCode = destinationCountryCode,
                            TransportModeCodes = transportModeCodes
                        },
                        Participation = CommunityPostOpportunityProjection.BuildParticipationEntry(linkedSource, language, vote, ledger)
                    };
                }
        }
    }

    private async Task SetInitialMomentumPromotionAsync(long postId, 커뮤니티원장Dto ledger, IReadOnlyList<string> authorProfessionalRoles, CancellationToken cancellationToken)
    {
        IReadOnlyList<CommunityPartyRoleAssignment> assignments = CommunityPostProfessionalParticipationProjection.ReadAssignments(ledger);
        int professionalCount = assignments.Select((CommunityPartyRoleAssignment assignment) => assignment.UserId).Distinct<string>(StringComparer.OrdinalIgnoreCase).Count();
        if (professionalCount == 0 && authorProfessionalRoles.Count > 0)
        {
            professionalCount = 1;
        }
        string momentumCode = CommunityPostProfessionalParticipationProjection.ResolveMomentumCode(ledger, assignments);
        switch (await _postStore.SetMomentumPromotionAsync(postId, ledger.원장Id, momentumCode, CommunityPostProfessionalParticipationProjection.ReadinessMessage(ledger, "ko-KR"), professionalCount, cancellationToken))
        {
            case CommunityPostMomentumUpdateResult.NotFound:
                throw new KeyNotFoundException("커뮤니티 게시글을 찾을 수 없습니다.");
            case CommunityPostMomentumUpdateResult.ConflictingLedger:
                throw new CommunityPostOpportunityConflictException("게시글의 가원장 연결이 변경되었습니다.");
        }
    }

    private static 커뮤니티원장저장요청 BuildProvisionalLedgerRequest(CommunityPostOpportunitySource source, CommunityInterestVotePromotionSnapshot snapshot, string ledgerId, string intentTypeCode, string tradeDirectionCode, string originCountryCode, string destinationCountryCode, IReadOnlyList<string> transportModeCodes, string actorUserId, string actorDisplayName, IReadOnlyList<string> authorProfessionalRoles)
    {
        string normalizedActorDisplayName = (string.IsNullOrWhiteSpace(actorDisplayName) ? "게시글 작성자" : actorDisplayName.Trim());
        List<커뮤니티원장참여자Dto> list = snapshot.Participants.Select((CommunityInterestVoteParticipantSnapshot participant) => new 커뮤니티원장참여자Dto
        {
            UserId = participant.UserId,
            DisplayName = participant.DisplayName,
            RoleLabel = ((participant.RoleCodes.Count == 0) ? "관심 참여자" : string.Join(", ", participant.RoleCodes)),
            ParticipationState = "비구속 관심표시"
        }).ToList();
        if (!list.Any((커뮤니티원장참여자Dto participant) => string.Equals(participant.UserId, actorUserId, StringComparison.OrdinalIgnoreCase)))
        {
            list.Add(new 커뮤니티원장참여자Dto
            {
                UserId = actorUserId,
                DisplayName = normalizedActorDisplayName,
                RoleLabel = "Facilitator",
                ParticipationState = "가원장 발의"
            });
        }
        CommunityPartyRoleAssignment[] array = authorProfessionalRoles.Select((string roleCode) => new CommunityPartyRoleAssignment
        {
            UserId = actorUserId,
            DisplayName = normalizedActorDisplayName,
            RoleCode = roleCode,
            SourceCode = "Author",
            VerificationScopeCode = "PlatformProfileOnly"
        }).ToArray();
        if (array.Length != 0)
        {
            int index = list.FindIndex((커뮤니티원장참여자Dto participant) => string.Equals(participant.UserId, actorUserId, StringComparison.OrdinalIgnoreCase));
            커뮤니티원장참여자Dto 커뮤니티원장참여자Dto2 = list[index];
            IEnumerable<string> values = array.Select((CommunityPartyRoleAssignment assignment) => CommunityPostProfessionalParticipationProjection.RoleLabel(assignment.RoleCode, english: false));
            list[index] = new 커뮤니티원장참여자Dto
            {
                UserId = 커뮤니티원장참여자Dto2.UserId,
                DisplayName = 커뮤니티원장참여자Dto2.DisplayName,
                RoleLabel = 커뮤니티원장참여자Dto2.RoleLabel + " | 플랫폼 역할 확인 · " + string.Join(", ", values),
                ParticipationState = "가원장 발의·역할 참여"
            };
        }
        string value = JsonSerializer.Serialize(snapshot.RoleCounts);
        string[] array2 = CommunityPostPartyRoleCodes.ForPlan(tradeDirectionCode, transportModeCodes, destinationCountryCode).Where(CommunityPostPartyRoleCodes.IsSpecialist).ToArray();
        string value2 = CommunityPostProfessionalParticipationProjection.SerializeAssignments(array);
        string value3 = ((array.Length == 0) ? "SeekingParty" : "PartyForming");
        string text = "[가원장] " + source.Title;
        if (text.Length > 180)
        {
            text = text.Substring(0, 180);
        }
        커뮤니티원장저장요청 커뮤니티원장저장요청2 = new 커뮤니티원장저장요청();
        커뮤니티원장저장요청2.원장Id = ledgerId;
        커뮤니티원장저장요청2.기대Revision = 0L;
        커뮤니티원장저장요청2.커뮤니티Id = source.AppKey;
        커뮤니티원장저장요청2.원장템플릿Key = "group-purchase";
        커뮤니티원장저장요청2.제목 = text;
        커뮤니티원장저장요청 커뮤니티원장저장요청3 = 커뮤니티원장저장요청2;
        if (1 == 0)
        {
        }
        string 원함 = ((intentTypeCode == "GroupImportCandidate") ? "공동수입 가능성과 당사자·통관·운송 조건을 비구속적으로 함께 검토합니다." : ((!(intentTypeCode == "GroupExportCandidate")) ? "공동구매 가능성과 조건을 비구속적으로 함께 검토합니다." : "공동수출 가능성과 당사자·통관·운송 조건을 비구속적으로 함께 검토합니다."));
        if (1 == 0)
        {
        }
        커뮤니티원장저장요청3.원함 = 원함;
        커뮤니티원장저장요청2.상태 = "초안";
        커뮤니티원장저장요청2.현재단계Key = "proposal";
        커뮤니티원장저장요청2.대상OsCode = "CommunityTrustOS";
        커뮤니티원장저장요청2.대상OsName = "커뮤니티 신뢰 OS";
        커뮤니티원장저장요청2.생성자UserId = actorUserId;
        커뮤니티원장저장요청2.생성자표시명 = normalizedActorDisplayName;
        커뮤니티원장저장요청2.참여자목록 = list;
        커뮤니티원장저장요청2.블록목록 = new 커뮤니티원장블록Dto[2]
        {
            new 커뮤니티원장블록Dto
            {
                BlockId = "non-binding-interest-evidence",
                BlockType = "Generic",
                Title = "비구속적 관심 모임 증빙",
                State = "기록됨",
                Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["SourcePostId"] = source.PostId.ToString(),
                    ["InterestVoteId"] = snapshot.VoteId.ToString("D"),
                    ["ParticipantCount"] = snapshot.ParticipantCount.ToString(),
                    ["RoleCountsJson"] = value,
                    ["EvidenceSnapshotHash"] = snapshot.EvidenceSnapshotHash,
                    ["LegalEffectNotice"] = "이 가원장은 관심이 모였다는 사실만 기록하며 주문, 계약, 결제, 배차 또는 운송 주선을 확정하지 않습니다."
                }
            },
            CommunityPostProfessionalParticipationProjection.BuildProfessionalBlock(array2, array)
        };
        커뮤니티원장저장요청2.외부참조 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SourceCommunityPostId"] = source.PostId.ToString(),
            ["SourceInterestVoteId"] = snapshot.VoteId.ToString("D")
        };
        커뮤니티원장저장요청2.확장속성 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LedgerMaturityCode"] = "Provisional",
            ["BindingEffectCode"] = "NonBinding",
            ["CollectiveIntentTypeCode"] = intentTypeCode,
            ["TradeDirectionCode"] = tradeDirectionCode,
            ["OriginCountryCode"] = originCountryCode,
            ["DestinationCountryCode"] = destinationCountryCode,
            ["TransportModeCodesJson"] = JsonSerializer.Serialize(transportModeCodes),
            ["InterestEvidenceSnapshotHash"] = snapshot.EvidenceSnapshotHash,
            ["ParticipantNotificationsRequested"] = bool.TrueString,
            ["RequiredProfessionalRolesJson"] = JsonSerializer.Serialize(array2),
            ["ConfirmedPartyRoleAssignmentsJson"] = value2,
            ["ConfirmedPartyRoleParticipantCount"] = ((array.Length == 0) ? "0" : "1"),
            ["AuthorVerifiedProfessionalRolesJson"] = JsonSerializer.Serialize(authorProfessionalRoles),
            ["CommunityMomentumCode"] = value3,
            ["CommunityPromotionRequested"] = bool.TrueString,
            ["InterestParticipantCount"] = snapshot.ParticipantCount.ToString(),
            ["InterestRoleCountsJson"] = value
        };
        return 커뮤니티원장저장요청2;
    }

    private static string NormalizeTradeDirectionCode(string intentTypeCode, string? requestedCode)
    {
        string text = CommunityTradeDirectionCodes.ExpectedForIntent(intentTypeCode);
        if (string.IsNullOrWhiteSpace(requestedCode))
        {
            return text;
        }
        if (!CommunityTradeDirectionCodes.IsSupported(requestedCode))
        {
            throw new InvalidOperationException("지원하지 않는 거래 방향입니다.");
        }
        string text2 = CommunityTradeDirectionCodes.All.First((string code) => string.Equals(code, requestedCode.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.Equals(text2, text, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("가원장 의도와 거래 방향이 일치하지 않습니다.");
        }
        return text2;
    }

    private static string NormalizeCountryCode(string? value, string fieldLabel)
    {
        string text = CommunityGroupPurchaseTradeRoutePolicy.NormalizeCountryCode(value);
        if (!string.IsNullOrWhiteSpace(text) && !CommunityGroupPurchaseTradeRoutePolicy.IsValidCountryCode(text))
        {
            throw new InvalidOperationException(fieldLabel + " 코드는 ISO 알파-2 두 자리로 입력해 주세요.");
        }
        return text;
    }

    private static IReadOnlyList<string> NormalizeTransportModeCodes(IEnumerable<string>? values)
    {
        string[] array = (values ?? Array.Empty<string>()).Where((string value) => !string.IsNullOrWhiteSpace(value)).ToArray();
        IReadOnlyList<string> readOnlyList = CommunityTransportModeCodes.NormalizeMany(array);
        if (readOnlyList.Count != array.Select((string value) => value.Trim()).Distinct<string>(StringComparer.OrdinalIgnoreCase).Count())
        {
            throw new InvalidOperationException("지원하지 않는 운송수단이 포함되어 있습니다.");
        }
        return readOnlyList;
    }

    private static bool RouteValueMatches(string requestedValue, string storedValue)
    {
        return string.IsNullOrWhiteSpace(requestedValue) || string.Equals(requestedValue, storedValue, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildParticipationTitle(CommunityPostOpportunitySource source, string? requestedTitle, string language)
    {
        string text = ((!string.IsNullOrWhiteSpace(requestedTitle)) ? requestedTitle.Trim() : (string.Equals(language, "en-US", StringComparison.OrdinalIgnoreCase) ? ("Join the conversation: " + source.Title) : ("함께 해보기: " + source.Title)));
        return (text.Length <= 180) ? text : text.Substring(0, 180);
    }
}
