# Codex → Claude · 구 지도 서울 전체 축소·전국 직접 전환 r184

- 작업실: Codex 기획 → Claude 개발 수용/구현 계획.
- 상태: `UserDirectionConfirmed / HandoffPrepared / DevelopmentAcceptancePending / ProductUnchanged`.
- 승인 방향: 사용자가 구 지도 내 서울 전역까지 연속 축소, 독립 서울 단계 제거, 구→전국 직접 전환을 지시했다.
- 정본: [PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION r184](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/district-seoul-continuous-zoom-and-nation-transition.design.r184.md).
- 정본 SHA256: `59a97c5683fbb490aa28ce1222cbc27b0eb48b0480bc4f8c473160bf60d173af`.
- [입력 지문/작업본](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/district-seoul-map-inputs.r184.json): 코드/현재 자원/기존25구 후보/가공 도구13파일. 같은 PC의 읽기 기준이며 실행·통합 증거가 아니다.
- 반환 위치: `docs/AI/Handoffs/2026-11-14-Claude-구지도서울전체축소-전국직접전환-반환.md`.

## 1. 이번에 만들 결과

구 지도에서 휠로 시야를 넓히면 서울25구가 한 화면에 들어온다. 선택 OS/업무를 유지한 채 다른 구를 살펴보고, 상위 지도 조작은 곧바로 대한민국 전국으로 간다. 돌아오면 이전 구 지도 문맥을 복원한다. 기존 서울 순회 안건은 구 지도 공통 안건으로 이어진다.

독립 서울 지도·자물쇠·단계 해금·필수 정거장을 새 활성 경로에 남기지 않는다. 서울이라는 지리 명칭·원본·좌표는 유지한다. 세계/지구본의 기존 경로·정거장은 이번에 추가/삭제하지 않는다.

r171의 서울 보충망 제안, 기존 Claude의 동→구→서울→전국 경로/시작 단계 칩/서울 해금 표는 이 범위에서 r184가 부분 대체한다. 기존 시안·반환 원문과 승인 hash는 수정하지 않는다. 전국 해금 조건·광고·연구 비용/시계 규칙을 새로 정하지 않는다.

## 2. 현재 작업본과 소유

- Hongdal: `Hongdal/` · `dev/mirror-integration@7c83c18cf9607b3854120c15f85d5dd8f9ab14d8`.
- Unity: `ssalddel/` · `unity/workstream-transition@605e87ef46b2ebd3b19f65ebd9d650e0ccf44d77`.
- WindowsGame은 미커밋/미추적 입력이다. HEAD가 같아도 입력 지문과 dirty 경계를 다시 비교한다.
- Claude의 [현재 고도화 순서](2026-11-14-Claude-고도화-순서표.md) A1~A6가 카메라/좌표 계산을 만질 수 있다. 같은 파일의 쓰기는 현재 담당과 조율해 한 소유자가 병합하고 기존 리팩토링을 되돌리지 않는다.
- 현재 Codex는 읽기 대조와 문서만 작성했다. Unity 파일·빌드·사용자 실행 창·세이브 소유를 인수하지 않았다.
- 다른 PC/계정에는 로컬 경로와 미커밋 자료가 자동 전달되지 않는다. 이 인계를 commit/push 완료로 해석하지 않는다.

## 3. 재사용과 수정 후보

아래는 Unity `Assets/Ssalddel/WindowsGame/` 기준이다. 정확 줄은 입력 판본의 탐색 힌트이며 수용 시 다시 확인한다.

