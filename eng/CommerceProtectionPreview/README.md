# 거래 보호 화면 개발 검토

`Development` 환경의 `http://127.0.0.1:5396`에서 실제 공통 Razor를 표시한다. 신규 안내·판매자 미확인·개인정보 권리·거래 문제 접수와 로딩/빈 목록/오류/익명 상태를 확인한다. `CommercePreviewClient`의 예시 DTO를 사용하며 운영 인증·DB 저장·메일 발송·법적 요건 충족 근거가 아니다.

PowerShell 실행:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run --project eng/CommerceProtectionPreview/CommerceProtectionPreview.csproj
```

판매자 판본 결속·변경 후 재확인은 `/commerce/confirmation?state=disclosure`에서 확인한다. 요청 접수, 보존 사유, 잠정 영업일 안내, 익명·실패 상태는 예시 데이터로만 표시한다.
검토: `node eng/CommerceProtectionPreview/verify-ui.cjs`.

## 음식 배달 4역할 r27 실제 UI 검증

기존 r26 실행은 그대로 유지한다. `COMMERCE_PREVIEW_FOUR_ROLE_R27=1`을 명시하면 이 개발 호스트는 `127.0.0.1:5397`에서 `127.0.0.1:5363`의 격리 Staging+Simulation 서버에만 연결한다. `/workspace/orderer/new`는 기존 `OrdererRestaurantWorkspace`·`OrdererFoodOrderComposer`를 사용하고 주문 등록 뒤 기존 `OrdererFoodOrderWorkspace`로 이동한다. 주문은 준비 스크립트/API에서 생성하지 않는다.

서버가 준비된 뒤 `prepare-four-role-r27.ps1 -AccountsPath <비공유 accounts.json>`을 실행한다. 실제 서버 로그인으로 얻은 합성 계정 토큰은 무시된 `artifacts/local/food-mobile-four-role-r27/server/.private/ui-sessions.json`에만 저장한다. 서버가 명시한 Simulation 상태에서는 운영 판매자 인증을 대신 만들지 않으며, 실제 판매자 확인 여부를 준비 결과에 별도로 기록한다. 메뉴/기사 운행/수신/위치 준비는 API fixture다.

호스트 실행 환경은 `ASPNETCORE_ENVIRONMENT=Development`, `COMMERCE_PREVIEW_FOUR_ROLE_R27=1`, `COMMERCE_PREVIEW_REAL_ACCOUNTS=<비공유 accounts.json>`, `COMMERCE_PREVIEW_REAL_TOKENS=<비공유 ui-sessions.json>`이다. 빌드한 공유 UI scoped CSS를 preview 출력 폴더의 `verification-shared-ui.css`로 복사한 뒤 실행한다. `node eng/CommerceProtectionPreview/verify-four-role-r27.cjs`가 작성 화면부터 수령 확인까지 실제 공유 UI/client/HTTP를 실행하고 320·390px 화면을 남긴다. 출력 위치는 `FOUR_ROLE_R27_ARTIFACTS`로 무시된 로컬 증거 폴더에 지정할 수 있다.

운영자 상세는 제품 `FoodOrderDetail.razor`와 API client를 링크해 사용하고 합성 인증 어댑터만 연결한다. `/verification/fail-next-admin-trace`는 r27 개발 호스트의 루프백에서만 한 번의 GET 503을 주입하여 기존 내용의 표시와 재조회를 확인한다. 제품 서버·공개 API·운영 인증에는 주입하지 않는다. 실제 로그인 화면, 지도/GPS, APK, 애저, 휴대폰, 법적 판매자 인증과 실제 결제 검증은 이 브라우저 결과에서 주장하지 않는다.

관리자 연결 화면은 제품 `RoleAppProviders`의 관리자 테마, `role-app-foundation.css`, 관리자 `app.css`, `FoodOrderDetail.razor.css`를 그대로 사용한다. CSS 응답 성공만으로 테마 적용을 인증하지 않고 실제 팔레트·카드 경계·글자 크기·페이지 scoped CSS를 확인한다. `node eng/CommerceProtectionPreview/verify-admin-style-r27.cjs`는 이미 완료한 동일 주문을 조회하고 미리보기의 단 한 번의 GET 503·재조회만 수행한다. 주문 생성·수락·조리·배달을 반복하지 않으며 기존 `real-ui/` 화면은 보존하고 별도 `admin-style-ui/` 증거에 저장한다.
