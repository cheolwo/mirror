# [기획·개발 인계 · 배달 플랫폼 운영 Simulation · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · 통합 개발계획 r61]

- 기준일: 2026-09-16
- 인계 상태: `ReadyForScopedDevelopmentHandoff / FirstNewSliceReadyForAcceptance / DistributionDataBlocked / OperationalActivationDeferred`
- 대체 관계: [개발계획 r33](development-handoff.r33.md)의 서버·예외·재무 계획을 보존하고, r48~r60의 공공자료 동기화·Unity 운영 게임·절기 캠페인을 추가한 후속 통합 인계다.
- 첫 신규 구현 추천: `DEV-REGIONAL-OPERATOR-09 · 지역 자료 세션·검증 캐시 기반`
- 첫 공개 공간: `station:kr:kric:s1107:0722` 사가정역 1km 관찰 창과 `region:kr:hjd:1126057500` 면목제3·8동
- 권위 원칙: 공공자료 게시와 업무 사실은 서버, 가상 운영 결과는 Simulation Core, 화면·카메라·Animation은 Unity가 소유한다.
- 배포 차단: 현행 행정동 자료 후보에는 `distributionApproved=false`가 남아 있으므로 개발 Fixture와 실제 동봉·배포 승인을 구분한다.

이 문서는 다른 GPT 계정이나 새 개발 스레드가 r33 이후의 새 기획을 빠뜨리지 않고 단계적으로 구현하도록 만든 통합 인계다. 전체 구현을 한 번에 승인하지 않는다. 각 개발 절편은 정확한 WI 하나, E1~E7 상호작용 수직 검증 명세, 파일 소유권과 검증 상한을 먼저 결속한 뒤 하나씩 수용한다.

### 기획 결속 hash

| 기준 | SHA-256 |
| --- | --- |
| `README.md` r61 | `830B2F78BBAC23000CE44D62AEF47664700C77F284F68D79BD7CBBD898D20546` |
| `development-handoff.r33.md` | `FC5CA2D8815225DEE296CD878EA02DAEE97AB5965D428249FB85864654EADCEE` |
| `public-data-observation-game-baseline.r53.md` | `51929A30D6378303F5B93C8D07D7225DAB47B27C1883D16EB66B155D2E8040E8` |
| `platform-operator-from-start.r58.md` | `9FB1A96B8EBEF7B2BB379901A30222CDC66F854EDBEE13828D51280AD0EA811F` |
| `seasonal-delivery-operation-campaign.r59.md` | `F9B6EBB2FBD1A6610E358D61837A74ECF302FBD51BC2409561E4A64BB92B7FCB` |
| `urgent-prologue-calm-seasonal-campaign.r60.md` | `66B1A1AEB28A3AD24B43DABA5C69301B601789BA58AC6029362227E848B3D0B5` |

다른 계정은 작업 시작 시 이 hash를 다시 계산한다. 불일치하면 최신 기획을 다시 읽고 쓰기 범위를 재결속하며, 과거 hash에 맞추려고 사용자 변경을 되돌리지 않는다.

## 1. 제품 목표

첫 제품은 사용자가 Unity에서 `가상 배달 플랫폼의 사가정·면목제3·8동 권역 운영자`가 되는 지역 운영 Simulation 게임이다.

```text
공식 공공자료
→ 서버 수집·검증·판본화·게시 승인
→ Unity 접속 시 manifest·tile·overlay 확인
→ 검증된 기존 판본으로 즉시 시작 + 새 판본 백그라운드 갱신
→ 선택한 지역 자료 판본에 세션 고정
→ 합성 음식배달·기사·음식점·주문자 Simulation
→ 운영자 정책 Preview·Confirm
→ 디오라마에서 결과 관찰
→ 피크·일·절기·13주 결산
```

