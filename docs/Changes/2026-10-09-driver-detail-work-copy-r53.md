# 기사 배달료 상세·현재 배달 안내와 USB 설치 r53

## 변경

[승인 기획](../AI/Planning/공통/PLAN-OPERATIONS-DRIVER-VERTICAL-REFACTOR/driver-detail-work-copy.r53.md)에 따라 r52 다음 우선순위 3·4를 적용한다. 완료 배달 상세는 배달료 합계·요금 구성·공제/수령액·정산/지급 상태 순서로 정리하고 항목명을 왼쪽, 값을 오른쪽에 둔다. 수락 당시 저장값을 사용하며 null·0·소수 금액·기본비 미분류·구성 누락/무효를 구별한다. 기존 설명 문자열·금액·거리와 개인정보 수명은 유지한다.

현재 배달은 공통 단계 제목과 `가게 도착 / 픽업 완료 / 전달 완료` 문구를 소비한다. 상호와 전체 주소를 따로 읽고 다음 행동 안내의 반복을 줄인다. 실제 버튼이 없거나 권한이 만료됐으면 버튼을 누르라는 안내를 하지 않는다. 준비·재조리·오류와 서버 권한/명령은 유지한다.

[수행 책임 r16](../ProjectOverview/page-docs/driver-workspace-sections-r1.md)·[완료 상세 책임 r5](../ProjectOverview/page-docs/driver-completed-delivery-detail-r1.md)·[작업 범위](../AI/Planning/공통/PLAN-OPERATIONS-DRIVER-VERTICAL-REFACTOR/driver-detail-work-copy.r53.work-order.json)를 따른다. 전체 지도·핀/경로·카드 접기·전경 업무·정산 계산·입금 정책·서버/API/DB는 변경하지 않는다.

## 검증·설치

집중 Fast 335/335와 Task 실행 9,807/9,807이 통과했다. 환경 의존 Mongo 시험 15개는 미실행이며 실패는 없다. 음식 기사 코드 지도 검사·네이티브 및 전체 솔루션 빌드가 통과했고 소유 소스 12개의 검증 전후 SHA-256이 동일하다. 기존 경고는 남아 있으며 빌드 오류는 없다. 시작 시 USB 인증 및 Samsung SM-N981N(Android 13/API 33)의 잠금 해제를 확인했다. 기존 기사 `0.1.31/31`의 설치 APK와 서명을 비공개 증거에 보관했다. 신규 0.1.53/53 ARM64 독립 APK를 기존 앱과 동일한 서명으로 2026-10-10 00:04 KST 업데이트 설치했다. 기존 앱 자료 삭제/초기화 없이 `install -r`가 Success를 반환했고, 단말 APK SHA-256이 로컬 확정본과 동일하다. r52 내 정보 메뉴/완료 목록·복귀도 함께 포함한다.

PC의 5363/5321 로컬 서버는 시작 시 응답하지 않았다. 기존 시험용 Docker 시작에서 Inference runtime socket 초기화 오류를 확인했고 정상 종료 1회도 실패했다. 강제 종료·런타임 폴더 변경·설정/DB/볼륨 초기화는 하지 않았다. 서버 통신과 단말 화면의 확인 범위를 설치 성공과 분리해 기록한다.

시험·APK·단말 해시·화면 근거는 Git 제외 `artifacts/local/driver-detail-work-copy-r53/`에 보관한다. 실제 배달 수락·전달·정산/입금·Azure·Unity·영상·commit/push는 수행하지 않는다.

## 코드와 실행 근거

- [요금 표시 모델](../../FDriverApp/PageModels/FDriverDailySettlementItem.cs)·[완료 상세](../../FDriverApp/Pages/CompletedDeliveryDetailPage.xaml): 저장 요금/근거 분기를 그대로 읽어 행으로 표현한다. 총액·공제·수령액을 새 요율로 계산하지 않는다.
- [현재 카드](../../FDriverApp/PageModels/MainPageModel.FoodPresentation.cs)·[공통 단계 표시](../../Ssalddel.Ui.Common/Areas/App/RoleWorkspace/Food/FoodDriverStagePresentation.cs): 단계/주 행동/주소/현재 안내만 파생한다. 기존 명령과 native 활성 조건의 만료 판정은 변경하지 않았다. 공통 가능 행동의 만료는 새 안내에서 반영한다.
- 새 회귀 21사례(요금 9·수행 안내 12)를 기존 스위트에서 실행했다. 상세의 3일 열람 제한과 pause/deactivate 시 개인정보 제거는 변경하지 않았다.
- Fast: `artifacts/local/validation/20261009-235109/`; Task: `artifacts/local/validation/20261009-235313/`. `test-results.json`은 TRX의 실행/미실행 건수를 구분한다.

이번 실행은 실제 DB/HTTP 주문 완주·배차·입금이나 고객 자료 전수 검수가 아니다. Docker의 원인 조사와 정상 종료 실패는 `docker-recovery.json`에 별도로 기록했다.

## APK·휴대폰 결과와 한계

