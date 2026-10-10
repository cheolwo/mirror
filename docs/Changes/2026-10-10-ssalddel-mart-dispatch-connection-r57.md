# 살뜰마트 배차 소프트웨어 연결 기반 r57

- 기준: [승인 범위](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-dispatch-connection.implementation.r57.md) · [작업 지시](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-dispatch-connection.r57.work-order.json).
- 사용자 요청: 프린터·카메라 등 하드웨어보다 운영 배차 소프트웨어 연결을 우선한다.
- 범위: `operations`, 서버·격리 시험. 화면을 변경하거나 새 시안을 만들지 않았다. [r56 샘플](2026-10-10-ssalddel-mart-package-pickup-r56.md)은 합성 자료이며 실제 기사 배차 완료 근거가 아니다.
- 개발 반환: `Implemented / LocalValidated / SoftwareConnectionFoundation / OperationalDeferred`.

## 변경

- 마트 주문·출고·피킹·포장이력을 읽기 전용으로 대조하는 `마트라스트마일원천Adapter`를 추가했다. 기본 원천 결속과 실제 픽업 준비를 구분하며 단일 포장이력의 복수 출고 중복 인정을 보류한다. 반환 사본에는 사용자 ID·상품명·주소·정밀 위치·원문 오류를 넣지 않는다.
- 같은 포장 완료의 인증된 명시적 재요청은 `창고포장배차재확인요청됨Event`로 배차 후속만 재확인한다. 포장 완료 사실·이력·이동·시각·원래 완료 Event는 복제하지 않는다. 최초 Event의 다른 문서/경험치 후속까지 복구하는 기능은 아니다.
- 마트 원천이 관계된 운송번호는 원본 유형·ID·화주·업무 유형이 일치해야 재사용한다. 공통 큐 변경과 마트 Service의 확정 기사 조기 반환 전에 검사한다. 충돌 시 기존 기사·상태·주소·인덱스를 덮지 않는다.
- 제품 readiness는 원천 Adapter를 재사용하지만 `MartLastMileConsumerBindingUnavailable`을 유지한다. 음식주문으로 위장하거나 실제 배차 플래그를 켜지 않는다.

## 실행 증거

직접 영향 Fast 149건과 전체 Task 10,180건이 통과했다. 실제 마트 Service·OS 인계 Coordinator·공통 큐를 SQLite에서 연결하며 소비자 준비 `true`는 시험 전용 주입이다. Mongo·Redis·음식/마트 원장 후속은 기록용 경계로 대체한다. 저장된 SQL OS 인계 Outbox 2건 확인은 실제 Mongo/Redis 소비 검증과 다르다.

- Fast: `artifacts/local/validation/20261010-180817/`, 통과 149·실패 0·건너뜀 0.
- 최종 Task: `artifacts/local/validation/20261010-180938/`, `Ssalddel.v3.5.slnx` 전체 역할 앱 build 오류 0·경고 61개, 시험 통과 10,180·실패 0·환경 의존 건너뜀 15·전체 10,195. 기존 AndroidX 패키지 버전·지도 SDK ID·Firebase/푸시 설정 경고는 남아 있으며 실제 단말 지도/푸시 성공을 뜻하지 않는다.
- 직접 연결 시험 15건: 원천 충돌의 확정 기사 유무, 유효 기존 수락 보존, 전체 포장과 미완 포장, 제품 기능 OFF·기사 미지원 보류, 실제 포장 UseCase→두 Event의 실제 handler→마트 Service→OS 인계/큐에서 최초 후속 실패와 새 DbContext 명시 재요청을 포함한다.
- 원천 Adapter 45건, 공통 큐 충돌 10건, 포장 UseCase 14건과 기존 피킹/포장·인계·운송 고유성·표본 회귀를 함께 실행했다.
- 첫 빌드는 다른 시험 프로세스의 DLL 파일 잠금으로 실패했다(`20261010-175945`). 중복 입고 참조 시험의 신규 행 EF 상태를 수정한 뒤 재실행했다(`20261010-180139`, 제품 원인 아님).
- 첫 Task(`20261010-180358`)는 14건 실패했다. 13건은 다른 동시 작업의 주문자 지원 보조 버튼과 수정 전 시험 DLL의 기대값 충돌이었다. 나머지 표본 시험은 모든 notification을 완료 Event로 세던 기대값이 새 배차 재확인 요청과 충돌했다. 해당 시험의 동시 수정은 보존했으며 현재 기준선으로 다시 build·test하여 모두 통과했다. 관련 없는 주문자 UI·시험은 이번 작업에서 수정하지 않았다.
- 승인 기획 hash 일치·검증 전후 소스/시험 14개 SHA-256 변화 0건을 확인했다. 소스 지문은 작업 지시에 남긴다. 실행 산출물은 Git 제외 로컬 근거이며 커밋된 실행 증거가 아니다.
- 최종 문서 Fast(`20261010-181952`)·로컬 Markdown 링크 1,034개·범위 `git diff --check`를 통과했다. 문서 Fast의 build/test 생략은 위 명시적인 소스 Fast·전체 Task 실행을 대체하지 않는다.

## 남은 경계와 다음 조각

마트 전용 기사 시도·운영 전표·요청 결과의 영속 저장, FDriver 수락/픽업/전달 소비 연결은 후속이다. 전표가 없어도 기사 수락과 현재 준비 상태를 검증할 수 있어야 하며, 이후 QR 즉시 픽업은 배정 기사·현재 전표·준비·중복 관문을 통과해야 한다. 비표준 과거 마트 운송번호와 실제 MySQL collation의 대소문자 경계도 운영 전 점검 대상이다. 이번 실제 기사·인증 운영 HTTP·공용 DB schema 적용·MySQL/Mongo/Redis 종단·Android·카메라·물리 인쇄·호스트 자동 복구는 미검증이다. 새 읽기 사본은 원자적인 권한 증빙이 아니며 최종 소비자는 업무 트랜잭션에서 현재 상태를 재검증해야 한다.

기존 dirty 변경과 r56 표본을 보존하며 commit·push·배포하지 않는다.
