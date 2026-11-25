# S2 판매 목록의 적용 조건과 상세 복귀 인계

2026-11-07 · 같은 PC/checkout의 전문 UI 채팅 인계. 사용자가 절차를 채팅별로 나누는 구성을 먼저 요청해 조사·수정 전 재현까지만 진행하고 제품 편집을 시작하지 않았다. 실제 담당·실행 수용은 [전문 채팅 실행 상태](../../../artifacts/local/thread-workflow-20261107/active-work.json)를 따른다. `executionGranted=true`와 자기 ownerThreadId가 일치한 뒤에만 구현을 재개한다.

## 현재 판본과 소유

- 저장소 `C:/Users/user/source/repos/Hongdal`, 브랜치 `dev/mirror-integration`, HEAD `e67cdbf0bbb086dc9ed547bdcd640074c9a18c86`.
- 기존 dirty 변경을 포함하는 기준이다. HEAD만으로 같은 소스라고 판단하지 않으며 [기준 파일 사본·SHA](../../../artifacts/local/seller-applied-query-return-20261107/baseline.json)를 대조한다.
- 제품 소유는 목록 VM `Ssalddel.Ui.Common/Areas/App/ViewModels/판매채널주문페이지ViewModels.cs`와 연결부 `Ssalddel.Ui.Common/Areas/App/Components/Sales/ShipperSalesOrderWorkspace.razor.cs`다. 기준 SHA는 각각 `7d242381085165b88ec71a789a052fe05e3b6e81cdad96eee68baac99879bc8a`, `bda53e045fa2f2bb1f0a746de76305ce30191b380686d8c4b39fdb1169c82bf7`이다.
- 신규 `Ssalddel.Tests/Ui/Common/SellerAppliedQueryReturnTests.cs`는 핵심 실패 회귀2개만 준비했다. SHA `8744ec17a3622a15542795df3257864b9c53f6fe379501af73f35d64e83ac51f`. 다른 시험·Razor·navigation 계약·API·권한·상태 전이는 수정하지 않는다.
- 작업별 산출물은 `artifacts/local/seller-applied-query-return-20261107/`에 이어 쓴다. 이전 실패·원본·기존 변경을 삭제하거나 덮어쓰지 않는다. CURRENT_WORK와 총괄 실행 상태는 총괄이 갱신하며 UI 담당은 `ui-worker-result.json`으로 수용/결과를 남긴다.

## 문제와 완료 조건

판매자가 A조건으로 조회한 주문42의 행을 보고 B조건만 입력하면, 새 조회 없이 상세 링크의 from이 B로 바뀐다. 같은42 상세의 **주문 목록으로**가 미조회 B를 새로 적용해 원래 목록을 잃는다. 조회 중 B를 입력해도 A응답의 행에 B복귀 문맥이 섞인다.

질문은 “내가 선택한 주문을 보던 목록으로 돌아가는가?”다. 우선 정보는 채널·주문 식별, 상품/라인 수량과 출고 연결, 마지막 성공 조회의 조건/페이지다. 현재 입력 자체는 조회하지 않고 명시 조회·새로고침·첫 페이지 조회는 현재 입력을 사용한다. 이 기존 S3·재시도·판매자30초 재조회 계약은 보존한다. 상세를 열 때 미조회 초안의 별도 영속 보존 기능을 새로 추가하지 않는다.

제안한 최소 구현은 기존 불변 `SalesOrderNavigationContext`를 목록 VM 내부의 마지막 성공 문맥으로 보존하는 것이다. 작업 delegate 안에서 실제 전송 요청을 로컬로 만들고, 그 응답을 반영하는 시점에 요청 Search/SyncScope/Status와 응답 Page+1로 문맥을 함께 저장한다. DetailPath는 이 문맥을 사용한다. await 뒤 현재 편집 값을 다시 읽거나 요청 시작만으로 성공 문맥을 바꾸지 않는다. 실패·협조 취소·차단된 중복 호출은 마지막 성공 문맥을 보존한다. 취소를 무시한 서비스의 기존 행 반영 경계나 공통 작업 base까지 이 항목에서 바꾸지 않는다.

