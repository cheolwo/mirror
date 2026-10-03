# 화면별 상세 README

- [음식 알림·전표·당일 정산의 페이지/코드/DB](food-notification-daily-settlement-r1.md) · 신규94건·실제 Android 검증, 기존 전체시험7실패 구별.

[첨부 문서 README](../README.md) / [코드 프로젝트별 전체 페이지 카탈로그](../app-page-catalog.md)

이 폴더는 살뜰 프로젝트의 각 화면을 독립 README로 설명합니다. 큰 카탈로그는 전체 위치를 찾기 위한 색인이고, 여기의 각 화면 문서는 실제 화면 캡처와 상세 설명을 함께 둡니다.

현재 전수 색인은 [페이지 소스·호스트 목록](current-pages.md)이다. `@page`와 평가된 MSBuild Content/RazorComponent를 대조하며 [ID 대장](page-identities.json)은 기존 ID와 신규 ID를 보존한다. 아래 수치는 이전 상세 문서 묶음의 이력이다. 새 route의 존재를 상세 검토·실제 조작·캡처 완료로 표시하지 않는다.

첫 [문서화·보완 결과](implementation-r1.md)는 관리자 음식 운영, 기사 음식 배달, 배달 내역, 월정산을 다룬다. 세 단계는 **페이지 책임·상태 → 실제 코드 호출 → DB·원장·재조회**다. 제품 내비게이션의 사방괘→다이어그램→데이터 페이지와 별개인 문서 구조다.

기존 앱을 다듬는 다음 검토는 [역할별 목록·판단 표](role-pages.md), [검색·필터 화면](role-pages.html), [첫 모바일 검증 결과](mobile-polish-r1.md)에서 확인한다. 실제 음식배달 기사 진입점인 네이티브 `MainPage.xaml`도 포함한다. 유지·통합 후보·보류는 명시적 검토 기록이며, 아직 판단하지 않은 화면을 불필요한 화면으로 취급하지 않는다.

역할 업무 앱의 색·글자·여백·표면은 [시각 디자인 기준](../../Architecture/RoleAppVisualDesignStandard.md)을 참조한다. 현재 주문자·음식점·네이티브 기사·관리자 추적의 [적용 범위와 책임 카드](visual-design-r1.md)는 공통 테마 상속과 개별 페이지 검증을 구별한다.

후속 [기사 업무 영역 분리](driver-workspace-sections-r1.md)는 기존 MainPage의 배달·정산·내 정보와 개별 스크롤 문맥을 나누고, 배달 복귀·인증 종료·기존 서버 업무 보존의 검증을 기록한다. 관리자 지급 테스트 분리는 별도 후속 작업이다.

전체 역할 앱의 다음 보완 중 [화물 기사·MAUI 화주 연결과 조회 복귀](cargo-workflow-recovery-r1.md)는 기존 네이티브 메뉴/현재 운송 진입, 인증 수명, 상하차 직접 상세 조회와 화주 목록/선택의 늦은 응답을 다룬다. 음식 기사 실행 결과와 화물 운송 완료 증거를 구별한다.

이어 [국내 기사·현장 정보 연결](cargo-contact-window-r1.md)은 미국 선택을 제외하고 화주가 입력한 실제 담당자·상하차 시간창을 같은 의뢰 재조회와 기사 화면까지 전달하며, 창고 상품 인계 이후 상차 완료로 진행하는 조건을 정리한다.

[기사 메뉴 축약·창고 하차 연락처](cargo-menu-handoff-r1.md)는 하단 업무를 유지하면서 중복 메뉴를 목적별로 줄이고, 기존 창고 운송 초안의 실제 하차 담당자·연락처와 한국시간 입력을 같은 운송 원장에 연결한다.

[창고 출고예정·화물 기사·화주 완료 연결](cargo-warehouse-journey-r1.md)은 기존 페이지의 인계·도착·증빙·같은 의뢰 재조회 책임을 정리한다. 서버 HTTP 전체 흐름과 역할 앱 UI 검증 대기를 구분한다.

