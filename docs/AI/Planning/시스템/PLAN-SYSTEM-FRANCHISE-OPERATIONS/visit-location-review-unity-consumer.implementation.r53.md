[구현 수용·검증 · 방문 자료·Unity 연결 · PLAN-SYSTEM-FRANCHISE-OPERATIONS · r53]

# 위치 보완 목록에서 기존 디오라마의 순서 재생 연결까지

- 기준일: 2026-09-26.
- 승인 근거: 사용자의 “우선순위 나눠서 계속 진행해서 구현” 후속 요청. 같은 스레드에서 아래 P0/P1만 `Approved / ReadyToDispatch / Accepted`로 수용한다. 전체 저장/전송·지역 확대·공개를 승인한 것으로 확대하지 않는다.
- 선행: [r52](visit-replay-focused-area.implementation.r52.md), SHA-256 `CD2D5C35E036D5A44BC0D29DD2CC828D97FB00932B01A6C05951B9FAE1776A47`. [r49](visit-sequence-cross-dong-refactoring.proposal.r49.md)의 순서·공통 공간 약속과 r51 보관/전송 경계는 보존한다.
- 주 책임: 비공개 입력 검토와 Unity 읽기 전용 표현. 운영·Simulation 상태 변경 없음. 새 Goal/WI·E 승격 없음.
- 현재 상태: `ImplementedAndEditModeVerified_ActualVisitsPending`. P0 검토 목록·P1 기존 지도 연결을 구현/시험했다. 실제 방문 위치 승인·Play/Game View·동간 조립은 미완료다.

## 우선순위와 이번 완료선

| 순위 | 이번 결과 / 경계 |
| --- | --- |
| P0 | 기존 방문7건과 동결 역사 경계를 대조하는 비공개 후보·결손 보고서. source/hash/frame·순서·포함/경계/복수/범위 밖을 분리하고 실제 좌표 승인·Verified·방문위치결속은 생성하지 않는다. |
| P1 | 기존 동 전체 지도와 r52 작은 focus를 검증한 뒤 방문 Session의 명시적 로드·재생·정지·번호 선택을 Unity consumer에 연결한다. 실제 기록 준비 실패는 진단·표시 없음으로 남기며 합성 fixture는 시험에서만 사용한다. |
| P2 | 동간 공통 좌표 조립과 실제 기록 재생. 필요한 위치 근거와 P1 검증 뒤 후속으로 수행한다. 이번에 범위를 자동 확대하지 않는다. |
| P3 | 기존 보관 DB·본인 권한의 선택 전송 API·철회/만료. 저장소·정책 선택을 현재 대화만으로 확정하지 않는다. |

실제7건의 `AddressGeocodeCandidate_NotEntrance`와 `Unresolved`를 사실확정으로 바꾸지 않는다. 역사 경계에 포함된 좌표 후보라는 결과는 현행 행정동·매장 동일성·출입구의 증거가 아니다. 오래된 명세에만 있고 실제 파일이 없는 자료를 재사용했다고 하지 않는다.

## 정확한 소유 경로

Hongdal:

- `eng/neighborhood/visit_location_binding_private_review_r1.py`
- `eng/neighborhood/test_visit_location_binding_private_review_r1.py`
- 이 r53, 정본 `README.md`, `docs/AI/PLANNING.md`, `docs/AI/CURRENT_WORK.md`
- 비공개 산출물 `artifacts/local/validation/visit-location-binding-private-review/r1/`, `artifacts/local/validation/visit-location-unity-r53/`

별도 Unity 작업트리 (`C:/Users/user/ssalddel`, 경로는 이번 로컬 실행 위치이지 배포 계약이 아님):

- `Assets/Ssalddel/Runtime/World/방문재생공간검토Models.cs` 및 `.meta`
- `Assets/Ssalddel/Presentation/World/방문순서재생검토Presenter.cs` 및 `.meta`
- `Assets/Ssalddel/Editor/방문순서재생검토진입.cs` 및 `.meta`
- `Assets/Ssalddel/Tests/EditMode/방문순서재생검토Tests.cs` 및 `.meta`
- 검증 산출물 `artifacts/visit-location-unity-r53/`

기존 dirty Models/View/MeshBuilder/Controller/검증기를 일괄 수정하지 않는다. 새 파일이 이미 있거나 같은 경로의 동시 수정이 보이면 소유권부터 재확인한다. Scene·Prefab 저장, 새 공식 Scene, 기존 지도의 전역 교체, commit·push·배포는 제외한다.

## 계약과 검증 상한

