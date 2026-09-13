# 사가정 가상 배달 이동 E1~E7 작업 명세

- 승인 기획: [r1](README.md)
- 대표 WI: `가상 주문 한 건을 사가정 이동망에서 픽업·전달·복귀하는 관찰`
- 준비된 주체: 가상 주문자·가상 음식점·가상 기사·가상 오토바이
- 수정 경계: 이동망 전처리·검증, Simulation 읽기 전용 여정 상태 사본, Unity 이동 해석·표현, 집중 시험과 현행 문서
- 보존 경계: 기존 운영 주문/배차 원장, `SagajeongReference.json`, 행정동 귀속·디오라마 projection hash, canonical `SimulationWorldShell`, 다른 작업트리 변경

## 논리·표현 영향

| E | Logic | Presentation |
| --- | --- | --- |
| E1 | 이동망·접근점·여정 schema와 hash 경계를 고정한다. | 전체/확대/근접 표현과 차량·도보 전환을 고정한다. |
| E2 | 원본 OSM을 결정적으로 타일화하고 검토된 명시 간선만 탐색한다. | 상태 사본을 엄격 해석하고 경로 polyline 위 위치를 계산한다. |
| E3 | 일방통행·수단·타일 stitch·불일치·직선 fallback 금지를 자동 시험한다. | stale revision·hash 불일치·전환·정리·무권위 도착을 시험한다. |
| E4 | 실제 사가정 후보 경로와 Entrance/CurbStop 근거를 검사한다. | 기사·오토바이·경로선 후보와 거리별 표현 전환을 준비한다. |
| E5 | 1차는 읽기 전용 shadow 사본만 허용한다. 권위 승격은 별도 WI를 요구한다. | 기존 디오라마 위 독립 Mobility Root의 저장 Scene 결속을 별도 확인한다. |
| E6 | 권위 WI에서 차단·재개·취소·Save/Replay를 검증한다. | 하차·보행·픽업·전달·복귀와 메모리 회수를 Play Mode에서 검증한다. |
| E7 | 동일 revision의 주문·경로·결과와 다음 주문 선택까지 닫는다. | 실제 입력과 Game View에서 전체 폐루프를 완주한다. |

## 1차 구현 관문

1. 정적 이동망 후보는 원본 hash·노드 ID·방향·수단·접근 검토·500m 타일 경계를 보존한다.
2. `food-delivery-journey-snapshot.v1`은 가상 주문만 허용하고 `distributionApproved=false`를 유지한다.
3. Unity는 상태 사본을 읽고 위치를 보간하지만 Command, 주문 상태 변경, 도착 확정을 호출하지 않는다.
4. 연결 결손에서는 움직이지 않으며 건물 중심이나 직선으로 건너뛰지 않는다.
5. 실제 Scene 결속 전까지 결과 상한은 `Logic E3 / Presentation E4 준비`다.

## 권위 승격 재개점

사가정 후보 경로의 연결성, Motorcycle/Pedestrian 통행, 음식점·배송지의 Entrance/CurbStop, graph/projection hash가 같은 판본으로 검토된 뒤 기존 음식배달 Goal의 가장 이른 Logic E1을 다시 연다. 그 WI에서 경로 진행을 Session Save/Replay에 넣고 도착을 픽업·전달 상태 전이의 필수 입력으로 결속한다. Unity 표현 성공만으로 이 승격을 수행하지 않는다.

## r1 구현·검증 결과

- 정적 이동망 생성·독립 감사 자체 시험 5/5와 동결 원본 Build/Audit가 통과했다. 실제 결과는 500m 4타일·노드 2,807개·간선 2,574개이고 projection hash는 `59DD155C0D49BEB0612062DF6E1571A751FE0EC1E9DA4AD4AF41A879EB4FE6F7`이다.
- Simulation 이동망·여정 집중 시험 41/41, 서버 로컬 원천·API·기능 플래그·지역 Package 집중 시험 27/27이 통과했다. 최종 범위 Fast는 두 solution build와 Simulation 15/15·서버 144/144가 통과했다(`artifacts/local/validation/20260913-125209`).
- 범위 Task에서 두 solution build와 Simulation 전체 1,928/1,928이 통과했다. 서버 전체는 5,224개 중 5,217개 통과·기존 작업트리 관련 7개 실패로 완료 관문이 닫히지 않았다(`artifacts/local/validation/20260913-123841`). 이번 이동망·여정 집중 시험 실패는 없다.
- 별도 Unity 저장소에는 엄격 Decoder·상태 해석·polyline 위치·전체/확대 표현·선택 경로·배우 pooling 계층과 EditMode 시험을 준비했다. Runtime/Presentation 단독 build는 각각 오류 0이며 최종 Editor 재컴파일 오류 0, 새 이동 계층 27/27·기존 사가정 운영 디오라마 7/7, Console 오류 0을 확인했다.
- 별도 Unity 저장소의 Decoder·Interpreter·Playback 계층에 더해 canonical `SimulationWorldShell` Play Mode에서 `SyntheticFixture` 7구간·116m 자동 이동을 실행했다. 시간 경과에 따른 사본·revision·Transform 변화와 전체·Motorcycle·Pedestrian 고정 단계 PNG 3개를 확인했고, 저장하지 않는 임시 Root를 사용해 Scene 변경 없이 종료했다. 이번 여정 관련 Console Error/Exception은 0건이었다.
- Hosted HTTP/live server, 실제 OSM 간선 통행 승인과 길찾기, 운영 주문·배차 상태 전이, 실제 입력 완주, Save/Replay와 Scene 영속 결속은 수행하지 않았다. 이번 여정 경로와 무관한 기존 bootstrap/replay/server 연결 Error/Exception 8건은 별도 잔존한다. DB 쓰기는 없었다. 관련 구현·화면 증거는 로컬 커밋으로 정리했고 원격 push는 하지 않았다.
