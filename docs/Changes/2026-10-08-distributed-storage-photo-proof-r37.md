# 분산 보관 합의·사진 완료 r37

기존 분산 물류에 공간 제공자↔실제 물품 소유자의 보관 합의와 입고·피킹·전달·반품 사진을 연결한다. 사진을 저장하는 것과 수량/운송 상태를 완료하는 것을 분리하고, 서버가 현재 업무·수량·판본과 정확히 연결한 사진만 완료 명령에서 소비한다.

이번 범위는 모바일 앱·서버다. 기존 Unity·배달 영상과 관련 없는 변경을 보존한다. [승인 기준](../AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/storage-agreement-photo-proof.implementation.r31.md), [페이지·코드·DB 관계](../ProjectOverview/page-docs/distributed-storage-photo-proof-r37.md).

## 로컬 검증

- 최종 Fast: **313 통과, 실패 0, Mongo 환경 시험 1 건너뜀**. `artifacts/local/validation/20261008-154257/`.
- 최종 Task: **8,938 통과, 실패 0, Mongo 환경 시험 2 건너뜀**. `artifacts/local/validation/20261008-154353/`. 최종 CSS까지 포함한 전체 솔루션 빌드와 변경 범위 공백 검사도 통과했다.
- 승인 범위 98개 파일 중 컴파일 대상 90개는 Fast/Task 실행 전후 SHA-256이 같았다. 기록은 `artifacts/local/distributed-proof-r37-20261008/root/`의 소유 목록과 해당 실행의 before/after JSON이다. 이전 r36·관련 없는 dirty 작업을 커밋하거나 되돌리지 않았다.
- 실제 로컬 JPEG 업로드·정규화·비공개 파일 재조회, HTTP 인증/타인 차단/NoStore, 완료 요청 재생, 사진 소비 뒤 재고 실패의 SQL 롤백·복구를 실행했다. 실제 배차가 해제된 이전 기사도 사진을 조회하지 못한다.
- 실제 관계형 DB 모델과 새 마이그레이션의 키·인덱스·암호화 변환기를 대조하고, 격리 SQLite에 이전 완료 영수증을 남긴 채 새 테이블을 추가했다. MySQL SQL 생성·snapshot 대조는 통과했으나 공용 MySQL에 적용한 것은 아니다.
- 양측 합의·새 판본 재동의·만료/철회·부분 수량·사진 묶음·30일/24시간 정리·진행 중/불명확 사건 보류를 시험했다. 실제 보호지원 UseCase의 72자 사건 번호가 SQL 보존 보호에 먼저 연결되는 순서도 실행했다. Mongo 서버를 이용하는 두 시험은 실행하지 못했으며, 출처 인덱스가 없는 기존 사건은 사진 정리 허가로 간주하지 않는다.
- 공통 지원만 구성한 경우와 두 모듈의 등록 순서가 다른 경우에도 사진 보존 연결이 해결된다. 사진 필수 명령은 새 묶음 API와 기존 단일 분산 완료 API 모두에 적용했다.

## 화면과 설치 파일

- 실제 서버 연습 원장에서 조회한 31개 상태 화면을 320px·390px 두 폭, 총 62개 조합으로 렌더링하고 브라우저에서 대조했다. 가로 넘침·오류 메시지는 0개이며 표시된 버튼·링크·입력·접이기·체크박스 라벨의 터치 높이는 48px 이상이다. 업무 복귀·입고 계획·적재 위치 링크의 좁은 터치 영역을 함께 보완했다.
- 생성된 공통 화면의 입고·피킹·전달·반품 사진 이벤트에서 촬영 취소, 미리보기, 재촬영 취소, 제거, 업무 입력 변경 시 사진 무효화를 실행했다. 이 화면 검증은 통과한 Task 실행의 DLL·실제 DTO와 최종 CSS를 사용한 읽기 전용 미리보기다. 사진은 명시적으로 표시한 시험 자료이며 실제 카메라·HTTP 전송·완료 버튼 실행 증거가 아니다.
- 대표 화면: [보관 합의](../assets/changes/2026-10-08-distributed-storage-photo-proof-r37/warehouse-agreement-active-390-full.jpg), [입고 사진 확인](../assets/changes/2026-10-08-distributed-storage-photo-proof-r37/warehouse-receive-photo-390-full.jpg), [반품 사진 확인](../assets/changes/2026-10-08-distributed-storage-photo-proof-r37/warehouse-return-photo-390-full.jpg). 원본 캡처의 화소·폭·SHA-256과 세부 측정은 `artifacts/local/distributed-proof-r37-20261008/ui/preview-rendered-links-final/qa-result.json`에 있다.
- 네 역할 APK는 최종 빌드 결과를 이 구간에 추가한다.

## 적용 경계

기능은 기본 OFF, 정확한 `distributed-fulfillment-r36` profile·Simulation 경계를 유지한다. 사진 바이트·업무 연결 확인을 물품 내용 판정이나 실제 거래·입금 확인으로 확대하지 않는다. 서버 사진의 보관·삭제 결과와 휴대폰/OS의 촬영 사본 정리도 같은 완료로 보고하지 않는다.

Unity·배달 영상 변경, 공용 DB 마이그레이션, 실제 휴대폰 촬영·설치, Azure 배포·저장소 시험, 실제 로그인/운영·계약 인증·금전 이체, commit/push는 수행하지 않았다. 페이지·코드·DB 문서와 승인 기획을 함께 유지한다.
