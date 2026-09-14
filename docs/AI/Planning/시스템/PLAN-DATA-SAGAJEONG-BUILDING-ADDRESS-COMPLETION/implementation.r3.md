[기획 · 데이터·공간 식별 · PLAN-DATA-SAGAJEONG-BUILDING-ADDRESS-COMPLETION · 구현 r3]

# 사가정역 1km 건물 도로명주소 결속 구현 결과

## 확정

- 사용자가 확정한 A안에 따라 하나의 공식 주소를 여러 건물이 공유해도 각 디오라마 건물 ID를 유지하고 `OfficialSharedComplexAddress`로 같은 주소를 참조한다.
- 가까운 건물의 주소를 복사하지 않는다. 건물 자체 주소, 건물 윤곽 안의 주소 지점, 유일한 공식 건물명 후보만 결속 근거로 인정한다.
- 상세 동·층·호, 소유자·입주자, 출입구·정차점, 배달·가격·Unity 적용 권한은 이 자료에서 생성하지 않는다.

## 공식 원본과 판본

- 공급자: 행정안전부 주소기반산업지원서비스
- 자료: `도로명주소 건물DB` 2026-08 월 전체분, 2026-08-31 기준
- 원본 ZIP: 149,263,413바이트, SHA-256 `4aa70c569aaf14f550313b5836346a1ba438491fb4cec61877e615c9e602afa7`
- 서울 건물 파일: 94,007,115바이트, MS949, 31열, SHA-256 `285286d60409e1cc93898146952d9bc4a805c8f955a765c9f0d72e8a6ea16081`
- 활용 가이드: SHA-256 `c024fefff0aa1dd0c812f2cf76db5296440575831e6cba4b313245071cc994c1`
- 이용조건: 공공데이터포털 metadata의 `이용허락범위 제한 없음`
- 원본·metadata·다운로드 목록·영수증은 `artifacts/local/public-data/sagajeong-building-address-20260914-r1/`에 비공개로 보존한다.

출처: [공공데이터포털 건물DB](https://www.data.go.kr/data/15050424/fileData.do), [주소기반산업지원서비스 건물DB 다운로드](https://business.juso.go.kr/jst/jstAddressDownload?menu=7)

## 전수 결과

| 결속 상태 | 건수 | 소비 허용 |
| --- | ---: | --- |
| `OfficialIndividualAddress` | 480 | 공식 주소 조회 가능, 배달·가격·Unity는 별도 관문 |
| `OfficialSharedComplexAddress` | 79 | 41개 공유 주소 그룹, 건물 ID 유지 |
| `RoadAddressCandidate` | 28 | 현재 OSM 주소는 있으나 2026-08 공식 월 전체분에 정확 일치 없음 |
| `NoIndependentRoadAddress` | 0 | 공식 무독립 주소 증거가 없어 임의 승격하지 않음 |
| `Unresolved` | 15 | 주소 근거 없음, 정확 주소 필수 기능 차단 |
| **합계** | **602** | 602/602 중복 없이 하나의 상태 보유 |

공식 결속은 559/602건(92.86%)이다. 기존 건물 주소 580건 외에 동결 OSM 원본의 건물 윤곽 안 주소 지점 6건과 유일한 공식 건물명 일치 1건을 추가 회수했다. 투영 SHA-256는 `86d318afdc34bd3af0e6470ccd49f29692a3f71b888fa0252c0b4aaac84c80bf`다.

## 저장과 검증

- 구현: `사가정건물도로명주소자료.cs`, `sagajeong-building-address-{acquire|self-test|prepare|preview|replay|apply|verify}`
- 자체 시험: 14/14
- 투영 재생: 동일 원본·지도로 동일 SHA-256 통과
- 로컬 MySQL: `hongdal-mysql-1 / hongdal_dev`, 정규화 행 602건, 원본 사본 ID 72, 행 ID 92965~93566
- 첫 적용: 신규 602 / 기존 0 / 독립 재조회 602
- 즉시 재적용: 신규 0 / 기존 602 / `databaseWriteAttempted=false`
- 별도 `verify`: 새 `DbContext`에서 602건과 원본 hash·기준일·투영 내용 일치
- 도구 project build: 경고 0 / 오류 0
- 범위 지정 Fast·Task: `git diff --check` 통과. 공용 검사기는 `eng` C#를 `Guidance/docs only`로 분류해 build·test를 생략했으므로, 위 도구 build·자체 시험·DB 재조회를 별도 실행했다. 상세 로그는 `artifacts/local/validation/20260914-083036`, `artifacts/local/validation/20260914-083045`이다.

## 미정·남은 위험

- 28개 후보 주소는 일변동·폐지·OSM 오류를 구분해야 한다. 2026-09 일변동을 임의로 혼합하지 않고 후속 revision으로 남겼다.
- 15개 미해결 중 지붕·부속 구조물 4개도 공식 `NoIndependentRoadAddress`로 확정할 근거가 아직 없다.
- 주소를 갖는 것은 배달 가능을 뜻하지 않는다. 건물 출입구, 보도·도로 접근 엣지, 정차 규칙, 운영 수령지는 별도로 검증해야 한다.
- 이번에는 API, Unity 자원·Scene·Prefab, Play Mode·Game View, 가격 수집, 주문·배차·길찾기를 변경하지 않았다.

## 다음 질문 하나

28개 후보와 15개 미해결을 처리할 다음 판본은 **출입구 좌표·건축물대장까지 추가해 전수 확정을 목표로 할지**, 아니면 **현재 559건만 주소 확정 기능에 열고 43건은 계속 차단할지** 확정해야 한다.
