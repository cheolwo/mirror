# 건물별 상세 외형 교체와 사진 참조 점포 시범

[기획 · 상세 건물 표현 · PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION · r114]

- 날짜: 2026-10-26
- 상태: `ApprovedByDirectUser / PrivateDetailedFrontsRendered / Focused143Passed / PrivateBuildingStudy / CanonicalApplied0 / GameViewNotVerified / OperationalDisabled`
- 직접 요청: 실제 외관을 가진 건물 모델과 거리별 표현 구조 제안에 대해 “한번 진행해 봐.”라고 실행 요청했다.
- 이전 결과: [재질·조명 현실감 시안 r113](diorama-realism-preview.r113.md). 실제 형태의 결손과 소형 외관 부품의 책임 한계를 확인한 후속이다.

## 구현 단위

기존 공간 원장·건물 ID·visualKey/영상 SHA·실제 PTS·보호·사용자 승인 상태는 재사용한다. 새 정본이나 운영 DB를 만들지 않는다. 기존 소형부품 Overlay의 LOD/예산 제한은 풀지 않으며 새 상세 건물 표현은 별도 책임으로 둔다.

이번은 같은 0111 근거의 비공개 연구 3건물 중 준비된 점포부터 사진 참조 상세 전면 모델을 만들고 기존 모형과 비교하는 좁은 작업이다. 전체 건물 높이·층수·지붕/뒷면·정면 방향은 기존 미확정 상태를 보존한다. 4m 기호 높이를 실측값으로 승격하거나 사진에서 보이지 않는 실내를 재현하지 않는다. 새 부분의 수치는 사진을 참고한 비실측 표현 치수로 기록한다.

원래 모형에서 상세 외형으로 원자적으로 교체하고, 미준비/로딩 실패·원거리·해제 때 원래 모형으로 돌아오는 상태·소유권을 검증한다. 사진 참조 모델은 창틀·입구·간판·차양 두께와 유리의 후퇴를 표현하고 점포별 직접 관측 차이를 따른다. 위치/업무 권위는 기존 원장에 남기고 표면 품질과 표시 대상을 별도로 관리한다.

## 허용 경로

- Unity `Assets/Ssalddel/Presentation/World/사가정상세건물표현*`, `사가정상세점포ModelBuilder*` 및 해당 집중 시험/`.meta`.
- 새 Editor 상세 시안 Capture 또는 기존 `사가정영상선분디오라마DemoCapture.cs`의 opt-in 최소 연결과 필요한 좁은 회귀 시험. 기존 `사가정비공개현실감Style`은 새 모델에 적용할 때 필요한 최소 연결만 허용한다.
- 새 `Assets/Ssalddel/Editor/사가정상세표면MaterialLibrary*`는 비공개 고정 영수증의 저해상도 CC0 재질을 메모리에서만 읽어 새 URP 재질을 만드는 좁은 adapter로 허용한다.
- 배달 `edit-kit/private/detailed-building-study/20261026-r1/`의 실행 입력·현재 검증·자료 영수증·사본/텍스처·렌더·비교·실행 보고. 소규모 외부 재질은 공식 공개 CC0 출처·라이선스·파일 크기 확인을 갖춘 저해상도 세트만 후보로 쓴다. 계정·유료 설치·대량 수집은 포함하지 않는다.
- 본 PLAN·Changes·현재 작업·기획 목차·기존 PLAN README.

원본 영상·이전 분석/포인터·컷/마커·사용자 검토/승인·기존 원형·Scene·Synty/Material/Prefabs·URP/품질 설정·운영 DB·공개 상태는 보존한다. 새 공식 Scene·HDRP/Addressables 설치·전체 엔진 이관·자동 3D 완전 복원·실측 확정·게시·commit/push는 이번 단위에 포함하지 않는다.

## 완료 기준

상세 모델 준비 전 원형 유지, 교체 후 중복 표시 없음, 거리·실패·해제의 원형 복구, 다른 건물 영향 없음과 소유 자원 정리를 자동 시험한다. 실제 원본 stat/기존 SHA·현재 보호·정수 PTS와 동일 원본 공개 외관 근거를 다시 확인한다. 소형 부품 기존 회귀도 함께 실행한다.

실제 Unity 그래픽 CLI의 조감/낮은 원근 화면을 비교하고 개선 모델의 비어 있음·카메라·깊이·문자/색·가림·겹침·오류 로그·원본 보존을 확인한다. 현재 실제 Editor/Pipeline tool 연결은 없어 CLI PreviewScene 카메라 렌더를 사용하며 Game View·Play Mode·실시간 FPS·정본 적용과 구분한다. 성능 예산·측정 지점을 준비하되 정적 렌더 시간으로 운행 FPS를 주장하지 않는다.

## 근거와 후보

지금 모형은 footprint 압출·기호 팔레트·규칙 창을 사용하고, 소형 외관 Renderer는 상세 건물 전체용이 아니다. 이 한 사례를 이유로 다른 역/지역의 공통 규칙을 자동 적용하거나 E 단계를 승격하지 않는다. 실제 결과·반례·결손과 규칙 후보 유무는 종료 시 별도로 기록한다.

## 실제 마감

세 점포 전면 모델·기존 ID/Profile를 재사용한 상세 교체·상호 배타적 50/140m 표시 전환·실패/제거 복구를 구현했다. 실제 EditMode143/143, 후속 그래픽 r4 종료0·기존20+상세12PNG·4시점 정확한 원형 복구·cleanup·원본14파일 불변을 확인했다. 간판 반전과 미관측 몸체의 과한 오염 무늬를 최종 그래픽에서 고쳤다. 16초/480프레임 무음 비교 영상·HTML을 만들고 파생 전체 디코딩과 대표 화면을 확인했다.

초기 실패·수정·자료 출처·범위 상한은 [r114 변경 기록](../../../../Changes/2026-10-26-detailed-building-representation-r114.md), 비공개 결과는 배달 `edit-kit/private/detailed-building-study/current-review.json`을 따른다. 실측 높이·건물 전체 복원·투명 실내·비동기 메모리 스트리밍·실제 Game View/FPS·정본/운영 적용은 남았다. 새 공통 디오라마 규칙 후보 없음.
