# 음식 배달 관찰 표본의 거리·동·구 표현 수명 r46

- 작성일: 2026-09-26
- 승인 근거: r45의 첫 City 음식 배달 표본 제안 뒤 사용자 `진행`.
- 기획: [r45](living-world-semantic-streaming.proposal.r45.md), SHA256 `604e5bda4c7894774ff1cf27f8e173fab39b47d8c1868c32045974a02228d3aa`.
- 인계 상태: `Approved / ReadyToDispatch / Accepted` — 같은 스레드에서 아래 읽기 전용 표현 범위만 수용. commit/push·배포·운영 명령·기존 중지 작업 재개 승인이 아니다.
- 상태: 좁은 구현·자동 시험·명시적 표본의 짧은 Play/Game View 확인 완료. 기존 Console 오류와 실제 서버 연결은 미해결이며 E 단계 승격을 주장하지 않는다.

## 좁힌 범위와 소유 경로

기존 사가정 `OperationalOsWorldRoot`에서 성공한 음식 배달 상태 사본을 거리 상세·동 요약·구 요약으로 읽는다. 구 요약은 관찰 중인 단일 지역 표본만 집계하며 중랑구 전체 통계가 아니다. 행정 경계·도로·업무 장소를 새로 생성하지 않는다. 다른 OS의 표현과 기존 배치·지구본 관문은 보존한다.

- Hongdal: `Ssalddel.Unity/Runtime/WorldProjection/생활관찰표현Session.cs` 및 `.meta`, `Ssalddel.Unity.Tests/생활관찰표현SessionTests.cs`.
- 별도 Unity: `Assets/Ssalddel/Presentation/World/생활관찰표현Layer.cs` 및 `.meta`, 기존 `운영관찰WorldView.cs`·`다중Os생명주기검증Layer.cs`의 OS별 표시/해제 경계, `Assets/Ssalddel/Bootstrap/운영관찰WorldController.cs`의 runtime-only 조립, 필요 시 `사가정운영디오라마View.cs`의 관찰 카메라 단계 진입만.
- 별도 Unity 검증: `Assets/Ssalddel/Tests/EditMode/생활관찰표현Tests.cs` 및 `.meta`, `Assets/Ssalddel/Editor/생활관찰표현검증.cs` 및 `.meta`; 해당 검증이 생성하는 `Documentation/Changes/2026-09-26-living-world-streaming/` 증거. 기존 `다중Os생명주기검증Tests.cs`의 Controller 결속 시험은 실제 UTC TTL 도입 후 과거 고정 Epoch가 만료되는 실패를 재현하여, 해당 한 시험의 입력 기준 시각만 현재 UTC로 바꾼다.
- 문서: 이 r46·기획 README·PLANNING·CURRENT_WORK. 기존 다른 변경을 stage·정리하지 않는다.

## 구현 계약

서버 상태·업무 revision·cursor의 소유권은 바뀌지 않는다. `WorldController`를 확대/축소로 disable하지 않는다. 상태 사본은 메모리에만 남기며 만료·명시적 Clear에서 제거한다. 동일 업무의 복수 표현 객체를 여러 주문으로 집계하지 않고 자료원/실행/업무 ID를 구분한다.

초기 검증 Profile은 안정 대기 0.5초·거리 상세 해제 유예 5초·최대 음식 사본128개로 제한한다. 전국 공통값이나 성능 보장이 아닌 이 표본의 조정 가능한 시작값이다. 기존 지구본 관문의 0.3초 기본값을 변경하지 않는다. 단위 시험에는 가짜 시계를 사용한다.

현재 표본은 새 API/네트워크 타일 수집 없이 이미 인증·해석된 placement 사본을 소비한다. 따라서 이번 로딩은 음식 상세 GameObject/Material의 재조립·해제이며 지형 전체·텍스처·전국 네트워크 스트리밍 완성이 아니다. 낮은 revision·자료 결손·만료·원본 변경 금지와 외부 응답 권위는 기존 interpreter 경계를 유지한다.

## 표현 전용 수직 검증 명세

새 권위 변화 WI·게임 Goal은 생성하지 않는다. 기존 읽기 전용 관찰 조작의 검증을 아래 순서로 기록하고 `evidenceStageClaimed=null`을 유지한다.

