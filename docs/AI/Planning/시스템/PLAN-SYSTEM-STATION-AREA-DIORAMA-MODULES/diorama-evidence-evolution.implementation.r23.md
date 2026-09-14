[기획 · 시스템·월드 투영 · PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES · r23]

# 사가정 기준 디오라마 증거 진화 최소 구현

- 상태: `ApprovedScopedImplementation / SagajeongCandidateSeedApproved / CandidateAutoRegistrationApproved / SharedRuleHumanApprovalRequired / EvidenceStageAutoPromotionForbidden`
- 승인 근거: 2026-09-14 사용자는 사가정역을 계속 깊게 발전시키며 발견한 보편 규칙을 증거 체계에 축적하고, 다른 역 구현에 재사용할 수 있도록 어느 정도 구현해 두라고 요청했다. 앞선 문답에서 `Candidate`까지 자동 기록하고 `ProvisionalSharedRule` 이상과 실제 공통 적용은 사용자 검토를 거치는 방식을 확인했다.
- 기준 체계: [역세권 디오라마 증거 진화 체계](../../../../Architecture/역세권디오라마증거진화체계.md)
- 후보 대장: [`station-diorama-evidence-rules.json`](../../../../../eng/execution-ledgers/station-diorama-evidence-rules.json)

## 확정

1. 기존 E1~E10의 의미·단계·승격 관문을 변경하지 않는다.
2. 개발 에이전트는 역세권 디오라마 작업 종료 시 증거 변화와 새 규칙 후보를 판정한다.
3. `Observed`·`Candidate` 기록은 자동화할 수 있지만 공통 생성·조립·검사에는 자동 적용하지 않는다.
4. `ProvisionalSharedRule` 이상은 성격이 다른 역 검증과 명시적인 사람 승인을 요구한다.
5. 출처 기록은 공식 링크, 기준일·수집일, 실제 확보 hash, 권리·결손과 저장소 근거를 보존한다.
6. 사가정역에서 이미 발견한 규칙은 최초 대장에 모두 `Candidate`로 소급 등록한다.
7. 면목역과 용마산역은 교차 검토 표본으로 연결하지만 이번 작업에서 후보를 승인 공통 규칙으로 승격하지 않는다.

## 최초 후보

- 역 중심 표현 창은 조회·표현 단위이며 행정·통행·운영 권위가 아니다.
- 수집 뒤에도 없는 자료는 `MissingCoverage`로 유지하고 실제 공터나 0값으로 해석하지 않는다.
- 공공자료 원본, Simulation 상태 사본, Unity 표현과 Game View 증거를 분리한다.
- 역별 수량·hash·범위는 역 Profile이 소유하고 공통 조립기는 알고리즘과 검증 경계만 소유한다.
- 다른 역의 도형·자료를 암묵적인 fallback으로 사용하지 않는다.
- 표시 토글은 Simulation·운영 상태와 가상 정산을 변경하지 않는다.
- 공공 통계는 비식별 합성 밀도의 근거일 수 있지만 실제 개인의 신원·위치·성실성 추론에 쓰지 않는다.
- Game View 이미지는 표현 증거이며 업무 완료·통행 가능·게임플레이 권위를 증명하지 않는다.
- 외관 자료의 권리·hash·Bounds 검증이 끝나지 않으면 기존 절차적 매스를 유지한다.

## 이번 구현 범위

- 위 원칙을 소유하는 Architecture 문서
- 출처 기록, 후보 규칙과 역별 검토 표본을 담는 JSON 대장
- 상태 승격, 출처 링크, hash, 저장소 참조와 자동 적용 금지를 검사하는 PowerShell 도구
- 이후 에이전트가 작업 종료 시 후보를 점검하도록 하는 공통 `AGENTS.md` routing

서버 API·DB schema, Simulation Runtime, Unity 코드·Scene·Prefab·Game View, 외부 재수집, 운영 적용, commit과 push는 변경하지 않는다.

## 현재 검증 상한

자동 검사는 대장 자체와 저장소 참조의 정합성만 검증한다. 외부 URL 응답과 이용조건의 현재성, 비공개 원본·로컬 DB, 실제 Unity 실행과 화면 품질은 각각 별도 증거가 필요하다. 최초 후보는 승인 공통 규칙이 아니므로 `applicationAuthorized=false`를 유지한다.

## 미정

- 첫 `ProvisionalSharedRule` 승격 후보와 사람 검토 시점
- 후보가 실제 공통 생성기 또는 Unity Adapter에 적용될 때의 별도 E7 작업 명세
- 외부 링크의 정기 재확인 주기와 끊어진 링크 대체 정책
