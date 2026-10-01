namespace Hongdal.Contracts.Common.Community;

public static class CommunityLedgerTemplateCatalog
{
    private static readonly IReadOnlyList<string> DefaultSuggestedStates =
    [
        "대화중",
        "진행하기로 함",
        "진행중",
        "확인 필요",
        "완료",
        "보류",
        "이견 있음"
    ];

    private static readonly IReadOnlyList<CommunityLedgerTemplateResponse> Templates =
    [
        new()
        {
            Key = CommunityLedgerTemplateKeys.Order,
            DisplayName = "주문 원장",
            Category = "통합 원장",
            WorkflowTag = "주문 이행 통합",
            TargetOperatingSystemCode = CommunityLedgerOperatingSystemCodes.CommunityTrust,
            TargetOperatingSystemName = "커뮤니티 신뢰 OS",
            Summary = "사용자의 주문을 루트로 두고 판매, 창고 입출고, 배송과 운송 원장을 연결해 전체 이행 상태를 한 맥락에서 조회하는 원장입니다.",
            EngineHints = [CommunityLedgerEngineHints.CommunityActivitySignal],
            UiSectionHints = ["주문자", "판매자", "주문 항목", "이행 방식", "포함 원장", "진행 요약"],
            ActionHints = ["판매 원장 연결", "입고 원장 연결", "출고 원장 연결", "배송 원장 연결", "운송 원장 연결"],
            CompositionRules =
            [
                Rule(
                    CommunityLedgerCompositionRuleCodes.OrderBeforeFulfillment,
                    "주문이 먼저 구성되어야 합니다.",
                    "판매, 창고 입출고, 배송과 운송 원장은 주문자와 주문 항목이 확인된 주문 원장 아래에 연결합니다.",
                    requiredUiSectionHints: ["주문자", "주문 항목"],
                    gatedActionHints: ["판매 원장 연결", "입고 원장 연결", "출고 원장 연결", "배송 원장 연결", "운송 원장 연결"])
            ],
            ProcessingSurfaces =
            [
                ApiEndpoint("GET", "주문원장Controller", "통합조회", "주문 원장과 연결된 하위 원장의 최신 상태를 함께 조회합니다.", "I주문원장통합UseCase.조회Async"),
                ApiEndpoint("POST", "주문원장Controller", "하위원장연결", "기존 판매, 입출고, 배송 또는 운송 원장을 주문 원장에 연결합니다.", "I주문원장통합UseCase.하위원장연결Async")
            ],
            PersistencePolicy = MongoPolicy(),
            BestLedgerPatternTitle = "주문을 중심으로 이행 원장을 모은 통합 원장",
            BestLedgerPatternSummary = "주문 원장에는 연결 관계만 두고 판매, 입출고, 배송과 운송의 상세 상태는 각 원장에서 독립적으로 관리합니다.",
            CommunityDiscussionPrompts = ["이 주문에는 어떤 판매 원장이 필요한가요?", "창고 입출고와 배송은 몇 건으로 나뉘나요?", "어떤 원장이 완료되어야 전체 주문을 완료로 볼 수 있나요?"],
            Roles =
            [
                Role("주문자", "원하는 상품과 수령 조건을 남깁니다.", CommunityLedgerPermissionCodes.InviteParticipant, CommunityLedgerPermissionCodes.ConfirmCompletion),
                Role("판매자", "판매 가능 여부와 준비 상태를 남깁니다.", CommunityLedgerPermissionCodes.ChangeState),
                Role("이행 담당자", "입출고, 배송 또는 운송 원장을 처리합니다.", CommunityLedgerPermissionCodes.ChangeState, CommunityLedgerPermissionCodes.AttachEvidence),
                Role("확인자", "하위 원장과 전체 주문의 완료 여부를 확인합니다.", CommunityLedgerPermissionCodes.ConfirmCompletion, CommunityLedgerPermissionCodes.CloseLedger)
            ]
        },
        new()
        {
            Key = CommunityLedgerTemplateKeys.CargoTransport,
            DisplayName = "화물 운송 원장",
            Category = "생활 원장",
            WorkflowTag = "국내 화물 운송",
            TargetOperatingSystemCode = CommunityLedgerOperatingSystemCodes.DomesticCargoTransport,
            TargetOperatingSystemName = "국내 화물 운송 OS",
            Summary = "상차, 이동, 하차, 수령 확인을 참여자들이 함께 보는 원장입니다.",
            EngineHints = [CommunityLedgerEngineHints.TransportDispatch],
            UiSectionHints = ["참여자", "상차지", "하차지", "화물 조건", "증빙", "정산 표시", "타임라인"],
            ActionHints = ["상차지 도착", "상차 확인", "사진 첨부", "하차 완료", "수령 확인", "입금 표시"],
            CompositionRules =
            [
                Rule(
                    CommunityLedgerCompositionRuleCodes.TransportRequestBeforePickupDropoff,
                    "운송 의뢰가 먼저 구성되어야 합니다.",
                    "상차와 하차 화면은 요청자, 상차지, 하차지, 화물 조건이 잡힌 뒤에 열려야 합니다.",
                    requiredUiSectionHints: ["참여자", "상차지", "하차지", "화물 조건"],
                    gatedActionHints: ["상차지 도착", "상차 확인", "하차 완료", "수령 확인"])
            ],
            ProcessingSurfaces =
            [
                ApiEndpoint("POST", "화주운송의뢰Controller", "의뢰생성", "원장을 운송 의뢰로 등록합니다.", "I화주운송의뢰UseCase.의뢰생성Async"),
                ApiEndpoint("POST", "기사운송진행Controller", "상차지도착", "기사 상차지 도착 상태를 처리합니다.", "운송상차지도착Command"),
                ApiEndpoint("POST", "기사운송진행Controller", "상차완료", "상차 완료와 인수증/사진 증빙을 처리합니다.", "운송상차완료Command"),
                ApiEndpoint("POST", "기사운송진행Controller", "완료", "하차 완료와 최종 인수 상태를 처리합니다.", "운송인수완료Command")
            ],
            PersistencePolicy = MongoPolicy(
                Projection("운송 의뢰", "TransportRequest / 화주운송의뢰", "CommunityLedgerId", "원장 규칙이 상차지, 하차지, 화물 조건을 충족하면 생성합니다."),
                Projection("기사 운송 진행", "DriverTransport / 운송진행", "TransportRequestId + CommunityLedgerId", "배차 확정 후 상차/하차 상태를 투영합니다.")),
            BestLedgerPatternTitle = "상하차 확인이 분명한 화물 운송 원장",
            BestLedgerPatternSummary = "상차·하차 확인자와 정산 확인자를 미리 나눠 현장 확인과 돈 표시가 뒤섞이지 않게 합니다.",
            CommunityDiscussionPrompts = ["상차 전 꼭 확인해야 할 정보는 무엇인가요?", "증빙 사진은 어느 단계에서 남기는 게 가장 자연스러운가요?", "입금 표시는 누가 확인하는 게 좋은가요?"],
            Roles =
            [
                Role("요청자", "옮길 물건과 조건을 제시합니다.", CommunityLedgerPermissionCodes.InviteParticipant, CommunityLedgerPermissionCodes.MarkPayment),
                Role("운반자", "상차와 하차 상태를 남깁니다.", CommunityLedgerPermissionCodes.ChangeState, CommunityLedgerPermissionCodes.AttachEvidence),
                Role("상차 확인자", "상차 장소에서 물건과 시간을 확인합니다.", CommunityLedgerPermissionCodes.AttachEvidence, CommunityLedgerPermissionCodes.ConfirmCompletion),
                Role("수령 확인자", "하차 또는 전달 완료를 확인합니다.", CommunityLedgerPermissionCodes.AttachEvidence, CommunityLedgerPermissionCodes.ConfirmCompletion),
                Role("정산 확인자", "입금 표시와 확인 메모를 남깁니다.", CommunityLedgerPermissionCodes.MarkPayment, CommunityLedgerPermissionCodes.CloseLedger)
            ]
        },
        new()
        {
            Key = CommunityLedgerTemplateKeys.FoodOrder,
            DisplayName = "음식 주문 원장",
            Category = "생활 원장",
            WorkflowTag = "음식 주문",
            TargetOperatingSystemCode = CommunityLedgerOperatingSystemCodes.FoodDelivery,
            TargetOperatingSystemName = "음식 배달 OS",
            Summary = "메뉴 주문, 음식점 수락, 조리, 준비 완료까지를 추적하고 필요한 경우 하나 이상의 음식 배달 원장으로 인계하는 원장입니다.",
            EngineHints = [CommunityLedgerEngineHints.FoodDeliveryDispatch],
            UiSectionHints = ["참여자", "메뉴", "주문 상태", "조리 상태", "수령 방식", "정산 표시"],
            ActionHints = ["주문 확인", "음식점 수락", "조리 시작", "준비 완료", "수령 방식 확정", "배달 원장 추가"],
            CompositionRules =
            [
                Rule(
                    CommunityLedgerCompositionRuleCodes.RequestAndParticipantBeforeProgress,
                    "주문 내용과 참여자가 먼저 필요합니다.",
                    "메뉴, 주문자, 판매자 또는 조리자가 정해져야 주문 수락과 조리 흐름을 구성할 수 있습니다.",
                    requiredUiSectionHints: ["참여자", "메뉴"],
                    gatedActionHints: ["음식점 수락", "조리 시작", "준비 완료"])
            ],
            ProcessingSurfaces =
            [
                ApiEndpoint("POST", "음식주문Controller", "등록", "커뮤니티 원장을 음식 주문으로 접수합니다.", "I음식주문접수UseCase.등록Async"),
                ApiEndpoint("POST", "음식주문Controller", "음식점수락", "음식점 주문 수락과 준비 상태를 처리합니다.", "I음식주문접수UseCase.음식점수락Async")
            ],
            PersistencePolicy = MongoPolicy(
                Projection("음식 주문", "FoodOrder / 음식주문", "CommunityLedgerId", "메뉴, 주문자, 판매자 정보가 확정되면 생성합니다."),
                Projection("음식점 수락", "RestaurantAcceptance / 음식점주문수락", "OrderNo + CommunityLedgerId", "음식점이 주문을 수락할 때 상태를 투영합니다.")),
            BestLedgerPatternTitle = "주문과 조리 준비에 집중하는 음식 주문 원장",
            BestLedgerPatternSummary = "주문자, 판매자, 조리자가 주문 수락과 준비 완료를 함께 확인하고, 배달이 필요하면 독립된 배달 원장을 필요한 수만큼 연결합니다.",
            CommunityDiscussionPrompts = ["주문 변경은 어느 시점까지 허용할까요?", "직접 수령과 배달 요청을 언제 확정할까요?", "분할 배달이 필요한 메뉴와 수량은 어떻게 나눌까요?"],
            Roles =
            [
                Role("주문자", "메뉴와 수령 조건을 남깁니다.", CommunityLedgerPermissionCodes.InviteParticipant, CommunityLedgerPermissionCodes.MarkPayment),
                Role("판매자", "주문 가능 여부와 준비 상태를 남깁니다.", CommunityLedgerPermissionCodes.ChangeState, CommunityLedgerPermissionCodes.AttachEvidence),
                Role("조리자", "조리 시작과 준비 완료 상태를 남깁니다.", CommunityLedgerPermissionCodes.ChangeState),
                Role("수령 방식 확인자", "직접 수령인지 배달이 필요한지와 분할 조건을 확인합니다.", CommunityLedgerPermissionCodes.ConfirmCompletion)
            ]
        },
        new()
        {
            Key = CommunityLedgerTemplateKeys.FoodDelivery,
            DisplayName = "음식 배달 원장",
            Category = "생활 원장",
            WorkflowTag = "음식 배달",
            TargetOperatingSystemCode = CommunityLedgerOperatingSystemCodes.FoodDelivery,
            TargetOperatingSystemName = "음식 배달 OS",
            Summary = "한 번의 픽업·이동·전달 시도를 추적하는 원장입니다. 하나의 음식 주문에서 분할 배달이나 재배달을 위해 여러 배달 원장이 파생될 수 있습니다.",
            EngineHints = [CommunityLedgerEngineHints.FoodDeliveryDispatch, CommunityLedgerEngineHints.TransportDispatch],
            UiSectionHints = ["원주문", "배달 회차", "분할 항목", "재배달 사유", "참여자", "픽업지", "도착지", "배달 상태", "전달 증빙", "수령 확인", "타임라인"],
            ActionHints = ["배달 회차 생성", "픽업 도착", "픽업 완료", "이동 시작", "전달 완료", "사진 첨부", "수령 확인", "재배달 원장 추가"],
            CompositionRules =
            [
                Rule(
                    CommunityLedgerCompositionRuleCodes.FoodOrderBeforeDelivery,
                    "원본 음식 주문이 먼저 필요합니다.",
                    "각 배달 원장은 원본 음식 주문을 참조하고 배달 회차, 분할 항목 또는 재배달 사유를 독립적으로 기록합니다. 한 주문에는 선택적으로 여러 배달 원장을 연결할 수 있습니다.",
                    requiredLedgerTemplateKeys: [CommunityLedgerTemplateKeys.FoodOrder],
                    requiredUiSectionHints: ["참여자", "픽업지", "도착지"],
                    gatedActionHints: ["픽업 도착", "픽업 완료", "전달 완료", "수령 확인"])
            ],
            ProcessingSurfaces =
            [
                ApiEndpoint("POST", "배차주소Controller", "저장", "주문 또는 픽업 의뢰를 배달 주소 입력으로 넘깁니다.", "배차주소Controller"),
                ApiEndpoint("GET", "기사배차추천Controller", "조회", "기사 후보와 추천 배차를 조회합니다.", "기사배차추천Controller"),
                ApiEndpoint("POST", "기사배차액션Controller", "수락", "기사 배차 수락을 처리합니다.", "기사배차액션Controller")
            ],
            PersistencePolicy = MongoPolicy(
                Projection("음식 배달 주소", "FoodDispatchAddress / 배차주소", "OrderNo + CommunityLedgerId", "원주문과 배달 원장별 픽업지·도착지가 정리되면 배차 주소로 투영합니다."),
                Projection("배차 추천", "DriverDispatchRecommendation", "CommunityLedgerId", "배달 회차별 배달권과 기사 후보를 계산할 때 실행 인덱스로 투영합니다.")),
            BestLedgerPatternTitle = "배달 회차와 재시도를 독립적으로 추적하는 음식 배달 원장",
            BestLedgerPatternSummary = "분할 배달과 재배달을 별도 원장으로 남겨 이전 시도의 증빙과 실패 사유를 덮어쓰지 않습니다.",
            CommunityDiscussionPrompts = ["어떤 메뉴를 별도 배달 회차로 나눌까요?", "재배달 원장은 어떤 실패 원장을 참조해야 할까요?", "문앞 전달 사진은 어느 회차에 필요한가요?"],
            Roles =
            [
                Role("배달 요청자", "픽업지와 도착지를 정리합니다.", CommunityLedgerPermissionCodes.InviteParticipant),
                Role("픽업 담당자", "음식 픽업 상태를 남깁니다.", CommunityLedgerPermissionCodes.ChangeState, CommunityLedgerPermissionCodes.AttachEvidence),
                Role("배달자", "이동과 전달 상태를 남깁니다.", CommunityLedgerPermissionCodes.ChangeState, CommunityLedgerPermissionCodes.AttachEvidence),
                Role("수령 확인자", "수령 완료를 확인합니다.", CommunityLedgerPermissionCodes.ConfirmCompletion),
                Role("정산 확인자", "배달비 표시와 확인 메모를 남깁니다.", CommunityLedgerPermissionCodes.MarkPayment)
            ]
        },
        new()
        {
            Key = CommunityLedgerTemplateKeys.HongdalMart,
            DisplayName = "알뜰살뜰 마트 배송 원장",
            Category = "생활 원장",
            WorkflowTag = "알뜰살뜰 마트",
            TargetOperatingSystemCode = CommunityLedgerOperatingSystemCodes.HongdalMartUrbanLogistics,
            TargetOperatingSystemName = "알뜰살뜰 마트 도심 물류 OS",
            Summary = "도심 재고, 피킹·포장, 기사 픽업, 고객 전달을 마트 배송 흐름으로 공유하는 원장입니다. 즉시배송, 예약배송, 묶음배송은 배송유형 속성으로 둡니다.",
            EngineHints = [CommunityLedgerEngineHints.PickingBatch, CommunityLedgerEngineHints.FoodDeliveryDispatch, CommunityLedgerEngineHints.TransportDispatch],
            UiSectionHints = ["참여자", "주문", "도심 재고", "피킹/포장", "포장 완료", "기사 픽업", "고객 전달", "증빙"],
            ActionHints = ["재고 확인", "피킹 시작", "피킹 완료", "포장 완료", "픽업 준비", "기사 인계", "전달 완료", "사진 첨부"],
            CompositionRules =
            [
                Rule(
                    CommunityLedgerCompositionRuleCodes.MartOrderBeforePickingPacking,
                    "마트 주문과 도심 재고가 먼저 필요합니다.",
                    "알뜰살뜰 마트 피킹·포장 화면은 일반 창고 출고 원장이 아니라 마트 주문, 도심 재고, 참여자가 정리된 뒤 구성되어야 합니다.",
                    requiredUiSectionHints: ["참여자", "주문", "도심 재고"],
                    gatedActionHints: ["피킹 시작", "피킹 완료", "포장 완료"]),
                Rule(
                    CommunityLedgerCompositionRuleCodes.MartPackedBeforeDeliveryPickup,
                    "기사 인계는 포장 완료 뒤에 열립니다.",
                    "배달 기사 추천은 피킹 중에도 미리 계산할 수 있지만 실제 픽업과 전달 상태는 포장 완료가 확정된 뒤 열려야 합니다.",
                    requiredUiSectionHints: ["포장 완료", "기사 픽업"],
                    gatedActionHints: ["픽업 준비", "기사 인계", "전달 완료"])
            ],
            ProcessingSurfaces =
            [
                PlannedApi("POST", "api/v1/hongdal-mart/orders", "알뜰살뜰 마트 주문 원장을 생성하고 도심 재고 후보를 연결합니다."),
                PlannedApi("POST", "api/v1/warehouse-operations/picking-tasks/{taskKey}/complete", "알뜰살뜰 마트 도심 창고 피킹 완료를 처리하고 창고피킹완료됨Event를 발행합니다."),
                PlannedApi("POST", "api/v1/hongdal-mart/orders/{orderId}/packing-complete", "포장 완료 뒤 알뜰살뜰 마트 배송 배차 대기 후보를 엽니다."),
                ApiEndpoint("GET", "기사배차추천Controller", "조회", "포장 완료 또는 곧 포장될 주문에 대해 근거리 기사 후보와 추천 배차를 조회합니다.", "기사배차추천Controller")
            ],
            PersistencePolicy = MongoPolicy(
                Projection("알뜰살뜰 마트 주문", "HongdalMartOrder / CommunityLedger", "OrderId + CommunityLedgerId", "주문과 커뮤니티 원장이 연결되면 마트 배송 원장으로 투영합니다."),
                Projection("도심 재고 피킹", "WarehousePickingTask", "TaskKey + CommunityLedgerId", "도심 재고가 예약되면 알뜰살뜰 마트 전용 피킹 작업으로 투영합니다."),
                Projection("마트 배송 배차", "DriverDispatchRecommendation / HongdalMartPackedOrder", "OrderId + CommunityLedgerId", "포장 완료 또는 포장 임박 상태에서 기사 추천과 픽업 대기열로 투영합니다.")),
            BestLedgerPatternTitle = "도심 재고와 배달 픽업이 맞물리는 알뜰살뜰 마트 배송 원장",
            BestLedgerPatternSummary = "일반 창고 출고와 분리해 마트 주문, 피킹·포장, 기사 픽업, 고객 전달을 짧은 시간 단위로 이어줍니다. 즉시배송 여부는 배송유형으로 남깁니다.",
            CommunityDiscussionPrompts = ["피킹 중 기사 추천을 언제 열면 좋을까요?", "품절이나 대체 상품은 원장에 어떻게 남길까요?", "포장 완료 뒤 픽업 지연은 누가 확인해야 할까요?"],
            Roles =
            [
                Role("주문자", "받을 상품과 수령 조건을 남깁니다.", CommunityLedgerPermissionCodes.InviteParticipant),
                Role("마트 피킹 담당자", "도심 재고를 확인하고 피킹 상태를 남깁니다.", CommunityLedgerPermissionCodes.ChangeState, CommunityLedgerPermissionCodes.AttachEvidence),
                Role("포장 담당자", "상품 포장 완료와 특이사항을 남깁니다.", CommunityLedgerPermissionCodes.ChangeState, CommunityLedgerPermissionCodes.AttachEvidence),
                Role("배달자", "픽업, 이동, 전달 상태를 남깁니다.", CommunityLedgerPermissionCodes.ChangeState, CommunityLedgerPermissionCodes.AttachEvidence),
                Role("수령 확인자", "전달 완료와 수령 상태를 확인합니다.", CommunityLedgerPermissionCodes.ConfirmCompletion)
            ]
        },
        new()
        {
            Key = CommunityLedgerTemplateKeys.WarehouseOutbound,
            DisplayName = "창고 출고 원장",
            Category = "생활 원장",
            WorkflowTag = "창고 입출고",
            TargetOperatingSystemCode = CommunityLedgerOperatingSystemCodes.WarehouseCommerceFulfillment,
            TargetOperatingSystemName = "창고·커머스 이행 OS",
            Summary = "판매채널 또는 일반 창고 출고 요청, 피킹, 검수, 포장, 운송 인계를 공유하는 원장입니다.",
            EngineHints = [CommunityLedgerEngineHints.OutboundBatch, CommunityLedgerEngineHints.PickingBatch, CommunityLedgerEngineHints.TransportDispatch],
            UiSectionHints = ["참여자", "출고 품목", "피킹 작업", "검수", "포장", "운송 인계", "증빙"],
            ActionHints = ["피킹 시작", "피킹 완료", "검수 요청", "포장 완료", "운송 인계", "사진 첨부"],
            CompositionRules =
            [
                Rule(
                    CommunityLedgerCompositionRuleCodes.InboundOrStockBeforeOutbound,
                    "출고 전에 입고 또는 재고 근거가 필요합니다.",
                    "출고 품목은 입고 원장, 재고 기록, 또는 운영자가 승인한 재고 근거에서 만들어져야 합니다.",
                    requiredLedgerTemplateKeys: [CommunityLedgerTemplateKeys.WarehouseInbound],
                    requiredUiSectionHints: ["출고 품목", "피킹 작업"],
                    gatedActionHints: ["피킹 시작", "피킹 완료", "검수 요청"]),
                Rule(
                    CommunityLedgerCompositionRuleCodes.OutboundBeforeHandoffTransport,
                    "운송 인계는 출고 검수와 포장 뒤에 열립니다.",
                    "상차나 배송 의뢰 화면은 피킹, 검수, 포장 상태가 정리된 뒤 출고 OS가 운송 OS로 넘길 때 구성되어야 합니다.",
                    requiredLedgerTemplateKeys: [CommunityLedgerTemplateKeys.CargoTransport],
                    requiredUiSectionHints: ["검수", "포장", "운송 인계"],
                    gatedActionHints: ["운송 인계"])
            ],
            ProcessingSurfaces =
            [
                ApiEndpoint("GET", "WarehouseOperationsController", "재고목록", "입고 완료 또는 재고 근거를 조회합니다.", "I창고작업UseCase.재고목록Async"),
                PlannedApi("POST", "api/v1/warehouse-operations/picking-tasks/{taskKey}/complete", "피킹 작업 완료를 처리하고 창고피킹완료됨Event를 발행해야 합니다."),
                ApiEndpoint("POST", "WarehouseOperationsController", "포장작업", "출고 전 포장 작업을 처리합니다.", "I창고작업UseCase.포장작업Async"),
                ApiEndpoint("POST", "WarehouseOperationsController", "재위탁운송생성", "출고된 재고를 국내 운송 의뢰로 전환합니다.", "I창고작업UseCase.재위탁운송생성Async")
            ],
            PersistencePolicy = MongoPolicy(
                Projection("창고 재고", "WarehouseInventory / InventoryItem", "InboundItemId + CommunityLedgerId", "입고 또는 재고 근거가 확인되면 출고 가능 재고로 참조합니다."),
                Projection("재위탁 운송 의뢰", "TransportRequest / 재고운송의뢰", "CommunityLedgerId", "포장과 운송 인계가 끝나면 운송 의뢰로 투영합니다.")),
            BestLedgerPatternTitle = "피킹·검수·운송 인계가 이어지는 창고 출고 원장",
            BestLedgerPatternSummary = "출고 작업을 피킹, 검수, 포장, 운송 인계로 나눠 창고 안 작업과 밖 운송이 자연스럽게 이어지게 합니다.",
            CommunityDiscussionPrompts = ["피킹과 검수를 같은 사람이 해도 되는 상황은 언제인가요?", "운송 인계 전에 어떤 사진이 필요할까요?", "분할 출고가 생기면 원장을 나누는 게 좋을까요?"],
            Roles =
            [
                Role("출고 요청자", "출고 품목과 도착 조건을 남깁니다.", CommunityLedgerPermissionCodes.InviteParticipant),
                Role("피킹 담당자", "물건을 찾고 출고 준비 상태를 남깁니다.", CommunityLedgerPermissionCodes.ChangeState, CommunityLedgerPermissionCodes.AttachEvidence),
                Role("검수자", "수량과 상태를 확인합니다.", CommunityLedgerPermissionCodes.AttachEvidence, CommunityLedgerPermissionCodes.ConfirmCompletion),
                Role("포장 담당자", "포장 상태와 특이사항을 남깁니다.", CommunityLedgerPermissionCodes.ChangeState, CommunityLedgerPermissionCodes.AttachEvidence),
                Role("운송자", "창고 밖 전달 또는 배송 상태를 남깁니다.", CommunityLedgerPermissionCodes.ChangeState)
            ]
        },
        new()
        {
            Key = CommunityLedgerTemplateKeys.WarehouseInbound,
            DisplayName = "창고 입고 원장",
            Category = "생활 원장",
            WorkflowTag = "창고 입출고",
            TargetOperatingSystemCode = CommunityLedgerOperatingSystemCodes.WarehouseCommerceFulfillment,
            TargetOperatingSystemName = "창고·커머스 이행 OS",
            Summary = "납품, 입고 검수, 보관 위치, 이상 여부를 공유하는 원장입니다.",
            EngineHints = [CommunityLedgerEngineHints.CommunityActivitySignal],
            UiSectionHints = ["참여자", "입고 예정", "납품 상태", "검수", "보관 위치", "이상 기록", "마감"],
            ActionHints = ["도착 예정 등록", "입고 시작", "검수 완료", "보관 위치 기록", "이상 보고", "입고 마감"],
            CompositionRules =
            [
                Rule(
                    CommunityLedgerCompositionRuleCodes.RequestAndParticipantBeforeProgress,
                    "입고 예정과 검수자가 먼저 필요합니다.",
                    "입고 검수 화면은 입고 예정 품목, 납품자, 검수 담당자가 정해진 뒤 구성되어야 합니다.",
                    requiredUiSectionHints: ["참여자", "입고 예정", "검수"],
                    gatedActionHints: ["입고 시작", "검수 완료", "보관 위치 기록", "입고 마감"])
            ],
            ProcessingSurfaces =
            [
                ApiEndpoint("POST", "WarehouseOperationsController", "입고생성", "입고 예정 원장을 창고 입고 요청으로 등록합니다.", "I창고작업UseCase.입고생성Async"),
                ApiEndpoint("POST", "WarehouseOperationsController", "입고검수", "입고 품목 검수를 처리합니다.", "I창고작업UseCase.입고검수Async"),
                ApiEndpoint("POST", "WarehouseOperationsController", "입고완료", "입고 마감과 재고 전환을 처리합니다.", "I창고작업UseCase.입고완료Async")
            ],
            PersistencePolicy = MongoPolicy(
                Projection("창고 입고", "WarehouseInbound / 입고요청", "CommunityLedgerId", "입고 예정 품목과 검수자가 정해지면 생성합니다."),
                Projection("창고 재고", "WarehouseInventory / InventoryItem", "InboundId + CommunityLedgerId", "검수와 입고 완료 후 재고로 투영합니다.")),
            BestLedgerPatternTitle = "이상 여부를 놓치지 않는 창고 입고 원장",
            BestLedgerPatternSummary = "납품자와 입고 검수자를 분리해 수량, 파손, 누락을 먼저 잡고 보관 위치를 남깁니다.",
            CommunityDiscussionPrompts = ["입고 이상은 어떤 사진을 남기는 게 좋을까요?", "보관 위치는 누가 최종 확인해야 할까요?", "납품자와 검수자 확인이 다르면 어떻게 표시할까요?"],
            Roles =
            [
                Role("입고 요청자", "입고 예정 품목과 조건을 남깁니다.", CommunityLedgerPermissionCodes.InviteParticipant),
                Role("납품자", "도착 예정과 납품 상태를 남깁니다.", CommunityLedgerPermissionCodes.ChangeState),
                Role("입고 검수자", "수량, 파손, 누락 여부를 확인합니다.", CommunityLedgerPermissionCodes.AttachEvidence, CommunityLedgerPermissionCodes.ConfirmCompletion),
                Role("보관 담당자", "보관 위치와 후속 처리를 남깁니다.", CommunityLedgerPermissionCodes.ChangeState),
                Role("정리 확인자", "입고 마감을 확인합니다.", CommunityLedgerPermissionCodes.CloseLedger)
            ]
        },
        new()
        {
            Key = CommunityLedgerTemplateKeys.LocalSale,
            DisplayName = "생활 판매 원장",
            Category = "생활 원장",
            WorkflowTag = "생활 판매",
            TargetOperatingSystemCode = CommunityLedgerOperatingSystemCodes.WarehouseCommerceFulfillment,
            TargetOperatingSystemName = "창고·커머스 이행 OS",
            Summary = "판매, 예약, 결제 표시, 전달 확인을 참여자끼리 정리하는 원장입니다.",
            EngineHints = [CommunityLedgerEngineHints.OutboundBatch, CommunityLedgerEngineHints.TransportDispatch],
            UiSectionHints = ["참여자", "판매 물건", "예약", "결제 표시", "전달", "확인", "메모"],
            ActionHints = ["예약 표시", "입금 표시", "전달 일정 확정", "전달 완료", "구매자 확인", "거래 정리"],
            CompositionRules =
            [
                Rule(
                    CommunityLedgerCompositionRuleCodes.SaleItemBeforeReservationSettlement,
                    "판매 물건과 상대가 먼저 정해져야 합니다.",
                    "예약, 입금 표시, 전달 화면은 판매 물건과 구매자가 잡힌 뒤에 구성되어야 합니다.",
                    requiredUiSectionHints: ["참여자", "판매 물건"],
                    gatedActionHints: ["예약 표시", "입금 표시", "전달 일정 확정", "전달 완료"])
            ],
            ProcessingSurfaces =
            [
                ApiEndpoint("POST", "커뮤니티게시글Controller", "Create", "거래 대화와 원장 기록을 커뮤니티에 남깁니다.", "I커뮤니티게시글발행UseCase"),
                ApiEndpoint("GET", "상품여정Controller", "스캔코드기반상품여정조회", "상품 또는 물건 여정 정보를 조회합니다.", "상품여정Controller")
            ],
            PersistencePolicy = MongoPolicy(
                Projection("커뮤니티 게시글", "PlatformCommunityPost", "CommunityLedgerId", "원장의 공개 대화와 후기 링크만 RDB 커뮤니티 글로 투영합니다."),
                Projection("상품 여정", "ProductJourney", "CommunityLedgerId", "판매 물건이 상품 여정으로 관리될 때만 선택적으로 투영합니다.")),
            BestLedgerPatternTitle = "예약과 전달이 섞이지 않는 생활 판매 원장",
            BestLedgerPatternSummary = "판매자와 구매자가 예약, 입금 표시, 전달 확인을 따로 남겨 동네 거래를 부담 없이 정리합니다.",
            CommunityDiscussionPrompts = ["예약 취소는 어떻게 남기는 게 좋을까요?", "입금 이미지는 선택으로 둘까요?", "직거래와 배송 거래를 같은 원장으로 볼 수 있을까요?"],
            Roles =
            [
                Role("판매자", "물건과 거래 조건을 남깁니다.", CommunityLedgerPermissionCodes.InviteParticipant, CommunityLedgerPermissionCodes.ChangeState),
                Role("구매자", "구매 의사와 수령 조건을 남깁니다.", CommunityLedgerPermissionCodes.MarkPayment, CommunityLedgerPermissionCodes.ConfirmCompletion),
                Role("전달자", "전달 또는 배송 상태를 남깁니다.", CommunityLedgerPermissionCodes.ChangeState, CommunityLedgerPermissionCodes.AttachEvidence),
                Role("확인자", "거래 완료 여부를 확인합니다.", CommunityLedgerPermissionCodes.ConfirmCompletion),
                Role("정산 확인자", "입금 표시와 확인 메모를 남깁니다.", CommunityLedgerPermissionCodes.MarkPayment)
            ]
        },
        new()
        {
            Key = CommunityLedgerTemplateKeys.GroupPurchase,
            DisplayName = "공동구매 원장",
            Category = "통합 원장",
            WorkflowTag = "공동구매",
            TargetOperatingSystemCode = CommunityLedgerOperatingSystemCodes.CommunityTrust,
            TargetOperatingSystemName = "커뮤니티 신뢰 OS",
            Summary = "공동구매의 모집, 제안, 가격 근거, 거래경로와 합의를 관리하고, 확정된 수요는 내부 주문집계로 인계하는 사용자 중심 원장입니다.",
            EngineHints = [CommunityLedgerEngineHints.Grouping],
            UiSectionHints = ["모집", "제안 주체", "공동 조건", "가격 근거", "투표/결정", "거래경로", "합의", "주문 집계", "개별 주문 원장", "주문 수량 합계", "수령 거점", "분배"],
            ActionHints = ["수요 모집", "공동 조건 조정", "가격 근거 확인", "거래경로 확정", "합의 확정", "주문집계 생성", "개별 주문 연결", "구매 확정", "수령 거점 확정", "분배 시작"],
            CompositionRules =
            [
                Rule(
                    CommunityLedgerCompositionRuleCodes.GroupPurchaseAgreementBeforeGroupOrder,
                    "주문집계 생성 전에 공동구매 합의가 필요합니다.",
                    "모집과 가격·거래경로·공급 조건을 합의한 뒤 결제 또는 확정된 개별 주문을 내부 주문집계로 묶습니다.",
                    requiredLedgerTemplateKeys: [CommunityLedgerTemplateKeys.GroupOrder],
                    requiredUiSectionHints: ["공동 조건", "투표/결정", "거래경로", "합의"],
                    gatedActionHints: ["주문집계 생성"])
            ],
            ProcessingSurfaces =
            [
                ApiEndpoint("POST", "공동구매자동집단화Controller", "수요등록", "커뮤니티 수요를 공동구매 자동 집단화로 등록합니다.", "I공동구매자동집단화UseCase.수요등록Async"),
                ApiEndpoint("POST", "공동구매수요투표Controller", "Create", "공동구매 조건과 참여 수량에 대한 투표를 등록합니다.", "I공동구매수요투표UseCase.CreateAsync"),
                ApiEndpoint("POST", "DomesticGroupPurchaseFulfillmentPlansController", "CreateOrderDraft", "전통시장·3PL·집합지 직송 옵션에 맞춰 발주 주문 원장과 후속 원장 생성 계획을 만듭니다.", "IDomesticGroupPurchaseFulfillmentPlanService.CreateOrderDraftAsync")
            ],
            PersistencePolicy = MongoPolicy(
                Projection("공동구매 수요", "GroupPurchaseDemand / 공동구매자동수요", "CommunityLedgerId", "참여자의 상품, 수량과 공동 조건을 수요로 투영합니다."),
                Projection("주문집계 인계", "GroupOrder / 공동구매주문집계", "CommunityLedgerId + AutomaticGroupId", "합의된 조건으로 확정된 개별 주문 집합을 공동구매 내부 주문집계에 투영합니다.")),
            BestLedgerPatternTitle = "합의와 실행을 분리하는 공동구매 원장",
            BestLedgerPatternSummary = "공동구매 화면에는 모집부터 이행까지 하나의 흐름으로 보이되, 결제·확정된 개별 주문의 집계는 내부 주문집계에 맡깁니다.",
            CommunityDiscussionPrompts = ["어떤 조건으로 공동구매 주문을 확정할까요?", "개별 주문 확정 기준은 무엇인가요?", "국내 구매와 공동수입 중 어떤 경로가 맞을까요?"],
            Roles =
            [
                Role("제안자", "공동구매 조건과 제안 근거를 남깁니다.", CommunityLedgerPermissionCodes.InviteParticipant, CommunityLedgerPermissionCodes.ChangeState),
                Role("참여자", "수요와 이의, 합의 의사를 남깁니다.", CommunityLedgerPermissionCodes.ConfirmCompletion),
                Role("공동구매 대표", "합의된 조건을 내부 주문집계로 인계합니다.", CommunityLedgerPermissionCodes.ChangeState, CommunityLedgerPermissionCodes.AttachEvidence),
                Role("확인자", "합의와 주문집계 인계 상태를 확인합니다.", CommunityLedgerPermissionCodes.CloseLedger),
                Role("정산 확인자", "개별 주문 합계와 공동구매 정산 표시를 확인합니다.", CommunityLedgerPermissionCodes.MarkPayment, CommunityLedgerPermissionCodes.CloseLedger)
            ]
        },
        new()
        {
            Key = CommunityLedgerTemplateKeys.GroupOrder,
            DisplayName = "공동구매 주문집계 원장",
            Category = "통합 원장",
            WorkflowTag = "공동구매 주문집계·이행",
            TargetOperatingSystemCode = CommunityLedgerOperatingSystemCodes.CommunityTrust,
            TargetOperatingSystemName = "커뮤니티 신뢰 OS",
            IsInternalAggregationTemplate = true,
            Summary = "공동구매 내부에서 동일 조건으로 확정된 개별 주문들을 묶고 수량, 금액과 참여자 수를 개별 주문의 합으로 계산하는 하위 집계 원장입니다.",
            EngineHints = [CommunityLedgerEngineHints.Grouping, CommunityLedgerEngineHints.OutboundBatch, CommunityLedgerEngineHints.TransportDispatch],
            UiSectionHints = ["원천 공동구매", "개별 주문 원장", "확정 주문 수", "주문 수량 합계", "예약결제 합계", "수령 창고 분포", "공동 물류", "분배"],
            ActionHints = ["개별 주문 연결", "집계 갱신", "공급 발주", "공동 입고", "참여자별 분배"],
            CompositionRules =
            [
                Rule(
                    CommunityLedgerCompositionRuleCodes.GroupOrderRequiresIndividualOrders,
                    "공동구매 주문집계는 확정된 개별 주문의 집합입니다.",
                    "주문집계의 참여자 수, 수량과 금액은 연결된 유효 개별 주문에서 계산하며 독립 값으로 입력하지 않습니다.",
                    requiredLedgerTemplateKeys: [CommunityLedgerTemplateKeys.Order],
                    requiredUiSectionHints: ["개별 주문 원장", "확정 주문 수", "주문 수량 합계"],
                    gatedActionHints: ["공급 발주", "공동 입고", "참여자별 분배"])
            ],
            ProcessingSurfaces =
            [
                ApiEndpoint("POST", "공동구매자동집단화Controller", "수요등록", "예약결제된 개별 주문을 공동구매 주문집계에 연결하고 합계를 갱신합니다.", "I공동구매자동집단화UseCase.수요등록Async"),
                ApiEndpoint("GET", "주문원장Controller", "통합조회", "주문집계와 연결된 개별 주문들의 이행·서명 상태를 계산합니다.", "I주문원장통합UseCase.조회Async")
            ],
            PersistencePolicy = MongoPolicy(
                Projection("공동구매 주문집계", "CommunityLedger / GroupOrder", "SourceGroupPurchaseLedgerId + AutomaticGroupId", "개별 주문 연결이 바뀔 때 확정 주문 수, 수량과 금액을 다시 계산합니다.")),
            BestLedgerPatternTitle = "개별 주문에서 계산되는 공동구매 주문집계",
            BestLedgerPatternSummary = "개인의 계약·결제·수령 조건은 개별 주문에 유지하고 주문집계에는 합계와 공동 물류 관계만 둡니다.",
            CommunityDiscussionPrompts = ["현재 확정된 개별 주문은 몇 건인가요?", "공동 발주 수량은 얼마인가요?", "어떤 수령 창고 기준으로 분배해야 하나요?"],
            Roles =
            [
                Role("공동구매 주문 담당자", "개별 주문 집합과 공동 발주를 관리합니다.", CommunityLedgerPermissionCodes.ChangeState, CommunityLedgerPermissionCodes.AttachEvidence),
                Role("개별 주문자", "자신의 주문·결제·수령 조건을 관리합니다.", CommunityLedgerPermissionCodes.ConfirmCompletion),
                Role("물류 담당자", "공동 입고와 참여자별 분배를 관리합니다.", CommunityLedgerPermissionCodes.ChangeState),
                Role("정산 확인자", "개별 주문 합계와 공동 정산을 확인합니다.", CommunityLedgerPermissionCodes.MarkPayment, CommunityLedgerPermissionCodes.CloseLedger)
            ]
        },
        new()
        {
            Key = CommunityLedgerTemplateKeys.GroupImport,
            DisplayName = "공동수입 원장",
            Category = "통합 원장",
            WorkflowTag = "공동수입",
            TargetOperatingSystemCode = CommunityLedgerOperatingSystemCodes.GroupPurchaseImport,
            TargetOperatingSystemName = "공동수입 OS",
            Summary = "확정된 공동구매 수요를 원천 원장으로 연결하고 해외 공급자 발주, 선적, 통관, 국내 반출과 3PL 입고까지 관리하는 수입 이행 원장입니다.",
            EngineHints = [CommunityLedgerEngineHints.ImportCustoms, CommunityLedgerEngineHints.OutboundBatch, CommunityLedgerEngineHints.TransportDispatch],
            UiSectionHints = ["원천 공동구매 원장", "수입 결정", "해외 선적", "통관 상태", "국내 반출", "3PL 입고", "세대 분배", "정산 표시", "수령 확인"],
            ActionHints = ["공동구매 원장 연결", "수입 진행 결정", "해외 발주/선적 등록", "통관 상태 동기화", "국내 반출 준비", "3PL 입고 인계", "세대 분배 시작", "수령 확인"],
            CompositionRules =
            [
                Rule(
                    CommunityLedgerCompositionRuleCodes.GroupPurchaseDemandBeforeImportDecision,
                    "확정된 공동구매 원장이 있어야 수입 결정을 열 수 있습니다.",
                    "원천 공동구매의 수량 합계와 구매 결정 근거가 부족하면 수입 진행 여부를 확정하지 않고 보류합니다.",
                    requiredLedgerTemplateKeys: [CommunityLedgerTemplateKeys.GroupPurchase],
                    requiredUiSectionHints: ["원천 공동구매 원장", "수입 결정"],
                    gatedActionHints: ["수입 진행 결정"]),
                Rule(
                    CommunityLedgerCompositionRuleCodes.GroupPurchaseImportDecisionBeforeShipment,
                    "수입 결정 뒤에 해외 발주와 선적 추적을 시작합니다.",
                    "수입 방식, 수량, 가격, FCL/LCL 같은 조건이 정해진 뒤 해외 선적과 통관 상태를 연결합니다.",
                    requiredUiSectionHints: ["수입 결정"],
                    gatedActionHints: ["해외 발주/선적 등록", "통관 상태 동기화"]),
                Rule(
                    CommunityLedgerCompositionRuleCodes.GroupPurchaseCustomsBeforeDomesticDistribution,
                    "통관과 반출 가능 상태가 국내 분배의 전제입니다.",
                    "통관 상태와 국내 반출 조건이 확인되어야 3PL 입고, 운송 인계, 세대 분배를 열 수 있습니다.",
                    requiredLedgerTemplateKeys: [CommunityLedgerTemplateKeys.WarehouseInbound, CommunityLedgerTemplateKeys.CargoTransport],
                    requiredUiSectionHints: ["통관 상태", "국내 반출"],
                    gatedActionHints: ["3PL 입고 인계", "세대 분배 시작", "수령 확인"])
            ],
            ProcessingSurfaces =
            [
                ApiEndpoint("GET", "공동구매물류워크플로우Controller", "Resolve", "공동수입 물류 흐름 정의를 조회합니다.", "I공동구매물류워크플로우저장소.ResolveAsync"),
                ApiEndpoint("GET", "공동구매해외선적추적Controller", "Lookup", "공동구매 선적 또는 진행 정보를 조회합니다.", "I공동구매해외선적추적UseCase.공개조회Async"),
                ApiEndpoint("POST", "공동구매해외선적추적Controller", "통관동기화", "문서관리번호 기준 수입 통관 상태를 공동수입 원장에 동기화합니다.", "I공동구매해외선적추적UseCase.통관동기화Async"),
                ApiEndpoint("POST", "WarehouseOperationsController", "재위탁운송생성", "국내 분배가 필요하면 운송 의뢰로 넘깁니다.", "I창고작업UseCase.재위탁운송생성Async")
            ],
            PersistencePolicy = MongoPolicy(
                Projection("공동수입 결정", "GroupPurchaseImportDecision / 공동구매물류워크플로우", "CommunityLedgerId + GroupPurchaseId", "원천 공동구매 원장과 수입 진행 여부, FCL/LCL, 가격, 수량 결정을 투영합니다."),
                Projection("공동수입 선적", "GroupPurchaseShipment / 공동구매해외선적", "GroupPurchaseId + CommunityLedgerId", "선적 문서나 통관 상태가 연결되면 참조 링크를 남깁니다."),
                Projection("국내 3PL 입고", "WarehouseInbound / 공동구매입고", "CommunityLedgerId + DocumentManagementNumber", "통관 뒤 3PL 창고 입고를 선택하면 입고 원장과 연결합니다."),
                Projection("국내 운송 의뢰", "TransportRequest / 공동구매국내운송", "CommunityLedgerId", "국내 반출 또는 세대 분배가 확정되면 운송 의뢰로 투영합니다.")),
            BestLedgerPatternTitle = "공동구매 원장에서 분리된 공동수입 이행 원장",
            BestLedgerPatternSummary = "공동구매 원장을 원천 수요로 참조하되 해외 발주, 선적, 통관, 입고와 국내 운송 상태는 별도 원장으로 추적합니다.",
            CommunityDiscussionPrompts = ["어느 공동구매 원장을 이번 수입의 원천으로 연결할까요?", "수입 방식과 공급자 조건은 무엇으로 확정할까요?", "통관 지연이 생기면 국내 입고와 분배 일정을 어떻게 조정할까요?"],
            Roles =
            [
                Role("수입 연결자", "확정된 공동구매 원장을 공동수입의 원천으로 연결합니다.", CommunityLedgerPermissionCodes.InviteParticipant, CommunityLedgerPermissionCodes.ChangeState),
                Role("수입 결정자", "수량, 가격, 수입 방식, 진행 여부를 남깁니다.", CommunityLedgerPermissionCodes.ChangeState, CommunityLedgerPermissionCodes.AttachEvidence),
                Role("선적/통관 확인자", "해외 선적, 문서관리번호, 통관 상태를 남깁니다.", CommunityLedgerPermissionCodes.ChangeState, CommunityLedgerPermissionCodes.AttachEvidence),
                Role("입고/분배 담당자", "3PL 입고 또는 세대 분배 상태를 남깁니다.", CommunityLedgerPermissionCodes.ChangeState),
                Role("정산 확인자", "각자 입금 표시와 확인 메모를 정리합니다.", CommunityLedgerPermissionCodes.MarkPayment, CommunityLedgerPermissionCodes.CloseLedger)
            ]
        },
        new()
        {
            Key = CommunityLedgerTemplateKeys.MeatImportReadiness,
            DisplayName = "육류 수입 준비도 원장",
            Category = "정보 협업 원장",
            WorkflowTag = "육류 수입 준비",
            TargetOperatingSystemCode = CommunityLedgerOperatingSystemCodes.CommunityTrust,
            TargetOperatingSystemName = "커뮤니티 신뢰 OS",
            Summary = "커뮤니티 대화에서 육류 수입 준비가 필요하다고 당사자가 선택했을 때만 열리는 정보 협업 원장입니다. 발주·계약·통관 대행이나 주선은 실행하지 않습니다.",
            IsCommunityOpportunityTemplate = true,
            EngineHints = [CommunityLedgerEngineHints.CommunityActivitySignal],
            UiSectionHints = ["참여자", "제품과 원산지", "준비 단계", "공식 확인 근거", "질문과 이의", "양측 확인", "절차 API 인계"],
            ActionHints = ["정보 제안 확인", "준비도 원장 시작", "근거 첨부", "질문 남기기", "이의 제기", "양측 확인", "공식 결과 기록"],
            CompositionRules =
            [
                Rule(
                    CommunityLedgerCompositionRuleCodes.CommunityDiscussionBeforeMeatImportReadiness,
                    "커뮤니티 대화와 당사자의 명시적 선택이 먼저 필요합니다.",
                    "게시글에 육류와 국경 간 거래 신호가 함께 있어도 원장을 자동 생성하지 않습니다. 게시글 작성자가 정보 제공 경계를 확인하고 직접 시작한 뒤에만 같은 커뮤니티에 준비도 원장을 연결합니다.",
                    requiredUiSectionHints: ["참여자", "제품과 원산지"],
                    gatedActionHints: ["준비도 원장 시작", "절차 API 인계"])
            ],
            ProcessingSurfaces =
            [
                ApiEndpoint("GET", "CommunityPostOpportunitiesController", "Get", "게시글 문맥에 맞는 선택적 정보 협업 제안을 표시 언어에 맞춰 조회합니다.", "ICommunityPostOpportunityQueryUseCase.GetAsync"),
                ApiEndpoint("POST", "CommunityPostOpportunitiesController", "StartMeatImportReadiness", "작성자가 명시적으로 동의한 경우에만 육류 수입 준비도 원장을 만들고 게시글에 연결합니다.", "ICommunityPostMeatImportReadinessUseCase.StartAsync"),
                ApiEndpoint("GET", "MeatImportReadinessController", "GetDiagram", "정부기관 확인 경계가 표시된 육류 수입 준비 절차도를 조회합니다.", "IMeatImportReadinessService.GetDiagram")
            ],
            PersistencePolicy = MongoPolicy(),
            BestLedgerPatternTitle = "커뮤니티 대화에서 필요할 때만 여는 수입 준비 정보 원장",
            BestLedgerPatternSummary = "국내·해외 사용자가 같은 게시글과 같은 원장을 사용하고, 국가나 표시 언어로 기능을 나누지 않으며, 당사자의 선택 이후에만 절차·근거·질문·이의를 함께 관리합니다.",
            CommunityDiscussionPrompts = ["이 대화가 실제 수입 준비 절차 확인을 필요로 하나요?", "한국 측과 해외 측이 각각 확인해야 할 정보는 무엇인가요?", "정부기관의 최신 공식 결과를 어느 단계에서 다시 확인해야 하나요?"],
            Roles =
            [
                Role("한국 측 참여자", "국내 수입 요건과 공식 결과 참조를 확인합니다.", CommunityLedgerPermissionCodes.ChangeState, CommunityLedgerPermissionCodes.AttachEvidence, CommunityLedgerPermissionCodes.ConfirmCompletion),
                Role("해외 측 참여자", "제품, 작업장, 수출증명 관련 정보와 근거를 확인합니다.", CommunityLedgerPermissionCodes.ChangeState, CommunityLedgerPermissionCodes.AttachEvidence, CommunityLedgerPermissionCodes.ConfirmCompletion),
                Role("지원 참여자", "필요한 질문과 설명을 남기되 당사자 대신 결정을 확정하지 않습니다.", CommunityLedgerPermissionCodes.AttachEvidence)
            ]
        },
        new()
        {
            Key = CommunityLedgerTemplateKeys.Errand,
            DisplayName = "생활 요청 원장",
            Category = "생활 원장",
            WorkflowTag = "생활 요청 원장",
            TargetOperatingSystemCode = CommunityLedgerOperatingSystemCodes.CommunityTrust,
            TargetOperatingSystemName = "커뮤니티 신뢰 OS",
            Summary = "심부름, 도움 요청, 동네 협업처럼 정형화되지 않은 일을 담는 원장입니다.",
            EngineHints = [CommunityLedgerEngineHints.CommunityActivitySignal],
            UiSectionHints = ["참여자", "요청 내용", "진행 상태", "확인", "증빙", "메모", "타임라인"],
            ActionHints = ["참여하기", "진행 시작", "메모 남기기", "사진 첨부", "완료 확인", "보류 표시"],
            CompositionRules =
            [
                Rule(
                    CommunityLedgerCompositionRuleCodes.RequestAndParticipantBeforeProgress,
                    "요청 내용과 참여자가 먼저 필요합니다.",
                    "진행 화면은 요청 내용과 최소한의 수행자 또는 확인자가 정해진 뒤에 구성되어야 합니다.",
                    requiredUiSectionHints: ["참여자", "요청 내용"],
                    gatedActionHints: ["진행 시작", "완료 확인", "보류 표시"])
            ],
            ProcessingSurfaces =
            [
                ApiEndpoint("POST", "커뮤니티게시글Controller", "Create", "비정형 요청과 진행 기록을 커뮤니티 원장으로 남깁니다.", "I커뮤니티게시글발행UseCase")
            ],
            PersistencePolicy = MongoPolicy(
                Projection("커뮤니티 게시글", "PlatformCommunityPost", "CommunityLedgerId", "비정형 원장의 공개 대화와 진행 요약만 RDB 커뮤니티 글로 투영합니다."),
                Projection("활동 신호", "CommunityActivitySignal", "CommunityLedgerId", "완료된 요청은 개인정보를 제거한 활동 신호로만 재가공합니다.")),
            BestLedgerPatternTitle = "가볍게 모여 처리하는 생활 요청 원장",
            BestLedgerPatternSummary = "정형 업무가 아니어도 요청자, 수행자, 확인자만 나누면 커뮤니티 안에서 느슨하게 일을 굴릴 수 있습니다.",
            CommunityDiscussionPrompts = ["어떤 생활 요청까지 원장으로 만들면 좋을까요?", "완료 확인자는 꼭 필요할까요?", "돈이 오가지 않는 도움도 같은 원장으로 볼까요?"],
            Roles =
            [
                Role("요청자", "필요한 일을 설명하고 참여자를 초대합니다.", CommunityLedgerPermissionCodes.InviteParticipant),
                Role("수행자", "진행 상태와 결과를 남깁니다.", CommunityLedgerPermissionCodes.ChangeState, CommunityLedgerPermissionCodes.AttachEvidence),
                Role("확인자", "완료 여부를 확인합니다.", CommunityLedgerPermissionCodes.ConfirmCompletion),
                Role("참여자", "메모와 확인을 남깁니다.", CommunityLedgerPermissionCodes.AttachEvidence),
                Role("정리자", "완료, 보류, 이견 상태를 정리합니다.", CommunityLedgerPermissionCodes.CloseLedger)
            ]
        }
    ];