| 확인 순서 | 검증 내용 |
| --- | --- |
| E1 의미 검토 | 관찰자가 동일 음식 업무를 다른 정밀도로 읽는다. 카메라 조작은 주문·배달·Actor 위치를 변경하지 않는다. |
| E2 조립 검토 | 기존 full placement 결과를 소비하고 food만 표시/해제. 타 OS·Scene 저장·전역 카메라 관문은 보존. |
| E3 순수 시험 | 최신 요청 안정 적용·유예·재진입·TTL·Clear·복제·revision·동일 업무 중복 집계·범위/예산·비정상 시계. |
| E4 표현 준비 | 기존 도형/Material·합성 장소 기준점 재사용. 거리 상세와 동/구 표본 요약의 범위/출처를 표시. |
| E5 결속 확인 | canonical Scene의 runtime-only 조정기로 기존 사본/명시적 검증 표본의 같은 revision을 소비하는지 확인. |
| E6 회복 확인 | 상세 해제 중 새 revision, 만료 후 복귀, 다른 OS 보존, 반복 왕복의 객체/Material 누적 및 Clear 검사. |
| E7 화면 확인 | 가능한 경우 실제 Play/Game View 3단계와 Console·Scene hash를 별도 기록. 검증 표본과 실제 서버 연결을 구분. |

운영 서버를 새로 시작하거나 실주문을 만들지 않는다. 실제 연결이 없으면 실패를 표본으로 감추지 않고 검증 도구의 명시적인 `VerificationSample` 입력만 별도로 사용한다. 자료원은 캡처/기록에 명시한다. 화면 밖 업무 진행은 새로 흉내 내지 않고 주입된 최신 revision/기존 서버 사본으로 복귀를 검증한다.

## 착수 확인과 남은 실행

Hongdal 74개·Unity 13개 변경 확인 시점에 대상 관찰 파일은 clean이었다. 기존 행정동 캡처 Editor PID40152가 점유 중이어서 종료를 기다렸다. 정상 자체 종료와 Scene 미저장 기록 확인 후에만 연결 패키지·Unity 파일을 편집한다. 기존 행정동·지형·차선 작업 파일은 소유하지 않는다.

미정인 전역 행정구역 집계·권한·메모리 예산·생활상 종류를 이 범위에서 임의 확정하지 않는다. 구현·자동 시험·실제 Unity·실제 서버 연결을 분리하여 후속 결과를 기록한다. 새 디오라마 규칙 후보 없음.

## 구현 결과와 자동 검증

- `생활관찰표현Session`: 지역/음식 OS 범위, 자료원·실행·업무 ID별 중복 없는 집계, 깊은 복제, 낮은 revision·동일 revision 충돌 보류, TTL·Clear, 최신 요청·0.5초 안정·5초 해제 유예를 가진 순수 메모리 정책이다.
- `생활관찰표현Layer`: 기존 Controller의 승인된 전체 배치 결과를 소비한다. 다른 OS를 잃지 않도록 전체 계획을 유지하고, 음식 상세 객체만 숨기거나 해제한다. 카메라 배율에는 진입/복귀 임계값 차이를 두고 실제 이동이 멈춘 뒤 전환한다. 초기 표본은 음식 사본 128개 이하이며 동·구 요약은 사가정 한 지역만 센다.
- 두 기존 표현기에 OS별 표시/보존·선택적 삭제를 추가했다. 상세를 지우는 모든 경로에서 소유 Material을 명시적으로 해제한다. `OnDestroy`만 의존할 때 EditMode에서 Material이 남는 실패를 실제 시험으로 발견·수정했다.
- 기존 전용 검증 endpoint의 `area:kr-seoul-jungnang-myeonmok`를 그대로 사용한다. 새로운 지역 권위를 만들거나 법정동 ID로 암묵 변환하지 않는다. 다른 지역 음식 응답은 `LivingObservationAreaMismatch`로 표시하고 마지막 유효 음식 사본은 원래 TTL까지만 유지한다. 일반 배포 서버 지역 연결을 검증한 것은 아니다.
- `Clear` 뒤 새 전체 사본이 들어오기 전에는 재조립하지 않는다. 카메라 이동 중에도 만료 사본을 제거하고, 다른 OS 사본·표현은 해당 수명과 권한을 유지한다.

| 증거 | 결과 / 경계 |
| --- | --- |
| 순수 정책 집중 시험 | 33/33. UTC·단조 시계 오류, 범위·예산, 복제·판본·삭제·만료·왕복 포함 |
| 기존 관찰 집중 회귀 | 99/99. 신규 정책 포함 |
| `Ssalddel.Unity.slnx` build | 경고 0·오류 0 |
| 공통 패키지 전체 시험 | 849/849. 실제 Unity 실행과 별도 |
| Unity 6000.5.6f1 EditMode | 최종 37/37, 새 표현 시험 14개 포함. `artifacts/local/validation/living-world-streaming/editmode-final.xml` |
| 공통 Fast 검증 | diff·Simulation Unity 코드 지도 검사 통과 후 E 책임 생성 지도 불일치로 차단. 이미 다른 변경이 있는 전역 생성물을 임의 갱신하지 않았다. Task 전체 통과를 주장하지 않음 |
| Play/Game View | 최종 `gameview-r46c` 실제 1280×720 PNG 6장 직접 검토·hash 대조. 약 14.68초 표본이며 HTTP 운영 연결·5/10분 안정성 검증을 대체하지 않음 |

