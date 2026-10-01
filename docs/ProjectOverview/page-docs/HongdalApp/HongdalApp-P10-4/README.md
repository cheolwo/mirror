# HongdalApp-P10-4 - 디자이너 홈 테마 패키지 등록

[전체 화면 문서](../../README.md) / [HongdalApp 화면 목록](../README.md) / [통합 클라이언트 가이드](../../../unified-community-client.md)

## 화면 캡처

<img src="../../../assets/app-pages/HongdalApp/HongdalApp-P10-4.png" alt="HongdalApp-P10-4 디자이너 홈 테마 패키지 등록" width="720">

## 기본 정보

| 항목 | 내용 |
| --- | --- |
| 라우트 | `/community/decorations/themes/submit` |
| 내비게이션 단계 | 3단계 보조 기능 제작 페이지 |
| 소스 | [CommunityDecorationThemeSubmitPage.razor](../../../../../HongdalApp/Components/Pages/CommunityDecorationThemeSubmitPage.razor) |
| 분류 | 확장·제작 |
| 캡처 | 완료, Android 1080×2400 |

## 왜 필요한가

디자이너가 개별 이미지를 흩어 올리는 대신 공통 홈의 태극 패널 전체를 하나의 버전형 패키지로 구성하고 실제 결과를 미리 보기 위해 필요하다.

## 사용자와 화면 책임

상품명, 디자이너명, 버전, 설명, 희망 가격을 입력하고 다음 8개 시각 슬롯을 구성한다.

1. 바깥 방편
2. 바깥 반야
3. 안쪽 커뮤니티
4. 안쪽 상점
5. 가운데 간괘
6. 원형 테두리
7. 라벨
8. 접힌 손잡이

각 슬롯은 필수 대체색과 선택 PNG·WebP·SVG 이미지를 가진다. 펼친 패널, 접힌 손잡이, 밝은 배경, 어두운 배경을 미리 보고 권리와 고정 이동 영역 정책을 확인한 뒤 내 제작함 초안으로 저장한다.

## 권리·보안 점검

- 이미지 파일은 페이지 기준 최대 2MB다.
- SVG는 서버 연결 전 스크립트와 외부 참조를 제거하는 정제 과정이 필요하다.
- 커뮤니티·상점 이동 영역과 접근성 라벨은 앱이 관리하며 테마가 바꾸지 못한다.
- 현재 저장 결과는 로컬 초안이며 공개 판매, 운영자 검수, 실제 결제, 환불, 정산을 수행하지 않는다.

## 다른 화면과의 관계

- 이전: [HongdalApp-P10 상점](../HongdalApp-P10/)
- 저장 결과: [HongdalApp-P10-1 상세](../HongdalApp-P10-1/)
- 적용 결과: [HongdalApp-P00 통합 홈](../HongdalApp-P00/)
