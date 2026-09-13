# 동네 지도·경로 사전 검사

실제 GIS 전처리 결과를 받기 위한 **후보 검증기**다. 현재 제공하는 표본은 합성 좌표이며 면목동 지도가 아니다. 서버 실행·배차 확정·Unity 배치를 하지 않는다. [기획과 남은 작업](../../docs/AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/implementation.md).

## 중랑구 색인·면목동 배치 참고 지도

수집·재조회가 끝난 비공개 자료를 개별 행 그대로 Git에 복제하지 않고, 중랑구 상위 색인과 면목동 500m 타일별 집계로 정리한다. 기존 도형 동네는 별도 가상 시나리오로 유지한다.

```powershell
$env:SSALDDEL_UNITY_ROOT = '<Unity 프로젝트 루트>'
pwsh -NoProfile -File eng/world-seedbeds/manage-myeonmok-reference-maps.ps1 -Mode Check
pwsh -NoProfile -File eng/tests/myeonmok-reference-maps.ps1
```

- [자료 판본 대장](../world-seedbeds/map-source-manifests/jungnang-myeonmok.v1.json)
- [Graph Map](../world-seedbeds/graph-maps/jungnang-myeonmok-reference.v1.json)
- [배치 Map](../world-seedbeds/placement-map-profiles/jungnang-myeonmok-reference.v1.json)
- [결과와 한계](../../docs/AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/regional-reference-map-result.r1.md)

이 검사는 DB·외부 API·Unity Scene을 변경하지 않는다. 상가 좌표는 측지 기준이 명시적으로 확인되기 전까지 `ReviewRequired`, 주소 연결은 입주가 확인되기 전까지 `Candidate`다.

## 실행

저장소 루트의 PowerShell에서:

```powershell
$지도입력 = 'eng/neighborhood/fixtures/synthetic-block.v1.json'
$검토한해시 = (Get-FileHash -LiteralPath $지도입력 -Algorithm SHA256).Hash
dotnet run --project eng/Ssalddel.NeighborhoodPreflight -- $지도입력 $검토한해시 depot restaurant-stop Vehicle
dotnet run --project eng/Ssalddel.NeighborhoodPreflight -- $지도입력 $검토한해시 restaurant-stop restaurant-door Pedestrian
```

차량 후보 200m, 도보 후보 5m를 반환한다. 종료 코드 0은 후보 탐색 성공, 2는 경로 없음, 1은 입력/판본 오류, 64는 명령 형식 오류다. 성공이어도 `runtimeAuthorized`는 항상 `false`다. 실제 파일은 검토할 때 기록한 해시를 다음 실행에도 사용해야 변경을 발견할 수 있다. 실행할 때마다 다시 계산한 해시만 넣으면 변경 승인 검사가 되지 않는다.

## 입력 계약 `neighborhood-geography.v1`

### 작은 배달 동네 표본

`fixtures/synthetic-delivery-block.v1.json`은 [승인된 합성 공간](../../docs/AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/synthetic-spatial-study.r1.md)의 건물3·노드9·도로8을 담는다. 실제 면목동이나 Unity Scene이 아니다. 기존 큰 단위 시험 표본은 유지한다.

```powershell
$배달지도 = 'eng/neighborhood/fixtures/synthetic-delivery-block.v1.json'
$고정해시 = '56EA63D55FC720B2348B7A310B0FF4DBBE96F41C392EDA6E270CD87B9BFEAECF'
dotnet run --project eng/Ssalddel.NeighborhoodPreflight -- $배달지도 $고정해시 restaurant-stop residence-a-stop Vehicle
dotnet run --project eng/Ssalddel.NeighborhoodPreflight -- $배달지도 $고정해시 residence-a-stop residence-a-door Pedestrian
dotnet run --project eng/Ssalddel.NeighborhoodPreflight -- $배달지도 $고정해시 residence-a-stop depot Vehicle
```

