[구현 수용·검증 · 기록 재생·소수 공간 준비 · PLAN-SYSTEM-FRANCHISE-OPERATIONS · r52]

# 방문 입력·재생 기반과 사가정 한 구간 선택

- 작성일: 2026-09-26.
- 승인 근거: 사용자의 r51 후속 요청 “그렇게 해주고 … 하나씩 골라가지고 … 구현을 계속 진행”. 같은 스레드에서 아래 좁은 준비 코드 범위만 `Approved / ReadyToDispatch / Accepted`로 수용한다. 문서 작성 역할에서 개발 역할로 전환하며 commit/push·배포는 포함하지 않는다.
- 주 책임: Unity 읽기 전용 입력/표현 준비와 비공개 공간자료 선택. 운영 원장·가상 업무 권위를 바꾸지 않는다.
- 현재 상태: `InputPreparationAndReplayCoreImplemented / SingleAreaFocusPrepared`. 승인된 순수 코드·한 구간 선택 범위를 구현·시험했다. Unity 화면·DB·전송 연결 완료는 아니다.
- 선행: [r49](visit-sequence-cross-dong-refactoring.proposal.r49.md), [r50](normalized-life-record-to-diorama.direction.r50.md), [r51](stored-record-opt-in-unity-send.direction.r51.md), [행정동 r39](../PLAN-SYSTEM-ADMIN-DONG-DIORAMA/focused-area-refinement.decision.r39.md).

## 고정한 기획과 작업 범위

| 기획 | SHA-256 |
| --- | --- |
| r49 | `07002E0EA9348B1259E1F26B2297D20E3D1C1DD7D844A69BCD6AD10EFEF567C0` |
| r50 | `F7C806C8BA1687C44C35ED808C389CF5FB36F641427CEFBFDDDDF6E8C79A11E0` |
| r51 | `67834248BEAE5B52375CACE1214D5512750975B8772F948FFAD609116E574E83` |
| 행정동 r39 | `279BB1640D380F2C4E0B4541D044F1BAC610DAE4D65C194424B9437BA674339F` |

현재 직접 방문 export에는 좌표 후보·지역 미결속이 남아 있다. 이를 실제 위치로 승인하거나 가짜 정상 화면으로 바꾸지 않는다. 이번에는 입력 검사→준비/보류 결과→연출 순서 계산을 먼저 구현하고, 공간에서는 기존 우선 대상 면목제3·8동의 도로 한 구간과 주변 객체를 선택하는 보조 자료를 만든다.

DB 종류·보관/철회 정책·실제 전송 API가 미정인 부분은 그대로 남긴다. 이번 결과를 DB 저장·Unity로 보내기 종단 연결 완료로 보고하지 않는다. 새 운영 서버·지오코딩·자료 수집·30개 동 재생성·Unity Editor/Scene 변경은 제외한다.

## 정확한 쓰기 경로와 소유

기존 `RegionalPickupRecordBinding.cs`와 공개 v2 속성·`.meta` GUID는 보존한다. 새 파일이 기존에 존재하거나 다른 담당이 수정 중이면 다시 확인한다.

- 입력 담당: `Ssalddel.Unity/Runtime/WorldProjection/방문기록JsonDecoder.cs` 및 `.meta`, `Ssalddel.Unity.Tests/방문기록JsonDecoderTests.cs`.
- 준비 담당: `Ssalddel.Unity/Runtime/WorldProjection/방문재생준비.cs` 및 `.meta`, `Ssalddel.Unity.Tests/방문재생준비Tests.cs`.
- 통합 담당: `Ssalddel.Unity/Runtime/WorldProjection/방문순서재생Session.cs` 및 `.meta`, `Ssalddel.Unity.Tests/방문순서재생SessionTests.cs`.
- 공간 담당: `eng/neighborhood/administrative_dong_road_focus_private_review_r1.py`, `eng/neighborhood/test_administrative_dong_road_focus_private_review_r1.py`. 기존 미완성 interior-ring 생성기는 읽기 참고만 하며 수정·import·실행하지 않는다.
- 비공개 실행 산출물: `artifacts/local/validation/visit-replay-focused-area-r52/`, `artifacts/local/validation/admin-dong-road-focus-private-review/r1/`; 기존 입력/세대 파일을 덮지 않는다.
- 문서: 이 r52, 기획 정본 README, `docs/AI/PLANNING.md`, `docs/AI/CURRENT_WORK.md`.

