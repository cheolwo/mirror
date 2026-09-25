# [기획 · 운영 관찰 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r22]

## 승인·범위

- 승인 근거: r21 보완 제안에 대한 사용자 `한번 진행해봐.` (2026-09-22).
- 기준: [r21 조사](normal-lifecycle-api-unity-gap-audit.proposal.r21.md), SHA256 `EACAFF561544322F3F53BF34BFF5FCC77948DB60E6C8F299784536C5EC02581A`.
- 수용 범위: 진행 중 음식 상태의 배치 허용, 같은 판본 유효기간 갱신, 낮은 판본 삭제 통지 거절, 기존 음식 단계의 표현 어휘 연결. 기존 API의 v1 호환과 읽기 전용 경계를 보존한다.
- 신규 게임 WI·업무 규칙을 만드는 작업이 아니라 기존 운영 상태 사본의 연결 결손 수리다. Goal 자동 활성화·E 승격·Scene 저장·실운영·commit·push는 제외한다.

## 쓰기 소유 범위

Hongdal:
- `Ssalddel/Application/WorldProjection/운영지역장면조회UseCase.cs`
- `Ssalddel.Tests/Application/WorldProjection/운영지역장면조회UseCaseTests.cs`
- `Ssalddel.Unity/Runtime/WorldProjection/OperationalWorldSceneInterpreter.cs`
- `Ssalddel.Unity/Runtime/WorldProjection/OperationalWorldScenePlacementPlanner.cs`
- 위 두 클래스의 `Ssalddel.Unity.Tests` 시험 파일
- 이 문서, 기획 README·PLANNING·CURRENT_WORK

별도 Unity 저장소 `C:/Users/user/ssalddel`:
- `Assets/Ssalddel/Runtime/World/다중Os생명주기검증Models.cs`
- `Assets/Ssalddel/Tests/EditMode/다중Os생명주기검증Tests.cs`

다른 기존 변경은 보존한다. 위치 자료·Scene·Prefab은 수정하지 않으며 미결속 의미 위치는 후속 공간 결속으로 보고한다.

## 검증 상한

서버 집중 시험, Unity 공유 코어 시험, 단계 매핑 EditMode 시험을 구분한다. 실제 DB·인증 HTTP·Play Mode·Game View·Windows 빌드는 실행하지 않았다면 통과로 표기하지 않는다.

## 결과

- 진행 중 `ActiveLifecycle`을 기존 배치 계획기에 연결했다. 업무 단계·판본·고유 식별자는 그대로 전달하고 원문 표현 JSON은 배치 명령으로 복사하지 않는다.
- 서버 v2의 진행 운영 사본은 cursor 이후 변경이 없어도 재조회에 포함한다. 업무 revision·PublishedAt을 인위적으로 올리지 않고 유효기간을 재확인한다. v1과 완료 사본의 cursor 필터는 유지한다.
- 같은 revision·같은 내용일 때만 TTL을 연장한다. 같은 revision에서 업무 내용이 달라지면 `RevisionConflict`로 해당 객체를 유지한다. 오래된 유효기간으로 단축하지 않으며, 낮은 revision 삭제/만료 통지도 최신 객체를 지우지 않는다.
- Unity 표현 모델에 운영 음식 6단계를 추가했다. 운영 음식 OS에만 적용하고 `전달완료`는 `전달 완료·수령 확인 대기`로 표시한다. 원문 코드·revision은 유지하며 수령 완료를 예측하지 않는다.

### 실제 검증 결과

| 검증 | 결과 | 증거 상한 |
| --- | --- | --- |
| 수정 전 신규 회귀 사례 | 신규 10건 실패, 기존 12건 통과 | 문제 재현 |
| 공유 관찰 코어·배치·OS Router | 28/28 통과 | 순수 C# 시험 |
| 음식 정상 수직·진행 Reader·지역 조회·Controller | 11/11 통과 | 자동 시험, 실제 DB/인증 HTTP 아님 |
| 별도 Unity 저장소의 실제 표현 소스를 직접 컴파일한 진단 | 음식 6단계, 다른 OS 거절 6건, 600개 시각 입력의 표본 77판본 통과 | 순수 소스 실행; 10분 실시간 관찰 아님 |
| 독립 재현 코드 재실행 | 배치 수 0→1, 원래 TTL 뒤 유지 수 0→1, 구판 삭제 뒤 유지 수 0→1 | 연결 결손 3개 수정 확인 |
| Unity Editor 재컴파일 | 도구 시간 초과 | EditMode·Play Mode·Game View 미검증 |
| 변경 경로 10개 지정 Fast | diff·두 코드맵·Unity/v0.0 솔루션 빌드·집중 시험 통과 | `artifacts/local/validation/20260922-155211/` |

`artifacts/local/validation/os-lifecycle-api-audit-20260922/`의 `observation-repair.trx`, `server-observation-repair.trx`, `probe/`는 비공개 로컬 시험 산출물이다. NUnit 음식 단계 시험 6건도 Unity 기존 시험 파일에 추가했지만 Editor 실행 성공으로 계산하지 않는다. 서버 기존 nullability/analyzer 경고와 순수 진단의 Unity 소스 nullable 경고는 남는다.

### 남은 연결과 다음 순서

1. `SagajeongObservationPlacement.json`에는 합성 표본 위치 6개만 있고 운영의 `semantic-place:area:food-restaurant`, `food-delivery-route`, `food-recipient-zone`이 없다. 배치 계획 수리는 실제 위치 결속 완료가 아니다. 다음 공간 작업에서 승인된 합성 의미 기준점에 명시적으로 결속하고 실제 시설 위치로 오해시키지 않아야 한다.
2. 진행 사본과 완료 사본의 식별자 전환·이전 사본 제거, 전체 조회 시 `FoodDeliveryOS.Active` 실패 보존, 삭제 후 지연 응답의 재등장 방지는 별도 회귀 항목으로 남는다. 이번 삭제 수리는 현재 보관 중인 최신 객체를 구판 통지가 지우지 않는 범위다.
3. Editor 재컴파일 정상화 후 추가 EditMode를 실행하고, 기존 음식 실제 API 실행기에 연결한 Play Mode·Game View를 확인한다. 그 뒤 화물 중간 단계·창고/마트 전용 조회의 독립 종단 연결을 진행한다.

새 공간 자료 수집·권리 변경·Scene/Prefab 저장은 없다. 기존 사실/합성 표현 분리 경계를 유지하며 새 디오라마 규칙 후보 없음. commit·push 없음.

두 저장소의 수정 코드 `git diff --check`와 새 문서의 상대 링크 확인도 통과했다.
