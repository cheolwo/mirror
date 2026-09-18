# E7 이후 개별 안정·영역 조화·제한 운영 상태

> `eng/execution-ledgers/post-e7-evidence-campaigns.json`에서 자동 생성된다. 직접 수정하지 않는다.

- 증거 모델: `horizontal-dual-cycle-evidence.r3`
- E8 개별 안정 후보: `23`
- E9 영역 조화·사람 승인 후보: `4`
- E9 보류 영역: `2`
- E10 제한 운영 창: `1`
- 파이프라인 안정성: 행위 원장·조건부 성장·trace digest 동등성 필수

## E8 PlayableUnit 안정성

| PlayableUnit | 현재 E | 상태 | 승격 가능 | 차단 |
| --- | --- | --- | --- | --- |
| `playable-loop:nature-basic-herbal-recovery.v1` | E4 | WaitingForE7 | False | 기존 PlayableUnit의 E7 완료 전 대기 상태만 등록한다. 실행·증거 승격 승인 아님. |
| `playable-loop:nature-camp-visitor-stay.v1` | E4 | WaitingForE7 | False | 기존 PlayableUnit의 E7 완료 전 대기 상태만 등록한다. 실행·증거 승격 승인 아님. |
| `playable-loop:farm-barracks-defense.v1` | E3 | WaitingForE7 | False | 기존 PlayableUnit의 E7 완료 전 대기 상태만 등록한다. 실행·증거 승격 승인 아님. |
| `playable-loop:nature-shelter-foundation.v1` | E7 | Blocked | False | 기존 v27 후보에는 소급된 행위 원장·조건부 성장·파이프라인 trace 증거가 없다. |
| `playable-loop:nature-twilight-return.v1` | E7 | Blocked | False | 기존 v27 후보에는 소급된 행위 원장·조건부 성장·파이프라인 trace 증거가 없다. |
| `playable-loop:nature-tactical-self-navigation.v1` | E7 | Passed | True |  |
| `playable-loop:nature-night-day2.v1` | E4 | WaitingForE7 | False | 현재 폐루프 원장의 E4 준비 상태를 반영한다. 여섯 WI의 실제 E5~E7을 닫기 전 E8 반복 검증을 시작하지 않는다. |
| `playable-loop:nature-workbench-foundation.v1` | E6 | WaitingForE7 | False | PlayableUnit E7이 아직 닫히지 않았다. |
| `playable-loop:nature-field-supply-return.v1` | E1 | WaitingForE7 | False | PlayableUnit E7이 아직 닫히지 않았다. |
| `playable-loop:farm-crop-cycle.v1` | E1 | WaitingForE7 | False | PlayableUnit E7이 아직 닫히지 않았다. |
| `playable-loop:farm-pack-store-return.v1` | E1 | WaitingForE7 | False | PlayableUnit E7이 아직 닫히지 않았다. |
| `playable-loop:hub-inbound-putaway.v1` | E1 | WaitingForE7 | False | PlayableUnit E7이 아직 닫히지 않았다. |
| `playable-loop:hub-outbound-ready-return.v1` | E1 | WaitingForE7 | False | PlayableUnit E7이 아직 닫히지 않았다. |
| `playable-loop:town-order-consume-return.v1` | E1 | WaitingForE7 | False | PlayableUnit E7이 아직 닫히지 않았다. |
| `playable-loop:city-demand-service-return.v1` | E1 | WaitingForE7 | False | PlayableUnit E7이 아직 닫히지 않았다. |
| `playable-loop:nature-base-reflection.v1` | E1 | WaitingForE7 | False | PlayableUnit E7이 아직 닫히지 않았다. |
| `playable-loop:nature-building-learning.v1` | E1 | WaitingForE7 | False | PlayableUnit E7이 아직 닫히지 않았다. |
| `playable-loop:nature-regional-threat-recovery.v1` | E1 | WaitingForE7 | False | PlayableUnit E7이 아직 닫히지 않았다. |
| `playable-loop:farm-player-placement.v1` | E1 | WaitingForE7 | False | PlayableUnit E7이 아직 닫히지 않았다. |
| `playable-loop:town-arcana-context.v1` | E1 | WaitingForE7 | False | PlayableUnit E7이 아직 닫히지 않았다. |
| `playable-loop:player-npc-learning-focus.v1` | E3 | WaitingForE7 | False | PlayableUnit E7이 아직 닫히지 않았다. |
| `playable-loop:nature-hans-farm-fence-restoration.v1` | E4 | WaitingForE7 | False | PlayableUnit E7이 아직 닫히지 않았다. 주체와 WI 직접 결과를 먼저 닫고, 반복 안정성은 그 뒤에 별도 검증한다. |
| `playable-loop:hexagram-campaign-retry.v1` | E1 | WaitingForE7 | False | Logic E3·Presentation E1의 낮은 통합 단계 E1을 반영한다. 실제 E7 폐루프가 닫히기 전 E8 안정성 실행을 시작하지 않는다. |

## E9 영역 조화와 사람 승인

| 영역 | 후보 | 구성원 | 논리 | 표현 | 통합 | 사람 승인 |
| --- | --- | ---: | --- | --- | --- | --- |
| Nature | `area-harmony:nature-core.v1` | 5 | WaitingForStableMembers | WaitingForStableMembers | WaitingForStableMembers | False |
| Nature | `area-harmony:nature-first-evening.v1` | 2 | WaitingForStableMembers | WaitingForStableMembers | WaitingForStableMembers | False |
| Farm | `area-harmony:farm-core.v1` | 2 | WaitingForStableMembers | WaitingForStableMembers | WaitingForStableMembers | False |
| Hub | `area-harmony:hub-core.v1` | 2 | WaitingForStableMembers | WaitingForStableMembers | WaitingForStableMembers | False |

Town과 City는 기존 단일 Core를 억지로 분할하지 않는다. 새 독립 Core 플레이 약속이 생길 때 E9 후보를 연다.

PlayableUnit Goal은 E7에서 끝난다. E8 결함은 같은 PlayableUnit의 가장 이른 E1~E7을 다시 열고, E9 결함은 조화 관문 또는 관련 PlayableUnit의 가장 이른 E1~E8을 다시 연다.
