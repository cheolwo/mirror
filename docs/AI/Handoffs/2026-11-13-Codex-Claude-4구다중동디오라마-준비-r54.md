# Codex → Claude · 4구 다중 동 디오라마 시안·개발 준비 r54

- 기준일/역할: 2026-11-13 · Codex 기획 작업실.
- 상태: `UserScopeRecorded / ExistingVisualsReviewed / PlanningInputPrepared / DevelopmentAcceptancePending / ProductUnchanged`.
- 정본: [PLAN-SYSTEM-ADMIN-DONG-DIORAMA r54](../Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/four-district-modular-diorama.proposal.r54.md).
- 정본 SHA256: `7727a2a57f7a97558470f16f89b42bb3c25b6a04cd843e87f45a65d5a9f6fe24`.
- 사용자 방향: 중랑구·광진구·동대문구·노원구부터 여러 동의 독립 디오라마를 구성해 각 동을 보고 경영 판단한다. 기존 결정 순회/가맹점 구현 소유와 공통 시간·업무·입력 보존은 유지한다.

## 확인한 현재 상태

면목본동/면목2동의 [최신 실행판 반환](2026-11-13-Claude-결정순회뼈대-반환.md) 캡처와 결정 순회 시안을 직접 대조했다. 두 정거장은 같은 동네 지형을 사용한다. 순회 버튼을 다시 만드는 과제가 아니라 **그 정거장의 지역 ID로 자료/표현 모듈을 선택하는 연결**이 필요하다.

기존 [r2 scope](../../../eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider.r2.json)의 모듈30개는 중랑16·광진 중곡4·동대문 일부10이며 노원은 포함하지 않는다. 역사 경계 후보이고 통행/게임/배포 플래그가 false다. 이를 현재 네 구 전체나 바로 사용할 Windows 자료로 보고하지 않는다.

현재 확인한 소비판은 별도 Unity 작업본 `C:/Users/user/ssalddel`, `unity/workstream-transition@605e87ef46b2ebd3b19f65ebd9d650e0ccf44d77`의 dirty `Assets/Ssalddel/WindowsGame`이다. 이 경로는 이번 대조 위치이며 고정 배포 경로가 아니다. `Runtime/SagajeongDispatchDiorama.cs`는 `hongdal-diorama`, `Runtime/SagajeongDispatchBuildings.cs`는 `hongdal-buildings` 고정 Resource를 읽는다. 실제 수용 전 현행 파일/지문·소유를 다시 확인한다.

## 다음 시안에 필요한 비교 장면

1. **서로 다른 두 동:** 같은 패널·회전/축척 원칙에서 면목본동/면목2동의 실제 각 공간을 비교한다. 이름·점선·가상 매장만 바꾸는 비교가 되지 않도록 자료 ID/판본·사용 경계를 표시한다. 자료가 준비되지 않았으면 준비 상태를 보여주고 다른 동 바탕으로 대신하지 않는다.
2. **네 구 목록/준비도:** 구→동 목록에서 기존 모듈·추가 대상·자료 결손/검토 중·현재 보기/통행 가능 상태를 구별한다. 검증하지 않은 현행 동 개수를 확정 표시하지 않는다.
3. **전환/실패/복귀:** 다음 동을 불러오는 동안 현재 보이는 지역과 선택 대상을 구별하고, 실패하면 마지막 정상 화면/업무를 보존한다. 같은 동 복귀 시 카메라·건물 선택/입력을 이어 본다.

이것은 다음 시안의 입력이며 이번에 새 PNG·모델·Game View를 만들었다는 뜻이 아니다. 캡처·합성 시안·실제 Unity 입력/배치 증거를 구별한다.

## 개발 수용에서 결속할 것

- 기존 행정동 manifest→500m 타일/overlay와 현재 HDIO·건물 색인의 최소 Adapter. 새 Core/Scene/타일 프레임워크 없이 기존 읽기/해석/표현 경계를 재사용한다.
- 네 구 현행 목록/코드와 별도 scope 후보 revision, [r16](../Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/current-boundary-building-entrance-collection-gate.decision.r16.md)의 현행 경계/같은 세대 건물·출입구 패턴과의 결속. 기존 r2/r16 기계 원본은 보존한다.
- 공통 ENU·단위·건물 단일 귀속·인접 격자/경계 도로·원본 고유 ID·자료 판본. [기존 Interpreter](../../../Ssalddel.Unity/Runtime/WorldProjection/AdministrativeDongDioramaInterpreter.cs)의 행정동별 타일 지역 ID·projection/hash와 선택 manifest의 일치 검사를 유지한다. 공통 원본/격자를 재사용하는 것과 한 동의 타일 payload를 다른 동에 넣는 것을 구별한다. 실제 자료와 합성/추정 표현을 구별한다.
- 표현 자원 로드/해제와 Core/Kernel 업무 수명을 분리한다. 지역 보기 성공을 그 지역의 배달/주행·정산 완료로 확대하지 않는다.
- 정본7절의 자동/실제 화면 검증·명확한 쓰기 경로/소유·WI/수직 검증 명세와 소비판 반환을 결속한다.

기획/자료/코드 존재만으로 `ReadyToDispatch`·현재 게시/배포·통행·Gameplay·E를 승격하지 않는다. 이번 문서는 실행 원장/수집 예약·자료 current·제품 코드·현재 수행자의 소유를 변경하지 않는다.
