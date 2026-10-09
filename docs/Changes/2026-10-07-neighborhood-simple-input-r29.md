# 생활 배송 간편 작성·엑셀 입력 r29

2026-10-07. [승인 r29](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/simple-input.implementation.r29.md), [작업지시서](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/development-work-order.r29.md), [화면 책임](../ProjectOverview/page-docs/neighborhood-simple-input-r29.md)을 따른다. 승인 문서 SHA256 `1D6D6CA37CDBEC26B5D5050B968C66F461E1F45AE82D7A2BACD5B0FAB0434BDE`, 기준 HEAD `9336637bde31ec97b5740e2a82a802dd490127ca`를 보존한다. 기존 다른 dirty 변경은 정리하거나 함께 stage하지 않았다.

## 구현 범위

기존 배송 작성 한 페이지에서 `물품 → 장소 → 시간 → 확인`으로 입력을 나눴다. 상세 작성도 같은 초안으로 돌아갈 수 있고 개인정보 동의·거래 안내·현재 견적·접수 결과 재확인은 기존 경로를 따른다. 음식 주문과 음성 입력은 이 slice에 포함하지 않는다.

다운로드 양식은 `neighborhood-delivery-input.v1`, `배송입력` 시트 A1 판본·A3 고정 19열·4행부터 최대 100개 업무 행이다. 개인 자료나 합성 배송값을 미리 채워 넣지 않았다. 전화 열은 문자 서식을, 시간 열은 Excel 날짜 서식을 사용한다. 한국 시간의 텍스트 날짜/명시적 날짜 서식의 숫자/시간대 있는 ISO를 읽고 서식 없는 숫자 날짜는 추정하지 않는다. 안내 시트와 입력 좌·우 영역을 각각 렌더해 글자·열·빈 행을 확인했다.

`GET api/v1/common/neighborhood-deliveries/input-template`와 `POST …/input-preview`를 기존 Controller에 추가했다. Client는 보호 JSON을 사용한다. 인증과 본인의 기존 개인정보 동의를 확인한 뒤 입력을 메모리에서만 파싱한다. 실제 서비스에서 DB·견적·주소 검색·배차·외부 업체를 호출하지 않는다. 기존 서버가 다른 업무에도 수용하는 비암호화 JSON 호환 자체는 이번에 바꾸지 않았으며 보호 특성과 클라이언트 선택을 시험한다.

파일 2 MiB, 전체 압축 해제 20 MiB, XML 구성 항목 2 MiB, 최대 100행 및 셀·문자열·ZIP 항목 수를 제한한다. 매크로·암호·수식·외부 연결·DTD·중복 ZIP 경로·경로 탈출·판본/열 오대응을 거절한다. 신규 업로드 경로의 전송 본문은 이중 Base64를 고려해 4 MiB로 제한하고, 같은 크기의 메모리 버퍼를 사용해 기존 middleware의 디스크 임시 버퍼 전환을 막는다. 기존 route의 동작은 바꾸지 않았다.

행별 원 셀과 오류를 표시하고 한 행만 초안에 적용한다. 누락 수량·중량·전화·시간을 추정하지 않는다. 잘못된 행 ID/중복 ID는 원 파일 수정 후 재업로드하며, 잘못된 선택 크기와 너무 긴 요청/상세주소는 빈 값으로 바꾸지 않고 사용자 수정을 요구한다. 업로드에서 동의·요금·GPS·외부 문맥·완료 상태를 받아들이지 않는다. 전달 시간창은 선택이며 생략 의사를 확인한 경우 null로 유지한다.

유효 행 ID는 기존 요청 번호로 사용한다. 행 ID가 없는 입력은 파일 SHA256·시트·행을 현재 작성 세션에만 결속한다. 같은 파일/행의 재선택·재업로드는 같은 ID이며 수정 파일·새 파일·화면 재시작의 과거 접수 여부는 자동 보장하지 않는다. 로그아웃·소유자 변경·화면 종료·늦은 응답은 초안/미리보기를 폐기한다. 결과 불명확인 원 접수는 새 파일로 덮지 않고 원 요청 그대로 확인한다.

## 검증 결과

| 증거 | 실제 결과 | 경계 |
| --- | --- | --- |
| 신규 파서·Controller/동의·보호 전송·입력 VM 시험 | 70개 통과: Excel 48, 메모리 전송 3, 간편 입력 19 | 대역과 순수 코어/Controller 시험이며 실제 인증 HTTP·DB 종단 아님 |
| Fast | 관련 시험 453개 통과, 실패 0 | 최종 소스의 영향 범위 |
| Task | 8,540개 통과, 실패 0, 환경 조건 Mongo 2개 건너뜀 | 미실행 Mongo 2개를 통과로 합치지 않음 |
| `Ssalddel.v3.5.slnx` 소비 앱 포함 build | 오류 0, 기존 Android 패키지 경고 55 | APK 패키징·휴대폰 실행 아님 |
| 검토 호스트 build | 오류/경고 0 | 루프백 합성 검토 호스트 |
| 공통 Razor/VM 320/390px | 36장면·6흐름 통과, 가로 넘침·page error·외부/API 요청 시도 0 | 실제 파서/공통 UI, 합성 동의·견적·등록 Client |
| 실제 `.xlsx` 양식 | 빈 입력·판본/19열·전화 문자·날짜 서식 확인, 입력 좌/우·안내 렌더 검토 | 실제 사용자 배송 파일 없음 |

