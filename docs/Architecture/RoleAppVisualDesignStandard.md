# 역할 앱 시각 디자인 기준

2026-10-03 r1. 사용자 요청은 **기존 내용과 기능을 유지하면서 모던하고 단순하며 쓰기 편한 화면으로 다듬는 것**이다. 이 문서는 색·글자·여백·표면·상호작용 표현의 기준이다. 업무 목적·정보 공개·독립 업무 분리는 [페이지 단일 책임 기준](WholeRoadmapPagePrinciple.md)이 소유한다. 디자인 변경으로 새로운 설명·기능·요약 수치를 추가하지 않는다.

## 조사한 공식 자료

2026-10-03 확인. 아래 원칙을 참고하되 특정 제품의 외형이나 구현 프레임워크를 그대로 복제하지 않는다. 오른쪽은 살뜰의 선택이며 공식 기관이 이 색상이나 글자 크기를 강제한다는 뜻이 아니다.

| 공식 자료 | 참고 원칙 | 살뜰의 적용 |
| --- | --- | --- |
| [Google Material 3](https://developer.android.com/develop/ui/compose/designsystems/material3) | color·typography·shape를 공통 테마로 구성하고 중요도에 따라 표현 | 기존 MudTheme와 XAML 자원 확장. 주 행동은 청록, 설명은 중립색, 상태색은 의미별 사용 |
| [Apple HIG Layout](https://developer.apple.com/design/human-interface-guidelines/layout) | 중요도·정렬·그룹화·공간으로 읽는 순서 구성 | 대상/현재 상태/핵심 금액/주 행동을 시각적으로 구분. 이번 작업에서 기본 내용과 기존 접기 조건은 보존 |
| [Carbon Spacing](https://www.carbondesignsystem.com/building-blocks/foundations/spacing/overview) | 일관된 간격으로 항목의 관계와 위계 표현 | 4/8/12/16/24/32 간격. 관련 항목 내부는 좁게, 다른 묶음 사이는 넓게 |
| [Android 접근성](https://developer.android.com/develop/ui/compose/accessibility/api-defaults#minimum-touch-target-sizes) | Android 터치 영역48dp 권장 | 네이티브48 DIP 목표, WebView 주요 조작48 CSS px 목표. dp·DIP·CSS px·pt를 동일 단위라고 설명하지 않음 |
| [WCAG 2.2](https://www.w3.org/TR/WCAG22/) · [글자 대비 해설](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html) | 일반 글자4.5:1·큰 글자3:1, 색만으로 의미 전달하지 않음. 포커스·확대·리플로·조작 표적 기준 | 색과 기존 상태 문구 함께 유지. 본문/보조 대비,320px·200% 확대·키보드 조작을 확인. AA 표적24px에는 예외가 있으며48px는 더 편한 조작을 위한 제품 선택 |
| [Apple Reduced Motion](https://developer.apple.com/help/app-store-connect/manage-app-accessibility/reduced-motion-evaluation-criteria) | 움직임에 민감한 사용자에게 대체/축소 제공 | 장식 이동/반복 애니메이션 추가 없음. Web의 기존 reduced-motion 대응 유지 |

## 시각 토큰

수치는 프로젝트 선택이다. 신규 폰트·아이콘 패키지·외부 이미지 없이 기존 한국어 시스템 글꼴과 아이콘을 재사용한다. 라이트 화면 기준이며 다크 모드 구현·전체 접근성 적합 인증은 별도다.

| 역할 | 기본값 | 용도 |
| --- | --- | --- |
| 주 강조 | `#0F766E`, 눌림/진한 값 `#0B5C56`, 연한 표면 `#EAF5F3` | 주 행동·현재 선택·핵심 수치. 모든 설명과 카드 제목을 강조색으로 칠하지 않음 |
| 배경/표면 | `#F5F7FA` / `#FFFFFF` | 화면과 업무 묶음을 구분. 반복 목록은 표면 하나 안의 행/여백으로 구성 가능 |
| 본문/보조 | `#172B36` / `#586B78` | 중요한 값과 설명의 위계. 미확정·실패는 보조 진단으로 희미하게 처리하지 않음 |
| 일반 경계 | `#DCE5E9` | 비활성 장식/묶음 경계. 입력/선택/포커스처럼 필수 인지 경계는 더 진한 값 사용 |
| 성공/정보/경고/오류 | `#166534` / `#1D4ED8` / `#92400E` / `#B42318` | 기존 의미 유지. 중요 경고와 실제/테스트·미확정 문구는 그대로 표시 |
| 간격 | `4/8/12/16/24/32` | 묶음 내부8~12, 카드 내부16~24, 독립 섹션24~32. 모바일 좌우16 |
| 반경 | 입력/버튼12, 큰 묶음16 | 기존 컴포넌트 체계를 재사용. 모든 행을 둥근 카드로 중첩하지 않음 |
| 글자 | 페이지24/32, 섹션18/26, 본문16/24, 보조14/20, 버튼14~16/20~24 | 역할별 크기/줄높이. 일반400·강조600·핵심 숫자700. 기존 Web 조작의16px 상속과 네이티브14를 허용. 큰 문단을 전부 굵게 만들지 않음 |
| 숫자 | `tabular-nums` 또는 네이티브 값 전용 스타일 | 금액과 거리의 축을 맞춤. 단위·소수·통화·표시 내용 변경 없음 |
| 포커스/모션 | 진한2~3px outline, 짧은 상태 전환 | 보이는 포커스. WCAG의 특정2px/3:1 AAA 규격을 AA 요구로 오인하지 않음 |

네이티브 기사 메인은 지도와 업무 패널을 함께 쓰므로 페이지20·섹션16·핵심 값18의 압축형 예외를 적용한다. 본문16·보조14·버튼14는 유지한다. Web 줄높이는 CSS/MudTypography가 소유하고 네이티브는 플랫폼 글자 측정을 사용하므로 CSS의 줄높이 수치를 네이티브 구현 완료로 취급하지 않는다. 필수 입력 경계는 `#788C97`로 흰 표면뿐 아니라 중립 캔버스 위의 대비도 확보한다.

## 나열감을 줄이는 구성 규칙

1. **여백으로 그룹화한다.** 같은 주문 안의 메뉴·금액·상태는 가까이 놓고 다음 주문과 간격을 둔다. 같은 목록에 큰 외곽 카드와 큰 내부 카드를 반복 중첩하지 않는다.
2. **강조를 배분한다.** 제목은 짧고 분명한 크기, 금액은 숫자 축, 식별자/시각은 차분한 보조 글자다. 현재 선택/주 행동/오류만 명확하게 강조한다.
3. **표면을 절제한다.** 밝은 중립 배경과 흰 표면, 약한 경계가 기본이다. 그라데이션·강한 그림자·색상별 큰 배경 상자를 줄인다. shadow 제거가 dialog/메뉴의 가림 순서를 바꾸면 안 된다.
4. **행동과 상태는 보존한다.** 버튼 문구·활성 조건·로딩·재시도·오류·수령/정산·개인정보 접기는 유지한다. 조회 실패를 빈 목록으로 바꾸거나 unknown을0으로 채우지 않는다.
5. **내용의 순서보다 읽는 순서를 먼저 정돈한다.** 기능 통합·정보 삭제·새 접기/탭·route 변경은 이번 시각 변경에서 하지 않는다. 주/보조 행동은 기존 업무 책임에 따라 표현 강도만 조정한다.

## 코드에서 참조할 위치

| 소비 지점 | 토큰/표현 소유 |
| --- | --- |
| 주문자·음식점·관리자 역할 MAUI | `Ssalddel.Ui.Common/Areas/App/Theme/RoleAppTheme.cs`, `wwwroot/Areas/App/css/role-app-foundation.css` |
| 관리자 웹 | `Areas/BackOffice/Theme/BackOfficeTheme.cs`가 같은 팔레트/글자 체계를 재사용. 실제 호스트의 `SsalddelAdmin/wwwroot/app.css` |
| 주문자 shell/주문 목록·상세 | `OrdererApp/Components/Layout/MainLayout.razor.css`, 기존 공용 Food component의 isolated CSS |
| 음식점 목록·상세 | `RestaurantDeskApp/wwwroot/app.css`, 기존 Inbox/Detail 표현 클래스 |
| 네이티브 기사 | `FDriverApp/Resources/Styles/Colors.xaml`, `AppStyles.xaml`, 기존 `MainPage.xaml`의 표시 자원 |

Figma `01 Community`와 `SsalddelApp` Community는 [호환성 정책](FigmaMauiCompatibilityPolicy.md)의 별도 대상이다. 이번 역할 업무 앱 변경을 그 디자인 적용 완료나 Figma 동기화로 보고하지 않는다. 기존 Community 전용 테마/배치/route는 보존한다.

## 적용과 검증

먼저 공통 토큰과 실제 소비 shell을 적용하고 주문자 음식 내역, 음식점 수신함/상세, 기사 메인/정산, 관리자 음식 추적을 대표 화면으로 확인한다. 공통 테마 상속과 각 페이지 수동 정리/실제 검증은 별개다. 전수 페이지 준수 완료를 선언하지 않는다.

변경 전후에는 문구·바인딩·행동/조건·API/DB·금액이 보존됐는지 대조한다. 실제 소비 앱과 웹의 좁은 폭·긴 식별자·빈/로딩/오류/비활성 상태, 키보드 포커스, 스크롤/고정 navigation, 확대를 검증한다. PNG·APK·소스 판본과 남은 제한은 해당 `docs/Changes/` 및 상세 적용 문서에 기록한다. 소스 검사·build·브라우저 렌더·Android APK·물리 단말·실운영 증거를 구분한다.
