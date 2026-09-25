# 12개 재료 공통 조회 첫 층 r34

- 기준일: 2026-09-24
- 승인 근거: r33 설명 뒤 사용자 "뭐 그렇게 진행해 보자고."
- 기준 기획: [r33](horizontal-layer-development.plan.r33.md)
- 기준 SHA256: `fae1c9cf6228d4b5b32acb3bc77f2a2bce97c7b2f8f8c3ee8fe17d9bc912edf0`
- 범위 상태: `Approved / ReadyToDispatch / Accepted` — 같은 스레드의 명시 요청으로 아래 첫 층만 수용. Git 교대·새 게임 Goal·다른 층 실행 승인이 아님.

## 소유 경로와 구현 범위

- `eng/Ssalddel.PublicDataPortalImport/메뉴재료기반Query.cs`와 해당 자체 시험: 공통 읽기 결과·출처/결손 검증.
- 같은 폴더 `Program.cs`: 새 명령 진입 분기만 추가. 기존 변경·가격 수집 명령은 보존.
- `eng/public-data/menu-ingredient-prices/README.md`: 조회 사용법.
- 이 문서·해당 README·`docs/AI/PLANNING.md`·`docs/AI/CURRENT_WORK.md`: 결과와 실제 검증 수준 기록.
- 실행 산출물은 Git 제외 `artifacts/local/public-data/menu-ingredient-foundation/`에만 생성.

기존 12개 scope·원본 receipt·로컬 MySQL의 저장된 후보/관측을 읽어 식별자·규격 조건·자료원별 분류 후보·근거 연결·미확인 이유를 반환한다. 업무 원장·새 Entity/Migration·새 공공 API·외부 수집·Unity는 변경하지 않는다. 기존 공공자료 CLI를 읽기 진입점으로 사용하며 HTTP/UI 제공 완료로 보고하지 않는다.

식약처/관찰 메뉴의 판본 참조는 보존하지만, 해당 재료와 개별 메뉴/레시피의 연결을 확인할 수 없으면 연결 결손으로 남긴다. 재료 이름으로 원본 ID·원산지·매입 경로를 자동 확정하지 않는다.

## 검증 계획

공통 결과 결정성·입력 hash·중복/모순·분류 코드 경계·일부 결손·원 단위·원산지 미확인·읽기 전용을 자체 시험한다. 실제 로컬 DB에서 두 독립 조회의 결과 hash와 원본 행을 대조한다. 원문 비밀값·운영 개인정보는 결과로 복사하지 않는다. 원본/기존 저장 자료를 덮어쓰지 않으며 build·자체 시험·실제 DB 증거를 분리한다.

## 실행 결과

상태: `ReadOnlyFoundationImplemented / LocalMySqlReadbackVerified / GlobalProductAndMenuRecipeEdgesUnresolved`.

- [조회 코드](../../../../../eng/Ssalddel.PublicDataPortalImport/메뉴재료기반Query.cs)·[자체 시험](../../../../../eng/Ssalddel.PublicDataPortalImport/메뉴재료기반QueryTests.cs) 추가. Program에는 `menu-foundation-*` 분기만 추가했다. [실행 안내](../../../../../eng/public-data/menu-ingredient-prices/README.md).
- 실제 조회: 12개 재료/저장된 후보12개, HS2022 후보16개, KAMIS 저장 근거가 연결된 재료10개. 190개 기존 관측 중 재료별 참조185개와 공통 주의/실패 이력5개로 나눠 보존했다. 참조 수는 독립 가격 수가 아니다.
- 커피·밀가루는 KAMIS 후보 없음. 실제 음식점의 원산지는 12개 모두 미확인이다. 전역 상품/식약처 재료 ID와 개별 메뉴·레시피 연결도 자동 확정하지 않고 결손 이유로 조회한다.
- 공통 결과 `menu-ingredient-foundation.v1`은 참조·규격·원 단위·출처·관측/수집 시각·판본·품질을 담는다. 새로운 가격 계산·공급자 추천·실제 업무 처리는 하지 않는다.
- 파일은 비공개 로컬 상태 사본으로 저장하며 기존 DB가 원자료를 유지한다. 신규 DB 저장이나 자료 이관 완료로 보고하지 않는다.

