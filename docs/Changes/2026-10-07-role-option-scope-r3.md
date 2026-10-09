# 역할별 기존 옵션의 적용 범위 표시 보완 r3

## 목적·범위

사용자가 전반적인 보완을 먼저 진행하고 후속에 한 업무를 선택해 수직 검증하는 순서를 수용했다. [승인 기획](../AI/Planning/시스템/PLAN-SYSTEM-ROLE-BUSINESS-OPTIONS/horizontal-hardening.implementation.r3.md)과 [작업지시서](../AI/Planning/시스템/PLAN-SYSTEM-ROLE-BUSINESS-OPTIONS/development-work-order.r3.md), [페이지 책임](../ProjectOverview/page-docs/role-map-workspace-r11.md#기존-옵션의-적용-범위-표시-r3--구현-전-책임-카드)을 따른다.

이번 첫 수평 보완은 기존 공통 UI의 표시 의미를 정리한다. 새 설정 저장·요금표 합의·알림 채널·금전 계산을 구현한 작업은 아니다.

## 변경

| 대상 | 변경 | 보존 |
| --- | --- | --- |
| 주문자 음식점 탐색 | 조회 반경과 주문 허용 지역·배달료 구별 | 공개 기준점·반경 필터·조회 |
| 음식점 주문 행동 | 이번 주문 조리시간과 가게/기기 기본값 구별 | 조리 입력·확인·서버 요청 |
| 음식 기사·운영자 문제 행동 | 이번 배달 중단 사유·이번 중단 건 책임 판정 | 신고/판정 구별·권한·재조회 |
| 화주 절차 | 이번 의뢰 금액·추가 작업, 참고 견적·예정액·실지급 구별 | 차량·금액 입력·계산·원장 |
| 화물 기사 참여 | 의뢰 결제 예정액의 정확한 이름과 건별 참여 범위 | 조건 확인·수락·만료·재조회 |
| 창고 작업 | 이번 작업 기록과 개인 기본값·계약 단가 구별 | 수량·위치·포장·인계 검증 |

제품 변경은 공통 Razor 다섯 파일의 문구·제목에 한한다. 기존 바인딩·가능 행동·disabled/분기·요청·정본 재조회·금액·API/DB/JSON을 변경하지 않는다. r26/r27의 기존 dirty 파일을 쓰지 않았다.

검증 전용 `/role-option-scope-preview/{scenario}`는 실제 공통 화면을 예시 DTO에 연결한다. `shipper`, `restaurant`, `cargo-offer`, `warehouse`와 `cargo-offer?scenario=expired/failure`를 지원하며 이 경로의 업무 POST·PUT·업로드는 차단한다. 예시 금액은 실견적·기사 대가·실지급이 아니다.

## 검증 결과

첫 수평 표시 보완은 `Implemented / LocalPreviewValidated`다. 나머지 옵션 구현이나 운영 업무 전체 완료를 의미하지 않는다.

| 증거 | 이번 결과 | 원시 위치 |
| --- | --- | --- |
| 신규 회귀 | 9개 통과: 실제 HtmlRenderer 6개, 화물 입력/바인딩 소스 보존 3개 | `Ssalddel.Tests/Ui/Common/RoleOptionScopeR3Tests.cs` |
| Fast | 집중 187개 통과, 관련 project build·diff 검사 통과 | `artifacts/local/validation/20261007-164313/` |
| Task | `Ssalddel.v3.5.slnx` build 오류 0·경고 111, 전체 시험 8,397개 통과·2개 건너뜀 | `artifacts/local/validation/20261007-165038/` |
| 소비 preview build | 오류 0·경고 0 | `artifacts/local/role-option-scope-r3/preview-build.log` |
| 실제 브라우저 렌더 | 320/390px × 정상 4종·만료·조회 실패 = 12개 장면, 가로 넘침·page error·외부 요청 0 | `artifacts/local/role-option-scope-r3/ui/verification.json` |
| 실제 입력·확인 | 각 폭에서 조리 20분 입력→확인→입력 복귀, 화물·창고 확인 입력→미리보기 저장 차단, 총 6개 흐름 통과 | `artifacts/local/role-option-scope-r3/ui/interactions.json` |

Fast는 이번 9개 소스 경로와 관련 filter 8개를 전달했다. Task는 같은 경로를 전달했지만 저장소 검사 계약에 따라 전체 솔루션과 전체 시험을 실행했다. Task에서 건너뛴 두 시험은 별도 실제 격리 Mongo 시험이며 이번 작업의 DB 검증 증거로 사용하지 않는다. 빌드 경고 111개도 남겨두었으며, 다른 역할 앱의 패키지/Android 경고 등을 이번 문구 변경의 범위로 확장해 수정하지 않았다.

브라우저 검사는 기존 Playwright와 Chrome을 사용해 loopback `127.0.0.1:5689`의 실제 공통 Razor를 확인했다. UI 자료는 모두 합성 예시이며 **확정 시안·실제 DB/API·기사 대가·운영 완료 근거가 아니다**. 외부 origin은 검사 중 차단했다. 제품의 확인 절차와 오류·재조회·만료 비활성 상태는 유지되었다. 사용한 전용 preview 호스트는 검사 후 종료했다.

검증 중 신규 화물 fixture에서 선택 query 누락 시 예외가 발견돼 `TryGetValue`로 수정하고 12개 장면을 다시 통과시켰다. 최초 실패 진단 캡처는 완료 증거에서 제외한다. 통과 캡처는 정상/만료/오류 12장과 조리 확인 2장이며 390px 정상 네 화면과 조리 확인 화면은 이미지로 직접 판독했다. 원시는 로컬 산출물이며 Git 인계·공개 게시 증거로 간주하지 않는다.

기획 r1과 승인 r3의 SHA256은 각각 `7ADD568F46273E3739BC934CB7CA0B151ADEAFA260BCA44C15237451854A47DC`, `96725BEE35968CAAE7C91865B2843E578BF6BC55841BCE57765E8ED8425A5B75`로 보존했다. 승인 r3 본문은 당시 상태를 유지하고 현재 구현 결과는 이 문서·작업지시서·목차·현재 작업에 분리해 기록한다.

최종 관련 diff 검사와 신규 기획·작업지시서·변경 기록 3문서의 상대 링크 존재 검사는 통과했다. HEAD `9336637bde31ec97b5740e2a82a802dd490127ca`는 유지했으며 stage·commit·push·배포는 수행하지 않았다.

## 남은 범위

- 기기/계정/가게 기본값 저장과 실제 음식 기사 알림 소비 연결은 별도다.
- 사전 합의 요금표·승인/철회·판본 동결의 서버 영속화는 r1의 후속이며 이번 표시 수정으로 완료되지 않는다.
- 수평 보완 후 대표 업무 하나를 선택해 입력→서버 명령→원장→역할 인계→결과→예외 복구를 수직 검증한다. 대표 업무는 아직 선택하지 않았다.
- 실제 DB/인증 HTTP·외부 경로·네이티브 앱/휴대폰·APK·실금전·Unity·Figma·운영 활성화·commit/push·배포는 이번 실행에 포함하지 않는다.
