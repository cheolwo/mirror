# 배차 학습자료 제작 r51

상태: 현재 사용자 요청 `학습 자료 제작을 좀 시작`으로 이 범위 수락. 구현 시작 때 SHA-256으로 동결한다. 기준 HEAD는 `86f3eae59328cfe5ef7ec9e3aef48ae351cf54d9`이며 기존 dirty 작업은 보존한다.

## 목적과 범위

기존 r50 중랑·동대문·광진 504건·339지점/확인 메뉴488항목을 실제 관측의 출처로 재사용하고 가상 주문·전달지·기사·시간·수요를 결합한 지도학습 후보를 제작한다. 모델 호출·가중치 학습·운영 배차·외부 알림·USB·Unity·Azure·APK·커밋/푸시는 실행하지 않는다. 제품 서버·앱·기존 r44~r50 코드/증거·자료 원본은 수정하지 않는다.

## 정답 초안

새 TrainingMaterialRunner가 현재 음식배달수요참여Policy 및 음식권역배차관리Service/기존 음식멀티배차조합Service를 AI Disabled와 거부형 네트워크 경계로 직접 실행한다. 후속 픽업은 r50의 제품 정책/서비스를 실행하는 공개 테스트 fixture를 제한된 대표 사례에서 재사용한다. 새 정책을 Python으로 복제하여 정답으로 삼지 않는다. 레이블은 현재 정책의 증류용 초안이며 최적 배차·실제 결과·사람 승인으로 인증하지 않는다. 모델 답변을 정답으로 재사용하지 않는다.

504개 원본 주문별 부족·과잉·장기대기·관측만료·관측누락·적격기사부재 수요 사례, 단건/2건 조합 사례를 생성한다. 단계마다 익명 토큰을 결정적으로 섞어 정답이 항상 첫 번째가 되는 위치 지름길을 줄인다. 메뉴명/가격은 비공개 출처 대장에만 두고 실제 무게·조리 시간·현재 가격·고객 위치를 추론하지 않는다. 기본3.5건/시간은 보존한다. 결과는 정책 버전/소스 해시·가정·선택/보류 근거와 함께 기록한다.

## 합성 위치와 한계

기존 공개 매장 좌표와 합성 전달 오프셋은 실제 고객 주소가 아니다. 현재 도로망/실제 차량 경로는 확보되지 않았으므로 이를 도로 위 검증점이나 실경로로 표시하지 않는다. 이번 공간 입력은 명시적 기하 근사이며 현재 정책 반응을 학습하는 연습 자료다. 실제 도로 연결·차량별 이동 시간과 실제 운영 성적은 후속이며 지금 완성했다고 보고하지 않는다. 실제 주소·좌표·이름·메뉴·전화·고객·원본 UI는 모델 messages에 포함하지 않는다.

## 자료 계약과 분리

raw.jsonl 한 행: schemaVersion=1, caseId, taskCode, scenarioFamily, sourceRecordIds[], sourceBranchIds[], split(train/validation/test), input(익명 수치/코드), target(정책 출력), teacher{kind=CurrentPolicyDraft,policyVersion,optimalityProven=false}, provenance{synthetic=true,routeBasis,realCustomerLocationsUsed=0}, review{status=Pending,eligibleForTraining=false}. 소스 ID/상세 비공개 출처는 messages 바깥에만 둔다. raw.manifest.json은 datasetSha256,sourceHashes,rawSha256,caseCount,modelCalls=0,weightsTrained=false와 형식 판본을 연결한다.

지점 stable ID의 SHA-256 기반 70/15/15 배정으로 동일 지점의 주문·변형을 한 묶음에 유지한다. 조합에 쓰는 다른 지점도 같은 split에서만 선택한다. 따라서 모든 원 주문·지점·변형이 학습/평가 양쪽에 섞이지 않는다. 지역/시간대 실측 일반화 증거는 아니다. 동일 messages 입력의 중복/상충 레이블은 격리하고 학습/평가 중복을 허용하지 않는다. 고정 seed·소스 해시·파생 해시와 검토 이력을 보존한다.

새 Python compile 도구는 raw 계약·해시·개인정보 whitelist·분할 누수·중복을 검사하고 candidates/train.jsonl·validation.jsonl·test.jsonl, quarantine.jsonl, summary.json, 사람이 읽는 번호형 review.md를 Git 제외 artifacts/local에 만든다. 모든 초안은 Pending, 학습 활성 자료는0이다. 승인 입력 파일은 제공하지 않고 사람이 검토한 경우만 case/입력/정답/출처 해시에 연결된 별도 검토 원장으로 추가한다. 이번 단계에는 자동 승인이나 학습기 연결이 없다. candidates를 ready 데이터로 이름만 바꿔 승격하지 않는다.

## 허용 경로와 검증

허용 쓰기: eng/local-dispatch-ai/TrainingMaterialRunner/**, eng/local-dispatch-ai/training_materials.py, eng/local-dispatch-ai/test_training_materials.py, docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/training-materials*, 같은 PLAN README, docs/AI/CURRENT_WORK.md, docs/AI/PLANNING.md, docs/Changes/README.md, docs/Changes/2026-10-11-dispatch-training-materials-r51.md, artifacts/local/local-dispatch-ai/training-materials-r51/**.

고의 변조·보호 정보 삽입·분할 오염·중복/충돌·잘못된 토큰·허위 승인 등 의미 있는 Python 반례를 시험한다. 별도 .NET 도구 빌드와 생성 결과 재조회, 관련 Fast와 Task를 수행한다. 원본/기존 dirty 범위 밖 불변·JSONL 재파싱·분할/라벨 분포·해시·Git 제외·문서 링크를 독립 대조한다. 모델 호출0·가중치 변경0·사람 승인0이며 자료 제작 완료와 학습 준비 승인/운영 개선은 별개다.
