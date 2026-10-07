# 거래 안내와 개인정보 요청 보호

기존 사업자 음식 거래와 개인 생활 거래에 중개 안내·판매자 확인·분쟁·개인정보 권리 요청을 연결했다. 거래 정보를 보호한 Mongo 원장으로 저장하고, 업무 개인정보의 보존·파기와 재복원을 제한한다. [정책과 법령 기준](../Architecture/CommerceIntermediaryPrivacy.md)이 단일 기준이다.

## 적용 범위

통합 앱·웹, 공통 주문 화면과 생활 배송·협업의 신청/조건 동의에 현재 안내 확인을 추가했다. 개인·사업자 공개 정보 범위를 구분한다. 신원기관·성년·공개 운영 준비가 미완료이면 서버가 새 거래 저장을 차단하며 일반 게시판·접수된 주문 재조회·취소와 구분한다.

분쟁·권리 요청·사고 사건은 자신의 원장 연결, 담당자 권한, CAS·멱등 접수, 한국 시간 영업일 기한, 실제 통지 증거를 다룬다. 파기 worker는 기본 비활성이고 정확한 원장·허용 필드의 검증·재시도·보존 중지·복원 차단을 제공한다. 외부 경로 검토 없는 저장·지도 전송을 막는다.

판매자 없는 과거 익명 게시글의 활성 판매 전환을 차단했다. 사건 보존 의도는 먼저 저장하고 외부 원장의 순번·실제 상태를 확인한 뒤 완료한다. 늦은 과거 요청과 불명확한 부분 저장은 파기를 허용하지 않는다. 안내문·사고 판단이 바뀌면 이전 통지 증빙을 완료 증거로 재사용하지 않는다. 공개 법률 안내와 본인 정보 페이지의 인증·capability를 네 소비 앱에서 구분한다.

## 검증

`Implemented / LocalValidated / OperationalPrerequisitesPending`. 최종 소스 141개 파일의 지문이 Fast·Task 검증 전후 일치한다. 상세 원시는 로컬 `artifacts/local/commerce-privacy-r25/`에 보존한다.

| 검증 | 결과 | 원시 근거 |
| --- | --- | --- |
| 최종 Fast | 전체 3.5 솔루션 빌드 오류 0, 집중 시험 218개 통과·Mongo 2개 별도 실행 | `artifacts/local/validation/20261006-153451/` |
| 최종 Task | 전체 솔루션 빌드 오류 0, 전수 시험 7,968개 통과·Mongo 2개 별도 실행 | `artifacts/local/validation/20261006-153658/` |
| 최종 실행 파일의 실제 Mongo | 인증된 임시 loopback Mongo에서 2개 통합 시험 통과·건너뜀 0. 암호문·계정 결속·CAS·재송신·보존 fence·파기/복원 차단과 자기 DB 삭제·부재 확인 | `mongo-isolated-r25-final/verification.json`, `mongo-isolated-r25-final.trx` |
| 실제 공유 Razor | 320/390px 30장·16개 흐름, 브라우저 오류·가로 넘침 0, 입력/버튼 48px 기준 | `ui/verification.json`, `ui-source-fingerprint.json` |
| 후속 앱 연결 | 음식점 공개 안내 예외, 네 앱 capability 분류, 기존 주문 렌더 fixture 연결을 최종 Fast·Task에서 확인 | `ui-final-wrapper-fingerprint.json`과 최종 TRX |

Android·Windows 등 소비 앱을 포함한 솔루션 컴파일 결과이며 최종 휴대폰 설치·운영 API 업무 완주와 구분한다. 관찰된 이전 Debug APK를 이번 최종 소스의 설치 검증 증거로 사용하지 않는다. AndroidX 버전 제약·플랫폼 API·기존 nullability 등의 빌드 경고는 남아 있다. 공통 UI 전용 미리보기 빌드는 경고·오류 0이다.

초기 Fast의 시험 컴파일 오류, 월말 계산과 .NET 10 SQL 식 해석 오류, 주문 렌더 서비스·라우트 대장 누락을 수정한 뒤 재검증했다. 과거 실패 로그도 보존한다. 실제 신원 인증·발송·전체 사용자 삭제·클라우드 버전/백업 파기·휴대폰·Azure·Figma 동기화·법적 적합성 인증은 완료로 주장하지 않는다. 운영주체와 실제 인증·권리 실행·통지 채널, 전체 파기 목록 검증이 준비되기 전에는 새 Operational 거래를 차단한다.

기존 Mongo의 초기화 환경 인증값은 현재 인증과 일치하지 않아 별도 임시 컨테이너를 사용했다. 기존 DB·컨테이너·계정은 수정하지 않았으며 임시 컨테이너 제거·부재까지 확인했다. 실제 Mongo 시험 통과를 기존 사용자 Mongo 연결 준비 완료로 해석하지 않는다. 최종 Test/Server DLL SHA를 해당 실행에 결속했다.

이번 범위 153개 경로와 관련 없는 기준선 파일 1,007개의 SHA-256, HEAD·branch를 보존했다. `preservation.json`에 확인 결과를 둔다. 커밋·푸시·기존 운영 데이터 파기·영상/MYBOX·Unity 형상 변경은 수행하지 않았다.

## 화면

실제 공통 컴포넌트를 Development 예시 DTO로 표시한 화면이다. 접수 성공 화면은 실제 사용자 요청의 운영 처리 완료 증거가 아니다. [페이지·API·저장 책임](../ProjectOverview/page-docs/commerce-protection-r25.md)을 함께 확인한다.

| 화면 | 모바일 캡처 |
| --- | --- |
| 공개 안내 | [안내 390px](../assets/changes/2026-10-06-commerce-privacy-r25/notices-390.png) |
| 판매자 정보와 미확인 상태 | [판매자 390px](../assets/changes/2026-10-06-commerce-privacy-r25/seller-unverified-390.png) |
| 개인정보 요청 접수 | [접수 390px](../assets/changes/2026-10-06-commerce-privacy-r25/rights-submitted-390.png) |
| 보존 사유·이의제기 | [보존 390px](../assets/changes/2026-10-06-commerce-privacy-r25/rights-retained-390.png) |
| 보존 확인 대기 | [대기 390px](../assets/changes/2026-10-06-commerce-privacy-r25/rights-retention-pending-390.png) |
| 거래 문제 상세 | [상세 390px](../assets/changes/2026-10-06-commerce-privacy-r25/dispute-detail-390.png) |
| 판매자 변경 후 재확인 | [재확인 320px](../assets/changes/2026-10-06-commerce-privacy-r25/seller-disclosure-changed-320.png) |