플레이어는 기사·차량을 직접 조종하지 않는다. 기사 공급 알림, 플랫폼 부담 피크 할증, 자동화 범위, 예외 처리, 예산·현금 방어와 절기 캠페인 대응을 결정한다. 실제 기사 알림·주문·배차·결제·정산은 첫 제품에서 호출하지 않는다.

## 2. 결속된 기획 묶음

### 기존 r33에서 보존하는 범위

- 음식배달 정상 7단계와 역할별 `AvailableActions`.
- 수락 후 취소, 조리 지연 보호 해제, 수령자 부재와 증거·보호 정책.
- 기사 수행대금, 음식점 상품대금, 고객 환급·준비금과 플랫폼 비용의 분리 원장.
- 정상 경로 자동화, 예외 격리, 주간·월간·13주 재무 확인.
- 서버→Simulation→Unity 권위 분리와 `SimulationWorldShell` 단일 공식 Scene.

### r42~r47에서 보존하는 공간·표현 범위

- 사가정역 1km를 첫 관찰 창과 시각 회귀 기준으로 유지.
- 면목제3·8동을 첫 운영·집계 권위로 사용하고 인접 행정동을 복제하지 않음.
- 검증된 건물·출입 기준점·이동 graph만 Actor 이동에 사용.
- 결손 구간은 `이동 중`·`MissingCoverage`로 표현.
- 공공자료 절차적 매스를 우선하고 중요 건물만 후속 Blender 상세화.
- Blender 출구 순서 1→2→3→4는 보존하지만 현재 운영 개발의 선행조건으로 만들지 않음.

### r48~r52에서 추가된 운영 범위

- 첫 위험은 `미배차·지연 임박 주문`.
- 원인은 `기사 부족`, `조리 지연`, `위치 갱신 중단` 카드로 분리하되 사건을 복제하지 않음.
- 기사 부족 첫 조작은 `가용 의향 알림 Preview`.
- 실제 부족이 지속되면 플랫폼 전액 부담의 피크 할증 후보를 엶.
- 주문자 배송료는 주문 확정 시 고정하고 내부 기사 할증과 자동 연동하지 않음.
- 피크 할증은 관리자 정책의 단계별 건당 정액이며 승인 범위 안에서만 자동 적용.
- 정책 금액·피크시간·예산·현금 방어선은 판본화하고 수락된 기사 지급액은 소급 변경하지 않음.
- 정확 단계 수·원화 금액·현금 연동 방식은 아직 미정이다.

### r53~r57에서 추가된 공공자료·첫 실행 범위

- Unity는 서버가 게시한 최신 승인 지역 자료만 읽고 외부 공공 API를 직접 호출하지 않음.
- 접속 시 최신 승인 판본을 확인하되 활성 세션은 선택한 `manifestRevision + projectionHash`에 고정.
- 검증된 기존 판본으로 즉시 시작하고 새 호환 판본은 백그라운드에서 임시 다운로드·검증.
- 첫 설치에는 배포 승인된 사가정·면목제3·8동 최소 기준선을 동봉.
- 동봉 기준선에는 같은 seed로 재현 가능한 정상 음식배달 1건 합성 Fixture를 포함.
- 현행 자료 후보의 배포 승인 완료를 선언하지 않음.

### r58~r60에서 추가된 게임·캠페인 범위

- 사용자는 첫 실행부터 가상 배달 플랫폼 권역 운영자.
- 정책 Preview와 Simulation 적용을 첫 회차부터 허용하고 무입력 시 안전 기본값 사용.
- 첫 프롤로그는 해결 가능한 긴박한 피크 운영, 이후 안정 복귀와 잔잔한 절기 캠페인 시작.
- 기존 절기 기획의 `실시간 현장 → 일 WorldTick → 절기 마감`, 초·중·후반과 두 전략 분기를 재사용.
- 아르카나 세 장 중 한 장과 대응을 선택하되 카드는 하위 업무를 직접 확정하지 않음.
- 공식 제철 자료는 합성 메뉴·시장·마트·주문 구성 후보이며 수요·가격을 자동 확정하지 않음.
- 첫 프롤로그 핵심 사건과 최종 캠페인 중첩 규모는 미정이지만 추천 기준은 `점심 주문 증가 + 기사 부족`, `피크→일→절기→13주`다.

