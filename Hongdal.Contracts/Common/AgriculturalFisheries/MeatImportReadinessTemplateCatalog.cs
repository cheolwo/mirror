using Hongdal.Contracts.Common.Community;

namespace Hongdal.Contracts.Common.AgriculturalFisheries;

public static class MeatImportReadinessTemplateCatalog
{
    private static readonly IReadOnlyList<MeatImportReadinessLaneResponse> Lanes =
    [
        Lane(MeatImportReadinessLaneCodes.KoreanImporter, "한국 수입업자", "국내 영업등록, 수입신고, 표시·이력과 최종 반출 준비를 확인합니다.", 1),
        Lane(MeatImportReadinessLaneCodes.OverseasCounterparty, "해외 수출자·작업장", "제품 규격, 작업장 정보, 수출국 증명과 선적 자료를 준비합니다.", 2),
        Lane(MeatImportReadinessLaneCodes.ExportingAuthority, "수출국 정부기관", "한국과 합의된 검역·위생증명서와 작업장 승인 상태를 관리합니다.", 3),
        Lane(MeatImportReadinessLaneCodes.Qia, "농림축산검역본부", "수입 가능 국가·품목, 검역증명서와 입항 검역 결과를 확인합니다.", 4),
        Lane(MeatImportReadinessLaneCodes.Mfds, "식품의약품안전처", "수입위생평가, 해외작업장 등록, 수입신고와 식품검사 결과를 확인합니다.", 5),
        Lane(MeatImportReadinessLaneCodes.Customs, "관세청", "요건확인, 세액·FTA 검토와 수입신고 수리 여부를 확인합니다.", 6),
        Lane(MeatImportReadinessLaneCodes.DomesticDistribution, "국내 유통 준비", "한글 표시, 축산물이력과 냉장·냉동 유통 인계 조건을 확인합니다.", 7)
    ];

    private static readonly IReadOnlyList<MeatImportReadinessSourceResponse> Sources =
    [
        Source(
            MeatImportReadinessSourceKeys.MfdsImportedFoodSafety,
            "식품의약품안전처",
            "수입식품 안전관리와 해외작업장 등록",
            "https://www.mfds.go.kr/eng/wpge/m_11/de011002l001.do",
            "축산물 수입위생평가, 해외작업장 등록과 수입 전 안전관리 절차를 확인합니다.",
            true),
        Source(
            MeatImportReadinessSourceKeys.ImportedFoodInformationMaru,
            "식품의약품안전처",
            "수입식품정보마루",
            "https://impfood.mfds.go.kr/",
            "해외작업장 등록·정지 상태와 수입식품 관련 최신 정보를 선적 전에 다시 확인합니다.",
            true),
        Source(
            MeatImportReadinessSourceKeys.QiaEligibleCountries,
            "농림축산검역본부",
            "한국 수출 가능 국가·품목 목록",
            "https://www.qia.go.kr/english/html/listqiaEngNoticeWebAction.do?clear=1&type=21",
            "가축질병에 따른 일시 수입중단이 생길 수 있으므로 계약 시점과 선적 직전에 모두 확인합니다.",
            true),
        Source(
            MeatImportReadinessSourceKeys.QiaCertificateStatus,
            "농림축산검역본부",
            "국가별 검역증명서 승인 현황",
            "https://www.qia.go.kr/livestock/qua/listSxzgWebAction.do",
            "국가·품목별 승인 증명서 서식과 세부 검역조건을 확인합니다.",
            true),
        Source(
            MeatImportReadinessSourceKeys.ImportedFoodAct,
            "국가법령정보센터",
            "수입식품안전관리 특별법",
            "https://law.go.kr/LSW/lsInfoP.do?lsId=012247",
            "영업등록, 해외작업장, 수입신고 등 법적 근거의 현행 조문을 확인합니다.",
            true),
        Source(
            MeatImportReadinessSourceKeys.CustomsLivestockImport,
            "관세청",
            "축산물 수입 통관 안내",
            "https://www.customs.go.kr/call/ad/crmcc/selectFaqViewPage.do?mi=6822&cnslKnwlSrno=442",
            "축산물 요건확인과 세관 수입통관의 기본 연결 관계를 확인합니다.",
            true)
    ];

