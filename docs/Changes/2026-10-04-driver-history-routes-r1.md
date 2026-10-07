# 기사 배달 내역의 목록·상세 페이지 분리

2026-10-04. 현재 배달 수행 화면의 `배달 내역` 버튼으로 별도 목록 페이지에 이동하고, 각 행의 `상세 보기`로 선택한 배달의 상세 페이지에 이동하도록 정리했다. 기존 한 페이지 안의 정산 목록/상세 전환을 독립 화면으로 옮겼다. 날짜별 목록은 최근 완료순 20건씩 표시하고 이전/다음 페이지를 제공한다. 상세에서 돌아오면 같은 날짜와 페이지를 유지한다.

목록에는 매장명·완료 시각·배달료·요금 기준 거리를 표시한다. 요금 구성·공제·수령액과 허용된 주문/고객 정보는 상세의 세 카드에서 확인한다. 일간 합계는 서버의 전체 날짜 값이며 현재 페이지의 부분 합계가 아니다. 실제 주행거리가 없는 기록에 GPS 거리를 생성하지 않는다.

[페이지 책임과 화면→코드→API→DB](../ProjectOverview/page-docs/driver-completed-delivery-detail-r1.md)가 현행 기준이다. `food-delivery-history`와 `food-delivery-history-detail`은 별도 Shell route다. 앱 창에서 실제 Shell을 사용하며, 기존 수행 화면과 알림 진입 route를 같은 루트 화면에 연결한다. MainPage는 현재 수행 화면을, 목록/상세 PageModel은 각각의 조회·표시 수명을 소유한다.

완료 후72시간의 주문/고객 열람 기한, 본인 권한·동결 요금·정산 원장은 기존 기준을 유지한다. 목록에서 고객 정보를 미리 조회하지 않는다. 상세 이탈·앱 중단·계정/기사 권한 변경 때 개인정보를 제거하고 앱 재개 시 서버 권한을 다시 조회한다. 목록/상세 페이지의 세션 이벤트 구독도 화면 활성 수명으로 제한한다. 서버 API·DB 스키마·요율·공제·지급 실행은 변경하지 않았다.

## 검증

- Fast `20261004-123451`: 기사 앱·시험 프로젝트 빌드, 알림 회귀를 포함한 관련205/205 통과. 독립 상세44건과 목록33건을 포함한다.
- 최종 Task `20261004-123633`: 전체3.5 빌드 통과,6,425/6,432 통과·기존7실패. 기준 `20261004-120002`와 공통 시험 결과 변경0, 실패 이름·오류·전체 스택 동일, 새 이름의59건 전부 통과다. 기존 상세 시험11개 이름/구성을 이관해 전체 건수는48건 증가했다. 전체 게이트는 미통과이며 기존 metadata/분류·HIOPS 문구·통합 beta route·재료 카드 실패를 이번 UI 변경으로 해결했다고 표시하지 않는다.
- 로컬 HTTP11개 검증: production Controller→UseCase→정산 Recorder와 격리 SQLite를 사용했다. 본인 일간23건·다른 기사 제외·상세/정확한 기한 경계·72시간 이후 금융 투영 유지·개인정보 제외를 확인했다. 서버의 전체 일간 조회를 앱에서20건씩 나누며 서버 페이지 API를 신설한 것은 아니다.
- 최종 제품/시험22경로의 지문은 실행 후에도 같으며, 보관 Debug APK와 설치된 base APK의 SHA-256은 `22B1CDFD581295B1D319431E35FF0415726E4F25BCDE2DB041F378CD64208248`로 일치한다.

## 실제 Android 화면

1080×2400·density420·글자1.0의 전용 Android 에뮬레이터에 최종 APK를 설치했다. [첫 목록](../assets/changes/2026-10-04-driver-history-routes-r1/list-page-one.png)에서 날짜·매장·완료 시각·배달료·거리와 전체 날짜 합계를 확인했다. [2페이지](../assets/changes/2026-10-04-driver-history-routes-r1/list-page-two.png)는21~23번째3건이며 합계108,560원을 그대로 유지한다. 표시된 금액과 주소·메뉴·고객은 검증용 예시이고 실제 수입·공제·입금 데이터가 아니다.

21번째 행의 `상세 보기`로 [별도 상세](../assets/changes/2026-10-04-driver-history-routes-r1/detail-page-two-top.png)에 진입해 [주문·고객 카드](../assets/changes/2026-10-04-driver-history-routes-r1/detail-private.png)를 확인했다. Android 뒤로 가기로 [같은 날짜·2페이지](../assets/changes/2026-10-04-driver-history-routes-r1/list-return-two.png)에 복귀했고, 일간 조회를 다시 요청하지 않았다. 이어22번째 행을 선택하자 [선택한 다른 배달](../assets/changes/2026-10-04-driver-history-routes-r1/detail-second-selection.png)의 상세로 바뀌었다.

22번째 기록의 서버 시각을 완료 후72시간 이후로 옮겨 다시 조회했다. [기간 종료](../assets/changes/2026-10-04-driver-history-routes-r1/detail-expired.png)에서는 주문·고객 카드가 사라지고 같은 정산 투영 전체가 유지됐다. 배달료4,720원·공제160원·수령액4,560원·요금 기준 거리1.68km가 같다. 앱을 HOME으로 보낸 동안 같은 기한을 넘긴 후 다시 열었을 때도 [재개 후 기간 종료](../assets/changes/2026-10-04-driver-history-routes-r1/detail-resumed-expired.png)로 표시되고 상세 API를 다시 조회했다. OS·기기 시각을 바꾸거나 실제로3일 기다린 결과가 아니다.

고객 카드가 보이는 상세에서 기존 Android 알림 진입 Intent를 전달해 [배달 수행 화면 복귀](../assets/changes/2026-10-04-driver-history-routes-r1/notification-workspace-return.png)와 서버 정본 재조회200을 확인했다. 검증 서버에는 활성 추천이 없으므로 종료된 대상으로 처리됐으며 신규 유효 배차의 선택·수락이나 실제 OS 알림/FCM 도착까지 확인한 것은 아니다. 날짜 선택기로 다른 완료일을 고르면 [빈 목록](../assets/changes/2026-10-04-driver-history-routes-r1/list-empty-date.png)과 비활성 페이지 버튼이 나오고, 원래 날짜로 되돌리면23건·1페이지가 복원됐다.

PNG10개는 편집하지 않았으며 원시 캡처와 문서 사본의 해시가 모두 같다. 첫 실행에 에뮬레이터 System UI 응답 지연 대화상자가 나타났고 `Wait` 이후 같은 설치본에서 위 동작을 확인했다. 이 현상을 제품 오류나 운영 기기 검증으로 확정하지 않았다. 운영 DB·실제 배달·실제 지도·물리 단말·은행/PG·운영 배포는 별도다.

이번 범위는 제품19경로(이전 부분 모델 삭제 포함)·시험/연결3경로·문서5경로·PNG10개다. 관련 없는 dirty 작업과 배달 영상/MYBOX를 보존했고 커밋·푸시는 하지 않았다. 검증 후 전용 서버·ADB 연결·에뮬레이터를 종료했다. 원시 근거는 Git 제외 `artifacts/local/driver-history-routes-r1/`에 보관한다. 기존 [상세 열람 r1 실행 기록](2026-10-04-driver-completed-detail-r1.md)은 이전 화면의 검증 이력으로 보존한다.
