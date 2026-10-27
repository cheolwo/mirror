# Mirror(거울) Current Work

## 검토한 제품 안내음의 저장소 푸시 r19 (2026-10-27)

- 19개 로컬 커밋 뒤 사용자가 푸시를 명시했다. [수동 검토 범위](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/generated-notification-assets-push.r19.md) · [자산/검증/전송 구분](../Changes/2026-10-27-reviewed-notification-assets-push-r19.md)에 따라 직접 생성한 두 단문 안내음의 정확한 경로·SHA만 허용한다. 기존 beep 이력·다른 매체 차단·정상 hook을 보존하고 실제 원격 전송과 SHA 확인은 별도 실행 근거로 남긴다. 원본 영상·비공개 다운로드 링크·APK·운영 배포는 포함하지 않는다.

## 맥락별 로컬 커밋 정리 (2026-10-27)

- 사용자가 커밋만 하도록 요청했다. [분리 기준과 결과](../Changes/2026-10-27-context-commits.md)에 화물·기사 알림·공동운영·입출고·음식·정산·시뮬레이션·검증·문서를 나눠 기록한다. 기존 검증 소스의 기능은 변경하지 않고 푸시·배포·운영 활성화는 하지 않는다. 비공개 다운로드 링크와 원본/실행 자료는 로컬에 보존한다.

## 기사 새 추천의 단발 진동·홍달배달 음성 r18 (2026-10-27)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/driver-recommendation-signal.r18.md) · [구현과 검증](../Changes/2026-10-27-driver-recommendation-signal-r18.md). 지도 정본 조회와 기존 Push 복구를 같은 설정/중복 방지 경로로 연결하고 ‘홍달배달’ 음원·180ms 단발 진동·기존 채널 무음/차단 유지·계정 왕복 이력·늦은 취소 경쟁을 보완했다. 최종 Fast174통과·Task16,353통과/환경16생략, 검토 APK133의 서명/음원/권한·두 ABI의 실제 앱 DLL/접속 설정을 확인했다. 실기기 설치·발음/볼륨·진동·실제 FCM은 수행하지 않았고 서버/운영 플래그·commit/push를 바꾸지 않았다.

## 기사 운행 흐름 정밀 보완 r17 (2026-10-27)

- [사용자 승인 범위](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/driver-workloop-precision.r17.md) · [실제 결과와 화면](../Changes/2026-10-27-driver-workloop-precision-r17.md). 기존 시작→지도·완료 확인→지도, 늦은 이동 취소와 활성 운송/신선한 위치에 한정한 위험 조회를 보완했다. 휴대폰에서 발견한 하차 상태 지연·숨김 버튼도 최종 APK131에서 재조회/지도 복귀로 확인했다. Fast249·Task16,326통과/환경16생략·최종 관련75·Python30·compiled관문28 통과. 새 격리 서버6완료/2만료무수락·SQL24전이/Mongo6문서·재시작 전후 동일, 실기기131 설치 해시/화면을 확인했다. 간헐 ready503과 auth429의 근거/범위는 보고에 보존했다. 시험기사Off/대기·소유호스트/USB연결/검토앱 정리, 실제 전 과정 운행·FCM·운영 활성화·commit/push와 구분한다.

## 관리자 상단 제목 간소화 r16 (2026-10-27)

- [소스 변경 범위](../Changes/2026-10-27-admin-header-simplification-r16.md). 사용자 요청대로 공통 상단의 `플랫폼 모바일 관제` 한 줄을 제거하고 제목·메뉴·계정·앱 상태·알림 연결을 보존했다. 단일 삭제/정적 검증이며 새 APK·실제 화면은 아직 미확인이다. FCM 필요성은 현재 구현과 공식 문서로 검토했으며 설정·실제 전송은 변경하지 않았다.

## 화물 화면·서버 준비 결손 보완 r15 (2026-10-27)

- [실제 결과·휴대폰 화면](../Changes/2026-10-27-cargo-runtime-followup-r15.md). 화주 홈을 상세 다음 행동 정책과 맞추고 운영자 전체 목록 제목·전문 기사 Android 안전 영역을 보완했다. 표준 Fast49/49·Task16,305통과/환경16생략·실패0, CLI15/15, 최신 검토 APK128 세 개의 서명/내부 endpoint/설치 해시 및 실제 SM-A346N의 화면·세션/재실행을 확인했다. 기존 EnsureCreated DB는 보존하고 새 짧은 GUID 시험 DB에 네 context migration98개를 실제 적용해 pending0·live/ready200을 확인했다. 같은 합성 의뢰의 배차/완료 서버 재시작, 제품 API·SQL 전이 각1회·Mongo 완료문서1개를 대조했다. 수신Off·합성기사종료·소유5378서버/USB reverse/검토앱프로세스 정리, DB/앱데이터/다른작업 보존. 전체 native 완주·지도 인증·실제 GPS/카메라/금융/FCM·운영 공개와 별도이며 commit/push 없음.

## 화물 서버·실제 휴대폰 검증 r14 (2026-10-27)

- [실제 결과·휴대폰 화면·남은 결손](../Changes/2026-10-27-cargo-lifecycle-runtime-r14.md). 신규 격리 Simulation DB의 같은 화물 1건을 후불 승인·배차·상하차·인수완료까지 처리하고 세 역할 제품 API 및 SQL/Mongo 원장 1개를 대조했다. 실제 SM-A346N에서 화주124·전문 기사125·운영자126의 로그인/조회·앱 재실행, 기사 도착 버튼/완료 알림, 운영자 연결 실패 보존·재조회와 완료 표시를 확인했다. 진행/완료 상태의 서버 재시작 보존도 확인했다. 전체 native 버튼 완주·실제 GPS/카메라/금융·운영 공개는 미검증이며 ready503·지도 인증 미설정과 홈 다음 행동/운영자 건수 제목/기사 하단 겹침 3개를 보완 대상으로 남겼다. 내부 6,000원 입금 요청 준비 1개·안내 예약3개와 실제 계좌 발급/입금/지급을 구분했다. 합성 기사 종료·이번 reverse/소유 서버 정리, DB/앱 데이터·다른 작업 보존. 추적 제품 코드·commit/push 변경 없음.

## 영상 일괄 실행·정확 결속·Unity 환류 r118 (2026-10-26)

- [세 부분 구현·실제 결과](../Changes/2026-10-26-video-diorama-integration-r118.md). 고정 CLI 실행/예산/잠금/원본별 재개, 정확 source/target·현재 지도/원형 결속, 새 Unity 결과의 현재 보호·실제 전후/복구와 일일 환류를 구현했다. Python214/214·Unity186/186·graphics 종료0/35PNG/5시점 exact 복구·실제11명령 종료0·최종 validate 문제0이다. 원본1/비공개대상3/단계6·새 의미 판독0·원본 재해시0·정본 동일성3보류/적용0을 유지한다. 나머지 의미 판독 미완료로 runner Held다. 최신 포인터 `배달/edit-kit/private/video-diorama-integration/current-review.json`. 아래 r115의 새 시각 검증은 이번 입력에서 보완했고 Game View/FPS·무인 위치 추론/렌더러·운영/공개/commit/push는 미실행이다.

## 앱 공통 제작 절차와 화물 마감 입력 r13 (2026-10-26)

- [적용 결과](Planning/시스템/PLAN-SYSTEM-PLANNING-IMPLEMENTATION-CHECKLIST/app-common-workflow.r13.result.md). 공통 안내·AGENTS·통합 1.0 마감을 연결하고 제작 도구의 화주/창고 역할과 현재 검증/포장 시험을 보완했다. 화물 화주·전문 기사·운영자 입력 3건과 같은 건의 검증 계획을 등록했다. 도구 97/97·실패/생략0, 생성 정합성·링크·범위 Fast와 실제 관리 브라우저를 확인했다. 입력 정보 Ready와 사람 검토 Pending·환경 Unknown을 구분하며 실제 화물 API/DB·APK/기기·운영 실행은 NotRun이다. 새 결과 근거를 과거 통과와 분리했고 기존 변경·서비스·기기와 commit/push 상태를 유지했다.

## 공통 영상 처리 절차 우선 보강 r117 (2026-10-26)

- [절차 준비 범위](Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/daily-video-common-procedure.r117.md). 사용자가 실제 처리는 나중에 몰아서 하고 공통 절차를 먼저 보강하도록 요청했다. 배달 공통 안내 r3에 자료 도착/처리 시작 구분·세 단계 시작/통과/인계 기준·공통 인계 검사·보류별 재개 조건·일괄 처리/중단/마감 규칙을 정리했다. 기존8단계/8상태·정책 JSON r2·도구 코드는 유지했다. 문서 변경이며 새 원본 처리·선분 등록·Unity·렌더·자동화는 실행하지 않았다. 실제 일괄 실행·새 Unity 환류·정본 건물 대응은 후속이다.

## 매일 영상 분석·선분·디오라마의 세 단계 인계 제안 r116 (2026-10-26)

- [절차 제안과 확인한 결손](Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/daily-video-three-stage-handoff.proposal.r116.md). 사용자는 구현을 후속으로 두고 내일부터의 원본 입력 절차 정리를 요청했다. 기존 접수/분석에서 도로 선분과 건물 관측을 나누고 정확 대상의 비공개 변경·전후/복구 결과를 그날 원본/관측에 되돌리는 방안을 제안했다. 새 상세/영상선분 Unity 결과의 일일 연결 adapter와 정확 정본 건물 대응은 남았다. 이번에는 제안 문서만 변경했으며 새 분석·Unity·자동화는 실행하지 않았다.

## 건물 높이·쇼윈도 보완 r115 — 시각 검증 전 보류 (2026-10-26)

- [직접 요청 범위](Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/building-envelope-and-glass.r115.md). 정확 GIS 원자료의 높이31.5m/8층 한 곳과 높이0/2층 두 곳을 확인하고 본체/점포 높이 분리·미확정4m 유지·opt-in 유리와 중립 셸을 구현했다. 기존 실행 시험의 최종 XML은175/175(실패/생략0)이다. 사용자가 절차 제안으로 전환해 새 graphics PNG·실제 유리 외관·Game View는 확인 전이며 새 영상 생성0·정본 반영0이다. 이전 r114 검증을 새 표현의 시각 검증으로 승계하지 않는다.

## 건물별 상세 외형 교체와 세 점포 시범 r114 (2026-10-26)

- [실제 구현·시각 검증](../Changes/2026-10-26-detailed-building-representation-r114.md). 기존 건물 ID/외관 Profile를 재사용하고 상세·중간·원형 표시와 실패/제거 복구를 소형 부품 Overlay 밖에 분리했다. 세 전면 모델은 Synty 없이 창틀·입구 가설·차양·간판 깊이를 만들고 일반 CC0 실사 표면을 일부에 연결했다. Unity143/143·최종graphics r4 종료0·32PNG/4시점exact복구·원본14+재질9파일보존·16초480프레임 MP4 디코딩을 확인했다. 최신 포인터 `배달/edit-kit/private/detailed-building-study/current-review.json`. 높이4m/정면/문/창구획 비실측, 불투명 유리·전면 한정·비동기 스트리밍/Game View/FPS 미구현·미검증이며 정본0이다. 기존 Scene/권위/승인/운영/공개/commit/push는 보존했다.

## 디오라마 재질·조명 현실감 비교 r113 (2026-10-26)

- [최종 구현·실제 시각 결과](../Changes/2026-10-26-diorama-realism-r113.md). 기존 0111·후보3·외관3의 동일 입력을 opt-in PBR/미세질감·반사·조명 사본으로 표현했다. Unity111/111·graphics r2 종료0·20PNG/두 종류 exact 복구·원본14파일 불변·16초 무음 MP4 전체 디코딩을 확인했다. 가까운 와플/맥주2시점만 실제 그림자가 보이고 전체/뚜레는 clipping 방지로 원거리 유지다. 가는 줄그림자·노면 겹침·유리 깊이·실측 높이/정면은 남았다. 최신 포인터 `배달/edit-kit/private/diorama-realism/current-review.json`. 정본0·Game View/FPS/운영/공개 미검증이며 기존 Scene·승인·원본은 보존했다. 초기 meta/직렬화 실패는 최종 성공과 분리한다.

## 영상→선분→동네 디오라마 시각 시안 r112 (2026-10-26)

- [실제 결과·대표 자료 위치](../Changes/2026-10-26-video-segment-diorama-demo-r112.md). 0111 실제 PTS 관측5·선분 후보3·공개외관3을 같은 입력으로 묶어 동네 배경에서 외관 전후·실제 제거 복구 12장과 무음 약32초 검토 MP4·HTML을 만들었다. Unity106/106·PNG/복구 동일 SHA·파생 전체 디코딩·방향/자막·cleanup을 확인했다. 원 baseline602 중 겹친3개는 깊은 preview 사본에서만 제외하고 Overture2247/원본 Scene/Prefs/승인은 보존했다. 일부 노면 줄무늬·상호 작은 표시·높이/정면 가설·정본 동일성 대응은 남았다. 연구 반영3·정본0이며 그래픽 CLI 카메라 렌더와 실제 Game View를 구분한다. 최신 비공개 포인터 `배달/edit-kit/private/video-diorama-demo/current-review.json`. 운영/공개/commit/push 없음.

## 영상 위치 판독·일일 근거 연결 보완 r111 (2026-10-26)

- [구현·시험·실제 원본 범위](../Changes/2026-10-26-video-diorama-daily-connection-r111.md). 정수 시계·읽기 결속/재개·건물 대응 후보와 관련 160/160을 검증했다. 0112 실제 PTS 사진 12장을 판독해 영화사로 진입/선분 후보를 기록하고 원본 시간표와 대조했다. 기존 접수 13개·구형 표본 761개를 r3/r4에 결속해 새 실적 중복 0·현재 입력 검증 통과를 확인했다. 현재 공간 2,247건물과 공개 원형 3개의 겹침 후보는 0으로 정본 적용을 보류한다. 기존 번호순/원본 포인터·사용자 승인·Scene·운영 DB는 보존했고 무인 위치 추론·Unity/배포·commit/push는 실행하지 않았다. 다음은 0112 미확인 창과 현재 공간 자료의 정확 건물 대응이다.

## 일일 영상 자료 누적 절차 정형화 r110 (2026-10-26)

- [현재 기준과 미해결 연결](Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/daily-video-diorama-accumulation.r110.md). 배달 작업공간의 공통 절차·단계/기록 JSON·AGENTS/기존 안내를 연결했다. 원본 SHA별 접수·실제 PTS/OCR·시간순 판독에서 선분 후보와 공개 건물 외관을 분기하고 기존 저장소·비공개 적용/복구를 재사용한다. 정의 저장과 자동 실행기 구현은 구분한다. 새 원본 분석·렌더·Unity·운영 DB·자동 감시·배포는 실행하지 않았다. r109의 실제 시험/연구 결과를 새 실적으로 중복 집계하지 않는다.

## 첫 구현 검증 — 영상 근거와 사가정 건물별 외관 보완 r109 (2026-10-26)

- [사용자가 승인한 좁은 범위](Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/video-facade-refinement.implementation.r109.md)의 상세 공간 보존·건물별 외관 입력·복구를 구현했다. Unity 98/98·Python 25/25, 공개 원형 3개의 비공개 연구·전후/복구 이미지 9장을 확인했다. 현재 Overture 2,247건물과 직접 ID 대응이 없어 정본 적용은 0건이며 높이·정면은 연구 가설이다. 실제 Game View·입력·성능 검증과 구분하고 기존 Scene·Prefs·원본·사용자 승인·운영 DB를 보존했다. [변경 기록](../Changes/2026-10-26-sagajeong-video-facade-r109.md), 로컬 포인터 `배달/edit-kit/private/diorama-facade-refinement/current-review.json`. 운영·학습·배포·commit/push 없음.

## 최신 보완 — 화물 Android 업무 복구 r124·기사 요약 후속125 (2026-10-26)

- [실제 결과·원캡처·남은 제한](../Changes/2026-10-26-cargo-android-recovery-r124.md): 화주 본인 요약·통합 기사 설정/거절·만료복귀·권한 없음 계정전환·Push 미설정 시작과 본인 운송주선 정보업무를 보완했다. 통합 APK124에서 화주 요약·본인 원장3개/상세·GUI 설정/운행/수신·거절과 만료 복귀를 확인했다. 독립 기사 후속125에서 같은 화면의 대기→운행중→종료 뒤 대기 갱신을 확인했다. 실제 휴대폰은 설치 확인과 업무 검증을 구분하며 에뮬레이터 결과를 승계하지 않는다.
- 거절 사유가 미입력으로 남던 기존 서버 누락도 안전한 분류 저장으로 보완하고 관련58개 시험을 통과했다. 과거 실제 NotProvided 사건은 보존하며 수정 서버의 새 HTTP/GUI 검증은 별도다. 최종 .NET 전체16,289통과·실패0·환경16생략, 3개 APK 빌드/입력동결/양 ABI endpoint를 확인했다. 표준 Task의 전체 Android 소비 빌드는 기존 리소스 누락으로 차단됐다. 운영·금융·지도 인증·실제GPS·외부Push·commit/push는 미수행이다. 기준·정확 범위는 [r124 설계](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-android-recovery.implementation.r124.md)·[작업지시서](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-android-recovery.r124.work-order.json)에 기록했다.

## 이전 Android 점검 — 화주·기사·운송주선업체 실제 업무 화면 (2026-10-26)

- [실행 결과·화면과 보완 항목](../Changes/2026-10-26-cargo-android-role-review.md): 최신 통합 검토 Release APK123을 별도 패키지로 빌드·기기/에뮬레이터에 데이터 보존 설치했다. 실기기는 잠금 해제가 어려워 업무 조작을 하지 않았고 실제 APK 화면 점검은 Android 에뮬레이터에서 수행했다. 소스 2,327개 변경0·완성APK endpoint/ABI/서명/해시를 확인했다. 일반 소비 솔루션 전체 빌드 복구로 표시하지 않는다.
- 화주 공개 진입→로그인·합성 의뢰2건 조회, 기사 로그인→GUI 운행 시작·위치 수신·신규배차 ON, 별도 API 시험설정→전용 추천1건·조건체크·만료 참여 차단을 확인했다. 화주 홈의 창고API403, 통합 기사 업무/차량설정·거절 연결 부재, 만료선택 유지로 복귀/재시도 실패를 찾았다. 수락·거절·상하차·완료 정상 완주·실제 GPS·지도·외부Push는 미검증이다.
- 운송주선업체의 독립 배차·수수료·업체 정산 작업공간은 현재 코드에서 찾지 못했다. 서버관리자 관제와 커뮤니티 전문참여 자격을 해당 업체 화면으로 대신하지 않는다. 독립 DriverApp 검토 APK도 빌드/에뮬레이터 설치했으나 새 검토패키지의 Firebase 초기화 오류로 업무 화면은 미검증이다. 원래 기사 앱의 동일 오류로 단정하지 않는다.
- 별도 Simulation DB·합성계정/의뢰를 사용했다. 운영 설정·원래 앱/소스·운영 DB·법적 분류·실금전·Unity·배포·commit/push는 변경하지 않았다. 서버 readiness503(시험 DB migration 이력 없음)과 새 검토패키지 설정 제한은 제품 실행 결과와 구분한다. 상세 로컬 증거는 `artifacts/local/cargo-android-role-review/20261026-r1/`에 보존한다.
- 문서 Fast(diff)·보고 링크20개·대표PNG8개 해시를 확인했다. 시험 운행은 별도 API로 수신OFF/종료 후 대기·활성운송0 재조회했으며 GUI 종료 성공으로 표시하지 않는다. 소유 시험 서버/에뮬레이터와 이번 reverse만 정리하고 공유 Docker 서비스는 보존했다. 결제·지급·수락후속0, 실제FCM미전송과 만료 후 시험DB의 공개fallback 상태를 분리 기록했다.

## 최신 화물 운영 방향 — 월정액 정보추천·관리자 준비 조회 r4 (2026-10-26)

- [단일 기준 r4](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-information-subscription.direction.r4.md): 월5,000원·10,000원 후보, 확정금액 없음, 건별 중개수수료 없음, 서버 추천·기사 수락/거절·당사자 최종 결정이라는 사용자 방향을 기록한다. 유료 정보 서비스 계획이며 월정액만으로 무상·허가 면제라고 판단하지 않는다.
- 이번 [정확한 수정 범위](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-information-subscription.r4.work-order.json)의 관리자 준비 조회 보완을 마쳤다. 현재 무료0원과 향후 후보를 구분하고 기관 회신·유효 검토가 없으면 법적 분류 확인을 보류한다. 최종 관련29개·서버 전체16,220개 통과/환경16개 생략, 계약·공통UI·서버와 별도 소비 호스트 빌드 및 390/1280px 실제 공통 화면을 확인했다. 최초 전체 소비 솔루션은 Android 리소스/Java 생성 오류로 실패해 전체 Task·APK 호환성 성공으로 표시하지 않는다. [이번 결과와 상한](../Changes/2026-10-26-cargo-information-subscription-r4.md)은 이전 검증과 구분한다.
- [기관 서면질의 초안](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-information-subscription.authority-question.r4.md)은 미발송이다. 납부 주체·세금·갱신/환불·확정금액·계약 성립/대금 경로는 미정이다. 실제 청구·입금 처리·무료/운영 설정·정산 계산·배차/금융/기관 전송·운영 DB·APK·Unity·배포·commit/push는 이번 범위에서 실행하거나 변경하지 않는다. r2 공고 구현과 r3 고정 활동권역·전용 단건 후속 설계는 보존한다.

## 최신 기사 운영 방향 — 지도와 고정 활동권역의 단건 배차 r3 (2026-10-25)

- [지도 중심 업무 설계 r3](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-map-first-workloop.design.r3.md): 사용자 답변은 집·지정 거점 기준의 고정 활동반경이다. 기사 홈은 목록 클릭 경쟁 대신 지도→업무 시작→전용 한 건 추천→현재 업무와 다음 한 건을 구분한다. 20/50km 활동 원과 현재/예상 종료 위치에서 픽업하러 갈 최대 거리는 별도 설정이다.
- 기존 모바일 지도·단건 배너·AI 적격 후보 선택은 기반이며, 조회의 20/50km는 화면 필터이고 복귀는 우대 가점이다. 고정 반경 강제·종료 임박 다음 한 건의 화면 연결은 완료로 표시하지 않는다. 상하차 끝점만 원 안인 경로의 권역 밖 우회·최신 위치/설정·추천 독점·수락 재검사를 구현 마감 기준에 넣었다.
- 이번은 코드 읽기와 canonical 설계·색인 갱신만 수행했다. 제품 코드·새 시험·APK·위치 수집·실제 배차·운영 설정·Unity·commit/push를 실행/변경하지 않았다. 아래 r2의 기존 구현·검증은 보존하고, 정보 게시·조회부터 법적 준비를 확인하는 우선 운영 범위도 유지한다.

## 최신 우선순위 — 화물 정보 공고 보완과 법적 읽기 자료 r2 (2026-10-25)

- [승인된 공고 r2](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-information-publication.implementation.r2.md)에서 독립 Mongo 초안·명시 공개·수정 후 재동의·철회·만료, 소유권·개정 충돌·원 요청 결과와 공통 MAUI/Web 화면을 보완했다. 과거 운송 원장을 동의 없이 공개하지 않으며 공고가 운송·배차·금융을 만들지 않는다. 초기 공개 지역은 기존 30동 목록이며 기본 옵션은 OFF다. [r1 감사](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-information-first-legal-review.r1.md)는 근거로 보존한다.
- 전체 Task16,210통과·환경16생략, 실제 격리 HTTP/Mongo31·합성 Client를 사용하는 실제 제품 컴포넌트 390/1280px 20검사를 통과했다. 그 뒤 공고 route가 없는 DriverApp에서 링크를 숨기는 마지막 표시 조정을 하고 최종 Fast292·소비 솔루션 빌드를 확인했다. 전체 Task와 마지막 표시 조정 뒤의 집중 검증 판본은 [변경 기록](../Changes/2026-10-25-cargo-information-publication-r2.md) 및 `artifacts/local/cargo-info-publication-r2/verification-summary.json`에서 구분한다. 미확정 요청의 관리자 해제·실제 회원 인증/휴대폰·운영 보존/파기·기관 확인·Figma는 별도다. 기존 작업·운영 설정·DB·Unity·commit/push를 변경하지 않았다.
- [법적 읽기 자료](../Legal/freight-information-platform-20261025-r1/README.md)의 한국어 PDF6쪽·동일 내용 HTML을 준비하고 전 쪽 렌더·원문8개·모바일/데스크톱 가독성을 검사했다. 현행과 2027-09-23 시행 예정·기관 질문을 구분하며 적법 판정·허가/등록 수리·운영 공개로 표시하지 않는다.

## 최신 방향 — 디오라마를 운영 지도로 사용 (2026-10-25)

- [운영 지도 방향 r9](Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/diorama-operating-map.direction.r9.md): 사용자는 디오라마가 맵이고 이를 보며 운영하는 Unity 역할을 제시했다. 모바일·데스크톱 업무 완성을 우선하면서 Unity는 공간별 상황 파악→객체 요약→기존 업무 상세 연결로 좁힌다. 기존 원장/Simulation Session을 재사용하고 모의·운영, 실제·추정 위치, 업무 완료와 애니메이션을 구분한다.
- 이번은 방향 문서만 정리했으며 Unity 추가 코드·Scene·실행·운영 연결은 재개하지 않았다. 아래의 직전 보류 기록과 r8 실행 근거는 보존한다.

## 최신 우선순위 — 모바일·데스크톱 앱 집중, Unity 후속 작업 보류 (2026-10-25)

- 사용자가 Unity 작업의 비용을 줄이고 휴대폰 앱·데스크톱 앱의 완성도에 집중하도록 방향을 정정했다. Unity의 추가 구현·실행·시각 보완은 보류한다. 기존 코드·저장·검증 자료는 보존하며 통합 앱 1.0의 실제 업무 흐름, 입력·진행·복구·역할별 화면을 우선한다. 구체적인 다음 앱 변경 범위는 기존 업무별 마감표와 현재 코드에서 정한다.
- [출고 Unity 첫 플레이 r8](Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/multi-center-fulfillment.unity.r8.md)은 중단 전에 가상 센터 비교→예약→부분 피킹→포장 준비·저장 복구를 실제로 확인했다. 관련 EditMode 46개 통과, 마지막 시험도 종료됐다. 아래 r7의 Scene 미연결 표기는 당시 상태이며 현재 구현/검증과 후속 보류는 r8을 따른다. 전체 경영 게임·운영 출고 완료는 아니다. 새 앱 기능·배포·commit/push는 이번 우선순위 정리에서 실행하지 않았다.

## 공간·계약·인력·물류의 통합 경영 게임 제안 (2026-10-25)

- [다중 센터·위치 출고 첫 구현 r7](Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/multi-center-fulfillment.implementation.r7.md): 같은 Simulation 세션의 가상 센터·선반·담당 후보→예약→위치/상품 확인·부분 피킹→포장 준비와 파일 저장/재시작 복원을 구현했다. Local 공통 Runtime·테스트 HTTP/인증·Remote adapter·한국어 Presenter를 연결했다. 집중 65, Fast 320+54, Task Simulation/Unity 전체 3,044 통과(중복 합산하지 않음). 실제 View/Scene·Play Mode는 아직 미연결·미검증이며 확정 예약의 센터 전환·피킹 후 복구는 후속이다. 운영 원장·활성 Goal·학습·배포·commit/push는 변경/실행하지 않았다. 실제 결과와 첫 소비 예는 r7을 따른다.
- [다단계 출고 제안 r6](Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/multi-center-fulfillment-decisions.proposal.r6.md)에 주문→센터 선택→위치별 재고→직원 피킹→포장/운송을 구체화했다. 내부 수행 가능성을 센터 평가에 반영하고 하위 병목 시 재선정·분할·일정 조정, 복수 센터 예약의 부분 성공과 실제 완료를 구분한다. 현재 출고 배치/피킹 계층은 기반이며 게임 연결·전체 조건 검증·일괄 예약/복구를 구현 완료로 표시하지 않는다. 문서 보완만 수행했다.
- [상세 제안 r5](Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/location-barcode-outbound-picking.proposal.r5.md)에 선반 위치 바코드·물품/로트·수량 적치 기록→주문별 재고 선택/예약→직원·위치 피킹 안내→확인·포장·인계와 불일치/부족/변경 대응을 추가했다. 일반 창고의 위치 문자열·확인 항목과 분산 물류의 위치/예약 검증을 구분해 현재 실물 스캔·통합 경로가 완성됐다고 표시하지 않는다. 이번도 문서만 보완하며 코드·DB·장치·Unity·학습·운영 실행은 없다.
- [제안 r4](Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/space-supply-workforce-logistics.proposal.r4.md)에 공간 임대·공급 계약·현장 직원 고용·근무 배치·입고/검수/적치/피킹/포장/출고 인계·배송·결산을 핵심 경영 범위로 추가했다. 음식점 직접 픽업·거점 출고·개별 화물은 서로 다른 흐름을 유지한다. 기존 공급경영/NPC 작업 배치/운영 계약과 HR 코드는 기반이지만 게임의 채용·근무·처리 능력·임대/급여/손익 연결은 미구현이다. 이번은 기획 문서만 갱신했고 코드·Scene·실제 계약/고용/금융·학습·운영은 변경하지 않았다.
- [방향 r3](Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/regional-platform-management-direction.r3.md)에 중랑구·동대문구·광진구·구리시·노원구를 무대로 한 플랫폼 운영자 경영을 중심으로 고정했다. 주행 영상·디오라마는 경영 상황의 현장 근거·표현이며 수익·품질·보수 균형의 경험을 쌓는다. 현재 지도는 중랑·동대문 기반과 광진/노원 일부이며 구리 독립 자료는 현 포인터에서 미확인이다. 지역 목표를 전 지역 준비 완료로 바꾸지 않는다.
- 사용자 최신 정정으로 직접 플레이 구현을 보류하고 [같은 동네의 영상·3D·플레이 연결 제안 r2](Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/video-diorama-play-experience.proposal.r2.md)를 작성했다. 지도 선분과 Unity 도로의 서로 다른 ID를 판본화된 관계로 잇고, 원본 SHA/실제 PTS·공간/규칙 판본·판단/결과 출처를 보존한다. 기존 영상·Synty 샘플은 있으나 선분–3D 직접 연결과 음식의 플레이어 후보 선택 기능은 미구현이다.
- [r1 학습 방향](Planning/시스템/PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/gameplay-experience-learning.proposal.r1.md)의 수익·배송 품질·참여자 보수 균형은 유지한다. 이번은 카탈로그·소스 읽기와 제안 문서만 변경했고 원본 분석·Unity 코드/Scene·앱·검토 DB·모델·학습·운영·배포·기존 승인 기획은 실행하거나 바꾸지 않았다.

## 통합 앱 1.0 목표·모바일 마감 r121 (2026-10-25)

- 사용자 정정에 따라 기존 0.0~3.5 전체 범위를 [통합 앱 1.0](../Versions/unified-1.0/README.md) 하나로 묶었다. 기존 업무 분류·저장/API 호환과 기본 기능 설정은 보존하고 [업무별 마감표](../Versions/unified-1.0/checklist.md)로 완성 여부를 관리한다. 목표 선언은 완료나 운영 활성화가 아니다.
- [이번 작업](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/mobile-app-closeout.implementation.r121.md)은 Native 화주 금융 상태 보존·미확정 테스트 결제 보호와 실제 로그인 한 열 배치다. 관련567·로그인8·전체16,159시험이 통과했고 환경 의존16개는 생략했다. 표준 Release 검토 APK0.1.122를 실제 기기에 데이터 보존 업데이트했으며 로그인·초안 복구·취소 복귀·재실행3회를 확인했다. 과거 첫 실행 native 종료의 원인 해결이나 전체1.0 공개 완료로 표시하지 않는다.
- 전용 GUID MySQL/Mongo DB에서 음식5·화물7·창고 인계/배송8검사를 통과하고 최신 서버 재시작 뒤 완료 원장의4가지 보존을 재조회했다. 제품 경로/적재 관문은 유지하고 신규 합성 사례의 선언 입력만 보완했다. 초기 실패 원장·앱 데이터는 보존한다. 표준 마감 검사와 상세 결과는 [r121 변경 기록](../Changes/2026-10-25-mobile-app-closeout-r121.md) 및 `artifacts/local/mobile-app-closeout/20261025-r121/verification-summary.json`을 따른다. 운영·실금융·외부 알림·배포·commit/push는 실행하지 않는다.

## 업무 흐름 표시 내부 정리 r120 (2026-10-24)

- 사용자의 새 구현 중단·리팩토링 요청에 따라 기존 요약/상세 화면의 동작 블록을 partial code-behind로 옮기고, 흐름 표시 계산을 회차 점검·단계·정산 helper로 분리했다. 공개 모델·문구·DOM·CSS·서버·계약·업무 동작을 유지하며 분리 전후 템플릿/동작 본문 해시가 일치한다. 공용 UI·소비 호스트 빌드, 기존440시험·격리HTTP27검사·390px 브라우저의 보류 펼치기/상세 왕복을 확인했다. 새 기능·새 시험·APK 설치는 없고 기존 별도 공백으로 중단된 공통 검사와 이전 기기 안정성 문제는 유지한다. [r120 범위와 증거](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/workflow-readiness-and-settlement.implementation.r119.md#r120--동작을-유지한-내부-정리).

## 통합 원장·시작 보류·정산 흐름 r119 (2026-10-24)

- [r119 구현](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/workflow-readiness-and-settlement.implementation.r119.md): 원장 안의 회차·업무를 묶고, 같은 회차의 실제 서버 점검만 보류로 표시한다. 부족한 역할은 회차에, 명시된 원천 연결 부족은 해당 노드에 표시한다. 정산의 지급 준비·실제 지급·이익을 구분한다. 관련440시험·격리HTTP27검사·브라우저·실제 APK 0.1.119의 보류/상세 왕복·정산 안내와 최종 여백 보정을 확인했다. 기존 별도 공백으로 중단된 공통 검사, 초기 실행 안정성과 Figma Pending은 별도 미해결로 남긴다. [화면과 검증 범위](../Changes/2026-10-24-workflow-readiness-settlement-r119.md).

## 모바일 업무 노드 연결선 r118 (2026-10-24)

- r117의 한 열 요약 노드에도 연결선을 표시했다. 생산·입고·보관 흐름과 주문은 출고에서 합쳐지고 배송·정산으로 이어진다. 기존 경로·업무·연락처 권한은 유지한다. [구현 기준](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/mobile-work-detail-contacts.implementation.r117.md)의 r118과 [실제 화면·검증](../Changes/2026-10-24-mobile-work-connectors-r118.md)을 따른다.

## 휴대폰 업무 요약·독립 상세·등록 연락처 r117 (2026-10-24)

- [r117 구현](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/mobile-work-detail-contacts.implementation.r117.md): 한 열의 요약 노드에서 같은 원장·회차의 독립 상세로 이동한다. 기존 업무에 등록된 담당·현장 연락처만 현재 전문 권한으로 조회하고, 전화 앱에는 번호만 입력한다. 관련386시험·격리HTTP21검사·브라우저·실제 별도 Android APK의 상세 이동과 더미 번호 입력창을 확인했다. 실제 통화·전문 명령·원장 변경은 없다. 초기 실행 native 종료의 원인·안정성과 기존 별도 파일 공백으로 막힌 공통 검사는 미해결이다. [실제 화면·범위](../Changes/2026-10-24-mobile-work-detail-contacts-r117.md).

## 생산부터 출고까지 업무 연결도 r116 (2026-10-24)

- [r116 구현](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/work-procedure-graph.implementation.r116.md): 기존 r115 통합 원장과 운영 체제 인계 정의를 읽기 전용 절차 노드로 연결했다. 단계 안내와 이번 회차의 확인된 참고 자료 관계를 구분하며 생산·개별 피킹 완료나 새 업무 명령을 만들지 않는다. 관련235시험·브라우저·390px 보조 렌더·실제 Android APK 노드 선택/상세를 확인했다. 격리 원장 판본은 변경되지 않았다. 시험 앱 초기화 뒤 최초 실행 1회 종료(status255) 원인·안정성은 미해결이고 공통 검사는 기존 별도 파일 공백 오류로 중단됐다.

## 통합 협업 원장·참여 약속 구현 r115 (2026-10-24)

- [r115 구현](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/integrated-cooperation-ledger.implementation.r115.md): 기존 공동운영 원장을 상위 정본으로 재사용해 회차·당사자 주문·검증한 전문 업무 계보와 공유 관계를 조회한다. 독립 시간 약속의 제안·각 당사자 수락·거절·취소 전 영향·명시 대체를 추가했다. 관련219시험·실제 격리HTTP32검사를 통과했고 별도 시험 APK의 실제 휴대폰 로그인 복귀·수락·저장·읽기 전용 취소 영향을 확인했다. 초기화 뒤 첫 실행 1회 종료의 원인·안정성은 미해결이다. 아래 r113/r114의 구현 전 설명은 당시 제안 이력이다.
- 일반 제안과 운영 담당의 회차 필수 약속을 구분한다. 같은 상위 원장 안의 일정 경합만 확인하며 HR·외부 일정·실제 작업·배차·금전과 앱 내 변경 이력을 분리한다. 실제 시험·HTTP·APK·기기 결과는 r115 문서의 새 증거를 따른다.

## 생산자·주문자 사전 참여와 약속 영향 제안 (2026-10-24)

- [r114 제안](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/advance-participation-and-promise-impact.design.r114.md): 사용자가 생산자/주문자도 사전 참여·예약하고, 취소가 상대 일정/비용/기대 이익에 주는 영향과 약속 이행 성과를 알림·화면으로 확인하는 구조를 요청했다. 기존 수요 의향·공급 초안·입고 합의/반환·취소 합의·정산/성과금과 대조해 의존관계·취소 영향 조회·명시 변경·관련자 알림·기대/실현 성과의 흐름을 제안했다.
- 사전 의향/예약과 실제 발주·생산/재고·근무·배차·지급을 분리한다. 확인 비용·약정 조건·추정 기대 손실·책임/정정은 별개이며 자동 위약금·공제·귀책/신뢰 감점은 정하지 않았다. 이번에는 문서만 보강하고 제품 코드·예약 DB·실제 알림·금전 효과·APK·운영·commit/push는 실행하지 않았다. 기존 r112 구현/93시험과 모바일 미검증 상태는 보존한다.

## 통합 앱 모바일 예약·상호 수락 제안 / 변화 조회 보완 (2026-10-24)

- [r113 제안](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/mobile-work-appointments.design.r113.md): 실제 통합 앱 모바일 검증을 우선한다. 현장 업무는 특정일 시간 약속·상호 수락·예약 확정과 실제 시작 준비를 분리하고 기사는 기존 실시간 배차를 유지하는 방안을 현재 코드와 대조했다. 주문자 참여/수령 약속, 단계별 필수 역할·선행 근거, 거절·변경·취소·충돌·복구를 제안했으며 새 예약/매칭 기능은 아직 구현하지 않았다.
- [r112 첫 보완](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/shared-outcomes.implementation.r112.md): 기존 비공개 상세에 현재 고유 관심/수락 인원·조건 동의와 회차별 전문 원천 상태를 연결했다. 관련93시험 통과(신규 서버6+UI21), 테스트 프로젝트 build 오류0·경고218. 새 판본 실제 서버/통합 앱 APK·휴대폰 화면은 아직 확인하지 않았으며 앞선 화면 증거를 승계하지 않는다. HR 예약·급여·배차·운영 DB·외부 효과·commit/push는 변경/실행하지 않았다.

## 협업 비전과 객관적 성과 제안 (2026-10-24)

- [r111 기획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/cooperation-vision-and-shared-outcomes.design.r111.md): 사용자가 사람·업무의 집적과 협업이 생활/소득 개선으로 이어지는 과정을 지속적으로 비전·수치·시각화로 보여 달라고 요청했다. 한 공간의 역할/인계·대기/비용·실제 완료·보수/수령·다음 개선을 연결하는 제안으로 정리했다. 밀집도에 따른 소득 증가는 검증할 가설이고 확정 수입으로 약속하지 않는다.
- 현재 홈 실적은 일부 완료 용달운송이며 확정 이익/실분배액 정본은 미연결이다. 성과금은 모의 재무 입력·예정/발생·실지급을 구분한다. 거래분쟁·정산 이의와 HR 고충의 연결은 별도 설계하며 신고/의견차이만으로 자동 귀책·감점·보수 삭감을 만들지 않는다. 이번에는 문서만 보완하고 코드·DB·앱·HR·보수/신뢰·배차·Unity·운영·외부 공개/알림·commit/push는 실행하지 않는다.

## HR 등록·업무 숙지·일정 신청 제안 (2026-10-24)

- [r110 기획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/hr-onboarding-work-schedule.design.r110.md): 사용자는 고정/현장 인력은 HR로 등록·관리하고 업무 절차를 익힌 뒤 날짜/시간이 정해진 작업에 신청하는 방향을 제시했다. 배송기사는 기존 배차를 유지하고 입고 처리량 및 판매에 따른 보수 조건을 신청 전에 안내하는 방안을 정리했다. 구현 전 제안이며 단가·산식·계약/지급 정책은 미정이다.
- 현재 HR 지원의 동의는 물류 업무 이해 확인이 아니고, 입고 시간창과 개별 근무 신청은 추가 연결이 필요하다. HR PerTask DTO만으로 건당 급여 완성을 주장하지 않으며 기존 시급 외 계약 검토 관문과 Simulation 정산 관문을 유지한다. 작업별 PerUnit·주문 성과금의 제한된 근거를 재사용하되 교육/행사 혜택 기록과 근무 보수를 합치지 않는다. 이번에는 문서만 보완하고 코드·DB·앱·계약·근무 배정·급여·알림·commit/push는 실행하지 않는다.

## 사람 중심 협업 매칭 제안 (2026-10-24)

- [r109 기획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/people-matching.design.r109.md): 사용자가 공간/선반 잔여량의 전산화보다 사람 연결을 우선하도록 정정했다. 함께 운영할 사람과 회차별 작업 담당자의 희망 업무·가능 시간·모집·추천·개별 수락·후속 인계·공석 재모집을 중심으로 제안했다. r108은 이전 제안으로 보존하며 제품 구현은 아직 승인되지 않았다.
- 기존 역할 참여를 특정 시간의 근무 확정이나 전문 작업 권한으로 간주하지 않는다. 먼저 한 모임의 기존 참여자로 입고검수·피킹포장·배송 담당을 연결하고 이후 신규 공개 모집과 반복 참여로 확대하는 제안이다. 이번에는 문서만 보완하며 코드·DB·앱·서버·기능 시험·실제 모집/배정·외부 발송·commit/push는 실행하지 않는다.

## 동네 공간 공동운영 순환 (2026-10-24)

- [변경·검증](../Changes/2026-10-24-community-space-cooperation-r104.md) · [구현 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/community-space-cooperation.implementation.r104.md): 공개 모집/선택 소개, 역할별 수락·현재 조건 동의, 공간 제공자의 명시 운영 수락·양측 인계, 기존 전문 원장 연결과 한 회차 결과·다음 회차를 공용 화면에 연결했다. 원장은 기존 Mongo 보호 블록과 전문 SQL의 권위를 유지한다.
- 관련537개 통과(새 공동운영66개 포함), Web/통합 Android 오류0, 실제 격리 Mongo28검사와 실제 HTTP/공용 화면의 제안·참여·동의·운영 수락·회차 준비·재시작 재조회를 확인했다. 전체 업무 순환은 별도 격리 SQLite 시험이며 UI 호스트 전문 실행은 OFF다. Fast는 기존 미관련 EOF에서 중단, 새 범위 diff는 통과했다. Figma 쓰기는 읽기 전용으로 차단됐다. 휴대폰 설치·운영 DB·실물·실지급·외부 발송·Unity·배포·commit/push는 미수행이다.

## 화물 기사 위치 전송·지도 확인 (2026-10-24)

- [동작·검증·제한](../Changes/2026-10-24-cargo-driver-tracking-r1.md) · [페이지 책임](../ProjectOverview/page-docs/cargo-driver-tracking-r1.md): 기존 GPS 전송을 명시적 운행의 계정·세션·서버·근무에 결속하고 현재 배정의 유효 위치를 화주/관리자 역할 지도에 연결했다. 지연·미수신·종료 상태, 시각·요약/상세·핀의 로컬 만료와 조회 권한을 보존한다.
- 현재32소스 변경0, 집중192통과/실패0/생략0, 기사 Android 오류0·기존 경고29를 확인했다. 최초 EF membership 식 문제는 수정 후 같은 범위를 재시험했고 최초 실패도 보존했다. Fast/Task는 기존 미관련 EOF에서 중단, 선택 범위 diff는 통과했다. 통합앱230의 합성 자료로 실제 휴대폰 화주·관리자 위치 상태/갱신 시각과 화주 종료 비공개를 확인했다. 네이버 SDK401로 지도 타일·핀·핀 이동은 미검증이며 콘솔 로그인 후 등록 확인이 남는다. 전문 기사 앱229는 유지했고 새 송신 코드 설치·실제 GPS·장시간 백그라운드·운영 DB/배차·commit/push/배포는 실행하지 않았다. 최신 근거는 `artifacts/local/cargo-driver-tracking/20261024-r1/`에 기록한다.
- 화주·관리자의 최신/지연/종료6화면을 실제 기기에서 확인했다. 기존 계정·USB5363·서버PID32404와 기사229를 보존하고 검증용 관리자 역할·시험 서버만 정리했다. 통합앱230은 데이터 보존 갱신 상태다. 최종 `validation-final.json`과 별도 `device/validation-device-final.json`은 상태 UI 확인·지도401·실제 GPS 미실행을 구분한다.

## 기사 단건 배차 추천 (2026-10-24)

- [표시·기본 정책·운영 경계](../Changes/2026-10-24-cargo-single-recommendation.md): 전문 Native 추천의 이전/다음·번호·가로 넘김을 제거하고 서버의 현재 제안 한 건만 지도/카드로 표시한다. 검토 중 같은 ID·차수는 새 조회 순서가 달라도 유지하고, 새 정본에서 종료 확인 후 다음 서버 제안을 받는다. 수락/거절·상하차 완료는 구분하며 기존 수락/표시 보호6구간은 보존했다.
- 집중221통과/실패0/생략0, 기사 Android 오류0/기존 경고29, 독립16소스 변경0이다. 기본 조율 입력·잠금을1건으로 맞추고 실제 SQLite의 기본1/잠금해제뒤다음/명시2호환·기존추천보존을 시험했다. 이 조율 batch는 운영 연결 전이며 실제 Queue 전역의 동시1건 강제는 미구현이다. 표준 Fast/Task는 기존 EOF 공백으로 중단됐다. APK229를 SM-A346N/Android16에 데이터 보존 갱신 설치·설치본 SHA 대조하고, 합성 GET 자료에서 한 건 표시·순서 변경 시 유지·현재 건 제거 뒤 전환·빈 화면을 실제 폰에서 확인했다. 실제 수락/거절·상하차0건이며 제품 DB·운영 발급 검증은 별도다.
- USB 원래 연결·기존 서버를 복원/보존하고 새 시험 서버만 종료했다. 최종16소스 변경0이며 통합 근거는 `artifacts/local/cargo-single-recommendation/20261024-r1/validation-final.json`, 단말 근거는 별도 `device/validation-device-final.json`이다. 단말 미확인 방식·만료·확인창은229에서 반복하지 않았고 원시 홈 건수/좌표 미확인/legacy 한계는 변경 기록에 보존한다.

## 기사 화물 추천 카드 간략 배치 (2026-10-24)

- [변경·실제 APK 화면·제한](../Changes/2026-10-24-cargo-compact-recommendation-cards.md): 전문 기사 추천에서 총중량/수량, 상차/하차를 좌우 비교하고 월일 일정과 전체 상세를 나눈다. 왼쪽 거절·오른쪽 독차/혼적(미확인 수락)은 기존 확인 절차이며 운송 방식 변경이나 상차 완료가 아니다. URL 거절 의도는 사유 입력만 연다.
- 집중103통과/실패0/생략0, 최종 기사 Android 오류0/기존 경고29, 최종9파일 및 전체 APK 빌드 입력 변경0이다. 표준 Fast/Task는 기존 다른 파일 EOF에서 중단됐다. 227 실제 화면에서 발견한 글자·운임/상세 잘림과 대상 전환 스크롤 잔존을 보완하고, 228을 SM-A346N/Android16에 데이터 보존 갱신 설치·설치본 SHA 대조했다. 실제 간략/상세·혼적/독차/미확인·전환/조회 후 스크롤을 확인했다.
- Native 본문 위 가로 swipe는 미인식이며 카드 이동은 상단 화살표를 사용한다. 읽기용 합성 HTTP이며 예시 좌표 없음·기존 WebView 하단 탐색 겹침·legacy 결정 화면의 사전 조건 재조회 한계는 남는다. 실제 수락/거절·상하차0건, 제품 DB 완주·운영·commit/push는 미실행이다. USB 원매핑·원래 서버를 복원/보존하고 새 시험 서버만 종료했다. 최종 근거는 `artifacts/local/cargo-compact-cards/20261024-r1/validation-final.json` 및 별도 `device/validation-device-final.json`이다.

## 화물 조건 비교 카드 r1 (2026-10-24)

- [구현·검증·APK 화면](../Changes/2026-10-24-cargo-condition-cards-r1.md) · [페이지 책임](../ProjectOverview/page-docs/cargo-condition-comparison-r1.md): 원장 조건8개를 인증된 조회에 투영하고 세 역할의 목록·현재 카드와 기사 수락 전 화면에서 같은 기준으로 표시한다. 단독은 표시만 독차, 누락은 미확인으로 유지하며 조건 변경·추천/상세 불일치 시 확인 해제·POST 차단을 추가했다. 익명 계약·기존 권한·운영 설정은 유지한다.
- 집중111통과/실패0/생략0(신규65·기존46), Contracts·공통 UI·서버·기사 Android 오류0과 최종 의존 DLL 정합을 확인했다. 표준 Fast·Task는 기존 `CargoDriverWorkSession.cs:245` EOF에서 중단됐고 선택 범위 diff는 통과했다. 실제 Razor의 로컬 합성 브라우저 세 역할·카드 링크/명시 수락과 390/320px 프레임(실제 내용380/310px)의 가로 넘침·조건 값 잘림0을 확인했다. 제품 HTTP·DB 완주·실금융·운영·commit/push/배포는 미실행이며 로컬 최종 근거는 `artifacts/local/cargo-conditions-r1/20261024-r1/validation-final.json`이다.
- 후속226 APK 두 종을 SM-A346N/Android16에 데이터 보존 갱신 설치하고 설치본 SHA를 대조했다. 읽기용 합성 HTTP로 실제 화주·운영자 조건 및 기사 목록/Native 혼적·미확인을 확인했다. 기존 하단 메뉴/시스템 막대 겹침과 Native 카드 넘김 정렬·헤더 불일치가 남아 있어 정상 완료로 합치지 않는다. 사용자 대표 검토는 APK 단말 캡처로 제공하며, 후속 기록은 `artifacts/local/cargo-conditions-device/20261024-r1/validation-device-final.json`이다.

## 화물 앱 재실행 복구·공개 탐색 r1 (2026-10-24)

- [구현·시험·휴대폰 확인](../Changes/2026-10-24-cargo-restart-guest-r1.md): `CompletedSyntheticDeviceValidation / OperationalDisabled`. 화주 작성 입력·접수/인수증 미확정 보호와 전문 기사 신고를 계정·서버별로 복원하며, 공개 화물 목록은 로그인 없이 조회하고 의뢰·배차 추천 시 기존 인증으로 인계한다. 로그인 성공은 업무 명령을 자동 실행하지 않는다.
- 집중253·전체15,510통과/실패0/환경16생략, 선택39소스 해시 변경0, 실제 브라우저의 페이지 다시 로드 후11항목을 확인했다. APK225 두 앱 데이터 보존 갱신 설치·설치 SHA일치, SM-A346N/Android16에서 공개목록·로그인 복귀·화주 초안과 기사 미확정 신고의 강제 종료 후 복원을 확인했다(신고 POST1회 유지). 합성 HTTP fixture이며 제품 DB 완주·인수증/카메라 실기기·운영/실금융·외부 푸시·commit/push는 미실행이다. 기존 기사 하단 메뉴와 시스템 막대 겹침은 남아 있다. 최종 근거는 `artifacts/local/cargo-restart-guest/20261024-r1/validation-final.json`이다.

## 화물 앱 마무리 보완 r1 (2026-10-24)

- [범위·실제 화면·남은 작업](../Changes/2026-10-24-cargo-app-closeout-r1.md): `CompletedLocalCloseoutWave1 / RemainingCloseoutWorkPending`. Web 화주 증빙에 기존 인수증 등록·동일 의뢰 결과 확인을 연결하고, 공통 상하차의 정형 미적용 거절 뒤 복구 및 전문 기사 신고의 계정/운송/화면 수명·중복 전송 보호를 보완했다. 기존 권한·사진·운송 보류·운영 비활성은 유지한다.
- 집중420·전체15,438통과/실패0/환경16생략, 신규85사례, 화주 Web·기사/통합 Android 오류0을 확인했다. 실제 재사용 컴포넌트의 루프백 브라우저에서 인수증 확인과 미확정 잠금·신고1회/운영자 안내 및 1280px 가로 넘침0을 확인했다. 표준 Fast/Task는 미관련 기존 EOF로 중단됐고 범위 diff·직접 시험/소비 빌드와 분리했다. 최종 선택 소스19개 해시 변경0이며 근거는 `artifacts/local/cargo-app-closeout/20261024-r1/validation-final.json`이다.
- 이 판본 검증 당시 Web 보호는 같은 브라우저 앱 scope, 전문 신고 보호는 현재 VM 수명이었다. 재실행 영속 보호는 위의 후속 판본에서 보완했고 요청별 신고 영수증, Native 일반 FakePG의 상태 덮기/응답 유실, 관리자 개별 상세/지급 준비·CLI guard 정합은 후속이다. 이 판본의 제품 HTTP/실DB 완주·단말 설치/카메라·실금융/외부알림·운영/배포·commit/push는 실행하지 않았다.

## 업무 단계 완료 안내 r1 (2026-10-23)

- [구현 범위·검증·실제 공통 화면](../Changes/2026-10-23-workflow-completion-feedback-r1.md): `CompletedLocalImplementationAndBuilds / PhysicalWorkflowPending`. 음식·마트 픽업/전달, 화물 상하차, 창고 입고 수령/적재/피킹/포장/인계 준비, 연습 환경의 기사 직접 피킹·전달에서 확인된 서버 결과와 재조회 뒤 완료 안내를 연결했다. 확인은 안내만 닫으며 수령 확인·정산·실제 지급이나 다음 단계 명령을 실행하지 않는다.
- 현재 화면에만 적용하는 단계별 창/짧은 안내/최종 전달만 표시와 최근 결과 최대 10개·중복 표시 방지를 제공한다. 추천·휴대폰 알림 설정과 분리하며, 새 미확정 보호 일부는 모델 수명 내 상태로 재실행 영속 보호와 구분한다. 계정·대상 전환과 이전 결과의 잘못된 완료 표시는 차단한다.
- 최종 전체15,353통과/실패0/환경16생략, 범위 한정 diff·최종 소비 빌드·실제 공통 컴포넌트 브라우저9항목/대표PNG2장을 확인했다. 최초 선택기·분산 시험 호스트의15개 실패는 보완 후 재시험했다. 표준 Fast/Task는 미관련 기존 EOF 형식 오류로 중단돼 직접 검증과 구분한다. 0.1.213 APK3종의 서명·ABI별 내장 assembly·handoff SHA256 및 빌드 전후 선택 소스 변경0을 확인했다. 새 완료창의 실제 단말 검증은 Chrome 초기 설정/삼성 인터넷 실행 자동 승인 거절로 미완료이며 자체 USB 경로는 복구했다. APK는 아직 설치하지 않았고 운영·실금융·외부 FCM·commit/push 없음. 최종 근거는 `artifacts/local/workflow-completion-feedback/20261023-r1/validation-final.json`이다.

## 음식·화물 추천 알림 구분 r1 (2026-10-23)

- [변경·화면·검증 범위](../Changes/2026-10-23-driver-recommendation-alerts-r1.md): 종류별 허용은 기본 끔이며 음식은 식기, 화물/퀵은 트럭과 각각의 업무명으로 표시한다. 설정 저장 후 본인 정본 재조회·계정 판본/수신/만료·기존 OS 차단과 무음을 유지했다.
- 두 서버 발송 경로는 종류별 허용을 대조한 data-only 힌트이며 화물 Android 로컬 게시를 연결했다. 기존 배차 수신·자격·수락·현재 수행과 알림 설정을 분리했다. 운영 플래그·공개 배포·외부 FCM·commit/push는 별도이며 시험/빌드 수치는 변경 기록의 검증 파일을 따른다.
- 최종 집중519·전체15,296통과/실패0/환경16생략, 세 앱 Android 오류0·공통 화면1280px/390px를 확인했다. 표준 Fast/Task는 미관련 기존 EOF 형식 오류로 중단됐고 직접 시험/소비 빌드로 구별했다. 음식 앱 저장 결과 미확인 동안의 허용 캐시·늦은 일반 조회 복원과 native 잠금 뒤 취소/계정 변경도 차단했다.
- 후속 Galaxy A34/Android16 실제 기기에서 음식212·화물211 설치본 SHA를 대조하고, 실제 게시기의 합성 알림 오른쪽 파란 식기/빨간 트럭과 종류·전체 끔 차단을 확인했다. OneUI 본문용 큰 아이콘과 음식 설정의 명시적 Geometry 변환을 보완했다. 음식만/화물만 설정은 앱에서 저장→격리 합성 서버의 실제 Controller/UseCase GET200으로 재확인했다. 시험 설정 모두 끔·시험 계정 로그아웃·원래 USB 연결 복구를 기록했으며 외부 FCM·알림 클릭·잠금 화면·소리/진동 검증과 구별한다. 상세는 `artifacts/local/driver-recommendation-alerts/20261023-device-r1/validation-device-final.json`이다.

## 기사 현재 업무·피킹 보수·약정 담당 확인 r1 (2026-10-23)

- [구현·대표 화면·검증 제한](../Changes/2026-10-23-integrated-driver-work-r1.md): 같은 계정의 음식·퀵/화물·직접 피킹 현재 업무를 GET으로 모아 기존 전문 수행으로 연결했다. 수락 전 작업·제안 운임/피킹 보수 확인과 포장 동의를 구별하고, 정산의 본인 조회 범위 담당/동의·지급 관계를 표시한다. 종류별 그림·기존 색·홈 진입·Web/통합 MAUI 배선을 보완했다.
- 최종 집중216·전체15,165통과/실패0/환경16생략. 통합 Web 소비 컴파일·Android 재빌드(오류0/기존경고14)와 실제 공통 컴포넌트1280px/390px·10PNG를 확인했다. 독립 검토에서 발견한 예약 상세 숨김 시 주문별 반납 책임과 허용 행동 안내를 보완하고 다시 전체 시험했다.
- 표준 Fast/Task는 기존 다른 파일 EOF 형식 오류로 diff 관문에서 중단돼 직접 시험/소비 빌드로 구분했다. 새 서버/API·원장/수락 정책·요금·후원결제·운영 설정·개인 고객정보 복제·APK 설치/실기기·실계약/수금/지급·공개 배포·commit/push는 변경/실행하지 않았다. 화면 예시와 실제 제품 HTTP·업무 완주를 구분한다.

## 생산 참여 홈 제목 r1 (2026-10-23)

- [변경·대표 화면·검증 제한](../Changes/2026-10-23-home-production-title-r1.md): 공통 홈 제목을 `생산에 참여해 보세요.`로 바꾸고 상단 브랜드 표시를 제거했다. 성과 요약·9개 역할 진입·하단 거래 안내를 유지했다. 실제 공통 UI 소비 빌드와 1280px/390px 브라우저를 확인했다.
- 변경 파일 diff 검사는 통과했다. 전체 Fast는 기존 다른 파일 `CargoDriverWorkSession.cs`의 EOF 빈 줄로 중단했고, 삭제된 assets를 복원한 좁은 소비 호스트 빌드는 통과했다. 새 시험/전체 시험·APK/단말·실금융·배포·commit/push는 이번 범위가 아니다.

## 정상 자료·예외·단독 운영 대응 리팩토링 r21 (2026-10-22)

- [구현·실행·대표 화면](../Changes/2026-10-22-cargo-operating-verification-refactor-r21.md): `CompletedLocalValidationWithFindings / OperationalDisabled`. 시험자료/실행/정본/증거, 운영 알림 조회/대응, 수납/지급 표시·조회 책임을 분리했다. 확인한 알림도 미해결 목록에 남고 확인 응답 유실은 공통 복구창→GET으로 복구한다.
- Python33·Fast115·Task15,069통과/환경16생략, 최종 소비 빌드·781px 브라우저11항목/PNG8장을 확인했다. 새5369·전용DB의 HTTP4사례는 완료3·경로미지원1이며 원실패를 보존하고 별도 팔레트판본으로 정상 검증했다. 모델 NetworkFailure는 규칙 대체로 기록했고 ready503 운영migration·실PG/계좌/송금·휴대폰/실카메라·외부발송/자동worker·CLI guards 호환은 후속이다. 기존 서버/DB·dirty1,016파일의 미관련 내용과 index를 보존, 새 commit/push 없음.

## 맥락별 커밋 정리 (2026-10-22)

- [커밋 목록과 보존 범위](../Changes/2026-10-22-context-commits.md): 앱·서버·화면·검증 기록을 맥락별로 저장했다. 이번 요청은 커밋만이며 푸시·배포는 하지 않았다. 이전의 별도 변경과 검증 실행 증거는 보존한다.

## 클라우드 제외 화물 운영 마감 r20 (2026-10-22)

- [구현·실제 검증](../Changes/2026-10-22-local-operations-closeout-r20.md): `CompletedLocalAndDeviceValidationWithFindings / OperationalDisabled`. 금액·수납/지급 분리, 선택 의뢰 신고/복귀, 정식 분쟁 지급 보호와 FakePG의 MySQL 재시도 충돌을 보완했다. 최종 Task15,018통과/실패0/환경16미실행, 신규125사례·55소스 불일치0, APK209 설치SHA·실제384px 화면3장을 확인했다.
- 두 합성 사례 중 명시 팔레트0의 한 건은 제품 API로 인수 완료했다. 실제 폰은 정상 상세·모의 결제·완료 표시·신고 복귀를 확인했으며 사진/기사 수행은 합성 API다. LLM NetworkFailure의 규칙 대체, ready503 운영 migration, 지도 사용불가, 초기 Mongo Unknown 원인, 실제 PG·계좌·송금·외부 알림·전체 복원은 별도다. 기존 서버/DB/증거를 보존했고 새5368 로컬 서버 연결만 유지했다. 로그아웃·자체 디버그 연결 제거, 클라우드 배포·commit/push 없음.


## 안내 창·화주 상세 버튼 정렬 r19 (2026-10-22)

- [정렬·실제 화면·검증](../Changes/2026-10-22-alignment-review-r19.md): `CompletedDeviceAlignmentValidation / ExistingServerReadFailuresRemain`. 공통 안내의 동폭 보조 버튼/전체 폭 재조회·제목 줄바꿈, 화주 상세의 탐색 분리/동폭 작업 버튼·짧은 안내 두 열을 보완했다. 207에서 실제42px 덮임을 발견해 좁은 선택자로 수정한 뒤 최종 Fast55·Task14,893통과/환경16미실행, APK208 데이터 보존 설치/설치SHA·384px 넘침0·상단160×48/보조148×64.111/주행동304×48·실제PNG2장을 확인했다. 닫기/재개방/재조회 후 짧은 실패 안내 유지/업무 복귀, 로그아웃/역할 홈·자체 연결 정리를 기록했다. 정상 상세/전 역할 실기기 전수·기존 서버 조회 실패·운영/금융은 남아 있으며 commit/push 없음.

## 오류 안내 창 실제 휴대폰206 반영 r18 (2026-10-22)

- [설치·실제 화면·제한](../Changes/2026-10-22-recovery-dialog-device-r18.md): `InstalledAndDeviceDialogVerified / ExistingServerReadFailuresRemain`. 통합앱206을 Galaxy A34에 데이터 보존 갱신하고 설치본/제작본 SHA 일치를 확인했다. 제작3,137입력 변경0·r17 Razor/CSS 일치, 실제 CSS384px에서 단색 아이콘/버튼3개·가로 넘침0·닫기/재개방/재조회/`/shipper` 복귀와 실제PNG2장을 기록했다. 기존5363→5365 연결/서버는 보존했고 공개요약404·의뢰상세 조회 실패는 후속이다. 합성 계정 로그아웃·역할 선택 홈 복귀, 검증용 자체 WebView 연결 제거 완료. 제품 추가 수정·신규 업무/결제·운영/외부배포·commit/push 없음.

## 업무 오류 안내 창 시각 보완 r17 (2026-10-22)

- [변경 전후·검증](../Changes/2026-10-22-recovery-dialog-visual-r17.md): `CompletedLocalVisualValidation / DeviceNotUpdated`. 공통 오류 안내 창에 단색 문서 확인/버튼 아이콘과 본문 간격·구분선·자연스러운 한국어 줄바꿈을 적용했다. 기존 문구·닫기/재조회/복귀 조건·동작은 유지한다. 최종 Fast8/8·공통 UI/독립 소비 빌드·기본/326px 창 폭의 실제 브라우저2PNG를 확인했다. 전체 Task14,893통과/환경16미실행은 마지막 CSS keep-all 두 줄 전 판본이며 최종 CSS는 별도 빌드/화면 검증했다. 실제 모바일 viewport·APK 설치·기기·운영/금융·commit/push 없음.

## 화물 선결제 접수·통신 복구 r16 (2026-10-22)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-prepayment-recovery.r16.md) · [업무 트리·화면·검증](../Changes/2026-10-22-cargo-prepayment-recovery-r16.md): `CompletedLocalValidation / ActualPGCheckoutPending / NativeRecoveryPending / OperationalDisabled`. 결제 전 접수 준비와 배차 큐/추천 차단, Simulation 승인 뒤 같은 의뢰의 배차 인계, 기사 수락·상하차 시간초과 보호 저장/재조회와 Web 대상·계정 전환을 보완했다. 최종 Fast362·Task14,893통과/환경16미실행, 제품·시험38소스 동결과 공통 Razor 가상 API 브라우저4화면을 확인했다. 실제 PG 결제창·수납/송금·제품 HTTP 종단간·단말 재실행·APK 설치·배포·commit/push는 미실행/미완료다. r15의 운임/보수·관제 수납/지급·분쟁/경합 후속은 남아 있다.

## 화물 화주·기사·운영자 한 건 재검토 r15 (2026-10-22)

- [현재 코드·가상 재현·다음 순서](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-three-role-readiness.review.r15.md): `ReviewedWithFindings / ProductUnchanged / DeploymentNotStarted`. 현재 소비 빌드·집중436통과/실패0, 별도9개 분기에서 선결제 UI/수락 순환·상하차 시간초과 안내 누락·Web 대상 전환·보수 매핑·관제 수납/정산 혼동의5유형을 재현했다. 화물 분쟁→지급 보류·전용 기사 신고/보류·관리자 선택 운송 인계·인수증 경합은 정적 조사와 구분한다. 로컬5364 live200/ready503, 다른 조회 전용 호스트 health405와 관리자 무인증401을 별도 기록했다. 제품·기존 DB·실제 폰·배포·운영·금융·commit/push는 변경/실행하지 않았다.

## 홈 반영 상태 확인·운영비 재원 비율 기획 보완 (2026-10-22)

- [r11 운영비 재원 비율](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/operating-support-contributors.r11.md#운영비-재원-비율--2026-10-22-후속-구체화): `DesignRecorded / ImplementationNotStarted`. 실제 지출 중 후원 충당과 운영자 직접 부담 금액·비율을 전체 누적 홈에, 월별·분류별 근거를 상세에 두는 기준을 추가했다. 미사용 후원·예산·실지출을 구분하고 근거 없는 직접 부담100%를 표시하지 않는다. 기존 r10/r12는 로컬 공유 화면 검증이며 최신 홈의 휴대폰 설치·실행 확인과 구분한다. 이번 작업은 문서 보완과 읽기 감사이고 제품·DB·APK 설치·서버 배포를 변경하지 않았다.

## 월50만원 네이버 클라우드 운영 조사 r14 (2026-10-22)

- [공식 요금·구성·계산](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/naver-cloud-budget.r14.md): `ResearchCompleted / EstimatePrepared / ResourcesNotCreated`. 관리형 DB 없이 한 VM의 별도 컨테이너로 구성한다. 8vCPU16GB·SSD300GB·전송200GB와 백업/Maps/GCS 등 예산 배정을 합쳐 VAT포함 약463,305원, 744h 전부 시간제 약474,479원이다. 4vCPU16GB 시작안은약340,105원이며8vCPU32GB는동일조건약533,705원으로예산초과다. 실제 처리량·콘솔 견적·자격/할인·배포·과금은 확인/실행하지 않았다. 제품 코드는 변경하지 않았다.


## 역할 앱 서버 네이버 클라우드 배포 방향 r13 (2026-10-22)

- [방향·조사·진행 순서](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/naver-cloud-role-hosting.r13.md): `DirectionSelected / MigrationPlanPrepared / DeploymentNotStarted`. 기존 .NET10 Linux Docker와 MySQL8.4/Mongo8/Caddy를 재사용해 NCP 한국 VPC 배포를 우선 검토한다. GCS·FCM은 우선 유지하고, NCP 전용 배포·사진 adapter·Ollama loopback 배치·도메인/HTTPS/백업·실기기 검증은 별도 준비한다. 코드·공식 문서만 조사했고 실제 클라우드 계정/운영 상태·견적 확정·리소스 생성·배포·이전·APK·commit/push는 하지 않았다.


## 운영 요약→내역·업무 첫 적용 r12 (2026-10-22)

- [승인 조정안](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/platform-overview-drilldown.r12.md) · [첫 구현·검증](../Changes/2026-10-22-overview-drilldown-r12.md): `CompletedWave1LocalValidation / RemainingR12WorkPending`. 공개 화물 참여 내역과 관리자 운송/지원 조회 조건 복귀, 모바일 게시판 내부 복귀를 보완했다. Task14,807통과·실패0·환경16미실행, 신규104사례·37소스 동결, 실제 읽기 HTTP·공유 Razor 브라우저·화면7장과 복구를 확인했다. 최초 route 분류 실패1건은 수정·재시험했고 기록을 보존한다. 전체 r12·외부 운영·단말 설치 완료가 아니다.

## 전체 누적 참여·이익 공유 홈 r10 (2026-10-22)

- [계획](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/home-participation.r10.md) · [명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/home-participation.r10.work-order.json) · [구현·검증](../Changes/2026-10-22-home-participation-r10.md): `CompletedLocalValidation / FinancialAggregateAndOtherBusinessCountsPending`. 홈 상단을 누적 현황으로 바꾸고 기존 역할 진입을 보존한다. 확정 이익/분배 지급액은 미집계, 화물 완료 참여는 확인된 계정의 기록 기준이며 시험 집계와 실제 운영을 구분한다. 최종Task14,703통과/실패0/환경16미실행, 실제공개HTTP·업무11표불변·브라우저4화면·서버중단/재시도복구를 확인했다. APK설치·외부운영·commit/push 없음.

## 화물3역할·음식4역할 독립 관리 r9 (2026-10-22)

- [계획](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/business-role-bundles.r9.md) · [작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/business-role-bundles.r9.work-order.json) · [구현·실제 화면](../Changes/2026-10-22-business-role-bundles-r9.md): `CompletedLocalAndDeviceGroupingValidation / MapFindingsRemain`. 같은 앱·DB·관리자 자격으로 홈 화물3·음식4·생활/창고 진입, 관리자 업무별 조회·카드·지도/선택·오류·미확정 요청 ID·복귀를 분리했다.
- Fast546·Task14,674통과/환경16미실행, 실제HTTP의 업무 필터/400/401/403·업무11테이블 불변, APK205 갱신 설치/설치SHA·384px 홈/관리자/사건복귀/로그아웃을 확인했다. 네이버401과 기존 음식 배차 지도 API의 기능 분류404는 후속이다. 신규 실물 업무·외부 운영·원장/정산 변경·commit/push 없음.

## 관계 기사 네이버 지도·위치 보호 r8 (2026-10-22)

- [계획](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/related-driver-naver-map.r8.md) · [작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/related-driver-naver-map.r8.work-order.json) · [구현·실제 화면](../Changes/2026-10-22-related-driver-naver-map-r8.md): `CompletedLocalAndDeviceFallbackValidation / NaverAuthenticationPending`. 통합앱의 네이버 지도 host와 현재 배정 이후의 최근 위치 보호를 연결하고 APK204를 데이터 보존 설치했다. 실제 단말 SDK 인증401로 타일·핀·경로 확인은 보류다.
- 집중90·호환81·Task14,631통과/환경16미실행과 v3.5·APK 빌드/설치SHA를 확인했다. 합성 화물의 서버→폰 위치 시각 자동 갱신·목록 대안·로그아웃 정리, 실제10분 경과 뒤 API 좌표 숨김을 검증했다. [주문 전 탐색·활동 인정](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/preorder-discovery-recognition.r8.md)은 설계만 준비했다. 외부 운영·새 권한·commit/push 없음.

## 단계 진행 알림 실제 휴대폰 r7 (2026-10-22)

- [계획](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/workflow-progress-device.r7.md) · [작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/workflow-progress-device.r7.work-order.json) · [결과·실제 화면](../Changes/2026-10-22-workflow-progress-device-r7.md): `CompletedDeviceSampleValidation / FullWorkflowAndHumanSoundPending`. 실제 Galaxy A34에 APK203 데이터 보존 설치, 실제 FCM7건으로 대표5단계 표시·중복/이전단계 억제·전경/배경/프로세스없음 수신·역할화면 클릭·로그아웃 정리를 확인했다. 단발 도구 사전검사14개 통과. 사용자가 지금 확인하기 어려워 소리 청취는 미확인이다. 실제 원장 변경→Outbox→폰의 신규업무 완주·27단계/전문앱 전수·잠금/절전·운영은 별도다. 제품 코드 변경·commit/push 없음. 시험 계정은 로그아웃했고 로컬 서버는 유지했다.

## 업무 단계별 진행 알림 r6 (2026-10-22)

- [승인 계획](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/workflow-progress-notifications.r6.md) · [작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/workflow-progress-notifications.r6.work-order.json) · [업무 트리·변경·검증](../Changes/2026-10-22-workflow-progress-notifications-r6.md): `CompletedLocalValidation / ExternalPushAndDevicePending`. 입고/음식/화물 27단계의 기존 Outbox 예약·관계자·기존 화면 이동을 연결한다. Fast536·Task14,588통과/환경16미실행, Android·Windows·웹·서버 소비 빌드 통과. 사진 준비·수령·정산·실지급·실제 푸시는 구분하며 운영 설정·APK 설치·commit/push는 실행하지 않았다.


## 사진 먼저 가 입고 신청 r5 (2026-10-22)

- [구현·업무 트리·화면](../Changes/2026-10-22-photo-inbound-request-r5.md) · [승인 계획](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/photo-inbound-request.r5.md) · [작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/photo-inbound-request.r5.work-order.json): `CompletedLocalValidation / DatabaseDeploymentAndNativeCameraPending`. 첫 신청은 사진만 접수하며 이름·창고·공급처는 나중에 보완한다. 본인 전용 준비 기록과 기존 정식 입고 한 건을 연결하며 실제 입고·재고·요금은 별도다.
- 소비 빌드·Fast203·Task14,468통과/환경16미실행, 관계형 DB·공통 화면6상태12렌더를 확인했다. 마지막 ARIA 표시 수정은 UI·Web 소비 빌드/렌더로 별도 검증했다. 기존 DB 적용·실제 저장소/HTTP·카메라·단말 재실행·APK 설치·운영·commit/push는 후속이다.

## 일반 입고 원요청 결과 확인 r4 (2026-10-22)

- [구현·업무 트리·화면](../Changes/2026-10-22-inbound-submission-recovery-r4.md) · [승인 계획](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/inbound-submission-recovery.r4.md) · [작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/inbound-submission-recovery.r4.work-order.json): `CompletedLocalValidation / DatabaseDeploymentAndNativeRestartPending`. 기존 입고 원장에 선택적 원요청 ID·최초 입력 지문과 작성자별 유일키를 연결하고, 응답 ID 전 유실을 본인 원요청 GET으로 확인한다. 새 client의 서버 전달형과 r3 단말 전용 관측을 구분하며404/미지원/잘못된 응답은 재제출 허용으로 바꾸지 않는다.
- 소비 솔루션 빌드·Fast180·Task14,386통과/환경16미실행. SQLite 별도 컨텍스트의 유일키/경합·최초 지문·권한과 복원, 실제 신청 화면3사례6렌더를 확인했다. 기존 DB 적용·단말 재실행·웹 프로세스 보존은 후속이다. 운영/worker·APK 설치·commit/push는 하지 않았다.

## 재실행 후 미확정 업무 복원 r3 (2026-10-21)

- [구현·업무 트리·화면](../Changes/2026-10-21-durable-workflow-recovery-r3.md) · [승인 계획](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/durable-workflow-recovery.r3.md): `CompletedLocalValidation / NativeRestartAndServerLookupPending`. 상하차·일반 입고의 명령 전 보호 기록과 재실행 후 정본 조회를 연결했다. 계정·실제 인증 판본·서버별로 분리하고 저장/삭제 검증 실패 시 완료·등록을 막는다. 사진 준비의 확정 미전송과 결과 미확정을 구분한다. 기존 사진 캐시·전문 원장·운영 비활성은 보존한다.
- 최종 소비 솔루션 빌드·Fast212·Task14,328통과/환경16미실행. 새 저장소 인스턴스/파일 IO 프로세스3회 복원과 실제 공통 컴포넌트 가상5사례·10렌더를 검증했다. 소스26개와 증거는 `artifacts/local/durable-workflow-recovery/20261021-r1/verification.json`을 따른다.
- 일반 입고 생성은 접수번호 전 유실된 결과 조회 계약이 없어 목록 수동 확인까지만 가능하다. 실제 단말 재실행, 구판/다른OS와 웹 영속 저장은 후속이다. 자동 POST·새 APK설치·외부 서버·운영·금융·commit/push는 하지 않았다.

## OS 단계·도메인 예외·업무 복구 r2 (2026-10-21)

- [구현·검증·화면](../Changes/2026-10-21-domain-workflow-recovery-r2.md) · [정상/예외 대응표](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/domain-workflow-recovery.matrix.r2.md): `CompletedLocalValidation / OtherOsAndDurableRecoveryPending`. 선택적 workflowContext(OS/단계/처리 결과)와 기존 실패/복구 행동을 서버→client→VM→역할 안내에 연결했다. 화물·화주 조회와 사진/인계/보류 guard, 상하차 완료 typed실패, 입고 저장 후 조회/미확정/대상 전환을 우선 보완했다. 전문 원장·허용 행동·사진·권한·운영 기본값은 유지했다.
- 소비 솔루션 빌드·최종 Fast286·Task14,260개 통과/환경16개 미실행. 첫 전체 분산 회귀3건은 중복 metadata를 수정한 뒤 기존 분산 전체/서버111개와 최종 전체에서 해소됐으며 최초 로그·소스를 보존했다. 실제 공통 컴포넌트 가상6사례와 Chrome390/1280px12장·대표6PNG를 연결했다. 내부 코드는 제품 본문에 출력하지 않는다.
- 다음은 앱 재실행 후 미확정 복원과 명령 결과조회/멱등 계약 → 구판·추천 수락 사유 전수 적용 → 나머지 OS 개별 화면이다. 현재 보호는 기존 VM 수명 내이며 전역 영속 보호로 과장하지 않는다. 새 USB APK설치·기기 조작·서버 재기동·외부 운영/FCM/금융·commit/push는 하지 않았다.

## 역할 앱 통신·짧은 오류 안내·업무 복귀 r1 (2026-10-21)

- [조사·변경·실제 화면](../Changes/2026-10-21-role-error-recovery-r1.md) · [계획](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/role-error-recovery.r1.md): `CompletedLocalDeviceValidation / RemainingRoleFindings`. 역할10종 대표 오류·서버 설정을 조사하고 공통 안내와 화주 상세·기사 현재 운송/상하차 조회 복구를 적용했다. 기술 원문을 짧은 창으로 바꾸며 같은 배경 오류의 반복 창·복구 후 과거 오류를 정리한다. 미확정 업무의 자동 POST 재전송·강제 복귀는 하지 않는다.
- 기존 로컬 API8건 기대 응답, 집중69·Fast366·전체14,172개 통과/환경16개 미실행과 소비 빌드, APK202두앱 기존서명/데이터보존 설치·설치SHA·제작입력3,335개를 확인했다. 실제 폰의 없는 대상/닫기/32초중복방지/다시열기/복귀·통신차단/GET재시도/자동복구9PNG를 보존했다. 같은 완료운송9·차수1·6,200원·AI Applied는 유지됐다.
- 남은 순서: 입고 POST성공/조회실패 구분 → Seller/HR모바일 endpoint → native음식 및 음식점/주문자/관리자 등의 개별 오류 → 기존 화주 홈 요약 경고. 조사·공통소비·개별적용을 구분한다. 외부 서버/FCM·실금융·운영활성화·신규 업무명령·commit/push는 하지 않았으며 USB시험연결/기존서버를 유지했다.

## 화물 업무 흐름·예외 안내 검증 r1 (2026-10-21)

- [검증·화면·보완 순서](../Changes/2026-10-21-cargo-ui-exception-audit-r1.md): `VerifiedWithFindings / ProductUnchanged`. APK201 실제 폰에서 통신 실패 안내·자동 복구 후 오류 잔존·수동 복구·없는 화주/기사 의뢰를 확인했다. 대표 PNG5장과 관측 원문을 분리했다.
- 정상 빌드·기존 13클래스184개 통과/실패·생략0. 제작 입력3,330개 SHA 일치, 같은 운송9의 인수완료·차수1·6,200원·AI Applied 유지. 신규 업무/신고/결제 요청은 없고 USB 시험 연결은 복구했다.
- 후속: 오류 정리/짧은 오류 안내 → 기사 운송보류·상차 미확정 안내 → 신고 결과 미확정/늦은 응답·기존 수락 연타 보호 → Web 의뢰/계정 전환·테스트 결제창 오류. 소스 결손과 실제 기기 재현을 구분하며 제품 수정·APK 제작/설치·외부 푸시/소리·운영·금융·commit/push는 실행하지 않았다.

## 화물 기사·화주 핵심 문구 정리 r1 (2026-10-21)

- [변경·전후 화면·검증](../Changes/2026-10-21-cargo-concise-ui-r1.md) · [계획](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-concise-ui.r1.md) · [작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-concise-ui.r1.work-order.json): `Completed / PhysicalDeviceReadbackVerified`. 현재 운송·상하차·화주 의뢰 상세/진행/증빙/결제에서 반복 설명·개발 용어·내부 번호·정상 조회 배너를 줄였다. 핵심 상태/값/행동·오류·미확정·사진·동의·테스트 결제 경계는 유지했다.
- 소비 솔루션 오류0, Fast355개 통과. Task의 기존 알림 시간 제한1건 실패를 보존하고 단독 재시험1개·같은 빌드의 전체 재시험14,139개 통과/환경16개 미실행을 확인했다. 시간 제한·제품 조건은 바꾸지 않았다.
- 두 APK0.1.201/201을 기존 서명·데이터 유지로 업데이트하고 설치 바이트 SHA와 제작 원본을 대조했다. 실제 폰에서 기존 운송9를 조회해 전후8PNG와 의뢰번호 기본접힘/펼침을 확인했다. 같은 인수완료·차수1·동결6,200원·AI Applied 유지, 신규 업무/결제/완료 요청은 없다.
- 이번 기기 증거는 완료 건의 읽기 화면이며 상하차 미완료 문구는 소스·빌드·회귀 범위다. 운영/현장·새촬영·FCM·금융·가중치학습·commit/push는 실행하지 않았다. 같은5364 수동서버를 재기동했고 기존 migration health 경고·5595검토서버/Ollama·선행 원문은 보존했다.

## 화물 AI 추천·휴대폰 수락·상하차 촬영 검증 r1 (2026-10-21)

- [결과·업무 트리·단계별 PNG](../Changes/2026-10-21-cargo-ai-phone-verification-r1.md) · [계획](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-ai-phone-verification.r1.md) · [작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-ai-phone-verification.r1.work-order.json): `CompletedSyntheticDeviceValidation / LocalOnly`. 기존 설치판200/190과 USB 단말에서 실제 제품 AI Applied → 추천 배너·6,200원 카드 → 본인 수락 → 상차/하차 사진·완료 → 화주 인수완료 재조회를 검증했다.
- 같은 phone-02 운송9의 SQL 인수완료·Mongo 완료/인수완료, 추천차수1·동결보수6,200원, 서로 다른 카메라 사진2개의 업무 결속·실제 저장 바이트/SHA를 대조했다. 서버 proof의 단말 미인증 값은 보존하고 실제 PNG를 별도 연결했다. 단말은 실제, 업무/위치/경로는 합성이다.
- 시험 호스트에 제품 AI 명시 프로필·현재 감사 재조회·추천 저장 판정을 연결했다. 기존 DB nullable10열만 추가하고 합성 차량 제원1행을 보완했다. 첫 후보없음 사례와 기존 원장은 보존했다. 최신 primary23·legacy31개 검사와 경고/오류0 빌드를 확인했다. 제품 전체 시험은 이번에 다시 실행하지 않았다.
- 기사 지도→현재운송 동선의 첫 전환 미확인, 화주 개발 문구·증빙 원장별 안내, 기존 DB migration health는 후속이다. 실제 이동/GPS·외부FCM/소리·실수금/지급·가중치학습·클라우드·새APK설치·commit/push는 하지 않았다. 기존5595검토서버/Ollama는 유지하며 이번5364수동호스트에 자동수락·완료배우는 없다.

## 화물 추천 약속 검사·AI 선택·안전 복귀 구현 r1 (2026-10-21)

- [구현과 검증](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-recommendation-default.implementation.r1.md) · [작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-recommendation-default.r1.work-order.json). 사용자 승인에 따라 대기 기사도 실제 전체 일정과 픽업·전달 약속을 검사하고, 서버가 허용한 화물 후보 안에서 Ollama가 선택하도록 연결했다. 모델 응답 뒤 현재 후보·의뢰를 다시 확인하며 지연·무효·혼잡은 현재 규칙으로 복귀한다.
- 운영 기본 OFF와 기사 본인의 수락을 유지한다. 기존 추천의 지도/목록을 재사용하며 공개 콜·음식/피킹 혼합·다건 진입을 새로 켜지 않는다. 다건 저장 재시도는 연결 전 안전 조건으로 별도 검증한다. 실제 실행·시험 수치와 남은 제한은 연결 문서의 최신 완료 기록을 따른다.
- 최종 전체 Task 14,139개·도구 자체 검사 72개 통과, 환경 의존 16개 미실행이다. 새 단건 프로필 2건에서 실제 정본 감사의 `Timeout` 복귀와 `Applied` 추천 저장을 각각 확인하고 모두 사진 픽업·전달 완료까지 마쳤다. 다건 commit 응답 유실과 기존 추적 행의 결합 회귀 11개도 통과했다. 실제 운영·기기·외부 푸시·가중치 학습·commit/push는 이번 범위가 아니다.

## 화물 우선 추천 기본·목록 조회 구조 검토 r1 (2026-10-21)

- [코드 검토·다음 순서](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-recommendation-default.review.r1.md): `DirectionAccepted / SourceAudited / PrioritiesSet / ProductUnchanged`. 사용자 방향은 AI 추천 기본·목록 보조·화물 우선이다. 같은 추천의 지도/목록 보기는 이미 있고 자율 공개 콜은 별도 동의/수락 경계다.
- 화물 실제 선택은 규칙이며 Ollama는 비교다. 대기 기사/수행 기사 픽업 시간 검사 차이, 훈련 응답 model/done 검증 누락, 다건 transaction의 잠재 재시도 충돌을 정적으로 확인했다. 공개 콜에는 보수/차량 판정·상세 접근·추천 판본의 추가 연결 결손이 있다. 현재 가동 경로의 재현 장애나 실제 부정 배차로 과장하지 않는다.
- 순서: 추천 전 약속·비교 자료 검증 → 화물 한 건 모델 선택/현재 규칙 복귀 → 기본 추천/보조 목록 사용 검증 → 여러 화물 조합. 음식·피킹 통합은 후속이다. 제품·설정·코드·실행 중 합성 캠페인/기존 검토는 변경하지 않았으며 이번 새 build/test·기기·렌더·모델 호출·commit/push는 없다.

## 배달 중 로컬 가상 운영 누적·대표 검토 r1 (2026-10-21)

- [캠페인 구현·검증](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/local-operating-campaign.r1.md) · [작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/local-operating-campaign.r1.work-order.json): `CompletedLocalValidation / SyntheticLocalOnly`. 기존 30분·15슬롯 시험기에 유한 관측·60초 corpus 접수·불명 명령 재전송 방지·본 회차의 신규 발생 종료를 연결했다. 별도 주문/배송 엔진은 만들지 않았다.
- 최대 5개 대표 검토표가 미확인·업무 문제·모델 비교·정상 결과와 후보 거리/대기 가점/총점·원문을 연결한다. 목록 번호와 당시 근거를 불변 packet에 고정하며 반복 관측 수를 독립 학습량으로 표시하지 않는다. 캠페인 31·corpus/선별 53·호스트 자체 검사 28개 및 경고/오류 0 도구 빌드가 통과했다.
- 전용 합성 DB의 11개 nullable 컬럼 누락, MySQL 재시도/transaction 충돌과 시험 입력/경로·차량 경고 확인을 보완했다. 제품 점수·수락 보호 조건은 유지했다. Fast 74·Task 13,993개 통과·16개 미실행, 소비 솔루션 빌드가 확인됐다. 기존 원문·실패/미확정 회차·5595 검토 자료는 보존했다.
- 새로 검증한 합성 회차 두 개에서 각각 동일 주문의 수락·공개 동의·픽업 사진/완료·전달 사진/완료와 서버 정본 완료 1·미완료 0을 확인했다. 그중 두 번째는 첫 추천 수락부터 완료까지 거부 없이 진행했다. 이후 새 2,200초 캠페인을 실행 중이며 현재 상태는 `artifacts/local/dispatch-operating-campaign/20261021-r1/current-campaign.json`, 확인 시점·수량은 `runtime-verification.json`을 따른다. 전체 15건 회차 완료를 미리 주장하지 않는다.
- 검토 시작점은 `artifacts/local/dispatch-experiences/current-representatives.md`다. 실제 화물 선택은 규칙이며 Ollama는 비교 전용이고 현재 비교 시간 초과가 기록된다. 사람 승인·가중치 학습·실제 운영·외부 푸시·휴대폰·정산/입금·클라우드·commit/push는 실행하지 않았다.

## 로컬 AI 시험실 실행·브라우저 검증 r1 (2026-10-21)

- [직접 시작·반복 시험 안내](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/local-ai-lab.guide.r1.md): `Running / BrowserModelRoundTripVerified / HumanReviewPending`. 사용자가 직접 시작한 5595 검토 서버와 12681 Ollama를 확인했다. 별도 자료 33개 중 학습 전 검토 사례 한 건을 브라우저 버튼으로 실제 비교했다.
- C1/C2 ETA 5분·대기 35/5분 사례에서 모델은 13,960ms 후 C1과 입력에 없는 PLAN_VERIFIED를 제시해 거부됐다. 기존 규칙 C1은 보존됐으며, 원응답/오류/시간 표시와 새로고침 후 지속을 확인했다. 모델 사건 1개·사람 검토 0개, dataset SHA 일치다. 현재 실행·화면 증거는 `artifacts/local/local-cargo-ai-lab/current-runtime.json`과 `runtime/20261021-r1/`이다.
- 초기 자동 시작 거절은 과거 사실로 보존하고 정책을 우회하지 않았다. 현재는 사용자가 시작한 두 서버와 검토 탭을 그대로 유지한다. 제품 코드/설정·운영 DB·이전 경험/검토·모델 가중치·실제 배차·commit/push는 변경하지 않았다. 다음은 사례별 사용자 답안 검토다.

## 서버 AI 배차·학습 연결 검증 r1 (2026-10-21)

- [소스·실행 검토와 다음 순서](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/server-ai-learning-readiness.review.r1.md): `Reviewed / GapsIdentified / WeightTrainingNotStarted`. 음식 후보의 AI 선택·규칙 복귀는 연결돼 있지만 화물·퀵 실제 선택은 규칙이고 훈련 Ollama는 비교다. 서버 배포·새 경험→학습 답안 연결은 미완결이다.
- 기존 corpus 366관측 중 예산 생략 349·단일 후보 5·시간 초과 12, 유효 AI 성공 0·사람 검토 0을 확인했다. 11경험을 학습 가능한 11답안으로 표시하지 않는다. 새 완전 입력·대표 사례·기존 검토 adapter가 우선이다.
- 기존 모델을 잠시 실행해 합성 3건을 비교했다. 11~15초, 세 건 모두 사실에 없는 PLAN_VERIFIED 주장으로 거부; 모델 중지 시에도 규칙 결과와 timeout을 분리했다. 음식 관련 시험 174·화물 검토 자체 검사 34개 통과. 모델 컨테이너는 원래 중지 상태로 복구했으며 기존 corpus·제품 코드/설정·운영 DB·학습/배포·commit/push는 변경하지 않았다.

## 배차 운영 경험 누적 r1 (2026-10-21)

- [누적 구조·결과](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/operating-experience-accumulation.r1.md) · [승인 범위](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/operating-experience-accumulation.r1.work-order.json): `CompletedLocalValidation / SyntheticLocalOnly`. 기존 가상 운영 3회차를 11건의 접수 시도 경험(접수 확인·완료 관측 9건, 미확정 2건)으로 접수했다. 예약 13·생략 21슬롯은 별도로 집계한다. 기존 API·운영 DB·훈련 session·사람 검토/approved 자료는 변경하지 않았다.
- 예약·생략 슬롯과 실제 접수 시도 경험을 구분하고, 같은 원문 재접수·재시작은 중복 추가하지 않는다. 명시적 검토는 판본/revision에 결속하며 새 관측으로 과거 승인을 자동 전파하지 않는다. 원문에 없는 조건·단계 시각·사진/정산·모델 해시는 미확인이다.
- 명시적 도구 빌드, 새 자체 검사 27개·CLI 12개·기존 훈련 14개와 원본/기존 검토 파일 해시 보존을 확인했다. scoped Fast/Task는 eng guidance 분류로 시험을 생략하여 위 별도 실행 검증과 구분했다. 실제 배차·가중치 학습·자동 수집·자동 승인·새 UI·기기·commit/push는 하지 않았다. 다음은 미확인·예외 사례의 사용자 문답 검토다.

## 운영 앱·서버 후속 리팩토링 r3 — 1·2번 완료 (2026-10-21)

- [구현·검증](Planning/공통/PLAN-OPERATIONS-REFACTOR-SRP/implementation.r3.md) · [범위 명세](Planning/공통/PLAN-OPERATIONS-REFACTOR-SRP/implementation-work-order.r3.json): `CompletedLocalValidation`. 사용자 “계속진행”에 따라 첨부파일 선택/순차 업로드와 푸시 설치 HTTP 전송을 분리했다. 나머지 12개 조사 후보는 후속 제안이다.
- 최종 Fast 111개·Task 13,989개 통과/실패 0개, Task 16개 미실행과 소비 솔루션 빌드를 확인했다. 기존 화면·문구·계정/토큰·취소/저장·요청/응답 해제 순서를 유지했다.
- 실제 화면/기기·외부 푸시·운영 연결·금전·APK 설치·stage/commit/push는 실행하지 않았다. 기존 경고와 별도 운영 준비를 로컬 시험 성공으로 덮지 않는다. 다음 제안은 마트 기사 보류 명령·결과 확인 수명 분리다.

## 운영 앱·서버 후속 구조 조사 r3 (2026-10-21, 제안만)

- 이번 요청은 기능 변경 없는 단일 책임 재조사와 다음 순서 제안이다. [조사 보고서](Planning/공통/PLAN-OPERATIONS-REFACTOR-SRP/follow-up-survey.r3.md) · [14개 후보 명세](Planning/공통/PLAN-OPERATIONS-REFACTOR-SRP/follow-up-work-order.r3.json): `ProposedOnly / ImplementationNotAuthorized`.
- 소스 8,723개·85개 프로젝트를 구조 색인하고 후보 본문/회귀를 대조했다. 이전 43개 첫 경계와 중복을 제외했으며 r2 완료 소스 대비 추가/삭제/변경은 0개다. 모든 메서드나 런타임 전수 검증은 아니다.
- 권고 순서: 공통 첨부 업로드 → 푸시 설치 HTTP → 마트 기사 보류/조회 → 주간 명세 계산 → 입고 완료 구성. 기존 계정·저장·시계/취소·후속 순서를 보존하고 이후 9개는 검증 공백/기기 위험에 따라 진행한다.
- 이번에는 문서와 로컬 조사 산출물만 작성했다. 제품·설정·시험 코드, build/test·DB/API/모델·APK/기기·stage/commit/push는 실행하지 않았다. 이전 시험 결과는 재실행 결과와 구분한다.

## 운영 코드 후속 리팩토링 r2 (2026-10-20, 21개 첫 책임 경계·로컬 검증 완료)

- 사용자가 후속 순서대로 리팩토링을 계속하도록 요청했다. [구현 현황](Planning/공통/PLAN-OPERATIONS-REFACTOR-SRP/implementation.r2.md) · [항목별 작업 명세](Planning/공통/PLAN-OPERATIONS-REFACTOR-SRP/implementation-work-order.r2.json)가 현재 상태를 소유한다. 직전 조사 판본과 완료된 22개 기록은 보존한다.
- 선정 21개 항목의 첫 책임 경계를 정리했고 최종 Task 13967개 통과·실패 0개·건너뜀 16개와 소비 빌드/명시 Android 컴파일을 확인했다. 화면·업무 정책·저장/예외 순서·공개 계약·기능 기본값은 유지했다. 실기기/운영·commit/push는 이번에 실행하지 않았다.

## 운영 코드 리팩토링 재조사 r2 (2026-10-20, 조사·제안만)

- 기존 22개 완료 뒤 현재 소스 8,672개·프로젝트 85개를 다시 구조 집계하고 운영 서버·역할 UI·공통/검증 도구의 직접 후보를 검토했다. [조사 정본](Planning/공통/PLAN-OPERATIONS-REFACTOR-SRP/follow-up-survey.r2.md) · [21개 후속 후보와 순서](Planning/공통/PLAN-OPERATIONS-REFACTOR-SRP/follow-up-work-order.r2.json): `ProposedOnly`. 전수 구조 집계와 모든 소스의 의미/런타임 전수 검증은 구분한다.
- 권고 순서는 입고 검수 계산 → 7개 역할 토큰 저장 → 배차 자료/점수/삽입 계산 → 화면 조회·명령 수명 → 공통 복구/명세/출력 → SDK·검토·정보·도구다. 기존 정책·저장/예외 순서·화면을 보존하며, 기존 원복/Mongo replay/늦은 응답 보호의 동작 개선은 분리한다.
- 제품 코드는 수정하지 않았다. 조사 원자료는 `artifacts/local/refactoring-survey/20261020-r2/`에 있으며 소스 해시·근거 경로/줄·JSON·문서 링크/diff를 정적으로 확인한다. build/test·DB/API·모델·APK/기기·stage/commit/push는 실행하지 않는다. 이전 통합 시험 성공을 이번 조사에서 재실행한 결과로 보고하지 않는다.

## 운영 앱과 서버 단일 책임 리팩토링 (2026-10-20, 리팩토링·로컬 통합 검증 완료)

- 사용자가 조사 후보 22개를 순서대로 모두 구현하도록 승인했다. [계획과 단계별 결과](Planning/공통/PLAN-OPERATIONS-REFACTOR-SRP/README.md) · [범위·보존 조건](Planning/공통/PLAN-OPERATIONS-REFACTOR-SRP/work-order.json). 기존 dirty 변경을 보존하고 단계별 검증 후 다음 영역으로 진행한다.
- 1단계 화주 접수/복구는 전체 소비 솔루션 빌드·167/167 시험, 2단계 AI 후보 준비/평가/순위와 모델 응답 처리는 서버/소비 빌드·308/308 시험을 통과했다(`artifacts/local/validation/20261020-164011/`, `20261020-165431/`). 3단계 추천 요금 준비·발송/비용·원천 메타데이터/실행 인덱스도 서버/소비 빌드·202/202 시험을 통과했다(`artifacts/local/validation/20261020-170700/`). 4단계 역할 앱은 코드 지도·전체 소비 솔루션 빌드와 1,332/1,332 시험을 통과했다(`artifacts/local/validation/20261020-171921/`). 5단계 주문 조회/관계·Outbox·창고 재위탁/분산명령도 서버/소비 빌드·코드 지도·578/578 시험을 통과했다(`artifacts/local/validation/20261020-173203/`). 6단계 공통 지급 준비·운영자 조회/모의 지급/요금 편집은 서버·관리자 소비 빌드와 302/302 시험을 통과했다(`artifacts/local/validation/20261020-174430/`). 7단계 커뮤니티 지도/작성·전문 상태 표현은 공통 UI/웹 소비 빌드와 380/380 시험을 통과했다(`artifacts/local/validation/20261020-175716/`). 8단계 DI 조립·유지관리 CLI·두 호스트 주소 조회 공유도 서버/FoodApi 소비 빌드와 128/128 시험을 통과했다(`artifacts/local/validation/20261020-180734/`). 22개 항목과 최종 Task 통합 검증을 마쳤다. 전체 소비 솔루션 오류0·경고145, 전체 시험 13,585개 통과·실패0·Mongo/MySQL 실환경 16개 건너뜀, 별도 FoodApi 경고/오류0, 코드 지도와 검증 전후 해시 확인을 통과했다(`artifacts/local/validation/20261020-181018/`). 최종 상세 결과와 기존 동작 개선 후보는 계획 문서가 소유한다. 기준선·상세 진행은 `artifacts/local/refactoring-implementation/20261020-r1/`에 기록한다. 구현·시험·UI/기기·운영 증거를 분리하고 아직 검증하지 않은 후속 단계는 완료로 표시하지 않는다.
- 공개 계약·요청 ID/원문/revision·DB 스키마·배차/요금 정책·기능/worker OFF와 실제 운영·모델 학습·영상/Unity 경계를 유지한다. commit/push는 요청받지 않았다.

## 화물 배차 사례 검토 책임 분리 (2026-10-20, 로컬 검증 완료·사람 승인 대기)

- 사용자 요청에 따라 [CargoTrainingStore](../../eng/CargoRegionalTrainingReview/CargoTrainingStore.cs)를 345줄에서 174줄로 줄였다. 사례 기준선·출처 판본은 [DatasetEvidence](../../eng/CargoRegionalTrainingReview/CargoTrainingDatasetEvidence.cs), 파일 잠금·이력 입출력은 [EventLog](../../eng/CargoRegionalTrainingReview/CargoTrainingEventLog.cs), 모델 비교 실행·결과 근거 검증은 [ModelComparison](../../eng/CargoRegionalTrainingReview/CargoTrainingModelComparison.cs)이 담당한다. Store는 기존 조회·사람 검토·승인 내보내기 절차를 조율한다. public API·JSON·오류 코드·잠금 범위·배차 조건·화면은 유지했다.
- 검토 도구 직접 빌드와 격리 self-check **34/34 통과**, 기존 **33사례 전체 snapshot 동일**을 확인했다. 새 소스 3개의 변조를 기본/계획 모드에서 각각 차단하는 검사를 추가했다. 명시 경로 Fast·Task는 해당 eng 도구를 안내 경로로 분류하여 diff 검사만 수행하므로, 직접 빌드·self-check·전후 대조를 별도 실행했다. 최종 증거는 `artifacts/local/cargo-training-srp/20261020-r1/`의 `build-final.log`, `self-check.json`, `case-compatibility.json`이다.
- 소스·실행 DLL 해시가 바뀌어 새 정책/자료 판본을 생성했으며 사례 입력·계획 해시와 판정은 보존했다. 기존 `dispatch-human-review/20261020-r1/batch-01-pending.json`은 변경하지 않았고 이전 승인을 새 판본으로 승계하지 않는다. 자동 시험의 모의 승인과 실제 사람 승인은 구분하며 **실제 사람 승인 0·모델 네트워크 호출 0·추가 학습 false·운영 적용 false**다. UI/휴대폰·APK·실제 Ollama 추론·운영 DB·commit/push는 이번 검증 범위가 아니다.

## 화물 입력 완결·이동 중 추천 갱신 r77 (2026-10-19, 로컬 검증 완료·운영 미활성)

- 직전 제안 1·2번의 사용자 승인을 받아 생활배송 포장 입력·화주 상하차시간·관리자 실차 확인과 추천 조회·수락 전후의 본인 추천 재검증을 연결했다. [승인 계획](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-input-refresh.implementation.r77.md) · [소유 경로](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-input-refresh.r77.work-order.json) · [변경·검증·화면](../Changes/2026-10-19-cargo-input-refresh-r77.md).
- 최종 소비 솔루션 빌드·전체 시험 13,165개 통과/실패 0개·환경 의존 16개 생략, 실제 Razor 18상태·대표 PNG 10장 및 해시를 보존했다. nullable 선택 표시·음수 경고와 전체 시험에서 발견한 EF 조회식 오류를 고쳤다. 구형 추천을 새 검증으로 승격하지 않으며 상시 GPS 재추천은 별도다.
- 운영 DB·feature/worker·기기·클라우드·모델 학습·commit/push는 별도다. r76 모델 시간 초과를 이번 변경으로 해결한 것은 아니다.

## 화물 배차 우선순위 2~6 연결 r76 (2026-10-19, 로컬 검증 완료·실제 모델 판단 보류)

- 사용자가 r75 다음 우선순위들을 계속 진행하도록 승인했다. [승인 계획](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-dispatch-evidence.implementation.r76.md) · [작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-dispatch-evidence.r76.work-order.json) · [구현/검증 기록](../Changes/2026-10-19-cargo-dispatch-evidence-r76.md).
- 기존 기사별 실차 근거, 같은 시각의 상태 지문, 전체 수행 계획 검증과 가능한 계획의 AI/사람 검토, 추천 저장/수락 최신 재확인을 연결한다. 실제 DB 적용·운영 활성화·가중치 학습·기기 설치·commit/push는 별도이며 미정 수치 정책을 임의로 확정하지 않는다.
- 전체 소비 솔루션 빌드 및 Task 13,103개 통과·실패 0개·환경 의존 16개 생략, 집중 81개·검토 도구 33개·실제 Razor 화면 6개 검사를 통과했다. 실제 Ollama 비교 2회는 20초 제한에서 보류됐고 사람 승인 0개다. 유효 모델 판단 검증은 남아 있다.

## 화물 입력·추천·접수·배차 조건 일치 r75 (2026-10-19, 로컬 검증 완료·운영 미활성)

- 사용자가 개발 우선순위 r74의 1번 진행을 승인했다. [승인 계획](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-loading-consistency.implementation.r75.md) · [허용 경로와 분담](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-loading-consistency.r75.work-order.json). 같은 포장의 치수×수량, 총중량, 입력/유효 CBM과 근거, 회전·적층을 추천→단건/일괄 접수→수정·재조회→Mongo/SQL→기사 판정에 연결한다.
- [변경·화면·검증 결과](../Changes/2026-10-19-cargo-loading-consistency-r75.md). App/Web의 현재 입력 서버 추천과 오래된 응답 보호, 일괄 담당자·약속 보존을 보완했다. 집중 1,182개·전체 13,017개 통과/실패 0개·환경 의존 16개 생략, 전체 소비 솔루션 빌드와 실제 Razor 화면 11개 검사·11장 캡처를 확인했다. 신규 nullable 5열 migration은 준비만 했으며 실제 DB 적용·운영/worker 활성화·모델 학습·기기/APK 설치·commit/push는 범위 밖이다. r74의 2~6번은 후속이다.

## 화물 접수부터 조합 결정까지의 개발 순서 (2026-10-19, 기획 제안)

- [개발 우선순위 r74](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/배차-사례-문답검토절차.md#2026-10-19-개발-우선순위-r74--접수부터-조합-결정까지). 입력·원장·판정 일치를 먼저 닫고, 실제 차량/장비와 주문/기사 상태를 연결한 뒤 가능한 조합을 계산하고 AI 비교·최신 재확인/수락으로 이어가는 6단계다. 기존 구성 재사용과 약속 준수 원칙을 유지한다.
- 이번은 우선순위 제안과 문서 정리다. 새 제품 구현·제원 입력·DB·모델·시험/기기·운영 상태를 변경하지 않으며, 조합 대기·우회 상한·최대 묶음 수·가중치는 문답 대기다.

## 소형차 적재 제원 보완 r73 (2026-10-19, 로컬 검증 완료·운영 미활성)

- [승인 계획](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/small-vehicle-loading-specs.implementation.r73.md) · [변경·검증 기록](../Changes/2026-10-19-small-vehicle-loading-specs-r73.md). 오토바이·다마스 미확인 템플릿을 추천 OFF로 추가하고 기존 실측 설정을 보존한다. 기존 관리자 API에 물리 제원 수정과 저장 전 상한 검사를 연결한다.
- 요구 축의 기준이 없거나 기사 제원이 없으면 적합으로 통과하지 않는다. 중량·CBM 물리 상한을 보존한다. 실제 모델 수치 확인·운영 DB 적용·기사별 장비 식별·UI/APK·학습·운영 활성화는 별도다.
- 소비 솔루션 빌드·집중 89개, 시험 환경 보완 후 집중 115개와 최종 Task 12,929개 통과·실패 0개·환경 의존 16개 생략을 확인했다. 미확인 템플릿의 요구값 없는 배차 우회와 실제 물리 부피/면적의 반올림 상향도 차단했다. 전체 시험에서 드러난 EF 준비 객체 누적은 시험별 고유 DB 이름을 유지하면서 root를 재사용해 해결했고 경고를 억제하지 않았다. 운영·실차·기기·모델 학습·commit/push 증거는 아니다.

## 화물 조건→차량 선정→AI 조합 입력 조사 (2026-10-19, 정적 조사·후속 제안)

- [코드 조사 결과](../Changes/2026-10-19-cargo-vehicle-selection-ai-input-audit.md). 기존 적재 엔진이 중량·CBM·치수·팔레트·온도/장비를 판정하며 서버 화주 추천과 기사 적합성에서 재사용됨을 확인했다. 일반 차량제원 초기 자료는 1~5톤급 22코드이며 오토바이/다마스 운임표와 적재 제원은 다르다. 현재 DB·실차 제원을 조회한 것은 아니다.
- 모바일 간이 추천과 상세 추천의 차이, 추정 CBM/회전·적층 조건의 접수·배차 보존 공백, 제원 누락 시 검증불가와 적합 혼용, AI 입력에 구간별 적재/순서 계획이 없는 점을 확인했다. 먼저 화물 조건·차량 제원·판정 근거를 일치시킨 뒤 가능한 조합을 AI가 비교하는 방향을 제안했다. 제품·설정·DB·모델·APK·운영·commit/push는 변경하지 않았고 새 시험 실행은 없다.

## 전국 가상 화물 AI 문답 검토 (2026-10-19, 사용자 검토 우선·주문 조합 기획으로 이동)

- [문답 기준](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/배차-사례-문답검토절차.md#2026-10-19-전국-가상-화물--사용자-문답-검토-우선). 사용자가 한 사례씩 적절성을 판단한 답안과 확인한 정정 답안을 중심으로 학습 후보를 모은다. 무응답·보류·코덱스 초안을 승인 자료로 만들지 않으며, 문답 기록·실제 추가 학습·평가·운영 적용을 구분한다.
- 가상 대전→청주 첫 사례의 조건과 B 우선 제안만 `artifacts/local/cargo-national-dialogue-review/20261019-r1/case-001.json`에 기록했다. 사람 승인·학습 후보 편입·엔진/모델 실행은 0건이다. 기존 r67 검토 원장·모델·제품 코드·운영 DB·APK·commit/push는 변경하지 않는다.
- 단건 조건 반복 대신 차량/업무에 맞는 여러 주문의 조합·수행 순서와 당사자별 이득을 문답 주제로 옮겼다. [조합 문답 기준](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/배차-사례-문답검토절차.md#주문-조합수행-순서로-문답-초점-이동)에 확인 방향과 제안 절차를 구분했다. 기존 약속·보수·선택 업무와 기사 수락을 보존하며, 5분 추가 우회의 허용 여부는 첫 미결정 질문이다. 교차 업무 혼합·새 단가·허용시간·가중치·학습/운영 적용을 확정하지 않았다.

## 화주 모바일 맥락별 입력 r72 (2026-10-19, APK200·실기기 입력/추천/수락/상하차 촬영 완료, 로컬 합성 검증)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/mobile-context-authoring.implementation.r72.md) · [단계와 결과](../Changes/2026-10-19-mobile-context-authoring-r72.md). 같은 운송 초안을 물품→포장→출발지→도착지→일정→차량→비용→최종 확인으로 나누고 이전/다음·현재 단계 필수값 확인·보완 링크를 연결했다. PC 통합 입력·기존 4단계·접수/동의/중복 보호는 보존한다. 실제 브라우저에서 발견한 날짜 변환 오류도 작은 공통 입력으로 보완했다.
- USB 승인 기기와 전용 합성 서버를 사용한다. 기존 시험 DB에 필요한 r71 nullable 2열·전문표 1개만 추가하고, 기사 본인 프로필 GET의 기능 분류를 보완해 실제 200을 확인했다. live200과 ready503은 구분한다. 최종 시험·APK·기기 결과는 연결된 변경 기록을 정본으로 사용한다. 실제 운영·금융·외부 FCM·공개 배포·Unity·영상·commit/push는 이번 범위가 아니다.
- 최종 Fast-r8 330개·Task-r3 전체 12,884개 통과, 실패 0개·환경 의존 16개 생략과 소비 솔루션 빌드를 확인했다. APK0.1.200/code200 교체 설치·설치 바이트 일치, 실제 휴대폰 8단계·초안 유지·17캡처를 확인했다. 첫 의뢰의 부피 누락 차단은 보존하고, 명시한 부피의 새 UI 의뢰에서 별도 API 후불 시험 준비→실제 qwen3:1.7b 추천→앱 수락→상하차 실제 촬영·저장→운송 인수완료·사진 2개 무결성·하위 Mongo 완료 수렴을 확인했다. 결제/정산은 입금대기이며 외부 FCM·소리·지도·GPS와 ready 503은 남아 있다. 198/199 및 시험 실패 기록은 보존한다.

## 무료 퀵·화물 등록 준비 보완 r71 (2026-10-19, 로컬 검증 완료·운영 미활성)

- [승인 구현 범위](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/registration-readiness.implementation.r71.md) · [변경·검증 기록](../Changes/2026-10-19-freight-registration-readiness-r71.md). 기사/실차의 수단별 내부 검토와 플랫폼 등록·요금/약관·사업계획·시설 등의 준비를 분리했다. 신규 운행·후보/AI·공개/전국 콜·추천 저장·수락·알림에서 검토를 확인하고 기존 운송 조회·사진·완료·멱등 복구는 유지한다. 무료 설정 0원, 화주 종류·포장 포함 중량·치수·출도착 입력/보존을 연결했다.
- 집중 333개·전체 12,823개 통과, 실패 0개·환경 의존 16개 생략. 실제 격리 HTTP·SQLite와 전체 소비 솔루션/로컬 운송 도구 4개 빌드, 공통 UI·화주 입력 14장면·대표 PNG 6장을 확인했다. 최초 실패와 수정 후 성공은 별도 보존했다. 실제 증빙 존재·소유/진위, 기관 조회·등록/신고·개인정보 보존/삭제와 운영 적격 판정은 미완료다. 실제 사용자/운영 DB·기관 전송·실금융·APK 설치·공개 배포·Unity·영상·commit/push는 변경하지 않았다.

## 무료 퀵·화물 등록 준비·기사 자격 확인 감사 (2026-10-19, 조사·기존 시험 확인)

- [법령·코드 대조와 보완 순서](../Changes/2026-10-19-freight-platform-registration-readiness-audit.md). 2027-09-23 개정법 시행을 목표로 현재 프로필·역할·추천·수락과 법정 자격 확인을 구분했다. 기사 기본 정보 등록 후 활동중 저장, 한국 허가 정책 AuditOnly, 면허·허가·보험 검토/효력 관리 공백을 확인했다. 화물 입력과 문제 신고는 기존 기반을 재사용하되 법령 필수 항목·위반 확인/제한 연결은 보완 대상이다.
- 무료 직접 제안 경로와 달리 기본/개발 설정에 건당500원·월상한5,000원 이용료 계산이 남아 있음을 기록했다. 실제 청구·유효 배포 설정 확인은 아니다. 현 소스 빌드 후 관련 기존 시험60개와 요금/운영 정책10개가 통과했으며 실제 법정 자격 검증 완료로 해석하지 않는다.
- 이번에는 문서만 추가/연결했다. 제품·설정·DB·운영 활성화·APK·외부 조회·등록 신청·commit/push는 변경하지 않았다. 사업자 서류·사무실·결격사유와 신설 등록 세부 기준의 적격은 미확인이다. 다음은 수단별 자격 검토 결과와 기존 기사/차량을 연결하고 운행 시작·추천·수락에서 효력을 재확인하는 서버 공통 관문이다.

## 퀵·화물 주문 한 건 마감 r70 (2026-10-19, 로컬 검증 완료·운영 미활성)

- [결과·단계별 화면](../Changes/2026-10-19-cargo-order-closeout-r70.md) · [승인 설계](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/order-closeout.design.r70.md) · [관리자 조회 계약 보완](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/order-closeout.admin-contract.r70.md). UTC 추천 시각·기사 약정/동결 보수·변경 재확인·EF 배열 조회를 보완했다. 같은 의뢰의 공통 앱 VM 수락 POST1회→픽업/전달 사진 첨부→인수완료→화주 완료 조회·기사 대기 복귀를 실제 격리 HTTP/SQL/Mongo로 확인했다. 운영자 숫자 열거 응답과 조회5개 기능 분류 누락도 보완했고 최종9GET·3개 기존 역할 SSR이 통과했다. 사진 없는 완료와 타기사 조회 차단을 확인했으며 초기 실패는 보존했다.
- 추천 조회 수정 포함 전수12,690통과·16생략·실패0 뒤 추가 관리자 수정은 최종 집중114/114와 솔루션·도구 빌드로 검증했다. SSR·브라우저 PNG와 실제 기기·Windows 앱 조작은 별개다. 일반 퀵의 상위 통합 주문 자동 연결, 운송 첨부 사진과 기존 POD/인수증 연계, 실수납·입금은 완료하지 않았다. 운송은 인수완료, 결제/정산은 대기 상태다.
- 통합앱0.1.194 USB 시험 APK를 Drive 소유자 전용으로 보관하고 [다운로드 안내](../../eng/release/downloads/README.md)를 준비했다. 내장5363과 이번 API호스트5364를 구분하며 설치·외부 통신·실제 FCM·익명 다운로드·GitHub 게시·commit/push는 하지 않았다. 운영 기본값·권한·Unity/영상은 유지했다. 시험 API호스트와 처음 꺼져 있던 컨테이너3개를 정리하고 DB/자료를 보존했다. 읽기 전용 화면 기록5366만 남겼다.

## 거래 신뢰 r107 (2026-10-19, 동일 두 사람 주간 총3회 방향·상세 논의 보류)

- [직접 거래·신뢰의 높이 정본](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/trust-elevation.design.r107.md). 미러 밖의 거래도 확인 이력으로 알아봐 주는 방향을 유지하며, 신뢰 인정은 상품/수량 차이와 관계없이 같은 두 사람 사이 일주일 총3회 정도로 제한한다. 이전 상품/수량별 계속 인정 예외는 현행으로 재사용하지 않는다. 이는 실제 거래 제한이 아니고 하루1~2회의 정확한 상한·주간 경계·증빙/상승량은 미정이다.
- 사용자 요청에 따라 이 주제의 추가 질문은 보류한다. 기존 상호 완료·유상 수령 확인·금액 중립·공개 동의는 유지하고 제품·점수·금융·외부 서비스는 변경하지 않았다.

## 화물 AI 선택→앱 API 업무 마감 r114 (2026-10-19, 로컬 API 검증·추천 UI 공백)

- [업무 트리·결과·실제 공통 화면 6단계](../Changes/2026-10-19-cargo-ai-device-verification-r114.md) · [책임 정본 r68](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-ai-device-verification.implementation.r68.md). 최신 사용자 답변에 따라 휴대폰은 조작하지 않고 새 전용 Simulation DB/5364에서 합성 법정동 표식의 주문 3개·기사 2명을 사용했다. 실제 모델 3회 중 이유 불일치 2회는 추천하지 않았고, 이유 규약 r2의 별도 1건은 기존 추천 전환→실제 API 수락→상차/하차 사진 업로드→인수완료와 SQL/Mongo·화주 조회·기사 대기 복귀까지 확인했다. 모델 가중치나 운영 배차를 활성화한 것은 아니다.
- 새 도구 빌드 경고/오류 0·자체 31개·추천 보호 5개·사진 필수 2개와 비소유 상세 거절을 확인했다. Fast/Task 라우터는 새 도구 경로를 guidance로 분류해 제품 시험을 생략한다. 실제 공통 client/ViewModel/Razor의 수락 후 5단계는 통과했지만 **일반 화물 추천 보수 미확인과 만료 시각의 시간대 해석 문제가 남았다**. 보수 부재가 수락 차단의 원인인 것은 아니다. 오프셋 없는 UTC 의미의 응답을 한국 시간대에서 Unspecified→ToUniversalTime으로 해석하면 9시간 일찍 만료된다. 수락은 직접 API로 진행했으므로 전체 UI 통과가 아니다. 대표 캡처는 실제 공통 SSR의 브라우저 화면이며 Android 캡처가 아니다.
- ready 503은 EnsureCreated 격리 DB의 미적용 migration(main 66·시장 2·농수산 17·공공수집 7)로 남겼다. Outbox Pending/시도 0·실제 FCM/배너/소리·GPS/도로 ETA·물리 카메라·실금융·APK·외부 배포·Unity/영상·commit/push는 미수행이다. 다음은 제안 보수/적격성/화면 수락 계약 연결 후 같은 업무의 실기기 검증이다. 읽기 전용 기록 검토는 `http://127.0.0.1:5365/view/completed`이다.

## 법정동별 퀵 배차 학습 준비·PC 검토 r113 (2026-10-19, 로컬 구현·검증 완료)

- [흐름·실행·대표 화면](../Changes/2026-10-19-cargo-regional-training-r113.md) · [기획 정본 r67](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/cargo-regional-training.implementation.r67.md). 공식 보관 자료의 법정동 15개 코드·명칭을 가상 퀵 주문/기사 사례에 연결했다. 검토 20개와 다른 상황군의 평가 8개를 나누고, 기존 순수 화물 조율 엔진·선택적 로컬 모델·사용자 의견을 PC에서 비교/저장하도록 공용 관리자 컴포넌트와 loopback 호스트를 준비했다. 실제 경계·인접·경로·ETA나 운영 권역 변경을 뜻하지 않는다.
- 최종 자체 26개·Fast 계약/관리자 빌드·Task 솔루션 빌드와 제품 전체 12,644개 통과·실패 0개·환경 의존 16개 생략이다. 실제 연결 화면의 법정동 필터·끝점 ETA·평가 제외·모델 차이와 별도 사본의 복수 후보 저장/복원·보류 전환을 관찰했다. 초기 경로 정규화·ZIP 한글 파일명·Razor 연결 실패도 보존했다.
- 기존 Ollama `qwen3:1.7b`로 실제 비교 3회 중 2회는 입력과 맞지 않는 근거로 제외됐고, 나머지도 같은 ETA에서 장기 대기 후보 대신 짧게 기다린 후보를 골랐다. 구조 검사 통과와 판단 품질을 구분하며 사용자와 검토할 자료로 남겼다. 실제 사람 검토/학습 가능/승인 내보내기는 모두 0이다.
- 로컬 검토 주소는 `http://127.0.0.1:5595/admin/freight-delivery/dispatch-ai-review/training`이다. 운영 DB·기존 관리자 route/DI/메뉴·모델 가중치·worker/추천 적용·APK/휴대폰·Unity·영상·외부 배포·commit/push는 변경하지 않았다. 다음은 대표 사례를 사용자와 검토해 허용 후보/보류와 미정 정책을 정리하는 일이다.

## 화주 PC MAUI 재확인·접수 후 안내 r112 (2026-10-18, 구현·로컬 검증 완료)

- [조사·변경 범위](../Changes/2026-10-18-shipper-pc-closeout-r112.md). 기존 `SsalddelApp`이 Windows MAUI 화주 호스트임을 현재 소스로 재확인했다. 별도 웹과 구분하고 기사 모바일 우선·화주 PC/모바일 지원을 유지한다. 기존 PC 셸의 폭 고정을 반응형으로 보완하고 공통 상세의 다음 담당/대기 이유·완료 단계 오분류와 웹 접수 응답 유실 보호를 다룬다.
- 최종 Fast154개·Task12,644개 통과·실패0·환경 의존16개 생략, Windows MAUI/제품 솔루션/미리보기 빌드 성공, 격리 JS8개 통과다. CUA5장면·캡처7장·대표5장, 콘솔 오류0·1280px의1180px 셸/2열·메뉴 복귀 입력 유지·표 목록을 관찰했다. 검사 소스31개 해시 변화0·기존 dirty312파일의 허용 범위 밖 변경0, 전용5594/탭16만 종료했다. 최초 복원 호출·시험 타입 연결·동의 구성 기대문자열 실패와 수정은 기록에 보존한다.
- 실제 선결제·승인·배차를 임의 활성화하지 않았으며 Windows 빌드/브라우저 공통 UI와 실제 Windows 창·서버/DB·USB·실금융을 구분한다. 초기 선결제 UI→서버 결제준비·확인→배차 연결과 일반 거절의 미접수 확정 계약, 새 Web 복구의 실제 브라우저/다중 탭·320/390px/단말 확인은 남아 있다. commit/push·외부 배포는 미수행이다.

## 퀵 기기별 마감 범위 r111 (2026-10-18, 기획 정리·제품 변경 없음)

- [현행 기기별 책임과 순서](../ProjectOverview/page-docs/quick-driver-usability-r110.md#기기별-마감-범위-r111--2026-10-18-사용자-확정). 기사 모바일은 추천/수락·픽업/전달·사진·내역에 집중하고 기사 데스크톱의 신규 제품 화면/별도 최적화는 이번 마감에서 제외한다. 화주는 모바일 단건 접수·현장 확인과 데스크톱 접수·다건 관리·증빙 조회를 모두 유지한다.
- 다음은 화주 데스크톱 단건 접수/결과 재조회 마감→기사 모바일 동일 건 수행→화주 모바일/PC 같은 의뢰 왕복 확인→데스크톱 일괄 접수다. 현재 웹 단건은 서버 등록에 연결돼 있지만 웹 CSV는 형식 미리보기이며 모바일 일괄등록과 다르다. 후불 승인 권한/준비 계약의 공백은 r110 후속으로 유지한다.
- 이번은 현재 소스의 진입/기능 범위를 읽고 문서로 책임을 나눈 작업이다. 제품 코드·새 UI 구현·실행 시험·APK/USB·서버/DB·운영 설정·commit/push를 변경하지 않았다. 기존 r110의 데스크톱 모의 화면을 기사 휴대폰 검증으로 확대하지 않는다.

## 퀵 우선: 통합 기사 시작·수신과 상하차 안내 r110 (2026-10-18, 구현·로컬 검증 완료)

- [업무 트리·결과·대표 화면](../Changes/2026-10-18-quick-driver-usability-r110.md) · [페이지 책임](../ProjectOverview/page-docs/quick-driver-usability-r110.md). 사용자의 퀵 우선 요청에 따라 통합 화물기사의 운행 시작·본인 신규 수신 토글·내 정보 운행 종료를 기존 서버 계약에 연결하고, 상하차 저장의 첫 부족 조건·이미 완료된 정본과 다음 업무를 표시했다. 진행 운송·음식/미확인 유형 근무·위치 실패와 응답 유실 보호를 유지한다.
- 최종 집중193개·Task 전체12,568개 통과, 실패0·환경 의존16개 생략이다. 소스21파일 변화0·기존 dirty280파일의 허용 범위 밖 변경0, 현재 DLL을 소비한 브라우저 조작/캡처9장·대표8장을 확인했다. 미리보기의 메모리 예시이며 지도·GPS·FCM·실제 API/DB/금융·Android 검증과 구분한다. 전용5593 서버/검증 탭만 종료했다.
- 다음은 퀵을 보내는 사람의 원천별 접수/결제·승인 다음 행동 연결이다. 간편 퀵 직접 현장지급, 일반 화주 선결제·플랫폼 수납, 별도 후불승인을 같은 흐름으로 합치지 않는다. 후불승인의 현재 권한·준비 상태를 앱에서 판독하는 계약이 부족해 임의 승인 버튼은 추가하지 않았다. 객체 수명 밖의 영속 복구·백그라운드 위치·APK 제작/설치·외부 공개·commit/push는 미수행이다.

## 첫 이용 관점의 화면·업무 경로 점검 r109 (2026-10-18, 조사·로컬 화면 검증)

- [점검 결과·보완 순서·대표 화면](../Changes/2026-10-18-first-use-task-audit-r109.md). 실제 공통 컴포넌트를 모의 자료로 조작하고 역할별 소스와 대조했다. 먼저 통합 화물기사의 운행/신규 수신 시작, 창고 초안의 조건 보완·견적·승인 인계, 비활성 저장의 부족 이유, 부분 성공/결과 미확정 복구를 보완한다. 시작/초안 문제는 소스 확인이며 실제 첫 빈 계정 렌더와 구분한다.
- 생활 배송4단계·음식 필수값 안내·전달/수령/지급의 구분은 직접 확인했다. 창고 입고에서 확인3개만 선택하고 보관 위치를 비우면 저장 불가 이유가 없음을 재현했고, 모의 쓰기 차단 후 재확인에서는 입력 보존을 확인했다. 의도적인 미리보기 차단을 운영 서버 오류로 판정하지 않았다.
- 320/390px의 기존 목적 사용성 검사26개·캡처14장, 브라우저 오류0을 별도로 확인했다. 일부 회귀 결과이며 모든 역할의 설명 없는 완주·실제 참가자/Android/지도/알림/결제 검증은 아니다. 제품 코드·APK·DB·운영 설정·commit/push는 변경하지 않았다. 상세 조사3개와 수동 관찰은 `artifacts/local/usability-review-r109/`에 보존한다.

## 최신 앱·로컬 서버 업무 완주와 수락 후속 복구 r108 (2026-10-18, 로컬 검증 완료·기기 대기)

- [작업 범위·결과·대표 화면](../Changes/2026-10-18-local-workflow-recovery-r108.md). 사용자 승인 1·2번인 정상 업무와 중단·응답 유실 복구를 로컬에서 검증했다. USB 조작·설치·외부 공개·실수납·실송금은 수행하지 않았다. 최종 역할 앱 빌드·집중291개·제품 전체12,515개 시험 통과, 실패0·환경 의존16개 생략이며 검사한 소스/시험30파일 해시 변화0이다.
- 화물 수락과 영속 후속 의도를 함께 저장하고 시간약속·알림 Outbox·완료를 원자 마감한다. 최신 수락/담당/차수·CAS/lease, 기존 관리자 Failed 재예약·Blocked 조회 전용·worker 기본OFF를 유지했다. 창고 출고의 저장 좌표 인계·승인 전 준비 검사, 앱 오류 `code` 판독, 신규작성 견적의 공급된 좌표·추가비 보존을 보완했다.
- 격리 실제 MySQL/Mongo에서 음식·퀵·창고 출고 연계 API 완주·재시작 후 보존과 수락 후속 INSERT 장애 복구를 확인했다. 공통 앱 VM/실제 HTTP43검사·성공응답 유실5회/업무POST5회·별도 수령 확인, 모의 제품 화면32상태/2흐름/40캡처를 서로 구분했다. 실제 경로·FCM·Android·금융은 미검증이다. 전용 서버는 종료하고 시험 자료는 보존했다.
- 통합 시험APK193을 같은 소스로 제작·서명/정렬·양 ABI·기존192 동일서명·수정 코드 실제 탑재까지 확인했고 설치/업로드는 하지 않았다. 기존 로컬 시험 주소5363은 이번 하네스5378과 별개다. 기존 창고 초안의 상세→수정·견적·승인 화면 인계·주소 좌표 해소와 일시적 Mongo 연결 오류 최초 원인 확인은 후속이다. 서버 API 완주나 신규작성 조건 회귀를 창고 앱 화면 전 과정 검증으로 확대하지 않는다.

## 직접 거래와 신뢰의 높이 r107 (2026-10-17, 기획·공식 자료 수집)

- [현재 기획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/trust-elevation.design.r107.md). 직접 계좌이체 선택·산 높이 신뢰 표현, 상호 완료/유상 대금 수령 확인·금액 중립 평가를 수용했다. 같은 상대라도 상품/실제 수량이 다른 별도 거래는 계속 인정하고 같은 조건 반복은 일주일3회 인정하는 방향이다. 동일 거래의 재전송은1회만 센다. 다음은 달력 주/최근7일의 집계 경계이며 상품 식별 단위·상승량·정정·공개 범위도 미정이다. 실제 점수/결제 기본값은 변경하지 않았다.
- 공식100대명산 CSV100행·11열 원본과 해시/출처, 후보 ID100개를 보관했고 용마산·북한산·설악산·지리산·한라산·후지산·에베레스트의 대표 근거를 조사했다. 전국100개 항목 전수 검수·제품DB 저장·사진 사용 허락·신뢰 엔진·APK 반영은 미수행이다.
- 기존 상호 완료·직접 송금/수령 확인 원장을 재사용하되 일반 생활협업의 상품대금 원천 어댑터와 모바일 개인 화면의 실제 인증 조회가 공백이다. 공개 주간 집계나 신고/사건 종결 수를 개인 신뢰·귀책 확정으로 오용하지 않는다. 후속 구현은 개인 화면/원천 연결→계산 정책 합의→근거 투영/산 표시 순서를 제안했다.

## 로컬 서버·통합앱 푸시 연결 r106 (2026-10-17, 로컬·실기기 통신 검증 완료)

- [현재 기록](../Changes/2026-10-17-external-server-push-connection-r106.md). Firebase 포함 통합APK190과 화면/통신 보안 보완192를 제작·업데이트 설치했고, 최종 base.apk 해시·로그인·기기 등록·알림 권한을 확인했다. 전용 r106 DB/계정9개/API를 사용하며 기존 데이터·다른 앱을 보존했다.
- 기존 Firebase 프로젝트에 통합앱을 등록하고 전용 SDK 설정을 저장소 밖에 확보했다. 새 발송 키/권한은 만들지 않았다. 외부파일 결속13개·기존 USB21개·배포 포장46개·빌드 준비 모의12개 통과다. 실제 MySQL에서 드러난 재시도/명시 transaction 충돌을 전체 생활배송 SQL 변경 경계에 보완했다. 최초 전체 시험33개 실패의 fixture 모델 캐시 혼용을 해결하고 엄격한 암호화를 유지했다. 최종Fast150개·전체Task15,415개 통과, 실패0/환경 의존16개 생략이다.
- 초기 case03(서버r6/APK190)과 최종 case04(수정 서버/APK192)를 나눠 기록한다. 192에서 앱을 뒤로 보낸 상태의 Google FCM 승인·Android 게시·알림 선택 후5,000원/2.5km 조회·하단 버튼/사람 아이콘/중량 표시, 실제 기한 후 재조회 시 수락 명령 차단을 확인했다. 실제 사람 청취는 대기다. 사용자 후속 `서버는 로컬에서 검증`에 따라 Azure 로그인 대기를 종료하고 배포/최종 외부 묶음 제작은 보류했다. 외부 HTTPS·배송 업무 완주·실금융은 미확인이다.

## 전체 역할 통합 공개 준비 r105 (2026-10-16~17, 로컬 구현·검증 완료·외부 공개 대기)

- [승인 기준](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/integrated-public-release.r105.md) · [구현/검증](../Changes/2026-10-16-integrated-platform-preparation-r105.md). 퀵만 먼저 공개하는 방향을 음식·퀵·화물·공간·현장 작업의 함께 공개 준비로 대체했다. 통합앱/웹·24시간 서버·정상 자동/당사자 처리·예외 비상 담당·서비스비 수납/주간지급·상품대금 직접 거래 기준이다.
- 음식 합의 취소·화물 상차 후 취소 보호·불량/소실 재고 마감·판매 실패 보존·기존 운송 채무 자동 명세를 구현했다. schema는 취소합의1테이블·판매 진단1열·자동명세 진행2열이며 전문 원장 권위와 기본OFF 유지다.
- 초기 컴파일·fixture·판매 계정 변경 즉시비노출 실패와 전체Task56개 실패를 보존하고 원인별로 수정했다. 최종Fast506개·Task12,402개 통과, 실패0/환경 의존16개 생략. 소개9개·판매 연속14개 실제 루프백 검사 통과. EF 모델차이0·DB연결없는신규SQL생성을 확인했다. 통합APK188 제작·설치와 최종189 제작(미설치)을 구분하며 사용자가 기기 화면 시험을 다음으로 미뤘다.
- 공개 준비 점검은 `NotReady`. 실수납/지급 계약·재원·Operational adapter·전체 외부HTTPS/단말/독립복구 미완료. Azure현재조회/USB연결은변경기록에서구분하며 공개/운영/commit/push는실행하지않았다.

## 역할 앱 첫 이용 사용성 보완 r1 (2026-10-16, 소스·로컬 검증 완료)

- [역할별 사용성 점검](../ProjectOverview/page-docs/first-use-usability-review-r1.md)과 [변경·대표 화면](../Changes/2026-10-16-first-use-usability-r1.md): 공식 KRDS·GOV.UK·W3C·Android 원칙을 기존 시각 기준 r8에 통합했다.9개 앱325개 route 파일·356개 정의·XAML21개를 목록화하고 대표36개를 소스에서 검토했다. 이 집계는 실제 도달 가능한 모든 화면·상태나 전수 렌더 완료가 아니다.
- 판매 주문 목록의 검색/페이지 보존·안전한 복귀, 상품 등록 빈 상태, 음식 주문 필수/선택·진행 안내, 계좌 미확인과 미연결 기능 안내, 화주 CSV 입력/보관 범위·창고/인력 용어·지도 준비 안내를 기존 책임 안에서 보완했다. 승인된 네이티브 지도/추천 카드·서버/원장/권한/운영 상태와 관련 없는 기존 변경4개는 보존했다.
- 전체 Task 빌드와12,286개 시험 통과·실패0/환경 의존16개 생략. 모바일320/390px의 실제 공통 컴포넌트26개 검사·브라우저 오류/업무·외부 HTTP 시도0개, 캡처14장 중 검토 PNG6개·해시 대장 보존. 초기 전체 시험3개 실패와 참조 출력이 이전 판본이던 브라우저 검사4개 실패도 별도로 유지한다. APK 설치·실기기·실주문/금융·접근성 인증·commit/push는 추가하지 않았다.
- 다음은 긴 주문 확인 화면의 묶음/표시 순서, CSV 불명확 접수 결과, 음식점 알림 결과, 설정 왕복의 편집값·키보드/포커스·긴 주소 관측이다. 검색값 보존과 실제 입력 포커스 보존을 혼동하지 않으며 교육 없는 사용 가능은 처음 쓰는 사람의 과업 관찰로 확인해야 한다.

## 대표 스크린샷과 화면 진행 문서 연결 (2026-10-16, 기존 기기 캡처 문서화)

- [화면 진행 요약](../Changes/2026-10-16-phone-ui-progress-summary.md)에 추천 배너·최신 카드/선택 핀·APK189→190 디자인 비교, APK180 같은 화물 사례의 상차/하차 사진 관문·완료 후 복귀, 음식 APK178의 픽업 전후 카드를 실제 화면으로 연결했다.
- 대표 PNG11개와 원본/파생 해시·재단 좌표·APK/단계 대장을 `docs/assets/changes/2026-10-16-phone-ui-progress/`에 보존한다. 실제 GPS·계정·시스템 정보·사진 증빙 원본은 제외하며 기존 비공개 원본/검증 기록은 유지한다. r67~r73에는 요약 진입만 추가하고 과거 시험·성공/실패를 다시 쓰지 않는다.
- Codex의 독립 픽셀 검토로 대표11장을 각각 확인했고, 원본/파생 SHA-256·재단 픽셀 일치·메타데이터 없음·문서 이미지 경로를 검사했다. 사람의 전체 검수나 자동 개인정보 탐지 완료로 표시하지 않는다.
- 이번 작업은 기존 캡처의 문서 자산 정리다. 제품 변경·새 빌드/설치/기기 수행·실제 배송/수령/금융 검증을 추가하지 않는다. 서로 다른 APK 화면을 한 사례의 연속 실행으로 합치지 않으며 APK190의 실제 수락/거절 확정·긴 주소 검증은 기존 미검증 상태다.

## 추천 카드 여백과 응답 색 정리 r73 (2026-10-16, Fast·전체 Task·APK190 표시/넘김·취소/복귀 검증 완료)

- [변경·독립 검토](../Changes/2026-10-16-compact-recommendation-card-design-r73.md): 사용자의 흰 여백 축소·거절 파랑/수락 주황 요청을 기존 추천 판단 책임 안의 시각 변경으로 정리했다. 배달료104 DIP 열(이름11/금액18)과 픽업/전달13 두 줄을 가로로 묶고 전체 요약 스크롤·하단 좌거절/우수락48 DIP를 유지하며 카드116 DIP로 조정했다. 주소 잘림 방지·긴 주소/좁은 폭·선택 구분을 검토했고 파랑/흰 글자5.169:1·밝은 주황/짙은 글자6.465:1 대비를 계산했다.
- 제품 소스 `5a59f73fbcb50269b2affa2430dbb89cb012dbb29c91d2bbee779e19218f30a5`와 r72 사본의 시각 변경만 대조했다. Fast 앱 빌드·관련69개 통과·실패/생략0개, Task 솔루션 빌드·전체12,274개 통과·실패0/환경 의존16개 생략과 실제 TRX를 확인했고 종료 후 소스도 동일했다. Task는 명시 필터와 관계없이 Full인 기존 도구 계약에 따라 전체 실행으로 기록했다. APK190 제작·설치 버전/해시 `0679981e3c299a19c6c46a0a08be8f2bbcfcb818e952325caefe8954a933d816` 일치와 별도 격리 증분 제작 기록도 확인했다.
- [최종 기기 원장](../../artifacts/local/physical-phone-r67/.private/compact-card-r73-r190-validation.json): V06의 두 열·각51px 주소·파랑/주황48 DIP 버튼, V07/V10/V12 두/세 건 양방향 카드·전체 핀/선택 색/카메라 유지, 거절/주의사항 창 취소와 V15 수신 OFF 복귀를 PNG·UI로 확인했다. V/W/X는 미확정·보수 미동결·후불승인완료/결제대기이며 실제 수락/거절 확정은0회다. 긴 실제 주소 스크롤은 미검증, 소스의 기존 스크롤 유지와 구분한다. 기존 지도/응답 가드·r72 증거·r71 실패 이력과 화면 유지15/권한·보안을 보존하며 서버·요금/정산·worker/FCM·금융·운영·commit/push는 변경하지 않는다.

## 추천 카드에서 바로 응답 r72 (2026-10-16, 최종 Fast·Task·APK189 카드/확인 창 취소·복귀 검증 완료)

- [책임·변경 기록](../Changes/2026-10-16-direct-recommendation-card-actions-r72.md): 128 DIP 카드에 배달료·픽업지·전달지와 하단 좌측 거절/우측 수락만 남겼다. 순번은 header로 옮기고 상세·보류·상시 체크를 제거했다. 필수 주의사항은 수락 클릭의 확인 창에서 처리하며 전체 핀/선택 색·카메라·실패 복구는 유지한다.
- APK188 소스 `9bcc782ddb295aafbaa83a6dd05b5f8e81f787d433cd7ad438646887ee92d3cc`의 Fast 69개·Task 12,274개 통과·실패 0개·16개 생략과 설치 일치를 확인했지만 T06 전달 주소 잘림을 재현했다. 세 여백 속성만 조정해 14 DIP를 확보하도록 했고 128 DIP 카드·글자·48 DIP 버튼·지도·응답 가드는 유지했다. 수정 후 소스 `c0bf2aa8c91dd197852a4460f745605061dde2d3394de699f215205ceebb823b`의 Fast 69개·최종 Task 솔루션 빌드/12,274개 통과·실패 0개·환경 의존 16개 생략과 실제 TRX, APK189 제작·설치 버전/해시 `e9ca3c163621432c50255a560227422e7fff15951d4b551b778023035de64f3c` 일치를 확인했다. U08의 세 정보·픽업/전달 각51px·좌거절/우수락, U09/U13/U15의 두/세 건 양방향 카드와 전체 핀·선택 색·카메라 유지, 거절/경고 확인 창 취소와 U19 수신 OFF 복귀를 실제 PNG·UI로 확인했다. 실제 수락/거절 확정은 실행하지 않았고 S/T/U 모두 미확정·보수 미동결·후불승인완료/결제대기다. 이전 통과를 소급하지 않는다. APK188 T08 PNG의 변경 중 상태·r71 실패와 모든 이전 자료도 보존한다.
- APK188 격리 host 빌드 경고/오류 0개·기존 ready/실행 식별자/DB/계정 유지 재시작과 P/Q/R 준비를 보존했다. APK189은 별도 S/T/U로 확인했고 [최종 기기 검증 원장](../../artifacts/local/physical-phone-r67/.private/direct-card-r72-r189-validation.json)에 소스/설치·실제 화면·시험·복구를 연결했다. 기존 A~R·r71의 전체 시험 1개 실패/단일 재시험 통과·APK187/188 자료를 보존하며 r72 전체 통과를 소급하지 않는다. 합성 좌표·300초·worker/FCM/금융 OFF를 유지하고 수신 OFF·화면 유지15를 복원/유지했다. 실제 운영·송금·commit/push로 확대하지 않는다.

## 지도 우선 추천 비교 r71 (2026-10-16, APK187 부분 확인·후속 r72 정정 진행)

- [책임·변경 기록](../Changes/2026-10-16-map-first-recommendation-cards-r71.md): 96 DIP 기본 카드에 순번·금액·짧은 주소·상세 진입을 두고 전체 주소/조건/경고/응답은 같은 ID·차수의 상세로 분리했다. 모든 유효 추천 핀과 선택한 주황/파랑·다른 회색, 전체 좌표 집합이 바뀔 때만 카메라 갱신을 연결했다. 기존 지도 기본값·현재 위치·명령 보호와 실패 후 핀 캐시 복구를 유지한다.
- APK187 페이지 소스 SHA-256 `cd0495061b2670836246501b6f61c7cda2c56de4ce2fc099fa5147b2364b1a35`와 Fast69개 통과를 확인했다. Task 빌드는 성공했지만 전체 시험은 12,273개 통과·알림 콜백 2초 시간 초과 1개 실패·16개 생략이었다. 단일 재시험 1개만 통과했으며 원인 확정·전체 재시험 통과로 처리하지 않는다. APK187 설치 버전/해시 `9e43c3885d2df0abe8eaa972e4c74c331f85cb50d1f836bf1caa8dade8c04e0c` 일치와 S08/S09 두 카드·네 핀·선택 색/카메라 유지, S10/S11 상세 경고 확인 전후 수락 비활성/활성을 확인했고 수락은 실행하지 않았다.
- M/N 추천 뒤 O 추가와 S12/S13의 세 카드·여섯 핀·선택 색을 확인했다. 수신 OFF는 S17 정착 PNG·UI로 확인했으며 앞선 S16 PNG의 변경 중 화면과 구분한다. 마지막 M/N/O 원장은 모두 미확정·보수 미동결·결제대기이고 수락/거절은 실행하지 않았다. 사용자가 상세 없는 카드로 다시 지정해 r72에서 보완하며 r71 부분 검증·단일 재시험을 최종 전체 성공으로 확대하지 않는다. host 파일 잠금 실패/재빌드 성공·r70 APK186·A~L·기존 실패를 보존하고 합성 좌표·300초·worker/FCM/금융 OFF를 유지한다. 서버 배차 정책·음식 기사 앱·요금/정산·Unity·공개 운영·commit/push는 이번 범위가 아니다.

## 화물 추천별 독립 카드 r70 (2026-10-16, 구현·로컬 검증·APK186 독립 카드 확인 완료)

- [변경·검증 기록](../Changes/2026-10-16-independent-recommendation-cards-r70.md): 기존 추천 페이지 한 파일에 독립 카드·양옆 미리보기·건별 버튼을 연결했다. APK185의 옆 카드 탭 후 중앙 G/지도 H 불일치 실패를 보존하고 탭·별도 선택 버튼을 제거했다. APK186에서 두 장·세 장 표시, 옆 카드 탭 전후 선택 유지, 양방향 넘김과 선택 지도, L 경고 확인/수락 활성과 K 미확인/수락 비활성의 분리, 운행 지도 복귀·신규 수신 OFF 복원을 확인했다. 일반 선택 동작은 좌우 넘김이다.
- 최초 `CS0136` 빌드/APK 실패와 이전 Fast/Task 결과를 보존한다. 탭 제거 최종 소스 SHA-256 `2ebda7bd041960774a469568205b62092b23a02c46cb5930ddc4b663892b91a3`, Fast-final 53개 통과·실패/생략 0개, Task-final 솔루션 빌드 및 12,258개 통과·실패 0개·환경 의존 16개 생략을 확인했다. APK186 설치 버전·해시 `3ce3c0f28f45fe2b832ad2738c6f97ad8e133801505d24194ae831455aed4d5f` 일치도 별도 확인했다.
- G/H/I와 J/K/L은 모두 수락하지 않았다. J/K는 자연 만료했고 L이 남은 상태에서 수신 OFF로 복원했다. J/K/L 마지막 원장의 확정 기사/동결 보수 없음·결제대기를 대조하고 기존 A~I·소스 사본·실패와 r69 증거를 보존했다. 합성 좌표·300초·worker/FCM/금융 OFF를 유지하며 원본 화면은 비공개 보관한다. APK186 수락·수행/취소/거절 재실행·실제 금융·주행·운영·commit/push로 확대하지 않는다.

## 화물 기사 추천 지도와 좌우 카드 r69 (2026-10-16, 구현·로컬 검증·APK184 설치·추천 화면 확인 완료)

- [화면 책임·진행 기록](../Changes/2026-10-16-recommendation-map-carousel-r69.md): 수행 지도에는 새 추천 배너를 표시하고, 배너에서 별도 추천 지도로 이동해 하단 건별 카드를 좌우로 넘기는 구조를 연결했다. 현재 업무와 추천 선택·수락을 분리하고 선택 카드의 픽업 주황/도착 파랑 핀을 연결한다.
- APK183 설치·해시·추천 두 건의 좌우 카드·경고 확인·선택한 6,200원 건 수락과 운행 지도 복귀를 확인했다. 상하차 사진·완료 명령 후 원장에서 D 인수완료·입금대기·결제대기를 확인했으며 실제 금융은 미검증이다. APK182 배너 미표시와 UTC 수정 이력은 보존한다. 최종 APK184 설치·해시 일치와 새 E/F의 자동 배너·별도 지도·좌우 카드·복귀를 별도로 확인했다. E/F는 미수락이며 시험용 신규 수신 ON은 원래 OFF로 복원했다. 고정 카드가 가린 핀은 지도 이동 후 확인했고 지도 크기는 유지했다.
- Fast-r4 43개 통과이며 방향 10개는 해당 집중 필터에서 빠져 이전 Fast-r3 53개와 전체 시험 근거로 구분한다. Task-r4 전체 빌드는 통과했으나 알림 콜백 시간 초과 1개가 실패했다. 후속 단일 1개·푸시 그룹 21개·같은 소스/산출물의 독립 전체 시험 12,258개 통과, 실패 0개·환경 의존 16개 생략을 각각 보존한다. 원인 확정·실제 주행/FCM/금융·운영·commit/push로 확대하지 않는다.

## 화물 기사 현재 위치 방향 옵션 r68 (2026-10-16, 구현·기기 표시 검증 완료)

- [변경·검증 기록](../Changes/2026-10-16-current-location-arrow-r68.md): 기존 화면 설정에 위치점·방향 화살표 선택을 연결했다. SDK 기본 화살표를 쓰며 미확인/10초 만료 시 위치점으로 복귀한다. 지도 전경과 옵션이 켜졌을 때만 방향 센서를 사용한다.
- 관련 집중 시험58개·Fast 빌드와 전체 Task 빌드·제품 시험12,238개 통과, 실패0개·환경 의존16개 생략. APK181 기기 설치·해시 일치, 위치점/화살표 ON/OFF·배경 복귀·재실행 선택 유지를 실제 화면으로 확인했다. 방향은 휴대폰 자체 상단 기준이며, 실제 회전/주행 정확도·센서 장애 재현·지도 카메라 회전·서버 위치 전송 변경·음식 기사 앱은 이번 범위가 아니다.

## 새 개발 휴대폰 배차·수행 검증 r67 (2026-10-16)

- [실기기 검증 기록](../Changes/2026-10-16-physical-phone-verification-r67.md): 역할 앱 7개 설치·로그인, 음식 기사 추천→수락→도착→픽업→전달과 주문자의 별도 수령 확인을 실제 기기·격리 서버에서 대조했다. 배차 알림음은 사용자가 청취를 확인했다. 실제 FCM·클라우드 운영·상품 배송·송금과는 구분한다.
- 화물 지도 정산·예약 오류 안내를 전용 페이지로 분리했고 관련 시험 23개가 통과했다. 수락 화면 가로 넘침도 고친 APK180을 기기에 설치해 새 화물 추천→수락→상차 사진·완료→하차 사진·완료를 검증했다. 사진 2장의 파일 해시·업로드 영수증·동일 운송 완료 참조를 읽기 전용으로 대조했다. 창고 연계 좌표 누락·전용 조회 오류·화면 하단 겹침과 기존 만료/공개 제안은 보존한다. 실제 FCM·주행·지급·전체 역할 생명주기·commit/push는 별도다.

## 운영자 관점 업무 흐름 검증 r66 (2026-10-16)

- [검증 보고서](../ProjectOverview/operator-workflow-review-r66.md): `Reviewed / LocalTestsValidated / IsolatedQuickHttpValidated / GapsReproduced / ProductUnchanged / DeviceDeferred / OperationalDisabled`. 기존 관련 시험 748개와 별도 격리 Mongo 1개, 훈련 자체 검사 14개가 통과했다. 새 격리 퀵 주문 2건을 실제 API·합성 사진으로 완료하고 신규 발생 중지 후 남은 업무 0을 확인했다.
- 픽업 후 일반 취소의 기사/시각 초기화는 독립 정책 실행으로, 관리자 반복 Start의 409는 실제 HTTP로 재현했다. 수락 후속 복구·APK 사건 상세 경로·관제 조회 오류·음식 사건 최종 인계도 보완 대상으로 기록했다. AI 비교는 Timeout이며 모델 품질/물리폰/실제 알림·송금은 미검증이다. 이전 회차·Docker/볼륨·제품 설정을 보존했고 이번 host는 종료했다. r65 기간 기준 질문은 미답변으로 유지한다.

## 기사 업무 집중 기간 기획 dispatch-core.r65 (2026-10-16)

- [집중 업무·기간 r65](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/driver-work-focus.design.r65.md): `DirectionAccepted / SwitchingRuleAccepted / PeriodBoundaryAsked / ImplementationPendingPolicy`. 복수로 가능한 업무와 일정 기간 집중할 한 종류를 구분한다. 기간 중에는 맡은 업무를 모두 마친 뒤 본인이 변경할 수 있도록 사용자가 선택했다. 현재 코드에는 기간 집중 선택이 없다.
- 다음 질문은 하루/주간 시간 기준이며 `Asked / Unanswered`다. 기존 업무 완료·정산·수신 중지/재개를 보존하며 이번에는 제품·DB·플래그·APK·운영·commit/push를 변경하지 않았다.

## 퀵 기사 후속 콜·연속 수행 보완 dispatch-core.r64 (2026-10-16, 구현·로컬 검증 완료)

- [승인 명세 r64](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/quick-driver-continuity.implementation.r64.md) · [변경·검증](../Changes/2026-10-16-quick-driver-continuity-r64.md): `Implemented / LocalValidated / StandaloneApkBuilt / DeviceDeferred`. 공통 신규 수신 관문·전체 업무 끝점/ETA·네이티브 A→B 전환·연속 API 분류를 연결했다. 기존 화면의 토글과 판본 조건부 최초 ON으로 수동 OFF를 보존한다.
- Fast 474개·Task 12,226개 통과, 실패 0개·환경 의존 16개 미실행. 단독 설치 APK 생성·서명 확인 완료. 기존 운행/수락 업무·기능 기본 OFF·다른 미커밋 작업을 보존했고 물리폰/주행·푸시/청취·외부 운영·commit/push는 수행하지 않았다.

## 퀵 기사 이동 중 후속 콜·연속 수행 검증 dispatch-core.r63 (2026-10-16)

- [검토·검증 r63](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/quick-driver-continuity.review.r63.md): `Reviewed / LocalTestsValidated / GapsIdentified / ImplementationNotAuthorized / DeviceDeferred`. 수행 중 추천 배너·추가 수락의 일정/적재 보호는 있으나 자동 후보의 현재 GPS 기준, 퀵 수신 OFF/미응답 관문 누락, 네이티브 수락 후 기존 카드 보존·자동 다음 업무 연결의 공백을 확인했다.
- 관련 기존 시험 451개 통과·실패/건너뜀 0. 실제 제품 필터에서 연속배차 조회 API의 `404 FeatureBoundaryUnclassified`를 재현했다. 제품 코드·설정·APK·모델·운영·commit/push는 변경하지 않았고 실제 휴대폰·주행/푸시·알림음은 미검증이다. 서버 수신 조건 → 마지막 목적지/종료시각 후보 → 네이티브 A→B 전환 → 별도 예약 마감을 제안한다.

## 연속 미응답 수신 중지와 같은 토글 재개 dispatch-core.r62 (2026-10-16, 구현·로컬 검증 완료)

- [승인 명세 r62](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/driver-no-response.implementation.r62.md) · [변경·검증](../Changes/2026-10-16-driver-no-response-r62.md): `Implemented / LocalValidated / AndroidBuilt / DeviceDeferred / OperationalConfigurationUnchanged`. 앱 표시 확인이 있는 유효 추천의 세 번째 연속 미응답에 신규 수신만 중지하고, 기존 토글의 명시 ON으로 재개한다. 운행·수락 업무·다른 서버 제한은 보존한다.
- 기존 활동 원장·추천 만료·본인 API·음식 기사 네이티브/공통 토글을 연결했다. Fast 433개·최종 Task 12,157개 통과, 실패 0개·환경 의존 16개 미실행. 토글 HTML 3상태와 기존 네이버 지도 설정 포함 Android 빌드를 확인했다. 브라우저 보안 정책상 화면 캡처는 하지 않았다. 휴대폰 설치·실제 FCM/알림음·외부 운영·모델·commit/push는 수행하지 않았다.

## 신규 배차 토글 확인과 본인 수신 재개 dispatch-core.r61 (2026-10-16)

- [답변·검증 r61](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/driver-dispatch-reception-toggle.review.r61.md): `ManualResumeAccepted / SourceAndLocalTestsVerified / DeviceVisualNotVerified / ImplementationGapsIdentified`. 세 번째 연속 미응답 중지 뒤 기사 본인이 토글을 켜서 재개하는 방향을 수용했다. 음식 기사 네이티브 지도 좌측 상단·공통 지도/목록 토글과 기존 서버 수신 연결을 확인했다.
- 기존 집중 시험 156개 통과, 실패·건너뜀 0개. HTML·PageModel·루프백 HTTP/SQLite 근거이며 실제 APK 화면·USB·알림 검증은 아니다. 최초 운행 시작 자동 ON, 서버 중지여도 켜짐 표시, ON만으로 서버 중지 해제 불가와 연속 3회 중지 미구현을 기록했다. 제품·설정·APK·기기·운영·모델·commit/push는 변경하지 않았다. 상세 로그는 `artifacts/local/validation/20261016-dispatch-reception-r61/`다.

## 세 번째 연속 미응답부터 신규 배차 수신 중지 dispatch-core.r60 (2026-10-16)

- [기획 정정 r60](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/driver-no-response-receiving-off.design.r60.md): `ThresholdAndActionAccepted / RecoveryPolicyOpen / ImplementationNotAuthorized`. 사용자 최신 정정에 따라 1·2회 연속 미응답은 유지하고 3회째 신규 배차 수신만 OFF로 전환한다. 운행 종료·기존 배달 취소는 하지 않는 기획 기준이다. r59는 당시 이력으로 보존한다.
- 다음 질문은 `driver-no-response-receiving-resume-mode / Asked / Unanswered`: 기사님이 직접 토글을 켜서 수신을 재개할지다. 직접 거절·통신 장애·집계 초기화·대기 우선순위는 미정이며 기획 문서만 갱신했다. r58 제품·설정·DB·APK·모델·운영은 변경하지 않았다.

## 배차 미응답과 자동 운행 종료 기획 dispatch-core.r59 (2026-10-16)

- [기획 답변 r59](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/driver-no-response-auto-stop.design.r59.md): `DirectionAccepted / ThresholdOpen / SafeguardsProposed / ImplementationNotAuthorized`. 배차에 미응답한 채 운행을 켜두는 상황에서 자체 운행 종료로 이어지는 방향을 수용했다. 직접 거절·통신 문제·수행 중 업무 보호와 대기시간은 별도로 구체화한다.
- 다음 질문은 `driver-no-response-auto-stop-threshold / Asked / Unanswered`: 한 번 만료에 종료할지, 두 번 연속 미응답에 종료할지다. 읽은 기존 운행 종료 경로에는 수행 중 업무 확인 보호가 없으므로 단순 자동 호출을 구현 완료로 보지 않는다. 기획 문서만 갱신하며 r58 코드·설정·DB·APK·모델·운영은 변경하지 않았다.

## 모바일 가상 퀵 운영 dispatch-core.r58 (2026-10-16)

- [승인 설계 r58](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/virtual-quick-operations.implementation.r58.md) · [변경·검증](../Changes/2026-10-16-virtual-quick-operations-r58.md) · [실행 안내](../../eng/Ssalddel.DispatchTraining/README.md): `Implemented / LocalApiValidated / ApkPrepared / UiVisualDeferred / OperationalDisabled / DeviceDeferred`. 기존 관리자 APK에 30분·2분 간격 합성 퀵 운영, 신규 발생 제어·지원 기사 참여·규칙/AI 비교를 연결했다.
- 전용 HTTP·MySQL·Mongo에서 실제 7건의 수락·동의·합성 사진 픽업·전달 완료와 지원 2명을 확인했다. 중지·재시작으로 8슬롯 생략, 재시작 확인 메모 1개 보존이다. 자체 검증 14개·최종 Task 12,100개 통과(환경 의존 16개 미실행), APK 내장 코드 대조 완료다. Ollama 성공 비교와 15건 무중단 수행·화면 픽셀·USB/외부 운영·실제 지급은 미검증이다. 미리보기 숨김 실행은 자동 승인 검토가 차단했고 재시도하지 않았다. 기존 대기·단가 정책과 관련 없는 미커밋 파일을 보존하며 commit/push는 수행하지 않았다.

## 기사 관점 배차 엔진 구성과 보완 검토 dispatch-core.r57 (2026-10-16)

- [검토 r57](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/driver-dispatch-structure.review.r57.md): `Reviewed / GapsIdentified / QuestionsOpen / ImplementationNotAuthorized / WeightTrainingNotStarted`. 음식 Ollama와 규칙 기반 화물·생활 퀵의 현재 역할, 공통 업무 선택/수락 보호, 추천 시 대기 가점 초기화, r56 장기 대기 방향과 점수 정렬의 차이·후속 수행/혼합 근거 공백을 정적으로 대조했다.
- 기획 문서·목차만 갱신한다. 제품·DB·설정·모델·단말·실운영은 실행하거나 변경하지 않았다. 다음 질문은 `quick-driver-waiting-preserve-on-offer / Asked / Unanswered`이며 기존 대기 시작점도 미정이다. 반복 수신한 r102 공동주택 화면 범위 답변은 이미 반영된 것으로 확인했으며 새 구현이나 대기 정책 승인으로 취급하지 않았다.
- 문서 전용 Fast와 관련 링크 6개·새 파일 공백 검사를 통과했다. build·제품 시험은 실행하지 않았다. 검증 로그는 `artifacts/local/validation/20261016-092019/`다.

## 이전 기획: 퀵 배차의 수행 가능성과 대기 우선 dispatch-core.r56 (2026-10-15)

- [기획 답변 r56](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/quick-dispatch-feasibility-waiting.design.r56.md): `AcceptedDirection / QuestionsOpen / ImplementationNotAuthorized / WeightTrainingNotStarted`. 픽업·전달과 기존 업무에 지장 없는 후보를 추린 뒤 장기 대기 기사를 우선하는 방향이다. 고정 ETA 차이 5분은 채택하지 않았다. 다음 질문은 `quick-driver-waiting-clock-start / Asked / Unanswered`이며 수행 중 기사 마지막 목적지 기준을 보존한다.
- 기획 본문과 목차만 갱신했다. 제품 코드·설정·자료 생성/승인·모델 호출/학습·운영 배차·commit/push는 수행하지 않았다. r55 전략과 기존 자료 상태는 그대로다.

## 이전 제안: 퀵 우선 AI 배차 학습 전략 dispatch-core.r55 (2026-10-15)

- [학습 전략 r55](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/quick-training-strategy.design.r55.md): `Proposed / QuestionsOpen / ImplementationNotAuthorized / WeightTrainingNotStarted`. 음식 AI 후보/조합/시점 연결과 퀵 미연결을 구분했다. r51/r52 후보는 각각 4,032개·ready 0이며 r51 정책 원천 2개 해시가 달라 과거 snapshot을 현재 정답으로 쓰지 않는다.
- 대표 퀵 20개 문답 검토·허용 판단/목표 우선순위 → 고정 평가 → 소규모 추가 학습 → 판본화된 Ollama 실행/관측 적용을 제안했다. 로컬 GPU/RAM과 공식 TRL/Ollama/OR-Tools 문서를 읽었으며 제품 코드·자료 생성·모델 호출/학습·실운영·commit/push는 변경하지 않았다. r103 퀵의 금융/취소/규격/수령 미정도 정답으로 만들어 채우지 않는다.

## 이전 검토: 생활 퀵 개별 흐름 검토와 맥락별 커밋 r103 (2026-10-15)

- [퀵 개별 검토 r103](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/quick-delivery-review.r103.md): `Reviewed / GapsIdentified / ImplementationNotAuthorized`. 현재 생활 퀵의 기사 직접 지급과 주간 지급 준비의 차이, 일반 접수 수정·취소, 희망 완료 시각 보존, 수락 전 기사 보수, 물품 적합성, 부재·반송/신고·수령 마감의 공백을 확인했다. 신규 기능·정책은 구현하지 않았다.
- 관련 기존 시험 358개 통과. APK/물리 기기·현장·외부 알림·실수납/이체는 새로 검증하지 않았다. 누적 변경 610파일을 11개 맥락으로 커밋했고 보호 도구 3개·알림음 1개는 로컬 보존했다. push는 수행하지 않았다. 이 검토 보고서는 후속 문서 커밋으로 구분한다.

## 이전 구현: 공동주택 기획의 기존 역할 화면 반영 r102 (2026-10-15, 구현·로컬 화면 검증 완료)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/role-screen-integration.design.r102.md)·[변경·검증](../Changes/2026-10-15-community-role-screens-r102.md): 주민의 기존 필요글 작성에 비구속 입고 희망 진입을, 활동정산 상세에 본인 약정 기본보수 요약을, 기존 입고 목록에 상품별 일정·예정/실입고·변경안/이전 예약 구분을 반영했다. 기존 업무 명령·원장·권한·미확정 값과 응답 유실 복구 경계를 유지한다.
- Fast 34개·Task 12,017개 통과·실패 0개·환경 의존 16개 미실행. 루프백 Chrome 20개 배치와 가상 글 저장·HTTP 재조회·새로 고침을 확인했다. 격리 SQLite 229개 테이블에서 글·기존 음성/키워드 후속만 증가했다. 기본 Preview의 미지원 협업 조회는 오류 안내로 남아 있으며 전체 협업 통합 검증은 아니다. 새 기여금 5%/10% 공제·관리비 반영·공개 일감·자가 피킹·주민 창고 권한은 구현하지 않았다. 기기·외부 운영·실제 지급·Figma·Unity·commit/push는 별도이며 r101과 이전 기획의 미정·구현 이력은 보존한다.

## 이전 기획: 분야 전환·주민 수요와 입고 판단 제안 r101 (2026-10-15)

- [기획 답변 r101](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/workflow-operating-decisions.design.r101.md): `TopicChangeAccepted / NextTopicProposed / ImplementationNotAuthorized`. 사용자의 요청으로 인력·보수 논의를 멈추고 다른 기획 후보를 정리했다. 다음 후보는 주민 수요와 입고 판단, 상품 상태, 잔여 재고 후속 처리다. 주민 입고 희망을 실제 주문과 구분하는 방식을 먼저 제안하며 새 분야의 선택·정책은 아직 미정이다.
- `onsite-task-list-and-piecework-offer-entry`는 `Deferred / NotDecided / TopicChangeRequested`다. 다음 `resident-nonbinding-inbound-interest`는 `Asked / Unanswered`이며 r100 이하 방향·미정·기존 구현 검증 상한을 보존한다. 제품 코드·DB·권한·실제 주민 자료 수집/업무/지급·기기 시험은 변경하지 않았다.

## 이전 기획: 고객센터 1차 검토·처리 인계 기획 r80 (2026-10-15)

- [기획 답변 r80](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/workflow-operating-decisions.design.r80.md): `AcceptedDirection / QuestionsOpen / ImplementationNotAuthorized`. 신고 후 정산 보류를 유지하고 고객센터 상담원이 문제 상황·증빙·당사자 설명을 먼저 파악해 처리 방향을 안내하거나 해당 업무로 인계하는 방향을 수용했다. 상담 처리와 실제 환불·재배송·보류 해제·송금 결과는 구분한다.
- `reported-delivery-issue-release-authority`는 `Answered / SupportTriageAccepted / FinancialAuthorityOpen`, 다음 `support-agent-financial-action-authority`는 `Asked / Unanswered`다. 상담원 직접 확인과 운영자 승인 대상 금융 조치를 나눌지 묻는다. 새 계정/역할·금융 결정권·실제 지급·제품 변경은 아직 승인하지 않았다. 기획 문서와 목차만 갱신했으며 r76 구현은 그대로다.

## 이전 기획: 고객 문제 신고 시 정산 보류 r79 (2026-10-15)

- [기획 답변 r79](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/workflow-operating-decisions.design.r79.md): `AcceptedDirection / QuestionsOpen / ImplementationNotAuthorized`. 고객이 미수령·누락·파손을 신고하면 해당 주문의 미지급 정산을 보류하는 방향을 수용했다. 전달 증빙·재원이 있는 단순 무응답 건과 구분하며, 일부 작업 보수 선지급까지 승인한 것으로 해석하지 않는다. 수행 기록·발생액은 보존한다.
- 질문 `reported-delivery-issue-payment-scope`는 `Answered / HoldAccepted / PartialReleaseUnresolved`, 다음 `reported-delivery-issue-release-authority`는 `Asked / Unanswered`다. 초기 운영자가 양측 설명·전달 증빙을 검토해 해제 여부를 결정할지 묻는다. 다른 주문·주간 전체 동결·자동 환불·기지급 회수·책임 판정·구현 승인은 포함하지 않는다. 기획 문서만 갱신했으며 r76 제품·설정·실금전은 그대로다.

## 이전 기획: 전달 증빙·지급 재원 기반 작업 보수 r78 (2026-10-15)

- [기획 답변 r78](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/workflow-operating-decisions.design.r78.md): `AcceptedDirection / QuestionsOpen / ImplementationNotAuthorized`. 사용자 답변에 따라 전달 증빙과 해당 작업 보수의 지급 재원이 확인되면 고객 무응답과 별도로 확인된 작업 보수를 정산할 수 있는 방향을 확정했다. 고객 수령 상태·공동 성과금·실제 입금·기존 주간 합산은 별개로 유지한다.
- 기존 질문 `unconfirmed-receipt-earned-work-release`는 `Answered / AcceptedDirection`, 다음 `reported-delivery-issue-payment-scope`는 `Asked / Unanswered`다. 실제 미수령·누락·파손 신고에서 문제 관련 금액과 다툼 없는 작업 보수의 처리를 다음 문답으로 남긴다. 기획만 반영했으며 r76 제품 코드·DB·설정·실제 지급·기기·외부 서비스·commit/push는 변경하지 않았다.

## 이전 검토: 운영 약속·예외 종결 기획 문답 r77 (2026-10-15)

- [검토 초안](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/workflow-operating-decisions.review.r77.md): `Draft / QuestionsOpen / ImplementationNotAuthorized`. 기존 r76 이후 코드 수정 대신 기획 문제를 다시 검토했다. 확인한 작업 보수의 예외 지급, 손실 재원·책임, 재작업 담당·대가, 공간 회수 종결, 운영자 부재 시 서비스 약속이 우선 문답 대상이다.
- 질문 `unconfirmed-receipt-earned-work-release`는 `Asked / Unanswered`다. 전달 증빙·보수 재원이 확인돼도 고객 무응답인 물품 주문에서 수령 상태와 별도로 작업 보수 검토·정산을 허용할지 묻는다. 기존 보수 발생/지급 구분은 유지하며 아직 자동 지급·보상·수령 완료 정책을 승인하지 않았다. 제품 코드·설정·데이터·기기·외부 서비스·commit/push 변경 없이 기획 문서와 목차만 갱신했다. r76 구현·검증은 아래와 같다.

## 휴대폰 없이 업무 예외 마감 보완 r76 (2026-10-15)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/workflow-exception-closeout.implementation.r76.md)·[구현 결과](../Changes/2026-10-15-workflow-exception-closeout-r76.md)·[역할별 업무 트리](../ProjectOverview/page-docs/role-workflow-tree-r76.md): 운행 시작 거절/유실, 화물 보수 수락·복수 사건 금융 보존, 음식 배차 지연·재조리·지원 인계, 입고 완료·마트 수령 해결·공간 반출과 본인 주간 명세를 보완했다. 반출 후 추가 보관료·기간 원가 배부는 근거 재확인으로 보류한다. Fast 481개·제품 Task 11,996개·공통 규칙 Task 2,001개 통과, 실패 0개·환경 의존 16개 건너뜀·소비 앱 빌드 오류 0개다.
- 기존 기본 OFF·Simulation·전문 원장·권한·사진·판본·같은 요청 복구와 미커밋 작업을 보존한다. 음식 취소/환불/재조리 비용·불량 처분·반출 추가 보수·활동 지급 신고의 프로세스 종료 뒤 복구는 별도 후속이다. 휴대폰·브라우저 시각·새 APK 설치·Azure/GCS·실제 FCM/송금·운영 활성화·Unity 장면·commit/push는 수행하지 않는다.

## USB 없이 주문·배차·기사 앱 검증 r75 (2026-10-15)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/dispatch-device-cloud-verification.implementation.r75.md)·[결과](../Changes/2026-10-15-local-dispatch-verification-r75.md): 화물 후보 LINQ 오류·푸시 식별자/클릭 복귀·운행 시작 유실 복구·부가 조회 장애/미확인 표시·음식 수락 후 보수 근거를 보완했다. 전체 소비 앱 빌드 오류 0개·Task 11,865개 통과/실패 0개/환경 의존 16개 건너뜀·집중 202개·별도 정책/복구 9개 통과. 기존 미커밋 작업을 보존한다.
- 음식·화물 APK 0.1.77/177의 서명/해시/현재 소비 소스를 확인하고 음식 ‘살뜰 배달!’ 음원의 패키지 내부 해시도 대조했다. Debug USB 로컬 서버용이며 설치하지 않았다. 실제 FCM·소리 청취·잠금/종료·사진/카메라·시각 검수는 휴대폰 연결 뒤 별도다.
- 기존 Azure F1 앱은 PC HTTPS 요청에 응답했으나 health 두 경로 404로 Hongdal 배포/업무 준비는 미확인이다. Azure 리소스/배포·운영 migration·실금전·commit/push는 변경하지 않았다. 전용 실행은 격리 SQLite와 명시 대체 포트이며 실제 MySQL/Mongo·Ollama/FCM·화물 HTTP/사진/완료의 성공으로 확대하지 않는다. 일반 화물 기사 보수 미확인과 SignalR/AndroidX 경고는 후속이다.

## USB 실제 휴대폰 APK 검증 r74 (2026-10-15)

- 일곱 앱 설치·기기 APK 해시 대조, 로그인·네이버 지도·주문/재고 목록과 상세·알림 설정·운영자 연결 복구·운행 시작/종료를 확인했다. 최종 기기 판본: 화물 기사 175, 음식 기사 176, 나머지 다섯 앱 174. [검증 결과와 화면](../Changes/2026-10-15-usb-apk-device-verification-r74.md).
- 기사 API 실행 분류·공간 조회 DI 순환·운행 시작 모드·지원 화면 잘림·commit 후 추천 실패에 따른 시작 실패 응답을 보완했다. 최종 전체 앱 빌드 오류 0, Task 11,779개 통과·환경 의존 16개 건너뜀, 실패 0. 기존 미커밋 작업은 보존한다. 두 기사 시험 근무 종료를 SQL과 대조했고, 화면 유지 설정은 복원했다. 격리 로컬 서버·USB 연결은 사용자 화면 확인용으로 유지한다.
- 유가/경로 미설정으로 정상 추천·수락 이후 촬영·배송/수령 완주는 차단됐다. 실제 FCM·결제/송금·Azure/GCS·운영 migration·Release 배포와 commit/push는 미실시다. 설치/로컬 검증을 운영 준비 완료로 합치지 않는다.


## 배송료 선결제·화물 주간 지급 준비 r73 (2026-10-15, 로컬 검증 완료)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/transport-prepayment-settlement.implementation.r73.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/transport-prepayment-settlement.r73.work-order.json): 저장 배송료/혜택 근거, 토스 입금 상태와 동일 결제 재조회, 승인 후속 상태 보존, 완료 주간 기준 화물 지급 준비 명세를 보완했다. 두 옵션 기본 OFF·실지급 Provider 미연결. [구현·검증](../Changes/2026-10-15-transport-prepayment-settlement-r73.md).
- 사용자 기준은 업무별 건별 보수 발생과 한 사람 기준 주간 합산이다. 음식·피킹까지의 통합 명세/공통 지급·주간 자동 마감·실송금은 아직 후속이다. [주간 연결 확인](../ProjectOverview/page-docs/weekly-worker-payout-audit-r73.md). Fast 268개·Task 11,736개 통과·환경 의존 16개 건너뜀·전체 소비 앱 빌드 오류 0개이며 공용 DB·실금전·휴대폰 설치·운영 ON·commit/push를 하지 않는다.


## 기사 직선거리 요금과 추천 배차 표시 r72 (2026-10-15, 구현·로컬 검증 완료)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/straight-line-driver-pricing.implementation.r72.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/straight-line-driver-pricing.r72.work-order.json): 음식/소형 배송 기사 거리비를 픽업→전달 직선 1km당 1,000원·원 단위 비례 반올림으로 준비하고, 두 기사 앱의 업무별 색 배너와 실제 맡을 작업/요금 근거를 연결한다. 기본/할증·기존 합의/동결·고객 청구·일반 화물·안전 관문을 구분한다. 기본 OFF이며 [변경·검증](../Changes/2026-10-15-straight-line-driver-pricing-r72.md)에 최종 범위를 기록한다.

- Fast 246개·Task 11,636개 통과, 실패 0개·환경 의존 16개 건너뜀·전체 소비 앱 빌드 오류 0개/경고 61개. 공통 Razor 정적 렌더 5상태·기존 범위 밖 변경 파일 341개 보존. 브라우저 연결 부재로 화면 캡처는 미실시이며 실제 휴대폰·새 APK·운영 ON·commit/push는 별도다.

## 기사 업무 선택과 공통 수락 보호 r71 (2026-10-15, 구현·로컬 검증 완료)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/driver-participation.implementation.r71.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/driver-participation.r71.work-order.json): 기사 본인의 음식·퀵·화물 희망과 직접 피킹 옵션, 공통 미완료 업무 수락 검사, 두 기사 앱 설정을 연결했다. 전문 원장·기존 자격·일정·사진·보수는 유지한다. Fast 493개·분산 묶음 5개·Task 11,550개 통과, 환경 의존 16개 건너뜀·전체 앱 빌드 오류 0개. 별도 실제 MySQL 두 연결의 마지막 한 자리 수락 시험 1개 통과. [변경·검증](../Changes/2026-10-15-driver-participation-r71.md). 기능 기본 OFF·혼합 근거 부족 시 보류·지도/배차 목록 통합·새 APK 배포/실기기·운영/commit/push 별도.

## 비공개 APK 준비와 화주 인수증 연결 r70 (2026-10-15, 구현·로컬 검증 완료)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/private-apk-preparation.implementation.r70.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/private-apk-preparation.r70.work-order.json): 기존 인수증 API의 모바일 입력·유실 재조회, 로컬 GCS APK 준비와 역할별 현황을 보완했다. Fast 155·Task 11,425개 통과, 환경 의존 15개 건너뜀·전체 소비 앱 빌드 오류 0개. 공통 패널 HTTP SSR 4상태와 로컬 Debug APK 두 개(0.1.70/170)의 서명·ABI·해시를 확인했다. [현황·다음 순서](../Deployment/PrivateApkPilotReadiness-20261015.md)·[결과](../Changes/2026-10-15-private-apk-preparation-r70.md). SignalR/.NET 보안 갱신, 외부 HTTPS·초대 접근·푸시·실기기는 후속이며 실제 업로드·운영·commit/push는 별도다.

## 국내 화물 한 건 마감 r69 (2026-10-14, 구현·로컬 검증 완료)

- [수용 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/cargo-closeout.implementation.r69.md): 화주 작성 원문·실제 동의 연결, 접수 원장/사진/완료 인수 보호, 기사 상차 재조회·암호화 사진 복구·영속 후속, 본인 운송대금 목록/상세를 보완했다. 기존 음식·마트·분산 업무, 금융 상태와 실제 송금은 구분한다.
- Fast 661개·API 경계 5개·Task 11,395개 통과, 환경 의존 MongoDB 15개 건너뜀·전체 소비 앱 빌드 오류 0개. localhost HTML 9개 상태와 격리 MySQL/MongoDB 화물 5단계, 서버 재시작 뒤 완료 인수·삭제 보호·지급 준비·후속 Done·금융 상태 보존을 확인했다. [구현·실행·한계](../Changes/2026-10-14-cargo-closeout-r69.md). 브라우저 시각·물리 기기·실입금·운영 활성화·commit/push는 별도다.

## 마트 고객 수령과 기존 보수 지급 조건 연결 r68 (2026-10-13, 구현·로컬 검증 완료)

- [수용 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/mart-customer-receipt.implementation.r68.md): 실제 주문자의 전량 수령·부분/누락/파손 보류를 기존 마트배송시도에 보호 저장하고 기존 정산 지급 관문에 연결했다. 기사 전달과 수령, 접수 기록과 현재 검증을 분리하며 사전 합의·기존 보수·지급 기록을 보존했다. 신규 창고 시각은 SQL/JSON의 UTC 마이크로초 정밀도를 맞췄다.
- 집중 45개·Fast 347개·DB 호환 Fast 4개·Task 11,236개 통과, 환경 의존 MongoDB 15개 건너뜀, 최종 소비 앱 빌드 오류 0개. 범위 밖 기존 파일 171개를 보존했다. 운영 기본 OFF·Simulation을 유지하며 보류 해제·실제 MySQL/동시 HTTP·화면/단말·공유 DB 적용·실제 이체·Azure/GCS·Unity·commit/push는 후속이다. [변경 및 검증](../Changes/2026-10-13-mart-customer-receipt-r68.md).

## 주문별 원가·보수와 마트 작업 근거 연결 r67 (2026-10-13, 구현·로컬 검증 완료)

- [수용 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/mart-order-compensation.implementation.r67.md): 사용자 선택 1·2번에 따라 기존 원주문의 참조 아래 본인 원가·보수를 읽고, 마트 피킹·포장의 실제 완료 근거를 기존 정산에 연결한다. 입고 작업자 채무와 주문별 원가 귀속은 구분하며 미정 단가·불명확한 과거 근거를 0원이나 완료로 바꾸지 않는다.
- 기존 r59~r65 미커밋 작업을 보존했다. 실제 완료자/수량 동결, 작업 전 합의 판본, 전역 중복 보호와 권한을 구현했다. Fast 250개·Task 11,202개 통과, 환경 의존 MongoDB 15개 건너뜀, 앱 소비 프로젝트 빌드 오류 0개. 운영 기본 OFF·Simulation을 유지하며 신규 앱 화면·APK·공유 DB 적용·고객 수령 후 지급 해제·실제 지급·Azure/GCS·Unity·commit/push는 후속이다.

## 역할별 업무 시작·완료 연결 검증 r66 (2026-10-12, 조사·재현 완료)

- [역할별 검증 보고서](../ProjectOverview/page-docs/role-work-completion-audit-r66.md): 음식 주문자/음식점/기사, 공급자/공간 제공자/입고·피킹/물품 배송/수령자, 지급자/수령자/운영자의 시작·마무리와 화물 완료 인계를 현재 코드·기존 시험으로 대조했다. 정상 작업·상대방 확인·정산·지급 신고·사건 종결은 별개다.
- 기존 집중 시험 620건 통과·실패/건너뜀 0, 현재 OS 카탈로그 63개 검사 통과. 독립 SQLite 두 개에서 같은 입고 행·재고가 수령/취소 순서에 따라 `PartiallyReceived`/`Received`로 달라지는 문제를 재현했다. 지급 신고 화면 연결, 공간 종료 재고 반출, 음식 수락 후 취소와 장기 미수령의 예외 종결이 주요 후속이다. 제품 수정·실제 계약/송금·기기/Azure/Unity·운영 설정 변경·commit/push는 수행하지 않았다. 이전 미커밋 작업은 보존한다.

## 입고·보관 공통 UI 구조 정리 r65 (2026-10-12, 로컬 검증 완료)

- [수용 설계](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/inbound-cleanup.implementation.r65.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/inbound-cleanup.r65.work-order.json): 사용자의 정리 우선 요청에 따라 같은 ViewModel 안의 주문/사진 책임을 partial로 나누고 표시 중복을 기존 공통 파일로 모은다. 화면/문구/동작·서버/원장·Recovery 형식은 보존한다. [변경·검증·후속](../Changes/2026-10-12-inbound-cleanup-r65.md). 새 기능·서비스·DI·운영 활성화·commit/push는 범위 밖이다.

- 검증: Fast 225건·Task 11,167건 통과·실패 0건·환경 의존 MongoDB 15건 건너뜀·전체 앱 빌드 오류 0개/경고 145개. 기존 멤버 90개·Razor 출력 10개·CSS 3개·보호 파일 38개 보존을 확인했다. 기기 설치·운영 활성화·commit/push는 수행하지 않았다.

## 입고·공간·보관 협의 시각 정돈 r64 (2026-10-12, 로컬 검증 완료·운영 OFF)

- [수용 설계](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/inbound-visual.implementation.r64.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/inbound-visual.r64.work-order.json): 사용자 후속 요청에 따라 컬러 이모지 없이 단색 아이콘·수량/상태 위계·의미별 섹션·2열/좁은 폭 배치를 기존 r63 공통 화면에 적용한다. 서버/VM/계약/정산/승인 조건은 유지한다. [화면·검증](../Changes/2026-10-12-inbound-visual-r64.md). 기본 OFF·기기/Azure·commit/push 별도.

- 검증: Fast 74건·Task 11,167건 통과·환경 의존 MongoDB 15건 건너뜀·전체 앱 빌드 오류 0개/경고 144개·로컬 Edge 9개 배치 확인. 실제 단말/인증 HTTP·공유 DB 적용은 별도다.

## 공급자·공간 제공자 입고 협의 구현 r63 (2026-10-12, 로컬 검증 완료·운영 OFF)

- [수용 설계](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/supplier-space-inbound.implementation.r63.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/supplier-space-inbound.r63.work-order.json): r62 제안을 승인받아 공간 수용 조건·품목/포장/온도/용량 검사, 입고 행별 제안·동일 판본 양측 동의·예약, 현재 보관 계약과 변경안 분리를 기존 서버·공통 모바일 UI에 연결한다.
- [페이지→코드→DB 책임](../ProjectOverview/page-docs/distributed-inbound-collaboration-r63.md)·[변경 및 검증](../Changes/2026-10-12-supplier-space-inbound-r63.md). Fast 279건·Task 11,167건 통과, 실패 0건·환경 의존 MongoDB 15건 건너뜀·전체 앱 빌드 오류 0개. 예약은 실제 재고·보수가 아니며 사진·바코드·실수량이 있는 입고만 정산 근거가 된다. 기본 OFF·Simulation 유지, Unity·실제 DB/단말/Azure·실제 계약/지급·commit/push는 별도다. 기존 미커밋 작업은 보존한다.

## 공급자·공간 제공자 협의와 계약 후 입고 r62 (2026-10-12, 방향 기록·후속 설계)

- [현재 기획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/supplier-space-inbound.design.r62.md): 농가·공장·제조업체와 공간 제공자의 협의, 농산물 거부/공산품 허용 등 수용 조건, 계약 후 건별 물량·일정 승인을 제안했다. 기존 양측 보관 합의·합의 전 초안·사진 수령은 이미 있는 기반으로 구분했다.
- `AcceptedDirection / ProposedDesign / ImplementationPending / OperationalDeferred`. 조사·문서 작업이며 제품 코드·DB·APK·Unity·실제 계약·외부 메시지·운영 활성화·commit/push는 수행하지 않는다. 다음 구현 순서는 공간 수용 정책 → 계약 협의/입고 예약 승인 → 수령·공간 전체 용량·r60/r61 원가 연결이다. 첫 구현안은 건별 명시 승인이며 반복 자동 입고는 미확정이다. 기존 미커밋 구현은 보존한다.

- 문서 전용 Fast 차이 검사 통과·새 기획서 링크 17개 경로 확인·독립 읽기 검토 완료. 제품 빌드/업무 시험은 미실행이며 검사 기록은 `artifacts/local/validation/20261012-133948/`다.

## 원가 귀속·플랫폼 몫 r61 (2026-10-12, 로컬 검증 완료·운영 OFF)

- [수용 설계](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/settlement-economics.implementation.r61.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/settlement-economics.r61.work-order.json): 사용자 선택 1·2번의 입고·보관 원가 귀속 및 잔여금 5%/10% 플랫폼 몫을 기존 정산 원장에 연결했다. 무료 확인·원 단위 잔액·원천 조정·동시 변경·본인 정보·기간 중복 환원을 보호한다.
- [검증 반환](../Changes/2026-10-12-settlement-economics-r61.md): Fast 239건·Task 11,088건 통과, 환경 의존 MongoDB 15건 건너뜀·전체 앱 빌드 오류 0. 순차 피킹 뒤 과거 보관 수량과 플랫폼 증빙 비노출도 확인했다. 실제 DB·외부 지급·운영·단말·Azure·Unity·AI 경제 연계·commit/push는 별도다. 기존 미커밋 작업은 보존한다.

## 입고·보관과 출고·배송 정산 분리 r60 (2026-10-12, 로컬 검증 완료·운영 OFF)

- [수용 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/receiving-outbound.implementation.r60.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/receiving-outbound.r60.work-order.json): 기존 Lot/주문을 재사용해 새 약정의 업무 경계·지급 시점을 동결하고, 부분 입고·보관 근거와 출고 후 고객 수령 관문을 분리했다. 보수 발생과 실제 지급은 별개다.
- [검증 반환](../Changes/2026-10-12-receiving-outbound-r60.md): Fast 159건·Task 11,005건 통과, 환경 의존 MongoDB 15건 건너뜀·빌드 오류 0. 목록·상세의 업무 구분, 당시 항목별 지급 조건, 과거 채권·환불·멱등 재조회 보존을 격리 SQLite와 HtmlRenderer로 검증했다. 실운영·은행·단말·Azure·Unity·커밋/푸시는 별도이며 이전 r53/r54/r59 작업을 보존한다.

## 생활 공간 활동 보수·공동 성과금 r59 (2026-10-12, 로컬 검증 완료·운영 OFF)

- [수용 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/activity-compensation.implementation.r59.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/activity-compensation.r59.work-order.json): 실제 주문/수량/처리자/사진 근거, 기본 보수와 추가 성과금 분리, 최신 판본 동의·계산 지문·중복 발생 보호, 본인 활동/정산 목록·상세를 연결했다.
- [검증 반환](../Changes/2026-10-12-activity-compensation-r59.md): 전체 앱 빌드 오류 0·Fast 518건·Task 10,957건 통과, 환경 의존 MongoDB 15건 건너뜀. 실제 격리 입고/사진/피킹/배송/수령→정산 연결과 HtmlRenderer를 검증했다. 구형/미귀속 입고·보관은 추정하지 않는다. 경제 입력은 Simulation 합의 진술이고 실제 지급/은행/단말/Azure/운영 활성화/커밋·푸시는 별도다. 기존 r53/r54 미커밋 작업을 보존한다.

## 음식 배차 알림 복구·기사 겸 운영자 r54 (2026-10-11, 로컬 통합 검증 완료·운영 OFF)

- [승인 설계](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/notification-recovery.implementation.r54.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/notification-recovery.r54.work-order.json)에 따라 사용자 선택 1·2·4번을 구현했다. 유효한 추천의 누락 알림 복원, 제한 재시도·동시 발송 방지·중단 복구, 일시 오류 경보 억제와 조치 안내를 연결한다. r53 변경을 보존한다.
- [변경/검증 기록](../Changes/2026-10-11-food-dispatch-notification-recovery-r54.md): 일반 기사 두 건 연속 수행·동일 저장소의 HTTP host 재생성·본인 우대 없는 선정까지 검증했다. 최종 Fast 107개·격리 Mongo 24개를 포함한 Task 전체 10,895개 통과, 실패/건너뜀 0·빌드 오류 0이다. 최초 실패와 수정 이력·임시 DB/컨테이너 정리·이전 작업 보존을 기록했다. 실제 기기·FCM·GPS·전체 프로세스/Redis 재시작·운영 활성화·Azure·학습·commit/push는 별도다.

## 음식 배차 정보 준비 상태 r53 (2026-10-11, 로컬 검증 완료·운영 OFF)

- [수락 설계](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/information-readiness.implementation.r53.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/information-readiness.r53.work-order.json)를 현재 스레드 요청으로 승인·해시 고정했다. 기존 원주문·현재 조리·좌표·주문 풀·수요·기사 여력을 확인하고 수집 판단에 결속한다. 누락·만료이면 추가 수집 대신 기존 후보/요금 관문으로 복귀한다.
- 수집 판단의 준비와 전체 당사자 조율 준비를 구분한다. 고객 확약·비용 동의·기사 일정/적재·현재 후보 견적·플랫폼 확정 재원은 실제 확인 근거가 없으면 부족으로 보존한다. 금액을 0으로 만들거나 상품 매출/Simulation 동의를 운영 재원으로 승격하지 않는다.
- [변경/검증 기록](../Changes/2026-10-11-food-dispatch-information-readiness-r53.md): Fast 217개 통과 후 사용자 후속 요청으로 기존 Docker Mongo 이미지를 격리 replica set/standalone 시험에 재사용했다. Mongo Fast 24개·최종 Task 전체 10,855개 통과, 실패/건너뜀 0·solution 빌드 오류 0이다. 처음 건너뛴 15개는 활성화 뒤 23개로 펼쳐졌고 문서 왕복 1개도 실검증했다. 시험용 집계 오류와 임시 공간 여유 부족의 최초 실패 기록을 보존한다. 시험 DB/컨테이너 정리 완료·기존 DB/볼륨 변경 0이다. 원장 재조회 뒤 정보 만료·재조리 이전 예정시각 재사용을 차단하고 설계 해시·기준선 HEAD·빈 index·명세 밖 소스 변경 0을 유지한다. 기존 기본 OFF·r52 시간 상한·후속 후보/요금/수락 관문을 유지하며 실제 모델·외부 API 추가 호출·알림·학습·APK 설치/단말 실행·USB·Azure·Unity·커밋/푸시는 수행하지 않았다.

## 음식 주문 수집·배차 시점 판단 r52 (2026-10-11, 로컬 검증 완료·운영 OFF)

- [수락 설계](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/batch-timing.implementation.r52.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/batch-timing.r52.work-order.json)에 따라 첫 음식 추천 전에 짧게 주문을 모을지 현재 배차로 진행할지 서버 정책과 제한 AI 선택을 연결했다. 최초 접수 시각의 대기 상한·자료 신선도·확인된 조리 예정 시각·큐 판본을 재확인한다. 공유 기본값은 OFF다.
- [변경/검증 기록](../Changes/2026-10-11-food-dispatch-batch-timing-r52.md)·[자료 실행 안내](../../eng/local-dispatch-ai/DispatchTimingScenarioRunner/README.md)를 따른다. 신규 135개 포함 Fast 244개, Task 전체 10,753개 통과·실패 0·Mongo 환경 의존 15개 건너뜀이다. 별도 자료 도구 빌드 오류/경고 0·Python 42개 통과, 8유형의 정책 초안 4,032개·대표 24개를 준비했다. 지점 교차/입력 중복 0, ready 0행·모델 호출/학습/사람 승인/운영 변경 0이다.
- 수집 중 새 제안·요금·알림은 만들지 않는다. 기존 수요 판정 투영 복구는 가능하다. 실제 다건 공동 수락·4,000원 수렴 점수·모델 학습/운영 활성화는 별도다. 자료는 현재 순수 정책의 검토 초안이며 실제 AI 프롬프트 학습용 변환은 후속이다. 작업 명세 밖 소스 변경 0을 확인했다.

## 배차 학습자료 1차 제작 r51 (2026-10-11, 후보 제작 완료·사람 검토 대기)

- [수락 설계](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/training-materials.implementation.r51.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/training-materials.r51.work-order.json)에 따라 기존 504건·339지점의 출처와 합성 상황을 결합해 현재 정책 답안 초안 4,041개를 만들었다. 중복 후속 픽업 9개를 격리하고 후보 4,032개를 학습 2,784·검증 856·평가 392로 나눴다. 지점 교차·후보 입력 중복은 0이다.
- [변경/검증 기록](../Changes/2026-10-11-dispatch-training-materials-r51.md)과 [실행 안내](../../eng/local-dispatch-ai/TrainingMaterialRunner/README.md)를 따른다. 대표 24개를 한국어 검토표로 준비했다. 별도 도구 빌드 오류 0·Python 반례 45개 통과, 생성/컴파일 동일성 재실행을 확인했다. 실제 적재·도로 경로·최적 선택 검증은 아니며 모든 답안은 Pending, ready 0행·모델 호출/학습/사람 승인/운영 변경 0이다. 제품·원본·다른 dirty 작업을 보존한다.

## 지역 배차 판단 준비 r50 (2026-10-11, 로컬 시나리오 완료·단말/운영 보류)

- [승인 설계](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/regional-management.implementation.r50.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/regional-management.r50.work-order.json)에 따라 2·3·4·5번(기사 부족/과잉·마지막 전달지 후속 픽업·단건/2건 조합)을 기존 정책과 서버 권위 안에서 구현했다. 새 후속 픽업·권역 검토 AI 옵션은 기본 OFF다.
- 중랑·동대문·광진504건·339지점/확인 메뉴488항목의 비공개 파생자료를 준비했다. 고객 위치·기사 GPS는 합성이며 역사 가격·방문 요약 메뉴를 현재 단건 값으로 승격하지 않는다. [변경/검증 기록](../Changes/2026-10-11-regional-dispatch-management-r50.md)과 [실행 안내](../../eng/local-dispatch-ai/RegionalScenarioRunner/README.md)를 따른다.
- 최종 모의27개와 대표 실제 모델2회가 포함된27개를 통과했다. 앞선 실제4회를 포함한 r50 누적6/6회, 실제 선택은 모두 기준과 동일하다. SQLite6개를 독립 재조회했다. Python22개·Fast214개, Task `20261011-171622` 전체10,618개 통과·실패0·실제 Mongo 환경 의존15개 건너뜀이다. solution 빌드의 회원/관리자 Firebase 미설정 경고6개·오류0을 기록했다. 사람 검토 Pending, 모델 가중치 학습·실제 멀티 원자적 수락·전역 미래 끝점 탐색·단말/외부 FCM·운영 활성화·Unity·commit/push는 별도다.

## 주문·Ollama 배차·기사 앱 통합 시나리오 r49 (2026-10-11, 로컬 검증 완료·단말/외부 운영 보류)

- [승인 설계](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/workflow-scenarios.implementation.r49.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/workflow-scenarios.r49.work-order.json)에 따라 실제 주문·기사 Controller의 루프백 HTTP, 격리 SQLite, 기존 후보/추천/요금 서비스와 FDriver API·수신기·PageModel을 연결했다. 주문→음식점 수락→추천→앱 내부 안내→수락·도착·픽업·전달→별도 수령 확인을 실행한다. 제품 소스·설정은 변경하지 않는다.
- 신규 통합 22개와 최종 Fast 210개가 통과했다. Task `artifacts/local/validation/20261011-155651`은 solution 빌드 오류 0·10,522개 통과·실패 0·환경 의존 15개 건너뜀이다. 새 실행 도구 최종 빌드는 경고/오류 0이다. 최초 시험 빌드 실패는 보존하고 실제 저장소 API에 맞춰 수정했다.
- [변경·실행 기록](../Changes/2026-10-11-local-dispatch-workflow-scenarios-r49.md): 같은 선택 소스의 모의 세 흐름 성공 후 실제 `20261011T065859Z-actual-077d3d06`의 세 흐름을 통과했다. 같은 권역·인접권은 Ollama 각 1회 선택 적용, 현재 수요는 단일 허용 후보여서 호출 0회다. 실제 두 선택 모두 기존 기준과 같아 판단 개선 증거는 아니다. r49 시작 예산 2/4, 이전 예산·결과는 보존했다. 각 DB를 종료 뒤 독립 재조회했다.
- 추천 저장 뒤 알림 Outbox 생성 실패와 Failed 재발송 부재 2변형을 재현했다. 공백 발견이며 제품 수정 완료가 아니다. 완료 뒤 후속 동기화 Outbox 1개씩은 Pending이다. 알림 성공은 기록용 포트이며 실제 FCM·Android 배너/소리·화면 렌더·생산 JWT·자동 이벤트 예약·운영 DB/정산·APK/USB·Azure·commit/push는 미실행이다. 사용자 요청에 따라 로컬 통합 시험까지만 진행했다.

## 음식 배차 수행 경로의 Ollama 후보 선택 r48 (2026-10-11, 로컬 검증 완료·외부 운영 보류)

- [승인 설계](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/candidate-application.implementation.r48.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/candidate-application.r48.work-order.json)에 따라 서버 DI→음식 후보 정책→제한 Ollama 선택→현재 후보/원장 재확인→기존 추천·요금·감사 저장을 연결했다. 실제 허용 후보·픽업 단계·최신 수요 우선권을 유지하고 실패·지연에는 현재 엔진 기준 후보로 돌아간다. 새 API·원장·UI·화물/Unity는 추가하지 않는다.
- typed 서비스 65개·정책/직접 추천/SQLite·DI 96개, 합계 161개 집중 시험이 통과했다. 새 검증 도구 빌드는 경고·오류 0이며 기본 실행은 추론·배차 0회다. 최초 Fast 166개와 최종 Fast 169개(`artifacts/local/validation/20261011-152240`)가 통과했다. 최초 Task의 메타데이터 예상 목록 누락 1개 실패를 보존하고 관련 시험 한 단계만 보완했다. 최종 Task `20261011-152313`은 solution 빌드 오류 0·전체 10,500개 통과·실패 0·환경 의존 15개 건너뜀이다.
- [변경·실행 기록](../Changes/2026-10-11-local-dispatch-candidate-application-r48.md)의 실제 실행 `20261011T061842Z-6f2e87e0`은 Ollama 1회·현재 허용 선택 적용·격리 SQLite 저장 후 재조회를 확인했다. 모델은 기존 엔진과 같은 합성 기사를 골랐고 시험 고정 요금 4,000원 동결·추천 라운드 1·감사/활동 각 1건, 중복 전환 없음이다. 전체 실행 8.923376초는 DB 준비를 포함하며 판단 개선·실제 요금 계산·제품 호스트의 10초 설정 검증은 아니다. r48 누적 시작 예산은 1/2이고 두 번째 호출은 하지 않았다.
- 공유 기본값/예시는 OFF, Git 제외 PC 설정에만 ON·timeout 10초다. 검증의 외부 알림 요청 1건은 시험 경계에서 억제했고 실제 고객 변경·외부 발송·기사 수락·제품 호스트 실행은 없다. r47 최종 0개 통과/4개 복귀와 r46 기록·소진 예산을 보존하며 소스 변경에 따른 과거 근거 만료를 정상 처리한다. 운영 배포·APK·단말·Azure·학습·commit/push는 미실행이다.

## 배달권·기사 후보 근거를 연결하는 로컬 AI r47 (2026-10-11, 로컬 검증·모델 품질 한계·운영 OFF)

- [승인 설계](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/candidate-advisory.implementation.r47.md)·[작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/candidate-advisory.r47.work-order.json)에 따라 기존 음식 후보 정책의 같은 권역·인접권·제한 확장·후보 관문·선정 근거를 읽기 전용 결과로 분리하고 실제 정책을 합성 입력에서 실행하는 별도 도구와 제한된 Ollama 검토를 연결한다. 운영 알고리즘·최종 서버 권위·기존 기본 OFF를 유지한다.
- 실제 후보 정책 합성 10개·내부 검사 82개, 후보 정책 집중 시험 61개(신규 21·기존 40), Python 97개(신규 53·이전 44)가 통과했다. 기본 준비 검사는 10개·모델 0회다. Fast `artifacts/local/validation/20261011-142931`에서 서버/시험 두 프로젝트 빌드와 61개 시험을 확인했고 최종 Task 근거는 작업 명세에 별도 기록한다.
- [변경 기록](../Changes/2026-10-11-local-dispatch-candidate-advisory-r47.md)의 실제 모델 실행 `20261011T052841Z-1966597c9499`은 4회 완료, 최초 1개 관문 통과·3개 복귀였다. 관련 출처 대응 검사를 보강한 뒤 원응답 재검증에서는 0개 통과·4개 기존 정책 결과 유지다. 최초 결과와 후속 재검증을 별도 보존했고 추가 추론 0회·사람 승인 0건이다. r47 누적 예산 4회는 소진됐으며 이전 r46의 36회 기록·원응답·보고서를 보존한다.
- 현재 정책·사용자 확인 방향·합성 가정·미구현 제안을 구분한다. 약 4건/h 방향을 현재 기본 3.5건/h로 덮어쓰지 않고 시간대 가점과 마지막 전달지 기반 다음 픽업을 구현 사실로 표현하지 않는다. 정책 소스 변경으로 r46 근거가 현재 검사에서 만료되는 것은 정상이며 과거 해시를 갱신해 재승인하지 않는다. 실제 배차·DB·외부 알림·새 UI·APK·Unity·Azure·학습·commit/push는 범위 밖이다.

## 배차 사례 문답 검토 (2026-10-11, 수요 우선·장기 대기 기회 방향 확인·세부 기준 미정)

- [문답 검토 절차](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/배차-사례-문답검토절차.md)를 정리하고 기존 실제 모델 실행의 5개 합성 시점을 첫 검토 묶음으로 재사용했다. 상황·현재 정책 결과·AI 원문·코덱스 수정 초안·사용자 질문을 분리하고 입력/응답 해시를 연결했다. 비공개 자료는 `artifacts/local/local-dispatch-ai/dialogue-review-20261011/round-01/`이다.
- 사용자 검토 대상은 코덱스의 수정 초안이며 AI 원응답 승인과 구별한다. 사람 승인·학습 후보 생성·새 모델 호출·운영 효과는 없으며 제품 코드·정책·DB를 변경하지 않았다. 현재 수요 참여 검토를 기사별 배차·묶음 경로의 AI 선택 완료로 확대하지 않는다.
- 5개 사례의 입력·응답/산출물 해시, 사용자 검토 대기·학습 후보 제외, 미확인 목표 인원의 null 보존과 문서 링크를 확인했다. 문서 범위 Fast `artifacts/local/validation/20261011-133952`·Task `20261011-133956`의 차이 검사가 통과했으며 제품 build/test는 문서 전용으로 생략했다.
- 후속 사용자 의견은 피크 진입 시 신규 참여 가점, 피크 종료 시 기존 활동 기사 할당 우선, 공통 장기 대기 고려다. 앞선 일률 대기 초안의 완전 승인으로 기록하지 않고 첫 피드백을 묶음 해시에 연결해 별도 보존했다. 시간표·수요 보정·가점·예외 기준은 미정이며 새 엔진/모델 실행·정책 변경·AI 원응답 승인·학습 후보 생성은 하지 않았다.
- 두 번째 답변에서 시간표보다 실제 주문량·기사 가용 여력을 우선하는 방향을 확인했다. 사용자 경험의 약 4건/h를 계획 가정으로 기록하고 여력 초과 시 비피크 진입에도 추가 참여를 권고한다. 세부 수치·관측 창·안전 여유는 미정이며 contract 기본 3.5건/h·기존 실행은 보존했다. 이전 피드백 해시에 연결한 별도 의견 2를 추가했으며 새 추론·실제 배차·가중치 학습·제품 코드 변경은 없다.
- 대화 가정에서 기존 기사로 주문을 처리할 수 있어도 수행 조건이 비슷하면 오래 기다린 기사에게 다음 건을 우선 제안하는 방향에 사용자가 동의했다. 가정의 30분은 임계값 확정이 아니며 기존 업무·적격/픽업 관문을 유지한다. 이전 피드백과 연결한 의견 3을 별도 보존했고 이 가정의 엔진/모델 실행·AI 원응답 승인·학습 후보 생성·운영 효과는 없다.

## 프로젝트 근거를 참고하는 배차·운영 AI r46 (2026-10-11, 로컬 검증 완료·품질 한계·운영 OFF)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/project-advisory.implementation.r46.md)에 따라 기존 합성 시나리오의 관측 기준·3.5건/h·12개 상황에서 실제 30개 시점과 규칙·출처·기존 대안을 내보내고 Docker 로컬 모델의 설명을 받았다. 제품 서버 API·DB·배차·알림·기본 OFF를 유지하며 모델 가중치 학습은 하지 않았다.
- 최종 Python 44개·C# 도구 내부 174개 검사가 통과하고 기존 288개 비교 해시를 유지했다. 실제 30개 응답 중 구조 검사 18개 통과·12개 규칙 결과 복귀이며 설명 정확도는 측정하지 않았다. 모든 응답은 사람 검토 대기, 승인 0건이다. CPU 응답 중앙값 37.227초·p95 43.672초로 실시간 배차와 분리된 사후 검토만 다룬다.
- [변경 기록](../Changes/2026-10-11-local-dispatch-project-advisory-r46.md)에 실행·반례·해시와 수치 대조 보고서를 연결했다. AI 초안과 C# 계산 수치표를 분리하고 승인된 이력만 향후 학습 후보로 만들 수 있도록 했지만 실제 승인·학습은 하지 않았다. 기존 Docker 실제 실행과 r44/r45 당시 경계·관련 없는 변경을 보존했다.

## Docker Ollama 실제 실행·합성 응답 확인 (2026-10-11, CPU 로컬 검증)

- 사용자의 후속 한 번 실행 요청에서 Docker 시작은 실행 도구가 허용했다. [실행 상태](../../eng/local-dispatch-ai/docker/README.md)에 따라 공식 `ollama/ollama:0.40.2`의 digest를 고정하고 기존 `qwen3:1.7b`를 별도 Docker volume에 복사했다. 컨테이너 안 manifest와 blob 5개의 해시가 원본과 같다. Windows 캐시·기존 다른 컨테이너는 변경하지 않는다.
- 첫 읽기 전용 캐시 초기화 실패를 별도 모델 volume으로, 내부망 호스트 접속 실패를 루프백 포트가 게시된 bridge로 수정했다. 실제 API 버전·모델과 CPU 장치, balanced 합성 한 건의 `validated_advisory_only`를 확인했다(약 12.7초). 참고 기사 수 10명은 제공된 35건/3.5건/h 계산이며 수요 예측이나 배차 최적화 결과가 아니다. 기존 모의 방어 시험 14개 통과와 실제 추론을 구분한다.
- `127.0.0.1:12681`·클라우드 OFF·자동 재시작 OFF·CPU/RAM 제한을 유지하고 이번 컨테이너는 실행 상태로 남긴다. 운영 API/상태 변경 0건, GPU·MCP 등록·제품 배차 통합·Azure·APK·commit/push는 미실행이다. 실행 근거는 Git 제외 `artifacts/local/local-dispatch-ai/docker-runtime-20261011/`이며 앞선 준비 기록과 r44/r45 및 관련 없는 변경은 보존한다. 이전 Windows 거절 원인이나 전체 권한 해제를 확인한 것은 아니다.

## AI 가정형 오프라인 운영 시나리오 r45 (2026-10-11, 차단 유지·로컬 비교 완료)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/offline-scenarios.implementation.r45.md)와 [작업 명세](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/offline-scenarios.r45.work-order.json)에 따라 합성 주문·기사 시간표와 실제 수요 참여 Policy를 연결하는 개발 도구를 준비했다. r44 제품 코드·기본 OFF·실제 수락 관문은 변경하지 않는다.
- 12개 상황 × 8개 비교 × 계획 처리량 3·3.5·4건/h의 288개 비교와 내부 점검 165개를 통과했다. 빌드 경고/오류 0, 기존 서버 업무 회복 시험 61개 통과. 213개 비교는 배정 대기·완료소요·미완료 기준의 검토 대상이며 검사 통과와 시간 만족을 구분한다. 실제 경로·조리·사고 상태 기계를 복제하지 않았다.
- 입력·결과는 근거 해시별로 보존한다. 정확한 모의 예측의 배정 대기 p95 개선은 0/36개이며 기사 부족·긴 순차 수행이 남은 위험이다. 다음 우선순위는 약속 완료시간과 잔여 여력에 따른 제안·접수 용량·대안 안내의 검토다.
- r45 작업 당시에는 모델 실행 차단을 유지하고 Ollama 재시작·대체 추론·권한 완화·네트워크/업무 API·운영 DB 쓰기·APK·단말·Azure·배포·commit/push를 수행하지 않았다. 최종 수치와 제한은 [변경 기록](../Changes/2026-10-11-offline-dispatch-scenarios-r45.md)에 남긴다. 후속 사용자가 요청한 Docker 실행 결과는 상단 최신 절과 별도 실행 근거로 구분한다.

## 음식 배차 수요·참여 순서 r44 (2026-10-11, 기본 OFF 구현·로컬 검증 완료)

- [승인 범위·정책](Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/demand-participation.implementation.r44.md)에 따라 음식 후보 선정에 수요·잔여 업무·기존 활동 보호·신규 대기·장기 대기/지연 예외를 연결했다. 직접 지정도 같은 선정을 확인한다. 기본 OFF, 기사 의사·유효 제안·수락 업무·동시 수행 관문·요금·지급·화물 흐름은 유지한다.
- [변경·검증 기록](../Changes/2026-10-11-food-demand-participation-r44.md). 소프트 참여 순서이며 권역 원자적 정원/영속 대기열은 아니다. r44 당시 로컬 Ollama·모델은 준비했지만 실행 도구의 시작 거절로 실추론은 미검증이었다. 후속 Docker 합성 응답 검증은 상단 최신 절로 구분하며 운영 활성화·단말·Azure·commit/push는 r44 범위 밖이다.
- Fast 89건·최종 전체 Task 10,386건 통과, 실패 0·환경 의존 Mongo 15건 건너뜀. AI 방어 시험 14건 통과. 최종 전체 실행 전후 소스 16개 해시 변화 0이며 결과와 초기 실패 수정은 변경 기록에 남겼다.

## 예외 복구 코드 책임 분리 r2 (2026-10-10, 로컬 검증 완료)

- [사용자 승인 범위](Planning/공통/PLAN-OPERATIONS-EXCEPTION-CONTINUITY/exception-continuity.refactoring.r2.md)와 [작업 지시](Planning/공통/PLAN-OPERATIONS-EXCEPTION-CONTINUITY/exception-continuity.r2.work-order.json)에 따라 음식 역할별 요청 준비·완료 근거와 지원 사건/배송 복구 상태·표시를 분리했다. 기존 동작·권한·계정/세대·원장 완료 근거·저장 정책을 유지한다.
- `Implemented / LocalValidated / DeviceAndOperationalDeferred`. Fast 116건·전체 소비 앱 build·Task 10,328건 통과, 실패 0·Mongo 환경 의존 15건 건너뜀. 390×844 실제 제품 화면 37항목·브라우저 오류 0·업무/외부 HTTP 시도 0건이며 검증 전후 소스·시험·호스트 18개 지문이 일치한다. [변경·실행 근거·한계](../Changes/2026-10-10-exception-continuity-refactoring-r2.md). 초기 CSS 미적용 기준선은 보존하고 원본 DOM/CSS의 정적 참조 비교로 구분한다. 화주 부분 완료 후속·실기기·Azure·운영 효과·commit/push는 범위 밖이며 다른 작업 변경을 보존했다.

## 살뜰마트 기사 수락·픽업·전달 소프트웨어 연결 r58 (2026-10-10, 로컬 검증 완료)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-driver-work.implementation.r58.md)·[개발 반환](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-driver-work.r58.work-order.json)에 따라 실제 마트 원천 전용 기사 수락→전표 입력 즉시 픽업→전달과 FDriver 독립 화면을 연결했다. 보호 전표·요청 영수증·SQL Event/후속 Outbox를 저장하고 지연 원장 소비·최초 수락 경합에서 과거 SQL 상태로 되돌리지 않는다. 음식 주문 위장과 기존 화물 명령의 우회를 차단한다.
- `Implemented / LocalValidated / SimulationConsumerBound / OperationalDeferred`. 최종 Fast 262건·전체 역할 앱 build·Task 10,299건 통과, 실패 0·Mongo 환경 의존 15건 건너뜀. 승인 hash와 최종 검증 전후 소스·시험 44개 지문이 일치한다. [변경·실행 근거·한계](../Changes/2026-10-10-ssalddel-mart-driver-work-r58.md). Simulation·기본 OFF·기존 세 기능 관문을 유지하며 Migration은 준비만 했다.
- SQLite Controller/Service와 시험용 커뮤니티 저장소 증거이며 실제 MySQL/Mongo/Redis·인증 운영 HTTP·네이티브 화면 렌더·단말·카메라·프린터·결제/지급·외부 알림·commit/push·배포는 미실행이다. 다음은 창고 담당자의 실제 전표 발급 화면과 격리 인증 HTTP·DB 종단 검증이다. 다른 스레드의 기존 변경과 아래 이전 기준선을 보존한다.

## 역할 앱 예외 후 업무 지속 보완 (2026-10-10, 로컬 검증 완료)

- [승인·개발 반환](Planning/공통/PLAN-OPERATIONS-EXCEPTION-CONTINUITY/exception-continuity.implementation.r1.md)·[작업 지시](Planning/공통/PLAN-OPERATIONS-EXCEPTION-CONTINUITY/exception-continuity.r1.work-order.json)에 따라 C01~C09의 접수 보호 저장/결과 우선 확인, 분산 사진 재개, 화물 완료 확인, 지원 사건→연결 배송 복구, 음식 요청 결과 확인·문제 접수, 지도 재개와 늦은 조회 응답 보호를 보완했다. 기존 원장 권위·업무별 책임·다른 dirty 변경을 유지했다.
- `Implemented / LocalValidated / DeviceAndOperationalDeferred`. 최종 Fast 543개·전체 역할 앱 build·Task 10,180개 통과, 실패 0·Mongo 환경 시험 15개 미실행. 390×844 제품 컴포넌트 실제 렌더 10항목·현행 OS 선언 63개도 통과했다. [변경·실제 렌더·한계](../Changes/2026-10-10-exception-continuity-hardening.md)에 원시 근거를 연결한다. Task 근거는 `artifacts/local/validation/20261010-181751/`이며 이전 조사 927개를 이번 결과로 재사용하지 않는다.
- 화주 Mongo→SQL/운임·인계 부분 완료 복구, C04 사진 업로드 자체 및 재시작 원본, 웹/C06/운영자 화면 수명 경계와 실제 보호 저장·단말·운영 HTTP/DB·Outbox·배차·알림·입금은 후속 검증이다. Azure·실기기 설치·영상·MYBOX/SNS·Unity·commit/push·배포는 실행하지 않았다.

## 살뜰마트 배차 소프트웨어 연결 기반 r57 (2026-10-10, 이전 기준선)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-dispatch-connection.implementation.r57.md)·[개발 반환](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-dispatch-connection.r57.work-order.json)에 따라 실제 마트 원천 읽기 Adapter, 포장 완료 후속의 명시적 재확인 Event, 공통 큐/확정 기사 조기 반환의 원천 충돌 보호를 구현했다. `Implemented / LocalValidated / SoftwareConnectionFoundation / OperationalDeferred`. 아래 기존 r56 표본과 다른 작업의 증거를 보존한다.
- Fast 149건·전체 역할 앱 `Ssalddel.v3.5.slnx` build·Task 10,180건 통과, 실패 0·환경 의존 시험 15건 미실행. 실제 Service 연결은 격리 SQLite·시험 전용 준비 소비자·Mongo/Redis 기록용 경계로 검증했다. 승인 기획 hash와 검증 전후 소스/시험 14개 지문이 일치한다. [변경·실행 근거](../Changes/2026-10-10-ssalddel-mart-dispatch-connection-r57.md)에 최초 실패와 동시 작업의 시험 수정 보존도 구분한다.
- 기사 전용 소비자는 미구현이므로 제품 `MartLastMileConsumerBindingUnavailable` 보류를 유지한다. 실제 기사 콜·수락/픽업/전달·인증 운영 HTTP·MySQL/Mongo/Redis 종단·단말·프린터·자동 durable 복구·commit/push/배포는 이번에 수행하지 않았다. 기존 지도 SDK·푸시 설정과 패키지 경고는 남아 있다.

## 역할 앱·업무 OS 예외 후 업무 지속 검증 (2026-10-10)

- [조사·우선순위](../Changes/2026-10-10-cross-app-exception-continuity-audit.md): 8개 역할 진입점의 주요 복구 경로와 10개 OS·62개 책임 단계의 현행 계약을 대조했다. 화주/생활 배송 신규 접수의 미확정 번호 보존, 화물 완료 후 재조회, 운영자 사건→원 배송 복구, 분산 사진의 화면 종료 후 재개, 공통 음식 요청 결과 조회와 지도/수입 준비의 늦은 응답 처리를 보완 대상으로 남겼다. 제품 수정 요청으로 확대하지 않았다.
- 현재 소스 빌드 후 관련 시험 529+281+117=927개 서로 다른 시험명 모두 통과, 실패·건너뜀 0. 현행 카탈로그 검사 63건 통과. 소스 7,864개 해시가 빌드 전/후·보충 시험 후 같았다. 시험 통과가 이번에 발견한 미포함 실패 경로의 정상 동작을 뜻하지 않는다.
- `SourceAuditGapsFound / RelatedLocalTestsPassed / ProductRuntimeNotVerified`. 근거는 Git 제외 `artifacts/local/exception-continuity-audit-20261010/`. 실제 단말·Azure·외부 DB/기관·푸시·입금·운영 완주, APK 설치, 제품 코드 변경·commit·push·배포는 하지 않았다. 관련 없는 기존 변경을 보존했다.

## 살뜰마트 전표 출력·스캔 즉시 픽업 r56 (2026-10-10)

- [승인·개발 반환 r56](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-package-pickup.implementation.r56.md)·[작업 지시](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-package-pickup.r56.work-order.json)에 따라 기존 격리 표본에 포장 전 80mm 전표 출력·포장 담당자/기사 관점·QR 스캔 즉시 표본 픽업·네 자리 대체를 구현했다. 전표·요청·인수는 SQLite에 저장하며 준비·현재 판본·합성 기사·중복 관문을 유지한다. r54 조사·r55 선택과 r52/r53 이전 증거는 보존한다.
- `Implemented / LocalValidated / AndroidDeferred / OperationalDeferred`. preview build·집중 C# 69건·JS 62건·3폭 UI 21건·실제 QR 픽셀 디코딩 12건과 Fast를 통과했다. 관련 역할 앱 전체 `Ssalddel.v3.5.slnx` build와 Task 실행 9,993건 통과, 실패 0건·Mongo 환경 시험 15건 미실행이다. 늦은 인쇄 callback 결손을 수정했으며 UI 종료 후 242초의 서버 오류 0·stderr 0바이트와 소스/호스트 지문 변화 0건을 확인했다. [실제 렌더·샘플 이미지](../Changes/2026-10-10-ssalddel-mart-package-pickup-r56.md)는 합성 자료이며 확정 UI가 아니다.
- 실제 인증 HTTP·운영 MySQL/Mongo/Outbox·마트 기사 앱·Android 촬영/설치·물리 인쇄·호스트 재시작 복구는 미검증이다. 카메라·인쇄 창은 모의이며 기존 `NU1903` 의존성 경고는 남는다. 원본 입력·운영 플래그·마트 기사 차단 정책과 관련 없는 변경을 보존했고 commit·push·배포하지 않았다.

## 살뜰마트 선반 카메라 입력 r53 (2026-10-10, 로컬 검증 완료)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-camera-scan.implementation.r53.md)·[작업 지시](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-camera-scan.r53.work-order.json)에 따라 기존 표본의 선반 코드 입력만 QR·Code128 카메라로 보완한다. 합성 연습 표찰과 직접 입력을 함께 제공하고 상품명·수량 확인·피킹 완료 관문은 보존한다.
- 별도 preview build와 관련 C# 39건·JS 수명 30건·가상 카메라/모의 디코더 UI 27건·수동 UI 3건·실제 생성 표찰 디코딩 12건이 통과했다. Fast/Task routing 검사는 `eng/`·문서만 대상으로 build/test 자동 실행을 생략하며 명시적 실행 증거와 구분한다. 창 종료 예외·늦은 권한 정리·Code128 축소 판독을 보완했다. [변경·실제 렌더 샘플](../Changes/2026-10-10-ssalddel-mart-camera-scan-r53.md), 확정 UI 아님.
- loopback 표본의 브라우저 입력이며 실제 Android 촬영·MAUI/운영 DB/API/Outbox 결속은 미검증이다. 기존 SQLite 의존성 `NU1903` 경고는 남는다. 마지막 브라우저 활동 이후 221초의 지연 회로 종료 로그도 서버 오류 0·stderr 0바이트로 확인했다. 기존 r52·원본 입력·관련 없는 변경을 보존했고 commit·push·배포는 하지 않았다.

## 살뜰마트 피킹·포장 표본 r52 (2026-10-10, 로컬 검증 완료)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-picking-sample.implementation.r52.md)·[작업 지시](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/ssalddel-mart-picking-sample.r52.work-order.json)에 따라 기록 3건의 상품명·수량은 보존하고 업무 표현은 **살뜰마트**로 정했다. 원본 사업자 명칭·원문 주문 식별자·가격은 입력의 비공개 계보로만 둔다.
- 전용 loopback·Development·Simulation·격리 SQLite에서 기존 피킹·포장 UseCase와 전체 주문 선행 관문을 구현·검증했다. 취소 작업·분할 포장·중복 Event·일반 출고 호환을 보존한다. 선반·SKU·재고·기사는 합성이며 모의 인계를 실제 배차·배송 완료로 보고하지 않는다. 기존 작업·원본 DB·운영 플래그를 보존했다.
- Fast 101건·Task 9,963건 통과, 실패 0건, Mongo 환경 시험 15건 미실행. 관련 역할 앱/미리보기 build와 5모드 × 3폭 UI 15건·캡처 51장, 소스 15개·입력·빌드 hash 불변을 확인했다. [변경·검증 기록](../Changes/2026-10-10-ssalddel-mart-picking-sample-r52.md)에 합성 상품 **샘플 이미지, 확정 UI 아님**을 보존한다. 실제 마트 기사·MAUI 앱 결속·MySQL/Mongo·실물 스캔·운영 Outbox 종단은 미검증이며 기존 SQLite 의존성 보안 경고는 후속이다. commit·push·배포는 하지 않았다.

## 전국 생활 공간 입고 재고 관리 코드 기반 r51 (2026-10-10)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/nationwide-storage-foundation.implementation.r51.md)·[작업 지시](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/nationwide-storage-foundation.r51.work-order.json)에 따라 기존 공간·동의·선반·입고를 재사용하고, 한 공간의 인증·페이지 재고 조회와 참고 자료의 지역 밖 장애 격리를 구현했다. 입고 예정·실수령·가용·예약·기존 불량 수량을 분리하며 관계없는 사용자와 원천 계보 불일치를 차단한다. [변경·검증 기록](../Changes/2026-10-10-nationwide-storage-foundation-r51.md).
- `Implemented / LocalValidated / OperationalDeferred`. Fast 102건·관련 앱 `Ssalddel.v3.5.slnx` build·Task 9,926건이 통과했고 실패 0건, 환경 의존 MongoDB 15건은 미실행이다. 기획 hash 일치·독립 범위/코드 검토·검증 전후 소스/시험 8개 지문 변화 0건과 문서 Fast·로컬 링크 88개·공백 검사를 확인했다. 기능 기본 OFF·Simulation을 유지하며 전국 실운영·실제 MySQL/Mongo·단말·공용 DB 변경·문의 발송·commit/push·배포는 하지 않았다. 전체 workspace 페이지화·대량 성능·실제 불량 분리 입고는 후속이며 r48~r50과 이전 작업은 보존한다.

## 5개 지역 공통 사전 문의 양식 r50 (2026-10-10)

- 사용자 선택에 따라 [공통 문의 원본](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/administrative-common-inquiry.template.r50.md)과 [범위·지역별 준비 대장](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/administrative-common-inquiry.design.r50.md)을 만들었다. 특정 단지·공간이 없는 일반 질의로 구분하며 중랑구·동대문구·광진구·노원구·구리시의 공식 수신처·회신은 별도로 관리한다. [r49 조사·특정 공간 양식](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/administrative-document-drafts.design.r49.md)과 이전 구현·검증은 보존한다.
- `CommonTemplateScopeAccepted / TemplatePrepared / ProductImplementationDeferred / SubmissionDeferred`이며 문서 준비만 승인된 범위다. 문서 범위 Fast·`git diff --check`, 신규 문서 로컬 링크 5개·공백 검사와 독립 읽기 검토를 통과했다(build/업무 시험은 생략). 제품 코드·DB·앱·Unity·이메일·민원 접수·허가/운영 설정은 변경하지 않았다. 실제 관공서 연락·발송·접수·단말·commit/push·배포는 수행하지 않았다.

## 아파트 생활권 물류 거점 적용 검토 r48 (2026-10-10)

- [후속 검토안](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/apartment-shared-space.review.r48.md)에 r30/r31/r32의 보관·입고·피킹·인계·정산 코드와 아파트 사용 권한·담당 위임·공유 용량·출입 시간·검수/격리·철회 후 반출 결손을 연결했다. 기획 README·목차만 함께 갱신하며 기존 승인·구현은 보존한다.
- 상세 정책은 `ProposedDesign / ImplementationApprovalPending`이다. 현재 조문과 소스를 읽어 대조했으며 제품 코드·DB·앱·Unity·운영 설정은 변경하지 않았다. 문서 범위 Fast·`git diff --check`와 새 검토안의 로컬 링크 14개 확인을 통과했다(build/업무 시험은 생략). 실제 현장·단말·실입고·지급과 commit/push·배포는 수행하지 않았다.

## 주문자 메뉴 사진·기록 자료 연결 (2026-10-10, 로컬 검증 완료)

- [승인 범위](Planning/공통/PLAN-OPERATIONS-ORDERER-MENU-CATALOG/orderer-menu-catalog.r1.md)에 따라 기존 주문자 공개 메뉴의 대표 이미지 표시와 비공개 기록 메뉴 화면을 구현했다. 574주문·391지점·511메뉴 관측을 보존하고 사진 117파일·146연결·123메뉴를 검토한다. 명시 단가 없는 항목금액·주문합계는 현재 판매가로 등록하지 않는다.
- Fast 219개·공통 UI 74개·320/390/1280px 정상/미설정/손상/운영금지 12장면이 통과했다. 전체 앱 빌드와 Task 실행 9,886개가 통과했고 Mongo 환경 15개는 미실행이다. 원본·운영 DB·기존 주문 계약을 보존하며 구현·실행 증거는 [변경 기록](../Changes/2026-10-10-orderer-menu-catalog-r55.md)에 기록한다.
- 후속 사용자 요청은 기존 Azure Blob 구성 재사용이며 최종 범위는 **로컬 준비까지만**이다. 기존 어댑터·`community-public`/`platform-private` 배포 구성을 확인하고 원본 120파일을 바꾸지 않는 비공개 Blob 명세를 준비했다. 준비 시험 31개 통과·symlink 권한 시험 1개 미실행이며 정확한 계정 주소·실제 비공개 ACL·업로드는 미확인이다. 메뉴 상세 갤러리·운영 등록·단말 설치·공개·영상·commit/push는 하지 않았다.

## 실제 배달 기록 기반 로컬 검증 r54 (2026-10-10)

- [승인 r54](Planning/공통/PLAN-OPERATIONS-DRIVER-VERTICAL-REFACTOR/delivery-history-local-replay.r54.md)에 따라 기존 574건·391지점·510픽업을 기사 내역·음식점 관계 검증에 연결했다. 개인 자료는 Git 제외 로컬 파일이며 고객정보·원문 화면을 옮기지 않는다. 모의 상태/시각과 실제 관측값을 구별한다.
- Fast 194개·Task 실행 9,824개 통과(Mongo 환경 15개 미실행). 실제 SQLite 메모리 DB와 기사 조회 UseCase에서 17일별 대조, 320/390px 각각 574건·36페이지 및 같은 기록의 음식점 연결/복귀·메뉴·미확인값을 확인했다. 전체 솔루션 Android 컴파일과 별도 단말 배포/설치는 구별하며 이번 USB·운영 DB·외부 공유·commit/push는 없다. [변경·검증 기록](../Changes/2026-10-10-delivery-history-local-replay-r54.md).

## 기사 배달료 상세·현재 수행 안내와 USB 설치 r53 (2026-10-09)

- [승인 r53](Planning/공통/PLAN-OPERATIONS-DRIVER-VERTICAL-REFACTOR/driver-detail-work-copy.r53.md)에 따라 후속 3·4번인 배달료 상세 두 열 정렬·합계/공제/정산/지급 구분, 현재 수행 단계 제목·버튼 문구·주소 분리·중복 안내 제거를 구현했다. 전체 지도·독립 카드·기존 명령·계산·개인정보 수명을 보존한다.
- 최종 Fast 341개·Task 실행 9,813개 통과(Mongo 환경 15개 미실행), 시작 스타일 중복 오류를 수정한 ARM64 0.1.53/53을 2026-10-10 00:32 KST USB 업데이트 설치했다. 단말/로컬 APK 해시 일치·약 60초 정상 유지·내 정보/배달 내역/복귀 권역/경험 공유의 조회 화면 이동·복귀를 확인했다. 기기 접근성 텍스트/버튼 근거이며 시각 배치·지도·서버 데이터는 미검증이다. Docker 오류로 서버 업무 검증은 보류한다. [변경·검증 기록](../Changes/2026-10-09-driver-detail-work-copy-r53.md). USB는 후속 요청에서 해제됐으며 이후 로컬 검증만 진행한다. 실제 업무·입금·Azure·Unity·영상·commit/push는 하지 않았다.

## 기사 내 정보 메뉴·배달 내역 r52 (2026-10-09)

- [승인 r52](Planning/공통/PLAN-OPERATIONS-DRIVER-VERTICAL-REFACTOR/driver-profile-history.r52.md)에 따라 내 정보의 배달내역 우선 메뉴·독립 설정 화면·아이콘 행 버튼과 완료 목록의 행 선택·상세 복귀 위치를 구현했다. 복귀 권역/경험 공유의 기존 초안·계정/판본 보호, 지도·독립 카드·전경 자동업무·GPS/배차 수신을 유지한다.
- 관련 Fast 162개·Task 9,786개 통과, Mongo 환경 시험 15개 미실행. 최종 Android 컴파일·v2/v3 서명된 ARM64 APK를 확인했다. [변경·검증 기록](../Changes/2026-10-09-driver-profile-history-r52.md)과 [작업 범위](Planning/공통/PLAN-OPERATIONS-DRIVER-VERTICAL-REFACTOR/driver-profile-history.r52.work-order.json)에 결과를 갱신한다. 실제 단말 설치·운영·서버/DB·Unity·영상·commit/push는 이번 범위에 포함하지 않는다.

## 음식 기사 책임 메타데이터·탐색 지도 r51 (2026-10-09)

- [승인 r51](Planning/공통/PLAN-OPERATIONS-DRIVER-VERTICAL-REFACTOR/driver-code-metadata.r51.md)의 `food-driver-work` 핵심 45단계와 명시 소스 결속 41개를 구현했다. 계약·SQL/후속·현재 조회·정산·서버 조율·순수 표시·공통 셸 포트와 네이티브 암호화 복구를 구분한다. [탐색 안내](../Architecture/FoodDriverCodeNavigation.md)·[생성 지도](generated/food-driver-code-map.md).
- 전용 manifest와 `--work-area food-driver` 생성/검사, 필수 단계·소스·심볼·XAML/코드부·reparse 보호, scoped 검증 routing을 연결했다. Fast 264/264·Task 12,751개(서버·공통 UI 9,780 / Simulation 2,001 / Unity 970) 통과, 별도 Mongo 환경 시험 15개 미실행. 기존 기본 지도·E 책임 검사·독립 CLI 실패 분기·서명된 ARM64 기사 APK를 확인했다. 업무 본문 35개와 검증 소스 46개 보존 근거는 [변경 기록](../Changes/2026-10-09-food-driver-code-metadata-r51.md)에 남긴다.
- 기능 동작·공개 계약·DB 스키마·디자인은 변경하지 않는다. 실제 단말·GPS·지도·원격 알림·공용 DB·Azure·운영·지급·Unity·영상·commit/push는 제외하며 기존 dirty 변경을 보존한다.

## 음식 기사 DB–API–화면 리팩토링 r50 (2026-10-09)

- [승인 r50](Planning/공통/PLAN-OPERATIONS-DRIVER-VERTICAL-REFACTOR/driver-vertical-refactor.r50.md)에 따라 시작 요청 ID·동시 시작 보호·SQL 시작 후속 복구, 수락/도착/픽업/전달의 영속 후속 예약, 정산 없는 현재 조회와 공통 복구/표시 정책을 구현했다. r49의 운행 시작 한 번·GPS 자동 확인·명시적 OFF·전체 지도·독립 카드와 기사 본인의 수락을 유지한다.
- 최종 Fast 1,061/1,061·Task 실행 9,701/9,701·공통 모바일 18/18장면 통과, 별도 Mongo 환경 시험 15개 미실행. 소스 3,058개와 화면 소스/실행 DLL 15개가 검증 전후 같다. 추가형 SQLite migration 시험·MySQL SQL 생성·EF 모델 일치·서명된 ARM64 기사 APK를 확인했다. [변경·화면·검증 기록](../Changes/2026-10-09-driver-vertical-refactor-r50.md).
- 거절의 기존 두 단계 저장과 종료 인덱스 정리 실패의 로그 처리 경계는 유지했다. 실제 단말·지도/GPS·원격 알림·공용 DB 적용·Azure·실운영·실지급·commit/push는 미실행이며 기존 dirty 변경을 보존한다.

## 기사 운행 시작·자동 갱신 r49 (2026-10-09)

- [페이지 책임 r13](../ProjectOverview/page-docs/driver-workspace-sections-r1.md#운행-시작과-자동-갱신-r13--사용자-후속-기준)에 따라 운행 시작 한 번으로 기존 신규 배차 수신 ON까지 연결했다. 정상 수행의 수동·당겨서 새로고침을 제거하고 GPS·전경 주기·알림으로 확인한다. 전체 지도·독립 카드·지도 토글·내 정보 종료와 기사 본인의 배차 수락을 유지한다.
- 응답 유실은 정본을 자동 조회하고 미확정 수신 요청의 같은 ID로 복구한다. 시작 POST를 자동 재전송하지 않으며 명시적 OFF를 덮어쓰지 않는다. 미확인 운행 상태·계정 변경·늦은 응답을 보호하고 일반 위치 업로드 실패와 업무 조회를 분리했다.
- 최종 Fast 680/680·Task 실행 9,620/9,620·공통 모바일 18/18장면 통과, 환경 의존 Mongo 15개 미실행. 소스 22개와 화면 소스/실행 DLL 13개의 해시가 검증 전후 같다. 기존 로컬 네이버 SDK 설정을 포함한 ARM64 기사 APK를 생성했다. [변경·화면·검증 기록](../Changes/2026-10-09-driver-automatic-work-r49.md). 실제 단말·지도/GPS·원격 배차/알림·Azure·실운영·commit/push는 미실행이며 기존 dirty 변경을 보존한다.

## 기사 지도 배차 토글·내 정보 운행 종료 r48 (2026-10-09)

- [페이지 책임 r12](../ProjectOverview/page-docs/driver-workspace-sections-r1.md#지도-배차-수신-토글내-정보-운행-종료-r12)에 따라 신규 배차 받기를 지도 왼쪽 위 토글로 옮기고 운행 종료는 내 정보 맨 아래에 모았다. 전체 지도·독립 카드·상태별 업무와 진행 배달 중 종료 차단을 유지한다. 정상 수동 위치 갱신 대신 전경 자동 갱신을 사용하고 오류 복구·현위치 이동은 보존한다.
- 응답 유실 시 토글은 확인 필요/비활성으로 표시하고 정본 조회 뒤 복구한다. 늦은 위치 결과·계정/운행 변경을 보호하고 HTTP 위치 보고가 DB 운행 상태를 다시 시작하지 않게 했다. 요금·지급·DB 스키마는 변경하지 않았다.
- 최종 Fast 577/577·Task 실행 9,564/9,564·공통 모바일 14/14장면 통과, 환경 의존 Mongo 15개 미실행. 소유 소스 29개와 UI 소스/실행 DLL 11개가 검증 전후 같고 최신 기사 APK를 생성했다. [변경·화면·검증 기록](../Changes/2026-10-09-driver-map-controls-r48.md). 실제 단말·지도/GPS·백그라운드·Azure·배포·commit/push는 미실행이며 기존 r47 등 dirty 변경을 보존한다.

## 음식 배달 경험 공유 r47 (2026-10-09)

- [승인 r47](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/delivery-experience-sharing.implementation.r47.md)에 따라 기사·음식점·주문자의 현재 업무에 확인된 앱 완료 건수를 보조 문구로 연결한다. 같은 장소의 동시 주문도 건수로 세며 방문 횟수나 신뢰 점수로 표현하지 않는다. 기능과 타인 공유는 기본 OFF다.
- 공통·전문 기사 화면, 음식점 진행 영역, 주문자 전달 안내 및 선택 주소 검색의 식별자 전달을 구현했다. 미확인·조회 실패·종료·재배정·공유 철회 뒤의 타인 요약은 숨긴다. 음식점은 도착 여부를 추정하지 않는 중립 안내를 사용한다. [페이지·코드·DB 책임](../ProjectOverview/page-docs/delivery-experience-sharing-r47.md).
- 구현·로컬 검증 완료 / 운영 미활성. 최종 Fast 88/88·Task 실행 9,526/9,526·공통 Razor 모바일 38/38장면 통과, 환경 의존 Mongo 15개 미실행이다. Task 전후 소스 49개와 UI 소스/실행 DLL 12개의 지문 일치를 [변경 기록](../Changes/2026-10-09-delivery-experience-sharing-r47.md)에 결속했다. EF idempotent migration SQL 생성·세 앱 APK hash 수집을 확인했으며 실제 DB 적용·인증 HTTP·APK 패키지/서명·휴대폰·Azure·원격 알림·실운영·지급·commit/push는 미실행이다. 이전 r45/r46·주문 원장 r7 기록을 보존한다.

## 주문 원장 구조 보완·총괄 관리자 명세 r7 (2026-10-09)

- [승인 r7](Planning/공통/PLAN-OPERATIONS-ORDER-CENTERED-WORK-NETWORK/order-ledger-admin-refactor.implementation.r7.md)의 음식·물품 업무 인계를 대조하고 음식 루트 배송 연결·legacy 읽기 호환·단계 판독/목록 페이지·연결/분리 응답 공개 보호·기사 최신 조회 실패 차단과 정본 복구를 구현했다. 별도 상태 원장 없이 각 전문 상태 권위와 전달/수령/정산/지급 구별을 유지한다.
- 총괄 관리자 주문 진행·기사별 업무·문제 처리·정산/지급 확인 명세를 완성했다. 최종 Fast 886개·Task 9,462개 통과(각각 환경 의존 Mongo 12/15개 미실행), 음식 기사 Android Debug build 성공·APK 생성, 소유 C# 21개 검증 전후 동일을 [변경 기록](../Changes/2026-10-09-order-ledger-admin-refactor-r7.md)에 결속했다. 관리자 화면·지사별 권한은 후속 범위다.
- 파기된 루트/배송의 신규 읽기 참조 복원을 차단하고 기존 명시적 관계 이력은 보존했다. 실제 SQL/Mongo HTTP·제품 화면·단말·지도/알림 작동은 미검증이며 지도 SDK ID 미설정과 기존 Android API 수준 등을 포함한 경고 37개가 남는다. 기존 r45/r46과 다른 변경을 보존하며 운영·실지급·배포·commit/push는 수행하지 않았다.

## 회원 생활 배송 한 건 완주 r46 (2026-10-09)

- [승인 r46](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/member-delivery-completion.implementation.r46.md)의 최초 사진 촬영 권한, 협업 회원 배송 접수, 실제 상대 수령 확인을 구현했다. 같은 요청의 부분 저장/동시 완료 결과를 회복하며 전달·수령·지급을 합치지 않는다. 별도 알림 r45와 기존 변경을 보존한다.
- 최종 Fast 190개·Task 실행 9,458개가 통과했고 환경 의존 Mongo 시험 15개는 미실행이다. 회원/협업 집중 198개와 별도 UI 48개도 통과했다(중복 합산하지 않음). 모의 자료의 실제 제품 Razor 32화면·두 폭의 네 단계 클릭, 일반 앱 Debug APK 0.1.46/146의 서명·내장 실행 파일·빌드 지문을 검증했다. 소유 소스 27개는 최종 검증 전후 동일하다. [화면 책임](../ProjectOverview/page-docs/neighborhood-member-completion-r46.md)·[변경·검증 기록](../Changes/2026-10-09-neighborhood-member-completion-r46.md).
- 비소유 동시 변경으로 먼저 실패한 두 빌드 기록은 보존하고 반영 후 재검증했다. Docker 응답/서비스 권한 문제로 전용 MySQL·Mongo 인증 HTTP는 실행하지 못했다. 기능/Operational 기본 OFF; 실제 단말·원격 푸시·정산/지급·Unity·배포·commit/push는 하지 않았다. 잠긴 수령 intent 뒤 동의/기한 검증 실패의 자동 해제 및 기존 Mongo 운송 투영의 회원/협업 필드 추가는 후속이다.

## 공통 운영 알림·월 50만 원 운영 준비 r45 (2026-10-09)

- [승인 구현 r45](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/operator-alerts.implementation.r45.md)에 따라 관리자 알림 목록/상세, MySQL 알림·채널 outbox·확인 요청, 음식/생활 배송/화물/지원 사건 관찰, 재발·재시도·09시 KST 요약을 구현했다. 확인은 실제 업무 해결과 분리하며 외부 문구는 개인정보를 포함하지 않는다.
- Fast 82개·Task 9,413개 통과, 환경 의존 MongoDB 14개 미실행. 320/390 폭 16개 실제 Razor 화면, Firebase 조건부 컴파일, 키 없는 관리자 Debug APK, Bicep/Compose 및 로컬 암호화 백업·변조 차단을 확인했다. [페이지·코드·DB](../ProjectOverview/page-docs/operations-alerts-r45.md)·[실행 기록](../Changes/2026-10-09-operations-alerts-r45.md).
- 감시·외부 발송·예산 제한 기본 OFF. 실제 수신·유료 Azure 배포·공용 DB migration·실기기·off-VM DB 복구·실거래/지급·commit/push는 제외한다. 월 500,000원은 배송비와 분리한 배분안이며 실제 가격/하드 상한이 아니다. 별도 r46 회원 배송 변경은 보존하며 그 구현 결과를 이번 알림 완료로 대신 보고하지 않는다.

## 지역 거래·무료 배차 운영 시나리오 대조 r44 (2026-10-09)

- [운영 시나리오 r44](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/operating-scenarios.review.r44.md)에 직접 수령·직접 전달·회원 배송과 참여자 부재·동시 수락·응답/사진 실패·취소·미수령·미지급·정보 열람을 역할 행동 → 화면 → 서버/원장 → 운영자 개입 → 결과로 정리했다. 사용자 선택은 본인이 운영자와 배송 참여자를 겸하는 작은 지역 거래다.
- 기존 집중 시험 162개 통과·실패/건너뜀 0개. 소스 대조에서 최초 촬영 권한의 순환 조건, 협업/회원 배송 접수 조건 충돌, 전달 완료와 수령/협업 완료의 미연결을 확인했다. 공통 분쟁 경로는 있으나 완료 후 회원 화면과 정산 원천의 연결은 후속이다. 현재 정보 열람은 수락 후 72시간 이내이며 완료/취소 시 즉시 숨긴다.
- 이번 변경은 운영 조사·문서화다. 제품 코드·DB·APK·정책 활성화·휴대폰·Azure·실거래/실지급은 변경하거나 검증하지 않았다. 기능/Operational 기본 OFF와 이전 r43 실행 기록을 유지하며, 개별 시험 통과를 회원 거래 전체 완료로 보고하지 않는다. 문서 4개 지정 Fast·차이 검사와 추가 영역 로컬 링크 26개 검사를 통과했다. Fast는 문서 변경으로 빌드/시험을 생략했으며 집중 162개는 별도 실행 결과다.

## 일반 회원 생활 배송 참여 구현 r43 (2026-10-09)

- [승인 r39](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/member-delivery.implementation.r39.md)에 따라 일반 계정의 선택 참여·비공개 활동 기준점·근처 동시 제안·건별 수행 권한·사진 완료·앱 알림 복구를 연결한다. 기존 의뢰/운송원장·확정 요금·수수료 0원과 음식/화물 독립 상태를 유지한다.
- 구현과 최종 로컬 검증·APK 빌드를 완료했다. Fast 369개·Task 실행 9,331개가 통과했고 환경 의존 기존 시험 14개는 실행되지 않았다. 모의 자료로 제품 화면 30개와 네 단계 클릭 흐름을 확인했다. [r43 변경 기록](../Changes/2026-10-09-neighborhood-member-delivery-r43.md)에 로그·APK·화면과 Firebase/단말/Azure 미검증 범위를 결속한다. 기능/Operational 기본 OFF이며 미성년 배차·혼합 다건 수행·실지급을 활성화하지 않는다.

## 기사 기준 위치·권역 복귀 우대 구현 r42 (2026-10-08)

- [승인 r38](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/driver-return-area.implementation.r38.md)에 따라 기사 비공개 기준점·자동 7/6km·수동 선택·판본 저장/재조회와 실제 음식 큐·화물 추천/조율의 전체 일정 복귀 가점을 구현했다. 새 정책 선택 시 기존 복귀 가산을 중복하지 않고 현재 음식 최대 3건과 차량·시간·수락 조건을 유지한다. 공동 음식/화물 일정은 r36 후속으로 남기며 해당 진행 업무가 있으면 가점을 보류한다.
- 집중 184개·최종 Fast 157개 통과, 최종 Task 9,235개 통과·실패 0개·기존 환경 의존 Mongo 시험 13개 미실행이다. 새 설정의 실제 격리 Mongo 시험과 음식 기사 Android 빌드는 통과했다. 기존 화물 일정은 선택 경로의 구간 적재 근거를 연결하기 전까지 가점을 보류한다. [r42 변경 기록](../Changes/2026-10-08-driver-return-area-r42.md)에 이번 실행 결과를 기록하며 이전 시험 수와 실제 단말 검증을 혼용하지 않는다.

## 기사 기준 위치·권역 복귀 배차 제안 r37 (2026-10-08)

- [권역 복귀 설계 r37](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/driver-return-area.design.r37.md)에 오늘/기본 복귀지·화물 개인 추천/시간대 조율의 차이와 음식 실제 큐 미연결을 소스 대조로 정리했다. Web의 현재/복귀 좌표 재사용·선호 코드 불일치, 음식 앱 미전달, 화물 선제 10개/음식 정렬 앞단을 우선 보완점으로 남겼다. 기사 선택 기준점·7km 예시 반경과 해제 조건·전체 일정 종료 후 복귀 개선·중복 가산 방지·후보 탐색/화면의 순서를 제안했다.
- 현재는 읽기 조사와 문서 정리다. 제품 소스·배차 정책·DB·APK·실행 환경을 바꾸지 않았고 정적 확인을 실제 화면/업무 검증으로 표현하지 않는다. 수치/가점은 미확정 후보이며 구현·시험·휴대폰은 후속 범위다. 이번 문서 경로의 차이/링크 검증을 별도 수행하고 이전 구현의 시험 수는 재사용하지 않는다.

## 기사 선택에 따른 음식·생활 물품 다건 수행 설계 r36 (2026-10-08)

- 사용자는 기사 본인의 적재량과 수행 수준에 맞춰 3·4건 등 여러 배차를 선택해 수입을 챙길 수 있는 방향을 요청했다. [설계 r36](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/driver-flexible-multi-work.design.r36.md)에 현재 음식 고정 3건·화물의 기존 용달 일정/적재 검사·분산의 다른 업무 차단을 대조하고 기사 개인 선호 → 공동 여력/영속 수락 보호 → 지도 추가 후보·혼합 순서의 보완 순서를 정리했다.
- 음식/화물의 독립 원장·상태·정산을 유지하며 공통 여력과 선택 화면을 연결하는 제안이다. 현재 읽기 조사·문서 반영만 진행했고 제품 소스·정책·DB·APK·단말·실운영·수입 변화·Unity·영상·commit/push는 변경/실행하지 않았다. 문서 검증은 이번 소유 경로에 한정하며 r41 전체 시험 결과를 이 설계의 실행 검증으로 재사용하지 않는다.

## 원주문 선택·관계 동시 변경 보호 r41 (2026-10-08)

- [승인 r35](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/root-order-selection-concurrency.implementation.r35.md)에 따라 기존 분산 작성 화면의 보호된 원주문 선택과 Mongo 관계 변경의 다문서 동시 보호를 구현했다. 최종 Fast 308개·Task 9,147개 통과, 실패·건너뜀 0개. 실제 격리 Mongo 관계 보호 시험 19개와 제품 화면 예시 호스트의 선택·실패/재조회·빈 후보를 확인했다. [화면 책임](../ProjectOverview/page-docs/distributed-fulfillment-r36.md)·[반환 기록](../Changes/2026-10-08-root-order-selection-concurrency-r41.md)에 근거를 남긴다. 다른 조사 항목·APK·Unity·공용 DB·운영 활성화·commit/push는 제외한다.

## 주문 한 건의 역할·조건·다음 행동 리팩토링 r40 (2026-10-08)

- [승인 r34](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/order-ledger-role-refactor.implementation.r34.md)에 따라 기존 주문 루트와 분산 출고/운송 연결, 진행된 연결의 변경 제한, 음식 조건 갱신의 표시/실행 판정, 주문 상세의 현재 업무 안내와 문제 접수를 보완했다. 새 테이블·상태 복제 없이 독립 수령·정산·지급과 기존 OFF/Simulation 경계를 유지한다.
- Fast 323개·Task 9,078개 통과, 실제 격리 MongoDB 환경 시험 2개 미실행. 제품 Razor/공유 ViewModel 검토 호스트에서 주문 상세 4상황·두 폭과 일시 조회 실패 후 재시도를 확인했다. [주문 상세 책임](../ProjectOverview/page-docs/OrdererApp/OrdererApp-P05/README.md)·[분산 상세 책임](../ProjectOverview/page-docs/distributed-fulfillment-r36.md)·[검증 반환](../Changes/2026-10-08-order-ledger-role-refactor-r40.md)을 따른다. 당시 남은 원주문 선택 양식과 하위 상태 동시 변경의 원자적 보호는 위 r41에서 구현·로컬 검증했다. 실제 업무 HTTP/공용 DB·APK·단말·Azure·금전·Unity·영상·commit/push는 r40에서 진행하지 않았다.

## 건별 계약 정책 준비 서버 r39 (2026-10-08)

- [승인 r33](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/task-contract-policy-preparation.implementation.r33.md)에 따라 새 건별 의뢰의 프리랜서 계약 준비 기본값과 미성년 참여 OFF 설정 후보를 서버에 마련하고 로컬 검증했다. 인증된 정책 조회는 실행 허가를 반환하지 않으며 활성화 설정은 서버 시작과 직접 호출에서 거부한다. 전용22개를 포함한 Fast101개·Task9,008개 통과, MongoDB 환경 시험2개 미실행. 기존 정산 DB 시험의 내부 provider 격리를 보완했고 원 검증 항목을 보존했다. 소스8개는 시험 전후 동일하며 다른 기존 변경15,131개와 기존 공용 문서 본문을 보존했다. [서버·DB 책임](../ProjectOverview/page-docs/neighborhood-task-contract-policy-r39.md)·[검증 반환](../Changes/2026-10-08-neighborhood-task-contract-policy-r39.md)에 연결한다. 실제 계약·대리인 확인·미성년 배차·앱·APK·운영 전환은 이번 범위가 아니다.

## 생활 거래 정산 서버·원장 r38 (2026-10-08)

- [승인 r32](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/neighborhood-settlement.implementation.r32.md)의 음식·보관 상품·운송 약정/실제 비용/지급 신고/수령 확인을 서버·보호 원장·인증 API에 구현하고 로컬 검증했다. 공동 배송비 부담, 실물 보관 기간, 부분 지급·환급, 중복 청구와 기존 기사 지급 중복 생성을 구별한다. Fast 48개·Task 8,986개 통과, 기존 환경 의존 MongoDB 시험 2개 건너뜀. 컴파일 대상 22개 소스가 시험 전후 동일하고 기존 다른 변경 15,105개를 보존했다. [서버·DB 책임](../ProjectOverview/page-docs/neighborhood-settlement-r38.md)·[실제 검증](../Changes/2026-10-08-neighborhood-settlement-r38.md)에 연결한다. 앱 화면·APK는 사용자 요청으로 보류하며 실제 송금·공용 DB·휴대폰·Azure·Unity·영상·commit/push는 이번에 진행하지 않았다.

## 분산 보관 합의·사진 완료 r37 (2026-10-08)

- [r31 보관 합의·사진 완료](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/storage-agreement-photo-proof.implementation.r31.md)의 모바일·서버 구현과 로컬 검증을 완료했다. 공간/실제 물품 소유자의 같은 조건 판본 동의, 입고·피킹·전달·반품 사진 촬영/미리보기/전송과 정확한 업무·수량 결속, 비공개 열람·보존 보호를 연결했다.
- 최종 Fast 313개·Task 8,938개 통과, Mongo 환경 시험은 각 1개·2개 건너뛰었다. 최종 컴파일 대상 90개 파일의 시험 전후 소스가 같고, 작은 화면 31종·두 폭 62조합의 넘침·오류가 없었다. [페이지·코드·DB 책임](../ProjectOverview/page-docs/distributed-storage-photo-proof-r37.md), [실행·설치 파일 기록](../Changes/2026-10-08-distributed-storage-photo-proof-r37.md)을 따른다. 후속 사용자 요청으로 APK 추가 제작은 보류했으며 네 앱 최종 manifest 완료는 미확인이다. 기존 r36 default OFF·Simulation 경계와 Unity·배달 영상·다른 dirty 작업을 보존하며 공용 DB·휴대폰·Azure 검증은 별도다.

## 생활 공간의 분산 물류 모바일·서버 연결 r36 (2026-10-08)

- [승인 구현 r30](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/distributed-warehouse.implementation.r30.md)의 공간 동의·분할 입고·재고 예약·기사 직접 피킹·다거점 순회를 모바일·서버에 구현하고 로컬 검증했다. 최신 사용자 정정으로 Unity는 제외하고 기존 변경을 보존했다.
- [페이지·코드·원장 책임](../ProjectOverview/page-docs/distributed-fulfillment-r36.md), [변경·검증](../Changes/2026-10-08-distributed-fulfillment-r36.md)을 따른다. 기본 OFF·정확한 Simulation profile이며 기존 DB 적용·휴대폰·Azure·실운영·실금전·commit/push는 이번에 하지 않는다. Fast 181개·Task 8,868개 통과, 기존 Mongo 환경 시험 2개 건너뜀. 공통 UI 20화면·두 폭 40조합과 네 역할 Debug APK 0.1.36/36을 확인했다. APK 설치·실행은 미확인이다.

## 합의 요금표와 배차·주문 알림 r35 (2026-10-08)

- 사용자가 우선순위 3·4번과 메시지/소리 알림을 승인했다. [구현 범위 r4](Planning/시스템/PLAN-SYSTEM-ROLE-BUSINESS-OPTIONS/agreements-alerts.implementation.r4.md), [페이지 책임](../ProjectOverview/page-docs/agreed-rates-notification-settings-r1.md), [변경·검증](../Changes/2026-10-08-agreed-rates-native-alerts-r35.md)을 따른다.
- 음식점·음식기사·화주·화물기사의 요금표 제안/응답과 실제 업무의 명시 선택, 기존 음식/화물 계산·배차·정산 근거 연결을 구현했다. 합의 기사의 기존 적격 조건과 직접 배차 수락, 음식 조리 순서, 원 금액 동결과 SQL/Mongo 보류 재시도를 유지한다.
- 음식 기사와 음식점 Android에 ‘살뜰 배달’·‘살뜰 주문’ 메시지와 짧은 자체 기계음을 연결했다. 기사 내 정보에서 서버 설정을 저장/재조회하고 음식점은 계정별 기기 설정을 저장한다. 중복 발행·계정 변경·야간 무음·OS 차단/무음을 구분한다. 다른 역할의 새 알림 사건과 음식점 강제 종료 뒤 원격 푸시는 추가하지 않았다.
- 최종 Fast217개·Task8,708개 통과, 실패0·격리 Mongo 환경 시험2개 미실행이다. 전체 빌드와 소스77개 전후/인계 지문, 공통 UI42개·예시16시나리오를 확인했다. 요금표 EF snapshot과 기존 금액을 보존하는 격리 migration3개 시험도 포함한다. 두 개인 Debug APK는 `0.1.35/35`이며 실제 내장 화면/음원/계약·양 ABI와 지도/카드/아이콘·기존 서명을 대조했다. 고정 소스2,718개·현재 모바일 패치31개/기반89개와 이전 r34 소스/APK를 결속했다. 기사 APK에 Firebase 수신기가 없어 현재는 앱 열린 상태의 조회 알림이고, 식당은 실행 중 SignalR/재조회다. 백그라운드 푸시 설정·실폰 소리/업무·기존 DB 적용·Azure·실금전·commit/push는 미실행이며 다른 dirty 작업은 보존한다.

## 기사 전체 화면 지도와 독립 하단 카드 r34 (2026-10-08)

- 사용자는 지도/카드 보완 1·2번 승인 뒤 카드 높이에 맞춰 지도 크기·중심·줌을 바꾸지 않도록 정정했다. [책임 r10](../ProjectOverview/page-docs/driver-workspace-sections-r1.md#전체-화면-지도와-독립-하단-카드-r10--최신-사용자-정정), [이번 변경과 검증](../Changes/2026-10-08-driver-fullscreen-independent-card-r34.md)을 따른다.
- 수행 지도는 같은 인스턴스로 전체 영역을 채우고 하단 카드만 겹쳐 접기/펼치기한다. 지도 공간 예약 계산은 제거했으며, header·도구의 투명한 빈 공간은 입력을 통과시키는 설정을 적용했다. 내 정보/내역·현장 중단·수명·상태별 주 행동·r33 아이콘·API/DB/GPS·경로 handler는 보존한다.
- 독립 읽기 대조에서 기존 Binding/명령/가시·권한/이벤트와 주요 subtree 보존을 확인했다. Fast 187개·Task 8,608개 통과, 실패 0·격리 Mongo 조건 2개 미실행이며 소스 4파일은 전후 지문이 일치한다. 기사 Android·전체 솔루션 빌드는 오류 0개다. 최신 개인 Debug APK는 r34(`0.1.34/34`)이며 승인 제품 3파일만 별도 보존 소스에 반영했다. 양 ABI의 전체지도/독립 카드·카드 배치의 카메라 무접촉·기존 아이콘/GPS/교통 OFF·지도 입력/서명과 Fast/Task 소스 결속을 확인했다. 실제 화면·지도 입력·카드 조작 중 native 카메라/크기·테마/좁은 폭은 다음 USB 검증이다. 기기·에뮬레이터·애저·실운행·실지급·commit/push는 실행하지 않았다.

## 내 정보·배달 내역과 상태 카드 행동 아이콘 (2026-10-08)

- 배달 내역을 내 정보 안으로 옮기고 사람 아이콘을 붙였다. 기존 목록·상세·복귀 경로와 3일 고객 정보 열람 기준을 유지한다. 상태 카드 아이콘까지 포함한 최신 개인 Debug APK는 r33(`0.1.33/33`)이며 포장·양 ABI의 내장 XAML/helper/공통 UI/폰트/CSS와 검증된 제품 10파일 지문 대조를 완료했다. 실폰 설치는 하지 않았다.
- 후속 요청으로 상태별 카드 버튼에 문구와 행동 아이콘을 함께 표시한다. 네이티브 버튼은 기존 글자 색과 확장/운행/결과 대기 상태를 따르고, 공통 역할 카드는 안정적인 행동 키의 보조 아이콘만 선택한다. API/DB·명령·허용 행동·확인 절차는 보존한다. [변경·검증과 APK 판본](../Changes/2026-10-08-driver-profile-state-action-icons.md)을 참조한다.
- Fast 관련 254개·Task 전체 8,597개 통과, 실패 0·Mongo 환경 조건 2개 건너뜀이다. 검증 전후 12파일 지문이 일치한다. 첫 HTML 구조 시험 실패는 기존 허용/차단 조건을 유지하며 실제 control에 결속했고, 이후 CLR 시험 호스트 중단은 부분 결과로 보존한 뒤 동일 설정의 새 실행에서 완주했다. 실제 휴대폰 배치·터치·테마 검증은 별도다.
- 사용자의 후속 요청으로 에뮬레이터 화면 검증을 중지했다. 소유한 에뮬레이터만 종료했고 기존 자료는 보존했다. 다음 휴대폰 USB 연결 때 최신 APK·실제 카드/아이콘·탐색을 검증하고 설치한다. 로컬 빌드/시험, APK 준비, 실제 기기 화면 검증을 구분한다.

## 실제 휴대폰 조회·메뉴 저장·연결 복구와 위치 진단 (2026-10-08)

- 잠금 해제 후 기사 r30의 네이버 지도 교통정보 선 숨김, 완료 목록→상세→복귀·새로고침, 음식점 메뉴 저장·재조회·원복, 네 음식 역할의 동일 완료 주문 표시를 실폰에서 확인했다. USB 연결 제거 후 종료/재실행의 실패 안내와 복구 후 세션/배차 대기도 확인했다. [실제 결과와 화면](../Changes/2026-10-08-physical-phone-runtime-r31.md)에 기록했다.
- SQL/Mongo 완료 주문 보존 비교 10항목·메뉴 원복 7항목이 통과했다. 정산 공제/수령액 미확정·지급 NotRequested를 유지하고 기존 서버/DB를 초기화하지 않았다. 일반 음식 원장 읽기 보완은 현재 실행 서버에 미적용이다.
- Android Debug의 위치 서비스 한 파일에 좌표 없는 단계 진단을 추가했다. Fast 68/68·Task 8,589개 통과(Mongo 환경 2개 건너뜀), r30 대비 그 1파일만 포함한 기사 `0.1.31/31`의 포장·내장 설정·단말 설치 바이트를 대조했다. 실제 단발 위치 수신 7회와 중단 조회의 취소/세대 차단·앱 복귀 재시작을 확인했다. 연속 리스너 수신·실주행/GPS 안정성은 미확인이다.
- 새 휴대폰 주문 작성/수락 전 취소·3일 만료 경계·지원 사건 처리·실제 도로 경로·애저·실지급·다른 네 역할과 commit/push는 이번 완료 범위가 아니다. 원시 단말/위치/계정 자료는 Git 제외 영역에 유지한다.

## 음식 기사 지도 교통정보 표시 끄기 (2026-10-08)

- 도로를 따라 보이는 주황색·초록색 교통정보 레이어를 숨기도록 지도 페이지와 공통 속성 기본값을 변경했다. 픽업 주황·전달 파랑·대기 회색 업무 경로/핀과 위치 복구는 유지한다. [변경과 확인 수준](../Changes/2026-10-08-driver-map-traffic-off.md)을 참조한다.
- Fast 기사 빌드·Task 전체 3.5 빌드 통과, 전체 8,589개 통과·실패 0·Mongo 환경 조건 2개 건너뜀. 소스 두 파일은 검증 전후 지문 일치다. r29 소스·APK를 보존한 별도 r30 사본의 양 ABI에서 실제 내장 교통정보 기본값/XAML false를 대조했다. 같은 서명의 `0.1.30/30`을 단말에 덮어 설치하고 판본·설치 바이트 해시 일치를 확인했다. 설치 후 기록 도구의 변수 충돌은 재설치 없이 고쳤으며 앱 자료를 초기화하지 않았다. 후속 실폰 지도/통신과 r31 위치 진단 결과는 위 최신 기록을 따른다.

## 실제 휴대폰 USB 후속 검증·위치 복구 (2026-10-08)

- USB 인증·5363 연결·격리 서버 준비 상태를 재확인했다. 기존 완료 주문의 SQL/Mongo 상태·식별 검사 12개 통과, 정산 `AwaitingDeductions`·지급 `NotRequested`·공제/수령액 null을 유지한다. [이번 변경·완료 경계](../Changes/2026-10-08-physical-usb-followup.md)는 전날 수락 이후 실폰 시험과 구별한다.
- 기사 위치 원인별 안내·조용한 리스너 재취득·단일 공유 조회·60초 실패 재시도·늦은 응답/센서 시작 정리를 보완했다. Fast 206/206 통과. frozen r27에 승인한 제품 6개만 추가한 기사 `0.1.29/29`를 기존 서명으로 덮어 설치하고 실제 설치 바이트/판본을 대조했다. 계정 자료·원본 소스는 보존했으며 새 APK의 실행·GPS·서버 통신까지 완료한 것으로 보지 않는다.
- 일반 음식 원장 목록/상세는 권한 뒤 음식 블록 상태로 표시하는 읽기 보완을 소스에 반영했다. 전달/수령을 구분하고 DB·업무 상태·Revision·시각·JSON 구조를 보존한다. Fast 35/35·독립 검토 통과. 최종 Task 전체 3.5 빌드 오류 0·8,589개 통과·Mongo 환경 조건 2개 건너뜀, 소스 13파일 전후 지문 일치. 이 서버 보완은 현재 r27 컨테이너에 아직 미적용이다.
- r29 기록 당시 휴대폰은 잠금 해제 대기였다. 이후 완료 내역·메뉴 저장/원복·앱 복귀/통신 복구를 확인한 위 최신 기록을 현재 상태로 참조한다. r29 당시 근거는 Git 제외 `artifacts/local/physical-usb-r29-20261008/`에 보존한다. Azure·실제 도로 경로·실운행·실결제/입금·다른 역할 시험 준비·commit/push는 이번에 실행하지 않았다.

## 실제 휴대폰 네이버 지도 연결·USB 시험 (2026-10-07)

- 최신 사용자 승인으로 기존 네이버 클라우드 `hongdal` 앱에 `kr.ssalddel.fdriver`를 추가하고 기존 패키지/API/한도는 보존했다. 음식 기사 Debug `0.1.28/28`을 기존 서명으로 덮어 설치했으며 실제 Samsung Android 13에서 네이버 지도 타일·현재 위치·배차 대기를 확인했다. 키는 저장소 밖에 두고 Client Secret은 모바일에 복사하지 않았다.
- 음식점의 USB 시험 설정 누락은 기본 OFF·정확한 판본·Android Debug·HTTP 루프백 5363 한정으로 보완했다. Fast 79·Task 8,540개 통과(Mongo 환경 조건 2개 건너뜀), 음식점 후속 APK도 `0.1.28/28`로 설치·해시 대조했다. 기존 PC 생성 주문 한 건을 실제 휴대폰에서 음식점 수락→기사 수락→조리/픽업/전달→주문자 수령 확인하고 운영자 완료 조회까지 대조했다. 신규 주문 작성·실운행·금융 검증과 구별하며 [변경·검증과 한계](../Changes/2026-10-07-physical-phone-naver-maps.md)를 참조한다.
- `mirror-r27-food-mobile-check`의 기존 격리 DB를 보존했다. 마지막 주문은 수령 확인·정산 `AwaitingDeductions`·지급 `NotRequested`다. 주황/파랑 핀과 카드 전환은 확인했지만 실제 도로 경로는 대기 상태이며 Azure·실운행·실지급·다른 역할 지도·commit/push는 포함하지 않는다. 원시 단말 캡처는 현재 위치를 포함하므로 Git 제외 `artifacts/local/physical-usb-r27-20261007/`에 보관한다.

## 생활 배송 간편 작성·엑셀 입력 r29 로컬 검증 완료 (2026-10-07)

- 사용자의 `어` 승인으로 생활 배송 간편 작성·엑셀을 먼저 구현했다. [승인 r29](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/simple-input.implementation.r29.md) SHA256 `1D6D6CA37CDBEC26B5D5050B968C66F461E1F45AE82D7A2BACD5B0FAB0434BDE`·[수용/반환](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/development-work-order.r29.md)·[페이지 책임](../ProjectOverview/page-docs/neighborhood-simple-input-r29.md)·[실제 결과와 샘플](../Changes/2026-10-07-neighborhood-simple-input-r29.md)을 연결한다. r28과 다른 dirty 변경은 보존하며 상태는 `Implemented / LocalInputValidated / OperationalE2ePending / VoiceDeferred`다.
- 기존 한 페이지에 물품→장소→시간→확인과 상세 작성 복귀를 연결했다. 고정 판본의 빈 `.xlsx` 양식과 한 행 미리보기/적용을 추가했으며 누락값은 임의 보완하지 않는다. 인증/동의 뒤 메모리로만 파싱하고 업로드/선택은 견적·접수가 아니다. 기존 견적·동의·접수/재조회, 불명확 결과의 원 요청을 유지한다. 전달 시간창은 선택 null, 행 ID 없는 재업로드의 중복 보장은 세션 안으로 한정한다. 신규 입력 route만 4 MiB 메모리 버퍼를 사용해 디스크 임시 저장을 막는다.
- 신규 70·Fast 453·Task 8,540개 통과, 환경 조건 Mongo 2개 건너뜀. 소비 앱 포함 build 오류 0·기존 Android 패키지 경고 55, 검토 호스트 오류/경고 0. 실제 파서·공통 Razor/VM 320/390px 36장면·6흐름에서 오류·가로 넘침·외부/API 요청 시도 0을 확인했다. 화면은 합성 검토 샘플이지 확정 시안이 아니다. 검토 호스트 종료와 5400 listener 없음도 확인했다. 실제 인증 HTTP/실DB 종단·APK·휴대폰·음성/마이크·외부 업체·실금전·Unity·commit/push·배포는 실행하지 않았다.

## 생활 앱 외부 배송 가정 연결 r28 로컬 구현·검증 완료 (2026-10-07)

- [승인 r28](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/assumed-providers.implementation.r28.md)·[소유권/수용·반환](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/development-work-order.r28.md)·[페이지 책임](../ProjectOverview/page-docs/neighborhood-fulfillment-r28.md)·[변경과 샘플](../Changes/2026-10-07-assumed-fulfillment-r28.md)을 따른다. 기존 r25 수행처에 카카오 문서 기반/배민·쿠팡이츠 가정 모의를 추가했고, 선택 크기를 원 의뢰·원장·SQL 재구성에 보존했다. 실제 endpoint·업체 가격은 추정하지 않았다.
- 후보 목록은 서버의 원천별 활성 목록으로 표시한다. 가정 후보는 별도 opt-in 기본 OFF·Simulation 생활 배송 한정이다. 현재 원천·동의·시도·모의 job이 일치하는 전달 증거로 완료 확인만 열며 내부 기사/큐·양측 완료·공개 이력·지급을 자동 확정하지 않는다. 기존 직접 전달·자체/음식 배송과 다른 dirty 변경을 보존한다.
- 신규 73·Fast 701·Task 8,470개 통과, 환경 조건 Mongo 2개 건너뜀. 전체 3.5 build 오류 0·경고 102, 실제 공통 UI 320/390px 22장면·4흐름·외부 요청/가로 넘침/page error 0을 확인했다. 샘플 PNG는 확정 시안이 아니다. 전용 preview는 종료했다. 상태는 `Implemented / LocalSimulationValidated / RealProviderPending`이며 공용 DB schema·현재 인증 HTTP/실DB 종단·실제 업체·APK/단말·실금전·Unity·commit/push/배포는 미실행이다.

## 첫 수평 보완 완료: 기존 역할 옵션의 적용 범위 r3 (2026-10-07)

- 사용자가 전반적인 보완을 먼저 진행하고 후속에 한 업무를 정해 수직 검증하는 순서를 수용했다. [승인 r3](Planning/시스템/PLAN-SYSTEM-ROLE-BUSINESS-OPTIONS/horizontal-hardening.implementation.r3.md)와 [현 작업트리 수용·소유권](Planning/시스템/PLAN-SYSTEM-ROLE-BUSINESS-OPTIONS/development-work-order.r3.md)에 기존 비금전 표시 범위·정확 경로·검증 상한을 결속했다. 현재 결과는 `FirstSliceImplemented / LocalPreviewValidated / LocalOnly`이며 승인 기획 본문/hash는 유지한다.
- 공통 역할 화면에서 조회 반경/주문 범위, 이번 주문 조리시간/기본값, 건별 사유·판정, 의뢰 예정액/실지급, 이번 현장 작업/개인 설정을 구별한다. 기존 서버 행동·금액·권한·원장과 동시 r26/r27 변경은 보존한다. 알림 소비·기본값 영속화·실제 요금표 합의 연결은 이번 표시 보완의 완료 주장이 아니다.
- [재조사 r2](Planning/시스템/PLAN-SYSTEM-ROLE-BUSINESS-OPTIONS/role-options.review.r2.md)와 승인 r1 본문/hash는 보존한다. 신규 회귀 9개·Fast 집중 187개·Task 8,397개 통과(Mongo 2개 건너뜀), 전체 솔루션 오류 0·경고 111, preview 오류/경고 0. 실제 공통 Razor 320/390px 12개 장면과 입력/확인/복귀/미리보기 저장 차단 6개 흐름을 확인하고 [변경 기록](../Changes/2026-10-07-role-option-scope-r3.md)에 반환했다. 화면은 합성 예시이며 확정 시안이 아니다. 전용 preview는 종료했고 실제 업무 DB/API·APK 배포/휴대폰·실금전·Unity·운영·commit/push는 실행하지 않았다.

## 로컬 검증 완료: 음식 배달 네 역할과 USB 시험 APK r27 (2026-10-07)

- [페이지 책임](../ProjectOverview/page-docs/food-mobile-four-role-r27.md), [변경·검증](../Changes/2026-10-07-food-mobile-four-role-r27.md)을 따른다. 주문 작성 → 음식점 수락 → 서버 제안·기사 직접 수락 → 조리·픽업·전달 → 수령 확인을 격리 MySQL/Mongo와 실제 공통 화면으로 확인한다.
- r27 완료 당시 범위는 로컬 검증까지였다. 해당 기록에서 애저·실제 휴대폰·GPS/외부 경로·결제/입금·commit/push는 제외하며, 이후 사용자 승인으로 진행한 실제 휴대폰·네이버 지도는 위 후속 기록과 구별한다. 같은 checkout의 다른 r26 배송비 변경은 보존한다. 최종 검증 현황은 변경 기록을 참조한다.
- 마지막 표시 보완을 포함한 동결 소스의 Task 8,397개 통과·Mongo 환경 시험 2개 건너뜀, 공통 UI 42화면·14검사와 같은 새 주문의 SQL/Mongo 상태·식별자 검사 11개 통과. Mongo 요약 단계 일치 검증은 별도 한계로 유지한다.
- Debug 0.1.27/27 네 APK의 서명·소스 전후·사본 해시와 에뮬레이터 설치 바이트를 확인했다. 실제 로그인·역할 첫 화면 4개 성공, 원래 AVD/registry 해시 불변을 확인한 뒤 에뮬레이터를 종료했다. 네이티브 전체 업무·실폰·Azure·지도/GPS·실지급의 증거로 확대하지 않는다.

## 음식 배송비 선택 옵션과 부담 확인 r26 구현·로컬 검증 완료 (2026-10-07)

- [승인 계획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/delivery-cost-sharing.implementation.r26.md), [페이지 책임](../ProjectOverview/page-docs/delivery-cost-sharing-r26.md), [변경 기록](../Changes/2026-10-07-delivery-cost-sharing-r26.md)을 따른다. 음식점의 주문자 전액/정액/비율/음식점 전액과 주문자의 잔여 부담 상한을 연결한다.
- 원 주문 정책 전체 동결·공통 누적 배분·전액 한도 초과 보류·구형 정액과 진행 주문 보존을 구현했다. 집중 249·Fast 190·Task 8,388 통과(Task 실제 Mongo 2개 건너뜀), 공통 UI 새 40·기존 26장면과 소비 앱 포함 빌드를 확인했다. 기본 OFF Simulation이며 실제 업체·운영 DB·청구/지급·단말/Azure·외부 공유·commit/push는 별도다. 로컬 기준선은 `artifacts/local/fulfillment-cost-sharing-r26/`다.

## 현재 구현: 생활·음식 배달 자동 수행처 선정 r25 (2026-10-07)

- [승인 계획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/fulfillment-routing.implementation.r25.md), [페이지 책임](../ProjectOverview/page-docs/fulfillment-routing-r25.md), [변경 기록](../Changes/2026-10-07-fulfillment-routing-r25.md)을 따른다. 기존 생활 배송 비교를 음식 배달로 확장하고 플랫폼 자동 선정·조건 내 접수/재선정을 연결한다.
- 음식점 정액 지원은 주문 전체에서 한 번만 적용하며 주문자 잔여 상한·시간·동의·유효 기사 확보와 조리 순서를 검증한다. Unknown/취소 미확정·비용 미확인은 새 수행을 막는다. 구형 경로와 다른 dirty 변경을 보존한다.
- `Implemented / LocalSimulationValidated / SimulationOnly / RealProviderPending`. 집중 138개·Fast 344개·Task 8,234개 통과(환경 조건 Mongo 2개 건너뜀), 전체 3.5 제품/소비 앱 빌드와 실제 공통 Razor 26개 모바일 장면을 확인했다. SQLite 서비스·대역 인증/경로·공용 MySQL 미적용을 구분하며 실제 업체·운영 DB·결제/지급·단말/Azure·커밋/푸시는 별도다. 원시 근거는 `artifacts/local/fulfillment-routing-r25/`다.

## 현재 구현: 생활 배송 수행 방식 선택 r24 (2026-10-07)

- [승인 계획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/fulfillment.implementation.r24.md), [페이지 책임](../ProjectOverview/page-docs/neighborhood-fulfillment-r24.md), [변경·검증](../Changes/2026-10-07-neighborhood-fulfillment-r24.md)을 따른다. 우리 앱 접수의 자체 배차 또는 모의 외부 위탁 선택을 기존 작성·상세·사유 지도에 연결한다.
- 후보의 비용·시간 조건을 비교하고 사용자가 확정한다. 외부 선택은 자체 큐를 생성하지 않으며 접수·취소 결과 미확정에서 다른 수행을 자동 접수하지 않는다. 기본 비활성화된 Simulation 기능이며 실제 플랫폼/결제·Unity·커밋/푸시·배포는 이번 범위에서 제외한다.
- `Implemented / LocalSimulationValidated / RealProviderPending`. 최종 Fast 536개·Task 8,162개 통과(환경 조건 Mongo 2개 건너뜀), 전체 3.5 소비 앱 빌드 오류 0. SQLite 영속·동시 확정/재시작·실제 Mongo builder→SQL projector·loopback HTTP와 실제 공통 UI 22개 모바일 흐름을 확인했다. 실제 MySQL migration/동시성·업체 API·운영 로그인·휴대폰·Azure·지급은 별도다. 새 서버 연결 전에 원 의뢰 두 열과 세 수행 테이블의 migration이 필요하며 기본 플래그 꺼짐도 스키마 요건을 생략하지 않는다.

## 현재 보완: 생활 교류 약속과 선택 배송 r23 (2026-10-07)

- [승인 계획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/life-exchange-facilitation.implementation.r23.md), [페이지 책임](../ProjectOverview/page-docs/neighborhood-collaboration-r1.md#생활-교류-약속과-선택-배송-보완-r23), [변경·검증·화면](../Changes/2026-10-07-life-exchange-facilitation-r23.md)을 따른다. 기존 생활 지도의 선택 패널과 독립 작성 화면을 재사용하고 약속 상세의 상태/행동·조건·전달·금액을 분리했다.
- 직접 수령/전달에서는 기사 비용을 안내하지 않고, 금액 미정과 0원을 구분한다. 기사 배송은 추천/수락·전달/수금 상태를 분리한다. 배차 방식 변경 응답이 불명확해도 상세와 원 선택 결과 확인을 유지하며 기존 동의·취소·철회·반환·공개 선택을 보존한다. API·DB·요금·권한·플랫폼 결제/자동 정산은 변경하지 않았다.
- Fast 378개·Task 8,084개 통과(환경 조건 Mongo 2개 건너뜀), 전체 3.5 소비 앱 빌드 오류 0. 실제 공통 Razor/VM에 읽기 전용 예시 DTO를 연결한 17상태×320/390px의 표시·실패·복구를 확인했다. 이번 렌더는 실제 서버/DB의 새 업무 완료·APK 단말·Azure·운행·입금 증거가 아니다. Figma node 미연결 상태를 보존한다.

## 현재 보완: 본인 APK 업무 검증 r26 (2026-10-06)

- [화면 책임](../ProjectOverview/page-docs/apk-workflow-completion-r26.md), [변경·검증·설치 경계](../Changes/2026-10-06-apk-workflow-completion-r26.md)를 따른다. 안전한 로그인 복귀와 비공개 증빙 전송·폐기, 음식 기사 원본 주문 문제 접수, 운영자 사건 목록·처리를 기존 API에 연결했다.
- 격리된 Simulation 서버의 같은 음식 주문을 공유 UI/client/HTTP/MySQL/Mongo로 수령 확인까지 처리하고 같은 주문의 지원 사건·이의제기를 확인했다. 시험용 통지 증빙과 외부 발송·실제 입금은 구분한다. 최초 주문 생성은 실제 역할 client 준비이며 네 역할의 네이티브 APK 완주 증거로 확대하지 않는다.
- 첫 목표는 본인 APK 시험 사용이다. Fast 266개·Task 8,053개 통과(환경 조건 2개 건너뜀), 소스 2,203개와 최종 Debug APK 두 개의 설치 해시를 확인했다. Android 에뮬레이터에서 역할 홈·지원 화면 스크롤·복귀·입력 정리·완료 상세·연결 실패/재조회도 확인했다. APK는 5362 루프백 서버와 USB 연결을 대상으로 한다. 실제 휴대폰·지도/GPS·Azure HTTPS·운영 신원기관·공개 운영 준비는 별도 확인하며 서버 관문을 우회하지 않는다.

## 이전 구현: 거래와 개인정보 보호 r25 (2026-10-06)

- 사용자 승인에 따라 사업자 음식 거래와 개인 생활 거래의 공통 보호, 초기 만 19세 이상 거래 신청을 구현했다. [법령·화면·서버·저장 기준](../Architecture/CommerceIntermediaryPrivacy.md), [변경·검증](../Changes/2026-10-06-commerce-privacy-r25.md)을 기준으로 한다.
- 중개 안내·현재 판본 확인·판매자 검증·분쟁/권리/사고 지원과 보존/파기/복원 차단을 기존 업무에 연결했다. 판매자 없는 익명 판매 전환, 오래된 보존 의도·통지 증거 재사용, 불명확한 부분 저장 뒤 완료 표시를 차단한다.
- `Implemented / LocalValidated / OperationalPrerequisitesPending`. 최종 Fast 218개·Task 7,968개 통과와 최종 DLL의 실제 격리 Mongo 2개 통과, 전체 3.5 솔루션 빌드 오류 0, 실제 공유 Razor 320/390px 30장·16개 흐름의 오류/가로 넘침 0. 소스 141개 검증 전후 지문 일치, 범위 밖 파일 1,007개·HEAD·branch 보존을 변경 기록에 결속한다. 기존 APK 관찰과 이번 소비 앱 컴파일을 최종 휴대폰 설치 검증으로 합치지 않는다.
- 기본 신원기관 미연결·보존 전체 목록 미검증·운영주체 미설정 상태에서는 새 Operational 거래를 차단한다. 접수된 주문 재조회·취소·일반 게시판은 구분한다. 실제 발송·전체 사용자 삭제·백업 파기·휴대폰/Azure 검증·법적 적합성 인증·commit/push는 이번 구현 결과와 별개다.

## 현재 보완: OS 생명주기와 Android 설치 검증 r24 (2026-10-06)

- 사용자가 r23의 확인된 문제를 보완하고 APK를 휴대폰에 설치해 직접 사용할 수 있는 수준으로 조정하도록 요청했다. [승인 계획](../ProjectOverview/page-docs/os-lifecycle-hardening-r24-plan.md), [구현·남은 경계](../ProjectOverview/page-docs/os-lifecycle-hardening-r24.md), [변경·검증](../Changes/2026-10-06-os-lifecycle-hardening-r24.md)에 범위를 결속한다.
- 음식 배차 재시도의 상태/최초 시각 보존, 인증 수신자와 공개 동의·알림 transaction, 창고의 공통 상태/수량 관문, 확정 화물 조건 변경 차단, 교육 제출 CAS·영수증 보존 복구, 선적의 과거 이벤트/예외 뒤 상태 보호를 반영했다. 마트는 미지원 기사 연결을 보류하며 새 adapter 완료로 취급하지 않는다. 대장은 10개 OS·62개 책임 단계다.
- 별도 기사 앱의 최종 설치 파일로 완료 내역→상세→목록 복귀와 공제·수령액의 중복 없는 미확정 표기, 3일 상세 열람 기한을 확인했다. 실제 입금으로 표시하지 않는다. 네이티브 표기 수정 전 APK·소스 지문·화면·검증 기록도 보존한다.
- `Implemented / LocalValidated / PhysicalDevicePending`. 최종 Fast 402/402·Task 7,857/7,857, 현행 대장 63/63, 실제 격리 Mongo 32/32와 본인 DB 정리 통과. 실제 MySQL 음식 서버의 같은 주문이 기사 선행 배정→조리→픽업→전달→수령 확인과 300초 관찰을 통과했다. 초기 APK의 화면 밖 로그인 문제를 수정해 두 Debug APK를 재패키징·서명/설치 해시 검사하고 Android API 36/x86_64에서 로그인·업무 조회·목록 전환·세션 거절 복구를 확인했다. 대표 설치용 파일과 USB 안내는 로컬 `artifacts/local/os-lifecycle-hardening-r24/apk/`에 있다. 실제 휴대폰/지도/GPS/Azure/입금과 모든 역할의 운영 완주는 별도다. 마트 adapter·창고 재검수/재계수 등 잔여 경계는 보고서에 유지하며 commit/push·영상/MYBOX·Unity 형상 변경은 하지 않았다.

## 현재 조사 완료: 업무 OS 생명주기·절차·상태 r23 (2026-10-06)

- 사용자가 OS별 생명주기 안의 절차·상태를 폭넓게 정적 조사하도록 요청했다. [전체 조사](../ProjectOverview/page-docs/os-lifecycle-static-audit-r23.md)와 [기계 판독 원장](../ProjectOverview/page-docs/os-lifecycle-static-audit-r23.json)에 등록된 10개 OS, 공통 책임 단계 41개, 절차 연결 112개·상태묶음 101개를 담당·선행 조건·인계·회복·소스·관련 시험에 결속했다. 공통 생명주기가 없는 5개 OS는 관찰 절차로 구분하고 설명 순서를 실행 전이표로 사용하지 않는다.
- `SourceAuditComplete / StaticConsistencyGapsFound / RelatedTestsPassed / ProductRuntimeNotVerified`. 기존 관련 시험 1,582/1,582 통과, 새 정적 도구 4,628개 통과·화면/참여자 대장 불일치 3개 탐지, 오류 입력 21개 탐지를 확인했다. [변경·검증](../Changes/2026-10-06-os-lifecycle-static-audit-r23.md)에 원시 근거와 범위를 정리한다. 기존 시험 통과는 모든 실패 순서·동시성·재시도 경로의 안전성 증명이 아니다.
- 우선순위는 동의자·인증 actor 결속과 음식 Outbox/교육 worker의 상태 회귀 → 창고 기존 진입점의 관문·화주 핵심 조건의 판본/재동의 → 부분 저장·지연 사건 복구 → 인계 연결 미확인 → 공통 대장·문서 정리다. 제품 코드·DB·UI·정산·영상은 변경하지 않았으며 실제 단말·Azure·여러 계정 운영 완주·입금·commit/push는 별도다. 기존 r22와 동시 작업을 보존한다.

## 현재 구현: 생활 물품 전달 선택과 유연한 배차 r22 (2026-10-06)

- [승인 계획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/flexible-delivery.implementation.r22.md)에 따라 직접 수령·직접 전달·기사 배송, 건별 자동 추천/공개 콜/병행, 확정 전 방식 변경·합의 재확인과 원 요청 복구를 구현했다. [변경·화면·검증](../Changes/2026-10-06-neighborhood-flexible-delivery-r22.md)에 책임과 근거를 결속한다.
- `Implemented / ValidatedLocal / FieldRuntimePending`. 실제 전용 MySQL/Mongo·두 루프백 Operational 서버의 다섯 정상 흐름과 공통 웹의 양측 완료 확인, 10개 예외 흐름을 확인했다. 기존 Simulation·음식/화물·권한·정보 동의 경계를 유지한다. 물품 금액/배송비/실수금은 분리하며 외부 경로·기사 적격성 등 가상 입력과 실제 운행·휴대폰·Azure·입금은 구분한다. 최종 검증 수와 보존 결과는 변경 기록을 따른다.


## 현재 구현: 역할별 필수 절차와 선택 도구 r22 (2026-10-06)

- 사용자 승인에 따라 현재 절차·조건부 절차·복구는 기본 카드에 두고 독립 조회/등록은 기본 접힌 `업무 도구`에서 기존 페이지로 이동하도록 정리했다. 현재 어댑터의 37개 역할·행동 키를 분류하며 미분류/강조/서버 가능 행동은 보존한다. 종료 이력만 남은 경우에도 시작 진입을 직접 제공하고 생활 협업의 취소·거절·철회는 접힌 관리 영역 밖에 둔다. [책임 카드](../ProjectOverview/page-docs/role-map-workspace-r11.md#필수-절차와-선택-도구-r22--구현-전-책임-카드)와 [구현·검증·화면](../Changes/2026-10-06-role-tools-r22.md)이 근거다.
- `Implemented / ValidatedLocal / DeviceRuntimePending`. Fast 집중 151/151, 최종 Task 전수 7,733/7,733(신규 64개 포함), Android·Windows 소비 앱 및 공통 미리보기 빌드 통과. 실제 공통 렌더 320/390px 30개 화면·22개 흐름에서 메뉴 접힘/조회·선택 복귀·수행/취소 가시성·넘침/터치를 확인했다. 생활은 실제 전용 지도 컴포넌트로 검토하며 지도 키/실제 타일은 이번 증거에 포함하지 않는다.
- Web/MAUI 생활 배송·공간 목록 wrapper의 안전한 지도 복귀를 보완했다. API·DB·요금/정산·역할 권한은 이번 작업에서 변경하지 않았다. 관련 없는 동시 작업의 변화는 해시 대장에 별도로 기록하고 수정/스테이지하지 않는다. Figma 동기화와 물리 단말·GPS·Azure·다중 계정 서버/DB 업무·입금·commit/push·공개 운영은 별도다.

## 이전 구현: 역할 간 업무 연결 보완 r21 (2026-10-06)

- 사용자 요청에 따라 r20의 H01~H03을 구현했다. 운영자 화물 사건 목록/독립 검토, 화주·생활 기본 보류 안내, 음식/화물 기사 완료 내역의 목록→상세→현재 업무 복귀를 기존 API에 연결했다. [책임 카드](../ProjectOverview/page-docs/role-map-workspace-r11.md#역할-간-인계-보완-r21--구현-전-페이지-책임)와 [변경·검증·화면](../Changes/2026-10-06-role-handoff-fixes-r21.md)에 근거를 결속한다.
- 집중 136/136과 Fast·Task 통과, 예시 DTO를 소비하는 실제 공통 웹 렌더 320/390px 24개 상태·6개 이동 흐름을 확인했다. 현재 업무 카드에는 다른 배달 목록을 넣지 않는다. 원장/API/정산 정책과 관련 없는 dirty 작업은 보존한다. Figma 동기화와 실제 서버/DB 업무·휴대폰·Azure·입금은 별도다. 최종 소비 앱 빌드·보존 결과는 변경 기록에 남긴다.

## 이전 조사 완료: 여덟 역할의 업무 인계 r20 (2026-10-06)

- 사용자가 업무 인계를 우선하는 여덟 역할 조화 조사 계획의 실행을 승인했다. [조사·보완 명세](../ProjectOverview/page-docs/role-handoff-audit-r20.md)에 여덟 책임·12개 대표 인계·실패/재시도/권한·확인된 부족의 근거와 수정 책임을 기록했다.
- `SourceAuditComplete / LocalTestsPassed / GapsSpecified / DocumentationValidated / CrossRoleProductRuntimePending`. 관련 21개 시험군 261/261 통과와 현재 화주 adapter의 보류 전/후 매핑 차이를 확인했다. 운영자 화물 예외 UI 연결→의뢰자 기본 보류 안내→통합 기사 완료 내역 진입이 후속 순서다. 문서 Fast/Task와 링크 442개, 이번/이전 기준선의 범위 밖 파일 보존을 확인했다. 제품 코드·API·DB·요금/정산은 변경하지 않았으며 결과와 증거 범위는 [r20 변경 기록](../Changes/2026-10-06-role-handoff-audit-r20.md)에 결속한다.
- 실제 여러 계정의 제품 UI/HTTP/DB 완주·휴대폰/GPS/Azure·push/입금·공개 운영·commit/push는 이번 증거에 포함하지 않는다. r18·r19와 기존 dirty 작업을 보존한다.

## 이전 완료: 여덟 역할의 첫 진입 r19 (2026-10-05)

- 사용자는 중개 수수료보다 서로 협업하고 생활을 개선하는 장을 우선하며 여덟 입장의 완성도를 계속 높이려 한다. [현재 결정](DECISIONS.md#2026-10-05-수수료-중개보다-참여자의-교류와-생활-개선을-우선)에 방향과 후원/광고 후보의 미정 범위를, [역할 진입 검토](../ProjectOverview/page-docs/role-entry-review-r19.md)에 첫 활동·현재 연결·후속 질문을 기록했다.
- `Implemented / ValidatedLocal / DeviceRuntimePending`. 현재 구현 범위에 맞는 홈 설명과 Mirror 표제를 보완했다. Fast 집중 99/99·Task 전수 7,625/7,625 통과, 공통 예시 웹의 320/390px 홈 여덟 링크와 대표 세 역할 진입/복귀를 확인했다. 소비 앱과 공통 예시 웹·Android 빌드, 화면/검증 범위는 [r19 변경 기록](../Changes/2026-10-05-role-entry-review-r19.md)에 결속한다. 범위 밖 기존 dirty 파일 902개 해시와 HEAD·branch를 보존했다. 실제 제품 생활 지도·모든 상태·휴대폰/GPS/Azure·공개 운영은 별도이며 광고·후원·영상 게시·commit/push는 수행하지 않았다.

## 이전 완료: 상태별 업무 카드와 창고 연결 r18 (2026-10-05)

- 사용자가 r17 보완 우선순위에 따라 계속 구현하도록 승인했다. 화물 예외의 재조회·행동 조건, 창고 수령→검수/인계 준비→출고 검토·실제 인계, 음식 복합 상태 안내와 단계별 필수 정보를 기존 화면·API에 연결한다. [책임 기준](../ProjectOverview/page-docs/role-map-workspace-r11.md#상태별-업무-연결과-예외-복원-r18)과 [변경 기록](../Changes/2026-10-05-state-card-polish-r18.md)에 근거를 모은다.
- `Implemented / ValidatedLocal / CardReviewPending / DeviceRuntimePending`. Fast `20261005-224311` 집중 320/320·Task `20261005-224540` 전수 7,625/7,625 통과, 공통 예시 웹에서 화물 보류/재개와 음식 준비 단계, 창고 검수·출고·운송 요청·실제 인계의 독립 입력/지도 복귀를 확인했다. 마지막 19개 확인의 320/390px 가로 넘침과 브라우저 오류 0, 미리보기와 통합 앱 Android 빌드 오류/경고 0. 최종 원시는 `artifacts/local/state-card-polish-r18/`와 변경 기록에 결속한다.
- 기존 dirty 작업과 r17 조사 결과를 보존한다. 실제 휴대폰·GPS·Azure·운영 서버/DB 업무 완료·입금·commit/push는 수행하지 않았다. 이번 예시 실행은 지도 키 미설정 안내/목록 대체 상태이며 r14 타일 검증이나 단말 실행으로 확대하지 않는다. 카드 최종 배치는 사용자 검토 대상으로 남긴다.

## 이전 조사: OS 생명주기와 역할별 상태 카드 r17 (2026-10-05)

- 사용자가 OS의 생명주기·업무 흐름·단계 상태에 맞는 카드 배치를 조사해 달라고 요청했다. [상태 카드 조사](../ProjectOverview/page-docs/os-state-card-audit-r17.md)에 OS 설명 단계·저장 상태·서버 허용 행동·화면 수명을 구분하고 일곱 업무 역할의 공통 카드와 대표 전문 화면을 대조했다. 생활은 별도 동네 지도 흐름이며, 다른 OS는 대장·대표 진입 확인 범위와 미검토 세부 상태를 구분한다.
- `SourceAuditComplete / CardDesignPending / FullStateRuntimeNotVerified`. 실제 음식점·음식 기사 표시 adapter를 읽기 전용 예시 DTO로 실행한 다섯 사례에서 픽업 완료 후 인계 안내와 준비 미완료 재조리의 픽업 안내를 재현했다. 제품 코드는 변경하지 않았으며, 이번 조사에서 실제 앱·서버·DB 업무·휴대폰·GPS·Azure·입금·새 제품 전수 시험·commit/push는 수행하지 않았다. 기존 r16 화면·검증의 범위를 확대하지 않는다.
- 후속은 화물 예외의 읽기 투영·행동 조건, 창고 검수 연결, 음식의 복합 상태 안내를 먼저 보완하는 제안이다. 기본·상세 카드의 정보 배치·강조 순서는 사용자 검토 대상으로 유지한다. 원시 조사와 표시 코드 재현은 Git 제외 `artifacts/local/os-state-card-audit-r17/`에 보관한다.
- 문서 범위 Fast `20261005-212540`·Task `20261005-212553` 통과(제품 빌드·시험 생략). 네 문서의 로컬 연결 201개 누락 없음, 범위 밖 기존 변경 파일 874개 해시 유지와 HEAD·branch 불변을 확인했다.

## 이전 완료: 지도 위치·갱신과 역할별 기본 카드 r16 (2026-10-05)

- 사용자가 r15 제안 1·2·3 진행을 승인했다. [구현 계획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/role-map-reliability.implementation.r16.md)과 기존 [페이지 책임](../ProjectOverview/page-docs/role-map-workspace-r11.md)에 위치 근거·만료·표시 중 갱신·다음 업무 선택·기본 카드 초안을 결속했다. [상태별 카드 검토표](../ProjectOverview/page-docs/role-state-card-review-r16.md)와 [변경·캡처](../Changes/2026-10-05-role-map-reliability-r16.md)에 사용자가 후속 결정할 기본·상세·주행동을 정리한다.
- `Implemented / ValidatedLocal / CardReviewPending / DeviceRuntimePending`. Fast `20261005-203528` 집중 243/243, Task `20261005-203725` 제품 3.5 빌드 오류 0·전수 7,561/7,561 통과. 실제 공통 예시 웹의 일곱 역할 기본 카드·320/390px·위치 만료·목록/상세·다음 업무를 확인했다. 기존 dirty 작업을 보존했고 실제 휴대폰·GPS·서버 업무 완료·Azure·출시·commit/push는 수행하지 않았다. 모든 상태의 카드 디자인을 최종 승인한 결과는 아니다.
- 별도 화물 기사·통합 앱 Android 빌드 오류 0, 최종 미리보기 빌드 오류/경고 0. 새 검증 탭의 브라우저 오류 0과 작업 밖 기준선 1,066개 파일 해시 유지를 확인했다. Android 키·타일·실제 단말 실행과 카드 최종 검토는 별도로 남긴다.

## 이전 조사: 역할 앱의 지도 중심 구성 조사 r15 (2026-10-05)

- 다른 역할 앱의 지도·기본 카드·독립 업무 화면을 소스에서 대조했다. [역할별 조사와 보완 순서](../ProjectOverview/role-map-coverage-audit-r15.md)에 통합 여덟 역할과 전용 앱 진입의 차이, 화물 기사·화주·주문자·음식점·창고·운영자·생활의 조정안을 기록했다. 판매자·인적자원 관리 앱은 홈 책임만 확인했다.
- `SourceAuditComplete / Proposed / RuntimeNotVerified`. 우선순위는 화물 네이티브 현재 위치와 카메라 중심 분리 → 선택 업무 갱신·위치 만료·다음 업무 선택 → 소유 화물 운송의 기존 좌표 연결·기본 카드 → 음식 역할·운영자 → 전용 앱 진입이다. 조사 문서·목차·현재 작업 요약만 수정했으며 제품 변경·빌드·실행·APK·Azure·휴대폰·commit/push는 수행하지 않았다. r12~r14와 다른 dirty 작업을 보존한다.
- 문서 범위 Fast `20261005-195131`·Task `20261005-195138` 통과, 조사 문서 링크 42개 누락 없음, 범위 외 기존 변경 파일 841개 해시 유지와 이전 snapshot 보존을 확인했다. 제품 빌드·시험을 수행한 결과는 아니다.

## 이전 완료: 실제 Google 웹 지도 미리보기 r14 (2026-10-05)

- 사용자의 적용 화면 요청에 따라 기존 기사 공통 UI와 제품 웹 지도 host/JS를 로컬 미리보기에 연결했다. 실제 Google 타일 위의 주황 픽업·파랑 전달 핀, 수락 전/픽업 중/전달 중 핵심 카드·상세와 지도 확대/이동·핀 설정을 확인했다. [변경 기록과 화면](../Changes/2026-10-05-google-map-web-preview-r14.md)에 실행 근거를 결속한다.
- `WebMapRuntimeVerified / ExampleWorkflow / AndroidMapKeyPending`. 전용 빌드 오류/경고 0, 320/390px 넘침 없음·브라우저 오류 0, 모듈 HTTP 200/제품 바이트 일치와 Development·루프백·Origin·no-store 키 공급을 확인했다. 키는 프로세스 설정에서만 사용했고 제품/Android 설정과 r12·r13 및 다른 dirty 작업은 보존했다. 실제 GPS·도로 경로·Android SDK 실행·실배차/서버/DB·Azure·휴대폰·외부 배포는 이번 범위가 아니다.

## 이전 완료: Google 지도 SDK 연결 점검 r13 (2026-10-05)

- 기존 Android Google SDK·지도 host·핀/선 연결을 확인했고 통합 앱의 누락 coarse/fine 권한·선택적 위치 기능과 사용자 행동 시 권한 확인/요청·거절 복구를 보완했다. [변경 기록](../Changes/2026-10-05-google-map-sdk-connection-r13.md)에 키 설정·앱 ID/Debug 인증서와 검증 근거를 결속한다.
- Fast `20261005-190402` 통합 앱 빌드 오류 0, Task `20261005-190557` 0.0 slice 빌드 오류/경고 0·전수 7,447/7,447과 실제 생성 manifest의 위치 권한을 확인했다. `AndroidMapKeyPending / MapRuntimePending`: 다운로드한 항목의 종류·Android용 키 설정은 사용자 확인 대기이며 과거 브라우저 키를 임의 재사용하지 않았다. 실제 타일/GPS·기기 권한 대화상자·키 설정 내장 APK·Cloud 설정은 미완료다. r12와 다른 dirty 작업은 보존했다.

## 이전 완료: 완료 확인·기사 기본 카드 r12 (2026-10-05)

- 사용자 승인 보완 1·2에 따라 음식 행동의 실제 후속 상태 확인/조회 전용 복구와 기사 카드의 핵심 정보 기본 표시를 구현한다. [r12 구현 계획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/role-workspace-completion-card.implementation.r12.md)과 [책임 카드](../ProjectOverview/page-docs/role-map-workspace-r11.md#완료-확인기사-기본-정보-r12), [변경 기록](../Changes/2026-10-05-role-workspace-completion-card-r12.md)에 검증 결과를 결속한다. r11·기존 dirty 작업·서버 계약/권한은 보존한다.
- `Implemented / ValidatedLocal / PublicDeploymentPending`. Fast 집중 221/221, Task `20261005-184125` 전체 3.5 빌드 오류 0·전수 7,447/7,447, 실제 공통 UI의 기사 세 단계/조회 전용 복구와 최신 내장 Debug APK 서명·정렬·CRC·현재 CSS를 확인했다. UI는 예시 자료이며 실제 서버/DB 업무 완료·휴대폰·Azure·지도 연결과 다음 업무 선택·자동 갱신·목록 확장은 이번 범위가 아니다.

## 이전 완료: 역할 선택 홈·역할별 지도 r11 (2026-10-05)

- 사용자가 승인한 여덟 역할의 선택 홈과 같은 통합 Web/Android 앱 안의 역할별 지도·현재 업무 카드를 구현한다. 생활 화면은 기존 r10을 재사용하고 음식·화물·창고·운영자는 기존 API와 상태별 허용 행동을 연결한다.
- [구현 계획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/role-map-workspace.implementation.r11.md)과 [책임 카드](../ProjectOverview/page-docs/role-map-workspace-r11.md), [변경 기록](../Changes/2026-10-05-role-map-workspace-r11.md)에 입력·인증·지도 복귀와 실제 검증을 결속했다. 최종 Task `20261005-173938` 전체 빌드와 7,402/7,402 시험, 실제 통합 WASM의 역할/로그인 취소/입력 직접 진입·새로고침, 예시 카드 화면과 내장 Debug APK를 검증했다. `Implemented / ValidatedLocal / PublicDeploymentPending`이며 실제 새 역할 UI의 서버 업무 완료·단말·Azure·Android 지도 타일은 별도다.

## 이전 완료: 일반 이용자 지도 작업공간 r10 (2026-10-05)

- 사용자 승인 계획에 따라 SsalddelApp Android, 통합 Web, 독립 01 Community Web의 지도 조회·선택·현재 업무를 패널로 정리한다. 글·조건·공간·배송 등록은 입력 전용 페이지로 이동하고 저장/취소 뒤 기존 지도 맥락으로 복귀한다.
- [r10 구현 계획](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/map-workspace.implementation.r10.md)과 [구현 전 책임 카드](../ProjectOverview/page-docs/neighborhood-map-workspace-r1.md)에 상태 소유, 레이어·장면·플랫폼 분리, 개인정보와 검증 범위를 결속했다. 기존 업무 원장·기능 관문·전문 역할 앱·r9 흐름과 unrelated dirty 변경을 보존한다.
- 집중 시험 321/321, 최종 Task `20261005-155118` 전체 3.5 빌드(오류 0·경고 55)와 전수 시험 7,297/7,297을 통과했다. 공통 화면의 실제 HTTP/SQLite/보호 Mongo 흐름과 독립 01 WASM의 지도·입력·로그인 취소 복귀, 12개 화면 및 최신 내장 Android Debug APK의 서명·정렬·자산·해시를 확인했다. [변경 기록](../Changes/2026-10-05-map-workspace-r1.md)에 검토용 인증/배송, 실제 단말·Android 지도 키·Azure·Figma의 미검증 범위를 구별한다. `Implemented / ValidatedLocal / PublicDeploymentPending`이며 커밋·푸시는 하지 않았다.

## 이전 완료: 생활 협업·작은 보관 공간 r9 (2026-10-05)

- 사용자 승인 네 항목에 따라 지도 글·보관 능력에서 신청 → 양측 합의 → 수행 → 상대 완료 확인을 연결한다. 같은 계정의 요청·제공·참여 목록, 수량·시간 예약과 별도 인수·반환, 전원 동의 후 최소 공개 이력을 추가한다.
- 개인 합의는 별도 보호 Mongo 컬렉션에, 보관 인계 주소는 암호화해 저장한다. 전문 기사·음식 조리·창고·지급 권한은 기존 업무 경계를 유지한다. 보관 예약과 협업 취소의 교차 경쟁은 영속 intent와 공간 CAS 중단 증적으로 처리한다.
- 집중 시험 308/308, 최종 Task `20261005-140852` 전체 3.5 빌드와 전수 시험 7,256/7,256을 통과했다. 실제 공유 UI→HTTP→SQLite/Mongo의 신청·합의·상호 완료, 보관 등록·예약·인수/반환·비공개 차단과 9개 화면을 확인했고, 최신 내장 Debug APK의 서명·정렬·자산·해시를 확인했다. [변경 기록](../Changes/2026-10-05-neighborhood-collaboration-r1.md)에 검토용 인증/배송과 실제 운영 로그인·개인 단말·Android 지도 키·Azure 운영을 구별한다. 이전 r8 지도와 r7 배송 근거는 아래 보존한다.

## 이전 완료: 공통 생활 지도 홈 r8 (2026-10-05)

- 사용자 승인 계획에 따라 Web와 SsalddelApp Android의 기본 진입을 `/community/map`으로 연결했다. 제공·필요 글, 본인 진행 배송, 선택 공공자료를 지도 또는 목록에서 탐색하고 기기별 표시 설정을 기억한다. 작성·배송 접수·업무 수행은 독립 화면에 유지하며 기존 전문 역할 선택은 `/apps`에 보존했다.
- 초기 공개 위치는 공식 자료로 대조한 서울30행정동의 대표점만 사용한다. 공개 글의 동네 선택은 선택 사항이고 기존 글을 임의 배치하지 않는다. 정확 배송 주소·기사 GPS는 본인 인증과 현재 배정 증적을 통과한 개인 조회에만 제공한다. 최근 위치의 측정·수신 모두10분 이내 검증과 재배정·종결·이탈 시 정리를 추가했다.
- 공개 HTTP·생산용 UseCase·SQLite 저장, 실제 Google Web 지도 타일, 동네 선택→작성→지도 복귀와 설정 저장, 320/390/1100px 화면을 확인했다. 개인 배송 화면은 명시한 검토용 데이터이며 실제 기사 배정/GPS·외부 예정 경로 실행과 구별한다. 집중 시험93/93, 최종 Task `20261005-123154` 전체3.5 제품 빌드(오류0·경고95)와 전수 시험7,168/7,168을 통과했다. [변경 기록](../Changes/2026-10-05-community-map-home-r1.md)에 실행 범위와 원시 근거를 연결한다.
- Android 네이티브 코드 빌드와 최신 코드·런타임을 내장한 Debug APK 생성, 서명·ZIP 정렬·ARM64/x86_64 런타임·사본 해시를 확인했다. 전용 지도 키·실제 타일·개인 휴대폰·Azure 공개 통신은 별도 검증이 남는다. 브라우저용 키를 Android에 재사용하지 않는다. 배차·운임·플랫폼 결제 권위는 유지하며 다른 dirty 작업을 보존했다. 커밋·푸시·외부 배포는 수행하지 않았다.

## 이전 구현: 일반인 생활 교류·생활 배송 r7 (2026-10-05)

- 사용자의 최신 문답 방향과 구현 요청에 따라 [생활 교류 최소판 r6](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/exchange-mvp.r6.md)를 구현했다. 목록·글 작성·공개 문의의 세 라우트를 기존 게시글 API/DB에 연결하고 통합 앱 홈/커뮤니티 메뉴와 01 웹 메뉴에 진입점을 추가했다.
- [생활 배송 r7](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/server-dispatch.r7.md)은 공개 글과 분리된 본인 배송 의뢰를 서버 견적·기존 화물 원장/배차 큐에 연결했다. 선정 기사·추천 차수·기한에 결속한 별도 정보 제공 동의/철회를 추가하고 기사 조회·추천의 개인정보를 보호했다. 접수·제안·기사 수락·현장수금예정을 구별하며 음식 전용 흐름은 유지한다.
- 재고·예약·참여자 배차를 강제하지 않고 중단된 미검증 r5 코드만 로컬 보관 후 실행에서 분리했다. 기존 다른 dirty 변경은 보존했다. 원본의 기본 데이터 상태와 공개 게시·댓글/거래 인증의 의미를 섞지 않는다.
- 실제 공유 컴포넌트/HTTP client → 루프백 API → 생산용 UseCase/쓰기 정책 → SQLite 저장으로 다른 브라우저의 문의와 새로고침을 검증했다. 웹/Windows 통합 앱 빌드와 관련 시험·검증 결과는 [변경 기록](../Changes/2026-10-05-neighborhood-exchange-mvp-r1.md)에 기록한다. Azure·외부 공개·기기 설치·Figma node 검증은 별도다.
- 배송은 격리 원장/큐 시험과 같은 제품 공유 UI의 화면 시험을 구별하며 [서버 배차 변경 기록](../Changes/2026-10-05-neighborhood-server-dispatch-r1.md)에 근거를 기록한다. 다중 서버 DB 멱등성·실제 운영 자동 제안/수락/수행·실입금·Azure 공개 운영은 별도다.

## 이전 확장 설계: 주거 공간의 상품 등록·구매 신청·퀵 배송 r4 (2026-10-05)

- 사용자 요청으로 [주거 상품·퀵 배송 설계 r4](Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/residential-goods-quick-delivery.design.r4.md)를 기존 생활권 물류거점 기획에 추가했다. 집·구획·기능·동의 → 보유 상품·재고 → 구매 신청·수량 예약 → 기존 화물 배차·픽업·전달의 정보 관계, 상태, 페이지→API/DB, 권한과 구현 순서를 정리했다. 주거 유형은 새 설계의 참여 제한으로 두지 않으며 법적 요건 검토는 이번 범위에서 제외했다.
- 현재 거점의 비주거 고정·관리자 전용 예약·용량/모의 보상, 판매상품의 필수 입고 연결, 기존 구매 요청과 실제 주문의 차이, 일반 상품과 마트 음식 배송의 경계를 읽기 대조했다. 첫 구현은 집 한 곳·상품 한 종류·주문 한 건을 기존 화물 기사까지 연결하는 순서다. 상세 설계는 제안이며 제품 코드·DB·APK·외부 운영·결제를 변경하지 않았고 새 실행 시험·커밋·푸시는 하지 않았다.

## 이전 방향 기록: 일상 공간 활용을 중심으로 한 사업 협업 (2026-10-04)

- 사용자 요청으로 [일상 공간 활용 방향](DECISIONS.md#2026-10-04-일상-공간-활용을-중심으로-한-사업-협업-방향)을 기록하고 [공용 프로젝트 컨텍스트](../ProjectOverview/GptProjectContext.md)에 연결했다. 기존 공간과 사람의 기능을 활용하는 사업을 지향하며 물류센터 신축을 참여 전제로 삼지 않는다.
- 기존 생활권물류거점의 공간·동의·용량·보상 기반을 읽기 대조했다. 현재 독립 비주거·상온 밀봉품 Pilot 제한과 음식·화물 업무 분리는 유지한다. 다음 설계는 공간과 기능·담당자 연결, 이용 시간·구획·용량 예약, 기존 주문/출고/인계와의 연결이다. 이번에는 문서 방향만 기록했으며 제품 코드·DB·APK·외부 운영을 변경하거나 새 시험을 실행하지 않았다.

## 최근 검증 완료: 휴대폰 없는 Azure·모바일 연결 보완 검증 (2026-10-04)

- [구현·통합 검증 보고](../Deployment/AzureMobileHardeningVerification-20261004.md)에 운영자 음식 관제 독립, 7앱 인증/통신, 음식점·기사 미확정 요청 보존, 음식 전용 푸시 연결, 로그 최소화, 배포·서명·복구 도구를 페이지→코드→API/DB로 연결했다. 이전 조사와 실행 기록은 아래에 보존한다.
- 최종 Task `20261004-220250` 전체 3.5 솔루션 빌드 **오류 0·경고 119**, 전수 시험 **6,975/6,975 통과**. 앞선 Fast495/495, 음식점87/87, Admin84/84도 통과했다. 401 정리·계정 보호·미확정 요청 보존을 유지하며 관찰 배차 재탐색/만료, 기사 완료 상세의 실제 주문 출처 연결, 늦은 통신 오류와 최신 상태 표시를 보완했다. Debug APK 7개를 생성·서명·정렬·내장 런타임 확인했고 전용 에뮬레이터에 설치했다.
- 격리 Development/Simulation 서버·DB에서 같은 주문의 주문자→음식점→기사→주문자 수령 확인→운영자 조회를 실제 APK UI로 완주했다. 별도 DB4컨텍스트 migration·백업/복원·readiness200, 서버 재시작 후 원 주문 재조회, 음식점·기사 응답 유실 뒤 앱 재시작/정본 조회와 중복 쓰기 부재를 확인했다. 최신 기사 APK에서도 완료 상세와 3일 열람 제한 표시, 실제 GET503 안내와 동일 앱 실행 중 오류 해소를 확인했다. 공제 미확정·지급 미요청은 그대로 보존하며 실입금으로 보고하지 않는다.
- 내부 시험 서명의 Release 7개(기존 검증된 2개 재사용·나머지 5개 새 빌드)를 완성하고 서명·정렬·두 ABI·실제 다운로드 해시를 확인했다. 전용 API36 에뮬레이터의 7개 시작 화면과 설치된 base APK의 SHA-256 일치도 확인했다. 음식·화물 기사 OS 알림 권한 안내에서 중단한 원 기록을 보존하고, 권한을 거절한 뒤 로그인 화면을 확인해 검사를 이어갔다. 주소는 시험용 `https://mirror-field-test.invalid/`이며 Release 서버 통신·업무 완주·운영 서명 증거가 아니다. ELF 16KiB 정렬 구조 검사도 실제 16KiB 휴대폰 실행과 구별한다.
- Firebase 설정이 없는 기본 APK는 조회 대체를 유지한다. 선택 설정 경로는 실제 Firebase 패키지의 Android C# 컴파일을 통과했으나 GoogleServicesJson·APK·설치·토큰 등록·외부 수신을 실행한 결과가 아니다.
- Azure 계정의 리소스 조회는 재인증 필요로 실패했다. 실제 Azure·공개 HTTPS·개인 휴대폰·운영 서명·실입금은 미검증이다. 이 경계를 로컬 에뮬레이터/내부 시험 서명으로 대체하지 않는다. 검증용 Docker 프로젝트와 전용 에뮬레이터를 종료하고 APK·DB 볼륨·백업·화면·원 실패 기록을 보존했다. 커밋·푸시는 수행하지 않았다.

## 이전 조사 완료: Azure 서버·휴대폰 APK 연결 준비 (2026-10-04)

- 사용자가 APK 다운로드·개인 휴대폰 설치·Azure 통신의 현재 준비 수준과 부족한 부분을 전수 조사해 달라고 요청했다. [종합 조사와 보완 순서](../Deployment/AzureMobileReadinessAudit-20261004.md)에 7개 모바일 앱·인증/통신·역할 API·배포/서명·DB/health·알림·복구·기존 실행 근거를 대조했다. 제품 수정·새 빌드/시험·APK 생성/설치·서버/DB 실행·Azure 배포는 하지 않았다.
- 7앱 모두 공개 주소 주입 코드는 있지만 확인된 APK는 loopback5361 Debug·API36 에뮬레이터 범위다. 음식 네 역할 Release 도구는 있으며 화물 3개 앱은 대상 밖이다. APK+AAB와 env 서명 비밀번호의 공식 제한·최종 signed package 선택/검사·웹 다운로드/실휴대폰 업데이트도 미검증이다.
- 먼저 음식 운영자 홈·관제의 화물 종속을 분리하고 기존 음식 추적/중단 검토 API를 연결한다. 격리 Staging/Simulation profile은 존재하나 공통 배포/CI 묶음에 연결되지 않았다. 공개 HTTPS·네 APK 동일 주문 완주 후 주문자/창고 실행 중 갱신, 음식점/기사 pending 종료 복구·음식 전용 push를 좁게 보완하는 순서다.
- 복구 source=restore 미차단, 부분 비밀 생성의 기존 키 교체 조건, rollback의 이전 override 미복원, 실제 초기화4/readiness3/staging migration1 범위 불일치를 코드에서 확인했다. 실제 장애 재현은 아니다. 해당 도구 사용 전 보완과 실운영의 백업/감시·민감 query/오류 로그·확장 경계를 개인 테스트 필수와 나눴다. 72시간은 이미 구현된 기사 상세 열람 제한이며 DB/로그 삭제 기간이 아니다.
- Azure CLI 계정 캐시는 Enabled지만 실제 리소스 조회는 AADSTS50132 invalid_grant/InteractionRequired로 실패했다. 현재 구독/VM/App Service·공개 host는 미확인이며 재로그인·구독 변경·기동/배포를 하지 않았다. 직전 전체 시험6,812/6,819·기존7실패와 로컬2APK 재조리 근거는 아래 기록 범위를 유지한다. 조사 원시는 Git 제외 artifacts/local/azure-mobile-readiness-audit-20261004/에 보존한다. 커밋·푸시는 하지 않았다.

## 이전 완료: 재조리·새 기사 인계와 휴대폰 서버 연결 준비 r1 (2026-10-04)

- 휴대폰 설치 후 정상 업무와 미래 Azure 서버 연결을 목표로 다음 우선순위인 픽업 후 중단 → 재조리 → 새 기사 인계를 보완했다. [페이지→코드/API→DB 책임](../ProjectOverview/page-docs/food-recooking-mobile-r1.md), [변경·Android 화면](../Changes/2026-10-04-food-recooking-mobile-r1.md)에 연결한다.
- 과거 첫 조리·준비 이력은 보존하고 현재 차수의 조리·준비·가능 행동을 같은 서버 정책으로 판정한다. 재조리 음식 준비 전 픽업을 차단하며 픽업 전 기사 교체는 같은 음식의 준비를 유지한다. 음식점·기사 기존 화면은 이번 음식의 대기·예정·실제 완료만 해당 단계에서 안내한다. 조회만으로 픽업하지 않는다. 화물 정책·DB migration·정산 정책은 변경하지 않았다.
- 최종 Fast `20261004-185802` 전체 3.5 빌드·관련 165/165 통과. Task `20261004-190141` 전체 빌드 통과·시험 6,812/6,819. 기준 `182913`의 기존 실패 7개가 이름·메시지·전체 스택까지 동일하며 신규 30개 전부 통과·새 실패 0·기존 결과 변경 0·삭제 0이다. 전체 시험 게이트는 미통과다. 동결 24개와 범위 밖 기존 dirty 337개 지문 변경 0을 확인했다.
- 격리 Development/Simulation 서버·DB에서 주문 `FOOD-20261004100525112`의 중단→재조리→새 기사→수령 확인과 중복 중단·준비 전 픽업 차단·새 시도만 완료 정산을 검증했다. 별도 주문 `FOOD-20261004100615503`은 서버 재시작 뒤 실제 음식점·기사 Android APK에서 준비 완료→픽업→전달을 처리하고 주문자 Client HTTP로 수령 확인·차수 2·12개 이력을 재조회했다. 두 APK 서명·정렬·내장 런타임·해시, 음식점 가로 넘침 0·Android fatal 0을 확인했다.
- 실제 화면은 API36 별도 시험 AVD다. 저장 공간·ADB 재연결 후 명시 조회 복구가 있었으며 공개 HTTPS 안정성·개인 휴대폰·네 역할 APK 전체 완주·지도·실결제·입금·자동 Operational 배차 증거로 확대하지 않는다. 개발 주소는 `http://127.0.0.1:5361/`과 PC reverse다. 이번 서버·에뮬레이터는 종료하고 로컬 APK·DB 볼륨·근거를 보존했다. 커밋·푸시는 하지 않았다.
- Azure App Service 포트·PFX/키링·Redis 선택·배치/인스턴스 경계와 VM 인증서 경로·Staging/Simulation 현장 검증 기준을 현재 코드에 맞춰 문서화했다. 미래 사용 계획에 따른 준비이며 Azure 리소스 생성·배포는 하지 않았다. 다음은 운영자 모바일의 음식 주문 홈·관제를 화물 기능에서 독립시키고 기존 음식 추적·중단 검토 API를 연결하는 것이다. 개인 휴대폰/공개 HTTPS·Release 서명·음식 앱 별도 푸시 연결·기존 시험 실패 7개도 남아 있다.

## 이전 완료: 음식·화물 자동 배차 분리 r1 (2026-10-04)

- 사용자가 앞서 제안한 1번에서 음식 배달과 화물을 확실히 분리하도록 요청했다. [변경 기록](../Changes/2026-10-04-food-cargo-separation-r1.md)과 [독립 업무 기준](../Architecture/BusinessWorkflowResponsibilityModel.md#독립-업무-모듈과-서버-조립)을 따른다.
- 음식 큐 스캔·추천 만료·알림의 workload/기능 플래그/배치 설정을 화물과 나누고 자기 업무유형만 선택한다. 음식 추천 기록은 음식 AppKey를 검증하는 전용 Port로 유지하며 화물 공개배차 직접 전환은 차단한다. 음식 알림이 화물 앱의 legacy 토큰을 재사용하지 않도록 별도 대상 앱 경계를 둔다.
- 최종 Fast `20261004-182837`은 서버·시험 빌드와 관련 96/96을 통과했다. Task `20261004-182913`은 전체 3.5 빌드를 통과했고 전체 시험은 6,782/6,789다. 기준 165504의 기존 7개 실패가 이름·메시지·전체 스택까지 동일하며 신규 56개 모두 통과·새 실패 0·기존 결과 변경 0·삭제 0이다. 전체 시험 게이트는 기존 실패로 미통과다. 최종 제품·시험 26개와 범위 밖 기존 dirty 128개 SHA-256 변경 0을 확인했다.
- 기존 API·DB 형태·역할 앱 UI를 유지하며 별도 서버/DB 구축·APK 업데이트·운영 외부 발송·휴대폰 수신 완료로 보고하지 않는다. 음식 앱의 별도 푸시 토큰 등록은 아직 없으며 화물 앱 토큰으로 대신 보내지 않는다. 뒤의 재조리 예외/모바일 관제/미확정 요청 복구는 이번 배치 분리의 완료 주장에 포함하지 않는다. 커밋·푸시는 하지 않았다.

## 이전 검토: 음식 배달 네 역할의 보완 우선순위 (2026-10-04)

- 사용자가 음식점·주문자·음식 기사·운영자의 관계를 기준으로 가장 부족한 부분부터 보완 순서를 제안해 달라고 요청했다. 현재 코드·기획·최근 Android/서버 검증을 읽기 전용으로 대조했으며 아래는 구현 전 제안이다.
- 우선 1: 음식 배달의 자동 후속 처리를 화물 활성화와 분리한다. [큐 스캔](../../Ssalddel/Infrastructure/BackgroundJobs/DispatchQueue/배차큐스캔Job.cs), [추천 만료](../../Ssalddel/Infrastructure/BackgroundJobs/DispatchQueue/추천만료정리Job.cs), [활성화 정책](../../Ssalddel/Infrastructure/BackgroundJobs/SsalddelBackgroundJobActivationPolicy.cs)은 음식 후보 없음 재탐색·만료도 화물 workload 조건에 묶는다. Operational 음식 ON·화물 OFF의 후속 대기 조건을 우선 재현한다. 즉시 재추천 전체가 없는 것으로 해석하지 않는다.
- 우선 2: 픽업 후 중단→재조리→새 기사 인계를 복구한다. 음식점 조회가 과거 첫 준비 완료 시각을 계속 제공하고 [행동 판정](../../Ssalddel/Application/Food/음식배달가능행동Projector.cs)은 새 준비 완료를 닫지만, 새 기사 픽업은 자기 수락 이후 준비를 요구한다. 과거 기록을 보존하며 현재 재조리 차수의 준비 상태·행동을 구분한다. 코드상 공백이며 해당 예외의 실제 APK 재현은 아직 없다.
- 우선 3~5: [모바일 관제](../../SsalddelAdminApp/Services/AdminOperationsService.cs)의 음식·화물 조회 종속을 풀고 기존 웹 주문 추적·중단 검토를 모바일에 연결한다. 인지 이후 조리 불가·미수령의 운영 접수와 당사자 결과 조회를 연결하고, 음식점·기사의 일부 미확정 요청을 앱 재시작 뒤 복구하는 범위를 검토한다. 기존 직접 취소 제한·기사 보호 시각·공제 정책을 임의 변경하지 않는다.
- 최근 Simulation 정상 1건 서버·DB 완료와 아래 APK 검증은 그대로 유효하지만, Operational 자동 후속·재조리 예외·네 역할 APK의 동일 주문 전체 완주를 대신하지 않는다. 이번 검토에서는 제품 코드·시험·설정 변경, 새 빌드·실행·커밋·푸시를 수행하지 않았다.

## 이전 완료: Android 업무 상태와 설치 준비 r1 (2026-10-04)

- 사용자가 휴대폰 APK에서 역할별 업무 OS의 현재 상태에 필요한 정보·행동만 제공하고, 인증·업무 복귀가 자연스럽게 이어지도록 요청했다. [총괄 책임·OS 기준](../ProjectOverview/page-docs/mobile-workflow-readiness-r1.md), [변경·검증 기록](../Changes/2026-10-04-mobile-workflow-readiness-r1.md)에 연결한다.
- 주문자 선택 상세·목록 복귀와 취소·거절 안내, 음식점 같은 계정의 인증 만료 후 주문 상세 복귀, 음식 기사 인증 복원·늦은 조회와 업무 명령의 충돌 방지, 대기의 고정 운행 시작·수신·위치 확인을 보완했다.
- 화물 기사 재진입·세션 판본·늦은 조회와 오류 상태의 행동 차단, 독립 로그인 복귀와 가로 넘침을 보완했다. 창고 홈은 기능·인증·역할·계정 판본을 확인한 뒤 보호 조회와 업무 화면을 연다. 화물 기사·화주·창고도 APK에 지정한 서버 주소를 실제 사용한다.
- Fast 20261004-164809 전체 3.5 빌드와 관련 시험 238/238을 통과했다. 최종 Task 20261004-165504도 전체 빌드를 통과했고 전체 시험은 6,726/6,733이다. 기준 154057의 기존 7개 실패가 이름·메시지·전체 스택까지 동일하며 새 실패 0·기존 결과 변경 0·삭제 0·새 112개 전부 통과다. 전체 시험 게이트는 기존 실패로 미통과다. 최종 제품·시험·지원 56개와 범위 밖 기존 dirty 95개의 SHA-256 변경 0을 확인했다.
- 새 격리 Development/Simulation 서버·DB에서 음식 주문 `FOOD-20261004071201176`의 인지 → 기사 배정 → 조리 → 픽업 → 전달 → 수령 확인을 완료했다. 모의 정산 재시도·중복 방지와 관리자 조회를 확인했으며 실제 결제·입금·지도·Unity 앱의 증거로 확대하지 않는다.
- 최종 Debug APK 7개는 내장 런타임·ARM64/x86_64·서명·정렬·SHA-256을 확인하고 API36 에뮬레이터에 설치했다. 주문자 선택 상세, 음식점 배차 대기, 음식 기사 운행 시작, 화물 기사 독립 로그인, 창고 비활성 안내 등을 실제 화면에서 확인했다. 전체 역할의 업무 전수 수행은 아니다.
- 시험 주소는 `http://127.0.0.1:5361/`이며 PC 서버·USB reverse가 필요한 개인 개발용이다. 공개 HTTPS 서버·정식 서명·개인 휴대폰 설치·지도 정상 표시가 남아 있다. 원시 근거·APK·격리 DB 볼륨은 Git 제외 보관하고, 시험 에뮬레이터와 이번 Docker 프로젝트는 종료했다. 다른 dirty 작업을 보존했으며 커밋·푸시는 수행하지 않았다.

## 이전 완료: 주문 작성·조리시간 변경 복구 r2 (2026-10-04)

- 사용자가 후속 보완안의1번·4번을 승인했다. [페이지 책임·코드/API·DB](../ProjectOverview/page-docs/order-workflow-polish-r2.md), [변경·검증 기록](../Changes/2026-10-04-order-workflow-polish-r2.md)에 연결한다.
- 주문 작성은 메뉴 비공개·삭제·품절의 확정 거절과 미확정 통신 오류를 구별한다. 최신 메뉴 GET은 선택·수령 입력을 보존하며 변경 단가를 보여 준다. 명시 금액 확인 뒤 새 요청으로 제출한다. 복구 재제출의 메뉴 오류도 같은 안내로 이어진다.
- 서버는 기존 주문자+요청 ID의 접수 결과를 먼저 반환하고, 신규 가격 확인 요청은 저장 전 최신 단가와 비교한다. DB 스키마·배차·정산 정책은 유지한다.
- 음식점 조리시간 변경은 원래 입력·ID·revision을 보존하고 정본 GET부터 확인한다. 적용된 시간으로 안내하며 같은 계정 만료와 다른 계정/명시 로그아웃을 구별한다. 영속 복구는 현재 프로세스 메모리 범위다.
- 최종 Fast20261004-153840 관련198/198·전체3.5 빌드 통과. Chrome390×844에서 최종 제품 Razor/CSS의 정상 주문·가격 확인·품절 복귀6상태를 조작했고 자동GET→POST0·입력 보존·넘침0·브라우저 오류0을 확인했다. 모의 서비스이며 실제 운영 HTTP/DB·MAUI/실기기 증거가 아니다. 로컬 브라우저·미리보기 서버는 종료했다.
- 최종 Task20261004-154057 전체3.5 빌드 통과·전체시험6,614/6,621이다. 기준145843의 기존7실패가 이름·메시지·전체스택까지 동일하며 새실패0·기존결과변경0·삭제0·새43케이스전부통과다. 전체 게이트는 기존7실패로 미통과다. 제품·시험27경로의 실행 전후 SHA-256 변경0을 확인했다. 원시 근거는 Git 제외 artifacts/local/order-workflow-polish-r2/. 영상/MYBOX·지도·Unity·커밋·푸시는 이번 실행에서 수행하지 않았다.

## 이전 완료: 역할 앱 기존 페이지 보완 r1 (2026-10-04)

- 사용자가 지도 제외 보완안 일곱 항목을 모두 승인했다. [페이지 책임·코드/API·DB](../ProjectOverview/page-docs/role-app-polish-r1.md)와 [통합 변경·검증](../Changes/2026-10-04-role-app-polish-r1.md)에 연결한다.
- 화물은 연락처 수정의 전체 화물 원값 보존, 동일 완료 사진의 순차 재시도 중복 방지, 내역의 계정/페이지/조회 수명과 한국시간 표시를 구현했다. [개별 기록](../Changes/2026-10-04-cargo-role-polish-r1.md)을 참조한다.
- 음식점은 인증 만료 후 같은 계정의 메뉴 입력·미확정 요청 ID 복귀와 기존 주문 요청사항/배차 예외 안내를 구현했다. 인증·HTTP의 늦은 응답이 새 계정을 덮거나 종료하는 경계도 보완했다. [책임 문서](../ProjectOverview/page-docs/restaurant-role-polish-r1.md)를 참조한다.
- 주문자는 같은 상세의 일시 조회 실패에 마지막 내용/입력을 유지하고 변경 행동을 잠그며 복귀·재연결 뒤 조회한다. 미확정 제출은 원래 payload/ID를 저장하고 본인 GET 조회 뒤 명시 동일 요청 재시도로만 복구한다. 만료는 보존, 명시 로그아웃은 정리하며 정리 실패에도 개인 화면을 숨긴다. [조회 마감](../ProjectOverview/page-docs/orderer-refresh-polish-r1.md)을 참조한다.
- 최종 관련 시험409/409·전체3.5 빌드 통과. Task 20261004-145843은6,571/6,578이며 기준135108의 기존7실패 이름/메시지/전체스택 동일·새 실패0이다. 전체 게이트는 미통과다. 의미를 유지한 시험 이름/구조 대체2건과 순증123케이스를 통합 기록에 설명했다. 제품·시험·지원72경로는 최종 실행 중·후 지문 변경0이다.
- 제품 Razor/CSS의390px 실제 메뉴 목록→입력→저장 후 목록 복귀·가로 넘침0·브라우저 오류0, 처리11·모의 HTTP34검사를 확인했다. 주문자 복구/상세 HTML과 화물 수명은 회귀시험 근거다. 실제 운영 HTTP/DB·물리 보안 저장소·앱 재시작·실기기 전체 화면은 이번 새 실행으로 검증하지 않았다. 격리 브라우저와 메뉴 미리보기 서버를 종료했다. 원시는 Git 제외 artifacts/local/role-app-polish-r1/이며 다른 dirty 작업·영상/MYBOX를 보존하고 커밋·푸시는 수행하지 않았다.

## 보류·기존 완료 근거

- 네이버 지도는 사용자가 이번 범위에서 제외했다. [기존 경로 화면 맞춤](../Changes/2026-10-04-driver-navermap-viewport-r1.md)에서 SDK 인증401·실제 타일 미완료이며 새 키/콘솔/유료 호출 변경은 없다.
- 음식 기사 [완료 목록·상세·72시간 열람](../Changes/2026-10-04-driver-history-lifetime-map-r1.md), [지도 중심 현재 수행](../Changes/2026-10-04-driver-map-first-r2.md), [카드·고정 글자](../Changes/2026-10-04-driver-fixed-font-r1.md)는 기존 구현을 유지한다.
- [음식 알림·전표·당일 정산](../Changes/2026-10-03-food-notification-daily-settlement-r1.md), [예외 명령·신규 수신](../Changes/2026-10-03-food-exception-intent-r1.md), [인증·업무 분리](../Changes/2026-10-03-auth-workspace-separation-r1.md)의 실행 근거는 각 기록에 있다. 이번 소스 보완을 물리 단말·실제 지도·인쇄·입금 완료로 확대하지 않는다.
- 비교 기준 Task 20261004-135108의 기존 실패7개는 별도 마감 대상이며 이번 보완에서 성공으로 변경하지 않았다. 최종 동일 여부와 원시 비교 결과는 통합 기록에 연결한다.