| 위치 | 현재 확인 | 필요한 변화 |
| --- | --- | --- |
| `Runtime/SagajeongDispatchGu.cs` · UpdateGuCamera/GuWheel | 초기 `guFitScale`이 축소 바닥이 됨. 비수동 상태는 곧 상위 전환. 전체맞춤도 큰 비율로 잘림 | 초기맞춤/서울전체최소배율 분리, 작은 비율로 전체 포함·중앙정렬·팬/화면 크기 대응 |
| `Runtime/SagajeongDispatchMap.cs` | Dong/District/Seoul/Nation 전환·서울 표제 분기 | 명시적인 구↔전국 경로, 서울 내부값의 구 보기 호환, 표제/출처 정리 |
| `Runtime/SagajeongDispatchLevelPrompt.cs` | enum ±1 이동과 서울 안내 | 정수 산술 대신 인접 경로 사용, 서울 독립 안내/단계 제거 |
| `Runtime/SagajeongDispatchNation.cs` | 서울 권역 확대/서울 지도 버튼이 옛 단계로 귀환 | 구 지도의 이전 문맥 또는 서울 전체 맞춤에 연결 |
| `Runtime/SagajeongDispatchCycle.cs` | 서울 정거장 도착 시 Seoul 지도 | 구 공통 안건의 지도 문맥으로 도착 |
| `Runtime/결정순회Policy.cs`, `결정순회Checks.cs` | 서울 정거장·우유 부족의 seoul 올림/필수 보충 안건 | 활성 정거장에서 서울 제거, 기존 안건은 원 ID로 구 공통 영역에 결속하고 검사 갱신 |
| `Runtime/SagajeongDispatchEngine.cs` | SaveEnvelope는 음식/커널 JSON. 지도/배율/순회 영속 저장 없음 | 읽기 대조용. 이번에 영속 저장 형식 확장하지 않음 |

지도 enum과 순회 enum의 서울2·전국3은 조용히 재번호화하지 않는다. 숨은 옛 호출도 구 보기로 정규화하고 새 순회 목록에 독립 서울 정차를 다시 만들지 않는다. 지도 버튼만 바꿔 서울 필수 안건을 고립시키는 결과는 미완료다.

## 4. 서울 전체 바탕 후보

현재 `Resources/hongdal-gu-space.json`은 동북권 약20.7×11.6km/10시군구 명칭(서울9구 일부+구리)이며 서울25구 전체 자원이 아니다. `Resources/SagajeongDispatch/seoul.json`도 개략 외곽44선·이름표1·도로0이다.

실제로 보유한 후보:

- `Hongdal/artifacts/local/public-data/seoul-intermediate-boundaries-20260923-r1/OA-22161.zip`
  488,551바이트 · SHA256 `abd34d82897f0c2191fe4095df55a4aacfe8fce815fa4dacf730cc34c6dd962c` · EPSG:5181·UTF-8·2023-10-31·구25행.
- 같은 폴더 `seoul-globe-preview.json`
  2,564,753바이트 · SHA256 `d286faaa8899801c7f3af3141ee665e0e706fde2e69518028ac31e5301d907b0` · v3/r3 · 구25/행정동64·링/삼각형.
- 기존 가공 도구: `Hongdal/eng/neighborhood/build-seoul-intermediate-preview.py`는 읽기 참고. 이번 구 지도 작업을 이유로 다른 소비판의 도구를 고치지 않는다.

이 파생 JSON 전체를 새 지도 의미로 복사하지 않는다. **서울25구의 저상세 경계/이름만** 선별하고 구 코드·원점·배율을 현행 구 자료와 맞춘다. `observationPresentationOnly=true`, `traversalReady=false`, `distributionApproved=false`를 유지한다. 실제 도로 연결/실시간 사업 자료로 승격하지 않는다.

기존 후보의 바운드·구25개/고유 코드·위치 기준점·현재 상세와의 경계 일치를 검증한 뒤 새 내부용 바탕으로 연결한다. 역사 경계/지역별 상세 차이는 출처 칸에 표시한다. 새 서울 전역 도로/건물 수집과 내부 검토 자료의 배포는 범위 밖이다.

## 5. 쓰기 범위와 수용 절차

개발 수용에서 실제 재사용 WI/검증 명세와 정확한 파일 소유를 결속한다. 후보 writePaths는 다음과 같이 한정한다.