    private static readonly IReadOnlyList<CommunityLedgerImplementationModuleResponse> PriorityModules =
    [
        Module(
            1,
            CommunityLedgerImplementationModuleCodes.CommunityConversation,
            "커뮤니티 대화 원장",
            CommunityLedgerTemplateKeys.Errand,
            CommunityLedgerOperatingSystemCodes.CommunityTrust,
            "커뮤니티 신뢰 OS",
            "사용자가 아직 업무로 확정하지 않은 대화, 모집, 질문, 제안을 담는 진입 원장입니다.",
            ["커뮤니티 대화", "참여자 모집", "가벼운 요청"],
            ["참여자", "요청 내용", "메모", "타임라인"]),
        Module(
            2,
            CommunityLedgerImplementationModuleCodes.WishLedgerAssessment,
            "원함-원장 판단 원장",
            CommunityLedgerTemplateKeys.Errand,
            CommunityLedgerOperatingSystemCodes.CommunityTrust,
            "커뮤니티 신뢰 OS",
            "사용자의 원함을 어떤 업무 원장으로 만들 수 있는지 판단하고 필요한 블록을 제안하는 원장입니다.",
            ["원함 확인", "원장화 판정", "추가 정보 요청"],
            ["원함", "지원 범위", "사용자 확인 책임", "후속 원장 후보"]),
        Module(
            3,
            CommunityLedgerImplementationModuleCodes.OrderRoot,
            "주문 통합 원장",
            CommunityLedgerTemplateKeys.Order,
            CommunityLedgerOperatingSystemCodes.CommunityTrust,
            "커뮤니티 신뢰 OS",
            "주문을 루트로 두고 판매, 창고 입출고, 배송과 운송 원장의 최신 상태를 한 맥락으로 묶습니다.",
            ["주문 원장", "판매 원장", "입출고 원장", "배송 원장", "운송 원장"],
            ["주문 항목", "포함 원장", "진행 요약"]),
        Module(
            4,
            CommunityLedgerImplementationModuleCodes.CargoTransport,
            "운송의뢰 원장",
            CommunityLedgerTemplateKeys.CargoTransport,
            CommunityLedgerOperatingSystemCodes.DomesticCargoTransport,
            "국내 화물 운송 OS",
            "상차지, 하차지, 화물 조건, 정산 조건을 기준으로 실제 운송 의뢰를 구성하는 원장입니다.",
            ["화물 운송 원장", "화주 운송 의뢰"],
            ["참여자", "상차지", "하차지", "화물 조건", "정산 표시"]),
        Module(
            5,
            CommunityLedgerImplementationModuleCodes.TransportProgress,
            "운송진행 원장",
            CommunityLedgerTemplateKeys.CargoTransport,
            CommunityLedgerOperatingSystemCodes.DomesticCargoTransport,
            "국내 화물 운송 OS",
            "배차 확정 뒤 상차, 이동, 하차, 수령 확인과 증빙을 남기는 진행 원장입니다.",
            ["기사 운송 진행", "운송 증빙", "수령 확인"],
            ["상차지", "하차지", "증빙", "타임라인", "정산 표시"]),
        Module(
            6,
            CommunityLedgerImplementationModuleCodes.WarehouseOutbound,
            "창고출고 원장",
            CommunityLedgerTemplateKeys.WarehouseOutbound,
            CommunityLedgerOperatingSystemCodes.WarehouseCommerceFulfillment,
            "창고·커머스 이행 OS",
            "출고 품목, 재고 근거, 피킹, 검수, 포장, 운송 인계를 묶는 원장입니다.",
            ["창고 출고", "판매채널 출고", "재위탁 운송 준비"],
            ["출고 품목", "피킹 작업", "검수", "포장", "운송 인계"]),
        Module(
            7,
            CommunityLedgerImplementationModuleCodes.PickingPacking,
            "피킹/포장 원장",
            CommunityLedgerTemplateKeys.WarehouseOutbound,
            CommunityLedgerOperatingSystemCodes.WarehouseCommerceFulfillment,
            "창고·커머스 이행 OS",
            "상품을 꺼내고 검수하고 포장하는 현장 작업을 독립적으로 추적하는 원장입니다.",
            ["피킹 작업", "검수", "포장 완료"],
            ["피킹 작업", "검수", "포장", "증빙"]),
        Module(
            8,
            CommunityLedgerImplementationModuleCodes.HongdalMartOrder,
            "마트주문 원장",
            CommunityLedgerTemplateKeys.HongdalMart,
            CommunityLedgerOperatingSystemCodes.HongdalMartUrbanLogistics,
            "알뜰살뜰 마트 도심 물류 OS",
            "사용자가 원하는 마트 상품, 수령 조건, 도심 재고 후보를 연결하는 원장입니다.",
            ["알뜰살뜰 마트 주문", "도심 재고 확인"],
            ["주문", "도심 재고", "참여자"]),
        Module(
            9,
            CommunityLedgerImplementationModuleCodes.HongdalMartDelivery,
            "마트 배송 원장",
            CommunityLedgerTemplateKeys.HongdalMart,
            CommunityLedgerOperatingSystemCodes.HongdalMartUrbanLogistics,
            "알뜰살뜰 마트 도심 물류 OS",
            "포장된 마트 주문을 기사 픽업과 고객 전달로 이어주는 배송 원장입니다. 즉시배송은 배송유형입니다.",
            ["기사 픽업", "고객 전달", "수령 확인"],
            ["포장 완료", "기사 픽업", "고객 전달", "수령 확인"]),
        Module(
            10,
            CommunityLedgerImplementationModuleCodes.GroupPurchaseDemand,
            "공동구매 모집·합의 원장",
            CommunityLedgerTemplateKeys.GroupPurchase,
            CommunityLedgerOperatingSystemCodes.CommunityTrust,
            "커뮤니티 신뢰 OS",
            "공동구매의 모집, 가격 근거, 거래경로와 합의를 관리하고 확정 수요를 내부 주문집계로 인계하는 원장입니다.",
            ["공동구매 모집", "가격 근거", "거래경로", "합의"],
            ["모집", "공동 조건", "투표/결정", "합의"]),
        Module(
            11,
            CommunityLedgerImplementationModuleCodes.GroupPurchaseImportDecision,
            "공동수입 결정 원장",
            CommunityLedgerTemplateKeys.GroupImport,
            CommunityLedgerOperatingSystemCodes.GroupPurchaseImport,
            "공동수입 OS",
            "모인 수요를 보고 수입 진행 여부, FCL/LCL, 가격, 수량, 진행 조건을 확정하는 원장입니다.",
            ["수입 결정", "FCL/LCL", "가격·수량 결정"],
            ["수입 결정", "투표/결정", "정산 표시"]),
        Module(
            12,
            CommunityLedgerImplementationModuleCodes.GroupPurchaseShipmentCustoms,
            "공동수입 선적/통관 원장",
            CommunityLedgerTemplateKeys.GroupImport,
            CommunityLedgerOperatingSystemCodes.GroupPurchaseImport,
            "공동수입 OS",
            "해외 발주, BL/AWB, 문서관리번호, 통관 상태, 반출 가능 상태를 추적하는 원장입니다.",
            ["해외 선적", "통관 상태", "국내 반출"],
            ["해외 선적", "통관 상태", "국내 반출", "증빙"]),
        Module(
            13,
            CommunityLedgerImplementationModuleCodes.GroupPurchaseDistribution,
            "공동수입 입고/분배 원장",
            CommunityLedgerTemplateKeys.GroupImport,
            CommunityLedgerOperatingSystemCodes.GroupPurchaseImport,
            "공동수입 OS",
            "통관 이후 3PL 입고, 국내 운송 인계, 세대 분배, 수령 확인을 관리하는 원장입니다.",
            ["3PL 입고", "세대 분배", "수령 확인"],
            ["3PL 입고", "세대 분배", "수령 확인", "정산 표시"]),
        Module(
            14,
            CommunityLedgerImplementationModuleCodes.SettlementMark,
            "결제/정산 표시 원장",
            CommunityLedgerTemplateKeys.CargoTransport,
            CommunityLedgerOperatingSystemCodes.CommunityTrust,
            "커뮤니티 신뢰 OS",
            "플랫폼 보증이 아니라 참여자 간 결제 표시, 상대방 확인, 보류, 정산 메모를 남기는 원장입니다.",
            ["결제 표시", "정산 확인", "FakePG 개발 검증"],
            ["정산 표시", "결제 표시", "확인", "메모"]),
        Module(
            15,
            CommunityLedgerImplementationModuleCodes.ReportDispute,
            "신고/분쟁 원장",
            CommunityLedgerTemplateKeys.Errand,
            CommunityLedgerOperatingSystemCodes.CommunityTrust,
            "커뮤니티 신뢰 OS",
            "문제 신고, 이견, 보류, 운영자 검토, 제한 신호를 별도로 추적하는 원장입니다.",
            ["문제 신고", "이견 있음", "운영자 검토"],
            ["증빙", "메모", "확인", "타임라인"]),
        Module(
            16,
            CommunityLedgerImplementationModuleCodes.FoodOrder,
            "음식 주문 원장",
            CommunityLedgerTemplateKeys.FoodOrder,
            CommunityLedgerOperatingSystemCodes.FoodDelivery,
            "음식 배달 OS",
            "메뉴 주문부터 음식점 수락, 조리, 준비 완료까지를 독립적으로 추적하는 원장입니다.",
            ["음식 주문", "음식점 수락", "조리 준비"],
            ["메뉴", "주문 상태", "조리 상태", "수령 방식", "정산 표시"]),
        Module(
            17,
            CommunityLedgerImplementationModuleCodes.FoodDelivery,
            "음식 배달 원장",
            CommunityLedgerTemplateKeys.FoodDelivery,
            CommunityLedgerOperatingSystemCodes.FoodDelivery,
            "음식 배달 OS",
            "한 번의 픽업과 전달 시도를 추적하며 분할 배달과 재배달은 별도 원장으로 만듭니다.",
            ["배달 회차", "분할 배달", "재배달 시도"],
            ["원주문", "배달 회차", "픽업지", "도착지", "배달 상태", "전달 증빙"]),
        Module(
            18,
            CommunityLedgerImplementationModuleCodes.WarehouseInbound,
            "창고입고 원장",
            CommunityLedgerTemplateKeys.WarehouseInbound,
            CommunityLedgerOperatingSystemCodes.WarehouseCommerceFulfillment,
            "창고·커머스 이행 OS",
            "입고 예정 품목과 운송 하차 인계 내용을 받아 검수, 이상 처리, 보관과 재고 전환을 추적하는 원장입니다.",
            ["창고 입고", "운송 하차 인계", "입고 검수"],
            ["입고 예정", "운송 하차", "검수", "보관 위치", "이상 기록"]),
        Module(
            19,
            CommunityLedgerImplementationModuleCodes.GroupOrderAggregation,
            "공동구매 주문집계 원장",
            CommunityLedgerTemplateKeys.GroupOrder,
            CommunityLedgerOperatingSystemCodes.CommunityTrust,
            "커뮤니티 신뢰 OS",
            "확정된 개별 주문들을 연결하고 주문 수, 수량과 예약결제 금액을 개별 주문의 합으로 계산하는 원장입니다.",
            ["공동구매 주문집계", "개별 주문 원장", "공동 물류"],
            ["확정 주문 수", "주문 수량 합계", "예약결제 합계", "수령 창고 분포"])
    ];

