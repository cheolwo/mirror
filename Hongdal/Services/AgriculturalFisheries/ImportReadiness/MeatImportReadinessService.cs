using System.Text.Json;
using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Contracts.Common.Community;
using Hongdal.Services.Community;

namespace Hongdal.Services.AgriculturalFisheries.ImportReadiness;

public sealed class MeatImportReadinessService : IMeatImportReadinessService
{
    private const string CaseMetadataJsonKey = "MeatImportReadinessCaseJson";
    private const string ParticipantsJsonKey = "MeatImportReadinessParticipantsJson";
    private const string StepStateJsonKey = "MeatImportReadinessStepStateJson";
    private const string ProcessStatusCodeKey = "MeatImportReadinessProcessStatusCode";
    private const string InformationOnlyKey = "InformationOnly";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly I커뮤니티원장저장소 _ledgerStore;

    public MeatImportReadinessService(I커뮤니티원장저장소 ledgerStore)
    {
        _ledgerStore = ledgerStore;
    }

    public MeatImportReadinessDiagramResponse GetDiagram()
        => MeatImportReadinessTemplateCatalog.Get();

    public async Task<MeatImportReadinessCaseListResponse> ListMineAsync(
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var actor = RequireUserId(actorUserId);
        var ledgers = await _ledgerStore.원장목록조회Async(
            new 커뮤니티원장조회조건
            {
                원장템플릿Key = MeatImportReadinessCodes.LedgerTemplateKey,
                참여자UserId = actor,
                Limit = 100
            },
            cancellationToken);

        return new MeatImportReadinessCaseListResponse
        {
            Items = ledgers
                .Select(ParseContext)
                .Select(ToSummary)
                .ToArray()
        };
    }

    public async Task<MeatImportReadinessCaseResponse?> GetCaseAsync(
        string caseId,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var ledger = await _ledgerStore.원장조회Async(RequireCaseId(caseId), cancellationToken);
        if (ledger is null || !IsReadinessLedger(ledger))
        {
            return null;
        }

        var context = ParseContext(ledger);
        EnsureParticipant(context, RequireUserId(actorUserId));
        return ToResponse(context);
    }

    public Task<MeatImportReadinessCaseResponse> CreateCaseAsync(
        CreateMeatImportReadinessCaseRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default)
        => CreateCaseCoreAsync(null, request, actorUserId, actorDisplayName, cancellationToken);

    public Task<MeatImportReadinessCaseResponse> CreateCaseFromCommunityPostAsync(
        long sourceCommunityPostId,
        CreateMeatImportReadinessCaseRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default)
    {
        if (sourceCommunityPostId <= 0)
        {
            throw new InvalidOperationException("연결할 커뮤니티 게시글 ID가 올바르지 않습니다.");
        }

        return CreateCaseCoreAsync(sourceCommunityPostId, request, actorUserId, actorDisplayName, cancellationToken);
    }

    private async Task<MeatImportReadinessCaseResponse> CreateCaseCoreAsync(
        long? sourceCommunityPostId,
        CreateMeatImportReadinessCaseRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCreateRequest(request);

        var actor = RequireUserId(actorUserId);
        var actorName = Clean(actorDisplayName) ?? "참여자";
        var initiatorSideCode = CanonicalPartySide(request.InitiatorSideCode);
        var caseId = sourceCommunityPostId.HasValue
            ? MeatImportReadinessCaseIds.FromCommunityPost(sourceCommunityPostId.Value)
            : $"import-readiness-{Guid.NewGuid():N}";

        if (sourceCommunityPostId.HasValue)
        {
            var existing = await _ledgerStore.원장조회Async(caseId, cancellationToken);
            if (existing is not null)
            {
                if (!IsReadinessLedger(existing))
                {
                    throw new InvalidOperationException("게시글에 연결할 준비도 원장 ID가 다른 원장에 사용되고 있습니다.");
                }

                var existingContext = ParseContext(existing);
                EnsureParticipant(existingContext, actor);
                return ToResponse(existingContext);
            }
        }

        var metadata = new PersistedCaseMetadata
        {
            SourceCommunityPostId = sourceCommunityPostId,
            InitiatorSideCode = initiatorSideCode,
            ProductTypeCode = CanonicalProductType(request.ProductTypeCode),
            ProductName = request.ProductName.Trim(),
            HsCode = NormalizeHsCode(request.HsCode),
            OriginCountryCode = request.OriginCountryCode.Trim().ToUpperInvariant(),
            OriginCountryName = request.OriginCountryName.Trim(),
            ProductSpecification = Clean(request.ProductSpecification),
            KoreanImporterOrganizationName = request.KoreanImporterOrganizationName.Trim()
        };
        var participants = BuildInitialParticipants(request, actor, actorName, initiatorSideCode);
        var states = MeatImportReadinessTemplateCatalog.Get().Steps
            .ToDictionary(
                step => step.Code,
                _ => new PersistedStepState(),
                StringComparer.OrdinalIgnoreCase);
        var context = new CaseContext(
            new 커뮤니티원장Dto
            {
                원장Id = caseId,
                커뮤니티Id = Clean(request.CommunityId) ?? "platform",
                원장템플릿Key = MeatImportReadinessCodes.LedgerTemplateKey,
                제목 = request.Title.Trim(),
                원함 = "국내외 참여자가 육류 수입 준비 절차와 확인 근거를 같은 커뮤니티 원장에서 함께 관리합니다.",
                생성자UserId = actor,
                생성자표시명 = actorName
            },
            metadata,
            participants,
            states);

        try
        {
            var saved = await SaveAsync(context, sourceCommunityPostId.HasValue ? 0 : null, actor, cancellationToken);
            return ToResponse(ParseContext(saved));
        }
        catch (MeatImportReadinessConcurrencyException) when (sourceCommunityPostId.HasValue)
        {
            var existing = await _ledgerStore.원장조회Async(caseId, cancellationToken);
            if (existing is null || !IsReadinessLedger(existing))
            {
                throw;
            }

            var existingContext = ParseContext(existing);
            EnsureParticipant(existingContext, actor);
            return ToResponse(existingContext);
        }
    }

