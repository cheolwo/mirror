# 생활 물품 전달 선택과 유연한 배차 r22

2026-10-06. [승인 계획과 페이지 책임](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/flexible-delivery.implementation.r22.md)에 따라 일반 물품 교류의 직접 수령·직접 전달·기사 배송을 구현했다. 상태는 `Implemented / ValidatedLocal / FieldRuntimePending`이다.

## 바뀐 사용자 흐름

- 협업 작성에서 전달 방식과 비공개 인계 장소를 입력한다. 양측의 현재 안내 동의와 조건 합의를 받은 뒤 시작하며, 제공자가 완료를 제안하고 받는 사람이 확인한다. 제공/필요 글의 실제 관계로 제공자를 정한다.
- 직접 수령·직접 전달에는 배차 원장을 생성하지 않는다. 물품 거래 금액과 기사 배송비를 따로 표시한다.
- 기사 배송은 서버 견적과 별도 신청 동의로 접수한다. 자동 추천, 공개 콜, 병행을 배송 건마다 선택하며, 추천 자체가 기사 수락을 뜻하지 않는다. 병행에서 추천 기사와 콜을 잡은 기사도 같은 운송 원장 한 건을 수락한다.
- 기사 확정 전에는 같은 큐의 배차 방식을 바꿀 수 있다. 직접 전달로 조건을 바꾸거나 동의를 철회하면 대기 배송을 닫고 양측 합의를 다시 받는다. 확정 후에는 기존 배송 취소·재배차·물건 인수 절차를 먼저 확인한다.
- 기사 배송의 협업 완료는 실제 운송 원장 `인수완료`가 필요하다. 배송 완료와 수금/입금 확인은 독립 표시한다. 조건이 바뀌면 이전 상세 인계 동의를 재사용하지 않는다.

## 저장·권한·호환

협업과 복구할 원 요청은 기존 보호 Mongo 문서에 저장하고 조건 판본·양측 합의·행위자·출처·합의 수량을 CAS로 고정한다. SQL 운송 의뢰는 계정+client ID의 결정적 ID와 unique 접수 키를 사용한다. 기존 Mongo-first 생성의 SQL 투영부터 새 타입 큐에 선택/수락 차단을 기록하고, 현장 지급 조건과 협업 연결을 확정한 뒤 수락을 연다. 부분 실패 시 같은 원 요청으로 복구한다. 응답이 불명확해도 원 ID·입력·동의 증적·요금 조건을 보존한다.

배차 선택은 기존 기사 수락과 같은 SQL 큐의 transaction·선택 판본·추천 라운드를 사용한다. 이전 추천/라운드 수락은 거절한다. 공개 콜은 준비된 건만 노출하며 기사 적격성·권한·연속배차 경계는 기존 수락 handler가 검사한다. 자동 추천만 선택한 건은 후보 부재에도 공개 콜로 전환하지 않고 간격 후 재탐색한다. 음식 배달과 기존 null-mode 운송은 기존 정책을 유지한다.

가산 migration `20261006120000_AddNeighborhoodDispatchChoice`는 기존 행에 mode=null, revision=0, ready=true를 유지한다. MySQL 8.4 임시 테이블에 정확한 Up 연산을 적용해 기존 값과 unique 키를 검증했다. 저장소 전체 migration 이력을 처음부터 운영 DB에 적용한 증거는 아니다. 신청 동의 Guid의 Mongo 표준 BSON 왕복도 확인했다.

## 실제 로컬 실행

본 작업 전용 MySQL 데이터베이스와 인증된 Mongo 컨테이너, 127.0.0.1:5542/5543 두 서버를 사용한다. 실제 public 글 API, 보호 협업/신청 동의 저장, 화주 생성·현장 지급, Mongo→SQL 업무 투영, 정책 엔진, 기사 수락/픽업/전달 handler를 실행했다. 공개 글·합의·배차·운송 상태를 HTTP로 준비하고, 공통 Razor 화면에서 계정을 전환하여 제공자 수행/완료 제안과 수령자 완료 확인을 다섯 건 모두 기록했다.

