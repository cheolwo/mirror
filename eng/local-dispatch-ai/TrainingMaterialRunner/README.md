# 배차 학습자료 제작과 검토

[수락한 r51 설계](../../../docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/training-materials.implementation.r51.md)·[작업 명세](../../../docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/training-materials.r51.work-order.json)를 따른다. 이 도구는 현재 정책의 지도학습 **후보**를 만들며 모델 호출·가중치 변경·운영 배차를 하지 않는다. 기본 실행은 작업0이다.

## 자료와 정답의 뜻

공개 매장 위치와 확인 메뉴가 연결된 r50 자료는 관측 출처다. 가상 주문·기사·전달점·수요·조리 준비·시간은 합성 가정이며 실제 배달 당시 상황을 복원한 것이 아니다. 전달점은 공개 매장 주변의 가상 오프셋이고 현재 도로망/차량 경로 확인은 없다. 기하 근사와 테스트 경로를 실경로로 표시하지 않는다. 메뉴에서 무게·조리 시간·현행 가격을 추론하지 않는다.

정답 초안은 기존 음식 수요 참여 정책과 조합/후속 픽업 서비스를 직접 실행한 결과다. 현재 정책을 흉내내는 학습 후보이지 최적 배차나 사람의 승인을 뜻하지 않는다. 부족·과잉·장기 대기·자료 만료/누락·적격 기사 부재·단건/2건 조합·후속 픽업 허용/지각/근거 부족을 구분한다. 모델 messages에는 익명 토큰과 수치/코드만 넣고 상호·주소·좌표·메뉴·실제 ID는 넣지 않는다.

지점별 SHA-256 분할은 대략 학습70%·검증15%·평가15%다. 같은 지점·원주문·변형은 한 분할에 유지하고 조합의 다른 지점도 같은 분할에서 고른다. 동일 입력의 중복/상충은 격리한다. 이는 독립 지역/향후 실제 운행의 일반화 검증을 대신하지 않는다.

## 실행

Hongdal 저장소 루트에서 먼저 별도 도구를 빌드한다.

```powershell
dotnet build eng/local-dispatch-ai/TrainingMaterialRunner/TrainingMaterialRunner.csproj
dotnet run --project eng/local-dispatch-ai/TrainingMaterialRunner --no-build -- --prepare --dataset <r50_dataset.json_절대경로>
```

r50 dataset.json은 `artifacts/local/local-dispatch-ai/regional-management-r50/datasets/<fingerprint>/`에 있다. 준비 도구는 수락 설계·원본·소스·파생자료 해시를 확인하고 r51 비공개 자료 아래 raw.jsonl·raw.manifest.json을 생성한다. 소스가 달라지면 과거 증거를 덮어쓰지 않는다.

```powershell
$trainingPython = 'C:/Users/user/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
& $trainingPython eng/local-dispatch-ai/training_materials.py compile --raw <생성된_raw.jsonl_절대경로> --dataset <같은_r50_dataset.json_절대경로>
& $trainingPython -m unittest discover -s eng/local-dispatch-ai -p test_training_materials.py
```

## 산출물과 다음 검토

`artifacts/local/local-dispatch-ai/training-materials-r51/` 아래 생성된 번호형 `review.md`에서 상황·정책 정답 초안·근거·가정과 검토 상태를 확인한다. 후보 JSONL은 `candidates/train.jsonl`, `validation.jsonl`, `test.jsonl`이며 `ready/`의 세 파일은 승인 전 **모두0행**이다. 중복/상충은 quarantine.jsonl과 summary.json에 보존한다. 해시가 맞지 않거나 계약·분할·보호 필드가 잘못되면 컴파일을 중단한다.

현재 후보의 상태는 Pending이다. 검토자가 선택·보류 판단을 확인한 뒤 별도 검토 원장과 입력/정답/출처 해시를 연결해야 학습용으로 승인할 수 있다. 이 단계에는 승인 자동화·학습기·Ollama 모델 교체가 없다. 후보 수를 실제 승인 정답 수나 모델 성능으로 표현하지 않는다.

이후 소규모 추가 학습을 하더라도 학습 전 모델·기존 엔진·학습 후 모델을 고정 평가자료에서 비교한다. 허용되지 않은 후보 선택·현재 상태/동의 위반·잘못된 응답·보류 누락을 먼저 확인하고, 실제 운행자료가 쌓인 뒤 지각·대기·업무 기회 편중을 별도로 평가한다. 단말/FCM·실제 지도 경로·운영 DB·외부 입금·Unity·Azure·커밋/푸시는 포함하지 않는다.

## 1차 제작 결과

[r51 검증 기록](../../../docs/Changes/2026-10-11-dispatch-training-materials-r51.md)에 따라 원시4,041개에서 동일 입력9개를 격리하고 후보4,032개(학습2,784·검증856·평가392), 한국어 검토24개를 준비했다. 후속 픽업9개는 모두 격리되어 후보에 포함되지 않는다. 두 주문 검토는 두 건 묶음489개·단건 선택15개이며 합성 요청 순서로 동점 선택의 입력 근거를 보존한다. 모든 답안은 사람 검토 대기다.