    public async Task<MeatImportReadinessCaseResponse> UpdateStepStatusAsync(
        string caseId,
        string stepCode,
        UpdateMeatImportReadinessStepStatusRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = await LoadForMutationAsync(caseId, actorUserId, cancellationToken);
        EnsureExpectedRevision(request.ExpectedRevision, context.Ledger.Revision);
        var template = MeatImportReadinessTemplateCatalog.FindStep(stepCode);
        var state = context.StepStates[template.Code];
        var targetStatus = CanonicalStepStatus(request.StatusCode);

        if (template.RequiresJointConfirmation && IsCompletionStatusCode(targetStatus))
        {
            throw new InvalidOperationException("양측 공동 확인 단계는 상태를 직접 완료할 수 없습니다. 한국 측과 해외 측이 각각 확인해야 합니다.");
        }

        if (string.Equals(targetStatus, MeatImportReadinessStepStatusCodes.NotApplicable, StringComparison.OrdinalIgnoreCase)
            && !template.CanBeNotApplicable)
        {
            throw new InvalidOperationException("이 단계는 필수 절차이므로 해당 없음으로 처리할 수 없습니다.");
        }

        if (IsCompletionStatusCode(targetStatus))
        {
            EnsurePrerequisitesComplete(context, template);
        }

        if (template.RequiresOfficialResult
            && string.Equals(targetStatus, MeatImportReadinessStepStatusCodes.ParticipantChecked, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("정부기관 확인이 필요한 단계는 참여자 확인만으로 완료할 수 없습니다. 공식 결과를 기록해 주세요.");
        }

        if (string.Equals(targetStatus, MeatImportReadinessStepStatusCodes.OfficialResultRecorded, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(request.OfficialReferenceNumber)
            && state.Evidences.Count == 0)
        {
            throw new InvalidOperationException("공식 결과를 기록하려면 공식 참조번호 또는 증빙 메타데이터가 필요합니다.");
        }

        if (!string.Equals(targetStatus, MeatImportReadinessStepStatusCodes.Blocked, StringComparison.OrdinalIgnoreCase)
            && state.Discussions.Any(item => item.IsBlocking && !item.IsResolved))
        {
            throw new InvalidOperationException("미해결 차단 질문이나 이의가 있어 이 단계의 차단 상태를 해제할 수 없습니다.");
        }

        var previous = state.StatusCode;
        if (string.Equals(targetStatus, MeatImportReadinessStepStatusCodes.Blocked, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(previous, MeatImportReadinessStepStatusCodes.Blocked, StringComparison.OrdinalIgnoreCase))
        {
            state.StatusBeforeBlock = previous;
        }

        state.StatusCode = targetStatus;
        state.LastNote = Clean(request.Note);
        state.OfficialReferenceNumber = Clean(request.OfficialReferenceNumber) ?? state.OfficialReferenceNumber;
        state.OfficialResultDate = request.OfficialResultDate ?? state.OfficialResultDate;
        AddHistory(state, "StatusChanged", previous, targetStatus, request.Note, actorUserId, actorDisplayName);
        InvalidateJointConfirmationWhenUpstreamChanged(context, template.Code, actorUserId, actorDisplayName);

        var saved = await SaveAsync(context, request.ExpectedRevision, actorUserId, cancellationToken);
        return ToResponse(ParseContext(saved));
    }

    public async Task<MeatImportReadinessCaseResponse> AddEvidenceAsync(
        string caseId,
        string stepCode,
        AddMeatImportReadinessEvidenceRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.EvidenceCode) || string.IsNullOrWhiteSpace(request.Title))
        {
            throw new InvalidOperationException("증빙 코드와 제목이 필요합니다.");
        }

        if (!string.IsNullOrWhiteSpace(request.ReferenceUri)
            && request.ReferenceUri.Trim().Length > 2048)
        {
            throw new InvalidOperationException("증빙 참조 위치는 2,048자 이하여야 합니다.");
        }

        var context = await LoadForMutationAsync(caseId, actorUserId, cancellationToken);
        EnsureExpectedRevision(request.ExpectedRevision, context.Ledger.Revision);
        var template = MeatImportReadinessTemplateCatalog.FindStep(stepCode);
        var state = context.StepStates[template.Code];
        var actor = EnsureParticipant(context, actorUserId);
        var previous = state.StatusCode;
        state.Evidences.Add(new MeatImportReadinessEvidenceResponse
        {
            EvidenceId = $"evidence-{Guid.NewGuid():N}",
            EvidenceCode = request.EvidenceCode.Trim(),
            Title = request.Title.Trim(),
            DocumentNumber = Clean(request.DocumentNumber),
            IssuerName = Clean(request.IssuerName),
            ReferenceUri = Clean(request.ReferenceUri),
            IssuedOn = request.IssuedOn,
            ExpiresOn = request.ExpiresOn,
            Note = Clean(request.Note),
            AddedByUserId = actorUserId.Trim(),
            AddedByDisplayName = ResolveDisplayName(actor, actorDisplayName),
            AddedAtUtc = DateTime.UtcNow
        });
        if (string.Equals(state.StatusCode, MeatImportReadinessStepStatusCodes.NotStarted, StringComparison.OrdinalIgnoreCase))
        {
            state.StatusCode = MeatImportReadinessStepStatusCodes.EvidenceSubmitted;
        }

        AddHistory(state, "EvidenceAdded", previous, state.StatusCode, request.Title, actorUserId, actorDisplayName);
        InvalidateJointConfirmationWhenUpstreamChanged(context, template.Code, actorUserId, actorDisplayName);
        var saved = await SaveAsync(context, request.ExpectedRevision, actorUserId, cancellationToken);
        return ToResponse(ParseContext(saved));
    }

    public async Task<MeatImportReadinessCaseResponse> AddDiscussionAsync(
        string caseId,
        string stepCode,
        AddMeatImportReadinessDiscussionRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new InvalidOperationException("질문·답변·이의 내용을 입력해 주세요.");
        }

        var kind = CanonicalDiscussionKind(request.KindCode);
        var context = await LoadForMutationAsync(caseId, actorUserId, cancellationToken);
        EnsureExpectedRevision(request.ExpectedRevision, context.Ledger.Revision);
        var template = MeatImportReadinessTemplateCatalog.FindStep(stepCode);
        var state = context.StepStates[template.Code];
        var actor = EnsureParticipant(context, actorUserId);
        var replyTo = Clean(request.ReplyToDiscussionId);
        if (replyTo is not null && state.Discussions.All(item => !string.Equals(item.DiscussionId, replyTo, StringComparison.OrdinalIgnoreCase)))
        {
            throw new KeyNotFoundException("답변할 원문 질문이나 이의를 찾을 수 없습니다.");
        }

        var isBlocking = request.IsBlocking
                         || string.Equals(kind, MeatImportReadinessDiscussionKindCodes.Objection, StringComparison.OrdinalIgnoreCase);
        state.Discussions.Add(new MeatImportReadinessDiscussionResponse
        {
            DiscussionId = $"discussion-{Guid.NewGuid():N}",
            KindCode = kind,
            Message = request.Message.Trim(),
            ReplyToDiscussionId = replyTo,
            IsBlocking = isBlocking,
            CreatedByUserId = actorUserId.Trim(),
            CreatedByDisplayName = ResolveDisplayName(actor, actorDisplayName),
            CreatedAtUtc = DateTime.UtcNow
        });

        var previous = state.StatusCode;
        if (isBlocking)
        {
            if (!string.Equals(previous, MeatImportReadinessStepStatusCodes.Blocked, StringComparison.OrdinalIgnoreCase))
            {
                state.StatusBeforeBlock = previous;
            }

            state.StatusCode = MeatImportReadinessStepStatusCodes.Blocked;
        }

        AddHistory(state, isBlocking ? "BlockingIssueRaised" : "DiscussionAdded", previous, state.StatusCode, request.Message, actorUserId, actorDisplayName);
        InvalidateJointConfirmationWhenUpstreamChanged(context, template.Code, actorUserId, actorDisplayName);
        var saved = await SaveAsync(context, request.ExpectedRevision, actorUserId, cancellationToken);
        return ToResponse(ParseContext(saved));
    }