## 3. 현재 코드 기준선

### 이미 존재해 먼저 재사용할 행정동 디오라마 경로

| 책임 | 현행 경로 | 개발 의미 |
| --- | --- | --- |
| API | `Ssalddel/Controllers/Common/행정동디오라마Controller.cs` | manifest·tile·display-overlays GET과 ETag가 이미 있다. 새 route를 만들기 전에 재사용한다. |
| 조회 | `Ssalddel/Application/WorldProjection/행정동디오라마조회UseCase.cs` | 안정 ID 검증과 저장 투영 조회가 있다. Unity 캐시 권위나 세션 저장을 이 UseCase에 넣지 않는다. |
| 계약 | `Ssalddel.WorkflowRules.Contracts/UnityPackage/Runtime/AdministrativeDongDioramaContracts.cs` | `schemaVersion`, `sourceVintage`, hash, readiness, 배포 상태의 현재 계약이다. 공개 ID를 깨지 않는다. |
| 게시·저장 | `Ssalddel/Services/WorldProjection/AdministrativeDongDiorama/행정동디오라마PublicationService.cs`, `행정동디오라마ProjectionStore.cs` | 게시 승인과 불변 투영 책임을 유지한다. 클라이언트 다운로드 결과를 서버 원장에 되쓰지 않는다. |
| Unity Decoder | `Ssalddel.Unity/Runtime/OperationalTransport/UnityJsonAdministrativeDongDioramaDecoder.cs` | JSON 계약 검증 지점이다. 캐시 파일을 신뢰하기 전에 같은 검사를 사용한다. |
| Unity Client | `Ssalddel.Unity/Runtime/WorldProjection/AdministrativeDongDioramaClient.cs` | 현재 모든 tile을 메모리로 GET한다. 세션 pin·ETag·캐시·백그라운드 staging을 확장할 주 후보다. |
| Unity Interpreter | `Ssalddel.Unity/Runtime/WorldProjection/AdministrativeDongDioramaInterpreter.cs` | 승인된 manifest·tile을 메모리 모델로 적용하고 Clear하는 경계다. 다운로드 중 후보를 적용하지 않는다. |

### r33에서 계속 재사용할 음식배달 경로

- `Ssalddel/Controllers/Food/음식주문Controller.cs`
- `Ssalddel.Contracts/Food/음식주문Dtos.cs`
- `Ssalddel/Application/Food/음식배달수명주기SnapshotFactory.cs`
- `Ssalddel/Application/Food/음식배달수명주기조회UseCase.cs`
- `Ssalddel/Controllers/Food/음식배달수명주기Controller.cs`
- `Ssalddel/Application/Admin/Food/음식주문운영추적UseCase.cs`
- `Ssalddel.Simulation.Domain/UnityPackage/Runtime/Simulation음식배달.cs`
- `Ssalddel.Simulation.Application/Neighborhood/음식배달여정Projection.cs`
- `Ssalddel.Unity/Runtime/Observation/동네관찰Presenter.cs`
- `Ssalddel.Unity/Runtime/Observation/동네관찰SessionController.cs`
- `eng/Ssalddel.RoleAppHeadlessE2E/Program.cs`

실제 경로·클래스가 현재 checkout에서 달라졌으면 소스가 우선이며 인계 문서를 조용히 사실로 만들지 않는다.

## 4. 목표 아키텍처

