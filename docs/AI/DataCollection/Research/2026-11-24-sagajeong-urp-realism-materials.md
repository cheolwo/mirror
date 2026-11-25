# 사가정 URP 디오라마 현실감 자료 조사

2026-11-24 KST · 사가정·면목·용마산 세 역 표본 · 내부 검토

**첫 비교는 보도와 건물이 만나는 곳부터 한다.** 사가정역의 보도 경계·출구·낮은 건물 옥상이 읽히고, 밤에도 길과 장소 표식이 남아야 한다. 기존 지도와 재질로 이 장면을 먼저 맞출 수 있다. 추가 자료의 우선순위는 실제 출구 위치와 외형, 골목 단차와 옹벽, 보안등의 위치와 조사 시점이다.

[사가정 URP S1 실행 반환](../../Handoffs/2026-11-24-Codex-사가정-URP-S1-실행반환.md) · [Claude 현실감 개발계획](../../Handoffs/2026-11-24-Claude-개발계획-현실감-3D그래픽.md) · [기존 디오라마 자료 인계](../../Handoffs/2026-11-23-Codex-디오라마-생활자료수집.md)

## 1. 이번에 받은 원본

| 자료 | 확보·검증 결과 | 화면에서 확인할 것 |
| --- | --- | --- |
| 서울시 가로등 `OA-22205` | 2026-11-18 CSV, 19,031행 중 정상 범위 위경도 18,985행. 2023 파일과 다른 판본. | 관리 기관이 담은 가로등 위치를 대조한다. 사가정역 600m에서는 이 파일의 기록이 0이다. 주변 골목까지 전수 조사한 자료로 해석할 수 없다. [공식 파일](https://data.seoul.go.kr/dataList/OA-22205/F/1/datasetView.do) |
| 서울시 안심귀갓길 시설 `OA-21696` | WGS84 SHP·DBF ZIP과 코드 정의서 XLSX 저장. 서울 표본 시설 11,883행, 보안등 코드 `305`. 사가정역 600m 보안등 33지점, 장부 대수 37대. | 실제 조사된 지점과 대수를 사용할 수 있다. 현황일은 2022년 8월이다. 다등의 개별 위치와 현재 존치, 광원 종류·밝기·색온도는 미확인이다. 공공데이터포털은 출처표시 제1유형을 표시한다. [서울 조사 원본](https://data.seoul.go.kr/dataList/OA-21696/F/1/datasetView.do) · [공공데이터포털 이용 조건](https://www.data.go.kr/data/15112100/fileData.do) |
| 서울시 캐노피·지상 엘리베이터 표준디자인 | 발행 2022-12 캐노피 53쪽, 2023-12 승강기 61쪽. PDF 원본과 지문, 확인 페이지를 저장했다. | 캐노피 PDF 35–45쪽의 입구 구성·재료·조명과 승강기 PDF 27쪽부터의 설계 방향·형상 검토를 읽는다. 캐노피 PDF 52쪽은 동의 없는 전재·복제·변형 금지, 엘리베이터 PDF 60쪽은 무단 사용 금지 고지가 있다. 내부 열람 근거로 쓴다. 개별 사가정 출구의 설치형이나 제작용 도면 사용 허가는 별도다. [서울시 디자인 아카이브](https://publicdesign.seoul.go.kr/web/noti/arch/archDetail.do?archv_no=F1027077AA4B0176E053C0A8A022FBF7) |
| Poly Haven 방수재·골강판 | `bitumen` 3장과 `corrugated_iron` 4장, 각 1024×1024. 총 7장·18,061,082바이트. 공식 바이트 수·MD5·로컬 SHA-256을 대조했다. Blender 5.2.1에서 7장 모두 읽었다. | 저층 평지붕의 빛 반응과 골강판 창고·차양의 줄무늬를 시험한다. 공급자 반복 폭은 약 20m와 1.12m다. 실물 위치·재료 관측과 별개인 범용 CC0 표면이다. [방수재](https://polyhaven.com/a/bitumen) · [골강판](https://polyhaven.com/a/corrugated_iron) · [CC0 이용조건](https://polyhaven.com/license) |

원본은 약 139MB다. 같은 판본의 보도 포장·횡단보도·가로수·보행자 출입구는 기존 묶음에서 읽는다. [로컬 수집 폴더](../../../../artifacts/local/public-data/sagajeong-urp-realism-20261124/) · [설계 참고자료 지문과 권리 고지](../../../../artifacts/local/public-data/sagajeong-urp-realism-20261124/official-reference/source-receipts.json) · [재질 출처 기록](../../../../artifacts/local/public-data/sagajeong-urp-realism-20261124/materials/source-receipts.json)

## 2. 위치·재료·외형에 가진 근거

| 우선순위 | 대상 | 재사용하거나 확인한 근거 | 보강할 것 |
| --- | --- | --- | --- |
| 1 | **보도 경계와 차도** | r19 서울 보도 면과 r62 횡단보도·보도 관계. `OA-22240` 보도 포장 표의 중랑구 사가정로 13구간은 폭 2–3m, 면목로 20구간은 1–4m. 역 사거리 인접 면목로 양방향 4구간은 1.7m다. `OA-23081` 후보 횡단보도는 역 350m에 31점이다. | 행정구·도로명·구간 시작/끝·방향으로 먼저 대응한다. 보도 면과 포장재 현황의 날짜를 각각 유지한다. 경계석 높이, 현재 도색 각도, 배수구의 상세 위치는 별도 증거다. [보도 포장 통계](https://data.seoul.go.kr/dataList/OA-22240/F/1/datasetView.do) · [횡단보도](https://data.seoul.go.kr/dataList/OA-23081/F/1/datasetView.do) · [서울시 보도 설계·시공 자료](https://news.seoul.go.kr/safe/sidewalk-data) |
| 1 | **사가정역 1–4번 출구** | 서울교통공사 2026-08-31 r9에 1번 7.15m·신형, 2번 2.95m·도시형, 3번 2.85m·신형, 4번 4.40m·신형. S1 내부 카드가 폭·유형을 읽으며 안내 단면의 네 표식은 공통 크기다. r97 역 안내도와 번호, r26 기존 모형, 주소 기반 출구 후보를 각각 찾을 수 있다. | 주소 후보와 OSM의 3↔4번 간 거리가 약 16m와 63m로 다르다. 역·출구 번호를 확인한 뒤 양쪽 도로 접근 방향과 현장 외형으로 대조한다. 폭은 출입구 폭이다. 깊이, 지상 높이와 캐노피 프레임의 설계는 자료 없음으로 둔다. [최신 캐노피 설치정보](https://www.data.go.kr/data/15082999/fileData.do) · [공식 안내도](https://www.data.go.kr/data/15004974/fileData.do) · [기존 출구 모형 근거](../../Planning/시스템/PLAN-SYSTEM-STATION-AREA-DIORAMA-MODULES/sagajeong-station-blender-integration.r30.md) |
| 1 | **골목의 경사·옹벽·계단** | 위상 보정 NASADEM r2 약 30m와 서울 등고선·표고점 원본. 사가정역에서 사가정공원 입구 방향 약 772m 표본은 최근접 격자에서 26·31·38·49·86m로 상승한다. 광역 기복을 보여 준다. | 작은 단차와 옹벽은 상·하단 선·높이·재료가 필요하다. 국토지리정보원 수치지도 V2 `F004`의 해당 도엽 원본과 속성 유무를 확인하고 단위와 수직 기준을 보존한다. NGII DEM은 사가정 도엽의 실제 격자 크기를 파일에서 확인할 단계다. [수치지도 V2](https://www.data.go.kr/data/15059719/fileData.do) · [F004 표준 정의](https://www.law.go.kr/LSW/flDownload.do?bylClsCd=200201&flSeq=145877779) · [옹벽 개별 파일 목록](https://data.edmgr.kr/dataView.do?id=vworld_open_30214) · [NGII DEM](https://www.data.go.kr/data/15059920/fileData.do) |
| 2 | **저층 옥상과 상가 입면** | 26동 굽기의 건물 외곽·용도·높이와 추정 규칙. r56 중랑 30,902건 중 원높이 11,593건, 층수만 16,122건, 둘 다 미기재 3,187건. 기존 CC0 10종은 아스팔트·보도·적벽돌·회색 콘크리트·셔터·점자블록 등을 포함한다. | 실측 높이와 층수로부터의 추정 높이를 분리한다. 실물 색·재질은 가까운 건물 몇 곳의 허용된 참고 자료가 필요하다. 난간, 옥탑, 물탱크, 실외기의 위치·개수와 창문 배경은 연출로 둔다. [GIS 건물 원천](https://www.data.go.kr/data/15083092/fileData.do) · [건축물대장 표제부](https://www.data.go.kr/data/15134735/openapi.do) |
| 2 | **골목 보안등과 발광 창** | `OA-21696` 표본, r92 실제 점등 시각, r82 송파 별도 조사 조도·조명 제조사 배광 참고를 확보하고 있다. | 보안등 33개 위치는 2022 조사다. 실제 등기구와 배광 분포가 연결되지 않아 색온도와 빛 범위는 연출이다. 창문마다 직접 광원을 두기 전에 기존 발광 표면과 바닥 밝기 지도를 같은 밤 장면에서 비교한다. [안심귀갓길 안전시설물](https://www.data.go.kr/data/15112100/fileData.do) · [송파 야간 조도 표본](https://www.data.go.kr/data/15097745/fileData.do) |
| 2 | **가로수와 계절 실루엣** | r8 서울 2026 가로수 원본의 두 게시처는 바이트·지문이 같다. 사가정역 350m 중랑 탭 표본 92점 중 은행 66, 벚나무류 25, 기타 1이다. r27 자치구 수종 통계와 r84 비오톱은 풍경 비율 참고다. | 실제 위치 자료와 만들어 낸 나무는 화면 표기를 분리한다. 수고·수관 폭과 잎 밀도는 원본에 없다. 11월 23일 공식 제공 화면 두 곳은 이용 제한 없음, 과거 같은 원본 안내는 공공누리 4유형이다. 변경 조건의 적용 범위를 확인할 때까지 게임 탑재·대외 배포를 보류한다. [OA-23040](https://data.seoul.go.kr/dataList/OA-23040/F/1/datasetView.do) · [OA-22904](https://data.seoul.go.kr/dataList/OA-22904/F/1/datasetView.do) · [수종별 통계](https://www.data.go.kr/data/15047076/fileData.do) |
| 3 | **건물 주변의 소품** | r2 보행자 출입구의 단차·경사로 코드, 정류장 원장, 쓰레기통 위치 단서, r5 공식 규격 20건, r86 노면 예고표시와 안전지대. | 개별 좌표·규격·형상을 분리한다. 현장 근거가 충분한 것만 정확한 위치에 두고 보행을 가리는 배치나 중복 점을 점검한다. 용도와 실루엣으로 알아보는 가까운 소수 모형부터 비교한다. [서울 보행자 출입구](https://data.seoul.go.kr/dataList/OA-21699/F/1/datasetView.do) · [가로시설 표준디자인의 범위](https://news.seoul.go.kr/culture/archives/504586) |

역 중심에서 50m 이내에 있는 횡단보도 후보 4곳은 2026년 원본에서 모두 이단·신호 있음으로 기록됐다. r62의 비교에서 이 중 2곳은 2021년 횡단보도 면의 양 끝이 서로 다른 2020년 보도면의 3m 이내다. 출구와 길이 만나는 첫 비교 지점으로 고른다. 현재 연석이나 도색 형태의 확정에는 별도 자료가 필요하다. [기존 보도 비교](../../../../artifacts/local/public-data/diorama-public-layer-intake-20261123/r62-crosswalk-sidewalk-local/읽는법.md)

서울시 보도공사 매뉴얼 2.0 안내에서 횡단보도 진입부 경사석의 길이를 1m에서 2m로 늘린 설계 예를 확인했다. 연석 옆 경사·점자블록 모듈의 형상 참고다. 사가정 실제 경사석의 길이나 현행 시설의 규격을 이 예에서 가져오지 않는다. [서울시 보도공사 매뉴얼 안내](https://news.seoul.go.kr/traffic/archives/505042)

정밀 지형은 **받아야 할 도엽까지 특정**했다. 600m 사각 검색의 1:1,000 후보는 12개다. 역 중심 `377051738 성동1738`과 남서 `377051747 성동1747`을 공개 색인에서 재조회해 제작 2025년, 고시 `2025-3996`을 확인했다. DEM `37705 성동`의 최신 검색 항목은 2025년이며 파일의 해상도와 수직 기준은 미확인이다. 브이월드 옹벽 `dsId=30214`에는 약 211MB ZIP, 갱신 2026-09-21, 출처표시 CC BY 2.0 KR이 안내된다. 다운로드에 로그인이 필요한 원본은 취득 대기다. 현재 로컬에는 공개 검색 응답 4개를 보관했다. [국토정보플랫폼](https://map.ngii.go.kr/ms/map/NlipMap.do?tabGb=total) · [브이월드 옹벽](https://www.vworld.kr/dtmk/dtmk_ntads_s002.do?svcCde=MK&dsId=30214) · [도엽 색인 지문](../../../../artifacts/local/public-data/sagajeong-urp-realism-20261124/official-data/terrain-index/source-receipts.json)

서울 S-Map 3D 건물과 실내 지도는 높이·외형·시기 대조 창구다. 비교 도구의 공개와 모형·텍스처의 추출 및 Unity 배포 허가는 다른 증거다. 이번 회차의 모형 원본과 배포 허가는 미확인이다. [서울 S-Map 사용 안내](https://smap.seoul.go.kr/guide/guide.html)

옥상에 붙는 설비는 주소와 시설의 기록이 맞는 경우에만 실재 표시를 검토한다. r93 공공태양광의 2025-12판에 중랑구 25행이 있고 패널의 각도·치수·형상은 확인되지 않는다. 서울 옥상녹화 `OA-1369`의 공개 첨부를 열어 보니 열 정의서였고 위치 행은 없었다. 밝은 차열 도장과 옥상녹화의 정책 자료는 일반 표현 후보로 읽을 수 있다. [공공태양광 시설 현황](https://data.seoul.go.kr/dataList/OA-22261/F/1/datasetView.do) · [옥상녹화 제공 화면](https://data.seoul.go.kr/dataList/OA-1369/F/1/datasetView.do) · [서울시 쿨루프 안내](https://news.seoul.go.kr/env/archives/570114)

## 3. 역 세 곳의 자료 밀도 점검

동일한 역 중심 출처인 서울시 교통정책지원시스템 자료를 사용했다. 각 역 중심에서 위경도의 원호거리 600m를 잘라 자료별 기록 수를 비교했다. 범위가 맞닿거나 겹칠 수 있고, 표의 종별 조사 시점·관할도 다르다. 건수는 자료량이다. 실제 시설 총량과 세 역의 합계에는 사용하지 않는다. [역 중심 원천](https://t-data.seoul.go.kr/dataprovide/trafficdataviewfile.do?data_id=36) · [확인 파일](../../../../artifacts/local/public-data/sagajeong-urp-realism-20261124/station-radius-coverage.json)

| 역 600m | 새 가로등 파일 | 안심귀갓길 보안등 지점 / 대수 | 가로수 전체 관리기관 / 중랑구 관리 탭 | 조사된 보행 출입구 행 |
| --- | ---: | ---: | ---: | ---: |
| 사가정 | 0 | 33 / 37 | 304 / 304 | 31 |
| 면목 | 0 | 29 / 30 | 605 / 605 | 32 |
| 용마산 | 0 | 11 / 12 | 330 / 259 | 8 |

사가정 600m의 중랑구 가로수 표본은 은행 233, 벚나무류 60, 이팝 8, 나머지 3이다. 역 대표점이 약 2.91m 다른 기존 r27 기준으로 다시 계산하면 경계의 은행나무 한 그루가 포함돼 305였다. 좁은 지역의 정밀 배치는 어느 중심과 좌표계를 썼는지 함께 넘긴다. 후속 제작 때 이팝나무는 흰 꽃차례의 외형을 참고할 수 있다. 다른 지역의 개화 관측을 사가정 개화일로 설정하지 않는다. [국립생물자원관 이팝나무 형태 참고](https://species.nibr.go.kr/nibr/assets/K-BON_GUIDE.pdf)

보안등의 33개 위치와 당시 장부 대수는 별도 검토용 GeoJSON으로 뽑았다. 점은 WGS84이고 시점은 2022-08-05 5건, 08-19 28건이다. 실제 기구가 있는지 현행성을 확인하고, 모형·빛 범위·색온도는 연출 레이어에서 다룬다. [사가정 보안등 표본](../../../../artifacts/local/public-data/sagajeong-urp-realism-20261124/sagajeong-security-lamps-600m.geojson)

## 4. 새 구매 전에 살펴볼 에셋

| 그릴 것 | 지금 쓸 후보 | 필요한 보완 |
| --- | --- | --- |
| 접지·보도 단차·옹벽 | 기존 바닥 재질·점자블록, 현재 작업본의 접지 그늘, Construction `SM_Bld_Retainer_Wall_01_Short` 112삼각형 | 실제 벽과 경사가 파악된 곳에서만 상·하단과 대조한다. 짧은 연석, 경사석, 배수로를 조합하는 작은 모형부터 만든다. |
| 저층 지붕·상가 | 기존 절차형 실외기·물탱크·옥탑, Synty City `SM_Prop_Roof_Aircon_01` 188삼각형, Construction `SM_Prop_Watertank_01` 886삼각형, 신규 CC0 2종 | 실제 지붕 면의 높이와 소품 밑동이 맞는지 확인한다. 재사용할 난간·옥탑 계단실·점포 셔터 묶음은 서울 저층 실루엣에 맞춘다. |
| 거리 표식 | City `SM_Prop_BusStop_01` 648삼각형, Town `SM_Prop_Streetlamp_01` 202삼각형, City 맨홀 소품 | 원래 크기·피벗·재질·투명 유리부터 확인한다. 실물에 없는 표식이나 잘못된 정류장 쉘터가 추가되지 않도록 구분한다. |
| 나무 | 현재 절차 수목과 계절 셰이더, 가로수 수종·좌표 시험 표본 | 은행·벚나무류의 수관 실루엣을 가까운 장면에서만 두세 유형으로 비교한다. 멀리서는 현재 표현을 써서 밀도·계절 차이를 먼저 본다. |

Synty 수량은 보유 에셋 대장의 측정값이다. 이 소품들은 별도 기본 Unity에 있고 사가정 Windows URP 시험판에 탑재됐다는 근거는 없다. 기존 가로등·정류장 실렌더 시험의 파이프라인은 Built-in/Gamma다. 현 파일 경로·GUID와 대장을 대조했다. 작은 물탱크의 대장 크기는 약 1.45 × 1.42 × 1.45 Unity 단위이고 바닥 중심 피벗이다. 낮은 옹벽 모형의 바닥은 원점보다 약 0.076 단위 아래다. 두 값은 에셋 맞춤에만 사용한다. 원본 에셋은 공급자·구매처의 사용 조건에 따라 다루고 인계에는 식별자와 측정치만 보낸다. 이번 조사 범위에서 신규 구매가 필요한 에셋은 발견되지 않았다. [보유 에셋 5개 참조와 측정치](../../../../artifacts/local/public-data/sagajeong-urp-realism-20261124/owned-asset-references.json) · [Synty 구매 조건](https://syntystore.com/pages/one-time-purchase-licence) · [기존 소품 실행 증거](../../../../artifacts/local/owned-synty-map-preview-20261111/realistic-render-evidence.json)

## 5. Claude가 대조할 장면과 기술 조건

1. **역 사거리와 어울림마당.** 1번 출구 앞 사가정 마중 마을활력소, 2번 주변 보행 시설과 사가정51길 특화거리를 본다. 조사된 시장 대표점과 특화거리는 별도다. S1 건물 색인에서 마을활력소는 이름·주소 후보가 결속되지 않아 검수용 이름표만 먼저 확인한다. 보도 경계와 출구 폭, 벽 밑 그림자를 우선한다. [특화거리 사업 자료 199쪽](https://ecatalog.jungnang.seoul.kr/src/viewer/download.php?host=main&no=1&site=20260205_105531) · [중랑구 2023년 4월 소식](https://ecatalog.jungnang.go.kr/src/viewer/download.php?host=main&no=1&site=20230323_095159)
2. **사가정 3·4번 출구 쪽과 야간 골목.** 출구별 형상과 면목7동 쪽 접근을 확인한다. 4번 출구에서 교육센터까지 공표된 보행 거리 239m는 방향을 확인할 보조 자료다. S1의 주소 번호 후보와 실제 건물 외곽은 미결속이다. 보안등 일부 지점에서 지도 표식·기사 빛이 기구 발광에 묻히는지 확인한다. [중랑구 교육센터 오시는 길](https://educenter.jungnang.go.kr/science/contents/about_location.php)
3. **용마산 쪽 주거에서 공원으로 넘어가는 화면.** 같은 카메라로 지형 증가, 저층 옥상 실루엣, 수관 변화와 건물의 접지를 확인한다. 공원 인근 데크는 별도 사업 구간이며 전 구간의 도로 경사 자료로 사용하지 않는다. [중랑구 2023년 현황 자료](https://ecatalog.jungnang.go.kr/src/viewer/download.php?host=main&no=1&site=20230822_171318) · [5개 지형 표본과 원본 지문](../../../../artifacts/local/public-data/sagajeong-urp-realism-20261124/sagajeong-mountain-direction-terrain.json)

기존 S1 Gamma 실제 화면을 색과 운영 표식의 대조군으로 남긴다. Claude의 별도 S2b는 02:46 KST 자동 검사를 통과했다. 사가정 낮과 밤의 기존 캡처에서 옥상 소품과 창 발광이 확인된다. 같은 26동·건물 53,857채에서 렌더러는 S1 130개와 S2b 234개였다. S2b 로그에는 접지에 쓸 벽변 331,920개와 옥상 소품 정점 약 263만 개가 기록됐다. 이 수치는 등록된 객체와 정점이다. 실제 프레임 성능은 같은 위치·시간·카메라·그래픽 API로 한 항목씩 비교할 때 판정한다. [S2b 자동 검사](../../../../artifacts/local/hongdal-sagajeong-urp-s2-20261124/evidence/proof-20261124-024406/view-proof.json) · [렌더 실행 로그](../../../../artifacts/local/hongdal-sagajeong-urp-s2-20261124/logs/player-20261124-024406.log)

S2b의 낮 캡처는 낮은 건물 옥탑과 벽돌색 차이를 읽을 수 있다. 근접 야간 화면에도 창 불빛과 선택 표식이 남지만 골목 안쪽의 보도와 차도 경계는 비슷한 색으로 보인다. 다음 비교에는 **역 주변 횡단보도 한 곳에서 보도 높이·경계석·재질을 읽을 수 있는지**를 먼저 넣는다. 눈에 보이는 차이가 확인되면 동일 카메라로 옥상·보안등·나무를 한 항목씩 비교한다. 02:52 KST에 Claude S2c의 그림자 패스 변경판도 전체 자동 검사를 통과했다. 이번에 수집한 원본과 에셋의 연결·렌더링은 아직 각각 검증 전이다. [S2b 낮 화면](../../../../artifacts/local/hongdal-sagajeong-urp-s2-20261124/evidence/proof-20261124-024406/62-close-mid.png) · [S2b 근접 밤 화면](../../../../artifacts/local/hongdal-sagajeong-urp-s2-20261124/evidence/proof-20261124-024406/v-dong-08-near-night.png) · [S2c 자동 검사](../../../../artifacts/local/hongdal-sagajeong-urp-s2-20261124/evidence/proof-20261124-024959/view-proof.json)

- **재질 입력.** 기존 1K 원본은 보존한다. URP Lit 시험에서 기본색은 sRGB, 법선은 Normal Map, 거칠기와 금속성·차폐는 선형 데이터다. 매끈함 슬롯에 넣을 변환 사본은 `1 − roughness`다. 현재 지도의 전용 셰이더에는 재질 입력을 따로 연결해야 한다. S2a의 지도가 카메라 깊이·법선 자료에 들어가는지부터 확인하고, SSAO와 기존 접지 그늘이 겹쳐 골목이 검게 뭉치는지 본다. [Unity 6.5 Lit 입력](https://docs.unity.com/en-us/engine/6000.5/manual/materials-and-shaders/built-in/shaders-in-universalrp/reference/lit-shader) · [Unity SSAO](https://docs.unity.com/en-us/engine/6000.5/manual/lighting-overview/lighting/shadows-in-urp/post-processing-ssao/ssao)
- **노면 표시.** 횡단보도 점과 표지의 실제 폭·각도는 다른 값이다. 기존 지도 메시 표기와 데칼을 한 곳에서 비교한다. 투명면, 수신용 셰이더, 깊이/법선 패스, 차도 위 표지가 깜박이는 경우를 본다. [Unity URP Decal](https://docs.unity.com/en-us/engine/6000.5/manual/visual-effects/decals/renderer-feature-decal/decal)
- **비용 측정.** 낮·노을·밤의 같은 카메라, 1600×900, 같은 프레임 제한에서 렌더러·정점·텍스처 메모리·CPU/GPU 프레임을 기록한다. S1의 D3D11과 S2a의 D3D12 수치로 속도 우열을 정하지 않는다. 소품 확대 배치는 화면에서 차이가 읽히는 범위와 성능을 보고 판단한다. [Unity Profiler 카운터](https://docs.unity.com/en-us/engine/6000.5/manual/analysis/profiler/counters-reference)

## 증거 범위

새 출처 기록은 로컬 수집 폴더에 둔다. 실제 파일은 제공자·버전·크기·SHA-256과 연결하고, 기존 파일은 원본 위치와 판본을 참조한다. 이번 자료로 확인한 것은 원본 접근, 세 역의 공간 표본, 활용 후보와 결손이다. 출구 번호와 실제 위치 결속, 효과별 성능, 일반 실행의 조작 검증, 공개판 적용은 후속 확인이다. 조사는 공표 자료·연출·자료 없음이라는 기존 표시 기준을 지지한다. **새 디오라마 규칙 후보 없음.** [디오라마 증거 기준](../../../Architecture/역세권디오라마증거진화체계.md)
