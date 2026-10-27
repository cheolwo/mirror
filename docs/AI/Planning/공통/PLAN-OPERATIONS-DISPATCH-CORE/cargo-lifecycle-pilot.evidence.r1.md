[기획 · 화물 한 건 검증 준비 · PLAN-OPERATIONS-DISPATCH-CORE · cargo-lifecycle-pilot.evidence.r1]

# 화물 세 역할 한 건의 파일럿 근거 준비

- 기획 ID: `PLAN-OPERATIONS-DISPATCH-CORE`.
- 상태: `PreparedInputsOnly / IntakeHumanReviewPending / EnvironmentUnknown / ExecutionNotRun / OperationalDisabled`.
- 작성일: 2026-10-26. 제품 목표는 기존 모든 업무를 포함하는 [통합 앱 1.0](../../../../Versions/unified-1.0/README.md)이다.
- 이번 승인: [앱 공통 제작 절차 r13](../../시스템/PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST/app-common-workflow.r13.md)과 [정확한 작업지시서](../../시스템/PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST/app-common-workflow.r13.work-order.json)의 공통 절차·입력/파일럿 준비에 한정한다. 화물 제품 구현이나 실제 업무 실행을 새로 승인하는 문서가 아니다.
- 접수 시 읽은 기준 commit은 `d3a19163ba96a7dbed31028f639f3bf0c534ae79`이다. 작업 트리에는 다른 작업의 변경이 있으며 commit 하나를 현재 전체 소스·서버·APK 판본의 증거로 쓰지 않는다.

## 대상과 입력

첫 대상은 같은 합성 운송 한 건을 화주·전문 기사·운영자가 이어서 처리하고, 중단 뒤 같은 대상의 결과를 확인하는 기존 흐름이다. 신규 기능·자동 배차·운영 허가·실금전은 포함하지 않는다. 하나의 파일럿 실행 ID에서 의뢰 ID와 운송 ID를 연결하되 앱마다 프로필·작업 입력·검토 기록을 독립 유지한다. 서버의 최종 상태와 앱의 표시·복구 결과를 각각 기록한다.

| 업무 항목 | 실제 대상 앱 | 영향 역할 | 입력 / 검토 |
| --- | --- | --- | --- |
| `APP-CARGO-LIFECYCLE-SHIPPER` | `shipper`, `SsalddelApp/SsalddelApp.csproj` | 화주·기사·운영자 | [입력](../../../../../eng/planning-inquiries/app-production/inputs/cargo-lifecycle-shipper.intake.r1.json) · [검토](../../../../../eng/planning-inquiries/app-production/inputs/cargo-lifecycle-shipper.review.r1.json) |
| `APP-CARGO-LIFECYCLE-DRIVER` | `cargo-driver`, `DriverApp/DriverApp.csproj` | 화주·기사·운영자 | [입력](../../../../../eng/planning-inquiries/app-production/inputs/cargo-lifecycle-driver.intake.r1.json) · [검토](../../../../../eng/planning-inquiries/app-production/inputs/cargo-lifecycle-driver.review.r1.json) |
| `APP-CARGO-LIFECYCLE-ADMIN` | `admin`, `SsalddelAdminApp/SsalddelAdminApp.csproj` | 화주·기사·운영자 | [입력](../../../../../eng/planning-inquiries/app-production/inputs/cargo-lifecycle-admin.intake.r1.json) · [검토](../../../../../eng/planning-inquiries/app-production/inputs/cargo-lifecycle-admin.review.r1.json) |

프로필은 [화주](../../../../../eng/planning-inquiries/app-production/inputs/shipper.profile.json), [전문 기사](../../../../../eng/planning-inquiries/app-production/inputs/cargo-driver.profile.json), [운영자](../../../../../eng/planning-inquiries/app-production/inputs/admin.profile.json)를 쓴다. `targetAppIds`는 연결된 프로필 하나와 일치시키고 `impactedRoles`로 인계 관계를 표현한다. 입력은 기존 사실의 제작용 요약이며 새로운 업무 정본이나 별도 상태 DB가 아니다.

새 입력의 사람 확인은 `Pending`이며 확인 주체·시각을 만들지 않았다. r13 승인 연결은 준비 범위에만 유효하다. 격리 서버·화면·Android·서명·기기 환경은 모두 `Unknown`, 이번 화물 실행은 `NotRun`이다. 현재 기기·서버가 준비돼 있다고 과거 결과에서 추정하지 않는다.