| 흐름 | 배차 | 현재 결과 |
| --- | --- | --- |
| 받는 사람 직접 수령 | 없음 | 양측 협업 완료, 신규 운송 의뢰 없음 |
| 제공자 직접 전달 | 없음 | 양측 협업 완료, 신규 운송 의뢰 없음 |
| 자동 추천 | 추천 기사 수락 | 운송 인수완료 + 수령자 협업 완료 |
| 공개 콜 | 다른 기사 자발적 수락 | 운송 인수완료 + 수령자 협업 완료 |
| 자동 추천·공개 콜 병행 | 두 서버 동시 수락, 한 기사 확정 | 동일 원장 인수완료 + 수령자 협업 완료 |

원시 정상 실행 ID는 `r22-ec9715f97464`이며 `http-evidence.json`·`ui-evidence.json`·`completed-evidence.json`에서 동일 collaboration/request ID를 대조한다. SQL/Mongo와 UI의 완료를 지급 인증으로 올리지 않는다.

실제 HTTP 예외 10종: 확정 전 선택 변경/멱등/구형 수락 거절, 대기 기사 배송→직접 전달, 인계 동의 철회, 확정 후 변경 차단과 공개 콜 재배차, 신청 동의 철회, SQL 저장 후 Mongo 연결 실패/비공개 원 요청 재개, 두 서버 동시 동일 접수, 자동 후보 부재/재탐색, 병행 후보 부재의 공개 콜 유지/재탐색, 추천 거절/새 라운드. 중간 실패를 주입한 건은 복구 전 공개 콜·기사 수락이 모두 차단됐다. 재시도 후 원 의뢰/큐를 유지했다. 만료·적격성·기존 음식/화물 경계와 Simulation 게이트는 회귀 시험으로 확인한다.

## 비용·이동·대기 비교

동일 가상 조건은 물품 1개, 거래금액 0원, 경로 3.2km, 기사 운임 8,200원이다. 직접 전달의 플랫폼 배송비 0원을 총비용 0원으로 해석하지 않는다. 수령/제공 당사자의 이동·준비 시간, 유류비와 들고 옮기는 부담은 본인이 부담한다. 왕복이면 6.4km지만 다른 일정에 이어 이동하면 추가 이동이 달라진다. 기사 배송은 당사자의 이동 부담을 줄이며 기사 접근·대기·전달 노동과 배송비가 생긴다.

세 배차 방식의 운임은 이 가상 입력에서 같았다. 실제 기사 공급·ETA·GPS·적재 적합성·장소 접근·최종 실비는 측정하지 않았으므로 어떤 방식이 더 빠르거나 더 합리적이라고 확정하지 않는다. `검증-보고서.html`은 입력 거리·왕복 여부·속도·운임을 바꿔 가정의 이동 부담을 비교하며 실제 관측값과 구분한다.

## 검증과 실행 한계

Fast `20261006-112707` 집중 **455/455**, Task `20261006-112807` 전수 **7,738/7,738** 통과. 두 단계 모두 `Ssalddel.v3.5.slnx` 전체 제품/소비 앱 빌드 오류 0이며 최종 Task 빌드는 기존 종속성/분석 경고 58개다. 별도 실행 프로필 최종 빌드와 최신 코드에서 합의 수량 불일치 접수 차단/일치 수량 단일 접수, 다섯 완료 원장 재조회를 확인했다. 320/390px 배차 선택은 가로 넘침이 없고 실제 UI에서 직접 수령 신청·배차 방식 변경 저장/재조회가 성공했다.

