# Codex → Claude — 지도 경영 통합 기획 r162 준비 인계

## 무엇을 이어받는가

- 역할: **기존 코드 대조와 작은 구현 계획 검토**. 새 제품 코드 실행 인계가 아니다.
- 정본: [통합 기준 r162](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/integrated-map-management-baseline.r162.md).
- 입력: [판본·소스 지문 색인](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/consolidated-sources.r162.json). 이 JSON은 문서용 색인이며 Goal/실행 명세가 아니다.
- 수용된 범위: 최근 기획의 공통 규칙 통합·현황 정정·기존 구현 보존·구 기사 여력 후보의 우선 정밀화.
- 이번 준비 상태: `PreparedForImplementationPlanning`.
- 새 게임 코드 실행 상태: `NotReadyToDispatch`; `productWritePaths=[]`; 새 Goal/WI·기존 원장 변경 없음.
- 기준선 묶음의 master/index SHA는 이 문서 마지막 ‘입력 고정’ 절에 둔다.

기획 전체를 다시 읽어 새 플랫폼을 설계하거나 Core·우유 사슬·주문 효과를 재구현하지 않는다. 이번 대상과 관련한 전문 정본만 추가로 읽는다. 문서 정리를 한 번 더 허락받을 필요는 없다.

## 작업본·소유·보존

| 대상 | 현재 기준 | 의미 |
| --- | --- | --- |
| Hongdal | `C:/Users/user/source/repos/Hongdal`, `dev/mirror-integration`, `7c83c18cf9607b3854120c15f85d5dd8f9ab14d8` | 문서·공통 Core·수집 코드의 기존 작업본. 감사 당시 dirty614 |
| Unity | `C:/Users/user/ssalddel`, `unity/workstream-transition`, `605e87ef46b2ebd3b19f65ebd9d650e0ccf44d77` | 실제 `Assets/Ssalddel/WindowsGame` 소비판. 감사 당시 dirty125 |

HEAD만으로 미커밋 작업을 재현할 수 없다. dirty 개수는 감사 시점 정보이며 개수가 같다는 것만으로 동일 입력이라고 판단하지 않는다. 색인의 정확한 관련 파일/hash를 편집 직전에 대조한다. 관련 없는 dirty 변경이나 다른 Goal의 존재만으로 차단하거나 정리하지 않는다.

B0 1차 감사에서 WindowsGame의15개 반환 파일 지문은 최근 Claude 반환과 모두 일치했다. 이후 B1 마감 관찰에서는 관련10파일 변경으로 반환15개 중9개만 일치한다. 전국 N0/N1 새 소스/검사도 별도로 추가 관측됐다. 코드 보존 범위는 이름/투영/구 선택과 새 주문 효과·회전 보정이 함께 있는 **현재 파일**이다. 새 설계자가 예전 r152 소스나 2.5D 복사본으로 덮어쓰면 안 된다. 실행 중 창·빌드·사용자 저장의 소유는 이번 문서 감사로 인수하지 않았으며 실제 개발 수용 직전에 다시 확인한다.

B0 보호 지문은 감사 당시의 기록이며 지금의 쓰기 인수 승인으로 쓰지 않는다. 색인의 B1 관찰·변경/추가 목록을 읽고 공유 Host/Map/View의 실제 편집 소유와 새 반환을 먼저 대조한다. 현재 README가 설명하는 전국 진입 자동 정지는 r144/r162의 시간 계약과 검토 차이이며 이 문서에서 승인하거나 기존 수행자 코드를 되돌리지 않는다. 검토 준비는 계속할 수 있고 제품 쓰기는 아직 허용하지 않는다.

Core 직접 실행·GameKernel·FoodDeliveryModule·우유5팩 최소 사슬/제조·현재 자료팩·사용자 Save·MobileGame은 보호한다. 사슬의 배달 연결 잔여와 문서/자료대장 채우기는 별도 작업이다. 이번에 기존 모든 기능의 완성을 선언하거나 그 작업을 재배정하지 않는다.

