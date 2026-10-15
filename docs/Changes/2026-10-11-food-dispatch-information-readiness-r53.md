# 음식 배차 정보 준비 상태 r53

기존 수집 판단은 주변 주문과 기사 수를 읽어도 원주문의 현재 상태·현재 조리 차수·전달 좌표를 한 묶음의 준비 근거로 확인하지 않았다. 이번에는 기존 원장과 정책을 활용해 확인된 정보와 부족·만료 정보를 구분하고, 정보가 충분한 경우에만 짧은 추가 수집을 허용한다. 사용자 요청 1번의 로컬 서버 보완이다.

[승인 설계](../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/information-readiness.implementation.r53.md)와 [작업 명세](../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/information-readiness.r53.work-order.json)에 범위·해시를 고정했다. 기준선은 `08931c2371f29de615f4a4f133b535e5df76bf7f`다.

## 동작

- 여섯 가지 수집 정보: 원주문, 현재 조리, 픽업/전달 좌표, 주문 풀 조회, 수요, 기사 여력. 각각 `Current / Missing / Stale / NotApplicable`과 관측시각·선택 판본·고정 사유로 반환한다.
- 정보가 현재여도 0명·추가 주문 0건 등 기다릴 가치가 없는 상황은 기존 정책이 별도로 즉시 진행을 결정한다. 조리 예정시각이 없다는 것만으로 누락 판정하지 않는다.
- 원주문을 비동기 작업 뒤 다시 확인하고 상태·연결·판본·현재 조리 근거가 달라지거나 관측이 만료되면 추가 대기를 중단한다. 큐 자체의 Reload·fingerprint·최초 생성시각 재확인도 유지한다.
- 준비 상태가 없는/부족한 응답의 `Collect`를 전환 서비스가 수용하지 않는다. 큐 재조회 뒤에도 같은 유효시간으로 근거를 다시 계산하므로, 응답 당시 현재였어도 재조회 중 만료되면 기다리지 않는다. 기존 후보 선정·거리/일정·요금 동결·수락 관문은 유지하며, 기다리지 않는다는 것이 즉시 배차 확정을 뜻하지 않는다.
- 재조리 차수에는 이전 차수의 미래 조리 예정시각을 대기 상한으로 재사용하지 않는다. 현재 조리 이력과 선언된 차수/시각이 모순되면 준비 근거 부족으로 남긴다.
- 고객 전달 확약·비용 동의·실제 기사 일정/적재·현재 기사 견적·플랫폼 확정 재원을 포함한 전체 조율 준비는 별도로 반환한다. 현재 수집 단계에서 확인되지 않은 자료는 부족으로 남긴다. 상품 주문액·결제 승인·희망시각·Simulation 동의·기간 예비금으로 재원을 만들지 않는다.

## 검증 상태

- Fast `artifacts/local/validation/20261011-202839`: 전체 solution 빌드 오류 0·경고 152, 집중 시험 217개 통과·실패 0·건너뜀 0.
- 최초 Fast `20261011-202453`에서 시험용 조회 횟수 집계 오류 1개를 발견했다. 선택 콜백이 없으면 카운터 증가도 생략되던 시험 포트를 수정했고, 최종 Fast에서 해당 반례와 관련 시험 전체를 통과했다. 최초 기록은 보존한다.
- Task `artifacts/local/validation/20261011-203137`: 전체 solution 빌드 오류 0·경고 61, 서버 시험 10,832개 통과·실패 0·실제 Mongo 환경 의존 15개 건너뜀. 집중 시험 217개는 이 전체 시험에도 포함되므로 두 수를 별개의 총 통과 수로 합산하지 않는다. 빌드 경고에는 기존 Android 패키지 제약·회원 푸시 미설정 등이 남아 있다.
- 최초 기준선 HEAD·고정 설계 해시·빈 index를 유지하고 변경 19개 경로가 작업 명세 안에 있음을 확인한다. 최종 [범위 점검 기록](../../artifacts/local/food-dispatch-information-r53/scope-audit.json)에 경로/해시·문서 링크·차이 검사를 보존한다. 로컬 근거는 Git/공유에 자동 포함하지 않는다.

## Docker Mongo 후속 검증

사용자는 기존 Docker MongoDB 이미지를 활용해 미실행 시험도 확인하도록 요청했다. 이미지 부재가 아니라 시험용 연결이 지정되지 않아 건너뛴 상태였으며, 기존 `mongo:8.0` 이미지 ID `sha256:45b422ba19c0f028a917ea43746ab8e8cdbdaaeb50d2e6e634a17c59666e0dbb`를 다운로드 없이 재사용했다. 실제 서버 버전은 `8.0.26`이다.

