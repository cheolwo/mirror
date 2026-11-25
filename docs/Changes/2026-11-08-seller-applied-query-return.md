# 판매 주문 상세에서 마지막 성공 조회 목록으로 복귀

A 조건으로 읽은 주문42를 보며 B 조건만 입력하면, 조회를 누르지 않았는데 상세의 복귀 주소가 B로 바뀌었다. **주문 목록으로**가 미조회 B를 적용해 원래 목록을 잃는 문제를 마지막 성공 조회의 문맥과 현재 입력을 분리해 고쳤다.

| 기준 | 변경 축 | 실제 화면과 시각 근거 |
| --- | --- | --- |
| `dev/mirror-integration` · HEAD `e67cdbf0bbb086dc9ed547bdcd640074c9a18c86` · 로컬 미커밋 | 공용 판매 목록 VM·상세 링크 연결부·S2 회귀 세 경로 | 통합 웹 Chrome390/1280px 전후6쌍, 등록 별도 통합 검토앱149의 수정 후 화면 |

## 발생 조건과 작은 수정

시작 근거인 [원래 S2 인계](../AI/Handoffs/2026-11-07-S2-seller-return.md), 수정 전 예상 실패2개와 실제 Chrome 원본6장을 보존한다. [목록 VM](../../Ssalddel.Ui.Common/Areas/App/ViewModels/판매채널주문페이지ViewModels.cs)은 실행 delegate 안에서 실제 전송 요청을 만들고, 성공 응답 뒤 첫 행 변경 알림 전에 검색·국내외·상태와 **응답 Page+1**을 기존 불변 문맥으로 보관한다. [상세 링크](../../Ssalddel.Ui.Common/Areas/App/Components/Sales/ShipperSalesOrderWorkspace.razor.cs)는 현재 편집값 대신 이 문맥으로 from을 만든다.

A/page2 성공 뒤 B만 입력하면 GET 증가 없이 같은 주문42의 복귀는 A/page2다. 지연 A 조회 중 B를 입력해도 A 응답의 행과 A 복귀 문맥을 함께 유지한다. 다음 명시 B 성공 조회에서만 B로 바뀐다. 실패·협조 취소·차단된 중복 호출은 마지막 성공 문맥을 보존한다. 취소를 무시하는 서비스의 기존 행 반영 경계와 S1 상세 경합·권한·원장·상태 정책은 변경하지 않았다.

**조회**, **새로고침**, **첫 페이지 조회**와 판매자30초 재조회는 계속 현재 입력과 기존 재시도 페이지 규칙을 사용한다. [S3 현재 페이지 빈 상태 복구](2026-11-07-seller-current-page-empty.md)는 당시 범위 기록을 보존하며, 이번 후속 S2가 상세 복귀의 적용 조건을 고쳤다. 미조회 초안의 별도 영속 보존 기능을 추가하지 않았다.

## 실제 웹·등록 검토 앱과 전후 비교

통합 웹의 실제 Chrome/WASM·라우터·입력·버튼에 격리 가상 인증/읽기를 연결했다. 주문42 총12와 라인421/422의6+6, 같은 주문 상세에서 원래 A의42·41·40·39 네 건 복귀를 확인했다. B=아몬드를 입력만 하면 요청이 늘지 않고, 명시 조회가 성공한 뒤에는 주문41 한 건·수량8과 q=아몬드 복귀로 바뀐다. S2 확인27개와 S3 보존31개를 [실행 원본](../../artifacts/local/seller-applied-query-return-20261107/browser-after-acceptance-20261108.json)·raw 요청/DOM과 독립 대조했다. 실제 웹은 A/page1이며 page2는 별도 C# 회귀 범위다.

