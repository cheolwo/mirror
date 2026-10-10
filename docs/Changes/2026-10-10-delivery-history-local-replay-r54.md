# 기존 배달 기록을 기사·음식점 로컬 검증에 재사용 r54

사용자가 실제 배달 데이터를 로컬 테스트에 활용하도록 요청했다. USB 연결이 해제되어 있으므로 단말 배포본 지정·설치는 하지 않는다. 검증 정책의 전체 솔루션 빌드에는 Android 컴파일이 포함된다.

[승인 범위](../AI/Planning/공통/PLAN-OPERATIONS-DRIVER-VERTICAL-REFACTOR/delivery-history-local-replay.r54.md)·[작업지시서](../AI/Planning/공통/PLAN-OPERATIONS-DRIVER-VERTICAL-REFACTOR/delivery-history-local-replay.r54.work-order.json)를 따른다.

## 자료와 책임

최신 14개 영상 계획과 배달 건수 원장의 574건을 관계형 로컬 검증 자료로 추출한다. 기사 1명—배달 내역—음식점 지점을 연결하고 픽업 방문과 배달 건은 분리한다. 같은 주소의 공동 픽업에서도 다른 상호는 별도 지점이며 원자료를 변경하지 않는 검토된 예외 매핑을 사용한다.

메뉴는 건별 기존 입력과 10월9일 앱 직접 ID 연결만 사용한다. 과거 앱 일치 후보를 확정하지 않으며 방문 요약을 각 주문에 복제하지 않는다. 항목 표시 금액·주문총액·단가는 구별하고 단가를 수량으로 역산하지 않는다.

개인 자료·검토 매핑·검증 산출물은 Git 제외 `artifacts/local/delivery-history-replay-r54/`에만 둔다. 일반화한 추출기와 스키마·fixture·시험 코드를 보관한다. 고객 이름·전화·전달 주소·플랫폼 주문번호·원문 UI·전사는 추출하지 않는다. 공개 음식점 주소만 지점 식별에 재사용한다.

## 실제·모의 경계

원본 날짜·배달료·앱 거리·확인된 메뉴와 주문총액은 관측값이다. 원본 완료 시각·수령 확인·공제·실입금은 이번 자료로 확정하지 않는다. SQL 테스트의 계정·상태·생명주기 시각 및 최소 주문 FK를 위한 12,345원 주문총액은 명시적 합성 자료다. 이 금액을 실제 주문총액이나 수수료 계산에 사용하지 않는다.

기사 목록의 날짜 구간 검사에는 별도 모의 시각을 공급하며 화면은 원본 완료 시각 미확인으로 표시한다. 조회 원장의 배달료를 이 플랫폼의 수락 견적·실지급 근거로 승격하지 않는다. 개인정보 3일 보호는 실제 서버 코드로 검증하며 과거 날짜를 오늘로 바꾸지 않는다.

## 검증 상태

2026-10-10 구현·로컬 검증 완료. 9월23일~10월9일의 **574건·391지점·510픽업 방문**을 연결했다. 합계 배달료는 2,255,300원, 앱 표시 거리는 1,115.8978km이며 미션·프로모션·세금·실입금은 이 합계에 포함하지 않는다. 확인된 메뉴가 있는 기록 272건(메뉴 항목 497개), 주문총액이 있는 기록 301건을 재사용한다. 실제 완료 시각 574건과 메뉴 단가 497개는 미확인으로 유지한다.

- 추출 재실행의 JSON 해시 일치, 허용된 로컬 경로 밖 출력 차단, 미검토 공유주방 연결 차단, 원본 파일 해시 보존을 확인했다. 자료 해시: `89074dfbee38dd99cc7172469ce79640722bbdcda408931d6a03647899fecca6`.
- 실제 SQLite 메모리 DB와 `FoodDeliveryDriverWorkspaceUseCase`에서 574건·17일별 건수와 배달료를 대조했다. 중복 가져오기·다른 기사 접근 차단·3일 지난 상세 보호와 미확인 지급 근거도 확인했다. 실제 음식점 계정 로그인·식당 업무 API 전체 흐름을 검증한 결과는 아니다.
- 320/390px 각각 전체 574건·36페이지를 조회했다. 동일 배달의 기사→음식점→원래 기사 목록 복귀, 건별 금액·앱 거리, 확인된 메뉴·주문총액·옵션, 미확인 단가/완료 시각과 가로 넘침을 확인했다. 입력 없는 호스트와 잘못된 입력 호스트도 각각 2개 폭에서 안내만 표시했다. 외부/업무 API 요청과 소스·바이너리 변경은 없었다.
- Fast **194/194**, Task **9,824/9,824** 통과. 환경 의존 Mongo 시험 **15개 미실행**. 전체 솔루션 빌드는 오류 0개이며 기존 경고 28개가 있다.

로컬 근거는 `artifacts/local/delivery-history-replay-r54/`의 `import-proof.json`, `importer-verification.json`, `server-usecase-verification.json`, `ui/verification.json`, `ui-missing/verification.json`, `ui-invalid/verification.json`, `final-verification.json`에 둔다. Fast 로그는 `artifacts/local/validation/20261010-102832/`, Task 로그는 `artifacts/local/validation/20261010-103002/`이다. 초기 실패 기록은 별도로 보존하며 최종 통과 기록으로 덮어 설명하지 않는다.

## 다시 실행하기

`eng/RoleWorkspacePreview/import-delivery-history.py`에 `--source-root`, Git 제외 `artifacts/local` 아래의 `--output`, `--reviewed-overrides`를 명시한다. 개인 자료를 자동 탐색하거나 일반 샘플로 대체하지 않는다. 실행 시 `SSALDDEL_HISTORICAL_DELIVERY_DATASET`에 JSON 절대 경로를 지정한 뒤 [미리보기 실행 안내](../../eng/RoleWorkspacePreview/README.md#기존-배달-기록-로컬-대조-r54)를 따른다. 현재 PC 조회 주소는 `http://127.0.0.1:5581/historical-delivery-preview`이며 호스트 실행 중에만 접근할 수 있다.

운영 DB 반영·기존 앱의 실제 계정에 자료 입력·휴대폰 설치·Azure 배포·실지급·영상/공유·커밋/푸시는 수행하지 않았다. 검증 중 생성한 임시 빌드 사본의 선택적 정리는 자동 승인 검토에서 정책상 차단되어 실행하지 않았고 사본을 보존했다.
