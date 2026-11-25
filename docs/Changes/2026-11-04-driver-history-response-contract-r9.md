# 기사 배달내역의 누락 응답·null 항목 처리 r9

기사의 화물 배달내역 목록이 없거나 잘못된 응답을 받았을 때, 정상적인 기록 없음으로 표시하거나 null 항목을 화면 정렬에 넘기지 않도록 보완한다. 화면 배치·메뉴·서버 계약·운송 상태 변경은 그대로다. [내역 화면과 조회 책임](../ProjectOverview/page-docs/DriverApp/DriverApp-P05/README.md)을 따른다.

## 발견과 최소 수정

서버 목록은 `기사운송진행Controller → 운송목록조회Query`에서 본인 용달 운송을 조회해 배열로 반환한다. 빈 목록도 정상 배열이다. 공용 `DriverApiClient.GetAsync`는 개별 조회를 위해404/204를 null로 반환하며 JSON null도 같은 결과다. `DriverTransportApiService.목록조회Async`의 기존 `?? []`가 이를 성공한 빈 내역으로 바꿨다. `[null]` 배열도 그대로 통과해 페이지의 시각 정렬·표시에서 예외가 날 수 있었다.

실제 인증 세션·HTTP 클라이언트·목록 서비스·내역 ViewModel을 연결한 합성 HTTP 회귀15개 중, 수정 전8개가 실패하고7개가 통과했다. 목록 서비스에서 null 응답과 null 원소만 고정 안내 예외로 거절하는 검사를 추가했다. 공용 HTTP·인증·서버는 바꾸지 않았으므로 현재 운송/개별 상세의404 없음,503 상태, 정상 빈 배열과 실제 운송 내용은 유지한다. 화면은 기존 고정 오류 안내와 명시적 `다시 시도`로 회복하며 원문을 표시하거나 자동 요청을 추가하지 않는다.

## 검증과 재개

- Fast: 관련61/61 통과·실패/생략0. 새15개와 기존 내역 수명·인증/작업공간 회귀를 실제 실행했다. 소비 기사 앱 빌드 오류0/기존 경고65, 시험 의존 빌드 오류0/기존 경고229다. `artifacts/local/validation/20261104-180815`의 로그와 TRX로 확인한다.
- Task: `Ssalddel.v3.5.slnx` 빌드 오류0/기존 경고60, 전체16,919통과·환경17생략·실패0이다. `artifacts/local/validation/20261104-181123`의 개별 TRX 결과와 로그를 확인했다.
- APK138을 별도 새 출력으로1회 포장했다. 선택 입력3,595개 전후 변경0, 같은 서명과 두 ABI의 실제 앱 DLL·5378 주소를 검증했다. 포장 오류0·경고행52이며 고유 경고 수와 구분한다. APK SHA는 `e3847fabf3c2a5a2fb7b256ab08b733f220b386daf570fbdec98bec8bfe8d117`, 앱 DLL SHA는 `db0e738cd92fc8bcddeb2dd06febee02804edd41d95c1ba58383f40002027ff5`다. 과거137의 검증을 새 판본에 승계하지 않았다.
- 실제 등록 USB에서137의 기존 SHA를 먼저 읽고138을 `install -r`1회 업데이트했다. 설치 바이트·최초 설치 시각을 대조했고 원래 기사 앱229와 화주 검토144의 메타데이터를 보존했다. 사용자 입력이 없는 기존 합성 작성 화면을 확인한 뒤 설치된 MAIN으로 기사 앱을 열었다. 삭제·초기화·force-stop·권한 자동 승인은 없다.
- 시작 화면은 `기사 로그인`과 `현재 사용자: 미로그인`을 표시했다. 예약 지침의 로그인 인증 대리 입력 금지에 따라 입력/로그인 제출 없이 여기서 기기 검증을 중단하고 사용자에게 기존 합성 기사 계정의 직접 로그인을 한 번 요청했다. 설치 전 인증 상태는 관찰하지 않았으므로 업데이트 때문에 로그아웃됐다고 단정하지 않는다. 첫 관찰 도구의 password 플래그 기반 loginRequired=false는 불완전했으며, 명시적인 두 화면 문구를 재확인한 후속 기록을 따른다. **배달내역 조회·메뉴 복귀·불량 응답의 실기기 재현·이 APK의 실제 서버 통신은 미검증**이다.
- 같은 소유 r6 호스트·실행 시작 시각·DLL·격리 DB를 읽기로 재확인했다. 시작과 마지막 live/ready·홈·업무 proof2개는 각각200이고 두 업무 proof SHA 불변·금융 행0이다. 이는 도구의 실제 서버 조회이며 기사 앱 로그인/통신 성공을 뜻하지 않는다. 과거 readiness journal21개는 보존했으며 새 실패 관측은 없다. 원격 운영이나 모든 과거503의 해결로 확대하지 않는다.

재개 근거는 `artifacts/local/app-stability-r9/ab99f6d36a4f4d05ab848d9f3c66f9d0/completion.json`이다. 같은 폴더의 `reproduction/inputs.json`·원래 서비스 사본·`reproduction.trx`·`result.json`, `validation.json`, `driver138/proof.json`·`endpoint.json`, `phone-driver-baseline.json`, `device-install138.json`, `driver138-launch.json`, `driver138-authentication-stop.json`, `runtime-before.json`/`runtime-after.json`/`runtime-final-identity.json`과 `readiness-journal-before.json`/`readiness-preservation.json`이 코드·포장·기기·서버를 구분한다. 원시 UI와 스크린샷은 메모리에서만 관찰했다.

다음 한 가지는 사용자가 합성 기사 로그인을 완료했을 때 기기·138 설치/endpoint·같은 소유 서버를 다시 대조하고 정상 메뉴의 `배달 내역 → 내역 새로고침 → 사용자 메뉴로`를 확인하는 것이다. 인증에 의존하는 기기 항목만 보류하며 독립적인 역할별 코드/서버 조사는 계속할 수 있다. 원래 소스·정상 출력·실패 기록을 보존했고 실제 배차·운행·결제·정산/지급·배포·commit/push는 수행하지 않았다.
