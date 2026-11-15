# 사가정 공통 표면 깊이와 도로 접합 표현 고도화

[기획·제작 · 디오라마 공통 표면층 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r53]

- 사용자 요청: 2026-10-29. ‘일단 고도화를 진행’을 명시했다. [확인된 입력 우선 r52](verified-inputs-first-refinement.decision.r52.md)와 [전체 품질층 누적 r48](area-wide-layered-quality.decision.r48.md) 안에서 실제 보완·같은 조건의 전후 검증까지 진행한다.
- 상태: `LocalRefinementAuthorized / CommonSurfacePreviewValidated / BeforeAfterJourneyVerified / CanonicalUnchanged`.
- 선행: [제작 방식 추출 r50](sagajeong-construction-recipe.extract.r50.md), [사가정 픽업·라이딩 r122](../PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/pickup-riding-threeway.prototype.r122.md).

## 이번에 확인한 원인

현재 비공개 공간 입력은 Overture 건물 외곽 2,247개를 포함하지만 높이는 `SymbolicFallback4m` 기호값이다. 단순 평지붕과 같은 높이의 압출이 높은 주행 카메라에서 두드러진다. 실제 높낮이·지붕 형태를 확인한 상태로 해석하지 않는다.

현재 창 shader는 기존 벽 분류/격자식을 유지하고 있다. 따라서 ‘창이 모두 빠졌다’는 진단으로 새 창 수·층수를 만들지 않는다. 도로의 shoulder와 차도도 이미 존재한다. 실제 주행/조감 화면에는 일부 접합부에 빗금처럼 보이는 겹침이 있어 같은 평면에 놓인 strip/node cap과의 관계를 확인한다.

## 허용한 작은 구현

1. **공통 지붕 경계:** 활성 공통 Mesh의 기존 지붕 삼각형에서 외곽 edge만 구하고 얇은 평면 명암 띠를 만든다. 내부 삼각형 선은 제외한다. 실제 난간·설비나 지붕 실측 복원을 의미하지 않는다. 사진 전면/간판·후보 경로·기사/점포 전용 모델은 제외하며, 기존 135m 근접 상세만 확대하는 방식으로 만들지 않는다.
2. **확인 가능한 도로 겹침 보완:** 기존 y=.07/.10/.125의 도로 평면과 node fan 구조를 확인한 뒤 opt-in Mesh 사본에서 소규모 렌더 높이 차이를 줄 수 있다. XZ·도로 종류·폭·노드/선분의 원본 의미는 유지한다. 보완 후에도 남는 겹침은 해결로 보고하지 않는다.
3. **같은 장면 대조:** 원본 입력·형상·높이·카메라·조명을 유지한 before/candidate/restored를 조감·근접·주행 시점에서 출력한다. 선택한 후보로 r122와 같은 12초·24fps·288프레임 여정을 출력해 정지/이동 중 판독성을 확인한다.

이번에는 새 높이·실폭·출입구·창 수·옥상 설비·매장 동일성을 추정하거나 자산을 구매하지 않는다. 실제 높낮이를 해결하려면 확인 가능한 높이 원본과 건물 ID 결속을 별도 수집/재조회해야 한다.

## 소유와 수정 경계

- 별도 Unity checkout의 새 Editor 전용 `사가정공통표면깊이Style.cs`, `사가정공통표면깊이PreviewCapture.cs`와 필요한 `.meta`/집중 EditMode 시험.
- 기존 `사가정상세건물PreviewCapture.cs`에 새 opt-in 호출만 추가하고 r122 호출은 유지한다. 기존 파일은 patch 직전에 다시 읽는다.
- `SSALDDEL_SAGAJEONG_SURFACE_DEPTH_OUTPUT` 등 명시한 환경변수와 새 비공개 출력 경로에서만 실행한다. 원본 Mesh/Material을 수정하지 않고 Scope가 소유한 사본/추가 표현만 사용하며 제거 후 복구한다.
- Runtime builder/shader·공식 Scene·Prefab·설정/패키지·운영/Simulation Core·DB·Goal/WI는 수정하지 않는다. 이번은 새 게임 규칙 구현이나 E 승격을 위한 작업이 아니다.
- 작업 산출물: `artifacts/local/sagajeong-surface-depth-r53/`. 실패/수정 판본과 실제 원본/비교/복구·프레임·검증 기록을 보존한다.

