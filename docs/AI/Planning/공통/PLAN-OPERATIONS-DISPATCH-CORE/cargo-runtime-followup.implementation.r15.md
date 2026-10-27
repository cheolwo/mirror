# 화물 실제 검증 결손 보완 r15

- 상태: Approved / ReadyToDispatch. 사용자가 r14에서 발견한 보완점을 목표로 삼아 계속 구현·검증하도록 요청했다.
- 기준: [r14 실제 결과](../../../../Changes/2026-10-27-cargo-lifecycle-runtime-r14.md), 기존 [공통 앱 제작 절차](../../시스템/PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST/app-production-input-guide.md).
- 목표: 화주 홈 다음 행동 안내·운영자 운송 건수 의미·기사 Android 하단 겹침을 수정하고, 서버 준비 503을 안전한 시험 환경에서 진단·검증한다.

## 구현과 책임

1. 화주 홈은 기존 상세 다음 행동 정책과 후불/배차/운송 상태를 재사용한다. 결제대기와 다음 행동을 분리하고 기존 요약 생성 호출의 호환성을 유지한다. 새 결제·배차 명령을 만들지 않는다.
2. 운영자 현재 전체 목록 수의 제목을 전체 운송으로 바로잡고 완료 상태·목록·권한·조회 실패 보존을 유지한다. 새 집계 업무를 추가하지 않는다.
3. 전문 기사 Android의 화면 안전 영역을 실제 MAUI/Android 호스트 경계에서 처리한다. 시스템 바·키보드와 하단 메뉴가 겹치지 않게 하고 사용자 글자/내비게이션 설정은 바꾸지 않는다. 임의 기기별 고정 여백이나 전체 앱 공통 CSS 수정을 우선하지 않는다.
4. 서버 준비 실패는 현재 실제 실패 의존성을 조사한다. 건강 검사를 무조건 성공 처리하거나 migration/기능/권한 관문을 약화하지 않는다. 전용 합성 DB/하네스의 검증 범위와 운영 migration은 분리한다. 기존 DB/볼륨 초기화·공유 서비스 중지는 금지한다.

## 완료 조건과 중단·재개

- 변경별 집중 시험 및 영향 소비 앱 빌드, 최신 APK 입력/해시/endpoint 확인.
- 연결된 실제 휴대폰에서 수정 전후 안내·하단 메뉴·세션/운송 재조회 확인. 기기가 잠기거나 다른 작업에서 사용 중이면 해당 조작만 대기하고 독립 작업을 계속한다.
- 같은 합성 의뢰의 서버/DB 상태·중복·재시작·금융/외부 효과 경계를 유지한다. 실제 카메라/GPS·PG·송금·Push·공개 운영을 시험 결과로 승격하지 않는다.
- 준비503이 운영 migration/범위 밖 의존성에 남으면 정확한 원인과 후속 조건을 기록하며 성공으로 숨기지 않는다. 해결하지 못한 항목을 완료로 표시하지 않는다.
- 기존 사용자 앱·자료·다른 작업 변경을 보존한다. commit/push·배포·운영 활성화는 수행하지 않는다.

## 파일 소유

- 화주/운영자: `Ssalddel.Ui.Common/Areas/App/Services/ShipperHomeDashboardClient.cs`, `Ssalddel.Ui.Common/Areas/App/Components/Shipper/ShipperHomeScreen.razor`, `SsalddelAdminApp/Components/Pages/Operations.razor`와 관련 좁은 시험.
- 기사: `DriverApp/MainPage.xaml`, `DriverApp/MainPage.xaml.cs`, `DriverApp/Platforms/Android/MainActivity.cs`, 필요한 같은 폴더의 화면 inset adapter 및 관련 좁은 시험. 변경 전에 실제 적용 위치를 확정한다.
- root: 새 격리 실행/빌드/휴대폰 조작·검증 통합·보고서·CURRENT_WORK/PLANNING. 실제 기기와 DB/서버를 root만 조작한다.
- 서버 진단 담당은 우선 읽기 전용이며 제품/하네스 수정은 원인과 추가 소유 범위를 root와 결속한 뒤 시행한다.

로컬 근거는 `artifacts/local/cargo-runtime-followup-r15/`에 보존한다. 이 문서는 실행 계획이고 수정·시험·기기 성공 자체의 증거는 아니다.
