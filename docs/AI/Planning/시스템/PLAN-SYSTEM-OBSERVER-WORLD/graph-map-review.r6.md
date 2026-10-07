# 사가정 음식 생활 Graph Map 영향 반환 r6

- 담당: Graph Map / 개발 통합에서 위임한 좁은 읽기·반환 작업
- 검토일: 2026-10-02
- 결과: `Blocked` — 공식 Graph 기계 인계 등록. 승인된 관찰 프로필의 제품 구현 차단과 구별한다.
- 영향 검토: `UpdateExisting` 후보. 신규 Graph/WI/Goal 자동 등록이나 전역 맵 이행은 하지 않았다.
- 사용자 선택 재질문: 없음. 남은 항목은 Graph 자료 형식·기존 원장 신선도·개발 결속 확인이다.

## 동결 원문과 소유권

원문은 [사가정 음식 생활 구현 명세 r6](sagajeong-food-life.implementation.r6.md)의 `Approved / ImplementationInProgress`다. 검토 시 원문 SHA-256은 `66ADE600633996FBE0405D59A381019906AC5CBBB3CFEE025440AAB4988E8D10`이다. 원문이 바뀌면 이 반환을 새 판본에 대해 다시 대조해야 한다.

현재 위임 트리에는 개발 통합·월드·공간·배치·이 Graph 담당이 있으며 다른 Graph 쓰기 담당은 없었다. Graph 경로의 사전 Git 상태에 기존 수정은 없었다. 이번 쓰기는 이 반환 문서 한 파일뿐이다. 제품 코드, 기획 본문, PLANNING, CURRENT_WORK, 기존 Graph 원본·생성물·도구·인계 원장을 수정하지 않았다. 별도 앱 스레드 전체를 조회하거나 메시지를 보내지는 않았다.

## 플레이 관계의 대조

| 칸 | 승인된 의미 | Graph 영향 |
| --- | --- | --- |
| 지금 | 명시 재생한 1800Tick 표본, 미접속 따라잡기 없음 | 기존 Session 시간·저장 경계를 유지 |
| 여기 | 사가정역 참고 지도 중 샐러리아 주변 | 실제 자료 참고 층과 가상 생활 표현을 분리 |
| 나 | 조망·NPC 선택 추적·정지·읽기 전용 설명의 관찰자 | 플레이 정책·주문 결과를 변경하는 엣지 없음 |
| 너 | 음식점 담당 1·기사 3·주민 2, 모두 가상 | 기존 주체 고유 식별자 6개 재사용 |
| 이렇게 | 기존 Core 주문·조리·배차·픽업·전달·수령·복귀 | 기존 WI 결과를 판독하는 표현 결속 |
| 결과 | 같은 Session revision의 상태·대기 사유 | 표현/카메라 성공을 업무 성공으로 변환하지 않음 |
| 다음 선택 | 관찰 대상·카메라·정지·저장·완료 후 새 표본 | 관찰 입력과 저장 생명주기를 유지 |

이 slice는 기존 가상 생활의 **관찰·표현 보조 층**이다. 주인공의 회복·기여 자원·상대 필요에 대한 새로운 핵심 진행 엣지를 만들지 않는다. 역사 메뉴·문화 설명은 과거 기록 문맥이고 NPC의 현재 주문·직원 신상·국적·감정이 아니다.

## 레벨 1 — 재사용과 아직 없는 결속

기존 [합성 동네 Graph](../../../../../eng/world-seedbeds/graph-maps/synthetic-neighborhood.v1.json)는 `graph-map:synthetic-neighborhood.v1` / `synthetic-neighborhood.r1`이며 SHA-256은 `75262BE34CD581B25D398C0FF4FB014BF7B38B4F2E3A8B1195D66C46CF790D7A`다.