```text
지역 자료 게시 권위
  └─ manifest / tile / overlay + ETag + distributionApproved
       └─ Unity 지역 자료 Bootstrap
            ├─ 동봉 검증 기준선
            ├─ 활성 검증 캐시
            ├─ 백그라운드 staging 후보
            └─ 세션에 고정된 지역 자료 revision/hash

Simulation Core
  ├─ 합성 음식 주문·음식점·기사·전달지
  ├─ 정상 7단계와 예외 사건
  ├─ 기사 공급·피크 할증 정책
  ├─ 피크·일·절기·13주 시간층
  └─ 정책 Preview / Confirm / Expire / Rollback

Unity SimulationWorldShell
  ├─ 현실 기준선 디오라마
  ├─ 가상 Actor·차량·업무 상태 사본
  ├─ 운영자 지표·원인 카드·AvailableActions
  ├─ 절기·아르카나·제철 표현
  └─ 실제 업무 권위 없음
```

## 5. 통합 개발 순서

### 단계 0 — 현행화 감사와 작업 소유 결속

1. branch, dirty worktree, 가까운 `AGENTS.md`를 확인한다.
2. r33과 이 문서, README r61, `PLANNING.md`, `CURRENT_WORK.md`를 읽는다.
3. r33의 DEV-01~08 구현 여부를 route·test·실행 결과로 재판정한다. 문서 상태만으로 완료 처리하지 않는다.
4. 행정동 디오라마 r18과 현행 자료의 `distributionApproved`, source vintage, hash, readiness를 확인한다.
5. 첫 절편 WI 하나와 E1~E7 검증 명세를 만들고 정확 수정 경로와 제외 범위를 결속한다.

완료 조건: 첫 절편의 소유 파일·검증 목록·현재 차단을 기록하고 다른 dirty 변경을 침범하지 않는다.

### 단계 1 — `DEV-REGIONAL-OPERATOR-09` 지역 자료 세션·검증 캐시 기반

첫 신규 구현 절편이다. 실제 배포 자료 제작이나 Scene 변경 없이 계약·캐시·세션 pin을 닫는다.

#### 범위

1. 동봉 기준선·활성 캐시·다운로드 staging을 구분하는 프로젝트 소유 저장 경계를 추가한다.
2. 지역 자료 후보는 manifest·tile hash·schema·area stable ID·projection hash·distribution/readiness를 검증한다.
3. 기존 검증 캐시가 있으면 즉시 반환하고 서버의 새 manifest 확인은 별도 비동기 작업으로 수행한다.
4. 새 후보는 검증 성공 뒤 `ReadyForNextSession`으로만 표시하고 활성 Interpreter에 즉시 적용하지 않는다.
5. 세션 시작 시 선택한 manifest revision·projection hash·source vintage를 불변 세션 문맥에 기록한다.
6. 실패·취소·저장공간 부족 때 staging만 버리고 활성 캐시는 유지한다.
7. 실제 파일 쓰기 Adapter와 메모리 Fake를 분리해 EditMode/순수 C# 시험이 가능하게 한다.

#### 금지

- `distributionApproved=false` 자료를 일반 사용자 동봉 패키지로 승격.
- 외부 공공 API를 Unity에서 직접 호출.
- 다운로드 완료 후 현재 세션의 Interpreter를 자동 교체.
- 새 공식 Scene 생성 또는 `SimulationWorldShell` 구조 변경.
- 캐시를 서버 게시 원장으로 업로드.

#### 완료 조건

- 같은 hash는 재다운로드하지 않는다.
- 변조 tile·projection mismatch·schema mismatch를 거절한다.
- 취소·실패 뒤 기존 캐시로 시작할 수 있다.
- 같은 세션에서 자료 revision이 바뀌지 않는다.
- 동봉·캐시·staging 우선순위와 복구가 결정적이다.
- 코드 시험과 실제 파일 임시 디렉터리 round-trip을 분리해 통과한다.

### 단계 2 — `DEV-REGIONAL-OPERATOR-10` 합성 정상 배달 운영 상태 사본

r33의 DEV-01 정상 7단계를 재사용하고, 완료되지 않았다면 그 읽기 모델을 먼저 닫는다.

