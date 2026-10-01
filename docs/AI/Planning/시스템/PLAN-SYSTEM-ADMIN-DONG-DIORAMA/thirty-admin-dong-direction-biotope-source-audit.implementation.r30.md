# 30개 행정동 방향표시·비오톱 공식 원본 감사

[자료 감사 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r30]

- 상태: `OfficialSourcesRecoveredAndHashed / Historical30DongCoverageAudited / CandidateGenerationNotStarted / UnityNotApplied / CurrentPublicationBlocked`
- 기준: [역사 보행망 Unity 비공개 검토 r27](thirty-admin-dong-walk-network-unity-review.implementation.r27.md), [지형 비공개 검토 세대 r29](thirty-admin-dong-terrain-private-generation.implementation.r29.md)
- 대상: `scope:administrative-dong-diorama:northeast-seoul-rider:r2`의 정확한 30개 역사 행정동 후보

## 감사 결론

다음 정밀화 자료로 [서울시 방향표시 관련 정보 OA-15536](https://data.seoul.go.kr/dataList/OA-15536/A/1/datasetView.do)과 [서울시 생태현황도 OA-21145](https://data.seoul.go.kr/dataList/OA-21145/A/1/datasetView.do)의 공식 최신 첨부를 확보해 길이·SHA-256·내부 형식·CRS·행 수를 검증했다. 두 자료 모두 기존 `OA-22160` 2023-10-31 역사 행정동 경계와 독립 대조했을 때 지정 30개 동에 후보가 있다.

이번 단계는 원본 보존과 coverage 감사다. 방향표시를 접근·주행 방향이나 차로 통행 권위로 바꾸지 않았고, 비오톱 분류를 생물종 현장 관찰·안전·통행·게임 규칙으로 바꾸지 않았다. 후보 generation, DB·Mongo·current pointer, 공개 표시, Unity 적용은 시작하지 않았다.

## OA-15536 방향표시

| 항목 | 결과 |
| --- | --- |
| 공식 판본 | `A055_P_방향표시_20260910.zip`, 데이터 기준 2026-09-10, 첨부 수정 2026-09-17 |
| 실제 길이 | `6,071,161 bytes` |
| ZIP SHA-256 | `641432B09B0C4A7AE38909873287BEE3E553B5C4F2CC0CD5394E03E7C36CCBAD` |
| 형식 | ESRI Point Shapefile, SHP·SHX·DBF 각 158,373건 |
| CRS·인코딩 | `EPSG:5186`, EUC-KR |
| 공식 정의서 | 98,494 bytes, SHA-256 `0BD49B6C8D02E4C1EB029DE641B2EF4A87A05135725B491FF47ACDC860F23C3C` |
| 이용허락 | 공공누리 제1유형 |
| receipt SHA-256 | `2BD67989B5ED1139BAB98F1CF84E7F4B1105504B854AF249DEB99E568D0881C1` |

포털 표시 용량은 5.93 MB지만 실제 HTTP 응답은 6,071,161 bytes라 양쪽을 receipt에 보존했다. 공식 정의서에 따르면 `DRN`은 방향표시 각도, `A055_KND_C`는 001~024 종류 코드, `LENX`는 방향표시 길이다. `LENX`의 단위는 정의서에 없으므로 실제 치수나 렌더 크기로 사용하지 않는다.

역사 경계에 유일 귀속된 점은 8,415개다. 광진구 1,001개, 동대문구 3,237개, 중랑구 4,177개이며 범위 밖 149,958개, 복수 경계 0개다. 30/30개 동에 최소 한 점이 있다.

안전한 private candidate 필드는 원본 판본·hash, hash 처리한 관리번호, CRS 계보가 있는 점 위치, 공식 종류 코드·설명, 기호 자체의 표시 방향을 검토하는 `DRN`까지다. `직진`이나 `좌회전 금지` 표시는 노면 기호 분류이며 실제 접근 방향, 도로 방향, 차로, 정지선, 신호 현시 또는 현재 통행 허가를 확정하지 않는다.

## OA-21145 비오톱 유형·평가도

| 항목 | 결과 |
| --- | --- |
| 공식 판본 | `비오톱유형_평가도(2025년기준).zip`, 파일 수정 2025-08-27 |
| 실제 길이 | `15,413,945 bytes` |
| ZIP SHA-256 | `7FF3802116EC35BECABAAD8F5E8401C6AF8FC8C56FE1DFB526F39BF154657663` |
| 형식 | ESRI Polygon Shapefile, SHP·DBF 각 42,544건 |
| 유효 geometry | 42,543개, 빈 geometry 1개 |
| CRS·인코딩 | `EPSG:5174`, windows-949 |
| 이용허락 | 공공누리 제1유형, 법적 효력 없이 참고자료로 활용 |
| receipt SHA-256 | `CDF352945CF221B2850BC540D129396E32875250D8E1E8330DA78FED8E72AE0A` |

페이지에 함께 남은 2022년 `UPIS_BIOTOP_TYP.zip`은 최신 판본과 섞지 않았다. 유형 코드 70종, 평가 등급과 9개 유형 범례가 있으며 산림지·주거지·상업 및 업무지·교통시설·조경녹지·공업·도시기반시설·하천·습지·경작지·유휴지 등의 분류를 포함한다. 이는 polygon 유형·평가 자료이며 생물종 목록, 현장 개체 관찰, 현재 안전 상태 또는 이동 자료가 아니다.

역사 경계 30/30개 동에 유효 polygon이 겹치고 polygon–동 교차는 3,185건이다. 역사 경계 union 면적 `32,119,306.65m²` 중 겹침은 `32,086,321.85m²`, 약 99.897%다. 남은 약 `32,984.8m²`는 2023 경계와 2025 비오톱의 판본·변환·가장자리 차이를 포함하므로 빈 공간을 인접 등급으로 채우지 않는다. 현행 JUSO 경계 검증 전에는 현재 coverage로 승격하지 않는다.

## 로컬 보존

- 방향표시: `artifacts/local/public-data/admin-dong-road-direction-20260926-r1/raw/`
- 비오톱: `artifacts/local/public-data/admin-dong-biotope-20260926-r1/raw/`

각 폴더는 공식 ZIP, 공식 페이지 HTML, receipt를 보존한다. 방향표시는 공식 필드 정의서 PDF도 포함한다. 두 경로는 Git ignore 대상이며 tracked 파일·DB·Mongo·Unity를 변경하지 않았다.

## 적용 경계와 후속

| 항목 | 현재 상태 |
| --- | --- |
| 공식 원본 획득·hash | 완료 |
| 30개 역사 행정동 coverage | 감사 완료 |
| private candidate generation | 미착수 |
| 현행 행정동 경계 | 미확보·미검증 |
| 공개 표시·DB·Mongo·current | 미적용 |
| Unity Scene·Prefab·Asset | 미변경 |
| Runtime·Traversal·Gameplay | 권위 없음 |

후속은 두 자료를 서로 다른 private candidate schema와 generation으로 만든다. 방향표시는 point 기호 layer, 비오톱은 polygon 관찰 layer로 분리하며 기존 공개 `displayOverlays`에 넣지 않는다. Unity까지 이어질 경우 renderer 전용·Collider 0·읽기 전용 메모리 표현으로 시작하고 실제 접근·통행·생태 안전 의미는 계속 차단한다. **새 디오라마 규칙 후보 없음**.