- 주체: `actor:sim.restaurant-1`, `actor:synthetic-courier:1`, `actor:synthetic-courier:2`, `actor:synthetic-courier:3`, `participant:synthetic:a`, `participant:synthetic:b`.
- 직접 시설: `facility:sim.restaurant-1`, `facility:synthetic:a`, `facility:synthetic:b`, `facility:synthetic:courier-base`.
- 기존 관계: `relation:work:actor:sim.restaurant-1`, `relation:work:actor:synthetic-courier:1/2/3`, `relation:work:participant:synthetic:a/b`, `relation:order:a:facility:sim.restaurant-1`, `relation:order:b:facility:sim.restaurant-1`.
- 1-hop 경계: 기사 거점의 `relation:courier-base-location`은 `facility:synthetic:mart`를 참조한다. 기존 마트·창고 생활을 삭제하거나 샐러리아의 실제 물류라고 바꾸지 않는다. 2-hop 보충 화물·다른 Area로 새 인과를 전파하지 않는다.

요청의 `WI-CITY-SYNTHETIC-MOVE/PICKUP/DELIVER/RECEIVE/RETURN`, `WI-CITY-RESTAURANT-ACCEPT/COOK` **7개는 [공식 WI 대장](../../../../../eng/execution-ledgers/world-interactions.json)에 모두 존재한다.** 다만 위 합성 Graph의 `edge.wiRefs`에는 이 7개가 직접 결속되어 있지 않다. 기존 `OrdersAndReceives` 관계의 `wiRefs`도 빈 배열이다. 따라서 이 검토는 기존 ID와 Core 재사용 가능성을 확인한 것이며, 이미 7개 WI를 Graph에 통합했다고 보고하지 않는다. 대장의 과거 `NotStarted/E0`도 이번 검토로 변경하지 않는다.

공개 상호·주소·건물 수준 좌표는 기존 Simulation 시설의 정체를 실제 영업체로 바꾸는 권위가 아니다. 실제 지점 문맥은 별도 표현 Binding과 역사 기록 참조로 연결한다. 이 작업에서 신규 식당 Actor·고객·배달 WI·Goal을 만들지 않는다.

## 레벨 2 — 개발에 전달할 제약

| 제약 | 집행 책임과 확인 상한 |
| --- | --- |
| 자료 판본과 좌표계 | `sagajeong-reference.r3`, 아래 동결 자료 hash, WGS84-ECEF-ENU·영점 고도·원점 37.5806971/127.0884106·offset(550,8)을 Binding에서 검사 |
| 실제와 게임용 구분 | 지점 좌표 37.5802023/127.0892977은 건물 수준 참고. 접근점·주민 집·이동 경로는 게임용이며 실제 출입/주차/고객 경로가 아님 |
| 경로 연속성과 자료 부재 | 개발 소유 Binding의 연결·유한값·경로 판본 검사. 누락/불일치면 시작 차단. 임의 인근 위치나 합성 원점으로 fallback하지 않음 |
| Core와 표현 | Core의 합성 점유·업무 경로와 지도 위 표시 경로를 구분. 카메라·표시 도착이 명령 완료·점유 권한·통행 권위가 되지 않음 |
| 준비와 실제 배치 | 자료 준비·코드 검사는 실제 Renderer/Collider·통행·Game View 완료가 아님. 해당 증거는 월드 담당 반환으로 별도 판정 |
| 정확 수치 | 도로 폭·건물 높이·출입구·주차·경사·보행 수치의 새 실측값을 만들지 않음. 기존 250m 창을 이 매장 부지로 간주하지 않음 |
| 외관 교체 | 실사 외관/얼굴은 사용하지 않음. 외관 표현 객체를 중립 표식으로 교체할 수 있도록 이름·역사 기록과 분리 |
| 권위 경계 | 기존 자료의 ReferenceOnly/NoTraversal/NoOperational을 유지. 운영·실제 통행·공개 출시·E 자동 승격 없음 |

