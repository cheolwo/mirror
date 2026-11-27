# 인계 — 실시간 자료 주기표(R1)

2026-11-26 12:52 · Claude → Codex · 내부 검토용

## 시작점

- [실시간 자료 구조 개발 계획](2026-11-26-Claude-개발계획-실시간자료-Firebase.md)의 R1이다.
- 사용자 결정(12:50): **Firebase 무료 요금제(Spark)**. 그래서:
  - Cloud Storage · 예약 함수는 쓰지 않는다. 파일은 **Hosting**, 5~10분 값은 **Firestore**에 싣는다.
  - 수집기는 **집 PC 예약 작업**이다.

## 할 일

### 1. 층별 주기표

`artifacts/local/public-data/realtime-cadence-20261126/cadence.json`(+ 같은 내용의 `cadence.md`)

- **대상:** 게임에 이미 들어간 팩 14개(`Resources/hongdal-data-*.json`), 오늘 정한 세부칸 층(`field-catalog` · `role-tile` 추가분).
- **층마다 적을 칸:**
  - `layer`(fieldKey 또는 팩 이름) · `source` · `datasetId` · `officialUrl`
  - `cadence`: `5min` · `10min` · `hourly` · `daily` · `monthly` · `quarterly` · `yearly` · `irregular`
  - `lag`: 관측일과 공개일 사이 지연. 예: 생활인구는 며칠
  - `access`: 열린 API / 파일 내려받기 / 신청 필요
  - `keyNeeded`: 키 종류 이름만 적는다. **값은 적지 않는다.**
  - `dailyCallLimit`
  - `licence`: 공공누리 유형 · 출처 표시 문구 · 재배포 가능 여부
  - `publicGrade`: `public` / `bands-only` / `internal`. P0 기준과 같게.
  - `firebaseTarget`: `hosting-file` / `firestore-live` / `none`
- **규칙:** 공식 문서에 근거가 없는 칸은 `unknown`으로 둔다. 추정하지 않는다.

### 2. 서울 실시간 도시데이터 확인

- 주요 장소 목록(현재 121장소, 열린데이터광장 OA-21285 「서울시 주요 121장소 목록.xlsx」)을 받는다. 장소코드 · 이름 · 영역을 SQLite로 옮긴다.
  - **사가정역 · 중랑구 · 면목동 둘레가 포함되는지** 적는다.
  - 가장 가까운 장소와 거리를 적는다.
  - 영역 shp는 EPSG:5179 세부칸 `(q, r, slot)`과 교차 면적을 구한다.
- 인증키로 **한 장소만** 한 번 불러 표본을 남긴다. 응답 필드 목록과 갱신 시각 필드를 확인한다.
  - 분야별 실제 갱신 간격: 인구 · 상권 · 도로 · 날씨 · 따릉이
  - 키가 없으면 신청 필요로 적고 멈춘다. 키 이름만 남긴다.

### 3. 날씨 실황 확인

- 기상청 초단기실황(또는 이미 쓰는 지점 관측)에서 사가정 격자 한 곳을 한 번 부른다.
- 갱신 간격 · 공개 지연 · 키 필요 여부를 적는다.

### 4. 무료 한도 어림

- 층별 크기(지금 팩 바이트) × 갱신 주기로 하루 Hosting 전송량을 어림한다. 사용자 10명 · 100명 기준.
- 실시간 문서는 구 하나 · 5분 폴링 기준 하루 Firestore 읽기를 어림한다.
- Spark 한도와 대조한다: Hosting 전송 하루 360 MB · Firestore 읽기 하루 5만.

## 지킬 것

- 인증키 값은 어디에도 쓰지 않는다. 수집기 내부에서만 읽는다(`map_public_api_transport.credentials()` 방식).
- 공식 API · 공식 파일만 쓴다. 예시 키를 쓰지 않는다.
- 보안각서 자료(도로명주소 · 내비게이션용DB · 지하철 층)는 `internal`, `firebaseTarget: none`이다.
- 생활인구 · 체류 원값은 `bands-only`다.
- Firebase 프로젝트를 만들거나 무언가를 올리지 않는다. 이번 일은 **조사와 표**까지다.
- 게임 코드 · Resources · `Planning/**`은 고치지 않는다.

## 반환

`2026-11-26-Codex-실시간주기표-반환.md`에 다음을 적는다.

- 표 경로와 층 수
- 121장소에 사가정이 드는지
- 표본 호출 결과(필드 · 갱신 시각)
- 무료 한도 어림
- `unknown`으로 남긴 칸 목록