## 시작부터 역할별 재조회까지

| 단계 | 기존 화면·계약 | 다음 회차의 확인 / 현재 상태 |
| --- | --- | --- |
| 접수 | 화주 `/shipper/request`와 입력 단계, `POST api/v1/shipper/requests`, `GET .../by-client-request/{clientRequestId}` | 계정·동의·원요청 ID 보존, 응답 유실 뒤 같은 원장 확인. 기본 선결제와 승인 후불을 구분. `NotRun` |
| 결제 확보와 배차 진입 | 화주 `/shipper/request/{RequestId}/payment`, 기존 payments/후불 승인 계약 | 기본 신규 선결제의 결제 확보 전 수락 차단과 FakePG/실 PG 구분. 승인 후불 성공을 기본 선결제 성공으로 대신하지 않음. `NotRun` |
| 추천 판단 | 전문 기사 `/driver/recommendations/{requestId}/decision`, 기존 `dispatch-actions/{requestId}/accept|reject` | 현재 추천·기한·차량/수행 조건, 명시 수락/거절, 같은 요청 결과 재조회. 통합 앱의 기사 화면을 전문 기사 앱 실행으로 합산하지 않음. `NotRun` |
| 상차·하차 | 전문 기사 `/driver/transports/{transportId}/pickup|dropoff`, 기존 arrive/pickup-complete/complete API | 도착·필수 사진·허용 행동·정본 상태를 따로 확인. 실제 카메라와 합성 사진을 구분. `NotRun` |
| 화주 인수·증빙 | `/shipper/request/{RequestId}`, `/timeline`, `/proofs`, 기존 settlement/receipt | 운송 완료·실제 인수 확인·인수증 등록과 미확정 요청을 분리. `NotRun` |
| 운영자 재조회 | 모바일 `/overview/cargo`, `/operations`에서 기존 admin/transports·admin/drivers/operating | 같은 의뢰/운송을 확인하고 갱신 실패·오래된 표시를 구분. `NotRun` |
| 사건·정산 인계 | 기존 관리자 웹 `/transports/{RequestId}/events|proofs|settlement`, 기사 payout-preparations 조회 | 모바일 조회와 웹 상세의 호스트·권한·복귀를 구분. 수납·지급 준비·지급 요청·실지급을 분리. 새 지급 명령은 포함하지 않음. `NotRun` |

관리자 모바일의 현재 `AdminOperationsService`는 운송/운행기사 목록 조회다. 관리자 웹 상세 route의 존재를 모바일 상세 조작 완료로 승계하지 않는다. 완료 운송이 현재 목록에서 제외되면 그 목록 부재를 원장 소실로 판단하지 않고 기존 허용 상세/API를 별도로 확인한다. 현재 조회의 대상·시점과 결과를 기록한다.

## 중단·미확정 결과의 확인 수단

아래는 현행 소스에서 확인한 수단과 한계이며 이번 실제 복구 성공이 아니다. 모든 사례는 계정·세션·서버·대상·단계를 대조하며 늦은 응답이 새 화면을 덮지 않는지도 확인한다.

| 대상 | 재사용할 기존 수단 | 한계와 파일럿 판정 |
| --- | --- | --- |
| 화주 접수 | `ShipperRequestAuthoringPageViewModel`의 보호 기록, client request ID, 동일 원요청 조회 | 저장/재조회 실패는 미확정 유지. 다른 계정·서버에 이전 초안을 적용하지 않음. 실제 재실행은 `NotRun` |
| 화주 테스트 결제 | `FakeShipperPaymentService`의 계정·서버·의뢰별 보호 기록, 기존 원장 재조회 | 인증 요청 실패를 로컬 성공으로 대체하지 않음. 로컬 영수증으로 현재 금융 상태를 덮지 않음. 요청별 서버 영수증 확인/관리자 해제의 미구현 범위를 성공 처리하지 않음 |
| 화주 인수증 | `ShipperRequestDetailPageViewModel.Receipt`의 명시 결과 확인과 보호 기록 | 원 번호 비교 근거가 없으면 현재 등록 증빙과 이전 요청 성공을 구별. POST 자동 반복 없음 |
| 기사 상하차 | `DriverCargoCompletionRecovery`와 현재 운송 GET, 별도 암호화 사진 복구 저장 | 로컬 관측 번호는 서버 멱등 영수증이 아님. 현재 완료 또는 정확한 미적용 근거와 기록 삭제 확인을 구분. 사진·서명은 일반 복구 기록에 복제하지 않음 |
| 기사 신고 | `DriverCargoExceptionRecovery`의 계정·서버·운송·단계별 보호 보관과 `DriverTransportExceptionService`의 미확정 분류 | 현행 소스에는 재실행 중복 차단이 있다. 과거 문서의 화면 수명 보호 한정 설명을 현재 사실로 재사용하지 않음. 로컬 관측 번호/과거 신고 존재는 이번 신고의 서버 영수증이 아니므로 미확정이면 운영자 확인 유지 |
| 운영자 조회 | 기존 인증 복원/401 갱신, 명시 새로고침, 원장 연결 | 조회 실패를 0건·업무 종결로 바꾸지 않음. 대상/계정 전환·늦은 응답 차단은 실제 실행 전 확인 대상. 새 업무 재시도·지급 권한을 만들지 않음 |