    private static readonly IReadOnlyList<MeatImportReadinessStepTemplateResponse> Steps =
    [
        Step(
            MeatImportReadinessStepCodes.ProductScope,
            1,
            MeatImportReadinessPhaseCodes.Eligibility,
            "대상 확정",
            "제품·HS·보관조건 확정",
            "소고기·돼지고기 여부, 가공 정도, HS 코드, 냉장·냉동 조건과 포장 규격을 양측이 같은 내용으로 정리합니다.",
            [MeatImportReadinessLaneCodes.KoreanImporter, MeatImportReadinessLaneCodes.OverseasCounterparty],
            [],
            ["ProductSpecification", "HsClassificationBasis"],
            [],
            "제품명만 같고 가공 정도나 부위·포장 규격이 다른 부분은 없나요?"),
        Step(
            MeatImportReadinessStepCodes.CountryProductEligibility,
            2,
            MeatImportReadinessPhaseCodes.Eligibility,
            "대상 확정",
            "국가·품목 수입 가능성 확인",
            "수출국과 대상 육류가 한국의 검역·수입위생 요건상 허용 대상인지 공식 목록에서 확인합니다.",
            [MeatImportReadinessLaneCodes.KoreanImporter, MeatImportReadinessLaneCodes.Qia, MeatImportReadinessLaneCodes.Mfds],
            [MeatImportReadinessStepCodes.ProductScope],
            ["EligibleCountryProductCheck"],
            [MeatImportReadinessSourceKeys.QiaEligibleCountries, MeatImportReadinessSourceKeys.MfdsImportedFoodSafety],
            "어느 공식 목록을 언제 확인했고, 일시 중단이나 지역 제한은 없었나요?",
            requiresOfficialResult: true,
            liveRecheckRequired: true),
        Step(
            MeatImportReadinessStepCodes.ForeignEstablishmentEligibility,
            3,
            MeatImportReadinessPhaseCodes.Eligibility,
            "대상 확정",
            "해외 작업장 등록·승인 확인",
            "도축·절단·가공·보관 작업장이 한국 수출 대상 작업장으로 등록·승인되어 있고 정지 상태가 아닌지 확인합니다.",
            [MeatImportReadinessLaneCodes.OverseasCounterparty, MeatImportReadinessLaneCodes.ExportingAuthority, MeatImportReadinessLaneCodes.Qia, MeatImportReadinessLaneCodes.Mfds],
            [MeatImportReadinessStepCodes.ProductScope],
            ["ForeignEstablishmentRegistration", "ExportEstablishmentApproval"],
            [MeatImportReadinessSourceKeys.ImportedFoodInformationMaru, MeatImportReadinessSourceKeys.QiaCertificateStatus],
            "수출 증명서에 들어갈 작업장 번호와 한국 등록 번호가 서로 일치하나요?",
            requiresOfficialResult: true,
            liveRecheckRequired: true),
        Step(
            MeatImportReadinessStepCodes.ImporterRegistration,
            4,
            MeatImportReadinessPhaseCodes.Eligibility,
            "대상 확정",
            "한국 수입자 영업 준비 확인",
            "한국 측 수입식품등 수입·판매업 등록, 위생교육과 신고 주체를 확인합니다.",
            [MeatImportReadinessLaneCodes.KoreanImporter, MeatImportReadinessLaneCodes.Mfds],
            [MeatImportReadinessStepCodes.ProductScope],
            ["ImporterBusinessRegistration"],
            [MeatImportReadinessSourceKeys.ImportedFoodAct],
            "실제 수입신고인과 영업등록 명의가 일치하나요?",
            requiresOfficialResult: true),
        Step(
            MeatImportReadinessStepCodes.ExportCertificatePlan,
            5,
            MeatImportReadinessPhaseCodes.PreShipment,
            "선적 전 준비",
            "수출국 증명서·위생조건 준비",
            "국가·품목별로 합의된 검역·위생증명서 서식, 발급 기관과 필수 문구를 선적 전에 확인합니다.",
            [MeatImportReadinessLaneCodes.OverseasCounterparty, MeatImportReadinessLaneCodes.ExportingAuthority, MeatImportReadinessLaneCodes.Qia],
            [MeatImportReadinessStepCodes.CountryProductEligibility, MeatImportReadinessStepCodes.ForeignEstablishmentEligibility],
            ["ExportHealthCertificatePlan", "ApprovedCertificateForm"],
            [MeatImportReadinessSourceKeys.QiaCertificateStatus],
            "누가 어느 시점에 원본 증명서를 발급하고 한국 측에 전달하나요?",
            requiresOfficialResult: true,
            liveRecheckRequired: true),
        Step(
            MeatImportReadinessStepCodes.DocumentAndLabelPack,
            6,
            MeatImportReadinessPhaseCodes.PreShipment,
            "선적 전 준비",
            "제품·표시·상업서류 묶음 정합성 확인",
            "인보이스, 패킹리스트, 원산지, 제조·유통기한, 로트, 한글표시 기초정보가 서로 충돌하지 않는지 확인합니다.",
            [MeatImportReadinessLaneCodes.KoreanImporter, MeatImportReadinessLaneCodes.OverseasCounterparty],
            [MeatImportReadinessStepCodes.ProductScope],
            ["CommercialInvoiceDraft", "PackingListDraft", "LabelDataSheet", "OriginEvidence"],
            [MeatImportReadinessSourceKeys.ImportedFoodAct],
            "제품명·중량·로트·작업장 번호가 모든 문서에서 같은가요?"),
        Step(
            MeatImportReadinessStepCodes.PreShipmentJointCheck,
            7,
            MeatImportReadinessPhaseCodes.PreShipment,
            "선적 전 준비",
            "선적 전 양측 공동 확인",
            "한국 수입자와 해외 측이 동일한 최신 절차도와 증빙 목록을 보고 미해결 이의가 없음을 각각 확인합니다.",
            [MeatImportReadinessLaneCodes.KoreanImporter, MeatImportReadinessLaneCodes.OverseasCounterparty],
            [MeatImportReadinessStepCodes.ImporterRegistration, MeatImportReadinessStepCodes.ExportCertificatePlan, MeatImportReadinessStepCodes.DocumentAndLabelPack],
            [],
            [],
            "양측이 다르게 이해한 조건이나 아직 답을 받지 못한 질문이 하나라도 있나요?",
            requiresJointConfirmation: true),
        Step(
            MeatImportReadinessStepCodes.ShipmentColdChain,
            8,
            MeatImportReadinessPhaseCodes.ShipmentAndEntry,
            "선적·입항",
            "선적·콜드체인 기록",
            "컨테이너, 봉인, 선하증권, 출항·입항 예정과 온도관리 기록의 연결 정보를 남깁니다.",
            [MeatImportReadinessLaneCodes.OverseasCounterparty, MeatImportReadinessLaneCodes.KoreanImporter],
            [MeatImportReadinessStepCodes.PreShipmentJointCheck],
            ["BillOfLading", "ContainerSeal", "ColdChainRecord"],
            [],
            "문서상 컨테이너·봉인 번호와 실제 화물이 일치하나요?"),
        Step(
            MeatImportReadinessStepCodes.QiaQuarantineResult,
            9,
            MeatImportReadinessPhaseCodes.ShipmentAndEntry,
            "선적·입항",
            "동·축산물 검역 결과 기록",
            "입항 후 농림축산검역본부의 검역 접수와 합격·보완·불합격 결과를 공식 참조번호와 함께 기록합니다.",
            [MeatImportReadinessLaneCodes.KoreanImporter, MeatImportReadinessLaneCodes.Qia],
            [MeatImportReadinessStepCodes.ShipmentColdChain],
            ["QiaQuarantineCertificate", "QiaResult"],
            [MeatImportReadinessSourceKeys.QiaEligibleCountries, MeatImportReadinessSourceKeys.QiaCertificateStatus],
            "검역 결과의 조건·보완사항이 식약처 신고 자료에도 반영되었나요?",
            requiresOfficialResult: true,
            liveRecheckRequired: true),
        Step(
            MeatImportReadinessStepCodes.MfdsInspectionResult,
            10,
            MeatImportReadinessPhaseCodes.ShipmentAndEntry,
            "선적·입항",
            "수입신고·식품검사 결과 기록",
            "식약처 수입신고와 서류·현장·정밀검사 결과를 공식 접수·처리 정보로 기록합니다.",
            [MeatImportReadinessLaneCodes.KoreanImporter, MeatImportReadinessLaneCodes.Mfds],
            [MeatImportReadinessStepCodes.ShipmentColdChain],
            ["MfdsImportDeclaration", "MfdsInspectionResult"],
            [MeatImportReadinessSourceKeys.MfdsImportedFoodSafety, MeatImportReadinessSourceKeys.ImportedFoodAct],
            "검사 종류와 보완 요청, 처리 기한을 양측이 같은 일정으로 보고 있나요?",
            requiresOfficialResult: true,
            liveRecheckRequired: true),
        Step(
            MeatImportReadinessStepCodes.CustomsClearanceResult,
            11,
            MeatImportReadinessPhaseCodes.ShipmentAndEntry,
            "선적·입항",
            "세관 통관 결과 기록",
            "검역·식품검사 결과와 수입신고, 과세·FTA 검토를 연결하고 세관 수리 결과를 기록합니다.",
            [MeatImportReadinessLaneCodes.KoreanImporter, MeatImportReadinessLaneCodes.Customs],
            [MeatImportReadinessStepCodes.QiaQuarantineResult, MeatImportReadinessStepCodes.MfdsInspectionResult],
            ["CustomsImportDeclaration", "CustomsReleaseResult"],
            [MeatImportReadinessSourceKeys.CustomsLivestockImport],
            "검역·검사 결과와 세관 신고의 품명·수량·가격·원산지가 일치하나요?",
            requiresOfficialResult: true,
            liveRecheckRequired: true),
        Step(
            MeatImportReadinessStepCodes.LabelTraceabilityRelease,
            12,
            MeatImportReadinessPhaseCodes.DomesticRelease,
            "국내 반출 준비",
            "한글 표시·축산물이력 준비",
            "한글 표시사항, 원산지 표시, 수입축산물 이력번호와 국내 보관·판매 기록 준비 여부를 확인합니다.",
            [MeatImportReadinessLaneCodes.KoreanImporter, MeatImportReadinessLaneCodes.DomesticDistribution],
            [MeatImportReadinessStepCodes.CustomsClearanceResult],
            ["KoreanLabelReview", "LivestockTraceabilityNumber"],
            [MeatImportReadinessSourceKeys.ImportedFoodAct],
            "실물 포장과 시스템의 로트·이력번호가 끝까지 연결되나요?"),
        Step(
            MeatImportReadinessStepCodes.DistributionReleaseCheck,
            13,
            MeatImportReadinessPhaseCodes.DomesticRelease,
            "국내 반출 준비",
            "국내 유통 인계 준비 확인",
            "정부기관 결과와 표시·이력 준비가 모두 갖춰진 뒤에만 국내 보관·운송·판매 단계로 넘길 준비가 되었음을 표시합니다.",
            [MeatImportReadinessLaneCodes.KoreanImporter, MeatImportReadinessLaneCodes.DomesticDistribution],
            [MeatImportReadinessStepCodes.LabelTraceabilityRelease],
            ["DomesticReleaseChecklist"],
            [],
            "공식 반출 허용과 내부 준비 완료를 혼동하고 있지는 않나요?")
    ];

