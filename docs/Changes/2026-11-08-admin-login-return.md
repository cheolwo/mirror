# 관리자 로그인 뒤 같은 AI 배차 검토로 복귀

2026-11-08 · 운영자 · Admin-P37 · `dev/mirror-integration` / `e67cdbf0bbb086dc9ed547bdcd640074c9a18c86`의 기존 dirty checkout.

미로그인 진입과 조회401의 **로그인**이 `/dispatch/ai-review`를 반환 주소로 만들었다. 합성 로그인 성공 또는 기존 세션의 로그인 자동 복귀 뒤 현행 navigation policy가 이 옛 주소를 차단해 같은 검토 업무를 다시 열 수 없었다. 반환 주소 한 곳을 기존 허용 경로 `/admin/freight-delivery/dispatch-ai-review`로 맞췄다. 옛 route 선언·정책 차단, 권한·API·배정·판단 저장 조건은 바꾸지 않았다.

운영자의 질문은 로그인 재확인 뒤 같은 검토를 이어갈 수 있는가다. 401/403 구별·현재 의뢰/묶음·명시 로그인과 복귀를 우선했다. 기존 구성001 12상자·002 8상자와 확인된 상차 시간창, **구성 의뢰 확인**·**제안 선택**은 보존했다. 정상 화면의 읽는 순서·주 행동까지의 세로 길이와 정보 밀도는 바꾸지 않았으며 사용 시간 단축을 주장하지 않는다.

## 실제 검증

- 변경 전 핵심 회귀2개 실패·403 회귀1개 통과. 변경 후 실제 P37 렌더의401 링크·익명 업무GET0·실제 AdminLogin 기존 세션 복귀·403 문구·예상 밖 요청/POST0을 보호했다.
- 최종 Fast28통과, Admin 소비 빌드, Task17,509통과/실패0/환경17미실행. 같은 최종 소스의 완료 시험을 반복하지 않았다.
- 실제 Admin InteractiveServer와 loopback 전용 합성 API에서 폼 입력/로그인200, 조회401→로그인 Enter→기존 세션 자동 복귀, 같은 의뢰3건·제안3개·구성2건, 503 다시 조회와403 권한 구분을 확인했다. 기존 세션 자동 복귀는 새 로그인POST 성공과 다른 사실이다.
- 390·320·1280px의 같은 데이터·폭·상단 스크롤0을 포함한 전후9쌍을 대조했다. 정상390 JPEG는 전후 바이트가 같고320/1280은 다르므로 모든 폭의 픽셀 동일성을 주장하지 않는다. 가로 넘침은 관측되지 않았다.

[실제 수정 전후와 원본](../../artifacts/local/admin-login-return-20261108-1520/comparison.html) · [판본·TRX·확인·한계](../../artifacts/local/admin-login-return-20261108-1520/verification.json) · [캡처34원본·실제 픽셀 크기·스크롤·SHA](../../artifacts/local/admin-login-return-20261108-1520/captures-verified.json).

초기 캡처는 키 입력 뒤 스크롤 위치가 일치하지 않았다. 원본을 보존하고 동일한 원본 소스를 별도 출력 폴더에 재현 빌드해 native `Control_L+Home` 뒤 실제0을 확인하며12장을 추가했다. 최종 제품 소스와 기존 after DLL/PDB는 정확히 보존했다. before/after 각6개 소스와 실제 DLL/PDB 결속은 [before](../../artifacts/local/admin-login-return-20261108-1520/runtime-before-bound.json)·[after](../../artifacts/local/admin-login-return-20261108-1520/runtime-binding.json), [보존 근거](../../artifacts/local/admin-login-return-20261108-1520/alignment-correction.json)에 있다. 일부 초기 도구의 줄바꿈/버튼명/summary 역할 판정은 대체 확인까지 남겼다.

비교 HTML의 `file:` 주소 열기는 브라우저 도구의 URL 정책에서 거절됐다. 다른 host·브라우저·간접 실행으로 우회하지 않았고 HTML/이미지 링크와 원본 형식은 파일 읽기로 검증했다. 비교 HTML의 실제 브라우저 렌더 완료는 주장하지 않는다. 제품 웹 화면은 위 실제 클릭·입력과 원본으로 검증했다.

## 보존과 다음 한 가지

공개 대표 이미지는 정상 가상 구성의 [최신 실제 화면](../assets/changes/2026-11-08-admin-login-return/bundle-members-390.jpg)이다. [이전 실제 웹](2026-11-08-admin-platform-startup.md)·[구판 정적 렌더](2026-11-07-operator-bundle-members.md)와 원본은 보존했다. 운영 인증·계정·DB·실기기·물리 한글IME·운영 배차/학습·외부 쓰기·commit/push·배포는 미실행이다.

다음 후보는 `Admin-document-title-interactive-head`다. 로그인 뒤 본문은 검토인데 `document.title`은 운영자 로그인으로 남는 현상이 수정 전후에 관측됐다. App.razor:15의 정적 HeadOutlet와 InteractiveServer Routes를 로그인→P37→뒤로/새로고침에서 대조한다. 이번 반환 주소 수정과 구별하며 아직 구현하지 않았다.