- 위 표의 지도 이동/순회 Runtime7파일과 이 범위의 새 보조 자료형·검사. Engine은 기본 읽기 전용.
- `WindowsGame/Resources/hongdal-seoul-district-background.json` 및 그 신규 `.meta` 후보:25구 저상세 바탕. 파일명은 수용 계획에 확정하고 원본과 별개로 생성한다.
- 이 자료만 만드는 `WindowsGame/Tools~/`의 국소 가공/검사 도구. 기존 동 모듈 굽기 형식이나 원본을 함께 개정하지 않는다.
- 반환 문서와 자기 검증 산출물. 중앙 기획/PLANNING·공유 원장·기존 지문 원본은 수정하지 않는다.

기존 Core/WorkflowRules/Simulation/MobileGame·Graph Map/Goal 원장·원본 자료·기존 세이브·수집/예약은 이번 쓰기 범위가 아니다. 공유 계약 변경이 실제 필요하면 현재 담당과 별도 범위를 결속해 반환하며 문자열 치환으로 우회하지 않는다.

사용자 확정 방향에 대한 재승인을 요구하지 않는다. 기존 화면/자료/계약과의 수용 대조, 파일 쓰기 소유, 시안 선행 절차·WI의 E1~E7 검증 명세 결속은 실제 개발 단계에서 수행한다. 수용은 본 인계/hash와 현재 입력의 일치·차이를 기록하고 자기 작업 범위를 선언하는 것이다. 이 문서만으로 실행권/Goal/Runtime 성공이 자동 생성되지 않는다.

## 6. 검증과 반환

r184 §5의 완료표를 기준으로 한다.

1. 순수 검사: 구 상위가 전국/전국 귀환이 구인지, 옛 서울 요청의 호환, enum 수치 유지, 기존 안건의 원 ID/이행·자원 보존, 전국 해금/업무/시간 불변.
2. 공간 검사:25구 모두 포함·고유 코드/원본 지문·원점/배율·현재 상세와 위치 일치, 가로·세로/패널 상태별 전체맞춤/클램프·선택.
3. 같은 세션 왕복: 근접/서울전체 두 배율에서 전국 왕복 후 중심·배율·선택 구/OS/대상·작성 입력 복원. 문맥 없는 서울 진입은 전체맞춤.
4. 기존 자동검사/빌드: 현재 고도화 순서표의 기존 검사 수를 유지하며 관련 새 검사를 추가한다. `auto-build-check.cmd`의 명령과 실제 결과를 재확인하고 단순 스크립트 종료만으로 성공을 보고하지 않는다.
5. 일반 실행 화면: 연속축소 중간→서울전체→전국→이전 구 왕복, 지도/순회에서 서울 독립 단계 없음, 기존 서울 보충 안건 접근을 확인한다. `--sagajeong-…` 전용 촬영은 일반 화면 증거와 별도로 적는다.

코드/자원/검사 변경·자동 시험·빌드·실제 화면·미확인을 분리해 반환한다. 실패가 나면 기존에 되던 것과 가장 이른 재개 지점을 적고, 업무 완료/해금으로 가장하지 않는다. 이번 Codex 대조는 자동시험·Unity 빌드·게임 실행 검증이 아니다.

## Codex 작성 검증

문서/입력 검증 (로컬 검토 자료 `artifacts/local/district-seoul-plan-r184-20261114/verification.json`)에서 정본 SHA·이 인계의 hash 참조·로컬 연결·UTF-8/공백과 서울25구/고유 코드·원본 제한을 대조했다. 최초 대조13/13 일치 뒤 최종 점검에서 `SagajeongDispatchLevelPrompt.cs`의 동시 변경을 관찰했다. 입력 JSON은 당시 기준으로 보존하고 일치수/변경 목록은 검증 기록에 분리했다. 수용자가 최신 코드 차이와 쓰기 소유를 다시 확인한다. 지정7문서/JSON의 Fast와 `git diff --check`를 통과했으며 Fast 자체의 빌드/시험은 생략됐다. 실제 Unity/화면 검증·새 도형 전체 검사·실행권 수용은 이후 반환 범위다.