    public static MeatImportReadinessDiagramResponse Get()
        => new()
        {
            TemplateVersion = MeatImportReadinessCodes.TemplateVersion,
            Title = "한국 육류 수입 준비도 협업 절차도",
            Summary = "한국 수입업자와 해외 수출자·작업장이 같은 단계, 증빙, 질문과 확인 상태를 보면서 소고기·돼지고기 수입 준비를 정리하는 정보 제공용 절차도입니다.",
            LastReviewedOn = new DateOnly(2026, 7, 15),
            InformationOnly = true,
            IsBrokerageEnabled = false,
            OfficialDecisionBoundary = "홍달의 상태 표시는 참여자가 기록한 준비 현황이며, 정부기관의 승인·검역·검사·통관 결정을 대신하거나 보증하지 않습니다.",
            JointConfirmationPolicy = "선적 전 공동 확인은 한국 측과 해외 측이 각각 확인하고, 선행 단계에 미해결 차단 이의가 없을 때만 완료됩니다. 선행 정보가 바뀌면 기존 확인은 무효화됩니다.",
            Lanes = Lanes,
            Steps = Steps,
            Sources = Sources,
            Diagram = BuildDiagram(),
            Notices =
            [
                "국가·품목·작업장 허용 상태와 검역증명서 요건은 질병 발생이나 제도 변경으로 달라질 수 있으므로 선적 직전에 공식 원문을 다시 확인해야 합니다.",
                "증빙은 파일 원문이 아니라 문서번호·발급기관·보관 위치 같은 메타데이터를 먼저 기록하며, 민감정보가 있는 파일은 권한이 통제된 문서 저장소를 사용해야 합니다.",
                "이 절차도는 계약, 발주, 통관 대행, 운송 주선이나 수수료 정산을 실행하지 않습니다."
            ]
        };

