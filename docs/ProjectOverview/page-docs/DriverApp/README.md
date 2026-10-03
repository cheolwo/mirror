# DriverApp 화면 문서

[전체 화면 문서](../README.md) / [앱 전체 카탈로그](../../app-page-catalog.md)

이 문서는 DriverApp 에 속한 화면별 README를 모은 색인입니다. 각 화면 문서는 캡처 이미지, 화면 책임, 사용자와 참여자, API/서버 연계, 보안 점검을 별도로 설명합니다.

[화물 연결·조회 복귀 보완](../cargo-workflow-recovery-r1.md)은 로그인·네이티브 메뉴/현재 운송·상차/하차의 기존 경로와 API를 연결한다. [변경 기록](../../../Changes/2026-10-03-cargo-workflow-recovery-r1.md)은 실제 화면과 시험·환경 제한을 분리한다.

[기사 메뉴 축약·창고 연락처 연결](../cargo-menu-handoff-r1.md)은 기존 route를 유지하면서 펼친 메뉴와 보조 진입을 정리한다. 실제 APK 화면과 시험 결과는 [해당 변경 기록](../../../Changes/2026-10-03-cargo-menu-handoff-r1.md)을 따른다.

[창고 연계 운송 완료 책임](../cargo-warehouse-journey-r1.md)은 기존 상차·하차의 도착 명령과 같은 운송 ID 재조회, 창고 인계·증빙 조건을 설명한다. 아래 이전 캡처는 이번 도착 버튼의 실제 UI 검증 근거와 구별한다.

전체 현재 route는 [생성 색인의 DriverApp](../current-pages.md#driverapp)에서 확인한다. 아래 기존 상세 문서 ID·캡처는 보존하며, 이번에 새 음식 배달 문서를 연결했다. Web 역할 호스트는 별도 `Ssalddel.WebApp` 소스를 재사용하므로 같은 URL만으로 MAUI 화면을 실행했다고 판단하지 않는다.

| 페이지 ID / 제목 | 라우트 | 분류 | 화면 책임 | 캡처 |
| --- | --- | --- | --- | --- |
| [DriverApp-P00 - 기사 앱 시작 라우트 리다이렉트](DriverApp-P00/) | / | 시스템 | 기사 앱 시작 라우트 리다이렉트 | 완료 |
| [DriverApp-P01 - 로그인](DriverApp-P01/) | /login | 시스템 | 로그인 | 완료 |
| [DriverApp-P02 - 기사 앱 메뉴](DriverApp-P02/) | /driver/menu | 보조 | 기사 앱 메뉴 | 완료 |
| [DriverApp-P02-1 - 기사 앱 화면 노출 설정](DriverApp-P02-1/) | /driver/settings/views | 보조 | 기사 앱 화면 노출 설정 | 완료 |
| [DriverApp-P03 - 예약 운송 또는 예약 업무](DriverApp-P03/) | /driver/reservations | 확장 | 예약 운송 또는 예약 업무 | 완료 |
| [DriverApp-P04 - 탐색 캠페인/추천 확장](DriverApp-P04/) | /driver/exploration/campaigns | 확장 | 탐색 캠페인/추천 확장 | 완료 |
| [DriverApp-P05 - 운송/배달 이력 조회](DriverApp-P05/) | /driver/transports/history | 보조 | 운송/배달 이력 조회 | 완료 |
| [DriverApp-FoodDeliveries - 음식 배달 업무](DriverApp-FoodDeliveries/) | /driver/food-deliveries | 음식 배달 | 제안 총액·수락·픽업·전달과 서버 재조회 | 재캡처 대기 |
| [DriverApp-P06 - 운행 시작, 위치 송신 시작](DriverApp-P06/) | /driver/work/start | 필수 | 운행 시작, 위치 송신 시작 | 완료 |
| [DriverApp-P06-1 - 운행 조건과 선호 설정](DriverApp-P06-1/) | /driver/work/settings | 보조 | 운행 조건과 선호 설정 | 완료 |
| [DriverApp-P07 - 지도 홈, 추천 배너, 현재 운송 진입](DriverApp-P07/) | /driver/home | 필수 | 지도 홈, 추천 배너, 현재 운송 진입 | 완료 |
| [DriverApp-P07-1 - 기사 업무 허브/요약](DriverApp-P07-1/) | /driver/home/summary | 보조 | 기사 업무 허브/요약 | 완료 |
| [DriverApp-P08 - 추천 목록](DriverApp-P08/) | /driver/recommendations | 필수 | 추천 목록 | 완료 |
| [DriverApp-P09 - 추천 상세와 판단 정보](DriverApp-P09/) | /driver/recommendations/{의뢰Id} | 필수 | 추천 상세와 판단 정보 | 완료 |
| [DriverApp-P10 - 추천 수락/거절/보류 처리](DriverApp-P10/) | /driver/recommendations/{의뢰Id}/decision | 필수 | 추천 수락/거절/보류 처리 | 완료 |
| [DriverApp-P11 - 진행 중 운송과 다음 행동](DriverApp-P11/) | /driver/transports/current | 필수 | 진행 중 운송과 다음 행동 | 완료 |
| [DriverApp-P12 - 상차 증빙, 상차 예외](DriverApp-P12/) | /driver/transports/{운송Id:long}/pickup | 필수 | 상차 증빙, 상차 예외 | 완료 |
| [DriverApp-P13 - 하차 증빙, POD, 하차 예외](DriverApp-P13/) | /driver/transports/{운송Id:long}/dropoff | 필수 | 하차 증빙, POD, 하차 예외 | 완료 |
| [DriverApp-P14 - 월정산 확인](DriverApp-P14/) | /driver/settlements/current-month | 필수 | 월정산 확인 | 완료 |
| [DriverApp-P14-1 - 이용료/정산 정책 안내](DriverApp-P14-1/) | /driver/settlements/info | 보조 | 이용료/정산 정책 안내 | 완료 |
| [DriverApp-P14-2 - 기사 정산 계좌 정보](DriverApp-P14-2/) | /driver/account/bank | 보조 | 기사 정산 계좌 정보 | 완료 |
| [DriverApp-P15 - 알림함](DriverApp-P15/) | /driver/notifications | 필수 | 알림함 | 완료 |
| [DriverApp-P15-1 - 알림 수신 설정](DriverApp-P15-1/) | /driver/notifications/settings | 보조 | 알림 수신 설정 | 완료 |
| [DriverApp-P15-2 - 푸시 토큰/권한 설정](DriverApp-P15-2/) | /driver/notifications/push | 보조 | 푸시 토큰/권한 설정 | 완료 |