| 재료 | HS 후보 수 | 기존 근거 참조 수 | 국내 가격 근거 |
| --- | ---: | ---: | --- |
| 쌀 | 1 | 11 | 있음 |
| 밀가루 | 1 | 7 | KAMIS 후보 없음 |
| 소고기 | 2 | 51 | 있음·축산물 제한 유지 |
| 돼지고기 | 2 | 23 | 있음·축산물 제한 유지 |
| 닭고기 | 2 | 15 | 있음·축산물 제한 유지 |
| 우유 | 1 | 9 | 있음·축산물 제한 유지 |
| 감자 | 1 | 11 | 있음 |
| 양파 | 1 | 10 | 있음 |
| 마늘 | 1 | 13 | 있음 |
| 당근 | 1 | 12 | 있음 |
| 토마토 | 1 | 10 | 있음 |
| 커피 | 2 | 13 | KAMIS 후보 없음 |

## 검증 증거

- 도구 프로젝트 전체 의존성 포함 build: 경고0·오류0.
- 신규 자체 시험43/43: 정렬/결정성, DB surrogate ID 비의존, 입력 수치 변화 판본 반영, 부분 누락·빈 저장소 표시, 중복·hash·출처·범위·고유 식별자·코드 충돌 거절, 원 단위·원산지/레시피 미확인·축산물 주의·원문 비노출. 기존 가격 조사 시험27/27 회귀 통과. 실제 CLI의 `menu-foundation-apply` 요청은 DB 접근 전에 `IngredientFoundationModeInvalid`로 거절됨을 확인했다.
- 실제 로컬 `hongdal-mysql-1 / hongdal_dev` 읽기 전용 트랜잭션에서 독립 조회 두 번, 별도 재실행에서도 동일 출력 hash. query에는 SaveChanges/Upsert/업무 Command 호출이 없다.
- 기존 `menu-prices-verify`로 190/190 원본·수치·단위·판본 재검증. 재실행 전후 독립 SQL 관측 행수190과 내용 checksum `378623472037` 일치. 이 checksum은 보조 비교이며 출력 SHA256/원본 대조를 대신하지 않는다.
- 결과 SHA256: `080008048ef2172e0d86c653bdee8d544f50b6c247ad13d6d47d19a85762322d`.
- 원본 receipt SHA256: `3c49e51aeba20f0e51d6a83639ec811b656039a308cae6e140950ec9b0ab39ac`.
- 결과 경로: `artifacts/local/public-data/menu-ingredient-foundation/080008048ef2172e0d86c653bdee8d544f50b6c247ad13d6d47d19a85762322d.json`. Git 제외 확인·실행 계정 비밀값 포함 검사0건. 출처 URL의 인증 파라미터도 거절한다.
- 소유 경로8개 지정 Fast·`git diff --check`, 미추적 파일 포함 공백 검사, 로컬 링크7개·r33 기준 hash 확인 통과. Fast는 eng 도구를 문서성 범위로 분류해 build/test를 생략했으므로 위 별도 실행 결과를 검증 근거로 사용한다.

## 남은 범위

첫 층의 **조사 ID 기반 공통 조회**를 구현했다. 전역 제품/식약처 재료 ID 매핑과 재료별 메뉴–레시피 연결은 아직 결손이며 이 기록을 전체 데이터 통합 완료로 해석하지 않는다. 이후 층에 연결할 때 이 결손을 보존하거나 검증된 참조로 보완한다. 두 번째 공급 관계 층, 실제 제조·입출고·납품, HTTP/API·화면·Unity·영상은 이번에 구현/실행하지 않았다.

기존 223개 변경을 정리하거나 함께 stage하지 않았다. 신규 외부 수집·원본 DB 변경·Migration·운영 배포·commit·push 없음. 새 디오라마 규칙 후보 없음.