이 결과는 로컬 고도화 후보다. 실제 프레임을 사용자에게 제시하며 정본/공식 Scene 적용으로 확대하지 않는다. [시안 선행 절차](../../../../Architecture/화면시안선행제작절차.md)의 현재 모습 확인·후보 시각화·후속 의견·실제 화면 대조를 이 비공개 후보와 연결한다.

## 검증 기준

- 원본 지붕 외곽/높이/XZ/도형·입력 hash와 기존 Mesh/Material 참조/내용 보존.
- 지붕 내부 edge 제외, 공통 적용 대상과 추가 renderer/vertex 수, 잘못된 대상 필터링 확인.
- 같은 입력/카메라/조명에서 before/candidate/restored 출력과 제거 후 exact PNG 복구.
- 실제 카메라/모형 이동, 단계별 시각·프레임 경계와 MP4 전체 디코딩. 픽셀 변화가 있다는 사실만으로 시각 개선을 선언하지 않고 이미지를 직접 검토.
- 기존 Scene·Graphics/Quality/BuildSettings·Packages 보존과 새 C#/shader 오류·예외 확인. Pipeline 연결이 없으면 CLI graphics를 사용하고 Game View/실시간 FPS와 구분.

## 확정·미정·다음 질문 하나

- 확정: 현재 확인 가능한 입력을 보존하는 공통 표면층을 실제로 만들어 같은 조건에서 비교한다. 새 지역/게임 기능보다 현재 구역 품질을 우선한다.
- 미정: 후보의 최종 강도·시각 판독성·잔여 도로 겹침·추가 geometry 비용은 실제 출력으로 판정한다. 실제 높이/지붕·정식 주행/Scene 반영은 미확정이다.
- 다음 질문 하나: 준비된 범위는 진행한다. 전후 결과를 본 뒤 필요한 표면 강도 차이 하나만 검토한다.

## 실제 결과

최종 EditMode10/10·실패0·생략0, Unity CLI graphics attempt4 exit0를 확인했다. 같은 소스의 공통 Renderer16개에서 지붕면 구성2,847개·외곽13,444edge를 처리하고 내부edge4,903개는 제외했다. 벽 하단vertex26,888개와 검증된 도로fan3,977개를 보완했으며 추가Renderer1개·vertex53,776개·triangle26,888개다. 지붕면 구성 수는 실제 매장/건물 수로 승격하지 않는다.

4시점×before/candidate/restored12PNG와 전후 각288프레임을 실제 출력했다. 4시점 모두 제거 후 exact복구, 원본 Mesh/Material·Scene/설정/package/기존 제작기 등10파일 보존을 확인했다. 전후 여정은288/288프레임에서 기사·카메라·단계·시각이 같고 후보288프레임 모두 pixel 차이가 있다. MP4 두 편은 각각1080p·24fps·12초·전체288프레임 디코딩을 통과했다.

독립 시각 검토에서도 지붕 경계와 도로 빗금 감소를 확인해 현재 강도를 채택했다. 큰 회색 연구 건물의 넓은 지붕과 일부 짙은 접합 패치는 남는다. 기본 높이 결손은 그대로이며 실제 높이 자료와 건물 ID 대응이 다음 우선 확인 대상이다.

로컬 비교 화면에서 네 시점/크게 보기·동시 재생·6초 동시 이동·초기화·후보MP4 받기/해시 일치를 확인했다. 미리보기의 byte range 부재로 구간 이동이 안 되는 문제를 확인해 루프백 비교 서버를 보완하고 재검증했다. [변경 기록·실제 화면·근거](../../../../Changes/2026-10-29-sagajeong-common-surface-r53.md)에 자세한 수량/파일/실패 보완과 미검증 범위를 모았다.

정식 Play Mode/Game View·실시간 FPS·실제 높이/출입구·정본/운영/공개·commit/push는 미실행이다. 기존 원천/표현 분리·기호 높이의 결손 보존·기본 매스 유지 원칙을 지지하며 반례/무효화를 만들지 않았다. **새 디오라마 규칙 후보 없음.**