    public async Task<MeatImportReadinessCaseResponse> ResolveDiscussionAsync(
        string caseId,
        string stepCode,
        string discussionId,
        ResolveMeatImportReadinessDiscussionRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ResolutionNote))
        {
            throw new InvalidOperationException("해결 내용을 입력해 주세요.");
        }

        var context = await LoadForMutationAsync(caseId, actorUserId, cancellationToken);
        EnsureExpectedRevision(request.ExpectedRevision, context.Ledger.Revision);
        var template = MeatImportReadinessTemplateCatalog.FindStep(stepCode);
        var state = context.StepStates[template.Code];
        var actor = EnsureParticipant(context, actorUserId);
        var discussion = state.Discussions.FirstOrDefault(item =>
            string.Equals(item.DiscussionId, discussionId?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException("질문이나 이의 기록을 찾을 수 없습니다.");
        if (!discussion.IsBlocking)
        {
            throw new InvalidOperationException("차단 항목으로 등록된 질문이나 이의만 해결 처리할 수 있습니다.");
        }

        if (discussion.IsResolved)
        {
            throw new InvalidOperationException("이미 해결된 항목입니다.");
        }

        var isImporter = string.Equals(actor.RoleCode, MeatImportReadinessParticipantRoleCodes.KoreanImporter, StringComparison.OrdinalIgnoreCase);
        if (!isImporter && !string.Equals(discussion.CreatedByUserId, actorUserId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("차단 항목을 만든 참여자 또는 한국 수입업자만 해결 처리할 수 있습니다.");
        }

        discussion.IsResolved = true;
        discussion.ResolvedByUserId = actorUserId.Trim();
        discussion.ResolutionNote = request.ResolutionNote.Trim();
        discussion.ResolvedAtUtc = DateTime.UtcNow;
        var previous = state.StatusCode;
        if (state.Discussions.All(item => !item.IsBlocking || item.IsResolved)
            && string.Equals(state.StatusCode, MeatImportReadinessStepStatusCodes.Blocked, StringComparison.OrdinalIgnoreCase))
        {
            state.StatusCode = Clean(state.StatusBeforeBlock) ?? MeatImportReadinessStepStatusCodes.InProgress;
            state.StatusBeforeBlock = null;
        }

        AddHistory(state, "BlockingIssueResolved", previous, state.StatusCode, request.ResolutionNote, actorUserId, actorDisplayName);
        InvalidateJointConfirmationWhenUpstreamChanged(context, template.Code, actorUserId, actorDisplayName);
        var saved = await SaveAsync(context, request.ExpectedRevision, actorUserId, cancellationToken);
        return ToResponse(ParseContext(saved));
    }

    public async Task<MeatImportReadinessCaseResponse> AcknowledgeStepAsync(
        string caseId,
        string stepCode,
        AcknowledgeMeatImportReadinessStepRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Statement))
        {
            throw new InvalidOperationException("공동 확인 문구가 필요합니다.");
        }

        var context = await LoadForMutationAsync(caseId, actorUserId, cancellationToken);
        EnsureExpectedRevision(request.ExpectedRevision, context.Ledger.Revision);
        var template = MeatImportReadinessTemplateCatalog.FindStep(stepCode);
        if (!template.RequiresJointConfirmation)
        {
            throw new InvalidOperationException("양측 공동 확인이 필요한 단계가 아닙니다.");
        }

        EnsurePrerequisitesComplete(context, template);
        var gateScope = GetAncestors(template.Code).Append(template.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (context.StepStates
            .Where(pair => gateScope.Contains(pair.Key))
            .SelectMany(pair => pair.Value.Discussions)
            .Any(item => item.IsBlocking && !item.IsResolved))
        {
            throw new InvalidOperationException("선행 단계에 미해결 차단 질문이나 이의가 있어 공동 확인할 수 없습니다.");
        }

        var actor = EnsureParticipant(context, actorUserId);
        if (!string.Equals(actor.SideCode, MeatImportReadinessPartySideCodes.Korean, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(actor.SideCode, MeatImportReadinessPartySideCodes.Overseas, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("한국 측 또는 해외 측 당사자만 선적 전 공동 확인에 참여할 수 있습니다.");
        }

        var state = context.StepStates[template.Code];
        state.Acknowledgements.RemoveAll(item => string.Equals(item.SideCode, actor.SideCode, StringComparison.OrdinalIgnoreCase));
        state.Acknowledgements.Add(new MeatImportReadinessAcknowledgementResponse
        {
            AcknowledgementId = $"ack-{Guid.NewGuid():N}",
            SideCode = actor.SideCode,
            UserId = actorUserId.Trim(),
            DisplayName = ResolveDisplayName(actor, actorDisplayName),
            Statement = request.Statement.Trim(),
            AcknowledgedAtUtc = DateTime.UtcNow
        });

        var previous = state.StatusCode;
        var bothSidesConfirmed = state.Acknowledgements.Any(item => string.Equals(item.SideCode, MeatImportReadinessPartySideCodes.Korean, StringComparison.OrdinalIgnoreCase))
                                 && state.Acknowledgements.Any(item => string.Equals(item.SideCode, MeatImportReadinessPartySideCodes.Overseas, StringComparison.OrdinalIgnoreCase));
        state.StatusCode = bothSidesConfirmed
            ? MeatImportReadinessStepStatusCodes.ParticipantChecked
            : MeatImportReadinessStepStatusCodes.WaitingForCounterparty;
        AddHistory(state, bothSidesConfirmed ? "JointConfirmationCompleted" : "SideAcknowledged", previous, state.StatusCode, request.Statement, actorUserId, actorDisplayName);

        var saved = await SaveAsync(context, request.ExpectedRevision, actorUserId, cancellationToken);
        return ToResponse(ParseContext(saved));
    }

    private async Task<CaseContext> LoadForMutationAsync(
        string caseId,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var ledger = await _ledgerStore.원장조회Async(RequireCaseId(caseId), cancellationToken)
                     ?? throw new KeyNotFoundException("육류 수입 준비 작업공간을 찾을 수 없습니다.");
        if (!IsReadinessLedger(ledger))
        {
            throw new KeyNotFoundException("육류 수입 준비 작업공간을 찾을 수 없습니다.");
        }

        var context = ParseContext(ledger);
        EnsureParticipant(context, RequireUserId(actorUserId));
        return context;
    }

    private async Task<커뮤니티원장Dto> SaveAsync(
        CaseContext context,
        long? expectedRevision,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var evaluation = Evaluate(context);
        var diagram = BuildCaseDiagram(context);
        var extensions = new Dictionary<string, string>(context.Ledger.확장속성, StringComparer.OrdinalIgnoreCase)
        {
            [CaseMetadataJsonKey] = JsonSerializer.Serialize(context.Metadata, JsonOptions),
            [ParticipantsJsonKey] = JsonSerializer.Serialize(context.Participants, JsonOptions),
            [ProcessStatusCodeKey] = evaluation.ProcessStatusCode,
            [InformationOnlyKey] = bool.TrueString,
            ["BrokerageEnabled"] = bool.FalseString,
            ["TemplateVersion"] = MeatImportReadinessCodes.TemplateVersion
        };
        var externalReferences = new Dictionary<string, string>(context.Ledger.외부참조, StringComparer.OrdinalIgnoreCase)
        {
            ["HsCode"] = context.Metadata.HsCode,
            ["OriginCountryCode"] = context.Metadata.OriginCountryCode
        };
        if (context.Metadata.SourceCommunityPostId.HasValue)
        {
            externalReferences["SourceCommunityPostId"] = context.Metadata.SourceCommunityPostId.Value.ToString();
        }
        var saveRequest = new 커뮤니티원장저장요청
        {
            원장Id = context.Ledger.원장Id,
            기대Revision = expectedRevision,
            커뮤니티Id = context.Ledger.커뮤니티Id,
            원장템플릿Key = MeatImportReadinessCodes.LedgerTemplateKey,
            제목 = context.Ledger.제목,
            원함 = context.Ledger.원함,
            상태 = ToLedgerState(evaluation.ProcessStatusCode),
            현재단계Key = evaluation.CurrentStepCode,
            대상OsCode = CommunityLedgerOperatingSystemCodes.CommunityTrust,
            대상OsName = "커뮤니티 신뢰 OS",
            생성자UserId = context.Ledger.생성자UserId,
            생성자표시명 = context.Ledger.생성자표시명,
            블록목록 = MeatImportReadinessTemplateCatalog.Get().Steps.Select(step => new 커뮤니티원장블록Dto
            {
                BlockId = step.Code,
                BlockType = CommunityLedgerBlockTypes.State,
                Title = step.Title,
                State = context.StepStates[step.Code].StatusCode,
                담당자목록 = BuildAssignees(context.Participants, step),
                Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [StepStateJsonKey] = JsonSerializer.Serialize(context.StepStates[step.Code], JsonOptions),
                    ["PhaseCode"] = step.PhaseCode,
                    ["RequiresOfficialResult"] = step.RequiresOfficialResult.ToString(),
                    ["RequiresJointConfirmation"] = step.RequiresJointConfirmation.ToString()
                }
            }).ToArray(),
            블록담당자명시적갱신여부 = true,
            참여자목록 = context.Participants.Select(ToLedgerParticipant).ToArray(),
            포함원장목록 = context.Ledger.포함원장목록,
            다이어그램스냅샷 = diagram,
            외부참조 = externalReferences,
            확장속성 = extensions
        };

        try
        {
            return await _ledgerStore.원장저장Async(saveRequest, actorUserId.Trim(), cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("현재 상태가 다른 요청", StringComparison.Ordinal))
        {
            throw new MeatImportReadinessConcurrencyException("다른 참여자가 먼저 수정했습니다. 최신 작업공간을 다시 조회한 뒤 재시도해 주세요.", ex);
        }
    }

    private static CaseContext ParseContext(커뮤니티원장Dto ledger)
    {
        PersistedCaseMetadata metadata;
        if (!ledger.확장속성.TryGetValue(CaseMetadataJsonKey, out var metadataJson)
            || (metadata = JsonSerializer.Deserialize<PersistedCaseMetadata>(metadataJson, JsonOptions)!) is null)
        {
            throw new InvalidOperationException("육류 수입 준비 작업공간의 제품 정보가 손상되었습니다.");
        }

        List<MeatImportReadinessParticipantResponse> participants;
        if (!ledger.확장속성.TryGetValue(ParticipantsJsonKey, out var participantsJson)
            || (participants = JsonSerializer.Deserialize<List<MeatImportReadinessParticipantResponse>>(participantsJson, JsonOptions)!) is null)
        {
            participants = ledger.참여자목록.Select(participant => new MeatImportReadinessParticipantResponse
            {
                ParticipantId = $"participant-{Guid.NewGuid():N}",
                UserId = participant.UserId,
                DisplayName = participant.DisplayName,
                RoleCode = participant.RoleLabel,
                SideCode = string.Equals(participant.RoleLabel, MeatImportReadinessParticipantRoleCodes.KoreanImporter, StringComparison.OrdinalIgnoreCase)
                    ? MeatImportReadinessPartySideCodes.Korean
                    : MeatImportReadinessPartySideCodes.Overseas,
                ParticipationStateCode = participant.ParticipationState
            }).ToList();
        }

        var blocks = ledger.블록목록.ToDictionary(block => block.BlockId, StringComparer.OrdinalIgnoreCase);
        var stepStates = new Dictionary<string, PersistedStepState>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in MeatImportReadinessTemplateCatalog.Get().Steps)
        {
            PersistedStepState? state = null;
            if (blocks.TryGetValue(step.Code, out var block)
                && block.Data.TryGetValue(StepStateJsonKey, out var stateJson))
            {
                state = JsonSerializer.Deserialize<PersistedStepState>(stateJson, JsonOptions);
            }

            state ??= new PersistedStepState { StatusCode = blocks.GetValueOrDefault(step.Code)?.State ?? MeatImportReadinessStepStatusCodes.NotStarted };
            stepStates[step.Code] = state;
        }

        return new CaseContext(ledger, metadata, participants, stepStates);
    }

    private static MeatImportReadinessCaseResponse ToResponse(CaseContext context)
    {
        var evaluation = Evaluate(context);
        var template = MeatImportReadinessTemplateCatalog.Get();
        return new MeatImportReadinessCaseResponse
        {
            CaseId = context.Ledger.원장Id,
            Revision = context.Ledger.Revision,
            TemplateVersion = MeatImportReadinessCodes.TemplateVersion,
            Title = context.Ledger.제목,
            CommunityId = context.Ledger.커뮤니티Id,
            SourceCommunityPostId = context.Metadata.SourceCommunityPostId,
            InitiatorSideCode = context.Metadata.InitiatorSideCode,
            ProductTypeCode = context.Metadata.ProductTypeCode,
            ProductName = context.Metadata.ProductName,
            HsCode = context.Metadata.HsCode,
            OriginCountryCode = context.Metadata.OriginCountryCode,
            OriginCountryName = context.Metadata.OriginCountryName,
            ProductSpecification = context.Metadata.ProductSpecification,
            ProcessStatusCode = evaluation.ProcessStatusCode,
            CurrentStepCode = evaluation.CurrentStepCode,
            ReadinessPercent = evaluation.ReadinessPercent,
            OpenBlockingIssueCount = evaluation.OpenBlockingIssueCount,
            InformationOnly = true,
            IsBrokerageEnabled = false,
            CollaborationRoomId = DiagramLedgerRoomIds.Build(context.Ledger.원장Id),
            Participants = context.Participants,
            Steps = template.Steps.Select(step => ToStepResponse(context, step)).ToArray(),
            Diagram = BuildCaseDiagram(context),
            Notices =
            [
                template.OfficialDecisionBoundary,
                "OfficialResultRecorded는 참여자가 정부기관 결과의 참조정보를 기록했다는 뜻이며 홍달이 진위를 보증했다는 뜻이 아닙니다.",
                "발주·계약·통관 대행·운송 주선·정산은 이 작업공간에서 실행되지 않습니다."
            ],
            CreatedAtUtc = context.Ledger.생성시각Utc,
            UpdatedAtUtc = context.Ledger.수정시각Utc
        };
    }

    private static MeatImportReadinessCaseSummaryResponse ToSummary(CaseContext context)
    {
        var evaluation = Evaluate(context);
        return new MeatImportReadinessCaseSummaryResponse
        {
            CaseId = context.Ledger.원장Id,
            Revision = context.Ledger.Revision,
            Title = context.Ledger.제목,
            ProductName = context.Metadata.ProductName,
            OriginCountryName = context.Metadata.OriginCountryName,
            ProcessStatusCode = evaluation.ProcessStatusCode,
            CurrentStepCode = evaluation.CurrentStepCode,
            ReadinessPercent = evaluation.ReadinessPercent,
            OpenBlockingIssueCount = evaluation.OpenBlockingIssueCount,
            UpdatedAtUtc = context.Ledger.수정시각Utc
        };
    }

    private static MeatImportReadinessStepResponse ToStepResponse(
        CaseContext context,
        MeatImportReadinessStepTemplateResponse template)
    {
        var state = context.StepStates[template.Code];
        var missing = template.PrerequisiteStepCodes
            .Where(code => !IsStepComplete(MeatImportReadinessTemplateCatalog.FindStep(code), context.StepStates[code]))
            .ToArray();
        return new MeatImportReadinessStepResponse
        {
            StepCode = template.Code,
            Sequence = template.Sequence,
            PhaseCode = template.PhaseCode,
            PhaseName = template.PhaseName,
            Title = template.Title,
            StatusCode = state.StatusCode,
            LastNote = state.LastNote,
            OfficialReferenceNumber = state.OfficialReferenceNumber,
            OfficialResultDate = state.OfficialResultDate,
            RequiresOfficialResult = template.RequiresOfficialResult,
            RequiresJointConfirmation = template.RequiresJointConfirmation,
            LiveRecheckRequired = template.LiveRecheckRequired,
            PrerequisitesSatisfied = missing.Length == 0,
            CompletionSatisfied = IsStepComplete(template, state),
            MissingPrerequisiteStepCodes = missing,
            Evidences = state.Evidences.OrderByDescending(item => item.AddedAtUtc).ToArray(),
            Discussions = state.Discussions.OrderBy(item => item.CreatedAtUtc).ToArray(),
            Acknowledgements = state.Acknowledgements.OrderBy(item => item.AcknowledgedAtUtc).ToArray(),
            History = state.History.OrderByDescending(item => item.OccurredAtUtc).ToArray()
        };
    }

    private static DiagramSnapshotDto BuildCaseDiagram(CaseContext context)
    {
        var template = MeatImportReadinessTemplateCatalog.Get().Diagram;
        return new DiagramSnapshotDto
        {
            DiagramId = $"{context.Ledger.원장Id}:diagram",
            DiagramName = context.Ledger.제목,
            LedgerId = context.Ledger.원장Id,
            LedgerTemplateKey = MeatImportReadinessCodes.LedgerTemplateKey,
            WorkflowModeKey = template.WorkflowModeKey,
            Nodes = template.Nodes.Select(node =>
            {
                var state = context.StepStates[node.NodeId];
                var data = new Dictionary<string, string>(node.Data, StringComparer.OrdinalIgnoreCase)
                {
                    ["statusCode"] = state.StatusCode,
                    ["evidenceCount"] = state.Evidences.Count.ToString(),
                    ["openBlockingIssueCount"] = state.Discussions.Count(item => item.IsBlocking && !item.IsResolved).ToString(),
                    ["acknowledgementSides"] = string.Join(',', state.Acknowledgements.Select(item => item.SideCode).Distinct(StringComparer.OrdinalIgnoreCase))
                };
                return new DiagramNodeDto
                {
                    NodeId = node.NodeId,
                    Kind = node.Kind,
                    Title = node.Title,
                    GroupLabel = node.GroupLabel,
                    Description = node.Description,
                    X = node.X,
                    Y = node.Y,
                    RelatedRoute = node.RelatedRoute,
                    Data = data
                };
            }).ToArray(),
            Edges = template.Edges,
            Metadata = template.Metadata
        };
    }

    private static ProcessEvaluation Evaluate(CaseContext context)
    {
        var steps = MeatImportReadinessTemplateCatalog.Get().Steps;
        var completed = steps.Count(step => IsStepComplete(step, context.StepStates[step.Code]));
        var openBlocking = context.StepStates.Values.Sum(state => state.Discussions.Count(item => item.IsBlocking && !item.IsResolved));
        var hasBlockedState = context.StepStates.Values.Any(state => string.Equals(state.StatusCode, MeatImportReadinessStepStatusCodes.Blocked, StringComparison.OrdinalIgnoreCase));
        var current = steps.FirstOrDefault(step => !IsStepComplete(step, context.StepStates[step.Code]))?.Code
                      ?? MeatImportReadinessStepCodes.DistributionReleaseCheck;

        string process;
        if (openBlocking > 0 || hasBlockedState)
        {
            process = MeatImportReadinessProcessStatusCodes.Blocked;
        }
        else if (completed == steps.Count)
        {
            process = MeatImportReadinessProcessStatusCodes.Completed;
        }
        else if (IsComplete(context, MeatImportReadinessStepCodes.CustomsClearanceResult))
        {
            process = MeatImportReadinessProcessStatusCodes.DomesticReleasePreparation;
        }
        else if (HasStarted(context, MeatImportReadinessStepCodes.QiaQuarantineResult)
                 || HasStarted(context, MeatImportReadinessStepCodes.MfdsInspectionResult))
        {
            process = MeatImportReadinessProcessStatusCodes.BorderInspection;
        }
        else if (IsComplete(context, MeatImportReadinessStepCodes.ShipmentColdChain))
        {
            process = MeatImportReadinessProcessStatusCodes.InTransit;
        }
        else if (IsComplete(context, MeatImportReadinessStepCodes.PreShipmentJointCheck))
        {
            process = MeatImportReadinessProcessStatusCodes.ReadyForShipment;
        }
        else if (context.StepStates.Values.Any(state => !string.Equals(state.StatusCode, MeatImportReadinessStepStatusCodes.NotStarted, StringComparison.OrdinalIgnoreCase)))
        {
            process = MeatImportReadinessProcessStatusCodes.Preparing;
        }
        else
        {
            process = MeatImportReadinessProcessStatusCodes.Draft;
        }

        return new ProcessEvaluation(
            process,
            current,
            (int)Math.Round(completed * 100m / steps.Count, MidpointRounding.AwayFromZero),
            openBlocking);
    }

    private static void EnsurePrerequisitesComplete(CaseContext context, MeatImportReadinessStepTemplateResponse template)
    {
        var missing = template.PrerequisiteStepCodes
            .Where(code => !IsStepComplete(MeatImportReadinessTemplateCatalog.FindStep(code), context.StepStates[code]))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"선행 단계가 완료되지 않았습니다: {string.Join(", ", missing)}");
        }
    }

    private static void InvalidateJointConfirmationWhenUpstreamChanged(
        CaseContext context,
        string changedStepCode,
        string actorUserId,
        string actorDisplayName)
    {
        if (!GetAncestors(MeatImportReadinessStepCodes.PreShipmentJointCheck).Contains(changedStepCode, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        var joint = context.StepStates[MeatImportReadinessStepCodes.PreShipmentJointCheck];
        if (joint.Acknowledgements.Count == 0)
        {
            return;
        }

        var previous = joint.StatusCode;
        joint.Acknowledgements.Clear();
        var jointTemplate = MeatImportReadinessTemplateCatalog.FindStep(MeatImportReadinessStepCodes.PreShipmentJointCheck);
        joint.StatusCode = jointTemplate.PrerequisiteStepCodes.All(code => IsComplete(context, code))
            ? MeatImportReadinessStepStatusCodes.WaitingForCounterparty
            : MeatImportReadinessStepStatusCodes.NotStarted;
        AddHistory(
            joint,
            "JointConfirmationInvalidated",
            previous,
            joint.StatusCode,
            $"선행 단계({changedStepCode})가 변경되어 기존 양측 확인을 무효화했습니다.",
            actorUserId,
            actorDisplayName);
    }

    private static IReadOnlyList<string> GetAncestors(string stepCode)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>(MeatImportReadinessTemplateCatalog.FindStep(stepCode).PrerequisiteStepCodes);
        while (stack.TryPop(out var current))
        {
            if (!result.Add(current))
            {
                continue;
            }

            foreach (var parent in MeatImportReadinessTemplateCatalog.FindStep(current).PrerequisiteStepCodes)
            {
                stack.Push(parent);
            }
        }

        return result.ToArray();
    }

    private static bool IsComplete(CaseContext context, string stepCode)
        => IsStepComplete(MeatImportReadinessTemplateCatalog.FindStep(stepCode), context.StepStates[stepCode]);

    private static bool HasStarted(CaseContext context, string stepCode)
        => !string.Equals(context.StepStates[stepCode].StatusCode, MeatImportReadinessStepStatusCodes.NotStarted, StringComparison.OrdinalIgnoreCase);

    private static bool IsStepComplete(MeatImportReadinessStepTemplateResponse template, PersistedStepState state)
    {
        if (string.Equals(state.StatusCode, MeatImportReadinessStepStatusCodes.NotApplicable, StringComparison.OrdinalIgnoreCase))
        {
            return template.CanBeNotApplicable;
        }

        if (template.RequiresOfficialResult)
        {
            return string.Equals(state.StatusCode, MeatImportReadinessStepStatusCodes.OfficialResultRecorded, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(state.StatusCode, MeatImportReadinessStepStatusCodes.ParticipantChecked, StringComparison.OrdinalIgnoreCase)
               || string.Equals(state.StatusCode, MeatImportReadinessStepStatusCodes.OfficialResultRecorded, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCompletionStatusCode(string statusCode)
        => string.Equals(statusCode, MeatImportReadinessStepStatusCodes.ParticipantChecked, StringComparison.OrdinalIgnoreCase)
           || string.Equals(statusCode, MeatImportReadinessStepStatusCodes.OfficialResultRecorded, StringComparison.OrdinalIgnoreCase)
           || string.Equals(statusCode, MeatImportReadinessStepStatusCodes.NotApplicable, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<커뮤니티원장블록담당자Dto> BuildAssignees(
        IReadOnlyList<MeatImportReadinessParticipantResponse> participants,
        MeatImportReadinessStepTemplateResponse step)
    {
        var sideCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (step.LaneCodes.Contains(MeatImportReadinessLaneCodes.KoreanImporter, StringComparer.OrdinalIgnoreCase))
        {
            sideCodes.Add(MeatImportReadinessPartySideCodes.Korean);
        }

        if (step.LaneCodes.Contains(MeatImportReadinessLaneCodes.OverseasCounterparty, StringComparer.OrdinalIgnoreCase))
        {
            sideCodes.Add(MeatImportReadinessPartySideCodes.Overseas);
        }

        return participants
            .Where(participant => participant.UserId is not null && sideCodes.Contains(participant.SideCode))
            .Select(participant => new 커뮤니티원장블록담당자Dto
            {
                UserId = participant.UserId!,
                DisplayName = participant.DisplayName,
                RoleLabel = participant.RoleCode,
                ResponsibilityType = CommunityLedgerBlockResponsibilityTypes.Primary
            })
            .ToArray();
    }

    private static List<MeatImportReadinessParticipantResponse> BuildInitialParticipants(
        CreateMeatImportReadinessCaseRequest request,
        string actorUserId,
        string actorDisplayName,
        string initiatorSideCode)
    {
        var overseasUserId = Clean(request.OverseasCounterparty.UserId);
        var koreanUserId = Clean(request.KoreanImporterUserId);
        var koreanDisplayName = Clean(request.KoreanImporterDisplayName);
        var actorIsOverseas = string.Equals(
            initiatorSideCode,
            MeatImportReadinessPartySideCodes.Overseas,
            StringComparison.OrdinalIgnoreCase);

        if (actorIsOverseas)
        {
            if (overseasUserId is not null
                && !string.Equals(overseasUserId, actorUserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("해외 측이 시작할 때 해외 상대방 사용자 ID는 로그인 사용자와 같아야 합니다.");
            }

            overseasUserId = actorUserId;
            if (string.Equals(koreanUserId, actorUserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("한국 측과 해외 측은 서로 다른 사용자여야 합니다.");
            }
        }
        else
        {
            if (koreanUserId is not null
                && !string.Equals(koreanUserId, actorUserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("한국 측이 시작할 때 한국 수입업자 사용자 ID는 로그인 사용자와 같아야 합니다.");
            }

            koreanUserId = actorUserId;
            if (string.Equals(overseasUserId, actorUserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("한국 측과 해외 측은 서로 다른 사용자여야 합니다.");
            }
        }

        return
        [
            new()
            {
                ParticipantId = $"participant-{Guid.NewGuid():N}",
                UserId = koreanUserId,
                DisplayName = actorIsOverseas ? koreanDisplayName! : actorDisplayName,
                OrganizationName = request.KoreanImporterOrganizationName.Trim(),
                RoleCode = MeatImportReadinessParticipantRoleCodes.KoreanImporter,
                SideCode = MeatImportReadinessPartySideCodes.Korean,
                ParticipationStateCode = koreanUserId is null
                    ? MeatImportReadinessParticipationStateCodes.PendingAccountLink
                    : MeatImportReadinessParticipationStateCodes.Active
            },
            new()
            {
                ParticipantId = $"participant-{Guid.NewGuid():N}",
                UserId = overseasUserId,
                DisplayName = actorIsOverseas ? actorDisplayName : request.OverseasCounterparty.DisplayName.Trim(),
                OrganizationName = request.OverseasCounterparty.OrganizationName.Trim(),
                RoleCode = CanonicalOverseasRole(request.OverseasCounterparty.RoleCode),
                SideCode = MeatImportReadinessPartySideCodes.Overseas,
                ParticipationStateCode = overseasUserId is null
                    ? MeatImportReadinessParticipationStateCodes.PendingAccountLink
                    : MeatImportReadinessParticipationStateCodes.Active,
                EstablishmentNumber = Clean(request.OverseasCounterparty.EstablishmentNumber)
            }
        ];
    }

    private static 커뮤니티원장참여자Dto ToLedgerParticipant(MeatImportReadinessParticipantResponse participant)
        => new()
        {
            UserId = participant.UserId,
            DisplayName = participant.DisplayName,
            RoleLabel = participant.RoleCode,
            ParticipationState = participant.ParticipationStateCode
        };

    private static string ToLedgerState(string processStatusCode)
        => processStatusCode switch
        {
            MeatImportReadinessProcessStatusCodes.Draft => 커뮤니티원장상태.초안,
            MeatImportReadinessProcessStatusCodes.Blocked => 커뮤니티원장상태.보류,
            MeatImportReadinessProcessStatusCodes.Completed => 커뮤니티원장상태.완료,
            _ => 커뮤니티원장상태.진행중
        };

    private static MeatImportReadinessParticipantResponse EnsureParticipant(CaseContext context, string actorUserId)
        => context.Participants.FirstOrDefault(participant =>
               participant.UserId is not null
               && string.Equals(participant.UserId, actorUserId.Trim(), StringComparison.OrdinalIgnoreCase)
               && string.Equals(participant.ParticipationStateCode, MeatImportReadinessParticipationStateCodes.Active, StringComparison.OrdinalIgnoreCase))
           ?? throw new UnauthorizedAccessException("이 육류 수입 준비 작업공간에 참여할 권한이 없습니다.");

    private static void AddHistory(
        PersistedStepState state,
        string eventCode,
        string? previousStatus,
        string status,
        string? note,
        string actorUserId,
        string actorDisplayName)
        => state.History.Add(new MeatImportReadinessStepEventResponse
        {
            EventId = $"event-{Guid.NewGuid():N}",
            EventCode = eventCode,
            PreviousStatusCode = previousStatus,
            StatusCode = status,
            Note = Clean(note),
            ActorUserId = RequireUserId(actorUserId),
            ActorDisplayName = Clean(actorDisplayName) ?? "참여자",
            OccurredAtUtc = DateTime.UtcNow
        });

    private static void ValidateCreateRequest(CreateMeatImportReadinessCaseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) throw new InvalidOperationException("작업공간 제목이 필요합니다.");
        if (!MeatImportReadinessPartySideCodes.IsPrimaryParty(request.InitiatorSideCode)) throw new InvalidOperationException("시작 참여자 측은 Korean 또는 Overseas여야 합니다.");
        if (!MeatImportReadinessProductTypeCodes.IsSupported(request.ProductTypeCode)) throw new InvalidOperationException("현재 준비도 템플릿은 소고기(Beef)와 돼지고기(Pork)를 지원합니다.");
        if (string.IsNullOrWhiteSpace(request.ProductName)) throw new InvalidOperationException("제품명이 필요합니다.");
        var hsCode = NormalizeHsCode(request.HsCode);
        if (hsCode.Length is < 6 or > 10) throw new InvalidOperationException("HS 코드는 숫자 6~10자리로 입력해 주세요.");
        if (string.IsNullOrWhiteSpace(request.OriginCountryCode) || request.OriginCountryCode.Trim().Length != 2) throw new InvalidOperationException("원산지 국가 코드는 ISO 알파-2 두 자리로 입력해 주세요.");
        if (string.IsNullOrWhiteSpace(request.OriginCountryName)) throw new InvalidOperationException("원산지 국가명이 필요합니다.");
        if (string.IsNullOrWhiteSpace(request.KoreanImporterOrganizationName)) throw new InvalidOperationException("한국 수입업자 조직명이 필요합니다.");
        if (string.Equals(request.InitiatorSideCode, MeatImportReadinessPartySideCodes.Overseas, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(request.KoreanImporterDisplayName)) throw new InvalidOperationException("해외 측이 시작할 때 한국 수입업자 표시명이 필요합니다.");
        if (request.OverseasCounterparty is null) throw new InvalidOperationException("해외 상대방 정보가 필요합니다.");
        if (string.IsNullOrWhiteSpace(request.OverseasCounterparty.DisplayName)) throw new InvalidOperationException("해외 상대방 표시명이 필요합니다.");
        if (string.IsNullOrWhiteSpace(request.OverseasCounterparty.OrganizationName)) throw new InvalidOperationException("해외 상대방 조직명이 필요합니다.");
        if (!MeatImportReadinessParticipantRoleCodes.IsOverseasCounterparty(request.OverseasCounterparty.RoleCode)) throw new InvalidOperationException("해외 상대방 역할은 수출자 또는 해외 작업장이어야 합니다.");
    }

    private static void EnsureExpectedRevision(long expected, long actual)
    {
        if (expected <= 0 || expected != actual)
        {
            throw new MeatImportReadinessConcurrencyException(
                "작업공간이 다른 참여자에 의해 변경되었습니다. 최신 내용을 다시 조회해 주세요.",
                new InvalidOperationException($"ExpectedRevision={expected}, ActualRevision={actual}"));
        }
    }

    private static string CanonicalProductType(string value)
        => string.Equals(value.Trim(), MeatImportReadinessProductTypeCodes.Beef, StringComparison.OrdinalIgnoreCase)
            ? MeatImportReadinessProductTypeCodes.Beef
            : MeatImportReadinessProductTypeCodes.Pork;

    private static string CanonicalOverseasRole(string value)
        => string.Equals(value.Trim(), MeatImportReadinessParticipantRoleCodes.OverseasEstablishment, StringComparison.OrdinalIgnoreCase)
            ? MeatImportReadinessParticipantRoleCodes.OverseasEstablishment
            : MeatImportReadinessParticipantRoleCodes.OverseasExporter;

    private static string CanonicalPartySide(string value)
        => string.Equals(value.Trim(), MeatImportReadinessPartySideCodes.Overseas, StringComparison.OrdinalIgnoreCase)
            ? MeatImportReadinessPartySideCodes.Overseas
            : MeatImportReadinessPartySideCodes.Korean;

    private static string CanonicalStepStatus(string? value)
        => MeatImportReadinessStepStatusCodes.All.FirstOrDefault(item => string.Equals(item, value?.Trim(), StringComparison.OrdinalIgnoreCase))
           ?? throw new InvalidOperationException("지원하지 않는 단계 상태입니다.");

    private static string CanonicalDiscussionKind(string? value)
        => MeatImportReadinessDiscussionKindCodes.All.FirstOrDefault(item => string.Equals(item, value?.Trim(), StringComparison.OrdinalIgnoreCase))
           ?? throw new InvalidOperationException("지원하지 않는 질문·이의 종류입니다.");

    private static string ResolveDisplayName(MeatImportReadinessParticipantResponse participant, string supplied)
        => Clean(supplied) ?? participant.DisplayName;

    private static string RequireUserId(string? value)
        => Clean(value) ?? throw new UnauthorizedAccessException("로그인 사용자 식별자를 확인할 수 없습니다.");

    private static string RequireCaseId(string? value)
        => Clean(value) ?? throw new InvalidOperationException("작업공간 ID가 필요합니다.");

    private static string NormalizeHsCode(string? value)
        => string.Concat((value ?? string.Empty).Where(char.IsDigit));

    private static bool IsReadinessLedger(커뮤니티원장Dto ledger)
        => string.Equals(ledger.원장템플릿Key, MeatImportReadinessCodes.LedgerTemplateKey, StringComparison.OrdinalIgnoreCase);

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record CaseContext(
        커뮤니티원장Dto Ledger,
        PersistedCaseMetadata Metadata,
        List<MeatImportReadinessParticipantResponse> Participants,
        Dictionary<string, PersistedStepState> StepStates);

    private sealed record ProcessEvaluation(
        string ProcessStatusCode,
        string CurrentStepCode,
        int ReadinessPercent,
        int OpenBlockingIssueCount);

    private sealed class PersistedCaseMetadata
    {
        public long? SourceCommunityPostId { get; set; }
        public string InitiatorSideCode { get; set; } = MeatImportReadinessPartySideCodes.Korean;
        public string ProductTypeCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string HsCode { get; set; } = string.Empty;
        public string OriginCountryCode { get; set; } = string.Empty;
        public string OriginCountryName { get; set; } = string.Empty;
        public string? ProductSpecification { get; set; }
        public string KoreanImporterOrganizationName { get; set; } = string.Empty;
    }

    private sealed class PersistedStepState
    {
        public string StatusCode { get; set; } = MeatImportReadinessStepStatusCodes.NotStarted;
        public string? StatusBeforeBlock { get; set; }
        public string? LastNote { get; set; }
        public string? OfficialReferenceNumber { get; set; }
        public DateOnly? OfficialResultDate { get; set; }
        public List<MeatImportReadinessEvidenceResponse> Evidences { get; set; } = [];
        public List<MeatImportReadinessDiscussionResponse> Discussions { get; set; } = [];
        public List<MeatImportReadinessAcknowledgementResponse> Acknowledgements { get; set; } = [];
        public List<MeatImportReadinessStepEventResponse> History { get; set; } = [];
    }
}
