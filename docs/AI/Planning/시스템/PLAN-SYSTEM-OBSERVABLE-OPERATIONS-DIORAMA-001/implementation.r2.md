# 관찰 가능한 운영 디오라마 E7 수직 작업 명세 r2

## 결속

- 기획: `PLAN-SYSTEM-OBSERVABLE-OPERATIONS-DIORAMA-001` / `observable-operations-diorama.r2` / `Approved`
- 승인 기획 SHA-256: `9bd1fabab1610a4426277fe9b0eca752caaec4059c91a3d427fa82fdad544a66`
- 대표 작업: 완료된 운영 업무 한 건을 개인정보 없는 지역 상태 사본으로 발행하고 Unity의 같은 안정 식별자 객체에 반영한다.
- 기존 업무 단위: 음식 배달, 화물 운송, 창고·마트의 승인된 기존 업무 의미와 상태 코드를 재사용한다. 이 지원 조각을 새 실제 영업 업무나 자동 배차 권위로 해석하지 않는다.
- 쓰기 소유: Hongdal의 공유 계약·운영 지역 장면 조회·전용 검증 호스트·시험·문서, Unity 저장소의 `SimulationWorldShell` 조립·운영 관찰 표현·시험.

## Logic E7→E1 영향 검토

| 단계 | 범위 |
| --- | --- |
| E7 | 전용 호스트의 명시 시작부터 10분 종료, 재조회와 장애 뒤 재시도를 별도 증거로 확인 |
| E6 | 자료원·객체별 실패 격리, Outbox 멱등 재처리, 일시정지·재개 |
| E5 | 전용 MySQL 원장과 Event/Outbox, MongoDB 읽기 사본, Redis 단기 상태의 같은 실행 ID 결속 |
| E4 | 여섯 합성 사례와 단계 순서·완료 조건·의미 위치를 동결 |
| E3 | v1/v2 계약, 개인정보 금지, 안정 ID/revision 및 결정적 시간 정책 시험 |
| E2 | Controller→UseCase→저장·발행→지역 조회 순서와 권한·실행 모드 경계 |
| E1 | 운영 서버만 상태 권위를 가지며 Unity는 읽기 전용임을 유지 |

E1→E7은 역순으로 조립·검증한다. 자동 시험 통과를 실제 벽시계 실행이나 운영 증거로 승격하지 않는다.

## Presentation E7→E1 영향 검토

| 단계 | 범위 |
| --- | --- |
| E7 | 실제 Game View에서 여섯 객체의 생성·갱신·개별 동결·회복을 관찰 |
| E6 | 30초 갱신 실패 뒤 마지막 정상 표현 유지와 높은 revision 재개 |
| E5 | canonical `SimulationWorldShell/OperationalOsWorldRoot`에 안정 ID 객체 결속 |
| E4 | 기존 primitive/상태 표식을 재사용하고 새 외부 자산은 `NotApplicable` |
| E3 | v2 Decoder·Interpreter·Reconciler·배치 계획 집중시험 |
| E2 | 서버 상태 사본과 Unity 메모리 모델의 명시적 매핑 |
| E1 | 관찰자·운영 업무·표현 객체의 역할과 개인정보 비노출 원칙 |

`presentationE4Preparation`: 외부 시각 자산은 필요하지 않다. 주 후보는 기존 primitive 상태 표식, 대체는 텍스트 진단 표식이다. 정확 좌표·실존 업소·실사용자 Actor는 사용하지 않는다. 이 문서만으로 Presentation E5 이상을 선언하지 않는다.

## 실패·회복과 무효화

- 잘못된 한 객체는 `SnapshotStableId`별 마지막 정상 revision에서 멈추고 다른 객체는 계속 반영한다.
- 동일/낮은 revision은 덮어쓰지 않는다. 더 높은 정상 revision만 회복한다.
- MongoDB/Redis 쓰기 실패는 MySQL 완료 원장과 Outbox를 유지하고 운영자 재시도로 복구한다.
- 지원 판본, 개인정보 경계, 공식 Scene, 실행 모드 또는 여섯 사례의 단계 순서가 바뀌면 해당 궤적의 가장 이른 E를 다시 연다.
