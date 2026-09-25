# 단계별 Unity 지도 공급자 조사 r23

조사일: 2026-09-23. 상태: 공식 문서 조사. 인증 API 호출·타일 수집·DB 적재·Unity SDK 설치·표현 검증은 미실행. 이번 요청은 API 적합성 조사이며 지도 원본 수집 승인이 아니다.

## 결론

지도를 보는 API와 재가공 가능한 지형·건물 원본을 구분해야 한다. Google/NAVER만으로 국내 3D 디오라마 원본을 확보할 수 있다고 확정할 수 없다. 현 프로젝트는 지구본/중간 배경지도/자체 상세 디오라마를 같은 좌표 관심 지점으로 연결하는 혼합 방식이 적합하다.

## 공급자별 확인

- Google Map Tiles API는 2D와 Photorealistic 3D Tiles를 제공한다. Cesium for Unity 공식 가이드가 있다. [Google 개요](https://developers.google.com/maps/documentation/tile/overview), [Unity 연계](https://cesium.com/learn/unity/unity-photorealistic-3d-tiles/).
- Google 국가별 표에서 한국은 Map Tiles 열에 2D/3D 동시 표기인 두 개의 점이 아닌 한 개만 있고 Maps JavaScript 3D도 미지원 표시다. 서울 실사 3D를 전제로 설계하지 않는다. 서비스 지도에서 보이는 것과 API 제공 범위는 같지 않을 수 있다. [공식 범위](https://developers.google.com/maps/coverage?hl=en).
- Google은 지도 시각화 목적이며 임의 다운로드·장기 캐시·오프라인 이용·형상 추출에 제한이 있다. 원문 타일에서 건물을 추출해 Blender 자산으로 만드는 경로로 쓰지 않는다. 자체 객체 중첩은 출처를 분리해 표시해야 한다. [정책](https://developers.google.com/maps/documentation/tile/policies).
- NAVER Maps는 Web Dynamic Map, Mobile SDK, Static Map, Directions 5/15, Geocoding, Reverse Geocoding을 제공한다. 문서의 공개 제품 목록에서 Unity용 3D 건물/지형 메시 제공 API는 확인하지 못했다. 지도 내 건물 레이어와 메시 다운로드는 다르다. [현행 개요](https://guide.ncloud-docs.com/docs/application-maps-overview).
- 네이버 지도 자체를 쓰려면 웹 지도/WebView 또는 모바일 네이티브 지도 연결이 후보이며 이는 Unity 3D 월드와 다른 렌더링 경계다. Static Map은 이미지이지 높이·도로망 원본이 아니다. Unity 텍스처 적용·장기 저장·다른 배경 지도 위 경로 중첩은 이용조건 확인 전 승인된 용도로 보지 않는다. [Static Map](https://api.ncloud-docs.com/docs/ai-naver-mapsstaticmap-raster), [사용 준비](https://guide.ncloud-docs.com/docs/application-maps-spec).
- NAVER Reverse Geocoding은 좌표에 대해 법정동/행정동 코드 조회가 가능하지만 행정 경계 폴리곤을 제공하는 것과 다르다. [API](https://api.ncloud-docs.com/docs/en/ai-naver-mapsreversegeocoding-gc).
- 국내 중간 배경 후보인 국토교통부/VWorld WMTS/TMS는 EPSG:3857의 2D 배경지도·영상지도 등을 제공한다. 포털 이용허락은 공공누리 제1유형으로 표시되어 있다. 인증·호출 제한·실제 서비스 약관을 추가 확인해야 하며 3D 높이 데이터 제공을 뜻하지 않는다. [공공데이터포털](https://www.data.go.kr/data/3046388/openapi.do).
- Cesium은 공급자와 다른 렌더링/좌표/스트리밍 도구다. Unity에서 WGS84 지구·지형·영상·3D Tiles를 다루며 자체 또는 로컬 3D Tiles도 연결할 수 있다. 라이브러리 이용조건과 각 데이터 이용조건은 별도다. [Cesium](https://cesium.com/learn/cesium-unity/ref-doc/), [자체 자료](https://cesium.com/learn/unity/unity-datasets/). 현재 Unity Packages/manifest.json에서 Cesium 의존성은 확인되지 않았다.

## 단계별 제안 — 아직 공급자 확정 아님

| 관찰 범위 | 후보 |
| --- | --- |
| 지구·한반도 | 기존 지구본 보존, 필요 시 Cesium 지형/영상 별도 시험 |
| 수도권·서울·중랑구 | VWorld WMTS 또는 사용조건 확인한 Google 2D 타일로 중간 축척 표현 |
| 동·역세권 | 공개 경계·도로·건물 데이터와 기존 배치맵, NAVER는 주소 확인/운영 지도 보조 |
| 상세 디오라마 | 기존 SagajeongReference와 독립 제작 자산, 업무 애니메이션 |

배경 영상만으로 산 높이가 생기지 않는다. 산악 입체화에는 별도의 높이 자료가 필요하다. 행정 범위와 타일 LOD를 구분하고 같은 위경도 중심·화면 범위로 연결한다. 공급자를 확대 단계마다 불필요하게 바꾸지 않는다.

## 비용

Google 공식 종량표 기준 2D Tiles는 월 무료 100,000건, 첫 유료 구간 1,000건당 USD0.60. Photorealistic 3D Tiles는 월 무료 1,000건, 첫 유료 구간 1,000건당 USD6.00. 실제 과금 이벤트·할인·세금·환율·별도 Cesium 데이터 비용은 분리한다. [요금표](https://developers.google.com/maps/billing-and-pricing/pricing).

2D 타일과 3D 루트 요청의 과금/할당량 단위가 다르므로 화면 한 번=API 한 번으로 계산하지 않는다. [사용량](https://developers.google.com/maps/documentation/tile/usage-and-billing). 네이버는 신/구 서비스와 선택 API의 현행 계정 요금표 확인 후 산정한다. 이번에는 유료 호출하지 않았다.

## 다음 좁은 시험

사가정 중심의 서울→중랑구 중간 지도를 VWorld 공식 타일로 표시할 수 있는지 인증·이용조건·해상도부터 확인한다. 기존 Scene을 유지하고 자체 디오라마와 좌표 정합을 대조한다. Cesium 도입은 기존 지구본 전면 교체가 아니라 별도 검토로 둔다. Google/NAVER 응답을 기존 MySQL 공공자료 수집기로 무조건 영구 저장하지 않는다.

새 디오라마 규칙 후보 없음. 제품 코드·Scene·DB·commit·push 변경 없음.