## 이번 코드의 계약

1. JSON decoder는 기존 camelCase v2를 읽으며 필수 필드 누락·잘못된 형식·큰 입력·운영/완료 확정을 거절한다. Core에 UnityEngine 또는 새 외부 JSON 패키지 의존성을 넣지 않는다. 채택한 표준 라이브러리는 영향 project에서 실제 컴파일·시험한다.
2. 준비 검사는 순서 정렬·중복/누락·재방문·불명확한 좌표/지역을 구분한다. 문자열 `Verified` 하나만으로 위치 근거를 완료 처리하지 않는다. 원본 기록과 일치하는 명시적인 위치 결속 근거 및 공통 표시 좌표를 받아야 재생 가능 결과를 만든다. 원본과 결과의 변조를 분리한다.
3. 재생 Session은 준비된 읽기 전용 방문 목록과 연출 시간만 사용한다. 재생·정지·번호 선택·끝·0거리 재방문을 처리하며 실제 시간·거리·업무 완료로 해석하지 않는다. 입력 교체 실패는 기존 사본을 보존한다.
4. 공간 선택은 기존 r7 선택 동의 완전 bundle과 원본 hash를 검증한 뒤 `bbox·선택 도로/건물 ID·출처·결손`을 보조 파일로 만든다. 부분 타일을 완전 manifest로 위장하지 않는다. 도로 중점이 밖이어도 범위와 교차하면 선택하며 원본 도형을 바꾸지 않는다.
5. 새 비공개 검토 구간은 기존 사가정 상세 위치를 기준으로 작은 범위를 고른다. 실제 도로 폭·보도·출입구·내부 ring·주행 가능성은 확정하지 않는다. 다른 동은 이 절차를 재사용할 후속 대상이지 동시 실행 대상이 아니다.

## 표현 전용 수직 검증 명세·상한

새 권위 WI/장기 Goal은 만들지 않는다. Required 전문 연구는 이 비권위 입력/자료 선택 범위에는 없으며, 상세 형상·주행·새 플레이 약속을 결정하는 작업은 별도다. `evidenceStageClaimed=null`을 유지한다.

| 확인 | 이번 계획·경계 |
| --- | --- |
| E1 의미 | 기록 당사자가 과거 방문 순서를 관찰한다. 직접 결과는 준비/보류 목록과 표현 위치다. 업무 완료·NPC 상태·성장 변화 없음. |
| E2 조립 | 기존 v2 계약 뒤 별도 준비/재생 코드를 둔다. 원본·DB·Scene 불변. 순서 미정/위치 부족은 보류. |
| E3 시험 | decoder 필수 값·입력 상한, 순서·재방문·변조 격리·결정성·시계/정지·공간 교차/hash/동일 입력 검사. |
| E4 표현 준비 | 기존 가게 표지/기사 기호·공통 좌표 후보를 위한 입력만 준비. 신규 자산/Prefab 확정 없음. |
| E5 실제 결속 | 이번 범위 밖. 실제 Unity consumer 연결 전에는 배치 완료를 주장하지 않는다. |
| E6 회복·동작 | 순수 계산의 실패/재시도만 시험. 실제 이동·자원 해제·카메라는 미검증. |
| E7 실제 화면 | 이번 범위 밖. 후속 소유권 확인 후 Play Mode·Game View·Console을 별도 검증한다. |

## 검증 명령과 남은 일