    public static MeatImportReadinessStepTemplateResponse FindStep(string stepCode)
        => Steps.FirstOrDefault(step => string.Equals(step.Code, stepCode?.Trim(), StringComparison.OrdinalIgnoreCase))
           ?? throw new KeyNotFoundException($"수입 준비 절차 단계를 찾을 수 없습니다. StepCode={stepCode}");

    private static DiagramSnapshotDto BuildDiagram()
    {
        var laneOrder = Lanes.ToDictionary(lane => lane.Code, lane => lane.DisplayOrder, StringComparer.OrdinalIgnoreCase);
        var nodes = Steps.Select(step =>
        {
            var mainLane = step.LaneCodes[0];
            return new DiagramNodeDto
            {
                NodeId = step.Code,
                Kind = step.RequiresJointConfirmation ? "JointGate" : step.RequiresOfficialResult ? "OfficialGate" : "ReadinessStep",
                Title = step.Title,
                GroupLabel = step.PhaseName,
                Description = step.Description,
                X = 80 + (step.Sequence - 1) * 300,
                Y = 60 + laneOrder[mainLane] * 150,
                Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["stepCode"] = step.Code,
                    ["phaseCode"] = step.PhaseCode,
                    ["laneCodes"] = string.Join(',', step.LaneCodes),
                    ["statusCode"] = MeatImportReadinessStepStatusCodes.NotStarted,
                    ["requiresOfficialResult"] = step.RequiresOfficialResult.ToString(),
                    ["requiresJointConfirmation"] = step.RequiresJointConfirmation.ToString(),
                    ["liveRecheckRequired"] = step.LiveRecheckRequired.ToString()
                }
            };
        }).ToArray();

