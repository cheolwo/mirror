# 신규 배차 수신 토글 확인과 본인 재개 기획 r61

- 기획 ID: `PLAN-OPERATIONS-DISPATCH-CORE`, 판본 `dispatch-core.r61`, 2026-10-16.
- 상태: `ManualResumeAccepted / SourceAndLocalTestsVerified / DeviceVisualNotVerified / ImplementationGapsIdentified`.
- 후속 구현 승인: [r62](driver-no-response.implementation.r62.md). 아래 미구현 표기는 r61 검토 당시 상태이며 현재 구현·검증은 r62에서 추적한다.
- 이전 기획: [세 번째 연속 미응답부터 신규 수신 중지 r60](driver-no-response-receiving-off.design.r60.md). r60의 횟수·현재 업무 보호와 직접 거절·통신 장애·집계 초기화·대기시간 미정은 보존한다.
- 사용자 요청: 지도 좌측 상단의 신규 배차 수신 토글이 화면과 동작에 연결돼 있는지 검증하고, 기사 본인이 켜야 다시 받도록 한다.

## 확정한 기획

`driver-no-response-receiving-resume-mode`는 `Answered / DriverManualToggleResumeAccepted`다. 세 번째 연속 미응답으로 신규 수신이 중지된 뒤에는 기사 본인이 **신규 배차 받기**를 켜서 재개한다. 운행 종료·진행 배달 취소는 하지 않는다. 시간 경과·정기 조회만으로 수신을 자동 재개하는 정책을 추가하지 않는다.

이 답변은 수신 재개의 주체와 조작을 정한다. 미응답 집계 초기화와 대기 순위까지 확정하지 않는다. 최초 운행 시작의 수신 자동 ON은 아래와 같이 기존 코드의 별도 동작으로 보고하며, 이번 검증에서 임의로 변경하지 않는다.

## 현재 코드에서 확인한 것

| 확인 대상 | 판정과 근거 |
| --- | --- |
| 음식 기사 앱 지도 좌측 상단 | `FDriverApp/Pages/MainPage.xaml`의 `FoodDispatchReceptionControl`이 지도와 같은 Grid에 `HorizontalOptions=Start`, `VerticalOptions=Start`, `ZIndex=1`로 배치돼 있다. `FoodDispatchReceptionSwitch`와 켜짐·꺼짐 안내가 있다. 이는 XAML 구조 확인이며 휴대폰 픽셀 확인은 아니다. |
| 직접 ON/OFF | `MainPage.xaml.cs`의 `OnDispatchReceptionToggled`가 PageModel 명령을 호출한다. `IsToggled`는 서버 재조회값을 표시하는 OneWay 바인딩이며, 조작 즉시 성공으로 확정하지 않는다. |
| 서버 연결·응답 유실 | `MainPageModel.FoodExceptions.cs`가 기존 `PUT api/v1/driver/operational-dispatch/availability/intent`를 사용한다. 저장된 요청 ID와 정본 재조회로 변경 결과를 확인한다. |
| 공통 음식 기사 화면 | `FoodDriverDispatchToggle.razor`는 `role=switch`·`aria-checked`와 켜짐·꺼짐·변경 중·확인 필요를 표시한다. `RoleMapWorkspace.razor.css`는 지도 `top:12px; left:12px`에 고정하며 목록 보기에는 목록 상단에 둔다. |
| 비콘 표현 | 현재는 작은 스위치와 상태 문구다. 별도의 비콘 아이콘·점멸 애니메이션이 구현됐다고 보지 않는다. |
| OFF의 실제 의미 | 음식 추천 후보와 기존 제안 수락에서 OFF를 검사한다. 이미 수락한 업무의 정보·가게 도착·중단 등은 보존한다. OFF를 운행 종료로 처리하지 않는다. |

ON은 배차를 받을 의사이며 무조건 배차를 받을 권한이나 약속이 아니다. 운행·위치·업무 적합성과 서버의 실효 상태도 통과해야 한다. 일반 위치 갱신도 기존 운행·진행 업무·권한 조건을 따른다.

## 아직 맞지 않는 부분