정상 한 건과 별개로 응답 유실·조회 실패·앱 종료 후 재실행·다른 계정/서버/운송 전환을 작은 사례로 분리한다. 고의 실패 주입은 소유한 격리 환경에서만 다음 회차의 허용 범위로 실행한다. 단말 관측 번호, HTTP 요청 ID, 업무 원장 ID를 같은 값으로 추정하지 않는다. 처리 결과와 보호 기록 정리 결과를 따로 남긴다.

## 기존 도구와 목표 파일럿의 차이

[화물 여정 도구](../../../../../eng/Ssalddel.CargoJourneyVerification/README.md), [실행 진입](../../../../../eng/verification/cargo-journey.ps1), [단계별 HTTP 도구](../../../../../eng/verification/cargo-journey-workflow.py)를 재사용 후보로 둔다.

- 기존 fixture는 화주·기사·창고·운영자 **네 역할**과 출고 준비 재고에서 시작한다. 목표 입력은 **세 앱**이다. 창고를 부가 역할로 사용하면 해당 단계·계정을 별도 기록하며 세 역할 앱에서 창고 전 과정까지 수행했다고 표시하지 않는다.
- 기존 `prepare`는 창고 재위탁 출고에서 의뢰를 만들고 명시 후불 승인·제어 추천·기사 수락을 진행한다. 화주가 기본 신규 선결제 의뢰를 접수한 사례를 대체하지 않는다. 기본 선결제 검증과 승인 후불 검증은 다른 사례로 기록한다.
- 제어 추천은 `ControlledSyntheticDriverSelection`이다. 제품 AI 후보선택·실운영 추천 결과가 아니다. 초기 포장 재고와 1×1 합성 PNG는 실제 입고·피킹·포장·카메라·화물 외관 증거가 아니다.
- 도구의 Kestrel endpoint는 `http://127.0.0.1:5322/`이고 [USB APK 도구](../../../../../eng/release/README.mobile-usb-test.md)의 기본은 `http://127.0.0.1:5363/`다. APK의 실제 추출 endpoint·서버 listen·USB reverse를 일치시킨 결과가 있어야 통신 검증이다. 문서 기본값을 설치된 APK 설정으로 추정하지 않는다.
- `ready.json`의 준비/실행 ID와 `proof` 현재 조회는 실패 단계·전체 여정의 성공 인증이 아니다. 기존 완료·실패 sample, 볼륨, ready, HTTP/DB 결과와 앱 데이터를 보존한다.
- `Prepare/Up/Serve/Stop`·새 sample 전환·재시작·업무 명령은 이번 회차에서 실행하지 않는다. 실제 실행 담당은 현재 프로세스·compose·DB·기기 소유를 다시 확인한 뒤 기존 절차를 따른다. 공유 환경을 맞추려고 중지·초기화하지 않는다.

## 새 실행에 결속할 기준선

값이 없으면 `Unknown`, 실행하지 않았으면 `NotRun`으로 남긴다. 현재 파일로 과거 증거의 입력 기준선을 소급 생성하지 않는다. 실제 계정·연락처·주소·키·비밀 원문을 입력/공유 결과에 넣지 않는다.

