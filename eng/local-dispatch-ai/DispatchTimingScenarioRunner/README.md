# 배차 시점 학습 후보 r52

[승인 설계](../../../docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/batch-timing.implementation.r52.md)와 [작업 명세](../../../docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/batch-timing.r52.work-order.json)에 따른 별도 로컬 도구다. 기존 r50·r51 생성기와 자료를 수정하지 않는다.

실제 배달 기록 504건·339지점은 익명 출처와 지점별 분리 근거로만 재사용한다. 주문 도착 시각·대기 시간·수요·기사 여력·합류 가능성은 고정 seed로 만든 합성 조건이다. 상호·주소·좌표·메뉴·실제 ID는 모델 messages에 넣지 않는다. 과거 요금을 현재 요금으로 바꾸지 않으며 기사 평균 단가 4,000원 최적화 자료도 아니다.

순수 `FoodDispatchTimingPolicy`를 직접 호출해 수집/조합 발견/수집 종료/긴급/희박 수요/자료 미확인/기사 부재/재추천 8유형의 초안 4,032개를 만든다. `Collect`의 재검토 기한은 최초 큐와 고정 상한 안에 둔다. 답안은 `CurrentPolicyDraft / Pending`이며 최적해·사람 승인·실제 운영 결과를 뜻하지 않는다.

이 자료의 messages는 **순수 정책 검토 초안 형식**이다. 실제 `OllamaFoodDispatchTimingAI`의 입력·actionCode 한 개 응답 형식과 동일하지 않다. 정책 사유와 기한 검토를 보존하며, 실제 모델을 학습할 때 사용할 변환과 사람 승인은 후속 단계다. 현재 자료가 운영 프롬프트와 호환된다고 보고하지 않는다.

## 실행

기본 실행에는 생성·모델 호출·배차 효과가 없다. 아래 명령은 명시적으로 자료 제작을 요청할 때만 사용한다. 빌드는 다른 프로젝트 빌드와 동시에 실행하지 않는다.

```powershell
dotnet build eng/local-dispatch-ai/DispatchTimingScenarioRunner/DispatchTimingScenarioRunner.csproj
dotnet run --project eng/local-dispatch-ai/DispatchTimingScenarioRunner --no-build -- --prepare --dataset <r50 dataset.json의 절대 경로>

$taskPython = 'C:\Users\user\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
& $taskPython eng/local-dispatch-ai/dispatch_timing_materials.py compile --raw <생성된 raw.jsonl 절대 경로> --dataset <r50 dataset.json 절대 경로>
& $taskPython -m unittest discover -s eng/local-dispatch-ai -p test_dispatch_timing_materials.py
```

출력은 Git 제외된 `artifacts/local/local-dispatch-ai/dispatch-timing-r52/`의 원본/도구 해시별 새 경로에 보관한다. 같은 바이트는 재사용하고 기존 파일이 달라졌으면 덮어쓰지 않는다. 출처·디자인·정책·도구·원본의 해시를 생성 전후 대조한다. 현재 검증이 과거 r51 산출물의 상태를 자동 승격하지 않는다.

## 결과 읽기

- `raw/<fingerprint>/raw.jsonl`, `raw.manifest.json`: 합성 조건과 실제 순수 정책 답안, 출처·도구 해시.
- `compiled/<fingerprint>/candidates/{train,validation,test}.jsonl`: 익명 수치·고정 코드의 모델 messages와 별도 감사 정보. 같은 지점은 같은 묶음에만 둔다.
- `quarantine.jsonl`: 같은 입력과 서로 다른 답안, 지점 분리 경계를 넘는 같은 입력을 관련 사례 모두 격리한다.
- `review.md`, `summary.json`: 최대 24개 대표 사례와 수량·행동·검토 대기 상태.
- `ready/{train,validation,test}.jsonl`: 빈 파일. 자료 제작 도구는 사람을 대신해 승인하지 않는다.

실제 모델 추론·가중치 학습·모바일/알림·운영 DB/배차·결제·Azure·Unity는 실행하지 않는다. 수집 판단이 단건/2건 공동 수락이나 적재·실도로·모든 약속 시간 검증을 완료했다는 뜻도 아니다.