[같은 조작의 전후6쌍·원본12장](../../artifacts/local/seller-applied-query-return-20261107/comparison-20261108/comparison.html)은 정상·B초안·상세 후 복귀를390/1280px에서 비교한다. 정상2쌍은 PNG 바이트까지 동일하다. 초안2쌍은 본문/행이 같지만 PNG는 다르다. 복귀2쌍은 수정 전B의41 한 건에서 수정 후A 네 건으로 URL과 조회 결과가 달라지는 것이 관찰 결과이며 동일 응답 비교로 꾸미지 않았다. [최종 HTML 실제 표시](../../artifacts/local/seller-applied-query-return-20261107/comparison-20261108/browser-display-final.json)는 이미지15장·링크38개를 확인했다. 메타데이터의 이전 pending 문구는 이 후속 표시 영수증보다 앞선 시점이다.

[정상 통합 웹1280px 원본](../assets/changes/2026-11-08-seller-applied-query-return/orders-1280-full.png) · [390px 원본](../assets/changes/2026-11-08-seller-applied-query-return/orders-390-full.png) · [원본 출처와 SHA](../assets/changes/2026-11-08-seller-applied-query-return/source.json). 두 대표 이미지는 픽셀 편집 없이 복사했고 기존 판매자 전용 대표 화면과 S3 복구 화면을 보존했다.

등록 USB·공유 OS 잠금 아래 기존 별도 통합 검토앱을 같은 서명의 **149/0.1.149**로 데이터 보존 업데이트했다. [현재 기기 세션](../../artifacts/local/seller-applied-query-return-20261107/native-session-20261108.json)은19확인/0실패와 원본10장을 남겼다. 실제 ADB 터치·WebView384×757, 주문42의 두 라인6+6·A 네 건 복귀, 현재 B로 **첫 페이지 조회** 후 주문41 수량8·같은 조건 복귀를 대조했다. 확정 문자열 입력은 CDP이며 물리 한글 IME 조합 검증이 아니다. 같은 조건의 휴대폰 수정 전 이미지는 없어 **수정 후149 검증만** 인정한다.

초기 홈의 fixture 미모델링 GET74 거절과 console404 한 건은 보존하고, 선택 판매 화면 진입 뒤 unexpected GET0과 구분한다. 대체된 도구 판정2개·도구 이슈7개는 현재19확인에 합쳐 지우지 않았다. 휴대폰 원본 요청 로그는 query를 저장하지 않으므로 B 검색어는 DOM/from·필터 응답과 현재 Client를 결합한 확인이며 raw wire query 확인으로 확대하지 않는다.

## 시험·판본과 독립 수용

[Fast 원본](../../artifacts/local/validation/20261108-000710/Ssalddel.Tests.trx)은9통과/0실패다. [Task 원본](../../artifacts/local/validation/20261108-000850/Ssalddel.Tests.trx)은17,463통과/0실패/환경 의존17생략(총17,480)이다. S2 여섯 사례·S3 열네 사례·복구/재조회 스물네 사례를 합친 관련44개는 모두 통과다. 환경 Mongo/MySQL 연결 미지정17건을 성공이나 실제 DB 검증으로 세지 않는다. 소비 빌드는 오류0이며 [UI 동결 결과](../../artifacts/local/seller-applied-query-return-20261107/ui-worker-result.json)에 canonical 계획·로그를 연결한다.

[최종 소스/PDB/소비DLL/WASM 결속](../../artifacts/local/seller-applied-query-return-20261107/runtime-binding-final-20261108.json)과 [APK149 내 PE/CSS·서명/격리 endpoint 근거](../../artifacts/local/seller-applied-query-return-20261107/native-embedded-proof.json)를 독립적으로 파일·바이트까지 대조했다. 제안LF와 실제CRLF는 원본 SHA가 다르지만 정규화한 세 파일 내용은 같다. 실제 동결 소스·PDB checksum을 기준으로 검증했고, 진행 중 UI receipt의 이전 hash와 immutable packaging snapshot은 해당 시점 이력으로 보존했다.

[독립 검증 결과](../../artifacts/local/seller-applied-query-return-20261107/independent-validation-20261108.json)는 이 동결S2와 지정 통합 웹·등록 검토앱149 범위에서 **verified**다. 새 전체시험·APK빌드·기기 순회를 반복하지 않고 같은 판본의 실행 근거를 재사용했다. 실패/협조 취소/중복 보존·요청/응답 페이지 차이·명시 성공 전환, 실제 부모/Client/navigation과 원래 재현을 독립 대조했다. 이전 패치 정적 검토만을 제품 검증 완료로 승격하지 않았다.

