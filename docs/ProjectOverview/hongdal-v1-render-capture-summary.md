# 홍달 1.0 렌더링/캡처 검증 요약

이 문서는 홍달 1.0 화면을 README와 상세 문서에 설명할 때 사용할 렌더링 검증 결과와 캡처 자료를 한곳에 모은다. 루트 README에는 핵심 흐름만 두고, 실제 화면이 어느 앱과 어느 단계에서 렌더링되는지는 이 문서를 근거로 삼는다.

## 현재 검증 결과

2026-07-09 기준 Android 에뮬레이터에서 MAUI/Blazor Hybrid 화면을 실제 WebView로 열고 라우트를 순회했다. 판정 기준은 본문 텍스트가 렌더링되고 `#blazor-error-ui`가 보이지 않는 것이다.

| 앱 | 확인한 라우트 수 | 결과 | 결과 파일 |
| --- | ---: | --- | --- |
| `DriverApp` | 23 | 23 OK / 0 FAIL | [`driver-route-smoke.json`](../../artifacts/android-render-check/page-smoke/driver-route-smoke.json) |
| `HongdalApp` | 30 | 30 OK / 0 FAIL | [`shipper-route-smoke.json`](../../artifacts/android-render-check/page-smoke/shipper-route-smoke.json) |
| `WarehouseManagerApp` | 22 | 22 OK / 0 FAIL | [`warehouse-route-smoke.json`](../../artifacts/android-render-check/page-smoke/warehouse-route-smoke.json) |

`DriverApp`의 `/driver/home`은 처음 검증 때 Blazor 오류 UI가 보였고, `MudButton.OnClick` 수동 렌더링 콜백 타입 문제를 제거한 뒤 다시 23개 라우트가 모두 통과했다.

`HongdalAdmin-P16`~`HongdalAdmin-P22` 및 `HongdalAdmin-P22-1` 화면은 로컬 개발 서버를 메모리 데이터 모드로 실행한 뒤 Chrome headless로 캡처했다. 관리자 화면은 Android 앱 라우트 스모크와 별도로, 1.0 운영 확인에 필요한 8개 웹 라우트(`/dashboard`, `/requests`, `/requests/{RequestId}`, `/dispatch/wait`, `/drivers/operating`, `/transports`, `/transports/{RequestId}`, `/transports/{RequestId}/events`)를 기준으로 문서 캡처를 만들었다.

2026-07-11에는 당시 존재하던 `HongdalAdmin` 41개 라우트에 개발용 관리자 인증 세션과 문서용 메모리 데이터를 붙여 다시 캡처했다. 최종 판정은 41 OK / 0 CHECK이며 결과 파일은 [`capture-results.json`](../../artifacts/page-capture-check/HongdalAdmin-all-pages-final-2026-07-11/capture-results.json)에 둔다. 이후 추가된 `HongdalAdmin-P40` 개발용 Fake PG/정산 콘솔은 전용 캡처 대기 상태이며, 현재 전체 페이지 현황은 [코드 프로젝트별 전체 페이지 카탈로그](app-page-catalog.md)를 기준으로 한다. 오래 남길 이미지는 `docs/ProjectOverview/assets/app-pages/HongdalAdmin/` 아래의 페이지 ID 파일명으로 정리했다.

## 대표 캡처

아래 캡처는 README 본문에 직접 많이 넣기보다는, 홍달 1.0 필수 페이지 설명에서 필요한 곳에 연결한다. 실제 고객 정보, 주소, 연락처, 계좌, POD 원본은 캡처에 넣지 않는다.

| 앱 | 대표 캡처 | 문서에서 쓰는 용도 |
| --- | --- | --- |
| `DriverApp` | [`DriverApp-after-home-fix.png`](../../artifacts/android-render-check/DriverApp-after-home-fix.png) | 기사 지도 홈에서 Blazor 업무 화면으로 진입한 뒤 홈 허브가 오류 없이 열리는지 설명 |
| `DriverApp` | [`DriverApp-embedded.png`](../../artifacts/android-render-check/DriverApp-embedded.png) | 네이티브 지도 홈, 추천 배너, 진행 중 운송 하단 흐름의 대표 이미지 |
| `HongdalApp` | [`HongdalApp-embedded.png`](../../artifacts/android-render-check/HongdalApp-embedded.png) | 화주 업무 진입, 운송 의뢰, 창고/판매/통관 업무로 이어지는 흐름의 대표 이미지 |
| `WarehouseManagerApp` | [`WarehouseManagerApp-embedded.png`](../../artifacts/android-render-check/WarehouseManagerApp-embedded.png) | 창고 작업 보드, 입고/검수/피킹/포장 흐름의 대표 이미지 |

## 상위/하위 페이지 캡처 색인