- 기존 앱 Mongo 컨테이너·DB·볼륨과 분리된 standalone/replica set 두 인스턴스를 PC 루프백 임의 포트로 실행한다. DB 파일은 tmpfs에만 두고 자식 검증 프로세스에 시험 환경변수를 전달한다. 부모 프로세스의 MySQL/Redis 시험 연결과 전체 인프라 필수 옵션은 모두 미설정임을 값 노출 없이 확인했다.
- 기존 네 환경변수는 `SSALDDEL_ORDER_LEDGER_TEST_MONGO`, `SSALDDEL_ORDER_LEDGER_STANDALONE_TEST_MONGO`, `SSALDDEL_PRIVACY_SUPPORT_TEST_MONGO`, `SSALDDEL_RETURN_AREA_MONGO_TEST_CONNECTION`이다. 일반 Mongo 문서 왕복에는 기존 `SSALDDEL_TEST_MONGODB_CONNECTION/DATABASE`도 연결했다. 제품 설정이나 시험 코드를 바꾸지 않았다.
- 최초 Fast `20261011-205501`은 23개 통과·1개 실패·건너뜀 0이었다. 512MiB tmpfs의 실제 여유 공간이 Mongo 인덱스 생성 최소 500MiB에 못 미친 환경 오류다. tmpfs 용량 상한을 2GiB로 조정했고 최초 기록과 정리 결과는 보존했다.
- 최종 Mongo Fast `artifacts/local/validation/20261011-205545`는 24개 통과·실패 0·건너뜀 0이다. 이전 미실행 15개는 Theory가 접힌 표시였으며, 연결 후 23개로 펼쳐졌다. 여기에 일반 Mongo 문서 저장/조회/삭제 1개가 포함된다. 실제 원주문 관계 트랜잭션·동시 변경/CAS·standalone 차단·보호지원/판매자 암호문·복귀설정 재시도를 검증했다.
- 최종 Mongo Task `artifacts/local/validation/20261011-205559`는 전체 10,855개 통과·실패 0·건너뜀 0이다. solution 빌드 오류 0·기존 푸시 미설정 경고 6개다. Fast 24개는 전체 Task에도 포함되므로 합산하지 않는다. MySQL/Redis 실환경 시험을 새로 활성화한 결과는 아니다.
- 두 Mongo 인스턴스 모두 시험 뒤 `admin/config/local` 외 DB 잔존 0을 확인하고, 실행 라벨·이미지 ID를 다시 검사한 시험 컨테이너만 중지·삭제했다. `testDatabasesCleaned=true`, `cleanedUp=true`, 각 `removed=true`와 Docker 목록 부재를 확인했다. 기존 앱 DB·볼륨은 사용/변경하지 않았다.
- [실행·정리 근거](../../artifacts/local/food-dispatch-information-r53/mongo-20261011T115538Z-11a42c0d/summary.json)와 [로컬 재실행 도구](../../artifacts/local/food-dispatch-information-r53/run_mongo_validation.py)는 비공개 로컬 산출물이다. 이 도구는 기존 이미지가 필요하며 새 이미지 다운로드나 제품 운영 연결을 자동 수행하지 않는다. 환경변수는 자식 프로세스에만 설정하므로 일반 검증 명령이 자동으로 Mongo 시험을 활성화하지는 않는다. 같은 실검증은 이 도구로 다시 실행한다.

## 적용 한계

새 DB 테이블·API·화면·요금 산식·다건 최적화는 추가하지 않는다. 기존 기능 기본 OFF를 유지한다. AI에는 기존 익명 집계만 제공하며 주문/주소/연락처 원문이나 전체 준비 근거를 전송하지 않는다. 실제 모델 호출·경로/기상 API 추가 호출·학습·외부 알림·APK 설치/단말 실행·Azure·Unity·운영 활성화·커밋/푸시는 수행하지 않았다. solution 빌드에는 소비 앱 컴파일이 포함되지만 단말 검증 증거로 삼지 않는다. 기존 수요 서비스가 소유한 판정 투영 복구는 기존 경계 그대로다. 로컬 시험의 준비 상태는 실제 고객 약속 준수나 운영 수입 개선 증거가 아니다. r51/r52 자료는 이전 정책의 검토 초안으로 보존하며 이번 판본의 학습 승인 자료로 자동 승격하지 않는다.