각각 차량40m·도보4m·복귀50m 후보다. 새 `동네이동진행Engine`은 경로 지문에 묶인 불변 진행 후보를 차량5m/도보1m씩 계산한다. 차단 중 위치를 유지하며 중복 Tick의 내용 충돌·다른 경로 복원·유효하지 않은 거리/시간은 거부한다. `관찰시간누적기`는 재생 중 1초당 최대 한 Tick 요청 여부만 반환한다. 두 모듈 모두 기존 Runtime 명령·주문·기사·Unity에 아직 연결하지 않았다. 지원 모듈 복원은 실제 Session Save/Replay 증거가 아니다. [작업 범위](../../docs/AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/movement-support.r1.md).

### 형식과 제한

GeoJSON의 도형 표기와 닮았지만 **RFC 7946 GeoJSON이 아닌 전처리 후 전용 JSON**이다. SHP/NGI를 이 도구에 직접 넣거나 경위도에 미터 단위만 붙여 사용하지 않는다. 누락된 재투영·클리핑·원본 권리 검토를 자동으로 보충하지 않는다.

- `provenance`: 출처/데이터셋 ID, 원본 판본·UTC 기준 시각·원본 해시, 원본 CRS, 전처리 CRS, 정규화·권리 근거 참조, 권리 검토 상태, `PublicData` 또는 `SyntheticFixture`.
- 실제 자료의 전처리 좌표계는 현재 `EPSG:5186`만 지원한다. 원본 CRS는 `.prj` 등에서 별도로 확인한다. 합성 표본은 `LOCAL:SYNTHETIC`만 사용한다. 이 검사기는 전처리 CRS 선언의 정확성이나 근거 문서의 진위를 인증하지 않는다.
- `coordinates`: `unit=m`, `axisOrder=EastingNorthing`, 투영 좌표의 `origin=[E,N]`, `bounds=[minE,minN,maxE,maxN]`. 지역 좌표는 `X=E-originE`, `Z=N-originN`. 높이는 만들지 않는다. 범위 밖 점은 자르거나 옮기지 않고 거부한다. 이미 잘린 면적과 연결은 전처리/공간 검토의 책임이다.
- `features`: 전체에서 고유한 `id`, `kind`, `geometry`, `properties`. 노드는 `Point`, 도로는 `LineString`, 건물은 단일 외곽 고리 `Polygon`이다. 내곽/복합 도형은 현재 거부한다. 내곽을 채우거나 건물을 연결하는 자동 단순화는 금지한다.
- 노드 역할은 `Junction`, `VehicleStop`, `Entrance`. `Entrance`만 실제 포함된 건물 ID를 명시한다. 이것은 논리 연결이며 실제 접근 가능성을 입증하지 않는다.
- 도로는 `fromNodeId/toNodeId`, `direction=Unknown|Forward|Both`, `modes=[Vehicle|Pedestrian|Motorcycle]`, `accessReview=Unknown|Reviewed|Blocked`, 검토 근거를 담는다. Reviewed만 근거가 필수다. 방향 Unknown 또는 검토 Unknown/Blocked는 탐색에서 제외한다. 차량과 오토바이는 Entrance에 연결할 수 없다.
- 도로 양끝과 노드 좌표는 정확히 같아야 한다. 교차하거나 가까운 선을 자동 연결하지 않는다. 알려지지 않은 건물 높이·도로 폭은 `null`로 유지한다.
- 입력 4MiB, 최대 5,000 도형·100,000 점·도형당 512점, 범위 가로/세로 각각 최대 1,000m. 대량/전국 데이터 수집기가 아니다.

정규화 **파일 바이트 해시**는 호출자가 제공한 값과 실제 대조하고 경로 판본으로 사용한다. 원본 해시는 형식·보존만 검사한다. 실제 원본 파일과의 대조, DB 저장/재조회·중복 방지, 라이선스 근거 검토는 기존 수집 경계에서 따로 수행해야 한다. 합성 fixture의 `AAAA…` 원본 해시는 시험용 토큰이며 원본 확보 근거가 아니다.

