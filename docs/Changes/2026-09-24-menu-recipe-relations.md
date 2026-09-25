# 2026-09-24 — 방문 음식점 메뉴·공공 레시피 연결

- 화면: 없음. 별도 배달 프로젝트의 비공개 로컬 SQLite와 조사 도구만 변경.
- 선행: [기존 레시피 볼륨 복구](2026-09-24-local-recipe-volume-recovery.md).

## 최신 후속 — 메뉴60건 전체 대조

별도 배달 프로젝트 `edit-kit/menu-recipe-review.md`에 전체 메뉴별 결과를 기록했다. 기존 식약처 사본1,146건의 제목과 채택/반례 후보의 재료·필요한 조리 순서를 대조하여 메뉴21개에 레시피13종, N:M 연결22건을 저장했다. 최초1개 메뉴/1개 연결 대비 추가20개 메뉴/21개 연결이다. 실제 매장 레시피 확정이나 재료 전수 확인을 뜻하지 않는다.

- 메뉴60건 전부 대조 이유 저장: 유형 참고 후보21, 메뉴/세트 구성 확인 필요11, 현재 사본에서 적절한 후보 없음28. 미조사와 연결0건을 구별한다.
- 예: 고기 반찬인 동글동글도넛을 카페 도넛에 연결하지 않았다. 김밥·피자·버거는 조립 유형만 참고하고 실제 속재료/브랜드 소스/사이드는 만들지 않는다.
- 최신 연구ID: `1e4d757883b37eefc67903afa314f9430202800e24c7eef93b0015aa1e9eebfb`.
- 입력: `official-recipe-links.review.json`, `official-recipe-review.v1.json`. 원본 사본 hash `bae7eff431556bbec9a8939efc4eb5a95191556d4f37b1d0973fd721058fa0ed` 유지. 원본 수집일과 이번 비교일을 분리한다.
- 코드: `menu_recipe_review.py`의 판본/hash·전체 메뉴 누락·중복·연결 모순 검증 및 읽기전용 결과 조회. 기존 attach/저장 파이프라인 확장. v3 메뉴 JSON에 검토 이유를 넣고 기존 v2와 관계 테이블은 호환 유지.
- 검증: Python31/31, 실제 SQLite 저장/재입력/독립 재조회, 무결성 정상·외래키 오류0. 백업의 모든 기존 테이블 행을 EXCEPT로 대조하여 원문 보존 확인. 새 판본까지4개, 판본별 메뉴행240개지만 관찰 메뉴는60개다. 전체 연결23행은 과거1+최신22로 구별한다.
- 사전 백업: 배달 `edit-kit/work/record-archive/visits.before-full-recipe-review.20260924.sqlite3`. DB·백업·원문JSON은 Git 제외 유지.

자료 검토 결과는 `PendingReview`이며 실제 배합·알레르기·원산지·원가·HS를 자동 확정하지 않는다. 신규 외부 수집·MySQL/Mongo 원본 수정·서버API·Unity·영상 편집·commit·push는 하지 않았다. 아래 최초 관계 도입 증거는 그대로 보존한다.

## 최초 관계 도입

식약처 원본을 유지하고 방문 메뉴와 레시피 ID 사이에 `menu_recipe_mappings` N:M 관계를 추가했다. 원본 MySQL과 로컬 SQLite는 서로 다른 DB이므로 출처/RecordKey/원본checksum을 보존한 `official_recipe_reference_snapshots`에 외래키를 건다. 레시피 전체 이관 대신 실제 참조1건만 보관했다.

음식점→메뉴→연결→공공 레시피 참고 사본을 조회한다. 관계에는 조사 판본·일반 구성 참고·검토 대기·연결 이유를 기록한다. 매장 실제 레시피 승인·HS 확정으로 바꾸지 않는다. 메뉴1개-여러 레시피, 여러 메뉴-같은 레시피를 지원한다.

배달 변경: `menu_recipe_relations.py`, `test_menu_recipe_relations.py`, 기존 `research_menu_catalog.py`, `edit-kit/menu-recipe-relations.md` 및 안내 링크. 기존 JSON 증거와 조사 판본은 그대로 두고 재구축 가능한 조회 테이블만 추가했다.

## 최초 검증과 범위

Python22/22, 실제 DB 백업/관계 멱등 추가/외래키/별도 프로세스 JOIN 조회 확인. 큰손닭강정 회기점의 기록된 메뉴1건이 공공 닭강정 레시피1건과 검토 후보로 연결됐다. 전체60개 메뉴 전수 매칭은 아니다. MySQL 원본과 복구 볼륨·Unity·영상은 변경하지 않았다. 서버/API 실행·commit·push 없음.