## 먼저 읽을 입력

1. 통합 정본 r162, [현행 구현 표](Unity-Windows-구현현황.md), [최근 반환](2026-11-13-Claude-새주문핀파동과효과선택-반환.md).
2. [r161 구 초점](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/map-scale-os-attention-and-immersion.proposal.r161.md), [r144 전환/귀환](../Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/scale-os-management-focus.design.r144.md).
3. 색인에 적힌 다음 현행 소스. `unity:`는 위 Unity 저장소 상대 경로다.

```text
unity:Assets/Ssalddel/WindowsGame/Runtime/SagajeongDispatchGu.cs
unity:Assets/Ssalddel/WindowsGame/Runtime/SagajeongDispatchGuLabels.cs
unity:Assets/Ssalddel/WindowsGame/Runtime/SagajeongDispatchMap.cs
unity:Assets/Ssalddel/WindowsGame/Runtime/SagajeongDispatchView.cs
unity:Assets/Ssalddel/WindowsGame/Runtime/HongdalKernelHost.cs
unity:Assets/Ssalddel/WindowsGame/Runtime/지도새주문표현Policy.cs
unity:Assets/Ssalddel/WindowsGame/Runtime/SagajeongDispatchMapEvents.cs
```

커널/모듈/상태 사본의 정확한 선언 위치는 입력 색인의 WindowsGame 보호 목록에서 찾아 읽는다. 기존 ID/명령/저장/상태 소유를 확인하기 전에 새 DTO·이동 명령·별도 스케줄러를 만들지 않는다.

## 첫 후보 — 구 지도 기사 여력 판단

이 절은 **구현 계획을 작성하기 위한 비실행 초안**이다. 기존 [E1~E7 명세 체계](../../Architecture/E1-E7수직폐루프와E8-E10수평증거체계.md)의 내용을 준비하되, 정식 `.interaction-e1-e7-validation.json`·WI·주체 등록이나 `Approved` 판정으로 쓰지 않는다.

### 플레이 상황과 결과 후보

- 지금: 한 동네의 가상 픽업 대기가 늘었고 다른 동네의 여유도 지켜야 한다.
- 여기: 기존3구 지도 중 데이터/역할/범위가 준비된 한 구. 법정동·통계 행정동·서비스 권역을 동일시하지 않는다.
- 나: 현재 게임의 운영자 입장. 정식 주체/역할 결속과 허용 Command는 확인 전이다.
- 너: 기존 기사 중 다음 업무를 맡길 수 있는 후보, 확인된 동네/대기 대상, 이미 맡은 약속.
- 이렇게: 가용 후보와 목적/적용 가능 시점·부담을 비교→지원 대기 계획을 확정하거나 보류→같은 결과를 구/동네에서 다시 읽기→원래 선택으로 귀환.
- 첫 직접 결과 **추천**: 지원 **대기 계획 확정/보류와 재조회**까지. 실제 거점 도착·대기/이후 주문 배차는 별도 결과 후보이며 이번에 완료 범위를 확정하지 않는다.
- 대가: 지원 이동·다른 동네 여유 감소·기존 약속 영향과 불확실성을 비교한다. 실제 계산식/시간/비용·성장/보상은 미정이고 임의로 고정하지 않는다.
- 실패/회복: 이미 맡은 약속·가용 상태 변경·미확정 목적지·오래된 Revision 등을 이유로 기존 계획을 그대로 보존하며 거절/갱신/다른 후보/보류로 돌아간다. 지도 이동으로 실패를 성공 처리하지 않는다.
- 귀환: 같은 구/동네·선택 기사·대상 업무·카메라·입력/현재 상태를 읽는다. 카메라 이동·정보 닫기 자체는 권위 상태를 바꾸지 않는다.

### 입력 → 담당 → 권위 → 조회 → 표현