        var edges = Steps
            .SelectMany(step => step.PrerequisiteStepCodes.Select(prerequisite => new DiagramEdgeDto
            {
                EdgeId = $"{prerequisite}--{step.Code}",
                FromNodeId = prerequisite,
                ToNodeId = step.Code,
                Label = "완료 후 진행",
                MeaningCode = "Prerequisite"
            }))
            .ToArray();

        return new DiagramSnapshotDto
        {
            DiagramId = MeatImportReadinessCodes.TemplateCode,
            DiagramName = "한국 육류 수입 준비도 협업 절차도",
            LedgerTemplateKey = MeatImportReadinessCodes.LedgerTemplateKey,
            WorkflowModeKey = "InformationReadiness",
            Nodes = nodes,
            Edges = edges,
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["templateVersion"] = MeatImportReadinessCodes.TemplateVersion,
                ["jurisdiction"] = "KR",
                ["informationOnly"] = bool.TrueString,
                ["brokerageEnabled"] = bool.FalseString
            }
        };
    }

    private static MeatImportReadinessLaneResponse Lane(string code, string name, string summary, int order)
        => new() { Code = code, DisplayName = name, ResponsibilitySummary = summary, DisplayOrder = order };

    private static MeatImportReadinessSourceResponse Source(
        string key,
        string provider,
        string name,
        string url,
        string usageNote,
        bool liveCheckRequired)
        => new()
        {
            Key = key,
            Provider = provider,
            DisplayName = name,
            Url = url,
            UsageNote = usageNote,
            LiveCheckRequired = liveCheckRequired
        };

    private static MeatImportReadinessStepTemplateResponse Step(
        string code,
        int sequence,
        string phaseCode,
        string phaseName,
        string title,
        string description,
        IReadOnlyList<string> laneCodes,
        IReadOnlyList<string> prerequisites,
        IReadOnlyList<string> evidenceCodes,
        IReadOnlyList<string> sourceKeys,
        string prompt,
        bool requiresOfficialResult = false,
        bool requiresJointConfirmation = false,
        bool liveRecheckRequired = false,
        bool canBeNotApplicable = false)
        => new()
        {
            Code = code,
            Sequence = sequence,
            PhaseCode = phaseCode,
            PhaseName = phaseName,
            Title = title,
            Description = description,
            LaneCodes = laneCodes,
            PrerequisiteStepCodes = prerequisites,
            RequiredEvidenceCodes = evidenceCodes,
            SourceKeys = sourceKeys,
            CommunicationPrompt = prompt,
            RequiresOfficialResult = requiresOfficialResult,
            RequiresJointConfirmation = requiresJointConfirmation,
            LiveRecheckRequired = liveRecheckRequired,
            CanBeNotApplicable = canBeNotApplicable
        };
}