첫 Unity 시험은 신규 시험의 `GetInstanceID`가 현재 Editor에서 제거된 API여서 컴파일 실패했고 참조 동일성 비교로 바꿨다. 다음 시험은 Material 해제와 기존 Controller 시험의 과거 Epoch 만료 두 항목을 발견했다. 재시험 중 별도 행정동 지형 파일의 작성 중 컴파일 오류가 있었으나 해당 소스를 변경하지 않았으며, 소유 작업이 보완한 뒤 최종 37개를 다시 통과했다. 실패를 삭제하거나 성공으로 바꿔 기록하지 않는다.

## 실제 화면 검토 중 발견한 문제

- 첫 `gameview-r46`의 내부 상태 검사는 통과했지만 PNG에서는 지구본 카메라가 디오라마를 덮었다. 화면 성공 증거로 채택하지 않았다. 검증 도구가 표시 플래그만 바꾸던 경로를 기존 `세계지구본View.OpenRegionalDiorama()` 전환으로 교체하고, 실제 관찰 카메라 활성·지구본 카메라 비활성·계층 일치를 촬영 전후 검사한다. 겹친 두 상태 패널도 세로로 분리했다.
- 검증 도구의 신규 `.meta` GUID가 처음에는 33자리여서 Unity가 무시했다. 기존 자산 GUID는 바꾸지 않고 이 신규 GUID만 유효한 32자리로 바로잡았다.
- 재촬영 `gameview-r46b`는 다른 작업의 행정동 Editor 파일이 수정되는 동안 어셈블리가 다시 로드되어 `LivingObservationUnexpectedPlayStopOrReload`로 중단됐다. PNG 0개·Scene hash 불변이며 성공으로 취급하지 않는다. 앞선 시작 시점의 해당 파일 누락 선언 컴파일 오류 역시 그 파일을 수정하지 않고 소유 작업의 보완을 기다렸다.
- 실제 화면 검토 결과는 자동 `profilePassed`와 별도로 남긴다. 검증 도구의 명시적 표본 주입은 실제 HTTP 연결·실업무 진행·장시간 안정성을 증명하지 않는다.

## 최종 Play/Game View 결과

별도 Unity 저장소 `Documentation/Changes/2026-09-26-living-world-streaming/README.md`와 `gameview-r46c/capture-manifest.json`에 결과와 6개 PNG를 남겼다. 실패한 두 시도는 삭제하지 않고 `artifacts/local/validation/living-world-streaming/gameview-r46{,b}/`로 옮겨 보존했다.

2026-09-26 16:21 KST의 명시적 표본 실행에서 거리 상세→동 표본 요약→구 표본 요약→실제 5초 해제→최신 revision 거리 복귀→TTL 만료→Clear를 확인했다. 음식 사본 2건은 상세 해제 중 유지되고 복귀 때 revision 8·7에서 9·10으로 갱신된다. 이후 만료·Clear에서 음식 사본/표식 0, 화물 표식은 매 확인점에서 같은 객체로 보존된다. 각 PNG에서 지구본 카메라 비활성·관찰 카메라 활성·예상 계층 일치를 기록했고, 직접 화면을 열어 디오라마와 두 패널의 분리를 확인했다. 구/동은 같은 사가정 표본의 요약 단계이지 새 행정 지도 계층을 만든 것이 아니다.

최종 manifest SHA-256은 `D6B4E5F36CE26DA9F7E0DDAB72545FC30CB41258F2A665ADADFE8CDFAFAE4581`, 6개 PNG hash 불일치 0, Scene hash 전후 동일이다. Scene 저장 없이 전용 Editor가 자체 종료했다. Console 종료 집계는 오류 9건, 기록 구간 Error/Exception은 8건이며 기존 Replay 불일치·연결 실패·서버 세션 누락을 분리 기록했다. Console 0·실제 입력·실제 API·장시간 메모리 안정성·Windows 빌드의 완료를 주장하지 않는다. 화면은 구현 검토용 표본이며 확정 시안이 아니다.

## 남은 경계

전국 지구본·실제 구/동별 통계·다른 생활상·지형 자산 자체의 streaming은 이번 범위가 아니다. 원문 HTTP의 역순 응답·삭제 재등장은 기존 Interpreter 책임이며 이 정책에 무제한 tombstone 저장소를 추가하지 않았다. 실제 401/403 철회 시 즉시 삭제는 기존 검증 Transport가 상태 코드를 구분하지 않으므로 후속 연결 과제다. 이번에는 관찰 종료/Clear·TTL 삭제만 시험했다. commit·push·Scene 저장·DB·새 서버 실행·운영 효과·E 승격 없음.