1. **운행 시작의 자동 ON:** `MainPageModel.AutomaticWork.cs`와 공통 `FoodDriverRoleWorkspaceAdapter`는 본인의 운행 시작이 정본에서 확인되면 수신 ON을 요청한다. 이후 직접 OFF한 상태는 정기 조회로 뒤집지 않는다. 최초 시작도 반드시 토글을 따로 켜야 하는 흐름과는 차이가 있으므로 후속 구현 범위를 정할 때 구분한다.
2. **서버 중지와 토글 표시:** 현재 네이티브 `IsDispatchReceptionOn`과 공통 토글은 수신 의사를 표시한다. 서버 실효 상태가 `서버일시정지`여도 의사가 ON이면 켜짐으로 보일 수 있다. 별도 대기 안내는 일시 중지·운영자 문의이며, 토글만 보고 실제 수신 가능 여부를 판단하기 어렵다.
3. **직접 재개 경로:** 현 수신 의사 PUT은 기존 서버 실효 제한을 보존한다. OFF 뒤 ON으로 변경하는 것만으로 서버 일시정지가 풀리지 않는다. 향후 미응답 중지에서 본인 재개를 허용할 때는 해당 중지 사유만 권한·현재 상태를 재검사하여 해제해야 하며, 다른 서버 제한을 함께 해제하지 않는다. 이는 제안된 개발 경계이며 이번에 구현한 API가 아니다.
4. **연속 미응답 집계:** r60의 세 번째 미응답 중지·사유 안내·중복 방지·직접 재개를 연결한 제품 구현은 아직 없다. 서버 자동 중지를 `DriverExplicitIntent`로 기록하지 않는 구분도 필요하다.

`DriverApp` 화물 앱 소스에서는 같은 수신 토글과 operational-dispatch intent 호출을 찾지 못했다. `운행시작Page.razor`의 커뮤니티 공개 checkbox와 운행 시작·종료는 이 수신 토글과 다른 기능이다. 기존 음식 기사 화면의 구현 사실을 화물·생활 퀵의 동일한 토글/효과 구현 완료로 확대하지 않는다. 모든 역할의 픽셀·접근성·기기 통합 검증도 수행한 것으로 표시하지 않는다.

## 이번 검증과 변경 범위

- 기존 집중 시험 **156개 통과, 실패·건너뜀 0개**다. 제품 시험 프로젝트를 현재 소스로 build한 뒤 후속 시험은 그 산출물을 재사용했다.
- `FoodDriverMapControlsTests`, `FDriverAutomaticWorkTests`, `FDriverWaitingPresentationTests`, `운영배차공통PersistenceTests`, `운영배차수신상태PolicyTests`: 136개.
- 실제 루프백 HTTP·SQLite의 `food-ineligible-and-queue-recovery`: 1개. OFF 후보가 없을 때 추천·알림이 없고, 명시 ON 뒤 유효 후보가 추천되는 흐름을 확인했다. 연속 미응답 자동 중지나 실제 FCM 수신 시험은 아니다.
- OFF 후보 제외·수신 조회 실패 시 제외·수락된 업무 유지: 3개. `FDriverFoodExceptionIntentTests`: 16개.
- 로그와 TRX: `artifacts/local/validation/20261016-dispatch-reception-r61/`.
- 공통 UI 시험의 HTML 렌더와 네이티브 PageModel 시험은 실제 Android 화면·터치·지도 SDK·알림음 검증과 구분한다. 이번에는 USB·APK 설치·단말 화면 촬영을 하지 않았다.
- 이번 변경은 이 기획 본문과 목차다. 제품 코드·설정·운영 데이터·미응답 정책·APK·모델·commit/push는 변경하지 않았다. r58 관리자 가상 퀵 도구를 기사 화면 검증으로 승격하지 않는다.

## 다음 기획 항목

- `driver-no-response-consecutive-reset`: `Proposed / NotAsked / Unanswered`.
- 수락 또는 명시적 거절처럼 기사 응답이 확인되면 연속 미응답을 초기화할지 정할 수 있다. 수신 재개·통신 실패·시간 경과·중복 알림은 별도 경계이며 현재 학습 정답이나 제품 정책으로 확정하지 않는다.
