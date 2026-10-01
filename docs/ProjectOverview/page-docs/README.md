# 화면별 상세 README

[첨부 문서 README](../README.md) / [코드 프로젝트별 전체 페이지 카탈로그](../app-page-catalog.md)

이 폴더는 홍달 프로젝트의 각 화면을 독립 README로 설명합니다. 큰 카탈로그는 전체 위치를 찾기 위한 색인이고, 여기의 각 화면 문서는 실제 화면 캡처와 상세 설명을 함께 둡니다.

## 코드 프로젝트별 색인

| 코드 프로젝트 | 화면 수 | 필수 화면 수 | 인증 필요 캡처 수 |
| --- | ---: | ---: | ---: |
| [DriverApp](DriverApp/) | 23 | 10 | 0 |
| [HongdalAdmin](HongdalAdmin/) | 42 | 18 | 0 |
| [HumanResourcesManagerApp](HumanResourcesManagerApp/) | 1 | 0 | 0 |
| [OrdererApp](OrdererApp/) | 8 | 0 | 0 |
| [RestaurantDeskApp](RestaurantDeskApp/) | 5 | 0 | 0 |
| [HongdalApp](HongdalApp/) | 30 | 5 | 0 |
| [WarehouseManagerApp](WarehouseManagerApp/) | 13 | 0 | 0 |

## 문서 형식

각 화면 README는 다음 항목을 같은 순서로 가집니다.

| 항목 | 의미 |
| --- | --- |
| 화면 캡처 | 실제 렌더링된 화면 PNG를 인라인으로 표시합니다. |
| 기본 정보 | 앱, 페이지 ID, 라우트, 소스 파일, 분류, 캡처 상태를 봅니다. |
| 내비게이션 단계 | 공통 셸, 1단계 사방괘, 2단계 다이어그램, 3단계 데이터 페이지 중 어디에 속하는지 봅니다. |
| 왜 필요한가 | 이 화면이 업무 흐름에서 필요한 이유를 설명합니다. |
| 사용자와 참여자 | 주 사용자와 보조 참여자를 분리합니다. |
| 다른 화면과의 관계 | 이전/다음/상위/하위 화면 및 앱 간 상태 반영을 봅니다. |
| API와 서버 연계 | 서버 API, 상태 계약, 실패 처리 관점을 봅니다. |
| 보안과 개인정보 점검 | 주소, 위치, 금액, 문서, 사진, 계좌 등 민감 정보 노출을 확인합니다. |

## 관리 기준

- 새 @page 라우트가 생기면 app-page-catalog.md 와 이 폴더의 화면 README를 함께 갱신합니다.
- 캡처는 기존 assets/app-pages/{앱명}/{페이지ID}.png 를 참조합니다.
- 1.0 필수 화면은 hongdal-v1-required-pages.md 와도 맞춰 둡니다.
- 새로 만들거나 수정하는 화면 README에는 가능한 경우 진입 사방괘, 출발 다이어그램·노드 행동, 필요한 식별자, 뒤로 갈 때 복원할 문맥을 기록합니다.
- 프로젝트명은 코드 위치를 나타냅니다. 사용자 화면 관계는 [통합 클라이언트 3단계 내비게이션](../../Architecture/ThreeStageClientNavigation.md)을 우선합니다.
