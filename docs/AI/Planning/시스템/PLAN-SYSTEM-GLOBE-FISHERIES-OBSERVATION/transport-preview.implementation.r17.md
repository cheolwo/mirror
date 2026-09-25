# 운송수단별 DB 사본 지구본 표현 r17

2026-09-23 사용자 요청에 따른 `ApprovedScopedLocalPreview`. 기존 r14 미리보기와 r16 저장 자료를 연결하는 좁은 표현 변경이다.

## 구현 전 범위·검증 명세

- 관찰자가 기존 지구본에서 DB 검증 사본을 명시적으로 연다. 선박·비행기 모형이 국가 간 통계 연결 곡선을 왕복 방향별로 이동한다. 업무 WI·게임 Simulation 상태는 바꾸지 않는다.
- 실제 항로·선박 대수·항공기 대수·운항속도가 아니다. 각 국가/수단/방향별 모형 하나로 최대 20개를 고정한다. 중량이나 항만 합계를 시각 수량으로 환산하지 않는다.
- 대표 관측은 각 조합의 가장 최근 존재 월에서 BL 건수가 가장 큰 원자료 행 하나(동률은 고유 식별자순)를 선택한다. 국가 전체 합계가 아니며 해당 월·항만·공항·원필드·선택 규칙·출처·revision을 함께 전달한다. 없는 조합은 만들어내지 않는다.
- E1~E4: 읽기 전용 관찰자·명시 로드·자료 상세·실패 시 거부·종료 정리. E5~E7: 실제 DB 재조회/hash/결정적 export, Unity 유효성·방향·개수 상한·반복 갱신 시험, 가능한 실제 Editor 검증을 별도로 보고한다. 새 Goal·게임 E 승격 없음.
- 허용 쓰기: 기존 항만 CLI의 preview 분기, 별도 Unity의 신규 운송수단 View/Loader/Tests와 meta, 관련 문서. 기존 Scene·지도·업무 엔진·DB 권위·승인 상태·운영 API·배포 설정은 바꾸지 않는다. 원격 DB·commit·push 없음.
- source/DB 검증 실패를 샘플로 대체하지 않는다. 원본/키를 Unity 배포 자원에 포함하지 않고 비공개 로컬 검토 사본만 사용한다.

## 결과

상태: `LocalPreviewImplemented / EditModeAndPlayVerified`. r16의 중량 단위/항만 포함 관계 검토 보류는 그대로 유지한다.

- 기존 로컬 MySQL 2,431행을 독립 재조회·원본 대조한 뒤 20개 대표 관측 사본을 생성했다. 데이터 저장 변경 없이 읽기만 수행했다. 파일은 `artifacts/local/public-data/kcs-port-movement-2025-r1/transport-preview.json`, SHA256 `ced5401ac2e4f4057f69ec166c77323776980f8d4a00c81fbe428be8ff895e9a`이다.
- 기존 CLI `port-movement-preview` 추가. 수집기 build 오류/경고 0. 별도 Unity에 `운송수단관찰View`, `운송수단관찰Loader`, `운송수단관찰Tests`와 Unity 생성 meta를 추가했다. 기존 Scene 파일은 저장하지 않았다.
- Unity Editor 시험 8/8 통과: 유효 사본·중복·수단·숫자·공개승인 거부·방향·항공 높이·출처. 최초 시험은 import 시점 차이로 0건이었고 컴파일 완료 확인 뒤 재실행해 8건을 확인했다.
- 실제 canonical SimulationWorldShell Play에서 20개 모형 로드. 반복 로드 시 같은 View 하나·모형 20개 유지. 1,000회 Apply 전후 Transform 101개 유지, 이동 거리 10.69 Unity 단위, Collider 0을 확인했다. 이는 장시간 실운항 시험이 아니다.
- [실제 Game View](../../../../Changes/2026-09-23-transport-preview.md)를 확인했다. 기본 상세는 접힘이며 OnGUI 기반 간단한 검토 UI다. 물리 클릭·배포 빌드·실제 항로·항만 입출항 애니메이션은 미검증/미구현이다.
- API/DB는 Unity에서 직접 호출하지 않는다. Play에서 `Tools > Mirror > 2025 해상·항공 DB 사본 열기`로 파일을 선택한다. 기존 중립 화물 모드를 교체하고 합성 해양 연출을 잠시 숨긴다. 종료 시 정리한다.
- 기존 서버 세션 오류가 있어 전체 Console 0을 주장하지 않는다. Play 종료, Scene 저장·commit·push·정식 빌드 없음. 새 디오라마 규칙 후보 없음.
