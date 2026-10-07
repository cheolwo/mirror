# 사가정 샐러리아 음식 생활 관찰 구현 명세

- 기획 소유: `PLAN-SYSTEM-OBSERVER-WORLD` / 좁은 후속 `sagajeong-food-life.r6`
- 상태: `Approved / ImplementationInProgress`; 일반 관찰 세계 r5의 제어 정책은 보존한다.
- 승인: 2026-10-02 사용자가 관찰 중심, 실제 사가정 공간, 가상 주민 자율 생활, 음식점 1곳, 샐러리아 사가정점, 게임용 접근점을 선택하고 `Implement the proposed plan` 및 `구현`을 요청했다.

## 플레이 약속과 경계

| 항목 | 승인 범위 |
| --- | --- |
| 지금 | 사용자가 재생한 30분 표본, 미접속 시간 따라잡기 없음 |
| 여기 | 기존 사가정역 1km 참고 지도 중 샐러리아 주변 |
| 나 | 전체 조망·선택 NPC 추적·일시정지·읽기 전용 설명을 사용하는 관찰자 |
| 너 | 음식점 담당 1, 기사 3, 주민 2. 전원 가상 인물 |
| 이렇게 | 기존 Core의 주문→수락/조리→배차→픽업→전달→수령→복귀/재주문을 표시 |
| 결과 | 같은 Session revision의 상태와 대기 사유를 판독 |
| 다음 선택 | 관찰 대상/카메라 변경, 명시적 정지·저장·완료 뒤 새 표본 |

실제 상호는 `샐러리아 샌드위치&샐러드&포케 사가정점`, 공개 주소는 `서울 중랑구 용마산로51길 50 1층`, 기록 좌표는 위도 37.5802023 / 경도 127.0892977이다. 2026-09-28 방문 기록의 메뉴를 역사 설명으로만 사용한다. 현재 NPC 주문의 기존 감자 스튜 메뉴를 실제 주문 메뉴로 바꾸어 표시하지 않는다. 실제 직원·고객의 국적·감정·행동을 추론하지 않는다.

건물 수준 위치와 게임용 접근/주거/이동점을 분리한다. 기존 `SagajeongReference.json`의 `sagajeong-reference.r3`, SHA-256 `4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3`, WGS84-ECEF-ENU 영점 고도, 원점 37.5806971 / 127.0884106, offset (550,8)을 유지한다. 도로·건물 자료의 ReferenceOnly/NoTraversal/NoOperational 권위를 승격하지 않는다. 실제 진입·주차/폭/높이 정밀화는 이번 범위가 아니다. 기존 250m 집중 창을 이 매장 부지로 간주하지 않는다.

## 구현과 쓰기 소유

