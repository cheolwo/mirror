# 통합 국내 서비스 공개 준비 점검

`integrated-platform-release.json`은 기존 국내 역할 앱을 웹·통합 APK의 같은 공개 판본으로 준비하는 점검 계약이다. 새 배포기·결제기·배차 실행기를 만들지 않으며 기존 APK 제작·비공개 보관·VM Compose·백업 도구를 참조한다.

```powershell
pwsh -NoProfile -File eng/release/Test-IntegratedPlatformReadiness.ps1
pwsh -NoProfile -File eng/release/Test-IntegratedPlatformReadiness.ps1 -EvidenceDirectory artifacts/local/integrated-platform-release-evidence
pwsh -NoProfile -File eng/release/Test-IntegratedPlatformReadiness.Tests.ps1 -EvidencePath artifacts/local/integrated-platform-readiness-r105/self-test.json
```

첫 두 명령은 파일을 읽고 JSON만 출력한다. Azure 로그인·조회·배포, 서버/worker 시작, 키 생성·송금 또는 운영 설정 변경은 수행하지 않는다. 셸 종료 코드 0은 **점검 실행 완료**이며 `releaseReady=true`를 뜻하지 않는다. JSON의 `status`, `releaseReady`, 각 `issues`를 확인한다.

기본 결과는 `NotReady / releaseReady=false`다. 현재 공개 버전과 실제 외부 증거가 지정되지 않았고, 제공하는 `compose.integrated-platform-readiness.override.yaml`도 **격리 Staging + Simulation / 실행·금융 기본 OFF** 준비 프로필이다. 이 파일을 Operational 운영판으로 간주하거나 기존 운영 DB 위에 합성해 배포하지 않는다. JSON은 유효한 YAML의 부분집합이므로 기존 Docker Compose에서 읽을 수 있다.

## 어떤 증거를 연결하는가

`EvidenceDirectory/index.json`은 `schemaVersion=integrated-release-evidence-index.r1`과 `records` 배열을 가진다. 각 배열 항목은 `requirementId`, 디렉터리 안의 `path`, 해당 JSON 파일의 `sha256`을 갖는다.

증거 JSON은 `schemaVersion=integrated-release-evidence.r1`, `requirementId`, `kind`, 공개 `releaseRevision/releaseVersion/versionCode`, 점검 출력의 `sourceSnapshotSha256/contractSha256/profileSha256`, `runId`, `result`, `synthetic`, `executionMode`, `roleBindings`, `assertions`, `artifacts`를 갖는다. 선행 증거가 필요한 항목은 `previousRecordSha256`으로 해당 선행 JSON의 정확한 해시에 연결한다. `artifacts`의 각 항목도 `kind/path/sha256`으로 실제 파일에 결속한다. 로컬 빌드 결과·APK·웹 묶음만으로 HTTPS·기기·실수납·실지급·복구 증거를 대신할 수 없다.

APK와 웹 묶음을 제외한 증거 파일은 원본 로그/화면 대신 고객 정보 없는 구조화 JSON 보고서다. 각 보고서는 동일 판본·소스·profile·증거 종류·실행 ID·결과를 유지해야 한다. 인증값·고객 상세 정보·원본 스크린샷·비공개 증거 본문을 넣지 않는다. 기존 개인정보 검수와 운영 금융 보호를 대체하지 않는다.

- `local-release`: 같은 공개 판본의 소스·역할 시험·서명 APK·웹 묶음.
- `https-domestic-flows`: 실제 공개 HTTPS와 동일 운영 profile의 전체 역할 흐름·서버/worker 한 개·24시간/중지 없음·DB 격리·비공개 저장소 관측.
- `device-domestic-flows`: 직전 HTTPS 역할 검증에 연결된 실제 설치 APK와 단계/복귀·알림/백그라운드 관측.
- `service-cost-collection`: 배송·피킹·포장 서비스비의 실수납과 상품대금 제외·불명확 응답/중복 처리 관측.
- `weekly-service-payout`: 직전 수납·업무 자료에 연결된 실제 주간 지급, 재원·신고 보류 재검사와 중복/응답 유실 관측.
- `independent-recovery`: 서버 밖 암호화 백업을 독립 환경에 복원하고 보호 데이터·원장/Outbox·사진·지급 중복 방지를 확인한 관측.

해시와 선언이 맞으면 `HashBoundForReview`이며 **내용의 진위나 실제 송금·기기 관측 인증은 아니다**. 현재 준비 점검기는 별도의 검토된 Operational 프로필과 권한 있는 최종 공개 결정을 자동으로 만들지 않으므로, 모든 로컬 자료가 있어도 공개 준비 완료를 반환하지 않는다. 자체 시험의 가짜 APK·보고서는 형식/반례 검사이며 실제 증거로 재사용하지 않는다.
