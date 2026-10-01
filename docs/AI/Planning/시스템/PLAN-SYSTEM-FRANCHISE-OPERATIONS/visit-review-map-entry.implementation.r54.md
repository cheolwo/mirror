[구현 수용·검증 · 방문 자료·Unity 진입 · PLAN-SYSTEM-FRANCHISE-OPERATIONS · r54]

# 한 구간 지도를 직접 준비하고 방문 검토로 이어지는 진입

- 승인 근거: 2026-09-26 사용자 “진행해 봐 구현을 진행을 해 줘 그냥.” 같은 개발 스레드에서 아래 좁은 후속 범위를 `Approved / ReadyToDispatch / Accepted`로 수용한다.
- 선행: [r53](visit-location-review-unity-consumer.implementation.r53.md), SHA-256 `DFD158F422E9E3AEE1156294AD63C3C908A9D0ED5F472E2D3D7B774C5C875B74`.
- 상태: `Implemented_PresentationObservedWithConsoleError_ActualVisitsPending`. 자동 시험과 짧은 격리 Play에서 재생 기능을 확인했지만 Console 오류0 관문과 실제 방문 위치 확정은 미완료다. 새 운영 WI·권위·Scene·E 승격 없음.

## 범위와 우선순위

1. 실제7건의 위치 근거를 기존 로컬 자료에서 다시 대조한다. 역사 경계 후보·메뉴 근거·좌표 사실확정을 분리하며 미확정 기록을 바꾸지 않는다.
2. r53의 “이미 읽힌 View 필요” 의존도를 줄인다. 같은 메뉴에서 동결된 면목제3·8동 한 bundle을 검증하고 canonical `SimulationWorldShell` 안에 임시 검토 View를 명시적으로 준비한다. 기존 공개/API decoder·Interpreter·Projection·View를 가능한 범위에서 재사용한다.
3. 건물/도로뿐 아니라 r7의 기존6종 비공개 overlay를 보존한다. 전체30동 생성·다른 동 다운로드·현재자료 대체를 하지 않는다. 검증 실패 시 마지막 정상 View를 보존하고 이 창이 만든 객체만 정리한다.
4. 실제 기록은 계속 검사/보류하고, 위치 확정과 무관한 재생 기능은 명시적인 합성 시험 입력으로 검증한다. 실제 방문을 임의 위치로 옮기거나 합성 표본을 실제 기록이라고 표현하지 않는다.
5. 좁은 Play/Game View 검증을 시도한다. 합성 순서 재생/정지/방문 선택/정리와 실제 입력 거절을 구분하고 Console·Scene hash를 별도 기록한다. 실제7건 재생·동간 조립·DB/보내기 완료로 확대하지 않는다.

## 정확한 쓰기 경로와 담당

Hongdal 통합 담당:

- 이 r54, 해당 PLAN의 `README.md`, `docs/AI/PLANNING.md`, `docs/AI/CURRENT_WORK.md`.
- 비공개 검증 산출물 `artifacts/local/validation/visit-review-map-entry-r54/`.

별도 Unity 작업트리(`C:/Users/user/ssalddel`은 로컬 위치이며 배포 계약 아님):

- 지도 진입 담당: `Assets/Ssalddel/Editor/방문순서재생검토진입.cs` (기존 `.meta` 유지).
- 시험 담당: `Assets/Ssalddel/Tests/EditMode/방문검토지도진입Tests.cs` 및 새 `.meta`.
- 월드·공간·배치 담당: `Assets/Ssalddel/Editor/방문검토지도진입검증.cs` 및 새 `.meta`; 검증 산출물 `artifacts/visit-review-map-entry-r54/`; 대표 PNG·기록은 `Documentation/Changes/2026-09-26-visit-review-map-entry-r54/`.

기존 대형 지도 loader/View/Models·Scene·다른 dirty 파일은 수정하지 않는다. 모든 Unity 실행은 통합이 점유를 확인하고 순차 지정한다. View 캡처 실행은 월드·공간·배치 담당만 수행한다. 자동 생성된 설정 변경은 시작 hash와 대조하여 이번 실행이 만든 부분만 복구한다. commit·push·배포 없음.

## 검증 계약