    private static readonly IReadOnlyList<CommunityLedgerRelationResponse> PriorityLedgerRelations =
    [
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.OrderRoot,
            CommunityLedgerImplementationModuleCodes.FoodOrder,
            CommunityLedgerTemplateKeys.Order,
            CommunityLedgerTemplateKeys.FoodOrder,
            CommunityLedgerRelationTypes.Contains,
            CommunityLedgerRelationCardinality.OneToMany,
            required: false,
            "주문에 음식 판매·준비 과정이 필요할 때",
            "주문 원장은 하나 이상의 음식 주문 원장을 판매·준비 이행 원장으로 포함할 수 있습니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.OrderRoot,
            CommunityLedgerImplementationModuleCodes.WarehouseOutbound,
            CommunityLedgerTemplateKeys.Order,
            CommunityLedgerTemplateKeys.WarehouseOutbound,
            CommunityLedgerRelationTypes.Contains,
            CommunityLedgerRelationCardinality.OneToMany,
            required: false,
            "주문 상품을 창고에서 꺼내 포장해야 할 때",
            "주문 원장은 여러 창고와 분할 출고를 각각의 창고출고 원장으로 포함할 수 있습니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.OrderRoot,
            CommunityLedgerImplementationModuleCodes.WarehouseInbound,
            CommunityLedgerTemplateKeys.Order,
            CommunityLedgerTemplateKeys.WarehouseInbound,
            CommunityLedgerRelationTypes.Contains,
            CommunityLedgerRelationCardinality.OneToMany,
            required: false,
            "반품, 이동 또는 수입 상품의 입고가 필요할 때",
            "주문 원장은 주문 이행 중 필요한 창고입고 원장을 포함할 수 있습니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.OrderRoot,
            CommunityLedgerImplementationModuleCodes.FoodDelivery,
            CommunityLedgerTemplateKeys.Order,
            CommunityLedgerTemplateKeys.FoodDelivery,
            CommunityLedgerRelationTypes.Contains,
            CommunityLedgerRelationCardinality.OneToMany,
            required: false,
            "주문을 고객에게 배송해야 할 때",
            "주문 원장은 분할 배송과 재배송을 각각의 배송 원장으로 포함할 수 있습니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.OrderRoot,
            CommunityLedgerImplementationModuleCodes.CargoTransport,
            CommunityLedgerTemplateKeys.Order,
            CommunityLedgerTemplateKeys.CargoTransport,
            CommunityLedgerRelationTypes.Contains,
            CommunityLedgerRelationCardinality.OneToMany,
            required: false,
            "거점 간 이동이나 화물 운송이 필요할 때",
            "주문 원장은 창고 간 이동과 최종 운송을 각각의 운송 원장으로 포함할 수 있습니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.CommunityConversation,
            CommunityLedgerImplementationModuleCodes.WishLedgerAssessment,
            CommunityLedgerTemplateKeys.Errand,
            CommunityLedgerTemplateKeys.Errand,
            CommunityLedgerRelationTypes.Handoff,
            CommunityLedgerRelationCardinality.OneToMany,
            required: true,
            "커뮤니티 대화가 실제 업무 의도로 읽힐 때",
            "대화 원장은 여러 원함-원장 판단 원장을 만들 수 있습니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.WishLedgerAssessment,
            CommunityLedgerImplementationModuleCodes.CargoTransport,
            CommunityLedgerTemplateKeys.Errand,
            CommunityLedgerTemplateKeys.CargoTransport,
            CommunityLedgerRelationTypes.Handoff,
            CommunityLedgerRelationCardinality.OneToMany,
            required: false,
            "상차지, 하차지, 화물 조건이 확인될 때",
            "원함 판단이 국내 운송 업무로 판정되면 운송의뢰 원장으로 넘깁니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.WishLedgerAssessment,
            CommunityLedgerImplementationModuleCodes.GroupPurchaseDemand,
            CommunityLedgerTemplateKeys.Errand,
            CommunityLedgerTemplateKeys.GroupPurchase,
            CommunityLedgerRelationTypes.Handoff,
            CommunityLedgerRelationCardinality.OneToMany,
            required: false,
            "커뮤니티 대화에서 여러 사람의 유사 주문 의사가 확인될 때",
            "유사한 수요를 모집하고 가격·거래경로·공급 조건을 합의할 공동구매 원장으로 연결합니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.GroupPurchaseDemand,
            CommunityLedgerImplementationModuleCodes.GroupOrderAggregation,
            CommunityLedgerTemplateKeys.GroupPurchase,
            CommunityLedgerTemplateKeys.GroupOrder,
            CommunityLedgerRelationTypes.Contains,
            CommunityLedgerRelationCardinality.OneToMany,
            required: true,
            "공동구매 조건에 동의하고 예약결제한 개별 주문이 하나 이상 생길 때",
            "사용자에게는 하나의 공동구매로 보이되 확정된 주문 집합은 내부 주문집계로 분리합니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.GroupOrderAggregation,
            CommunityLedgerImplementationModuleCodes.OrderRoot,
            CommunityLedgerTemplateKeys.GroupOrder,
            CommunityLedgerTemplateKeys.Order,
            CommunityLedgerRelationTypes.Contains,
            CommunityLedgerRelationCardinality.OneToMany,
            required: true,
            "상품과 공동 조건이 맞는 개별 주문이 하나 이상 만들어질 때",
            "공동구매 주문집계는 여러 개별 주문 원장을 포함하며, 수량과 금액은 개별 주문에서 계산합니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.FoodOrder,
            CommunityLedgerImplementationModuleCodes.FoodDelivery,
            CommunityLedgerTemplateKeys.FoodOrder,
            CommunityLedgerTemplateKeys.FoodDelivery,
            CommunityLedgerRelationTypes.Handoff,
            CommunityLedgerRelationCardinality.OneToMany,
            required: false,
            "주문 준비가 완료되고 배달, 분할 배달 또는 재배달이 필요할 때",
            "하나의 음식 주문 원장은 배달이 없을 수도 있고, 분할 배달이나 재배달을 위해 여러 음식 배달 원장과 연결될 수 있습니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.GroupPurchaseDemand,
            CommunityLedgerImplementationModuleCodes.GroupPurchaseImportDecision,
            CommunityLedgerTemplateKeys.GroupPurchase,
            CommunityLedgerTemplateKeys.GroupImport,
            CommunityLedgerRelationTypes.Handoff,
            CommunityLedgerRelationCardinality.ManyToOne,
            required: false,
            "확정된 공동구매를 해외 공급자로 이행하기로 선택할 때",
            "국내에서 이행하는 공동구매는 그대로 닫고, 수입을 선택한 공동구매만 별도의 공동수입 원장으로 인계합니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.GroupPurchaseImportDecision,
            CommunityLedgerImplementationModuleCodes.GroupPurchaseShipmentCustoms,
            CommunityLedgerTemplateKeys.GroupImport,
            CommunityLedgerTemplateKeys.GroupImport,
            CommunityLedgerRelationTypes.Flow,
            CommunityLedgerRelationCardinality.OneToOne,
            required: true,
            "수입 진행이 확정되고 해외 발주가 시작될 때",
            "수입 결정 원장은 해외 선적, 문서관리번호, 통관 상태를 추적하는 원장으로 이어집니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.GroupPurchaseShipmentCustoms,
            CommunityLedgerImplementationModuleCodes.GroupPurchaseDistribution,
            CommunityLedgerTemplateKeys.GroupImport,
            CommunityLedgerTemplateKeys.GroupImport,
            CommunityLedgerRelationTypes.Handoff,
            CommunityLedgerRelationCardinality.OneToMany,
            required: true,
            "통관 완료 또는 반출 가능 상태가 확인될 때",
            "선적/통관 원장은 3PL 입고, 국내 운송 인계, 세대 분배 원장으로 넘어갑니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.GroupPurchaseDistribution,
            CommunityLedgerImplementationModuleCodes.WarehouseOutbound,
            CommunityLedgerTemplateKeys.GroupImport,
            CommunityLedgerTemplateKeys.WarehouseOutbound,
            CommunityLedgerRelationTypes.Handoff,
            CommunityLedgerRelationCardinality.OneToMany,
            required: false,
            "3PL 입고 뒤 창고 피킹/출고가 필요할 때",
            "공동수입 입고/분배 원장은 창고 출고 원장과 연결될 수 있습니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.GroupPurchaseDistribution,
            CommunityLedgerImplementationModuleCodes.CargoTransport,
            CommunityLedgerTemplateKeys.GroupImport,
            CommunityLedgerTemplateKeys.CargoTransport,
            CommunityLedgerRelationTypes.Handoff,
            CommunityLedgerRelationCardinality.OneToMany,
            required: false,
            "세대 배송 또는 거점 배송이 필요할 때",
            "공동수입 입고/분배 원장은 국내 운송 의뢰 원장으로 넘어갈 수 있습니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.CargoTransport,
            CommunityLedgerImplementationModuleCodes.TransportProgress,
            CommunityLedgerTemplateKeys.CargoTransport,
            CommunityLedgerTemplateKeys.CargoTransport,
            CommunityLedgerRelationTypes.Flow,
            CommunityLedgerRelationCardinality.OneToOne,
            required: true,
            "배차가 확정될 때",
            "운송의뢰 원장은 배차 확정 뒤 운송진행 원장 상태로 이어집니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.WarehouseOutbound,
            CommunityLedgerImplementationModuleCodes.PickingPacking,
            CommunityLedgerTemplateKeys.WarehouseOutbound,
            CommunityLedgerTemplateKeys.WarehouseOutbound,
            CommunityLedgerRelationTypes.Contains,
            CommunityLedgerRelationCardinality.OneToMany,
            required: true,
            "출고 품목이 피킹 작업으로 분해될 때",
            "창고출고 원장은 하나 이상의 피킹/포장 원장을 포함할 수 있습니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.PickingPacking,
            CommunityLedgerImplementationModuleCodes.CargoTransport,
            CommunityLedgerTemplateKeys.WarehouseOutbound,
            CommunityLedgerTemplateKeys.CargoTransport,
            CommunityLedgerRelationTypes.Handoff,
            CommunityLedgerRelationCardinality.OneToMany,
            required: false,
            "포장 완료 뒤 외부 이동이 필요할 때",
            "피킹/포장 완료 뒤 운송 인계가 필요하면 운송의뢰 원장으로 넘깁니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.TransportProgress,
            CommunityLedgerImplementationModuleCodes.WarehouseInbound,
            CommunityLedgerTemplateKeys.CargoTransport,
            CommunityLedgerTemplateKeys.WarehouseInbound,
            CommunityLedgerRelationTypes.Handoff,
            CommunityLedgerRelationCardinality.OneToMany,
            required: false,
            "운송 하차가 완료되고 도착 창고에서 입고 처리가 필요할 때",
            "운송진행 원장의 하차 품목과 증빙을 하나 이상의 창고입고 원장 입력으로 넘길 수 있습니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.HongdalMartOrder,
            CommunityLedgerImplementationModuleCodes.PickingPacking,
            CommunityLedgerTemplateKeys.HongdalMart,
            CommunityLedgerTemplateKeys.HongdalMart,
            CommunityLedgerRelationTypes.Requires,
            CommunityLedgerRelationCardinality.OneToMany,
            required: true,
            "마트 주문과 도심 재고가 확인될 때",
            "마트주문 원장은 도심 재고 기준 피킹/포장 원장을 열어야 합니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.PickingPacking,
            CommunityLedgerImplementationModuleCodes.HongdalMartDelivery,
            CommunityLedgerTemplateKeys.HongdalMart,
            CommunityLedgerTemplateKeys.HongdalMart,
            CommunityLedgerRelationTypes.Handoff,
            CommunityLedgerRelationCardinality.ManyToOne,
            required: true,
            "포장 완료 뒤 기사 픽업이 가능할 때",
            "여러 포장 완료 주문이 하나의 마트 배송 원장으로 묶일 수 있습니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.HongdalMartDelivery,
            CommunityLedgerImplementationModuleCodes.SettlementMark,
            CommunityLedgerTemplateKeys.HongdalMart,
            CommunityLedgerTemplateKeys.CargoTransport,
            CommunityLedgerRelationTypes.Reference,
            CommunityLedgerRelationCardinality.OneToOne,
            required: false,
            "수령 확인 또는 전달 완료 뒤",
            "마트 배송 완료 뒤 참여자 중심 결제/정산 표시를 연결합니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.TransportProgress,
            CommunityLedgerImplementationModuleCodes.SettlementMark,
            CommunityLedgerTemplateKeys.CargoTransport,
            CommunityLedgerTemplateKeys.CargoTransport,
            CommunityLedgerRelationTypes.Reference,
            CommunityLedgerRelationCardinality.OneToOne,
            required: false,
            "하차 완료 또는 수령 확인 뒤",
            "운송 완료 뒤 결제/정산 표시 원장을 참조합니다."),
        LedgerRelation(
            CommunityLedgerImplementationModuleCodes.TransportProgress,
            CommunityLedgerImplementationModuleCodes.ReportDispute,
            CommunityLedgerTemplateKeys.CargoTransport,
            CommunityLedgerTemplateKeys.Errand,
            CommunityLedgerRelationTypes.Reference,
            CommunityLedgerRelationCardinality.OneToMany,
            required: false,
            "문제 신고, 지연, 파손, 이견이 생길 때",
            "운송진행 원장은 필요 시 신고/분쟁 원장을 열 수 있습니다.")
    ];

    public static IReadOnlyList<CommunityLedgerTemplateResponse> All
        => Templates.Select(EnsureLedgerBlocks).ToList();

    public static IReadOnlyList<CommunityLedgerImplementationModuleResponse> PriorityImplementationModules
        => PriorityModules;

    public static IReadOnlyList<CommunityLedgerRelationResponse> LedgerRelations
        => PriorityLedgerRelations;

    public static IReadOnlyList<CommunityLedgerRelationResponse> FindLedgerRelations(string? moduleCodeOrTemplateKey)
    {
        if (string.IsNullOrWhiteSpace(moduleCodeOrTemplateKey))
        {
            return [];
        }

        var key = moduleCodeOrTemplateKey.Trim();
        return PriorityLedgerRelations
            .Where(relation => string.Equals(relation.FromModuleCode, key, StringComparison.OrdinalIgnoreCase)
                               || string.Equals(relation.ToModuleCode, key, StringComparison.OrdinalIgnoreCase)
                               || string.Equals(relation.FromLedgerTemplateKey, key, StringComparison.OrdinalIgnoreCase)
                               || string.Equals(relation.ToLedgerTemplateKey, key, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public static IReadOnlyList<CommunityLedgerBlockRelationResponse> FindBlockRelations(string? templateKey)
        => Find(templateKey).BlockRelations;

    public static CommunityLedgerTemplateResponse Find(string? key)
    {
        CommunityLedgerTemplateResponse template;

        if (string.IsNullOrWhiteSpace(key))
        {
            template = Templates[0];
        }
        else
        {
            template = Templates.FirstOrDefault(x => string.Equals(x.Key, key.Trim(), StringComparison.OrdinalIgnoreCase))
                       ?? Templates[0];
        }

        return EnsureLedgerBlocks(template);
    }

    public static string BuildDraftBody(string? key, string? appName, string? roleLabel)
    {
        var template = Find(key);
        var roleLines = template.Roles.Select(role => $"- {role.RoleName}: {role.Description}");
        var engineLines = template.EngineHints.Count > 0
            ? template.EngineHints.Select(engine => $"- {engine}")
            : ["- 필요 시 OS가 적절한 엔진을 선택합니다."];
        var schedulingLines = template.SchedulingHints.Count > 0
            ? template.SchedulingHints.Select(hint => $"- {hint}")
            : ["- 원장 상태를 보고 실제 처리 API 호출 순서를 정합니다."];
        var uiSectionLines = template.UiSectionHints.Count > 0
            ? template.UiSectionHints.Select(section => $"- {section}")
            : ["- 참여자", "- 상태", "- 메모", "- 증빙"];
        var actionLines = template.ActionHints.Count > 0
            ? template.ActionHints.Select(action => $"- {action}")
            : ["- 상태 변경", "- 메모 남기기", "- 증빙 첨부", "- 완료 확인"];
        var 원함확인질문줄들 = template.원함확인질문목록.Count > 0
            ? template.원함확인질문목록.Select(질문 => $"- {질문}")
            : ["- 무엇을 원하나요?"];
        var 홍달지원범위줄들 = template.홍달지원범위안내목록.Count > 0
            ? template.홍달지원범위안내목록.Select(안내 => $"- {안내}")
            : ["- 원함을 원장 블록으로 정리하고 가능한 다음 행동을 안내합니다."];
        var 사용자확인책임줄들 = template.사용자확인책임안내목록.Count > 0
            ? template.사용자확인책임안내목록.Select(안내 => $"- {안내}")
            : ["- 실제 조건과 확인은 참여자가 직접 입력하고 서로 확인해야 합니다."];
        var blockLines = template.LedgerBlocks.Count > 0
            ? template.LedgerBlocks.Select(BuildLedgerBlockLine)
            : ["- 원장 블록은 참여자, 대상, 상태, 증빙, 인계 조각으로 구성합니다."];
        var moduleLines = BuildPriorityModuleLines(template).ToList();
        var ledgerRelationLines = BuildLedgerRelationLines(template).ToList();
        var blockRelationLines = template.BlockRelations.Count > 0
            ? template.BlockRelations.Select(BuildLedgerBlockRelationLine)
            : ["- 블록 관계는 원장 블록 순서와 구성 규칙을 기준으로 계산합니다."];
        var compositionRuleLines = template.CompositionRules.Count > 0
            ? template.CompositionRules.SelectMany(rule => BuildCompositionRuleLines(rule))
            : ["- 참여자, 대상, 상태가 정리된 뒤 필요한 화면과 행동을 구성합니다."];
        var processingSurfaceLines = template.ProcessingSurfaces.Count > 0
            ? template.ProcessingSurfaces.Select(surface => BuildProcessingSurfaceLine(surface))
            : ["- 실제 처리는 원장 성격에 맞는 API 또는 내부 application service가 맡습니다."];
        var persistenceLines = BuildPersistencePolicyLines(template.PersistencePolicy);
        var participationLines = BuildParticipationPolicyLines(template.ParticipationPolicy);
        var experienceLines = BuildExperiencePolicyLines(template.ParticipationPolicy.ExperiencePolicy);
        var discussionLines = template.CommunityDiscussionPrompts.Count > 0
            ? template.CommunityDiscussionPrompts.Select(prompt => $"- {prompt}")
            : ["- 이 원장을 더 잘 굴리려면 어떤 역할이 필요할까요?"];
        var stateLines = template.SuggestedStates.Count > 0
            ? template.SuggestedStates.Select(state => $"- {state}")
            : DefaultSuggestedStates.Select(state => $"- {state}");

        return string.Join(Environment.NewLine, new[]
        {
            $"앱: {Normalize(appName, "Hongdal")}",
            $"작성자 역할: {Normalize(roleLabel, "플랫폼 구성원")}",
            $"원장 유형: {template.DisplayName}",
            $"처리 체계: {처리체계표시명(template.TargetOperatingSystemName)}",
            string.Empty,
            "원함 확인:",
            $"- 질문: {template.원함확인질문}",
            $"- 의미: {template.원함확인설명}",
            string.Join(Environment.NewLine, 원함확인질문줄들),
            string.Empty,
            "홍달이 도울 수 있는 범위:",
            string.Join(Environment.NewLine, 홍달지원범위줄들),
            string.Empty,
            "사용자가 직접 확인해야 하는 것:",
            string.Join(Environment.NewLine, 사용자확인책임줄들),
            string.Empty,
            "원장 목적:",
            "- ",
            string.Empty,
            "처리 체계/엔진 인계:",
            $"- 대상 처리 체계: {처리체계표시명(template.TargetOperatingSystemName)} ({template.TargetOperatingSystemCode})",
            $"- 처리 체계 역할: {template.OperatingSystemRoleSummary}",
            "- 처리 체계는 실행 주체라기보다 스케줄러이며, 실제 처리는 API 또는 내부 application service 호출이 담당합니다.",
            string.Join(Environment.NewLine, schedulingLines),
            string.Join(Environment.NewLine, engineLines),
            string.Empty,
            "실제 처리 표면:",
            string.Join(Environment.NewLine, processingSurfaceLines),
            string.Empty,
            "저장/반영 방식:",
            string.Join(Environment.NewLine, persistenceLines),
            string.Empty,
            "동적 UI 힌트:",
            string.Join(Environment.NewLine, uiSectionLines),
            string.Empty,
            "가능한 행동:",
            string.Join(Environment.NewLine, actionLines),
            string.Empty,
            "원장 블록:",
            string.Join(Environment.NewLine, blockLines),
            string.Empty,
            "원장 모듈/관계:",
            string.Join(Environment.NewLine, moduleLines),
            string.Join(Environment.NewLine, ledgerRelationLines),
            string.Empty,
            "블록 관계:",
            string.Join(Environment.NewLine, blockRelationLines),
            string.Empty,
            "구성 규칙:",
            string.Join(Environment.NewLine, compositionRuleLines),
            string.Empty,
            "베스트 원장 공유 포인트:",
            $"- {Normalize(template.BestLedgerPatternTitle, template.DisplayName, 120)}",
            $"- {Normalize(template.BestLedgerPatternSummary, template.Summary, 240)}",
            string.Join(Environment.NewLine, discussionLines),
            string.Empty,
            "참여/역할 정책:",
            string.Join(Environment.NewLine, participationLines),
            string.Empty,
            "성장/레벨 정책:",
            string.Join(Environment.NewLine, experienceLines),
            string.Empty,
            "참여자/역할 라벨:",
            string.Join(Environment.NewLine, roleLines),
            string.Empty,
            "진행 상태:",
            string.Join(Environment.NewLine, stateLines),
            string.Empty,
            "증빙/확인:",
            "- 사진, 메모, 링크 증빙은 필요할 때만 첨부합니다.",
            "- 결제나 입금 표시는 참여자 간 확인용으로 남깁니다.",
            "- 완료 확인은 관련 참여자가 서로 확인한 뒤 남깁니다.",
            string.Empty,
            "메모:",
            "- "
        });
    }

    private static CommunityLedgerTemplateResponse EnsureLedgerBlocks(CommunityLedgerTemplateResponse template)
    {
        if (template.LedgerBlocks.Count == 0)
        {
            template.LedgerBlocks = BuildLedgerBlocks(template).ToList();
        }

        if (template.BlockRelations.Count == 0)
        {
            template.BlockRelations = BuildLedgerBlockRelations(template).ToList();
        }

        return template;
    }

    private static IEnumerable<CommunityLedgerBlockResponse> BuildLedgerBlocks(CommunityLedgerTemplateResponse template)
    {
        var blocks = new List<CommunityLedgerBlockResponse>();
        var sections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddBlock(
            blocks,
            sections,
            template,
            CommunityLedgerBlockTypes.Participant,
            "참여자",
            "참여자 블록",
            "요청자, 수행자, 확인자처럼 원장에 참여하는 사람과 역할 라벨을 기록합니다.",
            dataHints: template.Roles.Select(role => role.RoleName).ToList(),
            actionHints: template.ActionHints
                .Where(action => action.Contains("참여", StringComparison.Ordinal) || action.Contains("확인", StringComparison.Ordinal))
                .DefaultIfEmpty("참여자 확인")
                .ToList(),
            requiredForAiJudgment: true);

        foreach (var section in template.UiSectionHints.Where(section => !string.Equals(section, "참여자", StringComparison.OrdinalIgnoreCase)))
        {
            var blockType = InferBlockType(section);
            AddBlock(
                blocks,
                sections,
                template,
                blockType,
                section,
                $"{section} 블록",
                BuildBlockPurpose(blockType, section),
                dataHints: BuildBlockDataHints(blockType, section),
                actionHints: BuildBlockActionHints(blockType, section, template.ActionHints),
                requiredForAiJudgment: SectionRequiredByRules(section, template) || IsJudgmentCoreBlockType(blockType),
                opensApiHandoff: BlockOpensApiHandoff(blockType, section, template));
        }

        if (!blocks.Any(block => string.Equals(block.BlockType, CommunityLedgerBlockTypes.State, StringComparison.OrdinalIgnoreCase)))
        {
            AddBlock(
                blocks,
                sections,
                template,
                CommunityLedgerBlockTypes.State,
                "진행 상태",
                "진행 상태 블록",
                "대화중, 진행중, 완료, 보류처럼 원장이 어느 단계에 있는지 기록합니다.",
                dataHints: template.SuggestedStates.Count > 0 ? template.SuggestedStates : DefaultSuggestedStates,
                actionHints: template.ActionHints
                    .Where(action => action.Contains("완료", StringComparison.Ordinal) || action.Contains("시작", StringComparison.Ordinal) || action.Contains("보류", StringComparison.Ordinal))
                    .DefaultIfEmpty("상태 변경")
                    .ToList(),
                requiredForAiJudgment: true);
        }

        if (template.ProcessingSurfaces.Count > 0)
        {
            AddBlock(
                blocks,
                sections,
                template,
                CommunityLedgerBlockTypes.Handoff,
                "OS/API 인계",
                "OS/API 인계 블록",
                "원장이 충분히 구성되었을 때 어느 OS, 엔진, API 또는 application service로 넘길지 기록합니다.",
                dataHints: template.ProcessingSurfaces.Select(BuildProcessingSurfaceHint).ToList(),
                actionHints: template.SchedulingHints,
                requiredForAiJudgment: true,
                opensApiHandoff: true);
        }

        return blocks;
    }

    private static IEnumerable<CommunityLedgerBlockRelationResponse> BuildLedgerBlockRelations(CommunityLedgerTemplateResponse template)
    {
        var relations = new List<CommunityLedgerBlockRelationResponse>();
        var blocks = template.LedgerBlocks.ToList();

        for (var i = 0; i < blocks.Count - 1; i++)
        {
            AddBlockRelation(
                relations,
                blocks[i],
                blocks[i + 1],
                CommunityLedgerRelationTypes.Flow,
                CommunityLedgerRelationCardinality.OneToOne,
                required: false,
                compositionRuleCode: string.Empty,
                $"{blocks[i].DisplayName} 다음에 {blocks[i + 1].DisplayName} 정보를 확인합니다.");
        }

        var handoffBlocks = blocks
            .Where(block => block.OpensApiHandoff || block.BlockType == CommunityLedgerBlockTypes.Handoff)
            .ToList();

        foreach (var rule in template.CompositionRules)
        {
            var requiredBlocks = blocks
                .Where(block => rule.RequiredUiSectionHints.Any(section => string.Equals(section, block.UiSectionHint, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            var targetBlocks = handoffBlocks
                .Where(block => block.CompositionRuleCodes.Contains(rule.Code, StringComparer.OrdinalIgnoreCase)
                                || block.BlockType == CommunityLedgerBlockTypes.Handoff)
                .DefaultIfEmpty(handoffBlocks.LastOrDefault())
                .Where(block => block is not null)
                .Cast<CommunityLedgerBlockResponse>()
                .DistinctBy(block => block.Code)
                .ToList();

            foreach (var requiredBlock in requiredBlocks)
            {
                foreach (var targetBlock in targetBlocks)
                {
                    if (string.Equals(requiredBlock.Code, targetBlock.Code, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    AddBlockRelation(
                        relations,
                        requiredBlock,
                        targetBlock,
                        CommunityLedgerRelationTypes.Requires,
                        CommunityLedgerRelationCardinality.ManyToOne,
                        required: true,
                        rule.Code,
                        $"{requiredBlock.DisplayName}이 채워져야 {targetBlock.DisplayName} 행동을 열 수 있습니다.");
                }
            }
        }

        return relations;
    }

    private static void AddBlockRelation(
        ICollection<CommunityLedgerBlockRelationResponse> relations,
        CommunityLedgerBlockResponse from,
        CommunityLedgerBlockResponse to,
        string relationType,
        string cardinality,
        bool required,
        string compositionRuleCode,
        string description)
    {
        if (relations.Any(relation =>
                string.Equals(relation.FromBlockCode, from.Code, StringComparison.OrdinalIgnoreCase)
                && string.Equals(relation.ToBlockCode, to.Code, StringComparison.OrdinalIgnoreCase)
                && string.Equals(relation.RelationType, relationType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(relation.CompositionRuleCode, compositionRuleCode, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        relations.Add(new()
        {
            FromBlockCode = from.Code,
            ToBlockCode = to.Code,
            RelationType = relationType,
            Cardinality = cardinality,
            Required = required,
            CompositionRuleCode = compositionRuleCode,
            Description = description
        });
    }

    private static void AddBlock(
        ICollection<CommunityLedgerBlockResponse> blocks,
        ISet<string> sections,
        CommunityLedgerTemplateResponse template,
        string blockType,
        string uiSectionHint,
        string displayName,
        string purpose,
        IReadOnlyList<string> dataHints,
        IReadOnlyList<string> actionHints,
        bool requiredForAiJudgment,
        bool opensApiHandoff = false)
    {
        var code = BuildLedgerBlockCode(template.Key, blockType, uiSectionHint);

        if (!sections.Add($"{blockType}:{uiSectionHint}") || blocks.Any(block => string.Equals(block.Code, code, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        blocks.Add(new()
        {
            Code = code,
            BlockType = blockType,
            DisplayName = displayName,
            UiSectionHint = uiSectionHint,
            Purpose = purpose,
            DataHints = dataHints,
            ActionHints = actionHints,
            CompositionRuleCodes = template.CompositionRules
                .Where(rule => RuleTouchesBlock(rule, blockType, uiSectionHint))
                .Select(rule => rule.Code)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            RequiredForAiJudgment = requiredForAiJudgment,
            OpensApiHandoff = opensApiHandoff
        });
    }

    private static string BuildLedgerBlockCode(string templateKey, string blockType, string uiSectionHint)
        => $"{templateKey}.{blockType}.{BuildSectionSlug(uiSectionHint)}";

    private static string BuildSectionSlug(string uiSectionHint)
        => uiSectionHint switch
        {
            "참여자" => "participants",
            "상차지" => "pickup-place",
            "하차지" => "dropoff-place",
            "픽업지" => "pickup-place",
            "도착지" => "dropoff-place",
            "보관 위치" => "storage-location",
            "화물 조건" => "cargo",
            "메뉴" => "menu",
            "주문" => "order",
            "도심 재고" => "urban-inventory",
            "출고 품목" => "outbound-items",
            "입고 예정" => "inbound-plan",
            "판매 물건" => "sale-item",
            "요청 내용" => "request-body",
            "모집 수량" => "recruitment-quantity",
            "개별 주문 원장" => "individual-orders",
            "주문 수량 합계" => "order-quantity-total",
            "공동 조건" => "shared-conditions",
            "투표/결정" => "decision",
            "구매 확정" => "purchase-decision",
            "수령 거점" => "pickup-point",
            "원천 공동구매 원장" => "source-group-purchase",
            "수입 결정" => "import-decision",
            "해외 선적" => "overseas-shipment",
            "통관 상태" => "customs-state",
            "국내 반출" => "domestic-release",
            "3PL 입고" => "third-party-logistics-inbound",
            "세대 분배" => "household-distribution",
            "정산 표시" => "settlement",
            "결제 표시" => "settlement",
            "증빙" => "evidence",
            "전달 증빙" => "delivery-evidence",
            "사진 첨부" => "evidence",
            "피킹/포장" => "pick-pack",
            "피킹 작업" => "picking",
            "포장" => "packing",
            "포장 완료" => "packing-complete",
            "조리 상태" => "cooking-state",
            "배달 상태" => "delivery-state",
            "진행 상태" => "progress-state",
            "타임라인" => "timeline",
            "운송 인계" => "transport-handoff",
            "기사 픽업" => "driver-pickup",
            "픽업" => "pickup",
            "전달" => "delivery",
            "고객 전달" => "customer-delivery",
            "분배" => "distribution",
            "납품 상태" => "delivery-state",
            "검수" => "inspection",
            "이상 기록" => "exception-record",
            "마감" => "close",
            "확인" => "confirmation",
            "수령 확인" => "receiver-confirmation",
            "메모" => "memo",
            "OS/API 인계" => "os-api-handoff",
            _ => "custom"
        };

    private static string InferBlockType(string uiSectionHint)
    {
        if (uiSectionHint.Contains("참여자", StringComparison.Ordinal))
        {
            return CommunityLedgerBlockTypes.Participant;
        }

        if (uiSectionHint.Contains("상차지", StringComparison.Ordinal)
            || uiSectionHint.Contains("하차지", StringComparison.Ordinal)
            || uiSectionHint.Contains("픽업지", StringComparison.Ordinal)
            || uiSectionHint.Contains("도착지", StringComparison.Ordinal)
            || uiSectionHint.Contains("거점", StringComparison.Ordinal)
            || uiSectionHint.Contains("보관 위치", StringComparison.Ordinal))
        {
            return CommunityLedgerBlockTypes.Place;
        }

        if (uiSectionHint.Contains("수량", StringComparison.Ordinal))
        {
            return CommunityLedgerBlockTypes.Quantity;
        }

        if (uiSectionHint.Contains("주문", StringComparison.Ordinal)
            || uiSectionHint.Contains("원천 공동구매 원장", StringComparison.Ordinal))
        {
            return CommunityLedgerBlockTypes.Order;
        }

        if (uiSectionHint.Contains("재고", StringComparison.Ordinal)
            || uiSectionHint.Contains("입고", StringComparison.Ordinal)
            || uiSectionHint.Contains("출고", StringComparison.Ordinal))
        {
            return CommunityLedgerBlockTypes.Inventory;
        }

        if (uiSectionHint.Contains("화물", StringComparison.Ordinal)
            || uiSectionHint.Contains("메뉴", StringComparison.Ordinal)
            || uiSectionHint.Contains("물건", StringComparison.Ordinal)
            || uiSectionHint.Contains("요청 내용", StringComparison.Ordinal))
        {
            return CommunityLedgerBlockTypes.Item;
        }

        if (uiSectionHint.Contains("투표", StringComparison.Ordinal)
            || uiSectionHint.Contains("결정", StringComparison.Ordinal)
            || uiSectionHint.Contains("구매 확정", StringComparison.Ordinal))
        {
            return CommunityLedgerBlockTypes.Decision;
        }

        if (uiSectionHint.Contains("선적", StringComparison.Ordinal)
            || uiSectionHint.Contains("통관", StringComparison.Ordinal))
        {
            return CommunityLedgerBlockTypes.State;
        }

        if (uiSectionHint.Contains("증빙", StringComparison.Ordinal)
            || uiSectionHint.Contains("사진", StringComparison.Ordinal)
            || uiSectionHint.Contains("확인", StringComparison.Ordinal)
            || uiSectionHint.Contains("이상 기록", StringComparison.Ordinal))
        {
            return CommunityLedgerBlockTypes.Evidence;
        }

        if (uiSectionHint.Contains("정산", StringComparison.Ordinal)
            || uiSectionHint.Contains("결제", StringComparison.Ordinal)
            || uiSectionHint.Contains("입금", StringComparison.Ordinal))
        {
            return CommunityLedgerBlockTypes.Settlement;
        }

        if (uiSectionHint.Contains("인계", StringComparison.Ordinal)
            || uiSectionHint.Contains("픽업", StringComparison.Ordinal)
            || uiSectionHint.Contains("전달", StringComparison.Ordinal)
            || uiSectionHint.Contains("분배", StringComparison.Ordinal)
            || uiSectionHint.Contains("반출", StringComparison.Ordinal))
        {
            return CommunityLedgerBlockTypes.Handoff;
        }

        if (uiSectionHint.Contains("시간", StringComparison.Ordinal) || uiSectionHint.Contains("예정", StringComparison.Ordinal))
        {
            return CommunityLedgerBlockTypes.Time;
        }

        if (uiSectionHint.Contains("상태", StringComparison.Ordinal)
            || uiSectionHint.Contains("피킹", StringComparison.Ordinal)
            || uiSectionHint.Contains("포장", StringComparison.Ordinal)
            || uiSectionHint.Contains("조리", StringComparison.Ordinal)
            || uiSectionHint.Contains("검수", StringComparison.Ordinal)
            || uiSectionHint.Contains("마감", StringComparison.Ordinal)
            || uiSectionHint.Contains("타임라인", StringComparison.Ordinal))
        {
            return CommunityLedgerBlockTypes.State;
        }

        return CommunityLedgerBlockTypes.Generic;
    }

    private static string BuildBlockPurpose(string blockType, string uiSectionHint)
        => blockType switch
        {
            CommunityLedgerBlockTypes.Place => $"{uiSectionHint}의 주소, 위치, 접근 조건을 구조화해 배차와 UI 표시의 기준으로 씁니다.",
            CommunityLedgerBlockTypes.Item => $"{uiSectionHint}의 품명, 수량, 조건을 구조화해 운송·판매·작업 판단의 기준으로 씁니다.",
            CommunityLedgerBlockTypes.Order => $"{uiSectionHint}의 주문자, 품목, 수령 조건을 구조화해 후속 피킹·배달 판단에 씁니다.",
            CommunityLedgerBlockTypes.Inventory => $"{uiSectionHint}의 재고 또는 입출고 근거를 구조화해 창고 엔진과 피킹 판단에 씁니다.",
            CommunityLedgerBlockTypes.Quantity => $"{uiSectionHint}의 목표 수량과 참여 수량을 구조화해 모집과 구매 판단에 씁니다.",
            CommunityLedgerBlockTypes.Decision => $"{uiSectionHint}의 선택지, 투표, 결의 내용을 구조화해 다음 행동을 여는 기준으로 씁니다.",
            CommunityLedgerBlockTypes.Time => $"{uiSectionHint}의 예정 시각, 마감, 대기 시간을 구조화해 스케줄링 판단에 씁니다.",
            CommunityLedgerBlockTypes.State => $"{uiSectionHint}의 진행 단계를 구조화해 가능한 다음 행동과 경험치 이벤트를 판단합니다.",
            CommunityLedgerBlockTypes.Evidence => $"{uiSectionHint}의 사진, 메모, 서명, 이상 기록을 선택 증빙으로 구조화합니다.",
            CommunityLedgerBlockTypes.Settlement => $"{uiSectionHint}의 결제 표시, 입금 확인, 보류, 분쟁 메모를 참여자 중심으로 기록합니다.",
            CommunityLedgerBlockTypes.Handoff => $"{uiSectionHint}의 인계 대상, 인계 시점, 후속 API/OS 연결을 구조화합니다.",
            _ => $"{uiSectionHint} 정보를 구조화해 동적 UI와 AI 판단 근거로 씁니다."
        };

    private static IReadOnlyList<string> BuildBlockDataHints(string blockType, string uiSectionHint)
        => blockType switch
        {
            CommunityLedgerBlockTypes.Place => ["주소", "위도/경도", "접근 조건", "담당자 연락 힌트"],
            CommunityLedgerBlockTypes.Item => ["품명", "수량", "부피/무게", "주의 조건"],
            CommunityLedgerBlockTypes.Order => ["주문번호", "주문 품목", "수령 조건", "대체 허용 여부"],
            CommunityLedgerBlockTypes.Inventory => ["재고 근거", "창고", "피킹 단위", "예약 수량"],
            CommunityLedgerBlockTypes.Quantity => ["목표 수량", "참여 수량", "최소 진행 수량"],
            CommunityLedgerBlockTypes.Decision => ["선택지", "참여자 의견", "결정 결과", "결정 시각"],
            CommunityLedgerBlockTypes.Time => ["예정 시각", "마감 시각", "대기 시간", "우선순위"],
            CommunityLedgerBlockTypes.State => ["현재 상태", "이전 상태", "다음 가능 상태", "상태 변경자"],
            CommunityLedgerBlockTypes.Evidence => ["이미지", "메모", "서명", "바코드", "링크"],
            CommunityLedgerBlockTypes.Settlement => ["결제 표시", "상대방 확인", "정산 메모", "보류 사유"],
            CommunityLedgerBlockTypes.Handoff => ["인계 대상 처리 체계", "API 경로", "서비스 힌트", "외부/내부 참조 id"],
            _ => [uiSectionHint]
        };

    private static IReadOnlyList<string> BuildBlockActionHints(
        string blockType,
        string uiSectionHint,
        IReadOnlyList<string> templateActionHints)
    {
        var actions = templateActionHints
            .Where(action => ActionFitsBlock(action, blockType, uiSectionHint))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return actions.Count > 0
            ? actions
            : blockType switch
            {
                CommunityLedgerBlockTypes.Evidence => ["증빙 첨부"],
                CommunityLedgerBlockTypes.Settlement => ["결제 표시"],
                CommunityLedgerBlockTypes.Handoff => ["인계 준비"],
                CommunityLedgerBlockTypes.State => ["상태 변경"],
                _ => ["정보 입력"]
            };
    }

    private static bool ActionFitsBlock(string action, string blockType, string uiSectionHint)
    {
        if (action.Contains(uiSectionHint, StringComparison.Ordinal))
        {
            return true;
        }

        return blockType switch
        {
            CommunityLedgerBlockTypes.Participant => action.Contains("참여", StringComparison.Ordinal) || action.Contains("확인", StringComparison.Ordinal),
            CommunityLedgerBlockTypes.Place => action.Contains("도착", StringComparison.Ordinal) || action.Contains("픽업", StringComparison.Ordinal),
            CommunityLedgerBlockTypes.Item => action.Contains("주문", StringComparison.Ordinal) || action.Contains("물건", StringComparison.Ordinal),
            CommunityLedgerBlockTypes.Order => action.Contains("주문", StringComparison.Ordinal),
            CommunityLedgerBlockTypes.Inventory => action.Contains("재고", StringComparison.Ordinal) || action.Contains("피킹", StringComparison.Ordinal) || action.Contains("검수", StringComparison.Ordinal) || action.Contains("입고", StringComparison.Ordinal),
            CommunityLedgerBlockTypes.Quantity => action.Contains("수량", StringComparison.Ordinal),
            CommunityLedgerBlockTypes.Decision => action.Contains("확정", StringComparison.Ordinal) || action.Contains("결정", StringComparison.Ordinal),
            CommunityLedgerBlockTypes.State => action.Contains("시작", StringComparison.Ordinal) || action.Contains("완료", StringComparison.Ordinal) || action.Contains("보류", StringComparison.Ordinal) || action.Contains("선적", StringComparison.Ordinal) || action.Contains("통관", StringComparison.Ordinal),
            CommunityLedgerBlockTypes.Evidence => action.Contains("사진", StringComparison.Ordinal) || action.Contains("첨부", StringComparison.Ordinal) || action.Contains("보고", StringComparison.Ordinal),
            CommunityLedgerBlockTypes.Settlement => action.Contains("입금", StringComparison.Ordinal) || action.Contains("정산", StringComparison.Ordinal) || action.Contains("결제", StringComparison.Ordinal),
            CommunityLedgerBlockTypes.Handoff => action.Contains("인계", StringComparison.Ordinal) || action.Contains("픽업", StringComparison.Ordinal) || action.Contains("전달", StringComparison.Ordinal) || action.Contains("분배", StringComparison.Ordinal) || action.Contains("반출", StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool IsJudgmentCoreBlockType(string blockType)
        => blockType is CommunityLedgerBlockTypes.Participant
            or CommunityLedgerBlockTypes.Place
            or CommunityLedgerBlockTypes.Item
            or CommunityLedgerBlockTypes.Order
            or CommunityLedgerBlockTypes.Inventory
            or CommunityLedgerBlockTypes.Quantity
            or CommunityLedgerBlockTypes.Decision
            or CommunityLedgerBlockTypes.Time
            or CommunityLedgerBlockTypes.State
            or CommunityLedgerBlockTypes.Handoff;

    private static bool SectionRequiredByRules(string uiSectionHint, CommunityLedgerTemplateResponse template)
        => template.CompositionRules.Any(rule => rule.RequiredUiSectionHints.Any(section => string.Equals(section, uiSectionHint, StringComparison.OrdinalIgnoreCase)));

    private static bool BlockOpensApiHandoff(string blockType, string uiSectionHint, CommunityLedgerTemplateResponse template)
        => string.Equals(blockType, CommunityLedgerBlockTypes.Handoff, StringComparison.OrdinalIgnoreCase)
           || template.CompositionRules.Any(rule => rule.GatedActionHints.Any(action => ActionFitsBlock(action, blockType, uiSectionHint)));

    private static bool RuleTouchesBlock(CommunityLedgerCompositionRuleResponse rule, string blockType, string uiSectionHint)
        => rule.RequiredUiSectionHints.Any(section => string.Equals(section, uiSectionHint, StringComparison.OrdinalIgnoreCase))
           || rule.GatedActionHints.Any(action => ActionFitsBlock(action, blockType, uiSectionHint));

    private static string BuildLedgerBlockLine(CommunityLedgerBlockResponse block)
    {
        var required = block.RequiredForAiJudgment ? "AI 판단근거" : "보조";
        var handoff = block.OpensApiHandoff ? ", handoff 후보" : string.Empty;
        var actions = block.ActionHints.Count > 0 ? string.Join(", ", block.ActionHints.Take(4)) : "정보 입력";
        var rules = block.CompositionRuleCodes.Count > 0 ? $" / 규칙: {string.Join(", ", block.CompositionRuleCodes)}" : string.Empty;

        return $"- {block.DisplayName} [{block.BlockType}, {required}{handoff}]: {block.Purpose} / 행동: {actions}{rules}";
    }

    private static string BuildLedgerBlockRelationLine(CommunityLedgerBlockRelationResponse relation)
    {
        var required = relation.Required ? "필수" : "선택";
        var rule = string.IsNullOrWhiteSpace(relation.CompositionRuleCode)
            ? string.Empty
            : $" / 규칙: {relation.CompositionRuleCode}";

        return $"- {relation.FromBlockCode} -> {relation.ToBlockCode} [{relation.RelationType}, {relation.Cardinality}, {required}]: {relation.Description}{rule}";
    }

    private static IEnumerable<string> BuildPriorityModuleLines(CommunityLedgerTemplateResponse template)
    {
        var modules = PriorityModules
            .Where(module => string.Equals(module.LedgerTemplateKey, template.Key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(module => module.Priority)
            .ToList();

        if (modules.Count == 0)
        {
            yield return $"- 우선 구현 모듈: {template.DisplayName}";
            yield break;
        }

        foreach (var module in modules)
        {
            yield return $"- P{module.Priority:00} {module.DisplayName} ({module.ModuleCode}): {module.Summary}";
        }
    }

    private static IEnumerable<string> BuildLedgerRelationLines(CommunityLedgerTemplateResponse template)
    {
        var relations = FindLedgerRelations(template.Key);
        if (relations.Count == 0)
        {
            yield return "- 연결 원장: 아직 명시된 우선 관계가 없습니다.";
            yield break;
        }

        foreach (var relation in relations)
        {
            var required = relation.Required ? "필수" : "선택";
            yield return $"- {relation.FromModuleCode} -> {relation.ToModuleCode} [{relation.RelationType}, {relation.Cardinality}, {required}]: {relation.Trigger} / {relation.Description}";
        }
    }

    private static CommunityLedgerImplementationModuleResponse Module(
        int priority,
        string moduleCode,
        string displayName,
        string ledgerTemplateKey,
        string targetOperatingSystemCode,
        string targetOperatingSystemName,
        string summary,
        IReadOnlyList<string> includedLedgerNames,
        IReadOnlyList<string> primaryBlockHints)
        => new()
        {
            Priority = priority,
            ModuleCode = moduleCode,
            DisplayName = displayName,
            LedgerTemplateKey = ledgerTemplateKey,
            TargetOperatingSystemCode = targetOperatingSystemCode,
            TargetOperatingSystemName = targetOperatingSystemName,
            Summary = summary,
            IncludedLedgerNames = includedLedgerNames,
            PrimaryBlockHints = primaryBlockHints
        };

    private static CommunityLedgerRelationResponse LedgerRelation(
        string fromModuleCode,
        string toModuleCode,
        string fromLedgerTemplateKey,
        string toLedgerTemplateKey,
        string relationType,
        string cardinality,
        bool required,
        string trigger,
        string description)
        => new()
        {
            FromModuleCode = fromModuleCode,
            ToModuleCode = toModuleCode,
            FromLedgerTemplateKey = fromLedgerTemplateKey,
            ToLedgerTemplateKey = toLedgerTemplateKey,
            RelationType = relationType,
            Cardinality = cardinality,
            Required = required,
            Trigger = trigger,
            Description = description
        };

    private static CommunityLedgerCompositionRuleResponse Rule(
        string code,
        string title,
        string description,
        IReadOnlyList<string>? requiredLedgerTemplateKeys = null,
        IReadOnlyList<string>? requiredUiSectionHints = null,
        IReadOnlyList<string>? gatedActionHints = null)
        => new()
        {
            Code = code,
            Title = title,
            Description = description,
            RequiredLedgerTemplateKeys = requiredLedgerTemplateKeys ?? [],
            RequiredUiSectionHints = requiredUiSectionHints ?? [],
            GatedActionHints = gatedActionHints ?? []
        };

    private static CommunityLedgerPersistencePolicyResponse MongoPolicy(
        params CommunityLedgerRelationalProjectionTargetResponse[] relationalProjectionTargets)
        => new()
        {
            PrimaryStoreKind = CommunityLedgerPrimaryStoreKinds.MongoDocument,
            PrimaryStoreName = "community_ledgers",
            FlexibleAttributeStrategy = "원장별 속성, 단계, 참여자, 증빙, 외부 참조는 MongoDB 문서에 유연하게 보관합니다.",
            RelationalProjectionPolicy = "관계형 DB에는 확정된 업무 엔티티와 조회 인덱스만 투영하고, Mongo 원장 id를 역참조 키로 남깁니다.",
            RelationalProjectionTargets = relationalProjectionTargets
        };

    private static CommunityLedgerRelationalProjectionTargetResponse Projection(
        string targetName,
        string entityHint,
        string linkFieldHint,
        string projectionTiming)
        => new()
        {
            TargetName = targetName,
            EntityHint = entityHint,
            LinkFieldHint = linkFieldHint,
            ProjectionTiming = projectionTiming
        };

    private static CommunityLedgerProcessingSurfaceResponse ApiEndpoint(
        string method,
        string controllerName,
        string actionName,
        string purpose,
        string serviceHint)
        => ProcessingSurface(
            CommunityLedgerHandoffModes.HttpApi,
            method,
            routePattern: string.Empty,
            purpose,
            serviceHint,
            isExistingSurface: true,
            controllerName,
            actionName);

    private static CommunityLedgerProcessingSurfaceResponse PlannedApi(
        string method,
        string routePattern,
        string purpose)
        => ProcessingSurface(
            CommunityLedgerHandoffModes.PlannedApi,
            method,
            routePattern,
            purpose,
            "커뮤니티 원장 handoff application service",
            isExistingSurface: false,
            controllerName: string.Empty,
            actionName: string.Empty);

    private static CommunityLedgerProcessingSurfaceResponse ProcessingSurface(
        string handoffMode,
        string method,
        string routePattern,
        string purpose,
        string serviceHint,
        bool isExistingSurface,
        string controllerName,
        string actionName)
        => new()
        {
            HandoffMode = handoffMode,
            ApiEndpointKey = string.IsNullOrWhiteSpace(controllerName) || string.IsNullOrWhiteSpace(actionName)
                ? string.Empty
                : $"{controllerName}.{actionName}",
            ControllerName = controllerName,
            ActionName = actionName,
            Method = method,
            RoutePattern = routePattern,
            Purpose = purpose,
            ServiceHint = serviceHint,
            IsExistingSurface = isExistingSurface
        };

    private static string BuildProcessingSurfaceLine(CommunityLedgerProcessingSurfaceResponse surface)
    {
        var status = surface.IsExistingSurface ? "기존" : "예정";
        var route = BuildProcessingSurfaceHint(surface);

        return $"- [{status} {surface.HandoffMode}] {route}: {surface.Purpose}";
    }

    public static string BuildProcessingSurfaceHint(CommunityLedgerProcessingSurfaceResponse surface)
    {
        if (!string.IsNullOrWhiteSpace(surface.RoutePattern))
        {
            return $"{surface.Method} {surface.RoutePattern}";
        }

        return !string.IsNullOrWhiteSpace(surface.ApiEndpointKey)
            ? $"{surface.Method} {surface.ApiEndpointKey}"
            : surface.ServiceHint;
    }

    private static IEnumerable<string> BuildPersistencePolicyLines(CommunityLedgerPersistencePolicyResponse policy)
    {
        yield return $"- 원본 저장소: {policy.PrimaryStoreKind} / {policy.PrimaryStoreName}";
        yield return $"- 유연 속성: {policy.FlexibleAttributeStrategy}";
        yield return $"- RDB 반영: {policy.RelationalProjectionPolicy}";

        foreach (var target in policy.RelationalProjectionTargets)
        {
            yield return $"- RDB 투영 대상: {target.TargetName} ({target.EntityHint}) / 링크 키: {target.LinkFieldHint} / 시점: {target.ProjectionTiming}";
        }
    }

    private static IEnumerable<string> BuildParticipationPolicyLines(CommunityLedgerParticipationPolicyResponse policy)
    {
        yield return $"- 기본 참여 방식: {policy.DefaultParticipationMode}";
        yield return $"- 역할 해석: {policy.RoleLabelPolicy}";
        yield return $"- 표시 이름: {policy.IdentityDisplayPolicy}";
        yield return $"- 행동 힌트: {policy.PermissionInterpretation}";
        yield return $"- 제한 정책: {policy.RestrictionPolicy}";
        yield return $"- 제한 트리거: {string.Join(", ", policy.RestrictionTriggers)}";
    }

    private static IEnumerable<string> BuildExperiencePolicyLines(CommunityLedgerExperiencePolicyResponse policy)
    {
        yield return $"- 시작 레벨: {policy.InitialLevelSummary}";
        yield return $"- 기준: {policy.LevelBasis}";
        yield return $"- 경험치 적립: {policy.ExperienceAccumulationPolicy}";
        yield return $"- 제한 연동: {policy.RestrictionInteractionPolicy}";

        foreach (var tier in policy.LevelTiers)
        {
            yield return $"- 성장 단계: Lv.{tier.Level} {tier.Label} ({tier.RequiredExperience} 경험치) - {tier.ParticipationScope}";
        }

        foreach (var experienceEvent in policy.ExperienceEvents)
        {
            yield return $"- 경험치 행동: {experienceEvent.DisplayName} +{experienceEvent.BaseExperience} / 근거: {experienceEvent.AuditSource}";
        }
    }

    private static IEnumerable<string> BuildCompositionRuleLines(CommunityLedgerCompositionRuleResponse rule)
    {
        yield return $"- {rule.Title} {rule.Description}";

        if (rule.RequiredLedgerTemplateKeys.Count > 0)
        {
            var names = rule.RequiredLedgerTemplateKeys
                .Select(key => Find(key).DisplayName)
                .Distinct(StringComparer.OrdinalIgnoreCase);
            yield return $"  - 선행 원장: {string.Join(", ", names)}";
        }

        if (rule.RequiredUiSectionHints.Count > 0)
        {
            yield return $"  - 먼저 필요한 UI 조각: {string.Join(", ", rule.RequiredUiSectionHints)}";
        }

        if (rule.GatedActionHints.Count > 0)
        {
            yield return $"  - 이후 열리는 행동: {string.Join(", ", rule.GatedActionHints)}";
        }
    }

    private static CommunityLedgerRoleTemplateResponse Role(
        string roleName,
        string description,
        params string[] permissions)
        => new()
        {
            RoleName = roleName,
            Description = description,
            Permissions = permissions
        };

    private static string Normalize(string? value, string fallback, int maxLength = 80)
    {
        var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private static string 처리체계표시명(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "처리 체계";
        }

        return value.Trim().Replace(" OS", " 처리 체계", StringComparison.OrdinalIgnoreCase);
    }
}
