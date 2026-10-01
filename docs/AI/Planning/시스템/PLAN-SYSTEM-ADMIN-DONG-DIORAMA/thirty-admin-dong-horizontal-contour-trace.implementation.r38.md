# 30개 행정동 수평 등고선 흔적 비공개 검토

[구현·검증 기록 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r38]

- 상태: `HorizontalContourTraceGenerationVerified / PrivateHistoricalFlatHorizontalContourTraceGameViewCaptured`
- 기준: [지형 높이 공식 규정 근거 심화 감사 r36](thirty-admin-dong-terrain-official-standard-evidence.audit.r36.md), [차선표시 선형 비공개 후보·Unity 검토 r37](thirty-admin-dong-lane-marking-private-generation.implementation.r37.md)
- 공식 자료: [서울 열린데이터광장 OA-22241](https://data.seoul.go.kr/dataList/OA-22241/F/1/datasetView.do)
- 대상: `scope:administrative-dong-diorama:northeast-seoul-rider:r2`의 정확한 30개 역사 행정동 후보

## 구현 결론

정밀화 후보를 다시 비교한 결과, 현재 로컬 자료에서 새 geometry를 안전하게 추가할 수 있는 층은 `OA-22241`의 2025 등고선 SHP에 있는 **수평 선형 흔적**이었다. 도로 실폭·도로경계는 도로명 텍스트와 NodeLink 중심선·차로 수만 있어 물리 폭으로 만들 수 없고, 횡단보도 1,533점·교차로 554점은 이미 기존 입력에 있다. 신호·정지선 원본은 없고, 가로수는 과거 manifest만 남아 30개 동 원본을 재현할 수 없다.

이번 생성기는 `N3L_F001`의 SHP·SHX·PRJ만 열고 DBF는 열지 않는다. `CONT`, `HEIGHT` 또는 다른 속성값은 identity·clip·출력·표현에 들어가지 않는다. `EPSG:5174` PolyLine geometry를 2023 역사 행정동 경계로 자른 평면 흔적일 뿐, 등고값·높이·고도·경사·지형 표면·Collider 의미가 아니다. r36의 수직 메타데이터와 전용 terrain consumer 차단도 그대로 유지한다.

## 원본·판본 고정

| 입력 | 판본·경로 | SHA-256 |
| --- | --- | --- |
| 등고선 ZIP | `artifacts/local/public-data/admin-dong-terrain-contour-20260926-r1/raw/OA-22241-seoul-contour.zip` | `4FBE3C7E061B5974E7403EC116855304ED8AE321EEBCC0D12C31CA8FB7BE30BF` |
| 획득 receipt | `artifacts/local/public-data/admin-dong-terrain-contour-20260926-r1/raw/receipt.json` | `8911948581B53B07BF59BDE353EA2743948A9733B6EA24F341FB4364496A345D` |
| 역사 경계 | `artifacts/local/public-data/admin-dong/20260912-seoul-oa22160/seoul-administrative-dong-boundary.zip` | `969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68` |

공식 파일은 2025-03-20 `서울시 등고선.zip`, 45,852,601 bytes이고 내부 `N3L_F001`은 8,570개 PolyLine과 `EPSG:5174`를 가진다. 공식 페이지는 2023년 기준 수치지형도에서 표고점·등고선 SHP를 제공한다고 설명하지만, 이번 층은 그 높이 속성을 의도적으로 읽지 않는다.

## 원천 생성과 도형 감사

- 생성기: `eng/neighborhood/administrative_dong_contour_trace_private_review_r1.py`
- 생성기 SHA-256: `1EE0CAC26922F7CB15F5B7BB6FB5B3A232064604BAF2AEB67610EDB6D09F491C`
- 원천 generation: `E5FC9551CEE5BB2C1DA8696939C882BE3141F0736AF9E36B945F9184C152CDFC`
- manifest SHA-256: `BAC3398814FEE00EF41FBFC1B974E47BA8E28902A0857708A2D5F093282C9A4C`
- audit SHA-256: `82B8F49766FD3FE3CBE11D4472691EE566D17ED59834E917856769265066E6F3`
- completion SHA-256: `83B866184B7056E215314DDA5CFBC5779201B5E488E29C2026224250DDB20A38`

| 감사 항목 | 결과 |
| --- | ---: |
| 원천 PolyLine | 8,570 |
| 범위 bbox 후보 | 513 |
| 역사 범위와 교차한 원천 feature | 377 |
| 원천 part / 점 / 선분 | 476 / 306,756 / 306,280 |
| 행정동 교차·배정 | 860 / 860 |
| clip 직후 fragment / 점 / 선분 | 1,277 / 186,025 / 184,748 |
| 원천 CRS 경계 포함 실패 | 0 |
| 공통 ENU 양자화 뒤 경계 밖 점 | 910, 최대 6mm |
| 1mm 이하 생략 | 1 |
| 물리 중복 선분 instance | 0 |
| 표시 후보 / 선분 / path 점 | 860 / 184,747 / 369,494 |
| 행정동 coverage | 30 / 30 |

경계 귀속은 원본 `EPSG:5174`에서 엄격하게 검사해 이탈 0개를 확인했다. WGS84-ECEF-ENU 변환과 1mm 양자화 뒤에는 변환된 역사 경계를 최대 6mm 벗어난 점 910개가 생겼다. 이를 원본 경계 실패로 합치거나 조용히 삭제하지 않고 별도 수치 오차로 기록했다. 표시 선분 등식은 `184,748 = 184,747 + 1 + 0`이다.

원천 stable ID는 원본 순번·part·행정동·판본·geometry를 hash해 만들며 DBF 원문 식별자나 높이값을 쓰지 않는다. 후보 payload에 `height`·`elevation` 필드가 없음을 자체검사한다.

## r7 결합 입력

결합 생성기 `eng/neighborhood/administrative_dong_contour_trace_unity_review_r7.py`는 v6 차선 검토 입력을 그대로 보존하고 수평 선형 흔적만 추가한다.

- 결합 generation: `13A3B1C6D7854D1A4A03BFDFDD7766966D9B4B447F3E7FB3C662555707C7B753`
- index SHA-256: `C9A43A36BFF6F3EB0664D4F16507E49BC8491D15D4B73CB76C3BF3D7E98785E9`
- completion SHA-256: `26974318DEB1731EF27141A17988815CECE3AF97EC857E5C9B84D7C3F3A5CC8D`
- wrapper audit SHA-256: `D8F2EBFFF77418BAA05396D0EF13E8222B1FBD4A0126A366E90159C7F303B65D`
- exporter SHA-256: `168721AABE97B3B2B6C64985500ABDAC5E1B779E8DB104C0C302C13EAC031E2B`
- 결합 입력: 36 files, 179,363,531 bytes

원천 생성기는 self-test 8/8, 결합 생성기는 self-test 37/37을 통과했다. 양쪽 모두 build·verify·동일 입력 재실행이 통과했고 재실행 `changedFiles=0`이다. 30개 bundle, index, 기존 audit 세 개, 새 contour audit와 completion의 hash 결속을 다시 확인했다.

## Unity 적용·실행 증거

Unity `6000.5.6f1` 실제 Play Mode·Game View에서 30개 overview와 면목제3·8동 detail 1장을 확인했다. 최종 증거는 `C:/Users/user/ssalddel/Documentation/Changes/2026-09-26-administrative-dong-private-horizontal-contour-gameview-v8-13a3b1c6/`에 보존한다. PNG 31개의 manifest hash 불일치는 0개다.

- 증거 schema: `administrative-dong-diorama-gameview-evidence.v8`
- manifest SHA-256: `E7312015B116096F42DD9CDA054CF81C043BC66249F5DBB48C97CAC58689767D`
- 평면 선형 정점/인덱스: 738,988 / 1,108,482
- 전체 Mesh 정점/인덱스: 3,674,084 / 5,718,744
- 모듈 최대 정점: 288,423, batch 최대 정점: 114,556 / 허용 500,000
- 모든 모듈 Renderer 4·MeshFilter 4·Collider 0
- Unity 집중 EditMode 시험 44/44 통과, 최종 증거 폴더 XML SHA-256 `D06292BFAC0FEBA807066443E759C1B66A9503A440B649AF01C6B0ACE646C0AD`

선분은 고정 평면 높이의 quad로 기존 2×2 결합 Mesh에 들어간다. v1~v6 호환과 이전 입력으로 교체할 때의 상태 초기화를 검증했다. 최대 선분 대상 중곡제4동과 면목제3·8동 overview·detail을 직접 확인했고 핵심 HUD 문구와 도형이 화면 안에 표시된다.

canonical Scene SHA-256은 실행 전후 `C31167703C4A7683DA374E237CBA3C9584A5F1DE4B5FB5746939A3632635AC65`로 같고, 저장하지 않았으며 종료 뒤 dirty 상태가 아니다. 기존 Console 오류 8건과 첫 `SimulationConflictException: SimulationReplayHashMismatch`는 남아 있다.

사용자의 후속 범위 축소에 따라 이 완료 세대는 보존하고 30개 일괄 확장·재캡처를 기본 반복 작업으로 삼지 않는다. 다음 작업 범위는 [소수 지역 집중 정밀화 r39](focused-area-refinement.decision.r39.md)가 소유한다.

현재 권위는 `sourceHorizontalGeometryObserved=true`와 비공개 검토 표현만 허용한다. `sourceAttributeDatabaseRead`, `sourceElevationValuesRead`, `contourMeaningAuthorized`, `elevationMeaningAuthorized`, `heightUnitVerified`, `verticalDatumVerified`, `terrainSurfaceAuthorized`, `terrainMeshAuthorized`, Collider·공개·DB·Mongo·current pointer·Runtime·Traversal·Gameplay는 모두 `false`다. commit과 push는 수행하지 않았다. **새 디오라마 규칙 후보 없음**.
