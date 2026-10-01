# HongdalApp-P10-2 - 꾸미기 FakePG 결제

[전체 화면 문서](../../README.md) / [HongdalApp 화면 목록](../README.md) / [통합 클라이언트 가이드](../../../unified-community-client.md)

## 화면 캡처

<img src="../../../assets/app-pages/HongdalApp/HongdalApp-P10-2.png" alt="HongdalApp-P10-2 개발용 FakePG 결제" width="720">

## 기본 정보

| 항목 | 내용 |
| --- | --- |
| 라우트 | `/community/decorations/{ProductKey}/checkout` |
| 내비게이션 단계 | 3단계 보조 기능 처리 페이지 |
| 소스 | [CommunityDecorationCheckoutPage.razor](../../../../../HongdalApp/Components/Pages/CommunityDecorationCheckoutPage.razor) |
| 분류 | 개발·확장 |
| 캡처 | 완료, Android 1080×2400 |

## 왜 필요한가

실제 결제 시스템을 연결하기 전에 전체 홈 테마 확인, 수단 선택, 동의, 승인, 보유, 명시적 적용 선택으로 이어지는 UX를 검증한다.

## 사용자와 화면 책임

가상 카드 또는 가상 간편결제를 선택하고 개발 결제 안내에 동의하면 현재 앱 실행 상태에 보유권만 추가한다. 첫 자산이나 전체 테마를 자동 적용하지 않는다. 승인 뒤 홈 테마는 별도의 `구매한 전체 테마 적용` 버튼을 제공하고, 개별 자산 상품은 상세 페이지로 돌려보낸다.

## 결제·보안 점검

- 실제 금액 청구, 환불, 창작자 정산을 수행하지 않는다.
- 현재 화면은 로컬 상태만 변경한다.
- 서버의 `POST api/v1/community/node-sticker-store/fake-pg/confirm`은 인증 및 Development 환경 검사가 있지만 아직 이 화면과 연결되지 않았다.
- 운영 환경에서는 클라이언트 금액·보유 상태를 신뢰하지 않고 서버 승인 결과로 갱신해야 한다.

## 다른 화면과의 관계

- 이전: [HongdalApp-P10-1](../HongdalApp-P10-1/)
- 결제 완료와 적용 선택: [HongdalApp-P10-2-1](../HongdalApp-P10-2-1/)
- 계속 탐색: [HongdalApp-P10](../HongdalApp-P10/)