1. 동봉 사가정 개발 Fixture에 합성 음식점 역할·기사·전달지·주문을 안정 ID로 결속한다.
2. 같은 seed·지역 자료 판본에서 정상 7단계와 시간 결과가 결정적으로 생성되게 한다.
3. 검증된 이동 graph만 경로 좌표로 사용하고 결손 구간은 `이동 중`으로 투영한다.
4. 운영자 읽기 모델에 진행 주문·가용 기사·미배차/지연 위험·가용현금 Fixture를 제공한다.
5. 실제 상호명·주문·개인·결제·GPS를 넣지 않는다.

완료 조건: LocalProcess와 RemoteHost/TestServer가 같은 상태 사본 hash를 만들고 Save/Restore 후 같은 진행 지점으로 돌아온다.

### 단계 3 — `DEV-REGIONAL-OPERATOR-11` Unity 운영자 첫 화면·긴박한 피크 프롤로그

1. 기존 `SimulationWorldShell` 안에서 사가정 디오라마·상단 지표·오른쪽 정책 카드·선택 상세를 조립한다.
2. 첫 위험 목록은 미배차·지연 임박 주문이고 원인 표지는 기사 부족·조리 지연·위치 갱신 중단이다.
3. 같은 사건에 원인이 여러 개여도 업무 객체를 복제하지 않는다.
4. Actor·건물 선택은 서버/Simulation 상태 사본을 열 뿐 결과를 확정하지 않는다.
5. 첫 긴박한 피크는 별도 승인 전 설명용 Fixture로 두며 실제 수량·시간·성공 조건을 코드 상수로 확정하지 않는다.

완료 조건: 실제 Play Mode·Game View에서 전체 조망, 원인 선택, 대상 선택, 일시정지·배속, 정상 회복과 결산을 확인하고 Console 오류·입력 절차·build revision을 보존한다.

### 단계 4 — `DEV-REGIONAL-OPERATOR-12` 기사 공급 알림·플랫폼 부담 피크 할증

1. 가용 의향 알림은 특정 주문 배차와 분리된 Simulation 상태로 구현한다.
2. 알림 대상·빈도·예상 반응·비용 Preview 뒤 정책 판본 안에서만 활성화한다.
3. 실제 부족 지속 시 단계별 건당 정액 피크 할증 후보를 생성한다.
4. 할증은 플랫폼 비용과 기사 지급의무로 기록하고 주문자 배송료를 바꾸지 않는다.
5. 기사 수락 시 금액·정책 revision을 동결하고 소급 감액하지 않는다.
6. 정확 단계 수·금액·현금 연동 방식이 없으면 Simulation Fixture profile만 사용하고 운영 기본값을 만들지 않는다.

완료 조건: 알림만/알림+할증/무개입 비교에서 같은 seed 결정성, 예산 상한, 현금 영향, 만료·원복과 실제 Adapter 미호출을 검증한다.

### 단계 5 — `DEV-REGIONAL-OPERATOR-13` 피크·일·절기·13주 시간층

1. 실시간 피크 회차 결과를 일 단위 WorldTick에 누적한다.
2. 일 마감은 정상 집계와 `정산 확인 필요` 예외를 분리한다.
3. 절기는 외부 시간 Profile이 제공하는 초·중·후반과 두 전략 분기를 소비한다.
4. 절기 마감 Preview와 Confirm, 다음 절기 상태 사본 재조회를 구현한다.
5. 13주 전망은 같은 재무 사건을 받아 매주 actual로 치환하며 절기 마감과 별도 역할을 유지한다.

완료 조건: 중간 Save/Replay, 마감 Preview 무변경, Confirm 멱등성, Local/Remote 동등성과 서로 다른 시간층의 이중 정산 방지를 검증한다.

### 단계 6 — `DEV-REGIONAL-OPERATOR-14` 절기 아르카나·제철 운영 문맥

