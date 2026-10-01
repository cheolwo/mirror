# HongdalApp-P10-3 - 내 꾸미기 만들기

[전체 화면 문서](../../README.md) / [HongdalApp 화면 목록](../README.md) / [통합 클라이언트 가이드](../../../unified-community-client.md)

## 화면 캡처

현재 전용 캡처를 다시 생성해야 한다. 페이지 구현과 Android 라우트 동작은 확인했으며 카탈로그에는 `캡처 대기`로 표시한다.

## 기본 정보

| 항목 | 내용 |
| --- | --- |
| 라우트 | `/community/decorations/create` |
| 내비게이션 단계 | 3단계 보조 기능 입력 페이지 |
| 소스 | [CommunityDecorationCreatePage.razor](../../../../../HongdalApp/Components/Pages/CommunityDecorationCreatePage.razor) |
| 분류 | 확장 |
| 캡처 | 대기 |

## 왜 필요한가

구매한 상품만 쓰도록 제한하지 않고 사용자가 자신의 기호, 색상, 권리가 있는 이미지를 괘상 또는 노드 꾸미기로 사용할 수 있게 한다.

## 사용자와 화면 책임

사용 위치, 이름, 최대 두 글자 기호, 강조 색상, 이미지 파일 또는 주소를 입력받고 개인 보유함에 저장한다. 공개 판매 등록과 운영자 검수는 이 화면의 현재 책임이 아니다.

## 등록·권리 점검

- PNG, WebP, SVG, 최대 2MB
- 정사각형 512px 권장, 핵심 도형은 중앙 80%
- 긴 이미지 내 문구 지양, 대체 텍스트 필요
- 직접 제작했거나 사용권을 가진 항목이라는 확인 필수
- 공개 판매 전 별도 검수·라이선스·정산 정책 필요

## 다른 화면과의 관계

- 이전: [HongdalApp-P10 상점](../HongdalApp-P10/)
- 저장 성공: 생성된 [HongdalApp-P10-1 상세](../HongdalApp-P10-1/) 경로
