# 이천 로컬 입체 지형 검토 r10

2026-09-23 사용자 「구현까지 해줘」에 따른 범위: r9의 실제 높이 표현을 로컬 Editor 검토로 구현한다. 배포 검토보류를 해제하지 않는다. 기존 Scene·지구본을 교체하지 않는다.

허용 경로: Hongdal `eng/public-spatial/build-icheon-preview.py`, 이 기획 문서와 목차·현재작업; Unity `Assets/Ssalddel/Presentation/WorldMap/이천입체지형검토View.cs` 및 meta. 공용 상태/계약 쓰기 없음.

표현 검증: E1 출처 hash 확인 → E2 같은 미터 좌표계 정렬 → E3 유효 셀·높이 범위 검사 → E4 실제 높이 1배·북쪽 +Z·색상 결속 → E5 임시 메시 생성 → E6 카메라 회전/거리 변경·닫기 → E7 실제 화면 확인. 이 목록은 시험 순서이며 E 승격/업무 WI 완료 선언이 아니다. 운영 Command·NPC·저장/Replay 해당 없음.

대상은 수집 창 내부 3km×3km, EPSG32652, 30m 간격 101×101이다. 수직 datum 변환은 하지 않고 원본 DSM 최저값을 상대 높이 원점으로 사용한다. 위성영상과 DSM을 같은 격자에 정렬한다. 30m 보간은 정밀도 향상이 아니다. 외부 파일은 로컬 경로에서만 읽고 Resources·배포 패키지에 포함하지 않는다. 공장·트럭·도로·자동 지구본 확대 전환은 이번 작은 표본에서 제외한다.

## 실제 구현과 검증 결과

- 10,201정점·20,000삼각형. 높이 46.3834~223.4707m, 상대 높이차 177.0873m. 전 정점 법선 +Y, Collider 없음, 잘못된 계약 거부를 Editor eval로 확인했다. 별도 EditMode suite는 실행하지 않았다.
- Python 두 번 실행의 결과 hash 동일: `a0876d4b31aaddec4a0b520bc6b77d132c9d4a015000b45cdaddf7915369d924`. 원본 hash와 전체 셀 유효성 검사 통과.
- canonical Scene Play에서 임시 View 생성, 카메라 API로 원경/근경 변경, 삭제 후 객체 0 확인, Play 종료. Scene 저장 없음. 회전/확대 UI 버튼 코드는 있으나 직접 클릭 검증은 하지 않았다.
- Unity `Documentation/Changes/2026-09-23-icheon-relief/overview.png`, `near.png`는 **지정 카메라 렌더**다. `game.png`는 screenshot 도구가 지구본을 담아 통합 화면 증거로 채택하지 않았다. 실제 전체 Game View의 최종 카메라 선택/기존 UI와의 통합은 미완료다.
- Console 오류 8건이 조회됐다: 기존 서버 세션 누락, WorldTileStream/TurnClosing 연결 실패, SimulationReplayHashMismatch. 무오류 통합 완료로 보고하지 않는다. 자료 표본에서 업무 서버를 우회하거나 오류를 숨기지 않았다.
- 원영상 색상을 30m 정점색으로 낮춰 흐림이 남는다. 다음은 10m 영상 텍스처를 별도 보존하는 UV 결속, 지구본 상세 진입과 기존 UI 충돌 검증이다. 현재는 자동 확대 전환이 아닌 메뉴로 여는 로컬 검토 도구다.
- DB 추가 등록·배포·commit·push 없음. 새 디오라마 규칙 후보 없음.