기존 `PLAN-TIME-SOLAR-TERM-TAROT-TURN-001` 계약을 재사용한다.

1. 절기 시작에 동결된 덱에서 결정론적으로 세 장을 제안한다.
2. 사용자는 한 장과 등록된 운영 대응을 Preview·Confirm한다.
3. 카드 효과는 하위 배달·정책 계산의 허용된 보정 입력만 제공한다.
4. 카드가 없어도 핵심 배달 운영은 Neutral 문맥으로 완주한다.
5. 제철 자료는 출처·기간·지역·품목·직접/파생 대응을 보존한 합성 메뉴 후보로만 쓴다.
6. 절기·제철 표현은 권위 상태 사본을 읽고 실제 수요·가격·업무 완료를 확정하지 않는다.

완료 조건: 같은 seed·덱 판본·턴 기록의 카드 제안 결정성, 제안되지 않은 카드 거절, 버림·재섞기 Save/Replay와 카드 없음 불변 결과를 검증한다.

### 단계 7 — r33 DEV-02~06 예외·역할 앱·재무 심화

다음은 새 게임 뼈대와 충돌하지 않게 r33의 기존 순서를 유지한다.

```text
수락 후 취소 Preview·확정
→ 조리 지연 보호 해제·재배차
→ 수령자 부재 보호 종료
→ 역할 앱 예외 카드
→ 13주 회계·현금 Stress 심화
```

각 예외는 첫 프롤로그에 한꺼번에 넣지 않고 별도 캠페인 또는 후속 난이도로 연다.

### 단계 8 — 배포 승인 기준선·실제 동봉·Unity 최종 검증

1. 행정동 디오라마 증거 체계와 배포 권리 검토를 통과한 판본만 초기 패키지로 승격한다.
2. 동봉 manifest와 서버 최신 판본의 호환·갱신·복구를 실제 빌드에서 검증한다.
3. Steam 패치와 게임 내부 지역 자료 갱신의 소유 책임을 분리한다.
4. 실제 Game View 캡처는 전체 운영 조망, 원인 카드 확대, 절기 시작/마감의 최소 세 장면을 남긴다.
5. 실제 외부 운영 Adapter는 계속 비활성으로 검증한다.

## 6. 의존 관계와 병렬 가능 범위

| 개발 묶음 | 필수 선행 | 병렬 가능 | 차단 |
| --- | --- | --- | --- |
| 09 자료 세션·캐시 | 단계 0, 기존 계약 확인 | 파일 Adapter 시험, ETag/Decoder 시험 | 실제 동봉은 배포 승인 전 차단 |
| 10 정상 배달 사본 | r33 DEV-01 계약 또는 동등 구현 | Simulation Fixture·조회 시험 | 실제 지도 경로는 이동 graph 결손 시 차단 |
| 11 Unity 프롤로그 | 09 메모리 모델, 10 상태 사본 | UI·표현 후보 조사 | 새 Scene·실제 업무 Command 금지 |
| 12 알림·할증 | 10 상태 사본, 정책 revision | 순수 Policy·재무 Fixture | 정확 금액은 기획 승인 전 Fixture만 |
| 13 시간층 | 10, 12의 원인·재무 사건 | WorldTick·Save/Replay 시험 | 정확 절기 길이 미정 |
| 14 아르카나·제철 | 13 절기 경계, 기존 카드 계약 | 덱 시험·공공자료 목록 조사 | 카드 수치·제철 수요 효과 미정 |
| r33 예외 심화 | 정상 7단계 | 예외별 비중첩 파일 | 약관·금액·보존기간 미정 |
| 배포 | 09~14 필요한 절편 + 증거 승인 | 패키징·Game View 담당 분리 | `distributionApproved=false` |

공유 계약·같은 Unity 파일·Scene 쓰기는 한 담당만 소유한다. 테스트와 읽기 조사만 병렬화하고 결과를 개발 통합 담당이 합친다.