- 패키지 `kr.ssalddel.fdriver`, 버전 `0.1.53/53`, ARM64, 92,174,357바이트. SHA-256 `C57E4D39BE5FEB285E7A248294F47CE9B6CCC43436CF36CD66FADA37BD5F6520`.
- 기존 설치 APK와 같은 인증서 및 v2/v3 서명, 생성된 네이버 SDK 설정 유지, APK에 내장된 기사/공통 UI DLL과 최종 빌드 DLL의 바이트 일치를 확인했다. `apk-proof.json`과 `phone-install-proof.json`에 별도 근거를 기록했다.
- 최초 앱 실행 명령은 `Status: ok`였지만 약 7초 뒤 실제 프로세스가 종료되고 MainPage XAML 생성 예외를 확인했다. 시작 오류를 보완 중이며 설치 성공을 정상 실행으로 인증하지 않는다. 휴대폰은 잠겨 있어 실제 표시 화면·내 정보/완료 목록/상세 이동·지도 렌더는 미검증이다. 잠금 해제를 우회하거나 설정을 바꾸지 않았다. `phone-runtime.json`에서 실행 성공과 화면 미검증을 구분한다.
- 로컬 USB 시험 서버는 Docker Inference 소켓 초기화와 정상 종료 실패로 시작하지 못했다. 실제 통신·새 현재 업무 API 호환·실제 배차/수행·공용 DB/정산/입금 검증은 하지 않았다. 기존 r27 시험 서버 이미지를 최신 서버로 간주하지 않는다.
- APK는 로컬 USB/5363용 Debug이며 Azure 배포용이 아니다. 휴대폰 설치 성공을 GPS·원격 알림·실제 업무 완주 증거로 바꾸지 않는다.

## 실제 단말 시작 오류와 최소 보완

첫 설치본은 설치/해시 대조에 성공했지만 실행 약 7초 후 종료됐다. 임시 Debug 시작 진단으로 `VisualStateGroup Names must be unique`와 `VisualStateGroupList.Validate/Add`를 확인했다. 기본 버튼 스타일이 가진 `CommonStates`에 내 정보 메뉴의 같은 그룹을 암묵적으로 추가한 것이 원인이다. 새 검사는 수정 전 실제 메뉴 1개 실패·의미 모델 5개 통과로 같은 중복을 재현했다.

[메뉴 행](../../FDriverApp/Controls/DriverProfileMenuRow.xaml)에 명시 `VisualStateGroupList` 래퍼 두 줄만 추가해 목록을 교체했다. 기존 한 그룹·다섯 상태·여섯 setter, 텍스트/아이콘·바인딩·입력·접근성은 유지한다. [회귀 검사](../../Ssalddel.Tests/Clients/FDriverNativeVisualStateTests.cs)는 스타일 상속과 목록 추가/교체 및 상태명 중복을 좁은 XML 의미 모델로 검증하며 실제 MAUI 실행을 대신하지 않는다.

임시 MainPage 생성자 진단은 원본 바이트로 제거했다. 최종 소스/검증/설치 증거는 초기 실패본 및 진단본과 구분해서 기록한다. 최종 결과는 아래 재검증·재설치 절에 기록한다.

## 최종 수정본 검증·재설치 (2026-10-10)

초기 설치/진단본과 구분한 최종 수정본은 Fast 341/341·Task 실행 9,813/9,813 통과, Mongo 환경 15개 미실행이다. 최종 실행 근거는 `artifacts/local/validation/20261010-002252/`와 `20261010-002604/`다. APK 전용 restore 이후 첫 Fast의 NoRestore는 다른 타깃 assets 누락으로 실패했으며 정상 restore 재실행으로 해결했다. 소스 14개 해시가 동일하고 임시 MainPage 진단은 백업과 바이트 단위로 일치하게 제거했다. 새 회귀는 총 27사례(요금 9·수행 안내 12·스타일 6)다.

최종 APK `driver-usb-r53-fixed.apk`는 0.1.53/53, 92,174,357바이트, SHA-256 `0C5DF30A7E64CF094B54D94E45872FCF323E6C6A7210A8077DEB746C5DD089BC`다. 기존 앱과 동일한 v2/v3 서명·네이버 SDK 설정·내장 기사/공통 DLL을 대조했다. 2026-10-10 00:32:06 KST 자료 삭제/초기화 없이 업데이트 설치했고 단말 해시가 일치한다. 초기 실패본/진단본은 비공개 근거로 분리 보존했다.

최종 실행은 약 60초 프로세스 유지·새 충돌 0건이다. 잠금 해제 후 실시간 접근성 텍스트와 활성 버튼으로 내 정보 → 배달 내역 → 복귀, 복귀 권역/배달 경험 공유 → 복귀를 확인했다. 원본 UI 덤프/스크린샷·고객/계정 값은 저장하지 않았고 정해진 화면 문구와 검증 결과만 남겼다. 설정 변경·실제 업무는 하지 않았다. 이는 실제 기기의 화면 이동 검증이며 시각 배치·지도 렌더·서버 데이터 조회·전체 업무 완주 증거는 아니다.

로컬 서버는 앞서 기록한 Docker 오류로 준비되지 않았다. 실제 배달 내역/완료 상세 데이터·현재 수행 카드·서버 통신·GPS·원격 알림·입금은 미검증이다. 후속 사용자는 USB를 해제했으며 이 기록 이후 기기 조작 없이 로컬 검증으로 전환한다. commit/push는 하지 않았다.
