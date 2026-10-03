# RestaurantDeskApp 화면 문서

[전체 화면 문서](../README.md) / [앱 전체 카탈로그](../../app-page-catalog.md)

이 문서는 RestaurantDeskApp 에 속한 화면별 README를 모은 색인입니다. 각 화면 문서는 캡처 이미지, 화면 책임, 사용자와 참여자, API/서버 연계, 보안 점검을 별도로 설명합니다.

기존 `/login`의 [Login](../../../../RestaurantDeskApp/Components/Pages/Login.razor)은 [AuthLayout](../../../../RestaurantDeskApp/Components/Layout/AuthLayout.razor)을 사용해 계정 인증만 담당하고, 로그인 성공 후 기존 가게 업무로 복귀합니다. 업무 drawer·하단 메뉴와 인증 입력을 분리하는 기준과 검증은 [로그인·업무 책임 카드](../auth-workspace-separation-r1.md), [페이지 원칙의 인증 경계](../../../Architecture/WholeRoadmapPagePrinciple.md#인증과-업무의-경계)를 참조합니다. 별도 로그인 상세 README가 없어 이 수동 목차에서 연결하며 기존 페이지 ID는 유지합니다.

| 페이지 ID / 제목 | 라우트 | 분류 | 화면 책임 | 캡처 |
| --- | --- | --- | --- | --- |
| [RestaurantDeskApp-P01 - 음식점 데스크 홈](RestaurantDeskApp-P01/) | / | 보조 | 음식점 데스크 홈 | 완료 |
| [RestaurantDeskApp-P02 - 주변 음식점 조회](RestaurantDeskApp-P02/) | /restaurants/nearby | 확장 | 주변 음식점 조회 | 완료 |
| [RestaurantDeskApp-P02-1 - 인기 음식점 조회](RestaurantDeskApp-P02-1/) | /restaurants/popular | 확장 | 인기 음식점 조회 | 완료 |
| [RestaurantDeskApp-P03 - 리뷰 관리](RestaurantDeskApp-P03/) | /reviews/moderation | 운영 | 리뷰 관리 | 완료 |
| [RestaurantDeskApp-P04 - 배차 주소 입력/검증 폼](RestaurantDeskApp-P04/) | /dispatch/address-form | 보조 | 배차 주소 입력/검증 폼 | 완료 |