# HongdalApp-P00 - 역할 기반 통합 커뮤니티 홈

[전체 화면 문서](../../README.md) / [HongdalApp 화면 목록](../README.md) / [통합 클라이언트 가이드](../../../unified-community-client.md)

## 화면 캡처

<img src="../../../assets/app-pages/HongdalApp/HongdalApp-P00.png" alt="HongdalApp-P00 역할 기반 통합 홈" width="720">

## 기본 정보

| 항목 | 내용 |
| --- | --- |
| 앱 | HongdalApp 기반 Hongdal 통합 클라이언트 |
| 라우트 | `/` |
| 내비게이션 단계 | 공통 셸·1단계 사방괘 진입 |
| 소스 | [UnifiedHome.razor](../../../../../HongdalApp/Components/Pages/UnifiedHome.razor) |
| 분류 | 필수 |
| 캡처 | 완료, Android 1080×2400 |

## 왜 필요한가

화주 앱과 창고 관리자 앱을 별도 진입점으로 외우지 않고, 현재 역할에 맞는 홈을 하나의 앱에서 열기 위한 시작 화면이다.

## 사용자와 화면 책임

주 사용자는 화주와 창고 관리자다. 이 페이지는 `HongdalClientRoleService`의 현재 역할만 판별하고 화주 홈 또는 `WarehouseManagerRoleHome`을 렌더링한다. 커뮤니티·업무 데이터·결제 처리는 하위 화면으로 넘긴다.

## 다른 화면과의 관계

- 우측 상단 사람 버튼: 역할 선택 후 `/`를 다시 연다.
- 화주: [HongdalApp-P01](../HongdalApp-P01/)로 이어진다.
- 창고 관리자: 같은 라우트에서 창고 역할 홈 컴포넌트를 렌더링한다.
- 역할별 상세 흐름: [통합 클라이언트 가이드](../../../unified-community-client.md)

## 상태·보안 점검

역할은 `Preferences`에 저장되는 UX 선택값이다. 이 값만으로 서버 권한을 부여해서는 안 되며, 실제 API는 인증 토큰의 역할·정책을 다시 검증해야 한다.