- P0는 기존 전체 생성기에서 필요한 좌표/경계 읽기 함수만 재사용하고 전체 생성은 실행하지 않는다. 원본 hash·동결한 scope/frame을 검사하고 참조/진단 출력의 동일 입력 결정성과 변조 거절을 시험한다. 원본 export는 변경하지 않는다.
- P1은 완전 사본의 검증을 유지하며 focus의 선택 ID·source/hash/frame을 대조한다. 동 전체 사본을 부분 manifest로 바꾸지 않고 기존 카메라와 표시 좌표를 재사용한다. 데이터 실패를 합성 표본으로 감추지 않는다.
- 준비된 방문의 공통좌표는 `(X/Z - SourceCenterX/Z) / MetersPerUnit`로 기존 지도 좌표에 옮긴다. frame 일치와 해당 지도/검토범위를 검사한다. marker는 정밀 GPS·입구·실제 이동선이 아닌 기록 재생 표시다.
- 진입은 명시적 선택/호출만 허용한다. 현재 canonical `SimulationWorldShell`과 기존 View를 재사용하고 저장하지 않는 표현 root를 사용한다. 시작 자동 재생 없음. 초기 실패는 marker0, 갱신 실패는 기존 정상 표현 보존+오류 표시, Clear/종료는 소유 객체와 자료만 정리한다.
- 원문 기록·토큰을 Scene/Prefab/PlayerPrefs/Save/Replay에 저장하지 않는다. 선택 전까지 상호·메뉴의 상세 표시를 기본 화면에 늘리지 않는다.
- 실제 실행 담당은 통합에서 Editor 상태를 확인한 뒤 한 명으로 지정한다. 실행 중인 다른 작업을 종료하거나 같은 Editor를 중복 점유하지 않는다. Pipeline 연결 가능 여부를 먼저 확인하고 불가능하면 Unity CLI 시험으로 대체한다.

| 확인 | 이번 좁은 검증 |
| --- | --- |
| E1/E2 | 비운영 과거 방문·근거 보완·선택적 표현. 기존 v2/Session과 완전 지도 사본 재사용. |
| E3 | hash·경계 점·권한 플래그·공통좌표·순서·실패 보존·재로드·자원 정리 회귀. |
| E4 | 중립 기호/번호·읽기 전용 UI, 기존 아이소메트릭 카메라. 새 실제 건물/Actor 자산 없음. |
| E5/E6 | Unity 실제 import·EditMode 객체/좌표/소유자원 검증은 실행 여부대로 기록. 시험 합성 결속은 실제7건 승인이 아니다. |
| E7 | Play Mode·Game View는 별도 실행 증거가 있을 때만 보고. 코드나 EditMode로 대체하지 않는다. |

위 표는 검증 계획이며 `evidenceStageClaimed=null`이다. 이 작업은 운영 행위나 새로운 게임 권위 WI를 만들지 않는다. 새 Required 전문 연구 결정을 추가하지 않으며 상세 형상·주행 경로·다중 동 조립은 이번 P1 계약 밖이다.

## 실행 결과

### P0 비공개 위치 보완 목록

- `visit_location_binding_private_review_r1.py`는 고정한 export·scope·역사 경계·재사용 helper의 SHA-256을 먼저 확인하고 기존 경계 읽기/ENU 변환만 재사용한다. 원본 방문 상태와 주소를 변경하지 않는다. 출력은 Git 제외 경로에만 생성한다.
- 실제 방문7건은 2023-10-31 역사 경계의 단일 내부 후보6·30동 검토 범위 밖1·경계 접촉0·복수 포함0이다. 모두 `PendingReview`, 승인0·재생 결속 receipt0이다. 역사 경계 안에 들어간다는 결과로 실제 위치나 현행 행정동을 확정하지 않는다.
- 기존 `sourceUrls`는 메뉴 근거이므로 위치 근거로 사용하지 않았다. 매장/주소/좌표 대조, 독립 좌표 근거, 현행 경계, 검토된 위치 결속의 결손을 방문별로 분리했다.
- generation: `A7889FA891E4633C08CE3138B726356E59EEC166E17E8E45E6C5501626A65418`.
- 로컬 산출물: `artifacts/local/validation/visit-location-binding-private-review/r1/generations/a7889fa891e4633c08ce3138b726356e59eec166e17e8e45e6c5501626a65418/report.json`; 파일 SHA-256 `10A6D693C97B76F612F49495CFA79BD2845F2853ADDB2237B59FEE2071AABD87`.
- 합성 시험21/21 통과. 최초 build2파일 뒤 재build·verify는 `changedFiles=0`이며 통합에서 시험과 실제 verify를 독립 재실행했다. 원본5파일 hash 불변을 확인했다. 이 결과는 실제 방문의 재생 가능 판정이 아니다.

### P1 Unity 연결과 나머지 검증

기존 지도 로더를 복제하지 않고 이미 로드된 완전 지도 View에만 명시적으로 연결한다. 지도 준비 자체·동간 조립·실제 방문 위치 승인은 이번 P1의 완료 범위가 아니다.

