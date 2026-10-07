# 생활 교류 로컬 실행 검증

`dotnet run --project eng/NeighborhoodExchangePreview/NeighborhoodExchangePreview.csproj` 실행 후 `http://127.0.0.1:5389/community/exchange`를 연다.

제품 공유 컴포넌트 → 실제 `NeighborhoodExchangeClient`/기존 게시글 HTTP client → 루프백 API → 실제 조회·발행·참여·운영 UseCase → SQLite 저장을 검증한다. 서버 재시작 후에도 글/댓글이 유지된다. 기록은 `artifacts/local/neighborhood-exchange-mvp-r1/preview.sqlite`이며 운영 DB와 구분한다. 검사 화면에 시험 용도를 표시하고 루프백에만 바인딩한다.

이 호스트는 운영 배포 대상이 아니다. 합성 자료용 개인정보 어댑터, 익명 사용자, 원장 문맥 없음, 외부 후속 이벤트 실행 생략을 사용한다. 현재 기존 자유·생활 게시판의 실제 쓰기 정책과 비밀번호 검증은 적용한다. 제품 앱은 원래 서버의 인증·개인정보 보호·컨트롤러·기능 플래그 경로를 계속 사용한다. Azure/HTTPS·실제 공개 접근·APK 설치·Figma node 검증은 별도다.

생활 배송 세 화면은 같은 제품 공유 컴포넌트와 VM을 사용하되, 이 호스트 안에서만 `PreviewDeliveryClient`/`PreviewDeliveryUser`의 화면 검토용 값을 사용한다. 동의→입력→견적→접수→목록/상세 및 선정 기사 동의/철회의 표시와 조작을 검토하며 운영 서버 인증·개인정보 동의 저장·배차·배송비 계산을 실행한 증거가 아니다. 화면 검토용 의뢰는 메모리에만 존재한다. 실제 서버 연결 시험은 별도 `NeighborhoodDeliveryTests`의 격리 운송 원장/큐 검증과 구별한다.

## 생활 협업·보관 검증

`-- --CollaborationPreview=true` 옵션은 `http://127.0.0.1:5391`에서 같은 공유 UI와 실제 협업·보관 서비스를 사용한다. `SSALDDEL_PREVIEW_MONGO_CONNECTION`은 전용 루프백 Mongo를 지정하며 원격 Mongo 접속은 시작 시 거부한다. 이 실행은 기존 업무 DB 대신 `artifacts/local/neighborhood-collaboration-r1/preview.sqlite`와 별도 `neighborhood_preview_…` Mongo DB를 사용한다. 보호된 원장 재조회에 필요한 키 ring은 같은 비공유 로컬 폴더에 유지한다.

`/preview/account/owner`, `requester`, `helper`, `anonymous`로 검토용 주체를 바꾼다. 운영 인증을 모방해 완료로 보고하지 않는다. 협업 신청·양측 합의·상호 완료·별도 공개 동의, 보관 조건의 판본 동의·수량 예약·양측 인수/반환을 실제 SQLite 출처와 보호 Mongo에 대조한다. 공간의 개인정보 전송은 제품의 공개키 발급·RSA/AES 전송 보호와 미들웨어를 그대로 사용한다. 지도 동네/글/보관 집계도 실제 조회이며 배송 위치와 외부 후속 사건은 검토용이다.

최신 실행 범위·화면·시험·Android APK는 [변경 기록](../../docs/Changes/2026-10-05-neighborhood-collaboration-r1.md)에 연결한다. 검증 호스트는 공개 배포용이 아니며 이번에 만든 전용 Mongo와 프로세스만 작업 후 중단한다.
