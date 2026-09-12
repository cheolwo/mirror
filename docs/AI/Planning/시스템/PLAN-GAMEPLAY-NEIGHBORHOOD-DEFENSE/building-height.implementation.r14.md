# 건물 높이 보완층 r14 구현 결과

- 기준: [승인 범위](building-presentation.r14.md)와 [높이 작업 명세](building-height.e7-work-order.json). 기존 준비 기획 r1/hash는 그대로다.
- 상태: `ReadOnlyHeightOverlayImplemented / AddressParcelCandidates / GameViewUnverified`.

## 변경

Unity 저장소 `C:/Users/user/ssalddel`에 별도 `SagajeongBuildingHeightOverlay.json`, `Runtime/World/건물높이보완자료.cs`, `Tests/EditMode/건물높이보완Tests.cs`와 Unity가 생성한 `.meta`를 추가했다. `사가정지리MeshBuilder.cs`에 검사된 높이 조회를 선택 인자로 연결하고 `사가정운영디오라마View.cs`에서 로드·검사·진단한다. 기존 dirty 변경은 보존했다.

8.3m/19.1m 두 값만 벽·지붕에 사용하며 기존 접지 그림자도 같은 Mesh를 따른다. 자료가 없거나 판본/주소/출처가 맞지 않으면 보완층을 거부하고 원본 배경을 유지한다. 상태 표시에서 후보 연결과 거부 여부를 구분한다. 5층/원문0은4m, 방어 옥상 A/B는44m/40m 그대로다.

원본 `SagajeongReference.json`의 SHA-256은 `4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3`으로 유지했다. 보완층의 응답 hash 두 개와 높이를 r13 동결 원본 파일과 독립 대조했다. 공식 건물 동일성·GIS 권리 검토·DB 승인 승격은 하지 않았다. 출처 생성일20220813은 현장 관측일이 아니다.

## 검증

- Unity 6000.5.6f1 컴파일 오류0. 연결된 프로젝트 경로 확인 후 Pipeline과 CLI를 사용했다. MCP의 recompile/run_tests 호출 응답은 지연/시간초과했으나 별도 상태 조회와 CLI에서 실제 완료를 확인했다. 같은 작업을 중복 실행 완료로 집계하지 않았다.
- 새 높이 시험19/19: 두 양수·결손 유지, 잘못된 높이5종, 판본/hash/출처/주소/중복 등10종, 기존 출처 높이 덮어쓰기 금지, 원본 불변, 생성 벽/지붕, View 초기화·해제 메서드·재조립.
- 기존 지도7/7, 방어8/8, 운영 표시5/5 통과(총39/39). 초기 시험의 `NotLoaded`, `ShouldRunBehaviour()` 오류는 일반 MonoBehaviour의 EditMode 자동 초기화/메시지 호출 가정 오류였으며 명시적 초기화·해제 메서드 시험으로 수정했다. 실제 Unity 자동 생명주기나 사용자 클릭 검증이라고 해석하지 않는다.
- 상세 원시 결과: `artifacts/local/validation/sagajeong-building-height-r14/`. 원자료 해시 대조 및 양쪽 저장소 해당 변경 diff 검사 통과.

이번 결과는 **코드·EditMode 검증**이다. Play Mode/Game View, 카메라에서 높이 차이가 충분히 보이는지, 그림자 시각 품질, 런타임 실패 알림 가독성은 미검증이다. Scene을 저장하거나 새 Scene·배치 후보·운영 효과·API/DB 저장·commit/push를 만들지 않았다. 다음 검토는 실제 화면에서 두 대상의 높이·주변 건물 대비를 확인하는 좁은 범위이며, 층수 환산 기준은 여전히 미정이다.
