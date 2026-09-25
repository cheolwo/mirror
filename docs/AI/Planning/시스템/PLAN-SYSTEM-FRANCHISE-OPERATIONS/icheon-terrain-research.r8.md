# 이천 소규모 실제 지형 자료 수집 r8

2026-09-23. 사용자 요청은 한반도 전체 고도화가 아니라 이천 일부 실제 공간을 먼저 수집하고, 이후 가상 제조시설과 서울 디오라마 연결을 준비하는 것이다. r7의 저해상도 지도 위 이동만으로 충분하다고 판단하지 않는다.

## 수집 범위와 결과

검토 중심은 기존 합성 기준점 위도 37.27, 경도 127.44다. 경계는 WGS84 `[127.4175,37.252,127.4625,37.288]`, 약 4km × 4km이며 행정 경계나 공장 부지 확정이 아니다.

| 자료 | 확보·검증 | 제한 |
| --- | --- | --- |
| Copernicus GLO-30 DSM N37 E127 | 전체 원본 49,403,795 bytes, SHA256 확인. 검토 창 162×130, 유효값 100%, 45~326.70m | 약 30m급 표면 높이. 나무·건물이 포함되며 지면만의 고도가 아님 |
| Sentinel-2 L2A RGB | 2025-11-12 관측 장면의 검토 창만 HTTP range 수집. 10m, 406×406 RGB, 유효값 100% | 2025년 검색 반환 3건 중 낮은 구름 비율 선택. 지역 내 구름·현장 일치 미검증. 연간 최적 영상 아님 |
| 도로·진출입·공장 부지 | 미수집·미확정 | 영상으로 도로 주행 가능성이나 시설 용도를 확정하지 않음 |

파일은 `artifacts/local/public-data/icheon-terrain-20260923-r1/`에 있다. `receipt.json`은 원본 출처·수집 시각·hash, `sentinel-search.json`은 동결 검색 응답, `quality.json`은 좌표계·해상도·유효값·각 clip hash를 기록한다. 이미지 파일은 Git 또는 Unity 배포 자원에 넣지 않았다.

## 출처와 이용조건

- [Copernicus 공개 DSM 설명](https://copernicus-dem-30m.s3.amazonaws.com/readme.html), [공개 배포 목록](https://registry.opendata.aws/copernicus-dem/), [라이선스](https://documentation.dataspace.copernicus.eu/APIs/SentinelHub/Data/DEM/resources/license/License-COPDEM-30.pdf). 공개 AWS 2021 판본 경로 사용. 개별 셀 관측 시각은 미확인이다. 원본·변형본 출처 표기와 배포 조건 검토를 유지한다. CDSE 제한 View 서비스 로그인이나 라이선스 수락을 대신 수행하지 않았다.
- [Sentinel-2 공개 COG 자료와 이용조건](https://registry.opendata.aws/sentinel-2-l2a-cogs/). Earth Search STAC에서 `S2B_52SCG_20251112_0_L2A` 선택. 관측 시각 `2025-11-12T02:27:08.989000Z`; 장면 구름 비율 0.058872%는 검토 창의 무구름 증거가 아니다.
- 정기 갱신·대량 수집은 설정하지 않았다. 재수집은 새 판본으로 수행한다. 최신 현장 모습이라고 표방하지 않는다.

## 저장·검증 수준

- 기존 `평창군공공공간원본등록Service`를 재사용하는 `이천지형원본.cs`와 CLI acquire/apply/verify 경로 추가. 등록은 비공개 파일 참조·hash·길이·수집 이력이며 정규화된 지형 셀 저장이 아니다.
- 현재 Docker는 dev-deps가 아니라 field-test compose이므로 첫 자동 판별이 `ComposeMismatch`로 쓰기 전에 차단됐다. 컨테이너·포트·권한 내 DB 목록을 재확인하고, 기존 명시적 로컬 연결 경로로 `hongdal_dev`만 지정했다. 보호 코드를 완화하지 않았고 자격 증명을 파일/출력에 기록하지 않았다.
- DSM 원본 MySQL 신규 1건, 같은 입력 재적용 신규 0건, 독립 verify 1건 통과. `PendingHumanReview`, `NoUnityDistribution`. 위성영상과 clip·품질 기록은 파일 확보만 완료했으며 DB 등록은 후속이다.
- importer build 0 오류·0 경고. Python 실제 추출 실행 성공. 원본 hash `4f8fef79da8c18ed9a9a0fb97b3d2695c4681181e013e039d87044e5479f7df6`.
- Unity 코드·Scene·Play Mode·Game View·build·commit·push는 이번 작업에서 수행하지 않았다.

## 다음 작은 구현 경계

1. 영상과 파생 자료의 DB 계보 등록을 닫고 DSM EPSG:4326 / 영상 EPSG:32652를 같은 미터 좌표계로 정렬한다. 높이 기준과 정확도도 확인한다.
2. 검토 창에서 한 대표 타일만 고도 메시로 만든다. 위성영상은 표면 색 참고이며 높이 메시를 대신하지 않는다. 타일 경계·LOD·좌표 오차를 시험한다.
3. 실제 도로 자료 확보·경로 검증 후 가상 공장을 별도 시나리오 오버레이로 배치한다. 실제 공장이 존재하거나 그 부지가 승인됐다고 표현하지 않는다.
4. 기존 서울 디오라마와 같은 차량 ID로 관찰을 연결한다. 미확보 구간은 합성 연결임을 표시하고 실제 주행 경로로 주장하지 않는다.

자료 수집은 Unity 적용 승인·업무 상태 변경이 아니다. 기존 출처/표현 분리 원칙을 지지하며 새 디오라마 규칙 후보 없음.
