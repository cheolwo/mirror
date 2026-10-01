# 앱 제작 전 입력 안내

기준: [부족한 정보만 문답으로 채우는 r10](app-production-guided-intake.r10.md), 자료 형식·도구는 [r9](app-production-intake.r9.md). 입력 자료는 제품 기획을 복제한 새 정본이 아니라, 기존 PLAN과 코드에서 필요한 내용을 모은 제작용 요약이다.

## 사용 순서

1. AI가 기존 PLAN·API·화면·시험을 대조해 앱 공통 프로필과 작업 입력을 먼저 채운다.
2. 입력 점검으로 출처·hash·누락·충돌을 확인한다. 코드로 확인할 수 있는 것은 사용자가 다시 설명하지 않는다.
3. **정보가 부족할 때만** AI가 질문 하나씩 제시하고 답변을 입력에 누적한다. 기존 확정 사항은 다시 묻지 않고 기술 사실은 AI가 조사한다. 사용자는 실제 제품 선택만 답하며, 빈 양식 전체를 작성하지 않는다. 질문 우선순위·답변 반영·종료 기준은 [r10](app-production-guided-intake.r10.md)을 따른다.
4. 입력 판본을 확인한 뒤에도 기존 `Approved` 기획·작업지시서·허용 경로 수용 절차를 별도로 거친다.
5. 작업 범위의 구현·시험·화면·APK를 수행하고 같은 입력 기준의 결과를 반환한다. 장치 설치·배포는 추가 단계다.
6. [보완 순환 r11](app-production-repair-cycle.r11.md)에 따라 실패 재현→작은 보완→검증의 결과에서 적용 조건과 한계를 갖는 규칙 후보를 남긴다. 후보만으로 새 기능이나 전체 앱 공통화를 승인하지 않는다.

## 공통 프로필과 작업 입력

| 자료 | 입력할 내용 | 재사용 |
| --- | --- | --- |
| 앱 공통 프로필 | 앱 ID, project, 플랫폼, 탐색, 표현, 인증, 서버, 시험 환경 | 앱마다 한 번 작성하고 변경 시 재검토 |
| 목적·범위 | 이번 업무, 대상 앱, 영향 역할, 포함/제외 | 선택 업무만 |
| 정보·권한·표본 | 읽고 쓰는 정보, 서버 소유자, 개인정보 경계, 비식별 예 | 기존 정보대장 연결 |
| 업무 순서 | 시작→사용자 행동→서버 처리→정본 재조회→종료 | 기존 API/UseCase 연결 |
| 화면 상태 | 정상·로딩·빈 목록·실패·저장 중·성공 | 기존 UI 우선 |
| 실패·회복 | 중복, 응답 유실, 충돌, 취소, 재시도, 실패 후 복귀 | 현재 보장과 새 요구 분리 |
| 결과물 | 업무 시험, 화면 환경·입력 절차, APK 대상 및 제외 | 결과물별 환경 준비 |
| 검토 기록 | 확인한 프로필/입력 hash, 사람 확인, 기존 승인 참조, 환경 준비 | 입력 변경 시 재검토 |

복사할 양식: [프로필](../../../../../eng/planning-inquiries/app-production/templates/app.profile.json), [작업 입력](../../../../../eng/planning-inquiries/app-production/templates/task.intake.json), [검토](../../../../../eng/planning-inquiries/app-production/templates/task.review.json). 각 사실은 `status/value/critical/sources`를 가지며 출처는 저장소 상대 `path/sha256`와 선택 `revision/anchor`로 연결한다. `#anchor`는 path에 합치지 않는다. `NotApplicable`에는 이유가 필요하다.

`ExistingConfirmed`는 기존 근거로 확인한 내용, `Proposed`는 제안, `Unresolved`는 미정이다. 코드가 있다는 사실이 실행 성공이라는 뜻은 아니다. 가게·기사·주문자의 실제 계정·주소·연락처·키·토큰·서명 비밀번호를 넣지 않는다. 비밀 설정은 저장소 밖에 두고 입력표에는 준비 상태만 기록한다. 도구의 명백한 비밀 패턴 검사는 모든 민감정보를 탐지하는 보증이 아니므로 내보내기 전 사람이 내용을 검토한다.

## 상태를 읽는 법

- 제작 입력: `NotReviewed` 미연결, `NeedsInformation` 입력/검토 보완, `Confirmed` 입력 확인, `Changed` 기준 변경.
- `inputState`는 입력 완비 여부, `reviewState`는 사람 확인 여부다. 확인은 정확한 입력 hash에 결속한다.
- `approval`은 기존 실행 승인 선언을 별도로 점검한 결과다. `Declared`도 이 조회 도구가 실행 권한을 새로 주는 상태가 아니다.
- `environment`는 결과물별 준비 여부다. APK 서명이 미확인이어도 자료 준비까지 중단할 이유는 없다. 반대로 환경 준비만으로 앱 개발을 승인하지 않는다.
- 코드 존재, 시험 소스 존재, 실행 결과, 현재성은 r8의 독립 축을 유지한다. 과거 업무는 접수표 미연결을 이유로 미구현으로 바꾸지 않는다.

입력 준비와 환경 준비는 분리한다. 확인된 입력도 환경 차단이 남을 수 있다. 주요 미정·기준 불일치·차단 충돌은 작업 인계 준비가 되었다고 표시하지 않는다.

## 점검과 결과 반환

```powershell
./eng/planning-inquiries/manage-app-production.ps1 -Mode IntakeCheck -WorkItemId APP-RESTAURANT-MENU
./eng/planning-inquiries/manage-app-production.ps1 -Mode Write
./eng/planning-inquiries/manage-app-production.ps1 -Mode Validate
./eng/release/publish-mobile-field-test.ps1 -ServerBaseAddress https://example.invalid -AppNames restaurant -PlanOnly
```

`IntakeCheck`의 종료 코드 3은 보완/검토 필요이지 제품 시험 실패가 아니다. `Write/Validate`는 기획 자료 정합성 검사이며 완료 승인 관문이 아니다. `PlanOnly` 주소는 형식 점검 예시이며 실제 접속·포장하지 않는다. `-AppNames`를 생략하면 기존 네 앱 전체 대상이므로 단일 업무는 반드시 선택한다.

화면 증거에는 정적 시안 / 공용 Razor 합성 / API 연결 / Android 실행을 구별한다. APK 파일 생성, 장치 설치, 배포도 별개다. 이 입력 도구나 Node 시험을 통과한 것으로 음식 업무·실제 서버·앱 출시를 통과했다고 보고하지 않는다.

첫 적용: [음식점 메뉴 검토 초안](restaurant-menu-input-review.md).
