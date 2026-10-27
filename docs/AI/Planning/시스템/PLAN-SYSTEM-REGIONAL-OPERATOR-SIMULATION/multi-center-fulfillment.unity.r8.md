# [구현 · 다중 센터 출고 Unity 화면 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · r8]

2026-10-25. `Approved / LocalUnityPlayValidated / DedicatedSaveRestoreValidated / FurtherUnityWorkDeferredByUser / MobileDesktopAppsPriority / OperationalDisabled`.

최신 사용자 정정으로 Unity의 추가 구현·실행·시각 보완을 여기서 보류한다. 휴대폰 앱과 데스크톱 앱에 집중하며, 아래의 이미 작성한 소스·저장·실행 근거는 보존한다. 마지막 진행 중이던 EditMode 9개도 모두 통과하고 종료된 것을 확인했다.

- 승인: 사용자가 Unity를 켜 두었으니 이어서 작업·실행해 보도록 직접 요청했다. [r7 출고 코어](multi-center-fulfillment.implementation.r7.md)의 실제 View/장면 연결과 플레이 검증을 이번 범위에 추가한다.
- 주책임·담당: Unity 표현·입력, 통합 담당 `01a0c8bc-d06d-7181-aa71-c34dd9986f98`. 기존 Simulation Session이 상태 권위다. 기존 Goal·WI·Scene을 자동 생성/활성화하거나 운영 출고를 실행하지 않는다.
- 공식 Scene: 기존 `Assets/Ssalddel/Scenes/SimulationWorldShell.unity`. 다른 공식 Scene을 만들거나 기존 Scene/Prefab을 덮어쓰지 않고 명시 프로필에서 런타임 자식으로 조립한다.
- 주체·관계: 모의 센터·적재 위치·담당자·주문을 r7의 안정 ID로 연결한다. 기존 진부 Hub나 실재 센터의 선반으로 임의 배정하지 않는다.
- 연구·공간 경계: `requiredResearch: []`. 이번 모형은 합성 센터/선반의 단순 표시다. 실제 도로·영상·건물·보관 여건을 검증한 디오라마로 승격하지 않는다.

## 플레이 범위

전용 메뉴로 시작 → 출고 표본/전용 슬롯 읽기 → 센터 후보·불가 사유 확인 → 센터/분할 조건 선택 → 주문 예약 → 선반/상품/수량 입력과 부분 피킹 → 남은 수량 조회 → 피킹 충족·포장 준비 확인 → 전용 슬롯 저장·복원.

중복 클릭·오래된 판본·위치/상품 불일치·불충분 수량은 코어가 거절하고 화면은 현재 원장을 다시 읽는다. 결과가 불명확한 POST를 새 명령 ID로 자동 반복하지 않는다. 모형 강조·직원/상품 표현이 예약·피킹 완료를 직접 만들지 않는다.

## 보존·수정 경로

- Unity checkout `C:/Users/user/ssalddel`의 기존 `unity/workstream-transition`과 dirty 작업을 보존한다. 미추적 RoadReviewGame·사용자 Scene·기존 저장·기본 Nature/음식점 프로필은 초기화하지 않는다.
- `Assets/Ssalddel/Infrastructure/Simulation/SimulationWorldLocalRuntimeScope.cs`: 새 enum 값 11·요청 flag·전용 슬롯·표본 생성 분기. Configure는 새 세션에서만 한 번 실행하고 복원 세션은 시나리오/모듈을 검증한다.
- `Assets/Ssalddel/Bootstrap/음식점관찰AutoBootstrap.cs`: 새 출고 프로필에서 음식점 UI를 중복 설치하지 않는 guard만 추가.
- 새 `Assets/Ssalddel/Bootstrap/다중센터출고AutoBootstrap.cs`, `다중센터출고SceneController.cs`, 새 `Assets/Ssalddel/Presentation/World/다중센터출고View.cs`, `다중센터출고선반Binding.cs`, 새 `Assets/Ssalddel/Editor/다중센터출고진입.cs`와 각 `.meta`.
- 새 EditMode 시험·검증 산출물과 `Documentation/Changes/2026-10-25-multi-center-fulfillment-r8/` 기록. 공유 Material은 바꾸지 않고 생성한 모형/Material만 소유한다.
- `관찰표현LayerContract`에 출고 표시 layer 27을 추가했다. 지구본/사가정 표시의 layer 30과 분리하고 출고 전용 카메라를 사용한다. 전용 실행 동안 기존 관찰 View·Canvas·카메라만 잠시 쉬게 하며 종료 때 복원한다. 실제 Play에서 기존 지구본이 화면을 덮는 충돌을 확인한 뒤 이 격리를 추가했다.
- 기존 미추적 RoadReviewGame의 시험 asmdef에 명시 `Newtonsoft.Json.dll`·`nunit.framework.dll` 참조를 추가해 컴파일 차단을 해소했다. 원래 파일 사본을 산출물에 보존했으며 시험 소스를 삭제/비활성화하지 않았다.
- 컴파일 차단이 있으면 원인을 먼저 확인하고 필요한 최소 참조/소비 호환만 고친다. 다른 기능·시험을 삭제하거나 비활성화하지 않는다.
- 상위 README·PLANNING·CURRENT_WORK와 이 문서가 진행/검증 결과를 소유한다. 코드·Editor 컴파일·EditMode·Play Mode·실제 클릭·Game View를 구분한다.

