# [기획 · 월드·데이터·관찰 · PLAN-SYSTEM-KOREAN-PENINSULA-PUBLIC-DATA-WORLD · 구현 r75]

## 범위와 현재 결과

P1 제61류 일상 양말·스타킹 HS6 8개를 `chapter61-everyday-hosiery-03`으로 준비했다. 면·합성섬유 기본 양말과 일상 스타킹류를 의료용 압박 양말·특수 스포츠용·희소 소재 품목보다 먼저 보았고, 의료용 압박 양말 `611510`은 이번 묶음에서 제외했다.

- 관세청 2026 HSK10 자식 8개 결속: 모두 소비재. 성질 분류는 소매 적합성의 확정 근거가 아니다.
- UN Comtrade 2025년 대한민국 수입 공개 preview 355행 동결
- 합성섬유 팬티스타킹·면 기본 양말 후보 2건 `PendingHumanReview`, 나머지 6개 `SearchNoCandidate`
- 배치 자체시험 12/12, 정규화 130행
- 전수 대장 자체시험 10/10, 로컬 생성 기준 완료 487개·후보 212개·검색 무결과 227개·대기 5,125개

로컬 Docker의 `hongdal-mysql-1`이 이 수집기가 허용하는 `docker-compose.dev-deps.yml`이 아니라 모바일 현장 시험 override로 기동되어 `ComposeMismatch` 보호 검사가 작동했다. 따라서 이번 판본에서는 MySQL 저장·독립 재조회·동일 입력 중복 방지를 완료하지 않았고 반복 배치 누계도 저장 완료 기준 48개·6,647행으로 유지한다. 실행 중인 다른 환경을 임의로 재구성하지 않았다.

## 재개 지점

허용된 개발 의존성 compose로 MySQL을 다시 기동한 뒤 이 배치의 `apply → verify → apply`와 전수 대장의 `apply → verify → apply`를 먼저 통과한다. 그 뒤에만 다음 P1 후보군인 제62류 일상 직물 셔츠·블라우스·바지를 검토한다. 원격 DB·게시·Unity·commit·push는 수행하지 않았다.
