# 관찰 가능한 운영 디오라마 r2

> 현행 표현 확장: [사가정역 생활 디오라마 r3](diorama.r3.md). 아래 r2는 서버·상태 사본 호환 기준으로 보존한다.

- 기획 ID: `PLAN-SYSTEM-OBSERVABLE-OPERATIONS-DIORAMA-001`
- 분야·판본: 시스템 / `observable-operations-diorama.r2`
- 상태: `Approved / ImplementationAuthorized / SampleOperationsOnly / OperationalEffectsDisabled`
- 승인 근거: 운영 데이터의 완료 생명주기를 상태 사본으로 조합하고 샘플 데이터로 모바일·Unity 관찰을 검증하자는 사용자의 연속 기획과 2026-09-12 `Implement the proposed plan.` 요청.
- 상위 기준: [운영 서버에서 Unity로의 이관](../PLAN-ARCH-OPERATIONS-UNITY-TRANSFER-001/README.md)

## 목표

지역 하나의 음식 배달·화물 운송·창고/마트 업무를 같은 관찰 장면에서 읽되 각 운영체제와 원장은 합치지 않는다. 첫 수직 조각은 10분 동안 결정적으로 진행되는 합성 업무 여섯 건이다. 각 분야는 정상 완료 한 건과 중단 뒤 회복 완료 한 건을 가진다.

| 분야 | 정상 표본 | 회복 표본 |
| --- | --- | --- |
| 음식 배달 | 주문 접수→조리→픽업→전달→수령 확인 | 픽업 뒤 사고→재조리→재배차→수령 확인 |
| 화물 운송 | 의뢰→기사 동의→상차→운송→인수 | 시간 충돌 감지→후속 예약 해제→재계획→인수 |
| 창고·마트 | 입고→검수→적치→피킹→포장 | 후속 투영 실패→복구 대기→재시도→완료 |

표본은 실제 영업이나 실제 사용자 행위가 아니다. `Development + Simulation + 전용 컨테이너 + 전용 MySQL/MongoDB/Redis` 조건에서만 명시적으로 시작한다. 결제·송금·메시지·외부 HTTP와 운영 데이터베이스 접근은 차단한다.

## 권위와 상태 사본

운영 업무의 필수 단계가 순서대로 끝난 뒤에만 관찰 상태 사본을 발행한다. 상태 변경 순서는 `Controller → UseCase → 전용 MySQL 원장과 Event/Outbox → MongoDB 읽기 사본 → Redis 단기 실행 상태`다. MongoDB와 Redis 실패는 완료 업무를 되돌리지 않고 Outbox를 복구 대기로 남긴다.

기존 `GET api/v1/world/areas/{areaStableId}/scene-snapshots`는 매개변수가 없으면 `operational-world-scene.v1`을 유지한다. `schemaVersion=operational-world-scene.v2`를 명시하면 다음 필드를 추가한다.

- `workStableId`, `lifecycleStageCode`, `attentionStateCode`, `objectKindCode`
- `semanticPlaceStableId`, `relationStableIds`
- `sourceKindCode`: `VerificationSample` 또는 `OperationalProjection`
- 합성 표본에만 `scenarioRunStableId`

지원하지 않는 판본은 HTTP 400 Problem Details로 거절한다. 상태 사본에는 이름·전화번호·정확 주소·GPS·인증 토큰을 넣지 않는다. 면목동 공공 지도는 행정 영역 식별과 윤곽의 참고 배경만 담당하고, 표본 객체는 `synthetic-place:*` 의미 위치에만 결속한다.

## Unity 표현과 회복

Unity는 canonical `SimulationWorldShell`의 `OperationalOsWorldRoot` 아래에서 안정 식별자별 객체를 조정한다. 30초마다 v2를 조회하고 메모리에서만 보관한다. `PlayerPrefs`, Save, Replay, 원문 응답 파일 저장을 사용하지 않는다.

한 객체의 필수 필드 누락·낮은 revision·금지된 개인정보 표식은 그 객체의 마지막 정상 표현만 멈추게 한다. 다른 객체와 지역 장면 갱신은 계속되며, 같은 객체의 더 높은 정상 revision이 오면 다시 갱신한다. Unity에는 운영 Command를 두지 않고 일시정지·재개·실패 Outbox 재시도는 로컬 검증 API와 명령줄에서만 수행한다.

## 완료·검증 상한

- 서버: v1 호환, v2 계약, 여섯 사례의 결정적 원장·Event/Outbox·MongoDB·Redis 투영, 상태/일시정지/재개/재시도 API와 집중시험.
- Unity 공통 코드: v2 해석, 객체별 동결·회복, 안정 ID 배치 계획, 휘발성 보장 시험.
- Unity 제품: 기존 공식 Scene과 조립 Builder에 관찰 루트를 결속하고 실제 Editor 컴파일·EditMode를 확인한다.
- 실제 서버 연결, 10분 벽시계 실행, Play Mode·Game View, Windows 빌드는 각각 별도 증거로 기록하며 하나가 다른 하나를 대신하지 않는다.
- 실제 운영 활성화, 실사용자/GPS 연결, 모바일·Web 프론트 화면 확장, 결제·송금·알림, 공개 지도 재배포와 Evidence 자동 승격은 범위 밖이다.
