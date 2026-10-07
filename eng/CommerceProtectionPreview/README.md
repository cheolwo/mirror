# 거래 보호 화면 개발 검토

`Development` 환경의 `http://127.0.0.1:5396`에서 실제 공통 Razor를 표시한다. 신규 안내·판매자 미확인·개인정보 권리·거래 문제 접수와 로딩/빈 목록/오류/익명 상태를 확인한다. `CommercePreviewClient`의 예시 DTO를 사용하며 운영 인증·DB 저장·메일 발송·법적 요건 충족 근거가 아니다.

PowerShell 실행:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run --project eng/CommerceProtectionPreview/CommerceProtectionPreview.csproj
```

판매자 판본 결속·변경 후 재확인은 `/commerce/confirmation?state=disclosure`에서 확인한다. 요청 접수, 보존 사유, 잠정 영업일 안내, 익명·실패 상태는 예시 데이터로만 표시한다.
검토: `node eng/CommerceProtectionPreview/verify-ui.cjs`.
