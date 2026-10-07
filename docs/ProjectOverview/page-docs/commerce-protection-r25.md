# 거래·개인정보 보호 화면 매핑

2026-10-06 r25. [페이지 원칙](../../Architecture/WholeRoadmapPagePrinciple.md), [시각 기준](../../Architecture/RoleAppVisualDesignStandard.md)을 따르는 공통 화면이다. 법령·운영 준비·보존·신원 확인 요건은 [통신판매 중개와 개인정보 보호](../../Architecture/CommerceIntermediaryPrivacy.md)에서 관리한다.

## 네 가지 페이지의 책임

| 경로 / 공통 화면 | 주체와 한 가지 결과 | 필요한 상태·정보 | 행동과 복귀 |
| --- | --- | --- | --- |
| `/commerce/notices` / `통신판매안내Page` | 누구나 현재 거래·개인정보 처리 기준을 확인 | 안내 버전·운영 연락처·준비 상태, 개별 문서는 펼쳐 보기 | 문서 조회·실패 재조회·홈 복귀. 거래 신청을 수행하지 않음 |
| `/commerce/seller` / `판매자확인Page` | 로그인한 본인이 판매자 정보를 등록하고 확인 상태를 확인 | 개인/사업자 구분·표시명·연락처, 사업자는 공개 사업 정보. 서버의 전화·이메일·성년 확인 상태 | 저장·같은 요청 재확인. 등록을 신원 확인 완료로 표시하지 않음 |
| `/commerce/privacy`와 `/{caseId}` / `보호지원Page Privacy=true` | 본인이 개인정보 권리 요청을 접수하고 진행 확인 | 요청 종류·접수 목록·현재 처리 상태·내부 안내 목표·제한 근거·보존 기한 | 접수·본인 상세·추가 내용·허용된 이의제기·목록 복귀 |
| `/commerce/disputes`와 `/{caseId}` / `보호지원Page` | 거래 당사자가 연결된 거래의 문제를 접수하고 진행 확인 | 출처 종류/원장 ID, 처리 상태·영업일 잠정 기한·허용 행동·처리 이력 | 거래 상세의 신고 링크에서 진입·접수·추가 내용·허용된 이의제기·목록 복귀 |

권리 요청과 거래 분쟁은 얇은 별도 경로로 진입한다. 본인 상태·목록·원문은 홈 지도 카드에 나열하지 않는다. 선택 사건 상세에서는 새 요청 폼 대신 그 사건만 표시한다. 서버 `AllowedActions` 중 사용자에게 허용된 `add-evidence`, `appeal`만 실행한다. 통지 준비·처리 종결·권리 실행 완료는 서로 다른 서버 상태다.

## 화면 → ViewModel → API → 저장

| 화면 | ViewModel / client | API | 저장·권한 경계 |
| --- | --- | --- | --- |
| 공개 안내 | `통신판매안내ViewModel` → `I통신판매보호Client.안내Async` | `GET api/v1/common/commerce/notices` | 버전 있는 서버 운영 설정. 공개 조회이며 개인정보 원장을 호출하지 않음 |
| 본인 판매자 정보 | `판매자확인ViewModel` → `판매자Async`, `등록Async` | `GET`, `PUT commerce/me/seller` | Mongo `commerce_seller_verifications`; 현재 인증 계정만 조회/수정. 신원 Gateway 결과는 별도 서버 검증 |
| 주문·협업의 공개 판매자 | `판매자공개정보ViewModel` → `공개판매자Async`, `공개거래판매자Async` | `GET commerce/restaurants/{id}/seller/public`, `commerce/posts/{id}/seller/public`, `commerce/collaborations/{stableId}/seller/public` | 개인은 표시명·유형·확인 상태만 공개, 검증된 사업자만 사업 정보. 생활 경로는 원장/당사자에 귀속된 서버 조회 |
| 권리 요청 | `보호지원ViewModel` → `목록Async`, `권리Async`, `상세Async`, `변경Async` | `GET/POST api/v1/common/privacy-rights-requests`; `GET {id}`, `POST {id}/commands` | Mongo `commerce_privacy_support_private`; 본인 요청만 조회/행동. 실제 실행 근거 없이 완료하지 않음 |
| 거래 문제 | 같은 ViewModel의 독립 분쟁 모드 → `분쟁Async` | 동일 형태 `api/v1/common/transaction-disputes` | 서버가 음식 주문·생활 배송·협업·화물 원장 소유/참여 검증. 상대 비공개 증거를 반환하지 않음 |

`통신판매보호Client`는 공통 `ISsalddelJsonApiClient`를 사용한다. UI는 API 실패를 빈 목록이나 확인 완료로 바꾸지 않는다. 개인정보 입력을 브라우저 저장소에 보관하지 않으며 계정 변경·로그아웃 시 본인 입력·응답을 초기화한다. 늦게 도착한 이전 계정 또는 이전 대상 응답이 새 화면을 덮지 않도록 요청 세대를 검사한다.