완료 조건은 A/page2 성공 → B검색/범위/상태만 입력(GET 증가0) → 현재42의 실제 상세 href/from은 A/page2이고, 지연 A재조회 중 B입력 후 A응답에도 같은 조건을 유지하는 것이다. 다음 명시 B성공 조회에서만 B문맥으로 바뀌어야 한다. 현재 입력으로 명시 첫 페이지/재조회, 정상0·현재 페이지만0·오류·로그인·입력/초점·같은 주문 복귀도 보존한다.

## 이미 실행한 근거와 미실행

수정 전 Fast의 컴파일은 통과했고 [핵심 회귀 실행](../../../artifacts/local/validation/20261107-225407/tests-01.log)의 두 assertion은 Expected=A 상품/Actual=B 미조회 초안으로 실패했다. 이는 예상한 제품 재현이다. 수정 후 시험·소비 빌드·Task는 아직 없다.

실제 Chrome/current WASM의 격리 가상 인증·읽기로 네 정상 주문42/41/40/39를 먼저 확인했다. A는 전체목록/UI page1이다. B=아몬드만 입력 → 기존42 상세 총12/라인6+6 → 실제 목록 복귀가 B의41 한 건으로 바뀌는 경로를390/1280px에서 재현했다. [수정 전 관측](../../../artifacts/local/seller-applied-query-return-20261107/before-observations.json)과 `browser-before-final/`의 normal/draft/return 원본6장을 보존한다. page2는 가상4건으로 실제 선택할 수 없어 핵심 C# 회귀에서 별도 확인한다. 관측 파일에는 href 관측2개가 저장돼 있으며 그 숫자를 전체 브라우저 assertion 수로 확대하지 않는다.

초기 관측 도구가 canonical q 대신 search 별칭을 기다린 실패와 RED 빌드 중 자산 fingerprint가 바뀐 도구 실패는 별도 원본에 남았다. final 수정 전 브라우저는 RED 완전 종료 뒤 새 manifest로 다시 실행했고 page error0이며 모두 종료했다. 실제 브라우저를 사용하는 동안 빌드로 소비 자산이 바뀌지 않게 한다. 가상 응답/현재 자산 매핑은 새 artifact의 S3 재사용 harness/fixture다. 기존 W1 host PID57832/5764는 보존하며 재시작·종료하지 않는다.

이 공용 링크는 통합 웹·역할03 웹·SsalddelApp·SellerApp이 소비한다. 새 소스의 실제 웹과 영향을 받는 등록 USB 검토 앱을 구분해 확인해야 한다. 이전 S3의 별도 검토앱148 완료 근거가 있지만 현재 설치본·기기/잠금은 아직 재확인하지 않았다. 149와 독립 endpoint5379는 정적 준비 제안일 뿐 빌드·설치·포트·장치 조작을 실행하지 않았다. 현재 Android 판본과 서명/연결/OS잠금·가상 endpoint를 확인하고 기존 승인된 데이터 보존 업데이트 절차만 재사용한다. 기기 화면이 남으면 `screen_pending`으로 유지한다.

## UI 담당의 종료와 다음 담당

현재 source/test baseline과 실행 소유를 확인한 수용을 먼저 기록한다. 작은 구현·필요한 실패/성공 회귀·정확3경로 Fast·소비 빌드와 실제 웹/USB·전후 비교를 진행한다. UI 수정과 실제 화면은 같은 판본으로 반복 확인하고 안정된 결과/소스 지문을 동결한다. 남은 플랫폼·차단·자기 소유 프로세스/포트 정리를 사실대로 적는다.

구현 결과는 `ready_for_validation` 또는 `screen_pending`으로 UI 결과 파일에 남긴다. 최종 Task를 포함해 어떤 검증을 실행했는지 실제 canonical log/수치와 함께 연결하며 미실행을 채우지 않는다. 검증 채팅은 동결 판본·현재 계약·핵심 반례/실제 소비 경로와 필요한 Task를 독립적으로 확인한다. 제품 수정이 필요하면 같은 S2 ID로 UI 담당에게 반환한다. 이 인계는 운영 업무·정산·배차·모델 학습·외부 쓰기·배포·커밋/푸시를 허용하지 않는다.
