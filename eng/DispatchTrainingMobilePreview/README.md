# 관리자 가상 퀵 운영 모바일 미리보기

실제 `SsalddelAdminApp`의 새 훈련 Razor 두 페이지와 서비스·인증 client를 링크하여 실행한다. 제품 페이지·대시보드를 복제하지 않는다. 관리자 테마와 제품 CSS를 사용하며 브라우저의 390×844 정도 화면에서 확인한다. Android WebView·SecureStorage·기기 검증과 구분한다.

전용 훈련 서버는 기본 `http://127.0.0.1:5324/`이며 `DISPATCH_TRAINING_API`로 지정할 수 있다. 합성 관리자 이름은 `dispatch-training-admin`이며 `DISPATCH_TRAINING_ADMIN_USERNAME` 환경변수로 지정할 수도 있다. 비밀번호는 `DISPATCH_TRAINING_ACCOUNT_PASSWORD` 환경변수로만 공급한다. 설정이 없으면 보호 조회를 열지 않는다. 인증값은 출력·파일·URL에 저장하지 않는다.

```powershell
dotnet run --project eng/DispatchTrainingMobilePreview --urls http://127.0.0.1:5325
```

`/training/dispatch`, `/training/dispatch/decisions`를 연다. 이 미리보기의 역할은 실제 페이지의 렌더 확인이며 별도 주문·기사 생성이나 실제 운영 권한을 추가하지 않는다.
