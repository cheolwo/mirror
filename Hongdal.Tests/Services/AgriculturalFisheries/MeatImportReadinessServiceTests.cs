using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Contracts.Common.Community;
using Hongdal.Services.AgriculturalFisheries.ImportReadiness;
using Hongdal.Services.Community;
using Hongdal.Controllers.Common;
using Microsoft.AspNetCore.Authorization;

namespace Hongdal.Tests.Services.AgriculturalFisheries;

public sealed class MeatImportReadinessServiceTests
{
    [Fact]
    public void 공개절차도와_참여자전용작업공간의_인증경계를_분리한다()
    {
        var controllerType = typeof(MeatImportReadinessController);
        var diagramAction = controllerType.GetMethod(nameof(MeatImportReadinessController.GetDiagram))!;
        Assert.NotNull(diagramAction.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).SingleOrDefault());

        var protectedActions = new[]
        {
            nameof(MeatImportReadinessController.ListMine),
            nameof(MeatImportReadinessController.GetCase),
            nameof(MeatImportReadinessController.CreateCase),
            nameof(MeatImportReadinessController.UpdateStepStatus),
            nameof(MeatImportReadinessController.AddEvidence),
            nameof(MeatImportReadinessController.AddDiscussion),
            nameof(MeatImportReadinessController.ResolveDiscussion),
            nameof(MeatImportReadinessController.AcknowledgeStep)
        };
        Assert.All(protectedActions, actionName =>
        {
            var action = controllerType.GetMethod(actionName)!;
            Assert.NotNull(action.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).SingleOrDefault());
        });
    }

    [Fact]
    public void 절차도는_공식기관관문과_양측확인_정보제공경계를_명시한다()
    {
        var diagram = MeatImportReadinessTemplateCatalog.Get();

        Assert.True(diagram.InformationOnly);
        Assert.False(diagram.IsBrokerageEnabled);
        Assert.Equal(13, diagram.Steps.Count);
        Assert.Equal(diagram.Steps.Count, diagram.Diagram.Nodes.Count);
        Assert.Equal(
            diagram.Diagram.Nodes.Count,
            diagram.Diagram.Nodes.Select(node => (node.X, node.Y)).Distinct().Count());
        Assert.Contains(diagram.Steps, step =>
            step.Code == MeatImportReadinessStepCodes.PreShipmentJointCheck
            && step.RequiresJointConfirmation
            && step.PrerequisiteStepCodes.Count == 3);
        Assert.Contains(diagram.Steps, step =>
            step.Code == MeatImportReadinessStepCodes.QiaQuarantineResult
            && step.RequiresOfficialResult
            && step.LiveRecheckRequired);
        Assert.Contains(diagram.Sources, source =>
            source.Key == MeatImportReadinessSourceKeys.QiaEligibleCountries
            && source.LiveCheckRequired);
        Assert.Contains("대신", diagram.OfficialDecisionBoundary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 생성하면_한국측과_해외측이_같은원장과_다이어그램을_공유한다()
    {
        var service = new MeatImportReadinessService(new InMemoryLedgerStore());

        var created = await service.CreateCaseAsync(CreateRequest(), "importer-1", "한국 담당자");

        Assert.Equal(1, created.Revision);
        Assert.Equal(MeatImportReadinessProcessStatusCodes.Draft, created.ProcessStatusCode);
        Assert.Equal(MeatImportReadinessStepCodes.ProductScope, created.CurrentStepCode);
        Assert.Equal(2, created.Participants.Count);
        Assert.Contains(created.Participants, participant =>
            participant.UserId == "importer-1"
            && participant.SideCode == MeatImportReadinessPartySideCodes.Korean);
        Assert.Contains(created.Participants, participant =>
            participant.UserId == "exporter-1"
            && participant.SideCode == MeatImportReadinessPartySideCodes.Overseas);
        Assert.Equal(created.CaseId, created.Diagram.LedgerId);
        Assert.Equal(DiagramLedgerRoomIds.Build(created.CaseId), created.CollaborationRoomId);
        Assert.True(created.InformationOnly);
        Assert.False(created.IsBrokerageEnabled);
    }

    [Fact]
    public async Task 공식기관단계는_참여자확인만으로_완료할수없고_공식참조가필요하다()
    {
        var service = new MeatImportReadinessService(new InMemoryLedgerStore());
        var current = await service.CreateCaseAsync(CreateRequest(), "importer-1", "한국 담당자");
        current = await SetStatus(service, current, MeatImportReadinessStepCodes.ProductScope, MeatImportReadinessStepStatusCodes.ParticipantChecked, "importer-1", "한국 담당자");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateStepStatusAsync(
            current.CaseId,
            MeatImportReadinessStepCodes.CountryProductEligibility,
            new UpdateMeatImportReadinessStepStatusRequest
            {
                ExpectedRevision = current.Revision,
                StatusCode = MeatImportReadinessStepStatusCodes.ParticipantChecked
            },
            "importer-1",
            "한국 담당자"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateStepStatusAsync(
            current.CaseId,
            MeatImportReadinessStepCodes.CountryProductEligibility,
            new UpdateMeatImportReadinessStepStatusRequest
            {
                ExpectedRevision = current.Revision,
                StatusCode = MeatImportReadinessStepStatusCodes.OfficialResultRecorded
            },
            "importer-1",
            "한국 담당자"));

        var recorded = await service.UpdateStepStatusAsync(
            current.CaseId,
            MeatImportReadinessStepCodes.CountryProductEligibility,
            new UpdateMeatImportReadinessStepStatusRequest
            {
                ExpectedRevision = current.Revision,
                StatusCode = MeatImportReadinessStepStatusCodes.OfficialResultRecorded,
                OfficialReferenceNumber = "QIA-LIVE-CHECK-20260715"
            },
            "importer-1",
            "한국 담당자");

        Assert.True(recorded.Steps.Single(step => step.StepCode == MeatImportReadinessStepCodes.CountryProductEligibility).CompletionSatisfied);
    }

    [Fact]
    public async Task 선적전관문은_양측확인이필요하고_선행이의가생기면_기존확인을무효화한다()
    {
        var service = new MeatImportReadinessService(new InMemoryLedgerStore());
        var current = await PreparePreShipmentGateAsync(service);

        current = await service.AcknowledgeStepAsync(
            current.CaseId,
            MeatImportReadinessStepCodes.PreShipmentJointCheck,
            new AcknowledgeMeatImportReadinessStepRequest
            {
                ExpectedRevision = current.Revision,
                Statement = "한국 측 확인 완료"
            },
            "importer-1",
            "한국 담당자");

        var gate = current.Steps.Single(step => step.StepCode == MeatImportReadinessStepCodes.PreShipmentJointCheck);
        Assert.Equal(MeatImportReadinessStepStatusCodes.WaitingForCounterparty, gate.StatusCode);
        Assert.Single(gate.Acknowledgements);

        current = await service.AcknowledgeStepAsync(
            current.CaseId,
            MeatImportReadinessStepCodes.PreShipmentJointCheck,
            new AcknowledgeMeatImportReadinessStepRequest
            {
                ExpectedRevision = current.Revision,
                Statement = "해외 측 확인 완료"
            },
            "exporter-1",
            "해외 담당자");

        gate = current.Steps.Single(step => step.StepCode == MeatImportReadinessStepCodes.PreShipmentJointCheck);
        Assert.Equal(MeatImportReadinessStepStatusCodes.ParticipantChecked, gate.StatusCode);
        Assert.Equal(2, gate.Acknowledgements.Count);
        Assert.Equal(MeatImportReadinessProcessStatusCodes.ReadyForShipment, current.ProcessStatusCode);

        current = await service.AddDiscussionAsync(
            current.CaseId,
            MeatImportReadinessStepCodes.DocumentAndLabelPack,
            new AddMeatImportReadinessDiscussionRequest
            {
                ExpectedRevision = current.Revision,
                KindCode = MeatImportReadinessDiscussionKindCodes.Objection,
                Message = "패킹리스트와 라벨의 순중량이 다릅니다."
            },
            "exporter-1",
            "해외 담당자");

        gate = current.Steps.Single(step => step.StepCode == MeatImportReadinessStepCodes.PreShipmentJointCheck);
        Assert.Empty(gate.Acknowledgements);
        Assert.Equal(MeatImportReadinessStepStatusCodes.NotStarted, gate.StatusCode);
        Assert.Equal(MeatImportReadinessProcessStatusCodes.Blocked, current.ProcessStatusCode);
        Assert.Equal(1, current.OpenBlockingIssueCount);
    }

    [Fact]
    public async Task 참여자가아닌사용자는_작업공간을조회하거나수정할수없다()
    {
        var service = new MeatImportReadinessService(new InMemoryLedgerStore());
        var created = await service.CreateCaseAsync(CreateRequest(), "importer-1", "한국 담당자");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetCaseAsync(created.CaseId, "stranger-1"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AddEvidenceAsync(
            created.CaseId,
            MeatImportReadinessStepCodes.ProductScope,
            new AddMeatImportReadinessEvidenceRequest
            {
                ExpectedRevision = created.Revision,
                EvidenceCode = "ProductSpecification",
                Title = "제품 규격서"
            },
            "stranger-1",
            "외부 사용자"));
    }

    private static async Task<MeatImportReadinessCaseResponse> PreparePreShipmentGateAsync(IMeatImportReadinessService service)
    {
        var current = await service.CreateCaseAsync(CreateRequest(), "importer-1", "한국 담당자");
        current = await SetStatus(service, current, MeatImportReadinessStepCodes.ProductScope, MeatImportReadinessStepStatusCodes.ParticipantChecked, "importer-1", "한국 담당자");
        current = await SetOfficialStatus(service, current, MeatImportReadinessStepCodes.CountryProductEligibility, "QIA-ELIGIBLE");
        current = await SetOfficialStatus(service, current, MeatImportReadinessStepCodes.ForeignEstablishmentEligibility, "EST-KR-1234");
        current = await SetOfficialStatus(service, current, MeatImportReadinessStepCodes.ImporterRegistration, "IMPORT-BUSINESS-1234");
        current = await SetOfficialStatus(service, current, MeatImportReadinessStepCodes.ExportCertificatePlan, "CERT-FORM-US-KR");
        current = await SetStatus(service, current, MeatImportReadinessStepCodes.DocumentAndLabelPack, MeatImportReadinessStepStatusCodes.ParticipantChecked, "importer-1", "한국 담당자");
        return current;
    }

    private static Task<MeatImportReadinessCaseResponse> SetOfficialStatus(
        IMeatImportReadinessService service,
        MeatImportReadinessCaseResponse current,
        string stepCode,
        string referenceNumber)
        => service.UpdateStepStatusAsync(
            current.CaseId,
            stepCode,
            new UpdateMeatImportReadinessStepStatusRequest
            {
                ExpectedRevision = current.Revision,
                StatusCode = MeatImportReadinessStepStatusCodes.OfficialResultRecorded,
                OfficialReferenceNumber = referenceNumber
            },
            "importer-1",
            "한국 담당자");

    private static Task<MeatImportReadinessCaseResponse> SetStatus(
        IMeatImportReadinessService service,
        MeatImportReadinessCaseResponse current,
        string stepCode,
        string statusCode,
        string userId,
        string displayName)
        => service.UpdateStepStatusAsync(
            current.CaseId,
            stepCode,
            new UpdateMeatImportReadinessStepStatusRequest
            {
                ExpectedRevision = current.Revision,
                StatusCode = statusCode
            },
            userId,
            displayName);

    private static CreateMeatImportReadinessCaseRequest CreateRequest()
        => new()
        {
            Title = "미국산 냉동 돼지고기 수입 준비",
            ProductTypeCode = MeatImportReadinessProductTypeCodes.Pork,
            ProductName = "냉동 돼지고기",
            HsCode = "0203.29-9000",
            OriginCountryCode = "US",
            OriginCountryName = "미국",
            ProductSpecification = "냉동, 20kg carton",
            KoreanImporterOrganizationName = "한국 수입사",
            OverseasCounterparty = new CreateMeatImportReadinessCounterpartyRequest
            {
                UserId = "exporter-1",
                DisplayName = "해외 담당자",
                OrganizationName = "US Exporter",
                RoleCode = MeatImportReadinessParticipantRoleCodes.OverseasExporter,
                EstablishmentNumber = "EST-1234"
            }
        };

    private sealed class InMemoryLedgerStore : I커뮤니티원장저장소
    {
        private readonly Dictionary<string, 커뮤니티원장Dto> _items = new(StringComparer.OrdinalIgnoreCase);

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
        {
            var items = _items.Values
                .Where(item => query.원장템플릿Key is null || string.Equals(item.원장템플릿Key, query.원장템플릿Key, StringComparison.OrdinalIgnoreCase))
                .Where(item => query.참여자UserId is null || item.참여자목록.Any(participant => string.Equals(participant.UserId, query.참여자UserId, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(item => item.수정시각Utc)
                .Take(query.Limit)
                .ToArray();
            return Task.FromResult<IReadOnlyList<커뮤니티원장Dto>>(items);
        }

        public Task<커뮤니티원장Dto?> 원장상태변경Async(
            커뮤니티원장상태변경요청 request,
            string updatedBy,
            CancellationToken cancellationToken = default)
            => Task.FromResult<커뮤니티원장Dto?>(null);
    }
}
