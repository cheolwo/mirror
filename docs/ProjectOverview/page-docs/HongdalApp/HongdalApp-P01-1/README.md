# HongdalApp-P01-1 - 화주 프로필과 운영 프로필 설정

[전체 화면 문서](../../README.md) / [HongdalApp 화면 목록](../README.md) / [앱 전체 카탈로그](../../../app-page-catalog.md)

## 화면 캡처

<img src="../../../assets/app-pages/HongdalApp/HongdalApp-P01-1.png" alt="HongdalApp-P01-1 화면 캡처" width="720">

## 기본 정보

| 항목 | 내용 |
| --- | --- |
| 앱 | HongdalApp |
| 페이지 ID / 제목 | HongdalApp-P01-1 - 화주 프로필과 운영 프로필 설정 |
| 라우트 | /shipper/settings/profile |
| 소스 파일 | [HongdalApp/Components/Pages/ShipperProfileSettings.razor](../../../../../HongdalApp/Components/Pages/ShipperProfileSettings.razor) |
| 분류 | 보조 |
| 1.0 필수 연결 | 직접 연결 없음 |
| 캡처 상태 | 완료 |

## 왜 필요한가

이 화면은 화주 프로필과 운영 프로필 설정을 담당하므로, 주 업무 화면을 보조하고 사용자가 다음 행동으로 이동할 수 있게 합니다.

## 사용자와 참여자

주 사용자: 화주, 판매자, 물류 의뢰자 / 보조 참여자: 기사, 관리자, 창고 관리자

이 화면은 보조 또는 확장 업무 워크플로우 안에서 화주 프로필과 운영 프로필 설정 책임을 갖습니다. 화면 하나가 너무 많은 결정을 떠안지 않도록, 이 문서에서는 이 화면의 주 책임과 다른 화면으로 넘겨야 할 책임을 구분해 관리합니다.

## 화면에서 다루는 일

- 주 책임: 화주 프로필과 운영 프로필 설정
- 사용자가 확인해야 하는 것: 이 화면에서 상태, 입력값, 다음 행동이 명확히 보이는지 확인합니다.
- 사용자가 조작해야 하는 것: 버튼, 입력, 선택, 업로드, 조회 같은 조작이 이 화면의 책임 안에 머무는지 확인합니다.
- 화면 밖으로 넘길 일: 다른 앱이나 관리자 화면에서 처리해야 하는 상태 변경은 이 화면에 과하게 넣지 않습니다.

## 다른 화면과의 관계

- 이전 화면: [HongdalApp-P01 - 화주 업무 홈, 운송 의뢰/상태/창고/판매 업무 진입](../HongdalApp-P01/)
- 다음 화면: [HongdalApp-P01-2 - 화주 앱 메뉴/화면 노출 설정](../HongdalApp-P01-2/)
- 상위 화면: [HongdalApp-P01 - 화주 업무 홈, 운송 의뢰/상태/창고/판매 업무 진입](../HongdalApp-P01/)
- 하위 화면: 없음

상호작용 관점에서는 다음 흐름을 우선 봅니다. 화주가 입력하거나 확인한 의뢰/결제/창고 상태는 기사 앱의 추천, 관리자 원장, 창고 작업 화면으로 이어질 수 있습니다.

## API 경로와 코드 연결

- 화면 소스: [HongdalApp/Components/Pages/ShipperProfileSettings.razor](../../../../../HongdalApp/Components/Pages/ShipperProfileSettings.razor)
- 클라이언트 서비스/계약: [HongdalApp/Services/Localization/ShipperLocalizationService.cs](../../../../../HongdalApp/Services/Localization/ShipperLocalizationService.cs), [HongdalApp/Services/ShipperOperatingProfileService.cs](../../../../../HongdalApp/Services/ShipperOperatingProfileService.cs)

현재 문서와 소스에서 직접 연결된 `api/v1/...` 경로를 찾지 못했습니다. 이 화면은 정적 안내, 라우팅, 메뉴 진입, 오류 표시, 또는 다른 화면으로 넘기는 책임이 중심일 수 있습니다.

검증할 때는 이 화면이 직접 메모리 데이터만 보는지, 위 API 응답을 받아 상태를 표시하는지, 실패했을 때 사용자가 다음 행동을 알 수 있는지 확인합니다.

## 보안과 개인정보 점검

화주 주소, 연락처, 의뢰 금액, 결제 조건은 역할 기반으로 제한해 노출해야 합니다.

## 캡처와 문서 상태

현재 캡처는 문서용 캡처 호스트 또는 기존 캡처 파일 기준으로 확인한 화면입니다.

이미지 파일을 다시 생성하면 이 README는 같은 경로의 이미지를 참조하므로 자동으로 최신 캡처를 보여줍니다.

## 보완 메모

- 화면 설명이 실제 구현과 달라지면 이 문서와 app-page-catalog.md를 함께 갱신합니다.
- 화면이 1.0 필수 워크플로우에 포함되면 hongdal-v1-required-pages.md에도 반영합니다.
- 렌더링이 깨지거나 내용이 잘리면 캡처 스크립트와 실제 화면 레이아웃을 같이 확인합니다.