## 이전 상세 문서 묶음 색인

| 코드 프로젝트 | 화면 수 | 필수 화면 수 | 인증 필요 캡처 수 |
| --- | ---: | ---: | ---: |
| [DriverApp](DriverApp/) | 23 | 10 | 0 |
| [SsalddelAdmin](SsalddelAdmin/) | 42 | 18 | 0 |
| [HumanResourcesManagerApp](HumanResourcesManagerApp/) | 1 | 0 | 0 |
| [OrdererApp](OrdererApp/) | 8 | 0 | 0 |
| [RestaurantDeskApp](RestaurantDeskApp/) | 5 | 0 | 0 |
| [SsalddelApp](SsalddelApp/) | 30 | 5 | 0 |
| [WarehouseManagerApp](WarehouseManagerApp/) | 13 | 0 | 0 |

## 문서 형식

새 페이지와 의미 있는 UI·UX 변경은 [단일 책임 기준](../../Architecture/WholeRoadmapPagePrinciple.md)을 먼저 읽고 [책임 카드 양식](page-responsibility-template.md)을 대상 상세 README에 채웁니다. 주 사용자·한 문장 목적·기본/보조 정보·독립 업무 인계·실패/복귀를 실제 코드와 대조합니다. [최근 화면 책임 점검](page-responsibility-review-r1.md)은 같은 목적의 구성과 남은 분리를 구별한 적용 예입니다. 미검토 페이지를 준수 또는 불필요로 자동 판정하지 않습니다.

각 화면 README는 다음 항목을 같은 순서로 가집니다.

| 항목 | 의미 |
| --- | --- |
| 화면 캡처 | 실제 렌더링된 화면 PNG를 인라인으로 표시합니다. |
| 기본 정보 | 앱, 페이지 ID, 라우트, 소스 파일, 분류, 캡처 상태를 봅니다. |
| 페이지 책임 | 주 사용자·대상·한 문장 질문/결과·상태별 주 행동·기본/보조 정보·독립 업무·진입/복귀를 책임 카드로 기록합니다. |
| 내비게이션 단계 | 공통 셸, 1단계 사방괘, 2단계 다이어그램, 3단계 데이터 페이지 중 어디에 속하는지 봅니다. |
| 왜 필요한가 | 이 화면이 업무 흐름에서 필요한 이유를 설명합니다. |
| 사용자와 참여자 | 주 사용자와 보조 참여자를 분리합니다. |
| 다른 화면과의 관계 | 이전/다음/상위/하위 화면 및 앱 간 상태 반영을 봅니다. |
| API와 서버 연계 | 서버 API, 상태 계약, 실패 처리 관점을 봅니다. |
| 페이지 → 코드 → DB | route·인증·입력/로딩/오류, Client→Controller→UseCase/Domain, 읽기/쓰기 테이블·키·관계·Event/재조회와 검증 상한을 연결합니다. |
| 보안과 개인정보 점검 | 주소, 위치, 금액, 문서, 사진, 계좌 등 민감 정보 노출을 확인합니다. |

## 관리 기준

- 새 @page 라우트가 생기면 `eng/page-docs/catalog.py --write`로 현재 전체 색인을 갱신합니다. `--write` 없이 실행하면 소스와 생성물의 차이를 차단합니다. 새 상세 문서는 확인한 범위만 작성하고 기존 ID·캡처를 보존합니다.
- 캡처는 기존 assets/app-pages/{앱명}/{페이지ID}.png 를 참조합니다.
- 1.0 필수 화면은 ssalddel-v1-required-pages.md 와도 맞춰 둡니다.
- 새로 만들거나 수정하는 화면 README에는 가능한 경우 진입 사방괘, 출발 다이어그램·노드 행동, 필요한 식별자, 뒤로 갈 때 복원할 문맥을 기록합니다.
- 프로젝트명은 코드 위치를 나타냅니다. 사용자 화면 관계는 [통합 클라이언트 3단계 내비게이션](../../Architecture/ThreeStageClientNavigation.md)을 우선합니다.
