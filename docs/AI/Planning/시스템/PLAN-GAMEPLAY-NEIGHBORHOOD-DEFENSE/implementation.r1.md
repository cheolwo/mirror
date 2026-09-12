# 사가정역 방어 준비 · 첫 구현 결과 r1

## 커밋한 기준선

2026-09-12, 구현에 앞서 main의 기존 변경을 아래 네 커밋으로 분리했다. 합계 43파일, 5,559줄 추가·7줄 삭제(생성 migration 포함). push하지 않았다.

| 커밋 | 맥락 |
| --- | --- |
| f231fdf8 | 행정동 조회 계약·Unity 읽기 경계 |
| b7fcd3cd | 서버 공간 투영·검토 기반 표시 원장 |
| abdabab8 | 공식 행정동 귀속·Mongo 게시 검증 |
| e1987725 | 실자료 관문·검증 문서 |

이후 다른 스레드의 생활인구 수집 변경과 커밋은 이 묶음에 포함하지 않는다. 아래 새 방어 준비 구현은 아직 커밋하지 않았다.

## 구현한 범위

- `Ssalddel.Simulation.Contracts/UnityPackage/Runtime/역방어준비Contracts.cs`: 명령·읽기 사본·불변 행위 기록 계약.
- `Ssalddel.Simulation.Domain/UnityPackage/Runtime/역방어준비Session.cs`: 메모리 세션별 단일 인원, 미리보기 무변경, 예상 판본 검사, 명령 멱등성, 재배치와 이전 자리 해제, 기록 1,024건 상한.
- `Ssalddel.Simulation.Application/RuntimeCore/역방어준비RuntimeFactory.cs`: 독립 Core 생성 진입. UI는 계약 인터페이스만 소비한다.
- Unity `Assets/Ssalddel/Bootstrap/역방어준비Controller.cs`, `Presentation/World/역방어준비View.cs`: 준비 열기→옥상 A/B 선택→확인→재배치·운영 복귀. 기존 지도와 카메라를 재사용한다.
- `Runtime/World/역방어배치자료.cs`, `Resources/StationDefensePlacement.json`: 지도 hash·높이 출처·건물 외곽·기호 여유 검사. 실패한 연구 r1과 새 수용 [연구 r2](placement-study.r2.md)를 분리했다.
- 기존 운영 View는 표시만 숨기며 사본 갱신·만료를 계속한다. 게임 상태는 운영 원장·API·실제 인물과 연결하지 않는다.
- 기존 `SimulationWorldShellBuilder`로 canonical Scene에 준비 Controller/View를 추가했다. 새 공식 Scene을 만들지 않았다.
- WI 하나와 주체 셋·승인 기획 hash·작업 명세를 연결했다. 주체 검사 도구는 현행 v3/기존 v2 분류 계약을 함께 허용하고, 순서 의존 시험은 명시적 주체 ID로 바꿨다.
- 새 WI의 주요 결과는 `DefenseUnitAssigned`이며 책임·이름 생성 목록과 공간모판 참조 판본을 함께 갱신했다. 음양은 기존 `Contextual` 경로를 사용하고 승인 문맥 없이 새 고정 의미나 보상 효과를 부여하지 않는다. 기존 공간모판 8개의 구성/경로는 확장하지 않았다.

## 증거와 남은 범위

- Core 집중 시험 12/12, Unity 컴파일 오류 0, 준비 EditMode 8/8·운영 표시 5/5·사가정 지도 7/7 통과. 원시 기록: `artifacts/local/validation/station-defense-preparation/`.
- `SubjectInteractionDevelopmentTests:Passed`, 주체·WI·작업 명세 검사 통과. 자동 성숙도 승격은 하지 않았다.
- 첫 범위 Task는 build를 통과했지만 1,912개 중 11개가 WI 추가에 따른 생성 이름 목록/참조 revision 누락으로 실패했다. 해당 연결과 새 WI 회귀를 보완한 최종 범위 Task는 Simulation solution build와 전체 시험을 통과했다(`artifacts/local/validation/20260912-202849`). 초기 11개 실패를 기존 오류로 분류하지 않는다.
- 추가 Scene 정책 회귀 3개 중 2개 통과·1개 실패: 기존 `AreaSetCompositionPatternReview.unity`, `NatureH3GameViewReview.unity` 분류 누락. 두 Scene은 이번 변경에서 만들거나 수정하지 않았다.
- 전역 Graph Map 생성 검사는 `PlanningAssessmentCoverageCount`로 차단된다. 현재 기획 목차에 대한 영향 판정이 11개 부족하며 이번 방어 기획 외 기존 운영/행정동 기획 10개도 포함된다. 전역 Graph Map의 의미를 임의로 보충하지 않았다. 이번 모판의 지휘자→인원→자리 관계는 연구 r2에 한정한다.
- 실제 Play Mode에서 준비→A확정(rev1/기록1)→B재배치(rev2/기록2)→운영 복귀→준비 재진입 시 B 보존을 확인했다. 인원 기호 1개·자리 표식 2개·게임 Root 1개였다. `ScreenCapture`로 OnGUI 포함 PNG를 확보했다. 전환은 View API 직접 호출이며 실제 마우스 성공 검증이 아니다. `GameView.SendEvent`는 두 전달 방식 모두 화면 전이를 일으키지 못했다.
- 실제 첫 화면에서 이름표가 인원 기호 일부를 가리는 문제를 발견해 이름표를 30px 위로 이동했다. 최종 컴파일 오류 0·준비 EditMode 8/8을 재확인하고 수정 A근접/B전체 화면도 다시 확인했다. [최종 화면 기록](../../../../Changes/2026-09-12-station-defense-preparation.md).
- 실행 중 방어 준비 전용 Console 오류는 없었지만 기존 canonical 시작의 MissingScript 10건·ReplayHashMismatch·외부 서버/Session 누락·Nature 지연 진단을 함께 보존했다. 프로젝트 전체 Console 정상이라고 선언하지 않는다. 첫 캡처 뒤 Editor stopped/clean, Scene hash `36D81A6986598F08D1EA1EA94E83F7FC7BD2D67B166FE6ABA4ED2A1395A34C55` 전후 동일이다.
- 최종 캡처 뒤에도 Editor stopped/clean, Scene와 배치 자료 hash 전후 동일이다. View hash `A33278EA66B54CDE9A8C4B626D5386D100EFB5709C59A217804990DC8996F260`, 배치 자료 hash `F5C316EE200F87A4F443C97241F428EC5D57E291D22C705F0593FA1A12F99684`. 캡처 담당은 실행 중 파일/Scene/Save를 변경하지 않았다. 최종 두 PNG를 main 문서 자산과 Unity Documentation/Changes에 보존했다.
- 전투·몬스터·바리케이드·사격·이동·경제·주민 피해·영속 저장·RemoteHost 연결·행정동 타일 이식·역 반경 재조회·실운영은 범위 밖이다. 본 결과는 고정 지도 위 준비 기호이며 완성된 디펜스 게임이 아니다.
