# 주문 원장 연결·역할 인계 보완과 총괄 관리자 명세 r7

사용자가 승인한 주문 중심 구조 보완을 구현했다. 음식 배달과 물품·창고 배송을 같은 깊이로 대조하고 기존 전문 원장의 상태 권위를 유지하면서 조회·연결·실패 복구를 맞췄다. 총괄 관리자 네 업무의 기획 명세를 완성했으며 관리자 화면 구현은 포함하지 않는다.

## 변경과 효과

- 음식 동기화는 SQL 큐와 기존 Mongo 배송 원장의 출처·템플릿·공동체·개인정보 파기 여부를 검증한 뒤 배송 참조를 연결한다. 새 운송·예약을 만들지 않고 기존 관계 메타데이터를 보존한다. 루트 CAS 경합은 현재 관계를 다시 읽어 재시도하며 운송 동기화 미확인은 Outbox 재시도로 남긴다.
- 이전 음식 주문은 ID를 추측하지 않고 동일 출처의 배송을 읽기 전용으로 찾아 기존 역할별 공개 보호를 적용한다. 파기된 루트는 후보 탐색을 생략하고 파기된 배송은 신규 후보에서 제외하면서 명시적 관계 이력을 보존한다. 조회는 원장·revision을 쓰지 않는다. 분산 물품의 선택적 원주문 미연결과 `Connected/Pending/Blocked`는 기존 의미를 유지한다.
- 음식 목록·검색·건수·통합 조회에 같은 revision의 음식 블록 판독을 적용했다. 준비 완료 요약을 전달/수령 완료로 표시하지 않는다. 저장소 200건 페이지를 순회해 후처리 전에 일치 주문이 잘리지 않도록 한다. 조회 조건 `Skip`은 기본 0의 추가 필드이며 기존 호출은 호환된다. 여러 페이지의 전역 원자 snapshot은 보장하지 않는다.
- 연결·분리 성공 뒤 현재 주문자 권한으로 재조회한다. 기존 응답 형식을 유지하면서 미승인 상세·서명·완료 집계를 숨긴다. 기존 판정 필드의 `공개요청필요`는 확인 불가를 성공·원장 누락과 구별한다.
- 음식 기사 앱은 최신 조회 실패 뒤 업무·입력을 유지하고 상태 전이를 차단한다. 정본 조회와 보류 요청 조정이 성공하면 행동을 복구한다. 수락·거절·묶음·픽업·전달 응답 유실은 자동 재POST 없이 정본을 조회하며 기존 durable 요청 ID·revision·입력을 보존한다.

## 두 흐름과 관리자 요구사항

[r7 기획·흐름 대조·책임 카드](../AI/Planning/공통/PLAN-OPERATIONS-ORDER-CENTERED-WORK-NETWORK/order-ledger-admin-refactor.implementation.r7.md)에 음식·물품의 접수, 준비, 배차, 연결, 수행, 수령, 정산, 지급을 API·원장·역할의 입력과 다음 행동으로 대조했다. 관리자 요구사항은 주문별 진행, 기사별 업무, 문제 처리, 정산·지급 확인 네 가지다. 총괄 `서버관리자전용`을 기준으로 하고 지사 권한이나 관리자 완료 명령을 만들지 않는다.

진행 중인 r45 운영 알림·r46 회원 수령 보완을 보존했다. 직접 인계의 배송 원장 0건도 정상이며 알림 확인·사건 종결·전달·수령·정산·실지급을 합치지 않는다. [주문 원장 기준](../Architecture/OrderRootLedger.md)의 r35/r41 관계 동시 보호 설명도 현재 구현에 맞췄다.

## 이번 실행 근거