- 집중 .NET 시험은 신규 3개 class와 기존 `RegionalPickupRecordBindingTests`, `생활관찰표현SessionTests`를 포함한다. 영향 `Ssalddel.Unity.slnx` build를 별도로 수행한다.
- 공간 도구는 합성 fixture 단위 시험→선택 동 실제 입력 build→동일 입력 재실행/검증 순서다. 출력은 비공개 산출물이며 공개·현재 행정 경계·Traversal·Gameplay 승격 없음.
- 정확 소유 경로의 Fast/Task 및 `git diff --check`를 실행한다. 전역 생성 지도 불일치가 다시 나오면 관련 없는 생성물을 덮지 않고 집중 build/test와 구분해 보고한다.
- 후속은 준비된 실제 위치 결속→Unity consumer/동간 조립→저장·선택 전송의 API/권한 계약 순으로 남는다. 어느 한 단계의 성공을 전체 연결 완료로 확대하지 않는다.

## 실행 결과

### 입력·준비·순서 재생

- `방문기록JsonDecoder`: 기존 v2 camelCase를 명시적으로 읽는다. 표준 .NET JSON reader의 느슨한 문법 허용을 사전 검사로 제한하고, UTF-8 8MiB·깊이 32·방문 10,000·JSON 값 400,000 상한, 필수 값·중복·운영/완료 권한 차단을 적용했다. 원문이나 내부 파서 오류를 예외에 싣지 않는다. 새 NuGet·UnityEngine 의존성은 없다.
- `방문재생준비`: 전체 기록 1..N 순서, 같은 가게 재방문, 기록 판본·공개 위치·공통 좌표계·경계/좌표 근거 결속을 검사한다. 문자열 상태만으로 위치를 승인하지 않는다. 근거를 조사하는 서비스는 이번에 만들지 않았으며, 신뢰된 검토 경로가 생성한 명시적 결속 값을 받아 일치 여부만 검사한다. 실패하면 전체 재생 목록을 비워 중간 결손을 건너뛰지 않는다. 원본 DTO 변경과 준비 결과를 격리하고 모든 입력을 SHA-256 fingerprint로 묶는다.
- `방문순서재생Session`: 준비→재생/일시정지→방문 번호 선택→배속→종료·Clear를 메모리에서 처리한다. 프레임 누적 대신 기준 시계로 계산한다. 같은 입력 재전달은 진행을 보존하고 같은 기록 판본의 다른 내용은 충돌로 거절한다. 새 판본은 자동 재생하지 않는다. 소수 연출 시간의 방문 경계·끝과 정지 중 `IsMoving=false`도 회귀 시험했다. 선형 보간은 연출 위치이지 실제 도로나 주행선이 아니다.
- 실제 로컬 export `delivery-visits.v2.json` SHA-256 `1BFDC56363D5611DE0C1D0A5A2A5BE755B35E4004167A7816842CFD2B5719661`: decoder **7건 읽기 성공**, 위치 근거 미제공으로 준비 목록 **0건·CanReplay=false**. 기존 미결속을 임의 승인하지 않았고 원본을 변경하지 않았다. 비공개 probe 산출물에는 상호·주소·방문 ID 대신 개수·hash·진단 종류만 남겼다.

### 면목제3·8동 첫 구간

- 기존 r39의 사가정 상세 후보 건물 외곽 중심과 가장 가까운 원본 도로 선분을 기준으로 **250m × 250m** 검토 상자를 골랐다. 현재 확정된 도로명·출입구나 방문 기록 7건과의 위치 일치를 주장하지 않는다.
- 동결 r7 index·completion·선택 동 완전 bundle **3파일**을 검사하고, 다른 동 bundle은 **0개** 읽었다. 원본 **12타일·건물 2,700개·도로 291개**를 보존하고 범위와 교차하는 **도로 9개·건물 86개**의 안정 ID만 별도 참조 자료로 생성했다. 도로 중점이 밖인 관통선·상자를 감싸는 건물도 선택한다.
- 최종 generation/content hash: `30E44F71DDA3319FD73C195CD60F14FB7FE3349292798C8EFB86CEA9C30F8713`. `focus.json` 파일 SHA-256: `9E53A5968E06421565EE1028EDF13673B39A00950753A8637E4F30D53B72A133`. 출력은 `artifacts/local/validation/admin-dong-road-focus-private-review/r1/generations/30e44f71dda3319fd73c195cd60f14fb7fe3349292798c8efb86cea9c30f8713/`에 있으며 Git 제외다. 초기 숫자 정규화 보완 전 `DFB5…` 후보도 보존했으나 최종 근거로 사용하지 않는다.
- 실제 `build → 동일 입력 build → verify` 통과. 첫 생성 2파일, 재생성·검증 `changedFiles=0`. 도형·타일·current pointer·DB·Unity는 바꾸지 않았다. 이는 **정밀화할 구간을 준비한 것**이며 실제 거리 형상·보도·내부 ring·이동 가능성 개선은 아니다.