## 다음 연결

원본/권리 확인 → 비공개 원장 저장·재조회 → 실제 CRS 재투영/경계 자르기 → 도형 입력 검사 → 출입구/정차/도로 방향·지형 연구 승인 → 단일 WI별 Core 도달/픽업/수령/귀환 상태·Save/Replay → 기존 Unity 경로 표현. 후보 길찾기는 2D 길이 기반이며 경사·차폭·실제 교통·안전 통행을 인증하지 않는다. 개별 Graph Map 원본·기존 H·Scene은 변경하지 않는다.

## 사가정 1km 정적 이동망 검토 후보

동결 OSM 원본을 기존 사가정 디오라마 좌표계에 맞춘 500m `2×2` 타일로 전처리한다. 결과는 Git 제외 `artifacts/local/sagajeong-mobility-graph/`에만 만들며, 서버의 개발 환경·관리자 전용 비공개 미리보기가 이 파일을 엄격히 재검증해 읽는다.

```powershell
pwsh -NoProfile -File eng/neighborhood/manage-sagajeong-mobility-graph.ps1 -Mode Build
pwsh -NoProfile -File eng/neighborhood/manage-sagajeong-mobility-graph.ps1 -Mode Audit
pwsh -NoProfile -File eng/neighborhood/manage-sagajeong-mobility-graph.ps1 -Mode SelfTest
```

- 계약은 `region-mobility-graph-manifest.v1`과 `region-mobility-graph-tile.v1`이다. 지역 범위는 단일 행정동이 아니라 `world-region:kr:seoul:jungnang:sagajeong.r1` 1km 창이고 행정동 ID는 분석 참조일 뿐이다.
- 현재 동결 입력 결과는 4타일·노드 2,807개·간선 2,574개이고 projection hash는 `59DD155C0D49BEB0612062DF6E1571A751FE0EC1E9DA4AD4AF41A879EB4FE6F7`이다.
- OSM의 명시적 `oneway`만 방향으로 인정하고 나머지 2,430개 간선은 방향 미확정으로 보존한다. 타일 사이 stitch와 1km 외곽 portal을 구분하며 가까운 선·건물·출입구를 자동 연결하지 않는다.
- 음식점 후보 2곳의 도로 접근 거리는 검토 자료일 뿐이고 생성 connector는 0개다. `PendingHumanReview`, `distributionApproved=false`, `traversalReady=false`, `runtimeAuthorized=false`이므로 실제 주문·배차·Unity 이동 권위에 사용할 수 없다.

[사가정 가상 배달 이동 기획](../../docs/AI/Planning/공간/PLAN-SPATIAL-SAGAJEONG-DELIVERY-MOBILITY/README.md)은 이 정적 후보와 별도의 합성 7구간 여정 상태 사본을 연결한다. 결손 시 건물 중심이나 직선으로 건너뛰지 않으며 실제 접근점 검토와 별도 권위 WI 전에는 읽기 전용 관찰 표현으로만 사용한다.

## 비공개 원본 등록

`Ssalddel.NeighborhoodSourceImport`는 위 지도 후보 검사기와 다르다. [확보 기록](../../docs/AI/Planning/시스템/PLAN-SYSTEM-MYEONMOK-OBSERVER/source-acquisition.md)의 **승인된 두 원본 파일과 해시, `hongdal-mysql-1 / hongdal_dev`**만 다룬다. 다른 파일·판본·DB에는 재사용하지 않는다. `apply`는 실제 DB 쓰기이므로 대상과 범위의 사용자 승인을 확인한 뒤 실행한다.

```powershell
$저장소경로 = 'C:\Users\user\source\repos\Hongdal'
dotnet run --project eng/Ssalddel.NeighborhoodSourceImport -- preview $저장소경로
# 원본 두 파일·대상 DB의 저장 승인이 있는 경우에만:
dotnet run --project eng/Ssalddel.NeighborhoodSourceImport -- apply $저장소경로
dotnet run --project eng/Ssalddel.NeighborhoodSourceImport -- verify $저장소경로
```

