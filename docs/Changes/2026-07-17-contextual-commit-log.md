# 2026-07-17 맥락별 변경 기록

이번 작업 트리의 변경을 기능과 되돌림 경계에 따라 나눈 커밋 기록입니다.

| 커밋 | 변경 축 | 화면 변경 | 시각·검증 근거 |
| --- | --- | --- | --- |
| `fcbb7faf` | 역할별 입고·출고, 개별·공동주문, 상차·하차 API와 ViewModel | 간접 | 서버·공통 UI 테스트로 역할 관계, 상태 정규화와 DI 확인 |
| `37fa3329` | YouTube 음식 후보와 Amazon 참고자료 수집 | 화면 없음 | Controller, 저장소, 모델, 외부 Adapter 테스트 |
| `8bb99054` | 커뮤니티 게시판, 음식 발견, 공동행동 작업실 | 있음 | [커뮤니티 홈](../assets/changes/2026-07-17-contextual-commits/community-home.png), [공동행동](../assets/changes/2026-07-17-contextual-commits/community-collective-actions.png) |
| `51456e31` | 판매 페이지 초안 API·ViewModel·Razor 조립 | 간접 | 공통 UI 빌드와 판매 페이지 서비스 테스트, 별도 캡처 없음 |
| `e91c5f69` | 역할 기반 광고 캠페인 계획과 플랫폼 Adapter | 화면 없음 | 캠페인 계획 단위 테스트, 운영 게시 기본 비활성 |
| `c40a4cad` | 기사 운행 프로필과 Android 네이티브 지도 | 간접 | 계약 단위 테스트와 Android 구현 검토, 별도 캡처 없음 |
| `41adecf5` | KAMIS·USDA 가격 수집 예약 작업 | 화면 없음 | 배치 Runner·일정 단위 테스트 |
| `3e3bc5a7` | 공개 Preview Site 구성 갱신 | 간접 | 정적 빌드 소스 확인, 별도 캡처 없음 |
| `730a855d` | 상태 확인, DB 초기화, 컨테이너·비밀 파일 배포 경계 | 화면 없음 | 전체 서버 빌드·테스트와 설정 비밀값 검사 |
| `e10e270e` | 커뮤니티 게시글 판매 제안 계약·저장·검증 | 화면 없음 | DTO·RDB 마이그레이션·UseCase 빌드와 전체 회귀 테스트 |
| `29a2141b` | Docker context의 에이전트 임시 파일 제외 | 화면 없음 | `.dockerignore` 범위 확인 |
| `676aabb9` | 커뮤니티 판매 제안 작성 UI와 초안 ViewModel | 간접 | 공통 UI 빌드와 게시글 입력 검증, 별도 캡처 없음 |
| `edd3e4ac` | Docker Compose 비밀 주입·초기화·상태 확인 절차 | 화면 없음 | Compose 설정·환경 예시·배포 문서 확인 |
| `590fbe38` | 커뮤니티 판매 제안 상세와 구매 문의 UI | 간접 | 공통 UI 빌드, 별도 캡처 없음 |
| `86573770` | 게시글 목록의 판매 상태·가격·잔여 수량 요약 | 간접 | 공통 UI 빌드, 별도 캡처 없음 |
| `ca4744ae` | 전용 공공데이터 DbContext와 주 Context 모델 분리 | 화면 없음 | 모델 격리 테스트와 전체 회귀 테스트 |
| `1a6e8f2c` | 판매 제안 null 입력 검증과 초안 테스트 | 화면 없음 | 판매 제안 ViewModel 단위 테스트 |
| `1ecfdec8` | 주 Context 모델 격리 회귀 테스트 | 화면 없음 | 전용 Entity 제외 단위 테스트 |
| `02e28fdc` | Docker Compose 농수산물 배치 cron 외부 설정 | 화면 없음 | Compose 환경변수와 예시 값 확인 |
| `a67cc0b3` | Preview Site 커뮤니티 판매글 작성·조회·문의 체험 | 간접 | 정적 빌드와 세 개 공개 경로 산출물 확인 |
| `refactor: modularize Apify actor integration` | 공통 Apify Gateway와 Amazon Adapter 책임 분리 | 화면 없음 | Apify 관련 단위 테스트 8개와 서버 빌드 |

## 확인 사항

- 로컬 `appsettings.Local.json`의 API key와 secret은 Git 추적 및 Docker context에서 제외했습니다.
- `artifacts/apify-amazon`의 외부 응답 샘플은 생성 산출물로 분류해 커밋하지 않았습니다.
- 커뮤니티 캡처는 실제 렌더링 화면이며 개인정보·계좌·결제·운송 증빙을 포함하지 않습니다.
- 판매, 기사 앱과 Preview Site는 별도 PNG가 없어 간접 확인으로 기록했습니다.
- 전체 스키마를 빈 모델로 바꾸던 잘못된 자동 생성 마이그레이션은 검토 과정에서 폐기하고 커밋하지 않았습니다.