기준은 `dev/mirror-integration`, HEAD `d1bab3eec08dc0a49c9a87f882590d394205c9a4`에 이번 미커밋 변경을 더한 작업 트리다. 검증 범위 28개 경로는 `artifacts/local/order-ledger-admin-r7/paths.json`, 수정·신규 C# 21개의 SHA-256 전후 비교는 `source-before-validation.json`·`source-after-validation.json`에 보관했다. 21개 모두 동일했다. 공유 문서의 기존 r45/r46 변경은 이번 소유 코드와 구별한다.

| 실행 | 실제 결과·근거 |
| --- | --- |
| `eng/validate-changes.ps1 -Level Fast -Paths <이번 28개 경로> -TestFilter <집중 11그룹>` | solution build 성공, 886개 통과·실패 0·Mongo 환경 의존 12개 건너뜀. `artifacts/local/validation/20261009-131514/`의 build·tests 로그와 TRX |
| `eng/validate-changes.ps1 -Level Task -Paths <이번 28개 경로>` | solution build 성공, 전체 9,462개 통과·실패 0·Mongo 환경 의존 15개 건너뜀. `artifacts/local/validation/20261009-131624/`의 build·tests 로그와 TRX |
| `dotnet build FDriverApp/FDriverApp.csproj -f net10.0-android -v:minimal` | Debug build 성공, 오류 0·경고 37. `artifacts/local/order-ledger-admin-r7/android-build-final.log`. `FDriverApp/bin/Debug/net10.0-android/kr.ssalddel.fdriver-Signed.apk` 생성 확인. 설치·실행 검증은 아님 |

Fast의 집중 필터는 `OrderLedger`, `FoodOrderChildConnection`, `FoodMartLedger`, `음식점수락배차Outbox`, `음식배달정상수직생명주기`, `FDriver`, `Distributed`, `CommunityLedgerRelation`, `RoleWorkspaceReliability`, `NeighborhoodMemberReceiptCompletion`, `OperationsAlert`다. 전체 시험과 중복을 합산하지 않는다. 새 보호 시험의 읽기 전용 검토, 7개 문서의 상대 파일 링크 870개(누락 0), 범위별 `git diff --check`를 통과했다. 요약 근거는 `artifacts/local/order-ledger-admin-r7/validation-evidence.json`, 링크 점검은 `document-link-check.json`에 보관했다.

음식 정상 생명주기와 물품 분산 이행의 두 거점·두 판매자·두 고객 시험은 각각 현재 UseCase의 접수/예약, 수락, 수행, 전달, 별도 수령과 결과 재조회를 검증했다. SQL 업무는 SQLite 격리 저장, Mongo 원장 경계는 대체 저장소이므로 실제 SQL/Mongo 종단·역할 앱 완주 증거로 확대하지 않는다. 실제 Mongo 관계 시험 12개 및 전체 시험의 별도 Mongo 3개는 시험 연결 환경이 없어 미실행이다.

추가 파기 보호 시험 작성 중 읽기 전용 DTO 사전의 직접 변경을 바로잡았고, 최초 Android build의 전역 `TargetFrameworks` 지정이 의존성 복원 대상을 바꾼 실패는 정상 build 명령으로 해결했다. 실패 로그는 `artifacts/local/validation/20261009-131342/`와 `artifacts/local/order-ledger-admin-r7/android-build.log`에 보존한다. 최종 통과 후 제품 소스 변경은 없다.

## 실행 범위

자동 시험·solution build·음식 기사 Android Debug build와 APK 생성까지 확인했다. 지도 SDK ID 미설정 및 기존 알림 API 수준·구식 API 등을 포함한 Android 경고 37개가 남는다. 실제 지도/알림 동작을 이 build 결과로 확정하지 않는다.

실제 Mongo 시험·SQL/Mongo 인증 HTTP·제품 화면·APK 설치/휴대폰·Azure·실거래·실지급·운영 활성화·배포·commit/push는 수행하지 않았다. 관리자 모바일 화면·실제 이체 연결은 후속 개발이다. 여러 페이지의 목록은 전역 원자 snapshot을 보장하지 않으며 전문 상태 변경은 현재 원장의 권한·판본을 다시 검사한다.
