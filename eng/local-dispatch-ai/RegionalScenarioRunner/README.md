# 지역 배차 시나리오 준비와 검토

현재 사용자 요청으로 수락한 [r50 설계](../../../docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/regional-management.implementation.r50.md)와 [작업 명세](../../../docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/regional-management.r50.work-order.json)를 따른다. 기본 실행은 아무 작업도 하지 않는다. 제품 운영 플래그와 별개인 로컬 검증 도구다.

## 준비된 자료

기존 배달 r54 자료와 14편의 공개 매장 계획/지도 관측을 해시와 stores[i] 참조로 결속한다. 명시된 중랑·동대문·광진 504건·339지점, 확인 메뉴 267기록·488항목, 주문총액296건을 보존한다. 방문 요약 메뉴는 단건에 나누지 않는다. 지점 대표 주소와 개별 방문 주소/좌표 관측은 별개다. 실제 고객 전달 위치와 기사 GPS를 가져오지 않는다. 전달점은 공개 매장점 주변의 합성 오프셋이다. 역사 가격·앱 거리로 무게·조리 시간·전달 위치를 추론하지 않는다.

저장소 루트에서 이 PC에 있는 Python 환경으로 실행한다. 다른 PC에서는 `$regionalPython`만 설치 경로에 맞춘다.

```powershell
$regionalPython = 'C:/Users/user/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
& $regionalPython eng/local-dispatch-ai/RegionalScenarioRunner/prepare_regional_dataset.py --dataset artifacts/local/delivery-history-replay-r54/dataset.json --source-root C:/Users/user/Documents/ChatGPT/배달
& $regionalPython -m unittest discover -s eng/local-dispatch-ai/RegionalScenarioRunner -p test_prepare_regional_dataset.py
```

출력은 Git 제외 `artifacts/local/local-dispatch-ai/regional-management-r50/datasets/<fingerprint>/dataset.json`과 summary.json이다. 같은 입력과 도구는 같은 바이트를 재사용하고 바뀐 입력은 새 fingerprint에 둔다. 원본·기존 영상·원장은 수정하지 않는다.

## 실행

```powershell
dotnet build eng/local-dispatch-ai/RegionalScenarioRunner/RegionalScenarioRunner.csproj
dotnet run --project eng/local-dispatch-ai/RegionalScenarioRunner --no-build -- --mock --dataset <준비된_dataset.json_절대경로>
dotnet run --project eng/local-dispatch-ai/RegionalScenarioRunner --no-build -- --actual --dataset <같은_dataset.json_절대경로>
```

실행 직전 원본29개와 준비 도구 해시를 검사한다. 영상 자료 루트는 현재 PC의 Documents/ChatGPT/배달이다. 실제 추론은 같은 소스·자료의 모의 성공 기록이 있어야 실행한다. Docker Ollama가 이미 실행 중이어야 하며 새 서버·모델을 설치하지 않는다. 고정 모델 `qwen3:1.7b`, 고정 루프백 `127.0.0.1:12681`만 호출한다. r50 대표 실제 추론은 4회(첫 구 멀티 검토1+세 구 기사 선택3), 누적 최대6회다. 실패·중단도 예약을 남기고 기존 r46~r49 예산을 재사용하지 않는다.

각 구의 부족·과잉·장기 대기·멀티 검토12개, 후속 픽업 정상·지각·근거 부족9개, HTTP/모바일 흐름6개, 총27개를 기록한다. 실제 모드에서도 대표4개 외에는 명시적 모의 응답을 사용한다. `--actual-smoke`는 같은27개 중 첫 구 멀티 검토·첫 구 기사 선택2개만 실제 모델로 재확인하는 후속 도구 검증 모드다. 같은 누적6회 예산을 공유하며 예산을 초기화하지 않는다. 현재 실행은 최초4회 뒤 실행 도구의 실패 중단·자료 해시 검사 보강을 검증하는2회로 누적6회가 소진됐다. 이후 새 추론은 별도의 명시적 범위/예산 설계가 필요하다. 수요 정책은 기본3.5건/시간을 유지하고4건/시간을 별도 민감도로 남긴다. 기사 수신을 끄거나 실제 참여 알림을 전송하지 않는다.

멀티 검토에서 선택된 조합의 출처 기록 중 하나를 HTTP 주문에 연결한다. **멀티 묶음의 원자적 수락은 검증하지 않는다.** 실제 서버 후보/수요 재검사는 별개로 수행한다. 메뉴명은 확인된 건별 역사 이름이지만 이 통합 시험의 수량1·단가9,000원은 명시적 합성값이다. 실제 메뉴 카탈로그의 현재 가격/품절 검증이 아니다.

## 판단 기록을 보는 방법

`runs/<runId>/`의 result.json이 사례별 성공/실패·실제/모의 호출·소스 해시를 요약한다. 권역별 evidence.json에는 입력 수요·기사 대기·기준/선택 조합·참여 허용 순서·4건/시간 민감도가 있다. model-request/response/exchange에는 익명 토큰과 수치만 포함한다. 후속 픽업 기록은 마지막 전달지/ETA·제외 사유를 담고 경로 포트가 합성임을 표시한다. HTTP 사례는 격리 SQLite와 제품 API client·알림 수신기·PageModel 상태를 기록한다.

사용자 검토는 `HumanReviewPending`으로 남긴다. 이후 사례 번호와 이유를 주면 입력/기대 결과·정책을 명시적으로 수정하고 같은 시험을 반복할 수 있다. 성공 사례가 자동으로 모델 가중치 학습이나 운영 정책 승인이 되지 않는다.

## 적용 범위와 다음 단말 검증

후속 픽업 옵션과 권역 검토 AI는 기본 OFF다. 현재 GPS 공간 인덱스가 발견한 후보 안에서 미래 끝점을 평가하므로 전역 미래 위치 탐색은 아니다. 조합 검토는 기하 근사이며 실제 차량 적재·전체 약속 시간 관문을 통과했다고 인증하지 않는다. 기존 3건 활성 업무 상한을 넓히지 않는다.

서버 권위의 주문 생성·추천 저장·기사 수락/수행과 별도 주문자 수령 확인은 루프백 HTTP와 시험 인증/격리 SQLite에서 실행한다. FCM과 단말 알림은 기록용 포트이고 GPS·지도는 합성이다. USB 연결 뒤 실제 APK 화면·배차 배너/소리·백그라운드 수신·네트워크 재연결·GPS·지도·실제 인증을 검증한다. Azure·운영 DB·입금·실제 알림 전송·Unity·커밋/푸시는 이 도구가 수행하지 않는다. 기존 r49의 알림 Outbox 생성/Failed 재시도 간극도 별도 후속이다.
