[기획 · 시스템·역할별 운영 UI · PLAN-SYSTEM-ROLE-PERSPECTIVE-OPERATING-WORKSPACES-UI-COVERAGE-AUDIT · r2]

# 운영 기획·코드·UI 존재 여부 조사

- 조사 기준일: `2026-09-18`
- 상위 기획: [하나의 운영 원장과 역할별 OS 작업공간 r16](README.md)
- 조사 범위: 현재 우선순위인 운영자·주문자·판매자·기사·마트·창고·화주 역할 화면
- 후순위 분리: Unity 디오라마, City/Town/Hub/Farm 독립 게임 화면, 스토리·PlayableLoop UI
- 판정 목적: 완성도나 사용성을 점수화하지 않고 `기획`, `제품 코드`, `제품 UI`가 존재하는지만 구분한다.

## 1. 판정 기호

| 표시 | 뜻 |
| --- | --- |
| `O` | 해당 책임을 명시적으로 다루는 파일이 존재한다. |
| `△` | 인접 기능이나 일부 정보만 존재하고 현행 기획의 전용 계약·화면은 없다. |
| `X` | 조사한 제품 경로에서 전용 파일을 찾지 못했다. |

이 표시는 실행 성공, API 결속, 장치 렌더, 운영 준비 또는 기획 의도 충족을 뜻하지 않는다.

## 2. 역할·기능별 대조

| 역할·기능 | 기획 | 코드 | UI | 확인한 대표 근거 | 다음 처리 |
| --- | --- | --- | --- | --- | --- |
| 총괄 운영 홈·예외 회복 | O | O | O | `SsalddelAdminApp/Components/Pages/Operations.razor`, `FollowUpRecovery.razor`, `Ssalddel.Ui.Common/.../운영후속처리복구작업대.razor` | 새 첫 화면 문답보다 기존 화면 결속 검증 대상 |
| 운영경제성·재무 영향·손익분기 | O | O | X | `플랫폼운영경제성UseCase`, `운영재무조회UseCase`, 두 관리자 Controller·DTO와 시험은 있으나 관리자 Razor/XAML 검색 결과 없음 | **첫 UI 문답 대상** |
| 음식 배달 기사 지도 업무 | O | O | O | `FDriverApp/Pages/MainPage.xaml`, `MainPageModel.cs`, 네이티브 지도 Handler | 현재 화면은 존재. 새 지급·관계 정보만 별도 공백으로 분리 |
| 음식 배달 기사 완료 건 지급 상세 | O | △ | X | 현행 기사 지급 원장은 공제 전 합산 금액 중심. 법정 공제·실지급 동결 계약과 전용 화면 없음 | 계약·원장 선행 뒤 UI 결속 |
| 음식점-기사 함께한 배달 관계 | O | △ | X | 일반 업무 관계 기반은 있으나 음식점-기사 쌍 Projection·상호 공개 설정·배지 UI 없음 | 관계 계약과 설정을 함께 설계 |
| 음식 판매 주문 접수·조리 | O | O | O | `RestaurantDeskApp/Components/Pages/OrderInbox.razor`, `OrderDetail.razor`, `PreparationTimeSettings.razor` | 같은 카드에서 조리로 전환할지 후속 문답 유지 |
| 주문자 진행 주문 우선 홈·기사 위치 추적 | O | △ | △ | `OrdererApp/Components/Pages/FoodOrderHome.razor`, `OrderDetail.razor`는 존재하나 현행 기획의 진행 주문 우선 카드와 주문 결속 기사 위치 지도는 없음 | 위치 공유 시점 결정 뒤 전용 Projection·화면 |
| 살뜰마트 관리자 홈·피킹·포장 | O | O | O | `WarehouseManagerApp/Components/Pages/MartHome.razor`, `MartWorkBoard.razor`, `MartPickingPacking.razor` | `MartEntryScreenNext`를 해제하고 기존 UI의 데이터 결속을 검증 |
| 일반 창고 입고·검수·적치·출고 | O | O | O | `WarehouseManagerApp`의 입고·검수·적치·피킹·포장·인계 route와 화면 | 새 전면 기획보다 예외·권한 공백을 좁게 조사 |
| 생활권 소형 물류거점 신청·검토·관리 | O | O | X | Domain·Controller·Service·DTO·Migration·시험은 있으나 Web/모바일 Razor/XAML 없음 | **두 번째 UI 문답 대상** |
| 화주 운송 의뢰·인수·문제 확인 | O | O | O | `SsalddelApp`과 `Ssalddel.Ui.Common`의 Shipper 요청·화물·운송·증빙·timeline 화면 | 기존 UI를 유지하고 새 회복 상태만 결속 검증 |
| 화물 기사 추천·상차·운송·하차 | O | O | O | `DriverApp`의 기사 홈·추천·진행 운송·상차·하차·문제 보고 화면 | 새 역할 홈보다 상태 계약 회귀 검증 대상 |
| 주문 중심 연결 원장·업무망 | O | O | △ | 주문·화주·화물 인계 코드와 `CommunityOrderLedgerHierarchyDetail.razor`는 있으나 총괄 운영자용 OS 간 timeline 전용 화면은 없음 | 재무 화면 뒤 운영 timeline 필요성 문답 |