| 연결 | 기존/후보 판정 | 구현 계획에서 확인할 것 |
| --- | --- | --- |
| 구 지도 선택/준비 화면 | 기존 입력·카메라 재사용 후보 | 현재 GUI/선택/현재 OS와 상세 닫기 동작 |
| 후보 조회·비교 | 기존 상태 사본/정책 재사용 우선, 부족 여부 미정 | 실제 가용/진행 상태와 이미 맡은 약속. 추정 근거는 별도 표시 |
| 계획 확정/보류 | 계약 존재 여부 대조 필요 | 명령·검증·Revision·저장 주인이 어디인지. 없다면 기획에 새 의미를 반환 |
| 결과 재조회 | 같은 권위 상태의 조회 결과 | 구 요약과 동네 상세가 같은 결과 ID/Revision을 읽는지 |
| 현장 이동/도착 | 첫 결과와 별도 후보 | 기존 운행/통제/애니메이션과 충돌, 실제 포함 여부·필요 연구 |

현재 기사/운영자/대상 공간은 **재사용 후보**다. 존재하는 클래스나 화면을 정식 `Ready` 주체로 자동 승격하지 않는다. 새 권위 결과가 필요하면 그 후보·기존 재사용 불가 이유·작은 계약안을 기획으로 반환한다. 현재 계약으로 해소되는 기술 선택은 제작실이 조사해 정리하며 사용자에게 다시 일반 허락을 묻지 않는다.

### E1~E7 검증 준비표 — 단계 달성 주장 없음

| 단계 | Logic에서 준비할 것 | Presentation에서 준비할 것 |
| --- | --- | --- |
| E1 | 결과가 계획 확정인지 실제 도착인지 결정, 변화/비변화·성장 적용 여부 | 플레이어가 무엇이 확정됐는지 읽는 순간과 말 |
| E2 | 기존 명령/Adapter/원장 소유와 LocalProcess/RemoteHost의 같은 Core 경계 | 같은 ID/Revision의 구 요약·동네 상세 소비 경로 |
| E3 | 중복 입력·멱등성·Save/Replay·Local/Remote 동등성 범위 | 복원/지도 재진입 때 과거 사건 재연출 금지·취향 분리 |
| E4 | TriggerSource·Actor·기사·대상 공간·정책/profile·허용 상태 | 시안·VisualKey·기존 마커/대체 표현·선택/강조·복귀와 필요한 연구 |
| E5 | 승인된 권위 반영·행위 기록·성장 적용 또는 사유 있는 NotApplicable | 같은 Revision 상태 사본/행위 결과를 실제 객체/화면이 읽는 증거 |
| E6 | 가용 상태 경합·실패·후퇴·오래된 상태/중단·재개 | 실패 이유·대체 후보·긴급 처리 후 복귀·움직임 감소/기존 입력 회귀 |
| E7 | 실제 입력부터 결과·재조회·다음 선택·귀환의 계보 | 실제 Windows/Game View 완주와 두 지도 결과의 판독 |

Logic·Presentation·통합 E는 **미평가**다. Hosted 실제 연결·새 Scene·전국/세계 전체 구현을 이번에 검증한 것으로 쓰지 않는다. 공식 Scene 추가 없이 기존 기준을 따르고, 새 의미/표현 가정이 드러나면 가장 이른 책임 단계로 반환한다. 현재 회차를 통째로 바꾸는 Save Migration은 범위 밖이다.

### 반드시 계획에 넣을 사례

정상 확정/보류, 취소로 기존 상태 보존, 중복 확정, 이미 맡은 기사/가용 상태 경합, 미연결 목적지, 오래된 Revision, 구↔동네 재진입, 저장/복원, 긴급 처리 후 원래 업무 복귀를 구분한다. 계획 확인과 실제 이동/배달 완료를 별도 결과로 검증한다. 기존 주문 핀·효과 간단히·이름/투영·선택·커널 인계·저장 회귀의 영향 범위를 명시한다.

