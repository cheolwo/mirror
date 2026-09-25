# 한반도 광역 참고·수도권 연결 r19

2026-09-23 사용자 요청: 서울만 고립돼 보이지 않도록 경기·인천까지 지리 관계를 확장하고 화면을 검증한다.

## 범위와 출처

국내 공식 최신 행정동 경계가 확보됐다고 주장하지 않는다. 이번에는 [Natural Earth Admin 1](https://www.naturalearthdata.com/downloads/10m-cultural-vectors/10m-admin-1-states-provinces/)의 1:10M 광역 참고 자료를 별도 context 층으로 추가했다. [공식 이용조건](https://www.naturalearthdata.com/about/terms-of-use/)은 public domain이며 정확성/최신성 보장을 제공하지 않는다. 공급자 페이지 표시판본 5.1.1이며 기준일은 미상이다. 원본 명칭/코드를 보존하므로 현행 명칭과 다를 수 있다. 법정/행정동 또는 법적 경계 자료가 아니다.

원본 다운로드 URL: `https://naciscdn.org/naturalearth/10m/cultural/ne_10m_admin_1_states_provinces.zip`. 14,909,524 bytes, SHA256 `efc59726337323058f9446210adc96673179cd344e053666ee3d28cb58ba2b05`. 수집 시각 2026-09-23T08:53:58Z. 로컬 `artifacts/local/public-data/korea-admin1-context-r1`에 원본/receipt 보관.

## 구현

- 기존 .NET 공공자료 도구에 `admin1-context-acquire/apply/verify`를 추가했다. 기존 원본등록 Service/로컬 DB 보호 검사 재사용.
- Python 생성기는 원본 hash/도형 유효성 검증 후 KOR17·PRK11, 총28개 광역 링을 추출한다. 서울·인천·수원 기준 좌표가 각 원본 권역에 포함되는지 검사했다. 이것은 전체 경계의 정밀 일치 검증이 아니다.
- 기존 서울 v3의 89개 상세 영역은 그대로 유지하고 별도 `korea-admin1-context.v1` 층을 추가한다. 서로 다른 출처의 경계를 병합하거나 공식 부모 관계로 확정하지 않는다.
- 재생성 두 번 같은 hash `2d87b48485493d8887d528c3d761b3f4af3f88cbbbabe0a803ef1291ae90ac0d`, 2,862,703 bytes. 로컬 Editor 자동 준비 파일을 이 사본으로 지정했다.
- Unity에서 같은 곡면/카메라로 한반도 광역 참고→수도권→서울 상세. 경기·인천 선택은 광역 위치 관찰과 하위 자료 미연결 안내만 제공한다. 확대에 따른 선 두께/지명 겹침 회피 추가. 서울 상세에서도 주변 context 경계를 유지한다.

## 실제 검증과 차단

- 수집 도구 build/다운로드 성공. DB apply는 `ComposeMismatch`로 쓰기 전 거절됐다. 현재 컨테이너는 다른 local field-test Compose 구성이며 별도 확인에서도 예상 DB identity 조건이 충족되지 않았다. 보호 조건을 완화하거나 root로 저장하지 않았다. **DB 저장·독립 DB 재조회·DB 멱등 시험 미완료**, 원본/파생 파일만 확보.
- Unity EditMode 서울중간공간Tests 15/15 통과. 관련 diff 검사 통과.
- Play 자동 Ready 후 ContextAreaCount28, IsCurved=true 확인. 한반도→수도권→서울은 API-direct로 실행. UI 포함 Game View 캡처는 Unity `Documentation/Changes/2026-09-23-capital-context-r19/01-peninsula.png`, `02-capital.png`, `03-seoul.png`.
- 실제 화면에서 경기·인천·서울 상대 위치가 함께 드러남을 확인했다. 배경 지구본의 낮은 해안 해상도와 광역 벡터 윤곽 차이가 눈에 띈다. 지도 정밀도/현행 행정구역 정합성 완성으로 판단하지 않는다. Console에는 기존 localhost:5204 연결 실패가 있으며 오류 0을 주장하지 않는다.

## 후속

공식 국내 시도→시군구 경계 확보와 출처/판본 통일이 우선이다. SGIS 행정구역 API는 accessToken을 요구한다. 인증/이용조건을 확인해 정밀 사본으로 교체하고 기존 낮은 해상도 해안 바탕도 조화시켜야 한다. 경기·인천의 실제 하위 행정동, 물리 클릭/휠 완주, 배포용 자료는 미완료.

새 디오라마 규칙 후보 없음. Scene 저장·commit·push 없음.
