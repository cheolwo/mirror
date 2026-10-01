# DriverApp-P01 - 로그인

[전체 화면 문서](../../README.md) / [DriverApp 화면 목록](../README.md) / [앱 전체 카탈로그](../../../app-page-catalog.md)

## 화면 캡처

<img src="../../../assets/app-pages/DriverApp/DriverApp-P01.png" alt="DriverApp-P01 화면 캡처" width="720">

## 기본 정보

| 항목 | 내용 |
| --- | --- |
| 앱 | DriverApp |
| 페이지 ID / 제목 | DriverApp-P01 - 로그인 |
| 라우트 | /login |
| 소스 파일 | [DriverApp/Components/Pages/Login.razor](../../../../../DriverApp/Components/Pages/Login.razor) |
| 분류 | 시스템 |
| 1.0 필수 연결 | 직접 연결 없음 |
| 캡처 상태 | 완료 |

## 왜 필요한가

이 화면은 로그인을 담당하므로, 라우팅, 오류, 샘플 화면 정리처럼 앱 운영 품질을 확인하기 위해 필요합니다.

## 사용자와 참여자

주 사용자: 기사 / 보조 참여자: 화주, 관리자, 창고 또는 현장 담당자

이 화면은 앱 운영/라우팅 보조 워크플로우 안에서 로그인 책임을 갖습니다. 화면 하나가 너무 많은 결정을 떠안지 않도록, 이 문서에서는 이 화면의 주 책임과 다른 화면으로 넘겨야 할 책임을 구분해 관리합니다.

## 화면에서 다루는 일

- 주 책임: 로그인
- 사용자가 확인해야 하는 것: 이 화면에서 상태, 입력값, 다음 행동이 명확히 보이는지 확인합니다.
- 사용자가 조작해야 하는 것: 버튼, 입력, 선택, 업로드, 조회 같은 조작이 이 화면의 책임 안에 머무는지 확인합니다.
- 화면 밖으로 넘길 일: 다른 앱이나 관리자 화면에서 처리해야 하는 상태 변경은 이 화면에 과하게 넣지 않습니다.

## 다른 화면과의 관계

- 이전 화면: [DriverApp-P00 - 기사 앱 시작 라우트 리다이렉트](../DriverApp-P00/)
- 다음 화면: [DriverApp-P02 - 기사 앱 메뉴](../DriverApp-P02/)
- 상위 화면: 없음
- 하위 화면: 없음

상호작용 관점에서는 다음 흐름을 우선 봅니다. 기사의 수락, 거절, 상차, 하차, 증빙, 정산 관련 조작은 화주 상세와 관리자 원장에 상태 변경으로 반영됩니다.

## API 경로와 코드 연결

- 화면 소스: [DriverApp/Components/Pages/Login.razor](../../../../../DriverApp/Components/Pages/Login.razor)
- 클라이언트 서비스/계약: [DriverApp/Services/AuthApiService.cs](../../../../../DriverApp/Services/AuthApiService.cs), [DriverApp/Services/AuthSession.cs](../../../../../DriverApp/Services/AuthSession.cs), [DriverApp/Services/IAuthSession.cs](../../../../../DriverApp/Services/IAuthSession.cs), [HongdalApp/Services/AuthApiService.cs](../../../../../HongdalApp/Services/AuthApiService.cs), [HongdalApp/Services/AuthSession.cs](../../../../../HongdalApp/Services/AuthSession.cs), [HongdalApp/Services/IAuthSession.cs](../../../../../HongdalApp/Services/IAuthSession.cs)

| 구분 | 메서드 | API 경로 | 클라이언트/문서 근거 | 서버 근거 |
| --- | --- | --- | --- | --- |
| 클라이언트 서비스 | POST | `api/v1/auth/login` | [DriverApp/Services/AuthApiService.cs](../../../../../DriverApp/Services/AuthApiService.cs) | `POST api/v1/auth/login` [Hongdal/Controllers/Common/인증Controller.cs](../../../../../Hongdal/Controllers/Common/인증Controller.cs) |
| 클라이언트 서비스 | POST | `api/v1/auth/login` | [HongdalApp/Services/AuthApiService.cs](../../../../../HongdalApp/Services/AuthApiService.cs) | `POST api/v1/auth/login` [Hongdal/Controllers/Common/인증Controller.cs](../../../../../Hongdal/Controllers/Common/인증Controller.cs) |

검증할 때는 이 화면이 직접 메모리 데이터만 보는지, 위 API 응답을 받아 상태를 표시하는지, 실패했을 때 사용자가 다음 행동을 알 수 있는지 확인합니다.

## 보안과 개인정보 점검

기사 개인정보, 위치, 운행 상태, 추천 수락/거절 기록은 필요한 범위 안에서만 노출해야 합니다.

## 캡처와 문서 상태

현재 캡처는 문서용 캡처 호스트 또는 기존 캡처 파일 기준으로 확인한 화면입니다.

이미지 파일을 다시 생성하면 이 README는 같은 경로의 이미지를 참조하므로 자동으로 최신 캡처를 보여줍니다.

## 보완 메모

- 화면 설명이 실제 구현과 달라지면 이 문서와 app-page-catalog.md를 함께 갱신합니다.
- 화면이 1.0 필수 워크플로우에 포함되면 hongdal-v1-required-pages.md에도 반영합니다.
- 렌더링이 깨지거나 내용이 잘리면 캡처 스크립트와 실제 화면 레이아웃을 같이 확인합니다.