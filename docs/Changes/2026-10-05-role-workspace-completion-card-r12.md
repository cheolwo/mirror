# 업무 완료 확인·기사 핵심 카드 r12

사용자가 승인한 보완 1번 완료 표시/실패 복구와 2번 기사 기본 정보 표시를 통합 역할 작업공간에 적용한다. [책임 카드](../ProjectOverview/page-docs/role-map-workspace-r11.md#완료-확인기사-기본-정보-r12)와 [구현 계획](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/role-workspace-completion-card.implementation.r12.md)을 기준으로 기존 r11과 관련 없는 변경을 보존한다.

## 완료 확인과 복구

음식 주문 취소·음식점 수락/거절/조리시간 변경·기사 중단·운영자 중단 검토는 명령 응답과 후속 조회를 나눠 처리한다. 같은 주문/배달 시도와 이번 요청 이력, 증가한 revision, 해당 행동의 상태·입력값을 대조한 후에만 완료를 표시한다. 기사 중단은 원래 시도가 수행 목록에서 사라진 것을 확인하며 같은 제안의 새 시도와 구별한다. 운영자 검토는 책임·악용 여부·사유까지 일치해야 한다.

typed 명령 응답을 확보한 뒤 읽기가 끊기거나 이전 상태가 나오면 `결과 확인 대기`를 유지한다. `결과 다시 확인`은 GET만 수행하므로 처리된 명령을 중복 전송하지 않는다. 명령 응답 자체가 미확인인 네트워크/응답 해석 실패는 기존 불변 본문·동일 요청 GUID 재시도를 보존한다. 비었거나 불일치한 typed 응답은 완료로 인정하지 않는다. 이 경우 기존 업무로 돌아가 최신 상태를 확인할 수 있지만 조회만으로 이번 요청의 누락 증거를 생성하지 않는다.

음식점의 정상 후행 픽업 준비·조리시간 변경·조리 시작·픽업 후 중단 재조리도 대조한다. 원 요청 ID와 정확한 행동 이력에 결속하며, 새 조리 시간을 적용한 경우에는 해당 전이와 현재 차수·시작·재조리·완료 예정 시각을 함께 확인한다. 정상 후행 상태를 단순 값 불일치로 영구 대기시키지 않는다. 계정/대상 변경·권한 실패·늦은 응답 보호와 기존 API/ActionId는 유지한다.

## 기사 카드

공통 표시 계약에 선택적 기본 요약을 추가했다. 음식 기사만 상태별 요약을 제공하며 다른 역할의 기존 표시 계약은 유지한다.

| 상태 | 접힌 카드의 기본 정보 | 상세 영역 |
| --- | --- | --- |
| 수락 전 | 배달료·API 제안 거리·픽업 주소 | 긴 메뉴·기존 배달 조건 |
| 픽업 이동/대기 | 배달료·픽업 주소·준비 예정·필요 시 재조리 안내 | 긴 메뉴·주문 정보 |
| 전달 중 | 배달료·전달 주소·필수 전달 요청 | 메뉴·수령인·가린 연락처 |

누락/음수 제안 거리·음수 금액은 `미확인`으로 표시하고 확인된 0은 보존한다. 수행 중 DTO에는 거리값이 없어 생성하지 않는다. 기존 업무 버튼과 48px 카드 터치 영역, 상세 접기와 단계·인증 경계를 유지한다.

## 검증

- Fast `20261005-183949`: 집중 시험 **221/221**, 관련 프로젝트 빌드 오류 0.
- Task `20261005-184125`: 전체 `Ssalddel.v3.5.slnx` 빌드 오류 0·경고 102, 전수 시험 **7,447/7,447**. 기존 nullable·패키지·분석기 경고가 남는다.
- 공통 실제 Razor 화면을 로컬 예시 API로 실행했다. 390px에서 기사 세 단계의 접힌 핵심 정보/펼친 상세, 320px 전달 카드의 가로 넘침 없음과 브라우저 실행 오류 없음을 확인했다.
- 명령 뒤 조회 실패 및 이전 상태 조회 두 상황에서 미완료 대기를 확인했다. 재확인 후 쓰기 1회·읽기 3회로 완료됐고 중복 명령이 없었다. `ui-verification.json`에 결속한다.
- 실제 통합 WASM 소비 앱을 최신 빌드로 실행해 여덟 역할 홈, 일곱 보호 역할의 미로그인 경계·로그인 취소 복귀, 다섯 입력 경로의 직접 진입/새로고침과 실행 오류 없음을 확인했다. API는 로컬 403으로 가로채 외부 서버에 업무를 기록하지 않았다. `wasm-ui-checks.json`은 실제 인증·서버 업무 완료 증거가 아니다.

원시 근거는 `artifacts/local/role-workspace-completion-card-r12/`에 보관한다. 실제 서버/DB에 대한 이번 UI의 업무 완료·휴대폰·Azure·지도 타일 검증은 위 로컬 예시 결과로 확대하지 않는다.

## 화면

기사 카드와 복구 화면은 실제 공통 컴포넌트에 예시 자료를 제공한 화면 배치다. 역할 홈·로그인 안내는 실제 통합 WASM 앱의 미로그인 화면이다. 지도 타일은 연결하지 않았다.

| 구분 | 기록 |
| --- | --- |
| 기사 기본 카드 | [수락 전](../assets/changes/2026-10-05-role-workspace-completion-card-r12/driver-offer-collapsed.png) · [픽업 중](../assets/changes/2026-10-05-role-workspace-completion-card-r12/driver-pickup-collapsed.png) · [전달 중](../assets/changes/2026-10-05-role-workspace-completion-card-r12/driver-delivery-collapsed.png) |
| 기사 상세/좁은 폭 | [전달 상세](../assets/changes/2026-10-05-role-workspace-completion-card-r12/driver-delivery-expanded.png) · [320px](../assets/changes/2026-10-05-role-workspace-completion-card-r12/driver-delivery-small.png) |
| 복구 | [조회 실패 대기](../assets/changes/2026-10-05-role-workspace-completion-card-r12/recovery-after-read-failure-pending.png) · [상태 반영 확인](../assets/changes/2026-10-05-role-workspace-completion-card-r12/recovery-after-read-failure-completed.png) · [이전 상태 조회](../assets/changes/2026-10-05-role-workspace-completion-card-r12/recovery-stale-read-pending.png) |
| 실제 통합 앱의 미로그인 화면 | [역할 홈](../assets/changes/2026-10-05-role-workspace-completion-card-r12/wasm-home-mobile.png) · [음식 기사 로그인 안내](../assets/changes/2026-10-05-role-workspace-completion-card-r12/wasm-driver-login-required.png) |

## 설치 파일과 검증 경계

최신 내장 Android Debug APK는 `artifacts/local/role-workspace-completion-card-r12/SsalddelApp-completion-card-r12-debug.apk`다. Android 빌드 오류 0·기존 인증 코드 nullable 경고 2, v2/v3 서명·ZIP 정렬/CRC·ARM64/x86_64 앱/공통 UI/런타임 포함·현재 공통 CSS 일치·원본/사본 해시 일치를 확인했다. 크기 135,430,196바이트, SHA-256은 `0f21c9496bb2f225b21bbcdd05ea25231357d0bcfe5b7158a8928accb99ca036`이다. `apk-verification.json`과 `source-hashes.json`에 결속한다.

이 APK의 Debug 기본 서버는 Android 에뮬레이터용 `http://10.0.2.2:5104/`다. 실제 개인 휴대폰 서버 주소·설치·운영 로그인·Azure 통신·지도 키/타일·위치 권한을 확인한 파일로 표현하지 않는다. UI의 실제 서버/DB 업무 완료 역시 로컬 예시 API 검증과 구별한다.

상태는 `Implemented / ValidatedLocal / PublicDeploymentPending`이다. 완료 후 다음 업무 선택·자동 갱신·목록 확장은 후속이며 이번에 추가하지 않았다. 기존 dirty 작업과 r11 증거를 보존하며 commit/push/외부 배포는 수행하지 않았다.
