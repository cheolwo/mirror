# 지역 디오라마용 방문 기록 데이터

사용자 정정에 따라 지구본에 펼치는 영상 카드를 제거했다. 이전 영상 재생 UI·Editor Loader·전용 UI 시험도 제거했다. 영상은 편집 작업공간에만 남긴다.

공유 파일: `C:/Users/user/Documents/ChatGPT/배달/edit-kit/delivery-visits.v2.json`.
프로젝트 내부 입력 사본: `artifacts/local/delivery-records/delivery-visits.v2.json` (원본과 SHA-256 대조 완료).
생성기: 같은 작업공간 `premiere-control/export_pickup_record.py`.

## 연결 범위

- 기존 `Ssalddel.Unity/Runtime/WorldProjection/AdministrativeDongDioramaInterpreter.cs`의 `BindPickupRecords`에서 현재 행정동에 확인 배정된 기록만 선택한다.
- `RegionalPickupRecordBinding.cs`의 데이터 계약은 매장·메뉴·방문 기록과 확인 상태를 담는다. `stationModule=true`는 별도 역세권 모듈 ID만 비교하며 행정동을 역세권으로 간주하지 않는다.
- 영상 경로·재생 시각·원본 클립은 공유 계약에서 제외한다. UI·VideoPlayer·지구본 연결·실제 주문/배차 변경이 없다.
- 매장별 방문 횟수와 메뉴 이력 등에 사용할 수 있다. 기록 ID/방문 ID는 영상 버전과 독립적이며 중복 방문 ID는 거부한다. 통합 원장 저장·여러 기록 병합 멱등 처리는 아직 구현하지 않았다.

## 근거와 남은 연결

7건의 실제 입력은 사용자 매장/메뉴 메모와 기존 NAVER 지오코딩 후보다. 기록 날짜와 인계 완료는 미확인이다. 현재 작업공간에는 기존 동 경계 원본과 생성 모듈 산출물이 없어 7건 모두 행정동·역세권 소속을 Unresolved로 남겼다. 지명만으로 면목3·8동 등 소속을 추정하지 않는다. 근거가 확인된 연결만 `Verified`와 근거 참조를 갖고 선택되며, 미배정/다른 모듈 기록은 분리한다.

현재 데이터 파일 생성·지역 선택 코드 연결까지다. DB/API 공급·행정동 소속 확정·실제 지역 화면 표시 완료가 아니다.

## 검증

지역 선택/기존 행정동 해석기 집중 시험 8/8 통과. 다른 동 제외, 미확인 제외, 행정동과 역세권 구분, 중복과 운영 사본 거부를 확인했다. 이전 지구본 UI의 실행 캡처와 시험 결과는 현재 기능의 증거로 사용하지 않는다.

새 디오라마 규칙 후보 없음. 운영 권위·공통 규칙·E 단계 승격 없음. Scene 저장·commit·push 없음.

최종 Task 검증 통과: 코드 지도·E 책임 지도·Ssalddel.Unity.slnx 빌드·Ssalddel.Unity.Tests 전체 시험. 검증 로그: artifacts/local/validation/20260922-231601. 실제 Unity에서 이전 영상 패널 객체 0개와 해당 View 타입 제거도 확인했다.
