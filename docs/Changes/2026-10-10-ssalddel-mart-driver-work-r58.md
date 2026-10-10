# 살뜰마트 기사 수락·픽업·전달 소프트웨어 연결 r58

- 기획: [승인 범위](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-driver-work.implementation.r58.md) · [작업 지시·개발 반환](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-driver-work.r58.work-order.json)
- 상태: `Implemented / LocalValidated / SimulationConsumerBound / OperationalDeferred`.
- 원본 기준: `d1bab3eec08dc0a49c9a87f882590d394205c9a4`와 대규모 기존 dirty 작업을 보존한다. 이번 기록은 커밋·push·배포 기록이 아니다.

## 소프트웨어 경로

기존 마트 원천·출고·피킹·포장 → 공통 배차 큐 → 마트 전용 기사 Controller/Service → 본인 수락 → 현재 전표 입력 한 번으로 픽업 → 전달 완료 → SQL 후속 Outbox → 기존 원장 Adapter 재처리.

- 기존 음식 주문/음식 배달시도·기사 정산을 가짜로 만들지 않는다. 기존 음식 Service와 화물 상태 Executor는 마트 원천을 처리하지 못하도록 차단한다.
- 전표는 현재 모든 관련 창고 권한이 있는 담당자만 명시적으로 발급한다. GET은 생성/회전하지 않는다. 포장 전 발급은 가능하지만 실제 픽업은 현재 전체 피킹·포장·배정·전표·재고 조건을 요구한다.
- 마트 배송 시도, 보호 전표, 인증 주체별 요청 영수증, 후속 Outbox 네 테이블과 Migration/Snapshot을 준비한다. 공용 DB에 적용하지 않는다. 네 자리 실패 회수·현재 판본·같은 요청의 의미 충돌을 검사한다.
- 픽업은 단일 출고에 전량 연결된 lot만 지원하고 예약·분할·공유 lot을 추론하지 않는다. 출고·재고 차감·이력·이동·배송 시도·최소 사건·요청 영수증·후속 의도를 같은 SQL transaction으로 저장한다.
- 전달 완료는 기사 사실이며 수령 확인·보험/배상·정산·지급 완료가 아니다. Mongo/마트 원장 후속 실패는 재처리 대기로 남기며 SQL 원본을 되돌리지 않는다.
- 기존 마트 원장 소비자는 최신 기사 수행 판본을 읽고 SQL 업무 열을 되쓰지 않는다. 과거 Pending·최초 수락 경합은 재처리 대기로 남긴다. 서버 상태 사본의 블록·참조 digest와 원장 상태/현재 단계 결속으로 지연 역투영을 막되 사람이 명시 편집한 원장은 기존 경로를 유지한다. SQL/Mongo 전체를 원자화한 것은 아니며 저장 중 판본이 바뀌면 잠시 오래된 사본이 보일 수 있고 후속 재처리로 수렴한다.
- 음식 배달 목록은 마트 별칭을 12건 제한 전에 제외한다. 마트 업무가 기존 음식 제안을 밀어내거나 동일 번호 음식 주문에 잘못 연결되지 않도록 유지한다.

## 앱·복구 책임

[화면 책임 카드](../ProjectOverview/page-docs/ssalddel-mart-driver-work-r58.md)에 따라 FDriver의 내 정보 → 살뜰마트 배송을 별도 진입으로 조립한다. 기존 음식 지도·업무·내역·정산은 유지한다. 제안·현재 수행·허용된 다음 행동, 비활성·로딩·빈 상태·실패·재조회를 구분한다.

카메라 대신 QR 문자열/네 자리 번호를 직접 입력한다. 제출 성공이 픽업 완료이고 추가 완료 버튼은 없다. 불명확한 전송은 요청 ID·행동·판본·코드 지문만 보호 저장한 뒤 GET으로 먼저 확인한다. 코드 원문·주소·토큰은 pending 저장에 넣지 않는다. 명확한 서버 거절은 현재 정본/목록을 확인한 뒤 새 입력을 허용하며, 접수 여부를 모르는 실패와 혼동하지 않는다. 자동 POST 반복은 없다.

## 검증

| 증거 | 실제 결과 | 원시 실행 기록 |
| --- | --- | --- |
| 최종 Fast·집중 시험 | 262건 통과, 실패·건너뜀 0 | `artifacts/local/validation/20261010-193237/` |
| 전체 역할 앱 build | `Ssalddel.v3.5.slnx`, 오류 0·경고 70 | `artifacts/local/validation/20261010-193355/build-01.log` |
| 최종 Task·전체 시험 | 10,299건 통과, 실패 0·Mongo 환경 의존 15건 건너뜀, 총 10,314건 | `artifacts/local/validation/20261010-193355/` |
| 원본 결속 | 승인 기획 SHA-256 일치, 최종 검증 전후 소스·시험 44개 지문 변화 0건 | 작업 지시의 `sourceFingerprints` |
| 문서 Fast·링크 | 문서 9개·로컬 링크 1,186개, 누락 0·범위 diff check 통과. 문서 전용 실행은 build/test 생략 | `artifacts/local/validation/20261010-193910/` |

격리 SQLite의 실제 Controller/Service·출고·재고·마트 시도·요청 영수증·SQL Event/Outbox를 실행했다. 후속 원장 소비자는 실제 구현과 시험용 커뮤니티 저장소로 검증했으며 MySQL/Mongo/Redis 운영 연결 증거가 아니다. QR/네 자리 정상 픽업, 잘못된·이전 전표, 현재 기사·revision·포장·단일 lot 관문, rollback, 요청 결과 재조회, 후속 실패·재처리, 지연 원장 소비와 최초 수락 경합, 음식/화물 우회 차단을 포함한다.

FDriver API·VM·구성 시험과 네이티브 XAML 컴파일은 통과했다. 실제 MAUI 화면 렌더·휴대폰 설치·보호 저장 장치 시험은 하지 않았다. 지도 SDK·푸시 미설정·패키지/nullable 관련 build 경고는 남아 있다. 최종 Fast build는 오류 0·경고 140이며, 증분 Task build의 경고 수와 구분한다.

최초 검증 실패도 보존한다. `190104` 코드 지도 stale, `190320` 시험 using 누락, `191843` .NET 10 배열 Contains/EF 조회와 명령 거절 응답, `192240` 경합 시험의 이전 상태 가정, `192637` API 분류 metadata 두 건을 보완한 뒤 위 최종 실행을 통과했다. 모두 같은 날짜의 `artifacts/local/validation/` 아래 실행이며 이전 r57 결과를 재사용하지 않는다.

## 남은 경계

`MartDriverWorkflow:Enabled` 기본 OFF, Simulation 및 기존 마트·음식 배달·창고 기능 관문을 모두 요구한다. 실제 MySQL/Mongo/Redis·인증 운영 HTTP·단말 설치/화면·보호 저장 실기기·실물 QR/카메라/프린터·결제/지급·외부 알림·운영 활성화·commit/push·배포는 수행하지 않는다. 네이티브 UI 컴파일·VM 시험·실제 화면 렌더는 서로 다른 증거로 보고한다.

다음 소프트웨어 우선순위는 창고/마트 담당자 화면의 실제 전표 발급 API 연결과 격리 인증 HTTP·DB 종단 검증이다. r56 합성 미리보기를 실제 창고 화면으로 간주하지 않으며 하드웨어 구매를 선행 조건으로 삼지 않는다.