문서에 오래 남길 이미지는 `docs/ProjectOverview/assets/v1-pages/`로 복사했다. `artifacts/` 아래 파일은 검증 산출물이고, README나 상세 문서에서는 아래 assets 경로를 기준으로 사용한다. 실제 이미지는 [홍달 1.0 필수 페이지 기준](hongdal-v1-required-pages.md)의 `상위/하위 페이지별 캡처` 섹션에 직접 첨부한다.

| 번호 | 화면 | 라우트 | 캡처 |
| --- | --- | --- | --- |
| HongdalApp-P01 | 화주 홈/업무 진입 | `/shipper` | [P01-화주홈.png](assets/v1-pages/P01-화주홈.png) |
| HongdalApp-P02 | 운송 의뢰 작성 | `/shipper/request` | [P02-운송의뢰작성.png](assets/v1-pages/P02-운송의뢰작성.png) |
| HongdalApp-P03 | 의뢰 상세/타임라인 | `/shipper/request/{RequestId}` | [P03-의뢰상세타임라인.png](assets/v1-pages/P03-의뢰상세타임라인.png) |
| HongdalApp-P03-1 | 결제/입금 안내 | HongdalApp-P03 내부 섹션 | [P03-1-결제입금안내.png](assets/v1-pages/P03-1-결제입금안내.png) |
| HongdalApp-P03-2 | 예외/분쟁 확인 | HongdalApp-P03 내부 섹션 | [P03-2-예외분쟁확인.png](assets/v1-pages/P03-2-예외분쟁확인.png) |
| DriverApp-P06 | 운행 시작 | `/driver/work/start` | [P06-운행시작.png](assets/v1-pages/P06-운행시작.png) |
| DriverApp-P07 | 지도 홈/추천 배너 | `/driver/home` | [P07-지도홈추천배너.png](assets/v1-pages/P07-지도홈추천배너.png) |
| DriverApp-P07-1 | 기사 업무 허브/요약 | `/driver/home/summary` | [P07-1-기사업무허브.png](assets/v1-pages/P07-1-기사업무허브.png) |
| DriverApp-P08 | 추천 목록 | `/driver/recommendations` | [P08-추천목록.png](assets/v1-pages/P08-추천목록.png) |
| DriverApp-P09 | 추천 상세 | `/driver/recommendations/{의뢰Id}` | [P09-추천상세.png](assets/v1-pages/P09-추천상세.png) |
| DriverApp-P10 | 배차 처리 | `/driver/recommendations/{의뢰Id}/decision` | [P10-배차처리.png](assets/v1-pages/P10-배차처리.png) |
| DriverApp-P11 | 진행 중 운송 | `/driver/transports/current` | [P11-진행중운송.png](assets/v1-pages/P11-진행중운송.png) |
| DriverApp-P12 | 상차 화면 | `/driver/transports/{운송Id}/pickup` | [P12-상차화면.png](assets/v1-pages/P12-상차화면.png) |
| DriverApp-P13 | 하차 화면 | `/driver/transports/{운송Id}/dropoff` | [P13-하차화면.png](assets/v1-pages/P13-하차화면.png) |
| DriverApp-P14 | 기사 월정산 확인 | `/driver/settlements/current-month` | [P14-기사정산확인.png](assets/v1-pages/P14-기사정산확인.png) |
| DriverApp-P15 | 기사 알림함 | `/driver/notifications` | [P15-알림함.png](assets/v1-pages/P15-알림함.png) |
| HongdalAdmin-P16 | 관리자 대시보드 | `/dashboard` | [P16-관리자대시보드.png](assets/v1-pages/P16-관리자대시보드.png) |
| HongdalAdmin-P17 | 관리자 의뢰 목록 | `/requests` | [P17-관리자의뢰목록.png](assets/v1-pages/P17-관리자의뢰목록.png) |
| HongdalAdmin-P18 | 관리자 의뢰 상세 | `/requests/{RequestId}` | [P18-관리자의뢰상세.png](assets/v1-pages/P18-관리자의뢰상세.png) |
| HongdalAdmin-P19 | 관리자 배차대기 | `/dispatch/wait` | [P19-관리자배차대기.png](assets/v1-pages/P19-관리자배차대기.png) |
| HongdalAdmin-P20 | 기사 운행 현황 | `/drivers/operating` | [P20-기사운행현황.png](assets/v1-pages/P20-기사운행현황.png) |
| HongdalAdmin-P21 | 운송 목록 | `/transports` | [P21-운송목록.png](assets/v1-pages/P21-운송목록.png) |
| HongdalAdmin-P22 | 운송 상세 | `/transports/{RequestId}` | [P22-운송상세.png](assets/v1-pages/P22-운송상세.png) |
| HongdalAdmin-P22-1 | 운송 이벤트 | `/transports/{RequestId}/events` | [P22-1-운송이벤트.png](assets/v1-pages/P22-1-운송이벤트.png) |