- 기존 단일 `SimulationWorldShell`, `CreateNeighborhoodLife(dayEnabled:true)`, 공통 Local Runtime를 재사용한다. 서버 운영 API/DB, Simulation Core 규칙·계약은 수정하지 않는다.
- 새 명시 프로필 `SagajeongFoodLife = 10`, 선택 환경변수 `SSALDDEL_SAGAJEONG_FOOD_LIFE=1`, 전용 저장 슬롯 `sagajeong-food-life-r1-primary`를 사용한다. 기존 프로필 5와 저장을 보존한다.
- 개발 통합 소유: shared `Ssalddel.Unity/Runtime/Observation/사가정음식생활관찰Profile.cs`, 관련 패키지 시험, Unity RuntimeScope/기존 Controller의 분기·휴대폰 Binding/View·RuntimeAdapter, 관찰 진입 Editor 도구, 이 문서/PLANNING/CURRENT_WORK/변경 기록.
- 월드·공간·배치 소유: Unity 새 `Runtime/World/사가정음식생활공간Binding.cs`, `Bootstrap/사가정음식생활View.cs`, `Resources/SagajeongFoodLifeBinding.json`, 관련 새 EditMode 시험과 meta. 기존 형상/다른 dirty 파일은 수정하지 않는다. 이후 독점 Editor 실행·Game View 검증을 인계한다.
- 월드 접점: Controller partial의 `사가정음식생활준비()`는 저장 설치 전 자료를 검증하며 실패 시 시작을 거부한다. `BuildSagajeongFoodLife()`, `RenderSagajeongFoodLife()`는 기존 state/fleetActors/fleetVehicles/fleetParcels/생활주체/observer/materials/참고Meshes를 재사용한다. `사가정음식생활카메라갱신()` 및 `사가정음식생활시설관찰(string)`은 카메라만 변경한다. 프로필 판정은 `SimulationWorldLocalRuntimeScope.SagajeongFoodLifeRequested`.
- Graph Map 담당은 승인 문서 SHA와 기존 WI를 읽고 레벨1 플레이 관계·레벨2 게임용 공간 제약·레벨3 코드 결속 영향만 판정/반환한다. 개발 통합은 Graph 원본/생성물을 직접 수정하지 않는다.
- 실사 외관/얼굴 자산은 넣지 않는다. 외관 표현은 독립 객체/설정으로 중립 표식 교체 가능하게 하며 이름·역사 기록 삭제와 구분한다.

## E1~E7 상호작용 수직 검증 범위

대표 재사용 WI는 `WI-CITY-SYNTHETIC-MOVE/PICKUP/DELIVER/RECEIVE/RETURN`와 `WI-CITY-RESTAURANT-ACCEPT/COOK`다. 신규 WI/Goal을 자동 등록하거나 E를 승격하지 않는다. Logic E7→E1 영향은 기존 Core 규칙 보존과 별도 슬롯·재시작·Save/Replay 호환이다. Presentation E7→E1 영향은 실제 지도 판본/좌표·게임용 경로 연속성·상태 판독·관찰 전용 입력이다.

| 단계 | 확인할 증거 |
| --- | --- |
| E1 | 승인된 6인 음식 생활과 실제/가상 구별, 정책 개입 없음 |
| E2 | 자료 hash/좌표계/주소 건물·접근점/경로 연결 검사, 없으면 차단 |
| E3 | 순수 투영 불변성·저장 재진입·결정성·완료/재시작·저장 실패 회복 |
| E4 | canonical Scene에서 명시 프로필/isolated slot와 읽기 전용 카메라·카드 |
| E5 | 최소 300Tick의 Core 수령·복귀 상태와 같은 revision의 표시 |
| E6 | 두 주민 각각 음식 수령 2회 이상, 복귀/재주문 및 대기 사유 판독 |
| E7 | 실제 Play Mode·Game View, 관찰 입력/카메라/설명·배치 확인. 자동 함수 호출과 물리 입력을 구별 |

기존 동네관찰 Controller의 일시정지·직렬화·저장 실패 차단을 재사용한다. 새 모드에는 정책·운영지도 전환을 노출/실행하지 않는다. 숨겨진 정책 호출도 거부한다. 원음/영상 편집·MYBOX·실제 정산/운영 주문은 이 게임 범위에 포함되지 않는다.

## 검증과 반환

개발: focused dotnet 및 Unity EditMode, 좁은 `validate-changes.ps1 -Level Fast/Task -Paths ...`, diff 검사. 월드 담당: Editor 소유권 확인, 새 슬롯 최소300Tick, 실제 Game View/Console/입력 절차, 기존 Scene/슬롯 보존. 이전 ReplayHashMismatch를 현재 결과로 가정하지 않고 재검증한다. 하지 못한 증거는 별도로 보류하며 구현/시험/실제 화면/수동 입력을 구분한다.

새 디오라마 규칙 후보는 종료 시 기존 규칙과 대조한다. 이번 자료의 준비 통과는 형상 정밀화·실제 통행·공개 출시 승인이 아니다.
