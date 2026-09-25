# 2026-09-24 — 기존 레시피 볼륨 복구 조회·로컬 개발 접속

- 화면: 없음. DB·도구·편집 조사 색인만 변경.
- commit/push/배포: 수행하지 않음.

## 원인과 복구 확인

현재 `hongdal-mysql-1`은 `hongdal_mysql_data`를 사용한다. 레시피가 있던 별도 볼륨은 `hongdal_hongdal_dev_mysql_data`다. 현재 컨테이너 환경은 `ssalddel_dev`를 가리키지만 기존 앱 계정의 실제 DB 권한은 `hongdal_dev`에 남아 있다. 비밀번호 입력 문제가 레시피 누락의 원인은 아니었다.

원본에 연결된 실행 컨테이너가 없음을 확인하고 읽기 전용 마운트로 약5.6GB를 `hongdal_recipe_recovery_20260924`에 복사했다. 원본은 변경하지 않았다. 복사본만 MySQL8.4의 내부 복구로 열었으며 네트워크 없음, 포트 없음, `skip_networking=1`, `super_read_only=1`, 이벤트/복제 자동 실행 금지 상태에서 조회했다. 복구 중에만 인증 없는 로컬 소켓 접근을 사용했고 작업 뒤 `hongdal-recipe-recovery-20260924` 컨테이너를 정상 정지했다. 원본과 복구 볼륨·정지 컨테이너는 보존했다.

실제 `hongdal_dev` 조회 결과:

- 식약처 `mfds-cookrcp01` 레시피1,146건.
- 재료1,903개, 레시피-재료 관계12,781건.
- 음식 후보1,139개는 PendingReview. 공개 승인 자료로 바꾸지 않았다.

## 비밀번호 없는 개발 접속

사용자의 로컬 개발 편의 요청에 따라 **현재 DB의 `local_developer@localhost` 계정만 빈 비밀번호**로 추가했다. 권한은 `hongdal_dev.*` 내부 개발 작업이며 전역 관리/다른 DB/GRANT OPTION은 주지 않았다. root와 기존 앱 계정의 비밀번호는 바꾸지 않았다. MongoDB·Redis·다른 프로젝트 DB도 변경하지 않았다.

```powershell
./eng/verification/local-mysql-console.ps1
./eng/verification/local-mysql-console.ps1 -Sql 'SHOW TABLES;'
```

도구는 compose 소유·작업 경로·127.0.0.1:13306을 확인한 후 Docker 내부 소켓으로 접속한다. `-EnableLocalAccount`는 새 환경에 최초 설치할 때만 사용하며 기존 계정이 있으면 실패한다. 컨테이너 내부 localhost 전용이므로 PC의 일반 TCP DB 도구나 다른 컨테이너가 비밀번호 없이 접속하는 설정은 아니다. Docker 권한을 가진 로컬 사용자는 개발 데이터를 수정할 수 있다. 원격/공유 운영 환경용이 아니다.

검증: 로컬 소켓 성공, 다른 컨테이너의 TCP 접근은1045 거절. 현재 hongdal_dev 테이블 수100 유지. 전체 DB의 `skip-grant-tables`를 켜지 않았다. 계정 되돌리기는 관리자가 `DROP USER 'local_developer'@'localhost';`를 실행하면 되며 이 작업에서는 실행하지 않았다.

## 레시피 재사용 결과

별도 배달 프로젝트 `recover_recipe_snapshot.py`로 복구본에서 기존 조회 DTO에 대응하는 레시피 필드를 읽어 비공개 동결 JSON으로 저장했다. HTTP API를 호출한 것이 아니다. 원문/출처/수집시각/권리/원본 checksum을 보존하고 이미지·인증정보는 포함하지 않는다.

- 사본 SHA256: `bae7eff431556bbec9a8939efc4eb5a95191556d4f37b1d0973fd721058fa0ed`
- 실제 파일: 배달 `edit-kit/work/record-archive/official-recipes/<SHA256>.json` (Git 제외).
- 최초 동일명 비교는 연결0건. 강제 유사 매칭을 하지 않았다.
- 명시적 일반 구성 후보로 닭강정1건을 선택해 기존 SQLite에 출처 연결을 저장·독립 재조회했다. 새 조사 판본 `62278249ed914a8b8c6e9d7d1992406f29619bfb63a3952b013b917f09328aba`. 기존 조사 판본 보존.
- `채소비빔밥`의 고기/조개, `치즈리조또`의 우유180mg/아기치즈 표기처럼 제목만으로 예상하기 어려운 구성·단위가 있어 일반 표준으로 일괄 처리하지 않는다. 닭강정도 카레/요거트 소스 변형이며 매장의11가지 맛 레시피가 아니다.
- Python16/16, 실제 DB 사본/관계 저장·재조회·멱등 확인. 실제 서버 API·앱·Unity 검증은 없음.

## 남은 경계

이 작업은 **기존 자료의 복구 조회와 안전한 재사용**이다. 예전 전체 DB를 현재 DB에 덮어쓰거나 compose 볼륨을 교체하지 않았다. 현재 DB와 예전 DB의 권한·schema·Migration·업무자료를 대조한 후 통합 대상을 별도로 결정해야 한다. 공공 레시피의 전체 메뉴 매칭, 재료별HS 확정, 가격 계산도 미완료다. 새 외부 자료 수집·영상 편집·게시 없음.