## README에 반영하는 방식

루트 README는 1페이지 보고서처럼 유지한다. 캡처는 README에 많이 넣지 않고 다음 세 가지만 보여준다.

1. 홍달 1.0 중심 흐름: `운송 의뢰 -> 기사 추천 -> 수락/거절 -> 상차 -> 하차 -> POD/정산 후보`
2. 화면 검증 링크: 이 문서와 [홍달 1.0 필수 페이지 기준](hongdal-v1-required-pages.md)
3. 현재 검증 상태: `DriverApp`, `HongdalApp`, `WarehouseManagerApp`의 렌더링 스모크가 통과했다는 요약

상세한 화면 번호, 캡처 예정 경로, 보안/암호화 확인, 페이지 간 상태 반영 관계는 [홍달 1.0 필수 페이지 기준](hongdal-v1-required-pages.md)에 둔다.

## 스테이지별 활용 기준

문서에서 화면을 설명할 때는 라우트 전체를 나열하기보다 아래 스테이지 단위로 묶는다. 이렇게 해야 독자가 앱별 화면을 보는 동시에 업무 흐름을 놓치지 않는다.

| 스테이지 | 주요 앱/화면 | 문서화 목적 |
| --- | --- | --- |
| S01 화주 의뢰 생성 | `HongdalApp` `/shipper/request`, `/shipper/request/{RequestId}` | 화주가 의뢰를 만들고 결제, 배차, 수락, 상하차, 정산 상태를 한 화면에서 추적한다. |
| S02 기사 운행 시작 | `DriverApp` `/driver/work/start`, `/driver/home` | 운행 시작, 위치 송신, 지도 홈, 추천 수신 진입점을 보여준다. |
| S03 추천 판단 | `DriverApp` `/driver/recommendations`, `/driver/recommendations/{의뢰Id}` | 기사에게 운임, 거리, 업무 유형, 증빙 조건, 경로 이점을 어떻게 보여주는지 설명한다. |
| S04 수락/거절 | `DriverApp` `/driver/recommendations/{의뢰Id}/decision` | 수락, 거절, 보류, 만료 이후 서버 상태 전이를 설명한다. |
| S05 상차/하차 증빙 | `DriverApp` `/driver/transports/current`, `/pickup`, `/dropoff` | 사진 업로드, POD, 인수증/서명, 예외 신고가 운송 상태를 닫는 지점을 설명한다. |
| S06 창고 작업 | `WarehouseManagerApp` `/work-board`, `/work/{ProcessCode}`, `/work/picking-batch` | 출고 배치와 피킹 배치가 창고 현장 화면으로 이어지는 방식을 설명한다. |
| S07 운영 확인 | `HongdalAdmin` 운송 상세, 이벤트, 증빙, 정산, AI 배차 검토 화면 | `HongdalAdmin-P16`~`HongdalAdmin-P39` 캡처로 운송 원장, 분쟁 대비 기록, AI 배차 판단 사례를 확인하는 기준을 둔다. |

## 남은 캡처 정리

상위 페이지와 현재 실제 캡처한 하위 페이지는 문서용 캡처를 붙였다. `HongdalAdmin` 전체 화면은 2026-07-11 기준으로 캡처가 완료되었으므로, 다음 단계에서는 앱 보조 화면과 창고 작업 세부 화면을 보강하면 README와 상세 문서의 설명력이 좋아진다.

| 우선순위 | 캡처 대상 | 이유 |
| --- | --- | --- |
| 1 | HongdalApp-P02-1/DriverApp-P06-1/DriverApp-P14-1/DriverApp-P15-1/DriverApp-P15-2 보조 화면 | 대량 등록, 운행 설정, 정산 안내, 알림/푸시 설정을 하위 페이지로 확인할 때 필요하다. |
| 2 | `WarehouseManagerApp` 피킹 배치 | 출고 배치 이후 창고 작업자에게 일이 어떻게 배정되는지 보여준다. |

## 캡처 파일명 원칙

페이지 식별자는 `HongdalApp-P03`, `DriverApp-P06`, `HongdalAdmin-P22-1`처럼 실제 프로젝트명을 접두사로 붙인다. 캡처 파일명은 링크 안정성을 위해 기존 `Pxx-화면명.png` 형식을 유지할 수 있다.

```text
docs/ProjectOverview/assets/v1-pages/P07-지도홈추천배너.png
docs/ProjectOverview/assets/v1-pages/P07-1-기사업무허브.png
docs/ProjectOverview/assets/v1-pages/P09-추천상세.png
docs/ProjectOverview/assets/v1-pages/P12-상차화면.png
```

Android/에뮬레이터 검증에서 생긴 임시 산출물은 `artifacts/android-render-check/`에 두고, README나 문서에 실제로 오래 남길 이미지만 `docs/ProjectOverview/assets/v1-pages/`로 옮긴다.