사건 일반 상세는 원문 아닌 서버의 제한된 처리 문구다. 별도의 본인 원문 열람 API는 감사가 필요한 절차이며 현재 이 일반 화면에서 호출하지 않는다. 사용자 추가 내용은 해당 요청에 접수하며 운영자 사건 종결 버튼을 노출하지 않는다.

## 실제 신청의 사전 확인

`거래보호Notice`는 현재 안내 버전의 명시적 확인을 받는다. 음식 주문과 생활 물품/음식 협업은 `판매자공개정보Panel`로 현재 계약 판매자를 표시하고, 확인한 `SellerRevision`을 `CommerceProtection`에 함께 전달한다. 판매자 조회 실패·판본 변경·대상 전환은 기존 확인을 해제한다. 판매자 정보와 안내를 다시 조회한 뒤 재확인할 수 있으며, 미확정 요청의 원래 내용·UUID는 해당 업무 ViewModel이 보존한다.

| 업무 | 판매자 조회 기준 | 확인이 전달되는 요청 |
| --- | --- | --- |
| 음식 주문 | 서버 음식점 ID | `음식주문등록요청.CommerceProtection` |
| 생활 물품/음식 새 협업 | 현재 교류 글 ID. 제공 요청 글에서는 서버가 현재 신청자를 판매자로 판별 | `NeighborhoodCollaborationCreateRequest.CommerceProtection` |
| 생활 물품/음식 동의·조건 수정 | 현재 협업 StableId와 서버 ProviderRole | `NeighborhoodCollaborationCommandRequest.CommerceProtection`의 Agree/UpdateTerms |
| 생활 배송·운송 도움·보관 | 판매자 판본 결속 대상 아님 | 현재 안내 버전 확인; 성년/업무 자격은 서버가 검사 |

확인 checkbox는 성년 증거, 인증기관 검증, 거래 성립, 지급 증거를 만들지 않는다. 판매자 공개 정보가 확인 이후 바뀌면 서버가 판본 불일치로 신청을 거절한다. 운영 신원기관 미연결 등 서버 준비 미완료를 UI에서 우회하지 않는다.

## 앱 경로와 검증

통합 앱 `SsalddelApp`, 웹 `Ssalddel.WebApp`, 주문자 `OrdererApp`, 음식점 `RestaurantDeskApp`의 `Commerce*Page.razor`는 동일 공통 UI를 표시하는 얇은 wrapper다. 주문자·음식점의 제한된 Route 목록에도 독립 경로를 등록했다. 홈에는 중개 사실과 안내 링크, 회원가입에는 전체 개인정보 안내 링크, 거래 상세에는 해당 원장을 연결한 신고 링크를 둔다.

네 소비 앱의 page capability 대장에도 공개 안내와 본인 처리 경계를 등록했다. 안내는 비로그인 조회이고, 판매자 등록·권리 요청·거래 문제는 인증을 요구한다. 관련 없는 역할 기능이 꺼져도 법적 안내와 본인 권리 접수는 가려지지 않는다. 이 페이지 분류는 거래 운영 준비 완료를 의미하지 않으며 서버의 준비 상태와 거래 자격 확인은 유지한다.

후속 r26에서 주문자 앱의 독립 `/login`은 기존 인증 서비스를 사용하고 안전한 `/commerce/*` 화면으로 복귀한다. 통합 앱·웹·음식점도 사건 또는 허용된 원천 식별자를 유지하며 로그인만으로 요청을 제출하지 않는다. 외부 주소·잘못 인코딩된 경로와 허용되지 않은 쿼리는 거절한다. 최신 연결과 검증 범위는 [본인 APK 업무 검증](apk-workflow-completion-r26.md)을 따른다.

`eng/CommerceProtectionPreview`는 Development·루프백 전용 호스트다. r25의 예시 DTO 검증과 r26의 격리 실제 서버 연결 모드를 구분한다. 실제 모드는 공통 Razor·client·HTTP를 사용하며 API 실패를 예시 자료로 대체하지 않는다. 기존 320/390px 공개 안내·판매자·권리·거래 문제·판본 변경 검증은 `artifacts/local/commerce-privacy-r25/ui/verification.json`과 [r25 변경 이미지](../../assets/changes/2026-10-06-commerce-privacy-r25/)에 보존한다.

r26은 별도 음식 기사 앱에서 원본 주문 번호를 연결한 문제 접수 화면을 제공하고, 웹 및 모바일 운영자 앱의 `/privacy-support` 목록·상세를 기존 관리자 API에 연결한다. 담당자가 명시적으로 요청한 비공개 기록 열람만 감사 API를 거치며 일반 상세에는 원문을 넣지 않는다. 계정·사건 전환, 권한 거절과 이탈 시 원문·미확정 입력을 폐기한다. 통지 준비와 실제 전달 근거는 구분한다.

Figma `01 Community`의 기존 Community 흐름과 신규 공통 페이지는 책임을 분리했다. 신규 안내·판매자·권리·분쟁 화면에 대응하는 Figma 프레임과 동기화 증거는 아직 없다. 로컬 화면 검토를 Figma 동기화·운영 API/DB 실행·실제 신원 확인·실휴대폰·Azure 검증으로 보고하지 않는다.