## 실행·검증 관문

현재 Editor·연결·컴파일·활성 Scene/dirty 상태를 먼저 확인한다. Editor가 연결되면 Unity Pipeline/CLI로 상태·Console·Scene·Play를 확인하고 UI 조작은 필요시 computer-use로 실제 화면을 대조한다. 기존 Scene에 저장하지 않고 실행 중 객체만 생성한다. 현재 Scene의 미저장 변경이 있으면 조용히 폐기하지 않는다.

새 프로필·저장 격리·버튼/코어 왕복·모형 stable ID 대응과 실패 후 상태 보존을 EditMode로 확인한다. 이후 같은 판본으로 실제 Play Mode에서 후보→예약→부분 피킹→포장 준비를 수행하고 화면과 Console을 확인한다. 카메라만 렌더한 PNG를 Overlay UI 전체 캡처와 혼동하지 않는다.

## 현재 결과

착수 시 Unity Hub만 열려 있고 Pipeline 연결 Editor는 0개였다. 라이선스 활성 상태를 확인한 뒤 설치된 프로젝트 버전 6000.5.6f1로 Editor를 열었다. 기존 RoadReviewGame의 Newtonsoft/JObject 참조를 복구하고 대량 Synty 자산 가져오기를 마친 뒤 정상 Editor와 Pipeline 연결을 확인했다.

- 실제 EditMode: 새 출고 UI/프로필 9개, 기존 Nature Runtime 15개, 기존 표시 Layer 2개, 참조를 보완한 RoadReview 계약 20개가 각각 통과했다. 합계 46개이며 r7 .NET 시험 수와 합산하지 않는다. 초기 UI 실패 두 판본도 보존했다. native MaterialPropertyBlock 필드 초기화·EditMode Toggle 배치 콜백·UI 앵커·예약 중 센터 변경을 수정하고 재검증했다.
- 실제 Play: 사용 화면 버튼으로 주문 검토→출고 예약→1개 피킹→진행 저장, Play 종료/재시작→같은 세션 revision 3의 피킹 1/잔여 2 복구, 수량 2 입력→전량 피킹→포장 준비→저장을 확인했다. 보관/예약은 10/3→9/2→7/0으로 변했다. 이후 재시작에서도 같은 세션 revision 5·`PackReady`를 확인했다. 일반 Scene은 dirty=false였고 종료 때 출고 flag가 null·기존 지구본 View가 다시 enabled=true로 돌아왔다.
- Game View: 실제 Overlay UI를 포함한 `ScreenCapture.CaptureScreenshot` 결과를 시각 확인했다. `Documentation/Changes/2026-10-25-multi-center-fulfillment-r8/출고-포장준비.png`와 `플레이안내.md`가 Unity checkout에 있다. Camera.Render만으로 만든 화면이 아니다. 최종 Play의 Console Error는 0개다.
- 근거: Unity checkout `artifacts/multi-center-fulfillment-r8/`의 EditMode 결과·`play-partial.json`·`play-restored.json`·`play-pack-ready.json`·`play-completed-restored.json`·`editor-restored.json`·`console-errors.json`. 원본 canonical Scene 파일은 수정하지 않았다. 후속 통합 manifest 정리는 사용자 보류로 실행하지 않았다.

이번은 가상 센터 선택→선반 피킹의 첫 직접 플레이 범위다. 고용/임대·공급 계약 체험, 입고/적치, 실제 포장/기사 인계/배송·손익/보수 균형, 실재 지역/주행영상 결속과 학습 환류까지 완성한 게임이 아니다. 확정 예약의 센터 전환·피킹 후 반납/복구도 후속이다. 운영 DB·실물 바코드·실제 고용/임대/운송·학습·게시·commit/push는 실행하지 않았다.