## 7. 첫 신규 절편의 예상 파일 범위

다른 GPT 계정은 단계 0 감사 뒤 아래 범위만 수용한다. 실제 코드 탐색 결과에 따라 파일명은 줄일 수 있지만 이유 없이 넓히지 않는다.

### 우선 읽기

- `Ssalddel/Controllers/Common/행정동디오라마Controller.cs`
- `Ssalddel/Application/WorldProjection/행정동디오라마조회UseCase.cs`
- `Ssalddel.WorkflowRules.Contracts/UnityPackage/Runtime/AdministrativeDongDioramaContracts.cs`
- `Ssalddel.Unity/Runtime/WorldProjection/AdministrativeDongDioramaClient.cs`
- `Ssalddel.Unity/Runtime/WorldProjection/AdministrativeDongDioramaInterpreter.cs`
- `Ssalddel.Unity/Runtime/OperationalTransport/UnityJsonAdministrativeDongDioramaDecoder.cs`
- 관련 Unity EditMode·서버 계약 시험

### 예상 추가 책임

- 지역 자료 캐시 abstraction과 메모리 Fake.
- 세션에 고정된 manifest revision/hash 문맥.
- staging 후보 검증·승격·폐기 결과.
- 기존 Client의 즉시 활성 Refresh와 새 세션 준비 다운로드를 구분하는 orchestration.

### 제외

- 실제 행정동 원본 수집·게시 승인 변경.
- 정상 음식배달·피크 정책·절기 캠페인 구현.
- Prefab·Scene·Game View.
- 운영 DB migration과 실제 외부 다운로드 서비스.

## 8. 공통 시험·증거 계약

### 자료 동기화

- ETag 304, 같은 hash 재사용, 변경 tile만 후보화.
- schema·area ID·tile hash·projection hash 불일치 거절.
- staging 중단·앱 종료·저장공간 부족·부분 파일 복구.
- 활성 세션 revision 불변과 다음 세션 전환.
- 검토 중·배포 불가 자료의 일반 사용자 경로 차단.

### Simulation

- 같은 seed·자료·정책 revision의 결정성.
- Preview 무변경, Confirm 멱등, 만료·원복.
- Save/Restore/Replay와 LocalProcess/RemoteHost 동등성.
- 실제 FCM·PG·기사 GPS·운영 DB Adapter 미호출.

### Unity

- 공식 `SimulationWorldShell` 하나만 사용.
- 자료 판본·업무 revision·카드 revision 판독.
- 중복 GameObject 방지와 지역 전환 시 메모리 정리.
- 실제 입력·Play Mode·Game View·Console을 자동 시험과 분리 보고.
- Unity Animation과 화면 입력이 서버·Simulation 권위를 직접 바꾸지 않음.

### 재무·캠페인

- 주문자 배송료와 플랫폼 부담 기사 할증 분리.
- 수락된 기사 지급액 소급 변경 금지.
- 일·절기·13주 이중 집계 금지.
- 카드 없음 기준 결과와 카드 보정선의 출처·단위·상한 보존.
- 제철 원자료·파생 대응·게임 해석 분리.

## 9. 중단·기획 반환 조건

- 공개 배포에 필요한 자료가 여전히 `distributionApproved=false`인데 release 패키지 생성을 요구하는 경우.
- 캐시 계약을 위해 기존 공개 manifest stable ID나 schema를 호환 없이 변경해야 하는 경우.
- 세션 pin 없이 플레이 중 공간 자료를 즉시 교체해야만 하는 구조.
- 검증된 이동 graph가 없어 실제 도로 주행을 추정해야 하는 경우.
- 첫 프롤로그 수량·임계값·할증 금액을 기획 승인 없이 운영 기본값으로 정해야 하는 경우.
- 카드가 주문·배차·재고·현금 결과를 직접 확정하거나 안전 필터를 우회하는 경우.
- 공공자료의 제철·월별 추천을 실제 수요·가격으로 자동 변환해야 하는 경우.
- 같은 파일·Scene·migration을 다른 dirty 작업이 소유해 안전한 병합이 불가능한 경우.

