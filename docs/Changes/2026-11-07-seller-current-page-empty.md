# 판매 주문 목록의 현재 페이지가 비었을 때 첫 페이지 조회

판매 주문 후보는 남아 있지만 현재 요청한 페이지의 항목만 0건이면, 빈 표와 `3/1페이지`가 표시되고 첫 페이지로 돌아갈 행동을 찾기 어려웠다. 기존 목록의 성공 응답 안에서 이 상태를 구분하고 **첫 페이지 조회**를 연결했다. 현재 입력한 검색 조건을 적용하는 새 조회이며, 자동으로 페이지를 이동하지 않는다.

| 기준 | 대상 | 변경 범위 |
| --- | --- | --- |
| `dev/mirror-integration` · 기준 HEAD `e67cdbf0bbb086dc9ed547bdcd640074c9a18c86` · 로컬 미커밋 | 판매 주문 원장 공용 목록의 현재 페이지 빈 상태 | 기존 읽기 권한·조회·복귀 경로와 정상 목록 구성을 유지 |

## 발생 조건과 바뀐 행동

화면의 3페이지(API `Page=2`)를 읽었을 때 `TotalCount=4`, `Items=[]`인 성공 응답으로 재현했다. 전체 후보가 없거나 조회에 실패한 상태와 다르다. [기존 공용 목록](../../Ssalddel.Ui.Common/Areas/App/Components/Sales/ShipperSalesOrderWorkspace.razor)의 정상 초기화 분기 안에 다음 안내만 추가했다.

- **이 페이지에 표시할 출고 후보가 없습니다.**
- 현재 입력한 검색 조건으로 첫 페이지를 다시 조회해 주세요.
- **첫 페이지 조회**: 기존 `SearchAsync`로 첫 페이지(API `Page=0`)를 읽는다. 기존 접근·조회 중 차단 조건도 적용한다.

검색어 입력 자체로는 주문 조회를 보내지 않는다. 사용자가 **첫 페이지 조회**를 누르면 그때의 검색어·국내외·출고 상태로 새 조회를 시작하고 입력을 유지한다. 이는 마지막으로 적용한 조건을 복원하는 동작이 아니다. 적용 조건과 편집 중 초안의 분리 과제 S2, 상세 복귀의 기존 의미는 이번에 변경하지 않았다.

전체 0건·검색 결과 0건·로그인·로딩·오류와 실패한 페이지의 재시도는 기존 분기를 유지한다. 정상 목록의 표·요약·페이지 구성, 서버 총건수와 요청 페이지도 바꾸지 않는다. 주문 수집·상품 발행·피킹·포장·운송 인계나 업무 저장을 실행하지 않는다.

## 같은 주문과 실제 웹 화면 확인

2026-11-07 설치된 Chrome에서 현재 통합 WebApp의 실제 WASM·라우터·입력·버튼과 격리 가상 로그인·읽기 응답을 연결했다. 서로 다른 상태와 긴 이름을 가진 주문 4건(42·41·40·39)을 먼저 읽었다. 주문42의 목록 수량 12개와 상세 두 라인 6개씩, 목록 복귀 후 같은 4건을 대조했다.

빈 페이지에서 검색어 `아몬드`를 입력해도 요청이 늘지 않고, **첫 페이지 조회**에 초점을 두고 Enter를 누르면 해당 입력으로 첫 페이지를 읽는다. 복구된 주문41의 목록·상세 한 라인·수량 8개와 **주문 목록으로** 복귀 주소의 검색어를 확인했다. 전체 0건·검색 결과 0건·조건 해제·503 오류·실패 페이지 재시도도 각각 대조했다.

[실제 브라우저 검사 31개](../../artifacts/local/seller-current-page-empty-20261107/browser-after-final/2026-11-07T11-18-17-724Z-seller-current-page-after-acceptance.json)가 모두 통과했다. 320px·390px·1280px에서 정상 목록과 새 빈 상태의 문서 가로 넘침이 없고 버튼을 볼 수 있음을 확인했다. 390px에서 새 버튼은 높이 48px이며 실제 키보드 초점을 받는다. 예상 밖 화면 오류와 업무 쓰기 시도는 0개다. 의도해 반환한 503은 정상 빈 결과와 구분한다.

[390px 전후 비교](../../artifacts/local/seller-current-page-empty-20261107/comparison/comparison.html)는 같은 가상 데이터·주소·폭의 정상 목록과 현재 페이지 빈 상태 두 쌍을 나란히 연결한다. [비교 검증](../../artifacts/local/seller-current-page-empty-20261107/comparison/verification.json)에서 원본 4장·메타데이터·상대 링크를 확인했다. 정상 목록의 수정 전후 PNG는 SHA-256까지 동일하며, 이전 화면과 원본은 이 비교 기록에 보존한다. [비교 페이지 표시 검증](../../artifacts/local/seller-current-page-empty-20261107/comparison/render-verification.json)에서도 실제 Chrome의 이미지 4장 로드·넓은 폭 두 열·좁은 폭 세로·원본 링크를 확인했다. 이 HTML 표시 검사와 원본 제품 캡처의 실제 Chrome 업무 조작은 구분한다.