- `preview`/`verify`는 읽기 전용이다. `apply`는 원본 두 개의 계보를 한 트랜잭션으로 등록한다. 같은 파일 재입력은 기존 기록의 마지막 확인 시각만 갱신하며 새 행을 만들지 않는다.
- 파일 잠금·크기·SHA-256·컨테이너 소속·포트·DB 이름을 대조한다. 지정 컨테이너의 비-root 연결값은 메모리에서만 사용하고 출력하지 않는다. 새 DB·migration·기본 서버 설정 변경은 하지 않는다.
- 원본 바이트는 Git 제외 로컬 폴더, 계보는 DB에 저장한다. `Partial / NeighborhoodSourceReviewPending`을 유지하며 정규화·건축물 원장 연결이 생겼으면 검증을 실패시킨다. 이 도구는 SHP/DBF 변환기나 건물 마스터 적재기가 아니다.
- 종료 코드 0은 해당 모드 검사 성공, 1은 차단/실패, 64는 명령 형식 오류다. 실패 JSON의 `committed`를 확인한다. 저장 후 독립 조회에서 실패했다면 `committed=true`일 수 있으므로 미저장으로 단정하지 않는다. `runtimeAuthorized`는 항상 `false`다.

## 사가정 공간 표현 오버레이

`manage-sagajeong-spatial-presentation.ps1`은 동결된 `AL_D010_11_20260809.zip`과 `map.osm`을 읽어 1km 정사각형 안의 **표현 후보**를 만든다. GIS 건물은 `.prj`의 EPSG:5186 선언을 확인한 뒤 `EPSG:5186 → WGS84 → WGS84-ECEF-ENU-at-zero-altitude` 순서로 기존 `SagajeongReference.json` r3 좌표계에 맞추고 경계를 실제로 자른다. OSM 토지표현 다각형도 같은 경계로 자른다. SHP·DBF 원본 행, 주소, 로컬 경로는 결과에 복제하지 않는다.

```powershell
$env:SSALDDEL_UNITY_ROOT = 'C:\path\to\ssalddel'
pwsh -NoProfile -File eng/neighborhood/manage-sagajeong-spatial-presentation.ps1 -Mode PrivateReview
pwsh -NoProfile -File eng/neighborhood/audit-sagajeong-spatial-presentation.ps1
pwsh -NoProfile -File eng/tests/sagajeong-spatial-presentation.ps1
```

`SSALDDEL_UNITY_ROOT` 대신 `-BaseMapPath`를 명시해도 된다. 저장소에 특정 사용자 Unity 절대 경로를 고정하지 않는다.

기본 결과는 Git 제외 경로다.

- `artifacts/local/sagajeong-spatial-presentation/private-review.json`
- `artifacts/local/sagajeong-spatial-presentation/coverage-audit.json`
- `artifacts/local/sagajeong-spatial-presentation/coverage-audit.html`

오버레이 계약은 `ssalddel.spatial-presentation-overlay.v1`, 판본은 `sagajeong-spatial-presentation.private-review.r2`, 상태는 `LocalPrivateReview`다. `contentHash`를 빈 문자열로 바꾼 객체를 키 정렬·공백 없음·UTF-8 JSON으로 직렬화한 SHA-256 대문자 값을 `contentHash`로 기록한다. 좌표는 소수 셋째 자리까지 고정하고 건물·표현면·고유 식별자를 정렬하므로 같은 동결 입력은 같은 JSON과 hash를 만든다.