- 검증한 동일 파일 바이트의 manifest/전체tiles/display overlay → 기존 Interpreter → 기존 전체 overlay Projection → focus 검증 → 임시 View 조립 순서다. Scene 객체는 파일 검증 성공 후에만 만든다.
- 정확한 hash와 선택동·12타일·건물2700·도로291·선택 건물86/도로9 및 기존 overlay 판본을 보존한다. source/frame/count 불일치와 누락은 실패이며 합성 fallback 없음.
- Scene 준비는 명시적 버튼/API 호출만 허용한다. canonical Scene 이외에서는 거절하며 자동 Scene 열기/저장은 제품 진입에 넣지 않는다. 새 host는 `DontSave`이고 외부 View/원장·기록은 수정하지 않는다.
- 준비 단계 실패·반복 호출·다른 지도 선택·Clear/창 종료·Play 전환의 자료 수명을 시험한다. 원문 기록·경로·token을 Scene/PlayerPrefs/Save/Replay에 저장하지 않는다.
- E1/E2는 기존 관찰 의미·계약 재사용, E3는 import·파일·객체 회귀, E4는 원래 지도와 중립 marker, E5/E6/E7 관련 실행·화면은 실제 수행한 범위만 기록한다. `evidenceStageClaimed=null`이며 별도 게임 WI의 E를 승격하지 않는다.
- 합성 fixture는 시험/명시적 검증 전용이다. 실제 입력 거절을 합성 성공으로 대신하지 않는다. Console의 기존 오류도 숨기지 않는다.
- 화면 검증은 별도 graphics Editor 프로세스에서 canonical Scene의 기존 root를 메모리에서만 비활성화한 **격리된 표현 시험**이다. 운영 Controller의 외부 호출을 막고 종료 후 저장하지 않은 Scene을 원본에서 다시 연다. 따라서 이 결과는 기존 NPC·운영 Controller가 동시에 계속 실행된다는 증거가 아니며, 기존 전체 월드의 Console 문제가 해결됐다는 뜻도 아니다.

## 실행 결과

### 기존 실제 방문 근거 재대조

- 실제7건의 후보 좌표는 현재 사가정250m focus 안에 **0건**이다. 역사 경계 후보는 면목7동1건·면목4동2건·이문1동3건·범위 밖1건이다. 임의로 focus 안으로 옮기거나6건만 정상 순서인 것처럼 재생하지 않는다.
- 기존 편집 route의 상호/주소7건과 지도점 사본의 상호/좌표7건은 export와 일치한다. route에 보관된 주소 출처 링크5건은 추가 검토 후보이나 이번에 외부 재조회/유효성 확인을 한 것은 아니다. sourceUrls의 메뉴 근거와 분리했다.
- 독립 geocode 응답·조회시각·현행 경계·검토된 위치 결속은 아직 없다. 동일 좌표의 사본 두 개가 있다는 사실은 독립 검증이 아니다. 상태는 전부 `PendingReview`를 유지한다.
- 비공개 기록: `artifacts/local/validation/visit-review-map-entry-r54/location-source-audit.json`. 원본별 hash·집계·순번별 결손만 남겼으며 공개 문서에 상호·주소·정밀 좌표를 복사하지 않았다. 원본 수정·DB·외부 수집 없음.

### 코드·시험·화면

구현 내용:

- 기존 `Ssalddel/검토/방문 순서 재생` 메뉴에 `동결 선택동 지도 직접 준비 · 현재 Scene만 사용`을 연결했다. 별도 지도 생성 메뉴를 먼저 실행할 필요가 없다.
- index·completion·선택 bundle·focus의 고정 hash를 검사한 동일 바이트에서 전체 12타일과 기존6종 overlay를 해석한다. 선택 범위만 남기고 원본 지도 목록을 잘라내지 않는다.
- 준비 중인 임시 객체는 비활성 상태로 조립하며, 성공한 뒤에만 이전 소유 객체와 교체한다. 동일 입력의 반복 준비는 정상 지도·Presenter를 재사용한다. `ClearReview`·창 종료·Play 전환은 이 창의 임시 객체만 제거한다.
- r54 신규 EditMode 시험은12개 사례다. 첫 실제 컴파일에서 Unity6000.5의 폐기 API `GetInstanceID()` 사용을 발견했고, 시험의 root 비교를 기존 프로젝트와 같은 `GetEntityId()` 기반으로 수정했다.

