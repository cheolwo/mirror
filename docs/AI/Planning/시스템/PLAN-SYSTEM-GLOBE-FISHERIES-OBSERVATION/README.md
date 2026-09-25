# [기획 · 지구본·수산물 관찰 · PLAN-SYSTEM-GLOBE-FISHERIES-OBSERVATION · r29]

생활상 확장: [새벽 식자재 인수 r29](dawn-restaurant-supply.scope.r29.md). 기존 입고·출고화물 인계·음식점 조리 표본 코드 조사, 첫 음식점 인수 범위 정리. 방향 승인·명세 준비이며 코드/장면 구현 완료 아님. 표현 기준선은 아래 r28 유지.

선택 현행: [행정동 디오라마 진입 r28](dong-diorama-entry.implementation.r28.md). 면목7동→사가정 역세권 일부 보기, 미준비 동 안내, 지도 시점 복원. 시험9/9·실제 Play 함수 호출/캡처. 물리 입력·빌드는 별도다.

현행 확장: [4개 구 행정동 r27](four-district-preview.implementation.r27.md). 중랑구16·동대문구14·광진구15·노원구19, 역사 자료64동. 선택별 확대·Play 캡처, 계약 시험7/7. 상세 건물 디오라마/최신 경계/배포는 별도다.

표현 현행: [서울·중랑구 중간 공간 r26](seoul-hierarchy-preview.implementation.r26.md). 2023년 경계 25구+16동의 로컬 Unity 표현과 사가정 진입·복귀, 실제 Game View 확인. EditMode 신규 4/4·기존 22/22. 최신 경계·정식 배포·연속 지형 전환 완료 아님. 아래 r25는 수집 시점 이력이다.

자료 현행: [공간 자료 재고·서울 경계 보충 r25](spatial-data-inventory.collection.r25.md). 기존 자산을 보존하고 2023년 서울 경계 원본2개를 확보·DB 계보 등록했다. 도형 검증·Unity 적용은 후속이다. [공급자 조사 r23](map-provider-research.r23.md)·[VWorld 점검 r24](vworld-connection-check.r24.md)는 이력으로 보존하며 VWorld는 필수 경로가 아니다.

후속 설계: [공간 확대 계층 r22](spatial-zoom-hierarchy.r22.md) — 수도권·서울·중랑구의 실질적인 중간 지도 표현을 준비한다. 소스 조사·설계 기록이며 구현 완료 아님. 아래 r21 구현은 유지한다.

표현 현행: [사가정 진입·복귀 r21](globe-diorama-transition.r21.md). 준비된 기존 디오라마에 짧은 확대·페이드로 전환하고 복귀 시점을 보존한다. [움직임 우선 r20](animation-first-preview.r20.md)을 보존하며, 연속 지형 변형·모든 지역 스트리밍은 미구현이다.

조회 현행: [HS 중심 자료 연결 r19](hs-centered-query.implementation.r19.md). 저장 HS408개를 H6·2025년 한국 수입 기준으로 묶고 Unity 검색·대표품목·국가 관계선을 확인했다. 기존 운송자료를 품목 운송량으로 해석하지 않는다.

표시 교정: [기울기·자전축 r18](axial-tilt.correction.r18.md). 북극 오른쪽 23.5도 표시·로컬축 자전, 지구본 시험 22/22·실제 Game View 확인.

표현 현행: [DB 기반 해상·항공 관찰 r17](transport-preview.implementation.r17.md). 기존 지구본에 선박·비행기 상징 모형 20개를 연결하고 Unity 시험 8/8·실제 Play/Game View를 확인했다. 실제 항로·대수 아님. 아래는 수집 및 이전 표현 기준선이다.

자료 수집 현행: [관세청 해상·항공 수집 r16](port-movement.collection.r16.md). 활용신청 반영 후 2025년 5국가 원필드 2,431행을 기존 MySQL에 저장·독립 재조회했다. r15의 API 접근 차단은 해소되었으며 중량 단위 검토는 남아 있다. Unity 변경 없음.

자료 수집 현행: [운송수단별 자료 수집 r15](transport-mode-data.collection.r15.md) — 인천공항 2025년 공개 파일에서 5국가 월별 관측 912개를 로컬 DB에 저장·독립 재조회했다. 해상 API는 서비스 키 등록 오류로 대기하며, 이번 Unity 변경은 없다. 아래 r14는 기존 표현 검증 기준선이다.

현행: [2025년 국가 무역 수집·표현 r14](trade-data-binding.implementation.r14.md) — `ApprovedScopedLocalPreview / CollectedStoredAndPlayVerified`. 한국↔5국가 공식 연간 총계 수집·로컬 DB 20개·재입력 신규 0·실제 Play 상징 화물 40개를 확인했다. 수단 미확인으로 선박/항공기는 임의 배분하지 않는다. [범위 확정 r13](trade-baseline.decision.r13.md)·[조사 r12](trade-weighted-transport.proposal.r12.md)는 이력으로 보존한다. 정식 빌드·공개 배포는 별도다.

현행: [세계 해역 가상 선박 r11](international-ships.implementation.r11.md) — `ApprovedScopedLocalPresentation / LocalPreviewVerified`. 중국·동남아·일본·미국·호주 주변을 포함한 6개 합성 항로·12척을 Editor 전용으로 표현한다. [물고기·선박 r10](marine-motion.implementation.r10.md)·[수온 실험 r9](virtual-temperature-school.implementation.r9.md)·[수온 사본 r8](temperature-globe-binding.implementation.r8.md)을 보존한다. 실제 어종 예측·항로/AIS·공개 배포·정식 게임 Simulation 통합은 별도다.

