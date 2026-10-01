# 음식점 메뉴 입력 정합성 r6 — 구현·검증 결과

2026-09-27. [승인 범위](restaurant-menu-validation.r6.md), [로컬 수용 명세](restaurant-menu-validation.r6.work-order.json).

## 변경

- `음식점메뉴입력Policy`를 추가해 화면/UseCase의 trim 후 메뉴명200자·소개1000자·사진 URL1000자, 음수 가격 거절, 선택 HTTPS 절대 URL 검사를 공유한다. 등록/수정 모두 저장 전에 검사한다. 문자열을 자동 절단하거나 URL을 다운로드하지 않는다.
- 기존 API 필드·DB 스키마·인증/음식점 범위·revision 사전 검사·메뉴명 기반 재등록 방어를 보존한다. 기존 비HTTPS 사진 주소는 조회를 막지 않지만 다시 저장할 때 수정 또는 제거해야 한다.
- 잘못된 입력의 등록/수정 거절과 저장상태 불변, 최대 길이·빈 사진·HTTPS 사진, 같은 요청 재시도, 다른 가게 조회 제외, 수정 후 목록 재조회 시험13건을 추가했다. 기존2건을 합해 메뉴 UseCase15건이다.

## 실행 증거

| 실행 | 결과 | 기록 |
| --- | --- | --- |
| 최초 Fast | 전체 솔루션 build 성공, 집중37/40. 새 경계값 fixture가 1001자인 계산 오류3건을 수정 | `artifacts/local/validation/20260927-180035/` |
| 최종 Task | `Ssalddel.v3.5.slnx` build 성공. 전체5454 중5447 통과·7 실패·skip0 | `artifacts/local/validation/20260927-180721/` |
| 최종 집중 재실행 | 메뉴 UseCase15 + 화면 구성25 = 40/40 통과. Task 빌드 산출물로 `--no-build --no-restore` 실행 | `artifacts/local/validation/restaurant-menu-validation-r6-final/menu-final.trx` |

전체 실패7건의 testName 집합을 이전 `20260927-134624/Ssalddel.Tests.trx`와 비교했고 차이는0이다. 전체 통과를 주장하지 않으며 이번 범위에서 기존 실패를 수정하지 않았다. 패키지 버전/기존 nullable 등 build 경고는 남는다.

## 증거 상한과 후속

- InMemory UseCase와 화면 구성 시험이며 실제 HTTP 인증·MySQL/MongoDB·동시 경합·Android 터치·네이티브 캡처는 아니다. APK/설치/배포·실결제 없음.
- 수정 revision은 여전히 사전 비교다. DB 동시성 토큰이나 요청 ID 영속 멱등성, 앱 종료 후 복구를 구현했다고 주장하지 않는다.
- r9 입력 예제의 출처3개가 변경되어 현황표에 `IntakeHashMismatch`가 표시된다. 과거 초안·사람 확인·승인 hash는 소급 교체하지 않았다. 다음 입력 검토에서 이 결과와 공유 정책 파일을 새 판본으로 연결해야 하며 r9의 기존 충돌 설명은 역사적 초안이다.
- 다음은 격리 인증 API/DB에서 메뉴 저장·재조회 확인과 새 입력 판본 검토다. 제품 적용과 사람 확인을 자동 승인하지 않는다.
- commit·push·DB migration·운영 설정 변경 없음.