| 검증 | 결과와 범위 |
| --- | --- |
| Unity 실제 import·EditMode | Unity `6000.5.6f1`, 최종 **70/70** 통과·실패0·건너뜀0. r54 신규12+r53 14+지도 회귀44. 실제 동결 파일·6종 overlay·실제7건 준비 거절 포함. |
| 시험 준비 수정 | 첫 실행은69/70으로, Test Runner의 미저장 untitled Scene에 Additive 새 Scene을 만들 수 없어 시험 준비가 실패했다. 이미 열린 noncanonical Scene을 그대로 거절 대상으로 사용하는 방식으로 고치고 전체70개를 재실행했다. |
| 시험 기록 | 별도 Unity `artifacts/visit-review-map-entry-r54/editmode-final.xml`, SHA-256 `F34E437522C56004B34ABB6E6BE8C5E8983A96405DC8F976180742457A374FC1`. 이전 실패 XML도 별도로 보존했다. |
| Scene 보존 | 저장 `SimulationWorldShell.unity` SHA-256 `C31167703C4A7683DA374E237CBA3C9584A5F1DE4B5FB5746939A3632635AC65`가 시험 전후 동일하다. |
| 실제 Play/Game View | 최종 `run-02`, 2026-09-26 20:08:09~20:08:27 KST. 검증 세션 약18.88초는 Scene 로드·Play 전환을 포함한다. 기존 root5개를 비저장 격리하고 실제7건 거절→합성 `A→B→A` 3방문 재생→실제 시계 이동→정지 유지→3번 선택→표시 제거→소유 host 제거를 확인했다. 1280×720 PNG4장. |
| 최종 화면 검증 판정 | `scenarioChecksPassed=true`, `profilePassed=false`. Console 시작0·종료1, 실행 구간 Error/Exception/Assert1·Warning9. 기존 `SimulationReplayHashMismatch`가 남아 오류0 관문은 미통과다. 원본 Console을 지우거나 범위 밖 Runtime 코드를 수정하지 않았다. |
| 실행기 자체 수리 | 첫 `run-01`은 Console 종료2였다. 이 중 Game View 크기 삭제 index 오류는 이번 실행기에서 전체 index 사용과 실제 항목 수 감소 검사로 수정했다. 재실행에서 해당 오류가 사라졌다. 첫 manifest의 `gameViewSettingsRestored=true`는 index 오류를 잡지 못한 잘못된 판정이므로 최종 결과로 사용하지 않는다. |
| 캡처 기록 | 별도 Unity `artifacts/visit-review-map-entry-r54/run-02/capture-manifest.json`, SHA-256 `C33570B7F47BB29444FCCF3D6922B253F35043245004AE951902301D6671F624`. 대표 PNG4장과 manifest는 `Documentation/Changes/2026-09-26-visit-review-map-entry-r54/`에 보관한다. 실제 화면을 사용한 **합성 검토 표본**이지 확정 UI·실제 배달 재생·NPC 연속 운영 증거가 아니다. |
| 종료와 보존 | 최종 Scene 미저장·hash 동일·재열기 후 clean·소유 host 제거·Game View 크기 항목 수 복구를 확인했다. `ProjectSettings.asset` SHA-256 `1173FB0729CBD9720435656B7B3EB0DDAF11F43010D4D02E641897698ED7D18D`도 시작값과 같아 추가 설정 수정을 하지 않았다. |

이번 합성 재생 검증은 실제7건의 재생 완료가 아니다. 공유 패키지958개·Python21/24개는 r53의 선행 결과이며 이번 변경에서 다시 실행한 시험으로 세지 않는다.

### 사용 진입과 다음 순서

1. 기존 canonical `SimulationWorldShell`을 연 상태에서 `Ssalddel/검토/방문 순서 재생`을 연다. 제품 진입이 Scene을 자동 교체하거나 저장하지 않는다.
2. Hongdal 폴더를 선택하고 `동결 선택동 지도 직접 준비 · 현재 Scene만 사용`을 누른다. 검증된 완전 지도와250m 참조 범위를 임시로 조립한다.
3. 방문 JSON과 별도 검토된 위치 근거를 선택해 준비 검사한다. 통과한 자료만 적용·재생·정지·방문 번호 선택이 가능하다. 현재 실제7건은 여기에서 계속 보류된다.
4. 다음 실제 기록 작업은 **그 방문이 속하는 지역의 위치 근거 보완**이 먼저다. 사가정으로 옮기거나 일부만 잘라낸 순서로 대체하지 않는다. 그 뒤 필요한 인접 동 조립, DB 보관·명시적 전송 순서로 진행한다.

문서4경로의 Fast/Task·diff 검사와 r54 상호 링크 확인을 통과했다. 문서 전용으로 분류되어 해당 도구의 제품 build/test는 생략되며 위 Unity70개 시험을 별도로 실행했다. 새 C#·meta의 짝/고유 GUID와 trailing whitespace0을 확인하고 최종 PNG4장의 hash를 독립 대조했다. 기존 전역 E 책임 생성 지도 차이를 해결한 것은 아니다. Windows 빌드·수동 UI 입력·실제 HTTP/DB·장시간/NPC 연속 운영은 미실행이며 commit·push·배포 없음.

### 디오라마 규칙 점검

기존 후보 `cross-station-fallback-forbidden`, `missing-coverage-remains-explicit`, `source-simulation-presentation-separated`, `game-view-is-presentation-evidence-only`의 경계를 유지했다. 사가정 범위 밖의 실제 방문을 임의 이동하지 않은 점과 합성 표현 시험을 분리한 점은 해당 후보를 지지한다. 현행 위치·경계 결손은 반례가 아니라 명시적으로 남긴 검증 한계다. 공통 규칙 적용·승격이나 E 승격은 하지 않는다. **새 디오라마 규칙 후보 없음**.
