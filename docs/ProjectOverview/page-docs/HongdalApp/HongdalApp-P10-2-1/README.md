# HongdalApp-P10-2-1 - 홈 테마 구매 완료와 적용 선택

[전체 화면 문서](../../README.md) / [HongdalApp 화면 목록](../README.md) / [통합 클라이언트 가이드](../../../unified-community-client.md)

## 화면 캡처

<img src="../../../assets/app-pages/HongdalApp/HongdalApp-P10-2-1.png" alt="HongdalApp-P10-2-1 홈 테마 FakePG 구매 완료와 적용 선택" width="720">

## 기본 정보

| 항목 | 내용 |
| --- | --- |
| 라우트 | `/community/decorations/{ProductKey}/checkout` 결제 완료 상태 |
| 내비게이션 단계 | 3단계 보조 기능 처리 완료 화면 |
| 소스 | [CommunityDecorationCheckoutPage.razor](../../../../../HongdalApp/Components/Pages/CommunityDecorationCheckoutPage.razor) |
| 분류 | 개발·확장 |
| 캡처 | 완료, Android 1080×2400 |

## 왜 필요한가

상품 구매와 홈 적용을 같은 동작으로 묶지 않고, 보유권을 얻은 뒤 사용자가 현재 홈 테마를 바꿀지 다시 선택하게 한다.

## 사용자와 화면 책임

FakePG 주문 번호, 수단, 승인 금액과 구매한 전체 테마를 확인한다. `구매한 전체 테마 적용`을 누르면 방편·반야·커뮤니티·상점·간괘·테두리·라벨·접힌 손잡이 8개 슬롯이 함께 바뀐다. 상점으로 돌아가도 구매 상태는 현재 앱 실행 동안 유지된다.

## 상태·보안 점검

- 구매 완료만으로 테마를 자동 적용하지 않는다.
- 적용 동작은 보유 상태를 다시 확인한다.
- 현재 주문 번호와 보유권은 개발용 로컬 상태이며 실제 결제 증빙이 아니다.
- 운영 연결 시 서버 결제 승인과 계정 보유권을 기준으로 다시 판단해야 한다.

## 다른 화면과의 관계

- 이전: [HongdalApp-P10-2 FakePG 결제](../HongdalApp-P10-2/)
- 상품 상세: [HongdalApp-P10-1](../HongdalApp-P10-1/)
- 적용 결과: [HongdalApp-P00 통합 홈](../HongdalApp-P00/)
- 계속 탐색: [HongdalApp-P10](../HongdalApp-P10/)