참고 층은 기존 [면목 참고 Graph v2](../../../../../eng/world-seedbeds/graph-maps/jungnang-myeonmok-reference.v2.json)의 `reference:sagajeong-station`, `layer:myeonmok:osm-buildings`, `layer:myeonmok:osm-roads`, `scenario:synthetic-neighborhood`를 대조했다. 이 파일의 SHA-256은 `12EAD7FA394F732552DC5EE3CD95716065560F5EE8D7B0880892DCF1F6441681`이다. 기존 `SourceGeometryNotNavigation`과 `ScenarioOnly` 의미를 보존한다.

위 표는 원문이 승인한 제약의 **전달**이다. 새로운 절대 좌표·배치 인스턴스·통행 엣지나 선택된 `placementRuleRefs`를 등록한 결과가 아니다. 구체 게임용 접근점·경로·건물 결속은 월드 담당의 `SagajeongFoodLifeBinding.json`을 현재 hash로 검토한 뒤 후속으로 결속해야 한다.

## 레벨 3 — 읽은 코드와 결속 상한

| 현재 읽은 소스 | 역할·심볼 | 판정 |
| --- | --- | --- |
| [가상동네생활.cs](../../../../../Ssalddel.Simulation.Domain/UnityPackage/Runtime/가상동네생활.cs) | `경영SimulationSessionAggregate`의 기존 생활 결과 | 기존 권위 Core 재사용, 규칙 수정 없음 |
| [동네관찰Presenter.cs](../../../../../Ssalddel.Unity/Runtime/Observation/동네관찰Presenter.cs) | `동네관찰Presenter.생성`, `동네관찰ScreenModel` | 같은 snapshot의 읽기 전용 모델 |
| [동네관찰SessionController.cs](../../../../../Ssalddel.Unity/Runtime/Observation/동네관찰SessionController.cs) | `동네관찰SessionController` | 명시 재생·정지·저장·완료·회복 경계 |
| [음식배달관찰경로.cs](../../../../../Ssalddel.Unity/Presentation/음식배달관찰경로.cs) | `음식배달관찰경로`, `음식배달관찰동선` | 표시 경로 후보·연속성 검사, 실제 통행 권위 아님 |
| [사가정음식생활관찰Profile.cs](../../../../../Ssalddel.Unity/Runtime/Observation/사가정음식생활관찰Profile.cs) | `사가정음식생활관찰Profile.생성/조회/관찰명령허용` | 개발 중 소스 존재·선언 확인. 최종 hash·시험은 개발 통합 반환 필요 |

위 코드의 기존 안정 범위에서 관측한 SHA-256:

- Core 생활: `E7A04BE8538D78A17DF19339EB2E0CA31E458152395CBA8FC7DC49E2C6EBBBAE`
- Session Controller: `AC59F72F55CC7DA25ECC66FF922406477B0D44055729952B0C383421E5B2B62F`
- 표시 경로: `B7916B084456FFB0DFA360336F1306D3E963D4B198557DA35C02ABDBAC21C5CC`
- Unity 참고 지도 `Assets/Ssalddel/Resources/SagajeongReference.json`: `4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3` — 원문과 일치.

별도 Unity 저장소의 현재 `사가정공간참고View.cs` (`사가정참고조립`), `가상동네생활View.cs` (`BuildNeighborhoodLife/RenderNeighborhoodLife`), `동네관찰휴대폰View.cs`, `동네관찰RuntimeAdapter.cs`를 기존 진입점으로 확인했다. 이 소스들은 개발·월드 담당이 수정 중이므로 최종 소스 hash를 고정한 공용 `gm-code:*` 결속으로 등록하지 않았다.

검토 시 새 `사가정음식생활공간Binding.cs`, `사가정음식생활View.cs`, `SagajeongFoodLifeBinding.json`은 아직 없었다. 이는 현재 동시 구현의 산출물 대기이며 플레이 기획 공백이 아니다. 해당 파일·심볼·시험이 반환되면 코드 결속을 다시 대조한다. 최종 코드 결속을 등록하지 않은 상태에서 `SceneBound`, `RuntimeObserved`, `ReadyForDevelopment`로 올리지 않는다.

## 공식 등록 차단과 검증