## 코드 검증과 보존

[실제 공용 컴포넌트 회귀](../../Ssalddel.Tests/Ui/Common/SellerCurrentPageEmptyRecoveryTests.cs)에서 수정 전 두 사례가 안내와 실제 버튼 부재로 실패했다. 수정 후 신규 14개를 포함한 Fast **87개 통과·실패 0개**, 소비 앱 빌드와 최종 Task **17,429개 통과·실패 0개·환경 의존 17개 생략**을 확인했다. Fast 빌드는 경고 248개·오류 0개, 별도 소비 빌드는 경고·오류 0개, Task 빌드는 경고 138개·오류 0개다. [코드 검증 영수증](../../artifacts/local/seller-current-page-empty-20261107/code-validation.json)에 수정 전 실패와 최종 TRX·로그·소스 해시를 구분한다.

제품 변경은 Razor 한 파일이며 시험은 새 회귀 한 파일이다. [보존 대조](../../artifacts/local/seller-current-page-empty-20261107/preservation.json)에서 두 추가 블록만 되돌리면 기존 페이지 전체와 같고, 기존 표·페이지 구성·코드 비하인드·목록 ViewModel·이전 복구 시험이 보존됨을 확인했다. [현재 소스와 실행 자산 결속](../../artifacts/local/seller-current-page-empty-20261107/runtime-binding-after.json)은 Razor의 Common PDB 체크섬, Common DLL/PDB의 통합 웹 소비 사본, 생성 WASM와 manifest 해시를 대조한다. 이 결속을 실제 브라우저나 휴대폰 조작 검증의 대체 근거로 사용하지 않는다.

## 실행 경계와 남은 확인

웹 확인은 격리 가상 인증·주문으로 수행했으며 운영 인증·서버/DB·실제 주문 처리는 검증하지 않았다. 기존 route·API·권한·주문 상태와 다른 작업의 변경을 보존했고 커밋·푸시·배포는 하지 않았다.

등록 단일 USB와 공유 OS 잠금 아래, 기존 별도 검토용 통합 앱을 같은 서명의 **148 / 0.1.148**로 데이터 보존 업데이트했다. [실제 기기 12개 확인](../../artifacts/local/seller-current-page-empty-20261107/native-after/acceptance.json)에서 기존 가상 로그인 화면 → 전체 4건·현재 페이지 0건 → 터치로 `아몬드` 입력(주문 조회 없음) → **첫 페이지 조회** 터치 → 주문41·수량 8개 → 같은 상세의 라인411·SKU → **주문 목록으로** 터치와 검색 조건 복귀를 확인했다. 실제 휴대폰은 CSS 384px·DPR 2.8125이며 가로 넘침이 없었다. [주문 상세](../../artifacts/local/seller-current-page-empty-20261107/native-after/order41-detail-384.png)와 [복귀한 목록](../../artifacts/local/seller-current-page-empty-20261107/native-after/returned-order41-384.png)은 실제 ADB 픽셀에서 시스템 표시줄을 제외한 앱 영역이다. 데스크톱 폭을 휴대폰으로 가장하지 않았고 물리 한국어 조합 입력을 검증한 것으로 쓰지 않는다.

검토 APK의 [현재 DLL·PDB·CSS·격리 endpoint 결속](../../artifacts/local/seller-current-page-empty-20261107/native-embedded-proof.json)과 [설치·복원 기록](../../artifacts/local/seller-current-page-empty-20261107/native-session.json)을 별도로 남겼다. 원 통합 앱230의 설치 APK 해시와 배달 수집 checkpoint가 동일하며, 원래 전면 앱을 복원하고 소유 포트 연결·공유 잠금·가상 서버를 반환했다. SellerApp 네이티브는 미설치다. 휴대폰의 동일 조건 수정 전 캡처는 없어 네이티브 전후 비교 완료로 처리하지 않는다. 웹의 503·전체 0건·키보드 Enter 검사도 휴대폰에서 반복한 것으로 표현하지 않는다.

초기 검토 서버가 네이티브의 chunked 가상 로그인 요청을 읽지 못한 400은 입력 크기를 제한한 검토 도구의 전송 처리로 보완했다. 실제 계정·제품 인증 코드는 바꾸지 않았다. CDP 캡처 시간 초과와 너무 엄격한 위치·선택자 조건도 도구 문제로 구분하며 실제 화면을 다시 읽은 뒤 진행했고 반복 탭으로 우회하지 않았다. 승인 거절된 선행 W1 host는 그대로 보존했다. 최종 통합 근거는 [validation.json](../../artifacts/local/seller-current-page-empty-20261107/validation.json)이며, 다음 후보 한 가지는 **W2 창고의 현재 페이지만 0건인 경우 복구**다.
