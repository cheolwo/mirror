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
| 2.0 운송 필수 연결 | 직접 연결 없음 |
| 캡처 상태 | 완료 |

## 페이지 책임 (2026-10-03 인증 복귀)

기사가 로그인하고 기존 업무의 내부 경로로 돌아오는 한 가지 결과를 소유한다. 아이디·비밀번호·인증 오류·로그인 행동을 유지하고, 푸시 등록은 인증 성공과 분리한다. 만료 또는 최종 거절은 현재 세션만 종료하고 일시 서버 장애는 재시도할 수 있도록 보존한다. 새 로그인과 토큰 갱신의 판본을 구분하며 다른 로그인 뒤 오래된 응답을 적용하지 않는다. 서버/API 계약과 로그인 화면 구성은 유지한다. 소스·시험·현재 실제 화면 검증은 [이번 보완 기록](../../cargo-workflow-recovery-r1.md)에서 구분한다.

[이번 화물 보완·검증 범위](../../cargo-workflow-recovery-r1.md) · [최신 변경 기록](../../../../Changes/2026-10-03-cargo-workflow-recovery-r1.md).

기존 `/login`은 [AuthLayout](../../../../../DriverApp/Components/Layout/AuthLayout.razor)에서 인증 입력·오류·복귀만 표시하고 업무 메뉴·하단 탭을 함께 조립하지 않습니다. 기존 내부 업무 복귀 경로와 토큰 처리는 유지합니다. 표시 책임과 최신 검증은 [로그인·업무 책임 카드](../../auth-workspace-separation-r1.md), [페이지 원칙의 인증 경계](../../../../Architecture/WholeRoadmapPagePrinciple.md#인증과-업무의-경계)를 참조합니다.

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
- 클라이언트 서비스/계약: [DriverApp/Services/AuthApiService.cs](../../../../../DriverApp/Services/AuthApiService.cs), [DriverApp/Services/AuthSession.cs](../../../../../DriverApp/Services/AuthSession.cs), [DriverApp/Services/IAuthSession.cs](../../../../../DriverApp/Services/IAuthSession.cs), [SsalddelApp/Services/AuthApiService.cs](../../../../../SsalddelApp/Services/AuthApiService.cs), [SsalddelApp/Services/AuthSession.cs](../../../../../SsalddelApp/Services/AuthSession.cs), [SsalddelApp/Services/IAuthSession.cs](../../../../../SsalddelApp/Services/IAuthSession.cs)

| 구분 | 메서드 | API 경로 | 클라이언트/문서 근거 | 서버 근거 |
| --- | --- | --- | --- | --- |
| 클라이언트 서비스 | POST | `api/v1/auth/login` | [DriverApp/Services/AuthApiService.cs](../../../../../DriverApp/Services/AuthApiService.cs) | `POST api/v1/auth/login` [Ssalddel/Controllers/Common/인증Controller.cs](../../../../../Ssalddel/Controllers/Common/인증Controller.cs) |
| 클라이언트 서비스 | POST | `api/v1/auth/login` | [SsalddelApp/Services/AuthApiService.cs](../../../../../SsalddelApp/Services/AuthApiService.cs) | `POST api/v1/auth/login` [Ssalddel/Controllers/Common/인증Controller.cs](../../../../../Ssalddel/Controllers/Common/인증Controller.cs) |

검증할 때는 이 화면이 직접 메모리 데이터만 보는지, 위 API 응답을 받아 상태를 표시하는지, 실패했을 때 사용자가 다음 행동을 알 수 있는지 확인합니다.

## 보안과 개인정보 점검

기사 개인정보, 위치, 운행 상태, 추천 수락/거절 기록은 필요한 범위 안에서만 노출해야 합니다.

## 캡처와 문서 상태

현재 캡처는 문서용 캡처 호스트 또는 기존 캡처 파일 기준으로 확인한 화면입니다.

이미지 파일을 다시 생성하면 이 README는 같은 경로의 이미지를 참조하므로 자동으로 최신 캡처를 보여줍니다.

## 보완 메모

- 화면 설명이 실제 구현과 달라지면 이 문서와 app-page-catalog.md를 함께 갱신합니다.
- 화면이 2.0 운송 필수 워크플로우에 포함되면 ssalddel-v1-required-pages.md에도 반영합니다.
- 렌더링이 깨지거나 내용이 잘리면 캡처 스크립트와 실제 화면 레이아웃을 같이 확인합니다.