초기 Task `20261006-112025`는 기존 `FDriverWorkspaceLifetimeTests.HistoryRoute_KeepsForegroundWorkLocationAndRecommendations_WithoutHiddenRoutes`의 이력→주행 경로 표시, `20261006-112506`은 기존 `FDriverFoodPushTests.Receiver_CallbackTimeoutCannotPublishAfterDelayedLocalRestore`의 제한 시간 초과로 각각 1개 실패했다. 해당 소스는 수정하지 않았다. 첫 시험 단독 재검증은 통과했으며, 최종 Fast/Task는 검증용 두 서버·Mongo를 중단하고 `DOTNET_PROCESSOR_COUNT=4` 조건으로 실행해 모두 통과했다. 이를 해당 시험 자체를 수정한 결과로 보고하지 않는다.

문서 링크 726개 누락 없음과 범위 지정 `git diff --check`를 확인했다. 기준선 범위 밖 12,516개 파일의 해시가 유지됐으며 12개 기존 파일과 21개 새 파일의 독립 동시 변경도 관찰했다. 그것들을 되돌리거나 stage하지 않았다. HEAD `b1b7cfc39b5183ed56246080c141d63965bf2704`·`dev/mirror-integration` branch가 유지됐고 삭제된 기준선 파일은 없었다. 보존 비교는 전 작업 트리가 불변이라는 주장이 아니다.

외부 경계는 명시적 fixture다: 로그인 계정, 주소 좌표, 경로·요금, 기사 후보 가용/적격성, 연속배차 정보, 알림, 사진 객체와 부분 실패 주입. 실제 운행·현장 인수·은행 입금·외부 경로 품질·휴대폰 설치·Azure 공개 운영은 확인하지 않았다. Unity 권위·영상/정산 자료·공개 기능 게이트를 확장하지 않았다. 기존 관련 없는 dirty 작업은 되돌리거나 함께 stage하지 않았으며 commit/push/외부 배포는 하지 않았다.

## 화면과 재실행

| 화면 | 실제 PNG |
| --- | --- |
| 직접 수령 완료 | [완료 화면](../assets/changes/2026-10-06-neighborhood-flexible-delivery-r22/flow-0.png) |
| 직접 전달 완료 | [완료 화면](../assets/changes/2026-10-06-neighborhood-flexible-delivery-r22/flow-1.png) |
| 자동 추천 완료 | [완료 화면](../assets/changes/2026-10-06-neighborhood-flexible-delivery-r22/flow-2.png) |
| 공개 콜 완료 | [완료 화면](../assets/changes/2026-10-06-neighborhood-flexible-delivery-r22/flow-3.png) |
| 병행 협업 완료 | [완료 화면](../assets/changes/2026-10-06-neighborhood-flexible-delivery-r22/flow-4.png) |
| 실제 화면의 직접 수령 접수 | [입력 저장 결과](../assets/changes/2026-10-06-neighborhood-flexible-delivery-r22/create-saved.png) |
| 320px 배차 선택 | [모바일 화면](../assets/changes/2026-10-06-neighborhood-flexible-delivery-r22/dispatch-choice-320.png) |
| 확정 전 배차 방식 선택 | [재조회 화면](../assets/changes/2026-10-06-neighborhood-flexible-delivery-r22/dispatch-choice.png) |
| 운송 인수완료·현장 지급 예정 분리 | [배송 상세](../assets/changes/2026-10-06-neighborhood-flexible-delivery-r22/delivery-complete.png) |

[검증 프로필](../../eng/NeighborhoodExchangePreview/FlexibleDeliveryPreview.cs), [시작 도구](../../eng/NeighborhoodExchangePreview/run-flexible.ps1), [다섯 HTTP 흐름](../../eng/NeighborhoodExchangePreview/verify-flexible.py), [예외 흐름](../../eng/NeighborhoodExchangePreview/verify-flexible-edges.py)을 사용한다. `run-flexible.ps1`은 자체 MySQL/Mongo만 허용하며 루프백 두 포트로 실행한다. 원시 로그·TRX·DB 이름·검토용 비교는 Git 제외 `artifacts/local/neighborhood-flexible-delivery-r22/`에 남긴다.