## 3. 우선순위

1. `운영경제성·재무 영향`: 서버 조회와 Preview 코드가 이미 있는데 제품 UI가 없어 운영자 앱에서 확인할 수 없다.
2. `생활권 소형 물류거점`: 신청·동의·검토·예약의 첫 서버 절편은 있으나 참여자·관리자 화면이 없다.
3. `기사 지급 상세`: 사용자 정보 계층은 닫혔지만 법정 공제·실지급·증빙 계약이 없어 화면부터 구현하면 가짜 값을 만들게 된다.
4. `음식점-기사 관계`: 완료 쌍 Projection과 양쪽 공개 설정이 없으므로 배지 화면보다 계약이 먼저다.
5. `주문자 진행 주문·위치 추적`: 기존 음식 주문 화면을 보존하고 주문에 결속된 최소 위치 사본을 마련한 뒤 확장한다.
6. `주문 중심 OS timeline`: 이미 있는 역할 화면을 대체하지 않고 총괄 운영자가 연결 원장을 따라가는 보조 화면 후보로 둔다.

Unity·디오라마·게임 스토리 화면은 현재 제품 우선순위에서 뒤로 미뤘으므로 이 문답 큐에 섞지 않는다. 화면이 없다는 이유만으로 게임 UI를 운영 UI보다 먼저 설계하지 않는다.

## 4. 첫 문답

운영경제성·재무 영향은 개별 `음식배달 OS` 화면이 아니라 기존 `PlatformOperationsOS`를 재사용한 총괄 운영 셸에서 연다. 첫 탭은 `현재 원장`, 두 번째 탭은 `시뮬레이션`으로 두는 후보를 유지한다.

```text
총괄 운영 (`PlatformOperationsOS`)
  → 범위: 전체 / 음식배달 / 화물운송 / 마트 / 창고
  → 재무 영향
      1. 현재 원장: 수익 후보·통과자금·비용·미지급·가용현금
      2. 시뮬레이션: 주문량·고정비·정책 가정을 바꾼 손익분기 Preview
```

총괄 홈은 기존 원칙대로 OS별 정상·주의·긴급 집계만 유지한다. 재무 숫자는 `재무 영향`을 눌렀을 때 열고, 실제 원장과 가정 기반 Preview를 같은 숫자인 것처럼 섞지 않는다. 개별 OS 선택은 제목 변경이 아니라 범위 필터로 표현하며, 상세 업무는 원래 책임 OS로 내려간다.

## 5. 조사 한계

- 현재 작업트리는 여러 스레드의 대규모 변경을 포함한다. 이 조사는 파일과 route 존재 여부만 읽었으며 기존 변경을 정리하거나 stage하지 않았다.
- build, API 실행, Android 렌더, 브라우저 조작, 실제 장치 검증은 수행하지 않았다.
- `UI O`는 화면 파일이 있다는 뜻일 뿐 현행 기획의 모든 상태와 버튼이 구현됐다는 뜻이 아니다.