### 연구·시각·승인 상한

- 기존 지도/마커만 재사용하는지, 새로운 대기 거점·이동·Actor 표현을 추가하는지부터 구분한다. 공간/배치/동작이 플레이 경험을 좌우하면 기존 전문 심화 연구를 결속한다.
- 필수 연구의 `Required / NotRequired` 판정은 범위 결정 후 명시한다. 미판정을 빈 `requiredResearch: []`나 임의 `Accepted`로 바꾸지 않는다.
- 의미 있는 화면 변경은 현재 화면→검토 시안→확정 범위→실제 화면 대조의 기존 절차를 따른다. 이 문서의 표는 시안 승인 이미지가 아니다.
- 새 실제 이동/재배치/저장 기능은 이번 문서 통합으로 승인되지 않았다. 필요하면 첫 결과를 더 작게 나누어 기획에 반환한다.

## 수정 경로와 반환

지금 허용하는 새 산출물은 **구현 계획 검토 반환 문서**다. 반환 제안 경로는 `docs/AI/Handoffs/2026-11-13-Claude-r162-구지도구현계획-검토반환.md`다. 기존 수행자의 소스·원장·예약·Save를 수정하거나 이 문서를 제품 `writePaths`로 해석하지 않는다.

실제 코드 작업의 exact `writePaths`, `ownerThreadId`, WI/Ready 주체·E1~E7 정식 명세·연구/기획 hash는 아직 미결속이다. 공유되는 Gu/Map/View/Core의 쓰기 소유와 현재 편집/실행 소유를 정한 뒤에만 기존 [개발 인계 도구](../../Architecture/CodexPlayableLoopGoal운영체계.md#설계실제작실-인계)로 Export/Check한다. 임의의 새 Goal 원장이나 실행 JSON을 만들지 않는다.

반환에는 다음을 남긴다.

1. 대조한 branch/HEAD·관련 현재 파일/hash와 이전 입력의 차이.
2. 재사용/수정/신규/미연결의 연결표, 첫 직접 결과·제외 결과와 근거.
3. 정확한 파일별 쓰기 소유 후보, 기존 계약 영향·검증 명령과 완료 증거.
4. 필요한 연구/시각 검토·기획에 남는 선택 하나. 기존 코드/문서로 답할 수 있는 것은 먼저 해소.
5. 실제 수행한 문서/소스 대조와 미수행한 build/Runtime을 분리한 결과.

셸·hash/검증 도구가 없는 환경이라면 실행했다고 꾸미지 않고 확인 가능한 파일/범위와 재대조가 필요한 항목을 반환한다. 계획 검토를 위해 새 기능을 임시 구현하지 않는다.

## 같은 PC·Git 교대

이번 묶음은 같은 PC의 현재 미커밋 문서/소스 기준이다. 별도 PC/계정이 이를 읽을 수 있다고 가정하지 않는다. GitHub 교대는 사용자가 정한 저장소/브랜치에 관련 문서만 commit·push하고 정확한 remote commit을 검증한 후 별도 전달한다. 이번에는 commit·push하지 않았고 무관한 dirty 변경을 stage하지 않았다. 이 문서가 현재 HEAD에 포함돼 있다고 보고하지 않는다.

## 입력 고정

통합 정본 SHA256: `1134670b3f8e40d3b69f2a27654192de0ae7eaadad6fbe3ddc8448547bc823f4`

색인 JSON SHA256: `734cc34ba5f58037bea0e34f09fcc3cc29c24c599f5fde8c4203ab3333564a4b`

색인은23기획 원문·기존 반환·정본 및 별도 Unity의 B0/B1 관찰 지문을 보존한다. B1 변경의 소유/의미 대조 전 현재 코드와 B0 실행 근거가 동등하다고 해석하지 않는다. 이 문서/색인의 존재는 제품 코드 실행 승인·새 E 증거·실제 교대 수용을 뜻하지 않는다.
