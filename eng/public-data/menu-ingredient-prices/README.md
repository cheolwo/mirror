# 메뉴 재료 가격 조사 r1

결과·가격 예시·주의사항은 [조사 기록](../../../docs/Changes/2026-09-24-menu-ingredient-price-research.md), 대상·조건은 [scope.r1.json](scope.r1.json)이 소유한다.

기존 레시피 관계를 바탕으로 후보 재료 12종/HS2022 16코드를 비교한다. 실제 음식점 배합·통관·매입가격 확정이 아니다. 모든 수치는 검토 보류 상태로 기존 로컬 MySQL에 저장한다. 수입 자료는 재수집하지 않고 기존 원본 계보를 재사용한다.

## 실행

`eng/Ssalddel.PublicDataPortalImport` 빌드 후 다음 모드와 저장소 절대 경로를 전달한다.

- `menu-prices-self-test`: 합성 검사만. 네트워크/DB 접근 없음.
- `menu-prices-acquire`: 기존 수입/HS 조회, 수출 4개 묶음과 KAMIS 6개 요청. 동일 폴더의 파일을 덮어쓰지 않는다. KAMIS 조건은 서울 요청/2026-09-23/원 조사 단위(N)로 고정.
- `menu-prices-retry-exports`: 실패한 수출 묶음만 `retry1` 별도 파일로 최대 한 번 보충. 성공 사본/실패 이력 보존.
- `menu-prices-preview`: 파일 hash/파싱 검증과 기존 DB 조회. 저장 없음.
- `menu-prices-apply`: 기존 원본 등록/정규화 저장소, 트랜잭션·GET_LOCK·충돌 거절·멱등 추가. 운영 host 실행 없음.
- `menu-prices-verify`: 같은 입력으로 재구성 후 새 DB context에서 원본 hash/수치/단위/판본/품질 검증. 전체 비공개 조회 사본 `observations.json` 생성.

현재 공용 개발 컨테이너는 `로컬공공자료Db`의 소유/loopback/DB 검증을 사용한다. 필요한 연결값은 기존 비밀 설정에서 실행 프로세스의 `SSALDDEL_PUBLIC_DATA_LOCAL_CONNECTION`으로만 주입한다. 값 자체를 명령 이력·문서·로그에 적지 않는다. DB/앱 계정의 인증을 제거하지 않는다. KAMIS 키는 기존 UserSecrets를 사용하고 인증값을 반향하는 `condition`은 저장하지 않는다.

## 공통 식별·분류·출처 조회 — 첫 층

[r34 구현 기록](../../../docs/AI/Planning/시스템/PLAN-SYSTEM-FRANCHISE-OPERATIONS/ingredient-foundation.implementation.r34.md). 가격의 재수집·가격 결정이 아니라 기존 12개 재료를 같은 형식으로 읽는 경로다.

```powershell
dotnet eng/Ssalddel.PublicDataPortalImport/bin/Debug/net10.0/Ssalddel.PublicDataPortalImport.dll menu-foundation-self-test C:/Users/user/source/repos/Hongdal
dotnet eng/Ssalddel.PublicDataPortalImport/bin/Debug/net10.0/Ssalddel.PublicDataPortalImport.dll menu-foundation-query C:/Users/user/source/repos/Hongdal
```

- 자체 시험은 DB/네트워크/파일 쓰기 없이 합성 입력만 사용한다. 실제 조회의 연결값은 위와 같은 실행 메모리 설정을 사용한다.
- `query`는 scope와 receipt의 파일 hash를 검증하고 기존 MySQL dataset을 읽기 전용 트랜잭션 두 개로 독립 조회한다. DB 실패를 파일 사본/샘플로 대체하지 않는다.
- `menu-ingredient-foundation.v1` 결과는 기존 조사 재료 ID·저장된 후보 StableId, 규격 조건, HS/KAMIS 후보와 관측 참조, 원 단위·시각·원본 hash·검토 상태를 담는다. 가격 수치·원문 JSON·실제 업체 관계를 복사하는 조회는 아니다.
- `Linked`는 후보 기록이 DB에 있다는 뜻이다. 분류 승인·실제 원산지·글로벌 상품 ID 또는 매장 레시피가 확인됐다는 뜻이 아니다. 자료원 코드들은 후보로 유지하며 서로 다른 체계의 코드를 합치지 않는다.
- `recipeResearch`는 기존 조사 묶음의 revision/hash만 연결한다. 실제 메뉴–레시피 N:M 관계는 별도 SQLite에 유지되며 이번 결과의 재료별 연결은 `IngredientToMenuRecipeEdgesNotResolved`다. 이름 유사성으로 연결을 만들지 않는다.
- 일부 후보/관측 누락은 대상 재료와 결손 이유를 반환한다. 중복·판본/hash 불일치·관계 모순은 오류로 거절한다. 원본 조회 실패를 빈 데이터로 성공 처리하지 않는다.
- 결과는 Git 제외 `artifacts/local/public-data/menu-ingredient-foundation/<SHA256>.json`에 저장한다. 같은 결과는 기존 파일과 일치 검증만 하며 덮어쓰지 않는다. 입력이 바뀌면 다른 파일로 보존한다.
- `apply` 명령은 없다. 신규 DB/테이블/원장/외부 API 요청·스케줄러·HTTP 서비스·UI·Unity 연결은 포함하지 않는다.

실행 사본은 `artifacts/local/public-data/menu-ingredient-prices/20260924-r1`에 있고 Git 제외 대상이다. 새 날짜/코드/지역은 이 동결된 r1에 덮어쓰지 않고 별도 조사 판본으로 만든다. 수입·수출 USD/kg은 통계 단위가액이지 국내 판매가가 아니며, KAMIS 축산물 02 요청 응답도 도매가격으로 해석하지 않는다.