반환 보고에는 발견 사실, 영향받은 계획 단계, 가장 이른 재개 E, 임시 Fixture 여부와 권위 변화 여부를 적는다.

## 10. 맥락별 커밋 후보

실제 커밋 권한을 별도로 받은 경우에만 다음 경계로 나눈다.

1. 지역 자료 계약·캐시·세션 pin.
2. 합성 음식배달 상태 사본·Simulation 시험.
3. Unity 운영자 화면·프롤로그 표현.
4. 기사 알림·피크 할증 Policy·재무 Fixture.
5. 피크·일·절기·13주 시간층.
6. 아르카나·제철 문맥과 카드 시험.
7. 문서·대장·검증 결과.

관련 없는 기존 dirty 변경을 stage하지 않고 commit 후에도 push는 별도 요청 전 수행하지 않는다.

## 11. 다른 GPT 계정용 첫 작업 지시문

아래 문장을 새 GPT 계정의 첫 요청으로 사용할 수 있다.

> `C:\Users\user\source\repos\Hongdal`에서 `DEV-REGIONAL-OPERATOR-09 · 지역 자료 세션·검증 캐시 기반`만 구현해 주세요. 먼저 루트와 가까운 `AGENTS.md`, `docs/ProjectOverview/GptProjectContext.md`, `docs/AI/PLANNING.md`, `docs/AI/DECISIONS.md`, `docs/AI/CURRENT_WORK.md`, `docs/AI/Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/README.md`, `development-handoff.r33.md`, `development-handoff.r61.md`, `public-data-observation-game-baseline.r53.md`, `session-pinned-public-data-revision.r54.md`, `background-regional-data-update.r55.md`, `bundled-sagajeong-baseline.r56.md`를 읽으세요. branch와 dirty worktree를 확인하고 관련 없는 변경을 보존하세요. 기존 `행정동디오라마Controller`, `행정동디오라마조회UseCase`, `AdministrativeDongDioramaContracts`, Unity Decoder·Client·Interpreter를 재사용해 동봉 기준선·활성 검증 캐시·백그라운드 staging·다음 세션 후보와 세션에 고정된 manifest revision/projection hash를 구분하세요. 현재 세션을 다운로드 완료와 동시에 교체하지 말고, `distributionApproved=false` 자료를 일반 사용자 배포로 승격하지 마세요. 외부 공공 API 직접 호출, 새 공식 Scene, 음식배달·피크 할증·절기 캠페인 구현은 이번 절편에서 제외하세요. 코드 전에 WI 하나의 E1~E7 상호작용 수직 검증 명세와 정확 수정 경로·시험을 결속하세요. 같은 hash 재사용, 변조·schema·projection mismatch 거절, 중단 뒤 기존 캐시 복구, 세션 revision 불변과 실제 파일 round-trip을 검증하세요. 완료 시 코드·시험·실제 Unity 실행·DB·commit·push를 각각 사실대로 분리 보고하세요.

## 12. 후속 인계 순서

```text
09 지역 자료 세션·검증 캐시
→ 10 합성 정상 배달 운영 상태 사본
→ 11 Unity 운영자 첫 화면·긴박한 피크 프롤로그
→ 12 기사 공급 알림·플랫폼 부담 피크 할증
→ 13 피크·일·절기·13주 시간층
→ 14 절기 아르카나·제철 운영 문맥
→ r33 예외·역할 앱·재무 심화
→ 배포 승인 기준선·실제 동봉·Game View 최종 검증
```

각 단계는 앞 단계의 문서 표시가 아니라 contract·test·저장·실행 결과를 다시 확인한다. 서버 사실, Simulation 결과, Unity 표현과 실제 배포 승인을 하나의 완료 증거로 합치지 않는다.