- 순수 공간 모델은 전체 지도 사본의 판본·개수·원점·단위와 선택 ID를 대조하고 공통 ENU 좌표를 기존 지도 로컬 좌표로 한 번만 변환한다. focus 밖·지도 밖·다른 동/좌표계의 방문은 거절한다.
- Presenter는 원래 지도 root를 교체하지 않고 선택 도로/건물의 중립 강조·참조 범위·번호 기반 방문 표시를 소유한다. 명시적 재생·일시정지·방문 선택, 동일 자료 재로드, 실패 시 마지막 정상 표시 보존, 소유 Mesh/Material 정리를 연결했다. 카메라가 검토 이후 사용자가 바꾼 상태이면 Clear가 덮어쓰지 않는다.
- Editor의 `Ssalddel/검토/방문 순서 재생`에서 이미 로드된 View·Hongdal 폴더·비공개 방문 JSON을 선택한다. `위치 준비 검사`와 `250m 검토 범위 표시`, `준비된 방문 적용`은 별개다. 검사 중인 입력과 이미 적용한 입력을 분리하여 이전 marker에 새 기록의 상호/메뉴가 섞이지 않게 했다. 선택 지도와 적용 지도도 일치해야 한다.
- 근거 JSON을 명시적으로 선택/신뢰해도 원본의 미확정 상태를 자동 변경하지 않는다. 현재 실제7건은 준비 실패로 재생할 수 없으며 합성 fixture는 시험에만 있다. 서버/API·DB·보내기 버튼·로그인 권한 경로를 새로 구현한 것은 아니다.

| 증거 | 결과 / 범위 |
| --- | --- |
| 기존 공유 패키지 | `Ssalddel.Unity.Tests` 958/958. `artifacts/local/validation/visit-location-unity-r53/tests/package-baseline.trx`. 이번 Unity 표현 변경과 별도 기준선이다. |
| 자료 검사 | 새 위치 보완21/21, 기존 구간 선택24/24. 실제 P0 재조회/동일 입력 무변경. |
| Unity 실제 import·EditMode | Unity `6000.5.6f1`. 최초 신규12/12 뒤 카메라 회귀2개를 추가해 최종 신규14+기존 지도44 = **58/58**, 실패/건너뜀0. `C:/Users/user/ssalddel/artifacts/visit-location-unity-r53/editmode-final.xml`. |
| 실제 파일 검사 | 위14개 중2개는 실제 r7 index/completion/선택동/focus의 hash·도형 ID·개수를 확인하고, 실제 방문7건의 `CanReplay=false`를 확인했다. 실제 방문 표시 성공 증거가 아니다. |
| 보존 | 기존 Scene SHA-256 `C31167703C4A7683DA374E237CBA3C9584A5F1DE4B5FB5746939A3632635AC65` 전후 동일. 새4개 C#의 `.meta` 짝/고유 GUID 확인. Unity 시험이 자동 변경한 `SENTIS_ANALYTICS_ENABLED` define만 원래 값으로 복구해 추가 설정 diff0이다. |
| 정적 검사 | 이번 Hongdal 6경로 범위 Fast/Task와 diff 검사 통과. 도구가 eng/docs를 guidance로 분류하므로 build/test 자동 실행은 생략되며 위 Python/.NET/Unity 시험을 따로 수행했다. r52에서 남은 전역 E 책임 생성 지도 차이를 해결한 것으로 보지 않는다. |
| 실제 화면 | Play Mode·Game View·캡처·Windows 빌드 미실행. EditMode 성공이나 Scene hash 동일을 실제 화면 완료로 대체하지 않는다. |

실행 중 새 코드의 `Application` namespace 충돌을 `UnityEngine.Application`으로 수정하고 재시험했다. 사전 검토에서는 decoder 인스턴스 호출과 focus의 저장소 기준 상대 경로 비교를 바로잡았다. 기존 프로젝트 경고 전체/기존 Play Console 오류가 해소됐다고 주장하지 않는다.

재현은 bundled Python 3.12로 P0 test·`build`/`verify`를 실행하고, Unity CLI의 EditMode 필터 `Ssalddel.Unity.Tests.EditMode.(방문순서재생검토Tests|행정동디오라마검토Tests)`를 사용한다. 실제 파일 시험은 `SSALDDEL_R53_REPO_ROOT`와 `SSALDDEL_R53_RECORD_PATH` 환경변수로 로컬 경로만 전달한다. 토큰이나 원문 기록을 검증 로그에 넣지 않는다.

### 남은 순서와 규칙 점검

1. 실제7건의 매장/주소/좌표 및 현행 동 결속 근거를 보완한다. 현재 후보 보고서로 승인 상태를 대신하지 않는다.
2. 기존 지도 한 구간에서 승인된 입력의 실제 Play/Game View를 확인한다. P1의 이미 로드된 지도 의존도는 남아 있다.
3. 그 뒤 공통 좌표로 인접 동을 조립하고 실제 방문 순서 재생을 넓힌다. 전체30동 재생성은 하지 않는다.
4. 보관 DB·선택 전송·철회/만료는 기존 r51 미정과 연결하며 구현 완료로 보지 않는다.

원천은 기존 비공개 export와 동결 공공 지도이며 새 외부 수집이나 공개 권리를 만들지 않았다. 출처/hash·동결 자료와 표현 분리·상태 권위 불변·Scene 보존 규칙을 지지한다. 기존 규칙을 무효화할 반례나 새 공통 규칙 후보는 이번에 없었다. **새 디오라마 규칙 후보 없음.** commit·push·배포·새 Goal/WI·Graph Map·E 승격 없음.