UI 흐름에서는 빈 양식·수식 파일·2 MiB 초과 거절, 정상 두 행의 미리보기, 누락값 비추정, 앞자리 0/날짜 보존, 한 행 적용, 뒤로 이동, 시간 명시 검토, 입력 수정 직후 견적 무효화를 확인했다. 업로드/행 선택/검토만으로 접수하지 않으며 등록 호출 수는 0이었다. 별도 VM 시험에서 접수·늦은 응답·로그아웃·불명확 결과의 원 요청 재시도를 확인했다.

시험 중 Excel 기본 ContentType 해석과 1904 날짜 소수 정밀도 문제를 수정하고 실제 생성 양식으로 재검증했다. 수식/잘못된 선택값/행 ID를 오류 없는 입력처럼 소비하지 않도록 보완했다. 최종 교차 점검에서는 workbook·Relationships의 root/namespace와 직접 자식 연결도 확인하도록 강화해 반례 6개를 추가했다. 그 뒤 Fast/Task·UI를 다시 통과했고 최종 제품 소스는 마지막 Task 뒤 변경하지 않았다.

최종 Fast TRX는 `artifacts/local/validation/20261007-203433/Ssalddel.Tests.trx`, Task는 `artifacts/local/validation/20261007-203656/`에 있다. UI의 `report.json`에는 시험 전/후 같은 제품 소스 hash와 `sampleOnly=true`, `realApi=false`, `apk=false`를 보관했다. 서버 양식과 사용자 제공 출력 사본의 SHA256은 모두 `05CA7E6F356A28F4DC94A339CE4CFEB396922C03572873AB3B141CBF1C02C68E`다. 검토 호스트는 종료했으며 루프백 5400 listener가 남지 않음을 확인했다.

현재 인증 운영 HTTP/실DB 종단·APK·물리 휴대폰·음성/마이크·외부 업체·실금전·Unity·commit/push·배포는 이번 범위에서 실행하지 않았다. r29 상태는 `Implemented / LocalInputValidated / OperationalE2ePending / VoiceDeferred`다.

문서 12개 경로의 Fast는 `artifacts/local/validation/20261007-204142/`에서 통과했다(build/test 생략). 신규/승인/작업지시서/페이지 책임/결과/양식 도구 6개 문서의 로컬 링크 34개는 누락 0이며 `git diff --check`를 통과했다. 승인 r29 hash와 기준 HEAD는 변경하지 않았다.

검토 화면은 합성 계정·주소·연락처·금액·시간을 사용한다. 순수 엑셀 파서는 제품 코드와 같지만 동의·견적·등록 Client는 메모리 대역이다. 따라서 캡처를 확정 디자인이나 실제 배송 접수/업무 완료 증거로 해석하지 않는다. 상세 로그·TRX·원 렌더는 `artifacts/local/neighborhood-simple-input-r29/`에 둔다.

## 실제 공통 UI 샘플

아래 PNG는 고정 시안이 아니라 코드 실행 화면의 검토 샘플이다. 실제 사용자 자료는 없다.

| 화면 | 확인점 |
| --- | --- |
| [물품 입력 390px](../assets/changes/2026-10-07-neighborhood-simple-input-r29/input-goods-390.png) | 기본 간편 작성·상세 작성 복귀·포장 보조 입력 |
| [장소 입력 390px](../assets/changes/2026-10-07-neighborhood-simple-input-r29/input-places-390.png) | 픽업/전달 주소·담당자·연락처 |
| [시간 입력 390px](../assets/changes/2026-10-07-neighborhood-simple-input-r29/input-time-final-390.png) | 필수 픽업·선택 전달·시간 검토 |
| [엑셀 행 선택 320px](../assets/changes/2026-10-07-neighborhood-simple-input-r29/import-rows-320.png) | 2행 요약·보완 항목·등록 전 선택 |
| [선택행 최종 확인 390px](../assets/changes/2026-10-07-neighborhood-simple-input-r29/import-confirm-390.png) | 원 파일 값을 확인하고 기존 안내/견적으로 합류 |
| [엑셀 안내 렌더](../assets/changes/2026-10-07-neighborhood-simple-input-r29/excel-guide.png) | 고정 열·범위·중복 주의·한국 시간 안내 |

## 변경 책임

- 계약/서버: `NeighborhoodDeliveryInputDtos`, `생활배송엑셀초안Service`, 기존 `생활배송의뢰Controller`, Operations DI, 정적 양식 및 EmbeddedResource, 신규 업로드 전송 버퍼 경계.
- 공통 Client/UI: 기존 `NeighborhoodDeliveryClient`/작성 VM/작성 화면, `생활배송간편입력ViewModel`/컴포넌트와 생활 배송 CSS.
- 시험/검토 도구: Excel/InputMemoryTransport/SimpleInput 시험, 기존 `AssumedFulfillmentPreview`의 실제 파서·공통 화면 결속 및 양식 생성 도구.
- 문서: 승인/수용/기획 목차/현재 상태/페이지 책임과 본 결과. 승인 r28/r29의 내용·hash와 이전 판본 증거는 덮지 않는다.
