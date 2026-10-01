# 기획에서 역할 앱까지

[현재 대표 실행 r12](../../../docs/AI/Planning/시스템/PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST/app-production-reference-run.r12.md)는 [r9 입력 형식](../../../docs/AI/Planning/시스템/PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST/app-production-intake.r9.md)과 r8 추적 기반을 재사용한다. 기존 기획·작업지시서·검증 결과의 정본을 합치거나 운영 DB에 복제하지 않는다. Node.js 표준 라이브러리만 사용하며 패키지 설치가 없다.

## r9 제작 입력

[입력 안내](../../../docs/AI/Planning/시스템/PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST/app-production-input-guide.md)의 공통 프로필·작업 입력·검토 기록을 작성하고 대장에 선택적으로 `appProfileRef/intakeRef/intakeReviewRef`를 `{path,sha256}`로 결속한다. 양식은 `templates/`, AI 선작성 음식점 예제는 `inputs/`에 있다. 한 작업 입력은 프로필 하나/대상 앱 하나를 소유하며 여러 앱 업무는 각 입력을 따로 결속한다. 영향 역할은 빌드 대상이 아니다.

```powershell
./eng/planning-inquiries/manage-app-production.ps1 -Mode IntakeCheck -WorkItemId APP-RESTAURANT-MENU
```

`IntakeCheck`는 준비 상태를 읽기만 하며 미확인/변경/보완은 종료3이다. 기존 Write/Validate의 자료 정합성과 구별한다. 입력 상태 `Confirmed`여도 실행 승인은 별도다. `approval.grantsExecution`은 항상 false이며 기존 승인 선언을 조회할 뿐이다. 환경 결손은 관련 결과물 ID에 표시한다. 입력·그 출처·검토가 바뀌면 관련 실행 증거는 재검증 대상이다. 기존 미연결 업무는 `NotReviewed`이고 미구현이 아니다.

입력 판본은 기존 파일을 보존한 채 `.r2.json`처럼 별도 파일로 결속할 수 있으며 `schemaVersion` 변경을 뜻하지 않는다. `Changed`인 입력의 충돌·환경 설명은 **입력 당시 기록**이지 현재 코드의 미해결 판정이 아니다. HTML·Markdown·인계 Markdown에도 이를 표시하고, 인계 JSON은 원래 `Changed`와 충돌 값을 보존한다. 후속 결과를 확인해 새 입력을 결속하되 과거 입력·보고서를 자동 해결하거나 Current로 승격하지 않는다.

상태판과 인계에는 허용된 요약만 들어간다. 원시 입력 JSON은 로컬 HTTP로 제공하지 않는다. 명백한 비밀 필드/패턴을 차단하지만 임의 개인정보를 완전히 찾아내는 보안 제품은 아니므로 입력·내보내기를 검토한다. Source anchor는 별도 필드이며 파일 hash와 경로를 검사하고 제목의 의미를 자동 판정하지 않는다.

APK 계획은 `eng/release/publish-mobile-field-test.ps1 -AppNames restaurant -ServerBaseAddress https://example.invalid -PlanOnly`로 음식점 하나만 확인할 수 있다. 생략 시 기존 네 앱 전체이며 실제 생성에는 HTTPS와 저장소 밖 서명키/환경변수 조건이 필요하다. 준비 점검은 서명·포장·설치·배포를 수행하지 않는다.

## 읽기와 생성

저장소 루트에서 실행한다. Node.js가 PATH에 있어야 한다.

```powershell
./eng/planning-inquiries/manage-app-production.ps1 -Mode Write
./eng/planning-inquiries/manage-app-production.ps1 -Mode Validate
./eng/tests/app-production.ps1
./eng/planning-inquiries/manage-app-production.ps1 -Mode Serve -Port 5388
```

`Write`는 `docs/AI/generated/planning-app-production.json`, `.md`, `.html`을 생성한다. `Validate`는 생성 결과의 변조/오래됨과 연결 오류를 확인하며 원본을 수정하지 않는다. `Serve`는 `127.0.0.1`에만 바인딩해 생성물과 등록된 참조만 GET/HEAD로 제공한다. 상태판은 자체 데이터를 포함하므로 `.html`을 직접 열 수도 있다. 서버나 브라우저를 열었다고 시험·업무를 자동 실행하지 않는다.

상태판과 서버는 같은 파일 제공 목록을 사용한다. 원문·코드·시험·명시된 작업지시서는 링크로 열고, 원시 `artifacts/`의 로그/manifest는 경로와 요약만 표시한다. 파일을 제공하지 않으면서 클릭 가능한 링크로 만들지 않는다. 실제 브라우저 화면 검증 결과는 기획의 구현 기록에서 확인하며 순수 DOM 계약 시험과 구분한다.

전체 기획은 `PLANNING.md`의 등록 관계, 현행 기획 폴더, 기존 문답 색인의 등록 원문을 탐색한다. 문서 역할과 조사 범위는 출력 `scope`/`documents`에서 확인한다. 임의의 가장 큰 revision 번호를 최신 승인으로 선택하지 않는다. 링크가 있으나 의미 검토가 안 된 문서는 자료 목록이지 승인/구현 판정이 아니다.

`role-app-links.json`은 기획 절·역할·코드·시험·명세·결과의 **관계**를 소유한다. 사람이 대조한 작은 업무를 추가하고 기획 hash와 정확한 파일 참조를 결속한다. 이름 유사성만으로 자동 구현 판정을 하지 않는다. 한 기획이 여러 업무, 한 업무가 여러 역할을 가질 수 있다. 누락/변경/애매함은 미확인 또는 진단으로 남긴다.