## 보존·한계와 다음 한 가지

원 통합앱230 APK·배달 checkpoint·원래 전면/설정과 다른 dirty 변경을 보존했다. 임시5379 host·6789 forward·reverse와 공유 OS 잠금 반환 근거를 대조했으며, 기존 W1 5764/PID57832는 유지된다. 검증 담당은 제품 코드를 수정하지 않았다. 역할03 전용 웹·SellerApp 네이티브·물리 한글 조합·새 휴대폰 로그인과 운영 인증/서버/DB·주문 처리·배차/정산·공개 준비는 미검증이다. 커밋·푸시·배포는 없다.

검증·인계가 결과를 수용해 이 기록과 README의 **통합 웹 후속 화면**을 마감한다. 다음 한 가지는 기존 순환의 W3 **표현 불가 작업 시각**을 현재 코드/읽기 계약/시험에서 대조한 최소 배정이며, 실제 담당·허용경로·실행범위는 [현재 실행 상태](../../artifacts/local/thread-workflow-20261107/active-work.json)를 따른다. 새 정책 결정을 요구하는 O3 자료 저장은 별도로 보존한다.

## 2026-11-09 화주 전용 웹 후속 마감

화주 전용 웹에서 판매 주문 목록의 **상세**를 누르면 역할 앱 밖의 주소로 이동했다. 실제 Chrome의 가상 주문 41번에서 이를 재현했다. 공용 목록 상세 링크, 웹의 목록 복귀 링크, 기존 orderId 주소 전환만 앱의 기준 경로를 따르게 했다. 서버 조회 계약과 적용 검색 조건은 그대로 사용한다.

390·1280px에서 ‘아몬드’를 조회한 뒤 ‘커피’를 입력만 하고 같은 주문 41번 상세를 열었다. 수량 8개를 확인하고 목록으로 키보드 복귀하면 ‘아몬드’가 다시 적용됐다. 기존 orderId 주소와 통합 웹 390px의 같은 경로도 실제 Chrome에서 확인했다. 격리 가상 인증 상태·읽기 자료를 사용했다. 관련 Fast 5개, 화주 전용·통합 소비 빌드와 Task 17,509개 통과·환경 17개 미실행이다. [같은 조건의 전후](../../artifacts/local/seller-role03-return-20261109/comparison.html) · [실행 근거](../../artifacts/local/seller-role03-return-20261109/completion.json).

## 2026-11-09 화주 전용 판매 목록 로그인 복귀

화주 전용 웹에서 판매 주문 목록을 열고 로그인하면 원래 조회로 돌아오지 못했다. 목록 안의 **로그인**은 반환 주소가 빠졌고, 상단의 로그인은 주소를 넘겨도 공통 로그인에서 거절했다. 목록 버튼은 현재 주소와 적용 조건을 전달하도록 연결하고, 공통 로그인은 동일 출처의 정확한 화주 전용 판매 목록만 반환한다. 외부 주소·유사 경로·다른 역할·개별 상세는 이 예외에 포함하지 않는다.

390px 목록 버튼과 1280px 상단 버튼으로 실제 역할01 로그인에 들어가 가상 인증에 성공한 뒤, 역할03의 ‘아몬드’ 검색 목록으로 복귀했다. 같은 주문 41번 한 건이 조회됐으며 익명 상태의 주문 요청은 없었다. 관련 Fast 96건, 역할01·03 소비 빌드와 전체 Task 17,517건 통과·환경 17건 미실행을 확인했다. 첫 전체 시험은 실행기 내부 오류로 중단되어 같은 소스와 시험 범위를 새 프로세스에서 다시 실행했다. [수정 전후 3쌍](../../artifacts/local/seller-role03-login-return-20261109/comparison.html) · [검증 기록](../../artifacts/local/seller-role03-login-return-20261109/completion.json).
