# 30개 행정동 차선표시 선형 비공개 후보·Unity 검토

[구현·검증 기록 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r37]

- 상태: `PrivateHistoricalLaneMarkingOutlineGameViewCaptured`
- 기준: [Unity 검토 화면 가독성 개선 r34](thirty-admin-dong-unity-review-readability.implementation.r34.md), [비오톱 비공개 후보·Unity 검토 r32](thirty-admin-dong-biotope-private-generation.implementation.r32.md)
- 공식 자료: [서울 열린데이터광장 OA-15537](https://data.seoul.go.kr/dataList/OA-15537/A/1/datasetView.do)
- 대상: `scope:administrative-dong-diorama:northeast-seoul-rider:r2`의 정확한 30개 역사 행정동 후보

## 구현 결론

서울시 차선 관련 정보의 `A058_L_차선.zip` 선형을 2023년 역사 행정동 경계 30개로 자르고, 공통 WGS84-ECEF-ENU millimeter 좌표에서 1mm 이하 선분과 방향을 뒤집어도 같은 물리 선분인 중복을 분리해 감사했다. 표시 가능한 선분은 기존 도로·건물·보행망·방향표시·비오톱 위에 비공개 윤곽으로 추가했다.

공식 페이지의 데이터셋 갱신일은 2026-09-01이지만 사용한 SHP ZIP의 수정일은 2021-08-09다. 따라서 이 결과는 2021년 파일에서 얻은 **역사적 차선표시 선형 후보**다. 현재 차선, 차로 수, 진행 방향, 통행 가능성, 정지선, 신호 또는 안전 의미를 부여하지 않았다.

## 원본·판본 고정

| 입력 | 판본·경로 | SHA-256 |
| --- | --- | --- |
| 차선 ZIP | `artifacts/local/public-data/admin-dong-lane-marking-20260926-r1/raw/A058_L_차선.zip` | `6A930A8B70B76F6FD0E2EE5576F2CA666620396AC3653E9A7CA28BDEC8A00120` |
| 획득 receipt | `artifacts/local/public-data/admin-dong-lane-marking-20260926-r1/receipt.json` | `DEBE451F2120C3C14E2EDB480E46CF28E15E0E72380F7146DC52227484C33E58` |
| source manifest | `artifacts/local/public-data/admin-dong-lane-marking-20260926-r1/source-manifest.json` | `1653911737093B53541245C89017AAFBB94E13D1340848BFDBC8F2B5C664BFB6` |
| 역사 경계 | `artifacts/local/public-data/admin-dong/20260912-seoul-oa22160/seoul-administrative-dong-boundary.zip` | `969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68` |

차선 원본은 27,185,849 bytes, 341,957개 DBF 행, `EPSG:5186`이다. 공식 페이지는 좌표계를 `EPSG:5186`으로 설명하며, 원본 제공기관은 서울특별시이고 이용허락은 공공누리 제1유형이다. 기존 `.acquiring-*` 사본은 수정하지 않고 동일 hash의 불변 입력을 별도 로컬 폴더에 결속했다.

## 비공개 후보 생성과 선분 감사

- 원천 생성기: `eng/neighborhood/administrative_dong_lane_marking_private_review_r1.py`
- 생성기 SHA-256: `62BF34863FAB73BAF1862F300F3B171FFCC2C079BC20E002CA2DC16FED97075C`
- 원천 generation: `E0430AAAD4D126086F760BB089AD4687B29B7A3930C6CABF0FFA7B672E729C1C`
- manifest SHA-256: `42067E9BEDDFD99D09490F258B905F86BA57BFEC454640A17CAFD035EC5E2899`
- audit SHA-256: `69A3C1BFC74810B871C79311E2F68398DBA1A2F830FD701D3185514249A31CFC`
- completion SHA-256: `CA16EA50C13F0E73247479CF4DAA1257B601522B01D517B34FBCDF004EDE18D4`

| 감사 항목 | 결과 |
| --- | ---: |
| 원천 행 | 341,957 |
| 역사 범위와 교차한 feature | 22,447 |
| 행정동 배정 | 23,379 |
| clip fragment | 23,632 |
| 절단 path 점 | 84,278 |
| 원천 절단 선분 | 60,646 |
| 1mm 이하 생략 | 2 |
| 물리 중복 선분 instance | 58 |
| Unity 전달 후보 | 23,360 |
| 표시 선분 | 60,586 |
| 표시 path 점 | 121,172 |
| 행정동 coverage | 30 / 30 |

`60,646 = 60,586 + 2 + 58`을 원천 audit, 결합 index·audit·completion, 30개 bundle과 Unity 캡처 manifest에서 각각 다시 확인했다. 관리번호 반복값 22개와 추가 occurrence 24개가 있었고, 그중 10개 반복 관리번호는 서로 다른 geometry를 가졌다. 원문 관리번호를 stable ID로 쓰지 않고 판본·원본 순번·geometry·행정동을 hash한 ID만 전달했다.

초기 탐색의 `3 short + 57 duplicate`는 실제 공통 ENU millimeter 좌표로 판정하면서 `2 short + 58 duplicate`로 한 건의 분류가 이동했다. 전체 생략 수와 표시 선분 수는 변하지 않았으며 최종 generation의 수치만 정본으로 사용한다.

## v6 Unity 검토 입력과 실제 화면

결합 생성기 `eng/neighborhood/administrative_dong_lane_marking_unity_review_r6.py`는 v5 비오톱 입력을 그대로 보존하면서 차선 선형만 추가했다.

- 결합 generation: `587601D4E9952149DA299D84496242CAD3CC761F6053D2B36A1A73C5C6B0E3D6`
- index SHA-256: `4DDE2DD68B6246D6DB046FCAB81FA153D111288D887D4C4729B006DFEB6B7AAF`
- completion SHA-256: `3BF7524435AF29615D8CE1593C8F7BBEE02A8CD07007C55D5BEF0C1E2B9041C4`
- lane wrapper audit SHA-256: `F935FC3FC37E883BAC7D6DB6C9D3DDB60E6FC550E67B8F2A88ECB19E2B6697A3`
- exporter SHA-256: `09123CAD89BF1F27DC63C1390725AA661224334E51283F084934EE5F53B3288F`
- 결합 입력: 35 files, 123,952,501 bytes

Unity `6000.5.6f1`은 v1~v5 입력 호환을 유지하면서 v6 선형을 fail-closed로 읽는다. 각 2점 선분을 같은 폭의 quad로 만들고 기존 2×2 결합 Mesh 네 개에 넣었다. 새 GameObject·Renderer·MeshFilter·Collider는 만들지 않았다. lane 적용 뒤 v5 또는 기초 geometry로 교체하면 lane overlay와 집계를 0으로 정리한다.

실제 증거 폴더는 `C:/Users/user/ssalddel/Documentation/Changes/2026-09-26-administrative-dong-private-lane-marking-r6-587601d4/`다. 실제 Play Mode·Game View에서 overview 30장과 면목제3·8동 detail 1장을 만들었고, PNG 31개 hash 불일치는 0개다.

- evidence schema: `administrative-dong-diorama-gameview-evidence.v7`
- manifest SHA-256: `2D99A95DF1ACD3657740ADB56F51D71D1E8E6E5A1741D4D38EADBA79399CDB1C`
- 면목제3·8동 overview SHA-256: `9889B1AEEB7DF7560736C8A613D1324F131D6DC78B5690441DCA1384B5699A1C`
- 면목제3·8동 detail SHA-256: `BC3E5EAAB6316E670C7DEB7E552FE07B4B9A5AC656F10BE7A56321A4443B604D`
- 차선 정점/인덱스: 242,344 / 363,516
- 전체 실제 Mesh 정점/인덱스: 2,935,096 / 4,610,262
- 모듈 최대 정점: 194,459, batch 최대 정점: 93,328
- 30개 모듈 모두 Renderer 4·MeshFilter 4·Collider 0
- 집중 Unity EditMode 시험: 38/38 통과, XML SHA-256 `9FB44BDB615431F5A1A9ADBEDEA6EAAD98506CFCD95F13C825305F499A3ED232`

overview는 82px compact HUD에서 차선 후보 수와 `차선/방향/신호/보도 권위 없음`을 표시한다. detail은 전체 근거·깊이 슬롯·Mesh 집계를 유지한다. 깊이 슬롯은 8개, `Mobility=Blocked`, `LaneDirection` 미확인, `laneSignalSidewalkVerified=false`다.

canonical Scene SHA-256은 실행 전후 `C31167703C4A7683DA374E237CBA3C9584A5F1DE4B5FB5746939A3632635AC65`로 같고 Scene 저장 `false`, 종료 뒤 dirty `false`다. 기존 canonical Scene 오류 8건과 첫 `SimulationConflictException: SimulationReplayHashMismatch`가 다시 관찰됐으므로 Console 0이나 서버·DB·Mongo 통합을 주장하지 않는다.

## 권위 경계와 다음 정밀화

`laneOutlineAuthorized=true`는 이 비공개 검토 화면에 역사 선형 윤곽을 그릴 수 있다는 뜻뿐이다. `laneMeaningAuthorized`, `laneDirectionAuthorized`, `laneTrafficAuthorized`, `laneStopLineAuthorized`, `laneSignalAuthorized`, 공개·DB·Mongo·current pointer·Runtime·Traversal·Gameplay는 모두 `false`다.

다음 우선순위는 현재 자료가 실제로 제공하는 별도 geometry를 감사해 도로 폭·횡단보도 면·안전지대·보도 중 하나를 같은 방식으로 추가하는 것이다. 자료가 없거나 속성 의미가 불명확하면 중심선이나 차선에서 폭·보도를 추론하지 않는다. 지형 높이는 r36의 export-level 수직 메타데이터와 전용 consumer 관문을 계속 유지한다. commit과 push는 수행하지 않았다. **새 디오라마 규칙 후보 없음**.