| 기준선 | 필요한 필드 |
| --- | --- |
| 요청·기획 | work item, PLAN revision/hash, 작업지시서/hash와 승인 범위, profile/intake/review hash, 사람 입력 확인 상태 |
| 소스 | branch/commit, 선택 경로, dirty 상태/hash, 실행 전후 파일 hash·변경 목록, 서버/앱 공통 계약 입력 |
| 서버 | build 입력/hash·DLL hash, configuration·framework, 소유 PID/시작 시각, 실제 endpoint, Development/Simulation·기능 설정, 외부 효과 차단 상태 |
| 저장/실행 | 전용 DB 식별·compose project/sample/volume 소유, ready/schema와 runStableId, requestId/transportId 연결, 필요하면 warehouse/outbound/item ID, 단계 전후 정본 상태·건수 |
| APK | 앱별 project/package ID, versionName/code, configuration·ABI, 최종 APK hash·서명/정렬·실제 추출 endpoint, 빌드 전후 소스 동일성 |
| 설치/화면 | 시험 기기 식별·에뮬레이터/실기기 구분, 설치된 APK hash/code·데이터 보존 여부, 화면/입력 절차·시각, 원캡처 hash, 서버 재조회와 앱 재실행 결과 |
| 결과 | 실행 단계별 Passed/Failed/NotRun, 자료 현재성 Current/Stale/Unknown, 환경 차단, 실패 뒤 결과 확인과 중복 생성/완료 여부, 금융·외부 효과의 실제 실행 여부 |

기존 app-production EvidenceManifest는 선택 입력과 실행 전후 현재성을 기록하는 도구다. 그 관리 검증의 Passed가 화물 HTTP/DB·Android·실기기·운영을 대신하지 않는다. USB 포장 도구의 manifest와 별도 앱 실행 증거를 기존 전후 evidence context에 참조로 연결할 수 있으며, 이 준비 작업에서 포장 도구를 새로 확장하거나 실행하지 않는다.

## 과거 근거와 이번 결과

| 근거 | 당시 확인 | 이번 상태와 남은 일 |
| --- | --- | --- |
| [모바일 마감 r121](../../../../Changes/2026-10-25-mobile-app-closeout-r121.md) | 격리 제품 HTTP·서버 재시작 보존, 실제 휴대폰 로그인/초안·데이터 보존 업데이트 | `HistoricalReport`. 최신 세 앱의 같은 운송 전체 수행은 `NotRun` |
| [Android 복구 r124·기사125](../../../../Changes/2026-10-26-cargo-android-recovery-r124.md) | 통합/기사 실제 APK의 에뮬레이터 설정·거절/만료 복귀·기사 시작/종료 요약 | `HistoricalReport`. 에뮬레이터와 실기기 분리. 서버 거절 사유 후속의 실제 HTTP/GUI 재검증과 전문 앱 재실행 전체 흐름은 미실행 |
| 통합 APK124 | `com.companyname.ssalddelapp.closeoutreview`, SHA-256 `118c2c19c1e0188a67105eff3989235a9f1d5c8b7e100d1af5592e5baa7771f3` | 과거 빌드 해시. 현재 소스 동일성·실기기 업무를 자동 승계하지 않음 |
| 전문 기사 APK125 | `kr.hongdal.driver.cargoreview`, SHA-256 `58986ccacf21b62da396b9af30473e65b899227af4b51927ffaf1c1d28e2f63c` | 당시 기존 a2 서버 사용, `backendReasonFixApplied=false`. 최신 서버 수정 검증이 아님 |
| 전체 소비 빌드 | r124 표준 Task는 Android 리소스/중간 생성 오류로 중단 | 별도 .NET 시험·격리 APK 성공과 분리. 범위 밖 cache 삭제·일괄 재빌드 없음 |
| 이번 입력 준비 | 기존 코드/기획/시험 소스·경로·hash 대조, 앱별 파일과 검증 계획 작성 | 제품 시험·API/DB·UI·APK 제작/설치·실제 운송 모두 `NotRun` |

과거 캡처·시험·APK·실패 기록은 보존한다. 실제 GPS 운행·카메라 촬영·지도 인증·외부 Push·기관 확인·실 PG·수납/송금·공개 운영은 별도이며 새 허가·가격·권한·DB schema·기능 플래그·Unity·자동 감시·게시·commit/push를 변경하지 않는다. 다음 회차는 정확한 입력/환경을 수용한 뒤 좁은 사례를 실행하고, 결과/차단/조건부 규칙 후보를 기존 제작 절차에 반환한다.