## r1 제안 이력

## 상태·관계

- `Proposed / ExistingSourcesInspected / NoRuntimeImplementation` (2026-09-22).
- 사용자 의도: 기존 지구본에서 물고기가 돌아다니는 모습을 관찰하며 수산물 통계를 확인한다.
- 상위: [한반도 공공데이터 관찰 세계](../PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD/README.md).
- 자료 기반: [신선·냉장 어류 저장 기록 r58](../PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD/p0-household-fresh-chilled-fish-batch.implementation.r58.md).

## 현재 확인한 기반

별도 Unity 저장소의 `Assets/Ssalddel/Presentation/WorldMap/세계지구본View.cs`에는 `GlobeVisualRoot`, 위경도 구면 변환, 국가 선택·상세보기 경로가 있다. `세계지구본CatalogModels.cs`에는 국가 경계와 출처·판본이 있다. 새 Scene이나 지구본을 만들지 않고 기존 곡면에 표현 계층을 붙이는 방향이다.

Hongdal의 `eng/public-data/trade-retail/batches/chapter03-household-fresh-chilled-fish-01.manifest.json`에는 HS6 신선·냉장 어류 10개에 대한 2025년 대한민국 수입 자료 참조가 있다. 이는 어군 위치·생산량·자원량 자료가 아니다. r58은 로컬 MySQL 저장·재조회를 기록하지만 이번에는 DB를 다시 조회하지 않았다. `distributionApproved=false` 및 상품 결속 `PendingHumanReview`를 유지하며 Unity 배포 자원으로 바로 내보내지 않는다.

## 첫 표현 제안

| 단계 | 표현 | 의미 경계 |
| --- | --- | --- |
| 지구 전체 | 소수 물고기 떼가 구면 바다 위에서 천천히 움직임 | 상징 연출이며 실제 어군·개체 수 아님 |
| 한반도 주변 확대 | 선택 가능한 수산 품목 표식과 저폴리 물고기 | 이동 구역은 합성 연출 영역이며 서식지·어획지 주장 아님 |
| 품목 선택 | 하단 카드에 국가·품목·기간·수입 중량/금액·출처 | 국가 통계는 국가 귀속으로 표시하고 주변 해역 통계로 바꾸지 않음 |
| 상세보기 | 상대국·단위·자료 기준일·누락 상태 | 수입 상대국은 실제 어획지·운송 경로의 근거가 아님 |

첫 모형 후보는 고등어·대서양 연어·대구 등 기존 자료가 있는 소수 품목이다. HS 품목군을 단일 생물종과 동일시하지 않고 정확한 모형 대응은 별도 검토한다. 사진 권리를 가정하지 않고 단순 도형·저폴리로 시작한다.

물고기 수·속도는 처음에는 통계와 무관한 제한된 연출로 한다. `통계 상징 · 실제 서식/이동 아님`을 간결하게 표시하며 상세 카드는 선택 전에는 접는다. 수량 비례 표현은 같은 지표·기간·단위·척도·누락 규칙을 정한 뒤 후속으로 검토한다. 무자료를 0으로 표현하지 않는다.

## 데이터·책임·순서

`검토된 통계 → 서버 읽기 전용 상태 사본 → 국가·품목 해석 → 물고기 표현 → 선택 카드`.

- 관측 ID, 품목/HS 코드, 국가·상대국, 기간, 지표 종류, 값·단위, 출처·판본·사용 승인·누락을 보존한다. 표현 모형 key와 합성 연출 영역 ID는 사실 자료와 분리한다. 기존 조회 계약을 대조하기 전 새 API/DTO를 자동 추가하지 않는다.
- 물고기의 이동·클릭은 실제 어획·재고·거래·성장·서버 업무 상태를 바꾸지 않는다. Unity는 DB나 공공 API를 직접 호출하지 않는다.
- 우선순위: 자료 승인/결손 점검 → 기존 지구본의 소수 모형 이동·선택 → 승인된 국가 통계 카드 → 실제 화면 검증. 기존 음식 관찰 수리와는 독립 기능이다.
- 확대 시 가시 범위만 활성화하고 객체 수 상한·재사용·저동작 옵션을 둔다. 곡면 이동, 육지 침범 방지, 카메라 회전/확대, 선택, 메모리 해제, 서버 권위 불변을 검증한다. Play Mode·Game View는 코드 시험과 별도 증거다.
- 실제 해역별 어획·생산 표현은 해역 코드·공간 범위·어종·기간 근거를 추가 조사한 뒤 연결한다. 국가 경계나 수입량으로 바다별 어군 분포를 추정하지 않는다.

## 확정 / 미정 / 다음 질문 하나

- 확정된 사용자 의도: 물고기의 움직임으로 지구본에 살아 있는 느낌을 주고 수산물 정보를 확인한다.
- 미정: 위 첫 범위의 승인, 대표 품목/모형, 연출 영역, 표시 승인 자료. 아직 구현 승인·새 WI/Goal 활성화가 아니다.
- 다음 질문: 우선 한반도 주변의 상징적 물고기 떼를 누르면 해당 품목의 국가 수입 통계 카드가 열리는 작은 버전으로 시작할까? 이 범위를 추천하며 실제 서식지·어획량 표현은 후속으로 둔다.

## 이번 작업과 증거

기존 r58·배치 manifest·Unity 소스를 조사하고 기획 후보만 기록했다. DB 재조회·새 공공자료 수집·제품 코드·Scene·commit·push 변경 없음. 기존 사실/합성 표현 분리 경계를 유지하며 새 디오라마 규칙 후보 없음. E 승격 없음.