public static class MeatImportReadinessLaneCodes
{
    public const string KoreanImporter = "KoreanImporter";
    public const string OverseasCounterparty = "OverseasCounterparty";
    public const string ExportingAuthority = "ExportingAuthority";
    public const string Qia = "QIA";
    public const string Mfds = "MFDS";
    public const string Customs = "KCS";
    public const string DomesticDistribution = "DomesticDistribution";
}

public static class MeatImportReadinessPhaseCodes
{
    public const string Eligibility = "Eligibility";
    public const string PreShipment = "PreShipment";
    public const string ShipmentAndEntry = "ShipmentAndEntry";
    public const string DomesticRelease = "DomesticRelease";
}

public static class MeatImportReadinessStepCodes
{
    public const string ProductScope = "product-scope";
    public const string CountryProductEligibility = "country-product-eligibility";
    public const string ForeignEstablishmentEligibility = "foreign-establishment-eligibility";
    public const string ImporterRegistration = "importer-registration";
    public const string ExportCertificatePlan = "export-certificate-plan";
    public const string DocumentAndLabelPack = "document-label-pack";
    public const string PreShipmentJointCheck = "pre-shipment-joint-check";
    public const string ShipmentColdChain = "shipment-cold-chain";
    public const string QiaQuarantineResult = "qia-quarantine-result";
    public const string MfdsInspectionResult = "mfds-inspection-result";
    public const string CustomsClearanceResult = "customs-clearance-result";
    public const string LabelTraceabilityRelease = "label-traceability-release";
    public const string DistributionReleaseCheck = "distribution-release-check";
}

public static class MeatImportReadinessSourceKeys
{
    public const string MfdsImportedFoodSafety = "mfds-imported-food-safety";
    public const string ImportedFoodInformationMaru = "mfds-imported-food-maru";
    public const string QiaEligibleCountries = "qia-eligible-countries";
    public const string QiaCertificateStatus = "qia-approved-certificate-status";
    public const string ImportedFoodAct = "korea-imported-food-act";
    public const string CustomsLivestockImport = "kcs-livestock-import";
}