## 결과의 의미

- 코드 있음: 결속한 제품 파일이 존재함. 기획 전체 구현을 뜻하지 않음.
- 시험 코드 있음: 시험 파일이 존재함. 실행 여부와 별개.
- 결과: 기록된 Passed/Failed/NotRun/Unknown. 현재성 값과 독립적.
- Current: 그 기록에 선언된 입력·검증 범위가 일치함. 모든 기능/의존성을 전수 검증했다는 뜻이 아님.
- Stale: 입력이나 산출물이 달라 재검증 필요. 과거 결과를 삭제하지 않음.
- Unknown: 당시 기준선 또는 실행 근거가 부족함. 현재 소스를 과거 실행에 소급 결속하지 않음.
- Missing: 참조 파일 없음. 실패 실행이나 코드 부재와 구분.

역사적 결과 문서는 `HistoricalReport`로 연결하며 당시 기록 설명을 읽을 수 있어도 자동으로 Current를 부여하지 않는다. 집중시험과 전체시험, SQLite와 실제 MySQL/API, 합성 브라우저와 Android, APK와 설치/배포를 혼동하지 않는다. 서명 키·HTTPS 등 기존 배포 요구사항도 그대로다.

## 실행 기록 연결 (선택)

기존 검증·테스트 앱 패키지 도구에 `-PlanningWorkItemId`를 추가하면 실행 전에 관련 입력의 hash를 고정하고 종료 후 결과·범위·산출물 hash를 남긴다. 옵션을 생략하면 기존 동작이고 `-PlanOnly`는 기록을 생성하지 않는다.

```powershell
# 실제 대상 업무에 맞는 Paths/TestFilter를 먼저 정한다.
./eng/validate-changes.ps1 -Level Fast -Paths RestaurantDeskApp/Services/Ssalddel음식주문Client.cs `
  -PlanningWorkItemId APP-RESTAURANT-MENU

# 실행 결과로 출력된 정확한 manifest 경로를 지정한다.
./eng/planning-inquiries/manage-app-production.ps1 -Mode Write `
  -EvidencePaths artifacts/local/validation/<실행번호>/planning-evidence.json
```

이 경로는 예시이며 실행 성공을 보장하지 않는다. `Validate`와 `Serve`에도 같은 `-EvidencePaths`가 필요하다. 지속 연결하려면 해당 업무의 `evidenceRefs`에 `format: EvidenceManifest`와 실제 `kind`를 추가한다. 과거 설명을 담은 Markdown은 `HistoricalReport`로만 연결한다. 없는 증거는 `Missing`이며 다른 PC로 옮겼다고 다시 `Current`가 되지 않는다.

`publish-mobile-field-test.ps1`도 같은 선택 인자를 지원한다. HTTPS·외부 서명 키 등 기존 필수 인자를 먼저 준비해야 하며, 이 옵션만으로 APK 빌드·외부 업로드를 승인하지 않는다. APK 생성과 설치·실제 조작·출시는 각각 다른 증거다.

현재성 비교는 `DeclaredInputsOnly`다. 명시한 파일과 코드/시험 참조의 상위 디렉터리 안 파일 목록(추가·삭제 포함)을 비교하되 `bin/obj/artifacts` 등 산출물은 제외한다. 선택 실행에서는 도구가 정한 입력 파일·project 디렉터리도 포함한다. 전체 전이 의존성을 자동 추론하지 않으므로 공유 contract·빌드 설정 등 중요한 의존성은 별도 입력으로 결속해야 한다. 실행 전 기록을 사후에 만들어 과거 통과를 현재 통과로 승격하지 않는다.

상태판은 자동시험·API/DB·UI·APK·장치·배포를 별개 종류로 다루며 manifest 종류와 참조 종류가 다르면 판정을 보류한다. `TOOL-APP-PRODUCTION`은 관리 도구 자체의 시험용 업무이며, 음식 업무나 네 역할 앱의 검증 수치를 늘리지 않는다.

## GPT/개발 인계

```powershell
./eng/planning-inquiries/manage-app-production.ps1 -Mode Export `
  -PlanId PLAN-SYSTEM-REGIONAL-OPERATIONS-E2E-SCAFFOLD `
  -Destination artifacts/local/exports/app-production-review-01
```

선택한 기획의 `handoff.md`와 `handoff.json`을 새 폴더에 만든다. 기존 폴더를 덮어쓰지 않는다. 기본값은 참조·검토 요약이며 **원문/코드/로그/DB/개인자료는 자동 복사하지 않는다**. 저장소 접근이 없는 GPT에 상세 원문이 필요하면 해당 문서를 별도 검토 후 첨부해야 한다. 사본의 상태는 개발 승인이나 push된 기준선이 아니다.

## 매 작업의 순서

1. 사용자 설명과 기존 기획 접수 → 목록 갱신 → 재사용/결손 대조.
2. 업무 하나와 역할별 입력·행동·결과·실패·복구를 검토한다.
3. 사람 승인 → 정확한 기획 판본/hash·기준 commit/로컬 변경·쓰기 경로·검증 상한을 고정한다.
4. 허용 범위 구현 → 자동시험·서버·실제 UI·테스트 APK를 각각 검증한다.
5. 해당 실행 증거를 연결 → 목록 재생성/검사 → 사용자에게 결과와 남은 확인을 반환한다.

별도 상시 감시/자동 개발/운영 효과는 없다. 작업 범위에 없는 역할의 미확인 연결을 완료로 채우지 않는다. 관리 도구 구현은 네 역할 앱의 재빌드·기기 설치·실결제·출시를 의미하지 않는다.
