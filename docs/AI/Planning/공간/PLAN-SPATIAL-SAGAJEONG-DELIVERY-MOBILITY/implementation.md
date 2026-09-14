# 사가정 가상 배달 이동 E1~E7 작업 명세

- 승인 기획: [r1](README.md), [사업장·음식점 주문 결속 r2](business-order-binding-proposal.r2.md)
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

## r2 비공개 음식점 자료·세 가상 음식점 폐루프 작업 명세

| E | Logic | Presentation |
| --- | --- | --- |
| E1 | 599개 실제 음식 관측의 원문 상호·부모 원장·입력 hash·공간/인허가 후보·비공개 상태와 세 합성 profile 계약을 고정한다. | 세 합성 이름과 음식점 고유 식별자를 기존 음식배달 수명주기 표시 모델이 보존한다. |
| E2 | 기존 `public_data_normalized_records` 부모 행을 재사용한 파생 directory와 기존 음식 주문 상태 기계를 결속한다. | Unity 패키지는 profile 목록과 주문 상태 사본을 읽되 Command·공개 상호 위치를 추정하지 않는다. |
| E3 | 599행 결정성·독립 재조회·재적용 멱등성, 세 profile의 주문→조리→배정→픽업→전달→수령→기사 귀환과 Save/Replay를 자동 시험한다. | 음식점 ID·합성 이름·상태 단계의 교차 오염이 없음을 .NET/Unity 패키지 시험으로 확인한다. |
| E4~E7 | 실제 상호 Claim, Entrance/CurbStop, 검토 통행, Hosted 전송, 저장 Scene과 실제 입력을 각각 다시 승인한다. | 이번 구현으로 새 Game View나 실제 Scene 결속을 주장하지 않는다. |

쓰기 범위는 `eng/Ssalddel.PublicDataPortalImport/`의 파생 directory 도구, Simulation 계약·합성 profile/기존 주문 흐름의 최소 확장, Unity 패키지의 읽기 전용 표시 모델, 집중 시험과 이 기획 snapshot이다. 실제 상호는 부모 SEMAS 원장의 `RawSnapshotId`·`SourceId`·`DatasetId`를 유지하며 새 외부 원천처럼 중복 등록하지 않는다.

새 WI나 Goal을 만들지 않고 기존 `restaurant-auto-accept`, `restaurant-cooking`, `synthetic-order-accept-r4`, `synthetic-order-assign-r4`, `synthetic-delivery-assign/pickup/move/deliver/receive/return` E7 v2 작업 명세를 재사용한다. 새 profile 계약과 주문 생성은 `synthetic-order-accept-r4`, 세 음식점 배정·합성 실행 입력은 `synthetic-order-assign-r4`, Unity 읽기 표시 결속은 `restaurant-cooking` 명세가 소유한다. 나머지 작업 명세의 직접 결과와 primary 기획 관문은 바꾸지 않으며 이번 통합 시험에서 같은 상태 전이 순서만 재검증한다.

## r2 구현·검증 결과

- 결정적 directory manifest는 음식 관측 599행·원문 상호 587종·단일 건물 후보 180행·인허가 일치 관측 334행이며 SHA-256은 `d89369ecf0fa85093522aef2a7a2dc9f413154c59d175f58391e3de3b8bb64cd`다. 로컬 MySQL에 599행을 저장하고 별도 문맥에서 전부 재조회했으며 두 번째 적용은 쓰기 없이 `existing=599`로 끝났다.
- 현재 로컬 DB에는 인허가 전용 테이블이 없어 후보 ID·원천 hash는 동결 입력 기준으로 보존했다. migration이나 추정 테이블은 만들지 않았고, 이 상태를 `SkippedTableUnavailable`로 명시한다.
- 실제 사업장과 무관한 가상 음식점 세 profile 및 전용 주문 ID 공간을 추가했다. 사가정 시나리오에는 기존 r4 마트 주문을 만들지 않으며 5개 음식 주문이 세 음식점을 순환해 수령 확인·기사 복귀까지 끝난다. 종료 시점과 이동 중간 시점 Save/Replay가 모두 결정적이다.
- `DisplayRouteKind`는 대로·생활길·골목을 후속 결속하기 위한 표시 후보다. `RouteApplied=false`, `TraversalReady=false`이며 이번 주문 폐루프의 기사는 기존 공통 합성 경로를 사용한다.
- Simulation 집중 시험 49/49, Unity 패키지 집중 시험 26/26이 통과했다. 범위 Fast는 Simulation 227/227·Unity 28/28, Task는 Simulation 전체 1,957/1,957·Unity 전체 785/785와 두 solution build·생성 지도를 통과했다. 세 기존 E7 v2 작업 명세 검사도 통과했으나 formal Evidence 단계는 계속 `E0`이고 새 Game View·Scene·Hosted 증거는 만들지 않았다.

## r1 구현·검증 결과

- 정적 이동망 생성·독립 감사 자체 시험 5/5와 동결 원본 Build/Audit가 통과했다. 실제 결과는 500m 4타일·노드 2,807개·간선 2,574개이고 projection hash는 `59DD155C0D49BEB0612062DF6E1571A751FE0EC1E9DA4AD4AF41A879EB4FE6F7`이다.
- Simulation 이동망·여정 집중 시험 41/41, 서버 로컬 원천·API·기능 플래그·지역 Package 집중 시험 27/27이 통과했다. 최종 범위 Fast는 두 solution build와 Simulation 15/15·서버 144/144가 통과했다(`artifacts/local/validation/20260913-125209`).
- 범위 Task에서 두 solution build와 Simulation 전체 1,928/1,928이 통과했다. 서버 전체는 5,224개 중 5,217개 통과·기존 작업트리 관련 7개 실패로 완료 관문이 닫히지 않았다(`artifacts/local/validation/20260913-123841`). 이번 이동망·여정 집중 시험 실패는 없다.
- 별도 Unity 저장소에는 엄격 Decoder·상태 해석·polyline 위치·전체/확대 표현·선택 경로·배우 pooling 계층과 EditMode 시험을 준비했다. Runtime/Presentation 단독 build는 각각 오류 0이며 최종 Editor 재컴파일 오류 0, 새 이동 계층 27/27·기존 사가정 운영 디오라마 7/7, Console 오류 0을 확인했다.
- 별도 Unity 저장소의 Decoder·Interpreter·Playback 계층에 더해 canonical `SimulationWorldShell` Play Mode에서 `SyntheticFixture` 7구간·116m 자동 이동을 실행했다. 시간 경과에 따른 사본·revision·Transform 변화와 전체·Motorcycle·Pedestrian 고정 단계 PNG 3개를 확인했고, 저장하지 않는 임시 Root를 사용해 Scene 변경 없이 종료했다. 이번 여정 관련 Console Error/Exception은 0건이었다.
- Hosted HTTP/live server, 실제 OSM 간선 통행 승인과 길찾기, 운영 주문·배차 상태 전이, 실제 입력 완주, Save/Replay와 Scene 영속 결속은 수행하지 않았다. 이번 여정 경로와 무관한 기존 bootstrap/replay/server 연결 Error/Exception 8건은 별도 잔존한다. DB 쓰기는 없었다. 관련 구현·화면 증거는 로컬 커밋으로 정리했고 원격 push는 하지 않았다.