[기획 인계 관리 도구](../../../../../eng/world-seedbeds/manage-graph-map-planning-handoffs.ps1)의 `Integrated`는 `graphMapStableId`, 생성 결과의 `sourcePlanHashSha256`, 생성 `plan`의 고유 식별자·판본 및 보고서 hash를 요구한다. 기존 합성·면목 Graph는 `simulation-world-graph-map.v1`과 `graphStableId`이며, [전역 Graph 관리자](../../../../../eng/world-seedbeds/manage-graph-map-plans.ps1)는 `mirror-graph-map-plan.v3`를 검사한다. 형식이 다른 기존 합성 맵을 억지로 전역 생성기에 편입하지 않는 기존 관리자의 경계도 보존한다.

| 실행한 읽기 전용 점검 | 결과 |
| --- | --- |
| `pwsh -NoProfile -File eng/world-seedbeds/manage-neighborhood-maps.ps1 -Mode Check` | 통과. 19노드·20관계·9인스턴스·44기준점·14경로·9주체. `SceneReady=false`; 기존 `mart-inbound` 외부 기준점 경고 및 3개 준비 공백 유지 |
| `pwsh -NoProfile -File eng/world-seedbeds/manage-graph-map-plans.ps1 -Mode Check` | 기존 전역 기준선 차단: `GraphMapInvalid:WiCatalogRevision` |
| `pwsh -NoProfile -File eng/world-seedbeds/manage-graph-map-planning-handoffs.ps1 -Mode Check` | 기존 r13 인계 차단: `GraphMapHandoffInvalid:PlanningSource:graph-map-handoff:northern-life-hub-discovery:r13:HashMismatch` |
| 공식 WI와 합성 엣지 대조 | 요청 WI 7/7 존재, 해당 7개의 기존 엣지 직접 결속 0개 |

이 반환 문서 한 경로에 `validate-changes.ps1 -Level Fast`와 `-Level Task`를 실행해 통과했고, 로컬 링크 11개 존재 및 `git diff --check`를 확인했다. 문서 검사이므로 제품 build/test는 실행하지 않았다. 로그는 각각 `artifacts/local/validation/20261002-173616`, `artifacts/local/validation/20261002-173647`이다.

처음 Windows PowerShell 5로 실행한 두 Graph 도구는 UTF-8 해석 문제로 경로/구문 오류가 나서 bundled `pwsh`로 다시 실행했다. 위 표는 올바른 UTF-8 실행 결과다. 인코딩 오류를 Graph 구조 오류로 해석하지 않았다.

공식 등록의 가장 이른 책임은 Graph 자료 형식·기계 인계 신선도다. 현재 승인된 명세의 동결 원문과 좁은 구현을 보존하며, 다른 전역 계획 59건의 hash나 WI revision을 임의 갱신하지 않았다. 기존 Goal·Approved 관문·작업 명세를 선택해 새 `ReadyForDevelopment` 인계를 만들지 않았다. 개발 통합이 진행하는 승인된 좁은 구현과 별개로 다음 Graph 작업에서 해당 형식의 명시적 호환/등록 범위를 정해야 한다.

## 반환과 제외

- 반영 완료: 동결 기획의 레벨1 재사용·직접/인접 경계, 레벨2 참고/게임용 제약, 레벨3 현재 코드 진입점의 검토·이 문서 반환.
- 미반영: Graph JSON·생성물·공식 인계 등록, 새 배치 좌표·규칙, 최종 새 코드 hash 결속.
- 후속 기술 검토: 월드 Binding의 실제 고유 식별자·경로·자료 hash, 개발 프로필의 최종 소스·시험, 호환 Graph 형식의 좁은 등록 방법.
- 증거 상한: Graph와 소스의 읽기 전용 검토. 실제 Scene/Prefab 변경·Runtime·Game View·실제 입력·Save/Replay 시험·서버 연결·실제 통행·E 승격을 하지 않았다.
- commit·push 없음. 새 디오라마 규칙 후보 없음.