### 검증 증거와 남은 경계

| 증거 | 결과 | 대신하지 않는 것 |
| --- | --- | --- |
| 신규/기존 입력·생활 관찰 집중 시험 | 초기 144/144 통과; 교차 검토 후 2개 회귀 시험 추가 | 실제 Unity Editor 컴파일·표현 |
| 최종 Unity 패키지 전체 .NET 시험 | **958/958 통과**, 건너뜀 0 | Play Mode·Game View·실제 HTTP |
| `Ssalddel.Unity.slnx` build | 경고 0·오류 0 | Unity 플랫폼별 빌드 |
| 공간 생성기 합성 시험 | **24/24 통과** | 현재 경계/권리/상세 형상의 승인 |
| 실제 한 동 입력·재실행·verify | 모두 통과, 다른 동 bundle 0 | 전체 30동 재검증·재생성 |
| 실제 방문 자료 입력 probe | 7건 파싱·재생 보류 확인 | 위치 근거 완성·지도 배치 |

.NET TRX와 build/probe는 `artifacts/local/validation/visit-replay-focused-area-r52/`에 남겼다. Session 시험은 v2 직렬화→decoder→준비→Session을 같은 fixture로 통과시킨다.

- 정확 소유 15경로의 Fast/Task는 `git diff --check`와 Simulation Unity 코드 지도 검사를 통과한 뒤 **공용 E 책임 생성 지도와 현재 소스 불일치**에서 각각 차단됐다. 공용 생성물은 이미 다른 변경이 있는 경로여서 이번에 `--write`로 덮지 않았다. 이 결과를 전체 관문 통과라고 하지 않는다.
- Fast 로그: `artifacts/local/validation/20260926-190054/evidence-map-check.log`, Task 로그: `artifacts/local/validation/20260926-190116/evidence-map-check.log`. 전역 관문에서 실행되지 못한 영향 solution build와 전체 패키지 시험은 별도 명령으로 실행한 위 증거를 사용한다.
- 최종 문서 4개 범위 Fast는 별도로 통과했다(`artifacts/local/validation/20260926-190436/`). r52 본문 전체 및 목차/상태판의 새 연결 8개, 신규 untracked 12파일의 후행 공백, `git diff --check`를 확인했다. 이미 있던 다른 문서 링크까지 전수 무결성을 주장하지 않는다.
- 통합 담당이 Python 합성 24개와 실제 최종 generation `verify`를 재실행해 모두 통과·`changedFiles=0`을 확인했다. 최종 .NET 결과는 `tests/all-unity-package-integrated.trx`, build는 `build-final.log`, 공간 결과는 `spatial-tests.log`·`spatial-verify.json`이다. 위 산출물은 모두 r52 비공개 검증 디렉터리에 있다.

남은 순서: (1) 실제 방문의 위치·행정동 결속 근거 확인, (2) 이 한 구간의 세부 형상 보완과 기존 Unity consumer 결속, (3) 동간 공통 좌표 조립·실제 Play/Game View, (4) 기록 보관·선택 전송의 API/권한/철회 계약. DB 종류·전송 계약을 추정하여 새 저장소나 자동 전송을 만들지 않았다.

출처 점검은 기존 r7의 source/hash/vintage·결손을 그대로 전달하는 범위다. 기존 출처 보존·작은 구간 선택 원칙을 지지하고 새 공통 규칙 후보는 없다. **새 디오라마 규칙 후보 없음.** 새 Goal/WI·Graph Map·E 승격, commit·push·배포는 수행하지 않았다.