건물 높이는 A16의 양수만 `ObservedSourceHeight`로 사용한다. 0·누락·비수치는 층수로 환산하지 않고 `SymbolicFallback4m`로 유지하며, A16과 A26 관측값·높이 정책 판본을 별도 필드로 남긴다. 기존 OSM 건물과의 footprint 겹침은 `legacyOsmIds`/`legacyMatch` 후보일 뿐 공식 동일 건물 확정이 아니다. `IoU >= 0.20`, 작은/큰 footprint 면적비 `>= 0.25`, 작은 footprint coverage `>= 0.30`, 정규화 중심거리 관문을 모두 통과해야 단일 별칭 후보가 된다. 약한 후보는 `WeakFootprintCandidateExcluded`, 중복 후보는 기존 ambiguity method로 기록하고 둘 다 별칭을 비운다.

감사는 생성기 결과를 다시 만들거나 그대로 신뢰하지 않는다. ZIP 안 SHP/DBF와 OSM XML을 별도로 재파싱·재투영해 `sourceFeatureId`, A8/A9/A16/A26, 정규 다각형 hash, OSM 별칭 존재, 실제 footprint overlap 수치를 전수 대조한다. 자체 hash를 다시 계산한 변조 파일도 원본 대조에서 거부한다.

coverage audit v2는 1km를 100m `10×10`으로 나누고 각 셀의 `buildingCoverageRatio`, 건물 영역을 제외한 `openCoverageRatio`, 나머지 `unknownCoverageRatio`를 기록한다. 건물 면적이 20% 이상일 때만 `ConfirmedBuilt`, 완전한 표면 근거에서 열린 면적이 20% 이상일 때만 `ConfirmedOpen`이다. 불완전 OSM 표면이 닿으면 건물 20% 미만 셀을 `IncompleteSurfaceEvidence`, 그 밖을 `MissingCoverage`로 둔다. `osm:relation:14428122`처럼 bbox 원본에서 member가 빠진 feature, 빠진 member ID와 영향 셀을 감사 JSON에 영속한다. 이 분류는 표현 자료 coverage이며 실제 건물·공터·통행·배치 허가 판정이 아니다. `evidenceMix`와 `boundaryClipped`는 별도 지표로 보존한다. 독립 대조가 통과했지만 누락 표면이 영향 범위에 있으면 감사 상태는 `PassedWithIncompleteSurfaceEvidence`, 없으면 `Passed`다. 대조 실패 시 감사 파일을 게시하지 않는다.

`-Mode Promote`는 기본 결과를 덮지 않고 `public.json`을 사용한다. 별도 권리 영수증이 다음을 모두 만족하기 전에 **출력 생성 전 실패**한다.

```json
{
  "datasetId": "data-go-kr-15083092",
  "rawHash": "674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755",
  "status": "RightsReconciled",
  "licenseCode": "KOGL-Type1",
  "sourceIdentityVerified": true,
  "acquisitionReceipt": {
    "provider": "국토교통부",
    "datasetId": "data-go-kr-15083092",
    "datasetDate": "2026-08-09",
    "fetchedAtUtc": "2026-09-06T14:42:52.765Z",
    "rawHash": "674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755",
    "rawLength": 135675376,
    "originalCrs": "EPSG:5186"
  },
  "reviewer": "<검토자 고유 식별자>",
  "reviewedAtUtc": "<UTC ISO-8601 시각>",
  "evidenceHash": "<evidenceHash만 빈 문자열로 둔 전체 객체의 canonical SHA-256>"
}
```

승격은 manager만 수행한다. builder는 같은 디렉터리에 `PromotionPendingAudit` 임시 후보만 만들고, 독립 원본 감사가 성공하면 auditor가 승인 임시 파일을 만든다. manager가 감사 JSON/HTML을 먼저 교체하고 승인 임시 파일을 같은 디렉터리의 최종 경로로 원자 이동한 때만 `sagajeong-spatial-presentation.public.r1`, `PublicOpenData`, `distributionApproved=true`가 성립한다. 실패 시 임시 파일을 제거하고 기존 최종 파일은 바꾸지 않는다. 어느 모드도 Unity 후보명 `SagajeongSpatialPresentationOverlay.json`으로 자동 복사하지 않으며 Scene·Prefab·서버·DB·게임 상태를 변경하지 않는다.
