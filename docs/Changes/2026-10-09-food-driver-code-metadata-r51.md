# 음식 기사 책임 메타데이터·탐색 지도 r51

## 변경

[승인 기획](../AI/Planning/공통/PLAN-OPERATIONS-DRIVER-VERTICAL-REFACTOR/driver-code-metadata.r51.md)에 따라 기존 `SsalddelCodeMetadataAttribute`를 음식 기사 계약·서버·DB·공통 화면에 적용했다. 기능 키는 `food-driver-work`, 핵심 단계 45개다. 현재 업무 조회와 호환 작업 공간·일일 정산·완료 상세를 분리하고 SQL·Mongo·Redis 권위, 순수 계산·실제 저장·위임 효과와 잔여 복구 경계를 설명한다.

[작업 영역](../../eng/work-areas/food-driver.json)은 읽기 순서·소스 범위·필수 단계 및 41개 명시 소스 결속을 소유한다. 네이티브 시작/정본 조회/암호화 복구/화면/정산/전경 알림의 6개 필수 연결을 포함한다. 각 partial과 Razor 원본, XAML/코드부를 명시하며 줄번호는 실제 심볼에서 계산한다. Android DLL은 로드하지 않고 reflection 그래프와 별도 소스 연결로 출력한다.

기존 CodeMap에 `--work-area food-driver`를 추가해 [Markdown 지도](../AI/generated/food-driver-code-map.md)와 JSON을 생성한다. 필수 단계·경로·심볼·XAML 결속·중복·reparse 경로·오래된 출력은 오류이며 범위 밖 보조 형식은 경고다. 기본 Simulation·Unity 출력은 유지한다. [사용법](../Architecture/FoodDriverCodeNavigation.md).

## 검증

| 확인 | 결과 |
| --- | --- |
| scoped Fast | 264/264 통과, 새 메타데이터/소스 결속 시험 79개 포함 |
| scoped Task | 서버·공통 UI 9,780/9,780, Simulation 2,001/2,001, Unity 970/970 통과. 실행 합계 12,751개, 실패 0. 환경 의존 Mongo 시험 15개 미실행 |
| 빌드 | `Ssalddel.v3.5.slnx`, `Ssalddel.Simulation.slnx`, `Ssalddel.Unity.slnx` 통과 |
| 지도·책임 | 기본 Simulation·Unity 지도, 음식 기사 지도 `--check` 및 기존 E 책임 `--strict` 통과. 기본 생성 지도 파일 변경 없음 |
| CLI 독립 시험 | 별도 복사본에서 정상 `--check` exit 0, 소스 심볼 줄 변경 후 오래된 지도 exit 2, 필수 단계 누락 exit 1·CODEMAP020, reparse 소스 exit 1·CODEMAP054 |
| 검증 routing | 음식 기사 native/server/common/manifest/tool/test/map 경로 포함, 무관 음식점·화물·일반 문서·obj 제외 통과 |
| 보존 | 특성 부착 업무 파일 35개는 새 특성/using·개행 차이 제거 뒤 기준 SHA-256과 일치. 검증 입력 소스 46개는 전후 해시 동일 |
| 문서 | 새 안내·기획·변경 기록·생성 지도의 로컬 링크 103개 확인, 이후 최종 기획 결과 링크도 추가 검사 |
| ARM64 기사 APK | Debug `kr.ssalddel.fdriver`, 0.1.0/1, 41,408,228 bytes. v2/v3 서명 검증, 기존 로컬 네이버 설정과 생성 리소스의 일치만 확인(값 비공개). 빌드 오류 0·기존 Android API/알림 관련 경고 35개 |

로그는 `artifacts/local/food-driver-code-map-r51/`, Fast 상세는 `artifacts/local/validation/20261009-221742/`, Task 상세는 `artifacts/local/validation/20261009-222236/`에 있다. 초기 NoRestore 시도의 EvidenceMap·Simulation.Tools 자산 누락을 확인한 뒤 복원한 최종 실행만 통과로 기록한다. 지도 오류 0·미분류 탐색 후보 경고 6개를 유지한다.

APK 경로는 `FDriverApp/bin/Debug/net10.0-android/android-arm64/kr.ssalddel.fdriver-Signed.apk`, SHA-256은 `7DB521DE4F634C836BCC0F680FE3342BCDF9755BC1582EEAB3E41E878629A533`이다. 소스/키 리소스 확인·서명 검증은 실제 단말 실행과 다르다.

화면 레이아웃·문구·업무 동작·API/JSON·DB 스키마는 변경하지 않는다. 특성 추가와 탐색·검증 도구를 수정한 작업이며 화면 캡처/기기 검증으로 보고하지 않는다. 실제 휴대폰·지도/GPS·푸시·Azure·공용 DB·실운영·실지급·Unity·영상·commit/push는 하지 않았다. 이전 dirty 변경을 보존한다.
