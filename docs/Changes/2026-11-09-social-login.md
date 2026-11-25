# 웹의 구글·카카오·네이버 로그인

살뜰 웹 로그인에 공급사 로고를 적용했다. 카카오·네이버는 공식 한국어 버튼 이미지를 그대로 사용하고, 구글은 공식 다색 G와 한국어 로그인 문구를 함께 표시한다. 서버에 설정한 공급사만 선택할 수 있다. [Google 디자인 기준](https://developers.google.com/identity/branding-guidelines) · [Kakao 디자인 기준](https://developers.kakao.com/docs/ko/kakaologin/design-guide) · [NAVER 디자인 기준](https://developers.naver.com/docs/login/bi/bi.md)

## 페이지 책임

| 항목 | 적용 |
| --- | --- |
| 진입 | 통합 웹과 커뮤니티 역할 웹의 [기존 로그인 페이지](../../Ssalddel.WebApp/Pages/LoginPage.razor). 주문 등 앞 화면에서 받은 반환 주소를 검증한다. |
| 목적 | 사용자가 선택한 인증 방법으로 자기 살뜰 계정에 들어가고, 로그인 전 보던 업무를 이어간다. |
| 최초 연결 | 공급사 인증이 성공한 뒤 연결할 기존 살뜰 계정의 아이디와 비밀번호를 확인한다. 기존 계정과 연결된 뒤에는 공급사 인증으로 로그인한다. |
| 실패와 복귀 | 인증 취소·만료 시 다시 시도하거나 기존 비밀번호 로그인을 사용할 수 있다. 관리자·관세사 및 잠긴 계정은 소셜 인증에서 차단한다. |
| 저장과 권한 | 서버는 검증된 공급사와 고유 계정 ID를 기존 Identity 연결에 저장한다. 이메일 일치로 계정을 연결하거나 새 계정을 만들지 않으며 기존 역할에서 토큰을 발급한다. |

## 서비스 설정

웹과 API를 같은 공개 HTTPS 주소에서 제공한다. 서버 보안 설정에 `SocialLogin:PublicOrigin`과 공급사별 `SocialLogin:{Google|Kakao|Naver}:ClientId`, `SocialLogin:{Google|Kakao|Naver}:ClientSecret`을 둔다. 값은 배포 비밀로 관리한다.

| 공급사 | 등록할 콜백 경로 |
| --- | --- |
| 구글 | `/api/v1/auth/social/google/callback` |
| 카카오 | `/api/v1/auth/social/kakao/callback` |
| 네이버 | `/api/v1/auth/social/naver/callback` |

각 경로 앞에 `SocialLogin:PublicOrigin`을 붙인 전체 주소를 등록한다. 공급사 인증이 끝나면 서버는 정해진 웹 로그인 주소로 보내고, 그 브라우저가 인증을 시작했는지 확인한 뒤 기존 살뜰 로그인 토큰을 발급한다. 공급사 토큰과 살뜰 토큰은 반환 주소에 들어가지 않는다. [Google OpenID Connect](https://developers.google.com/identity/openid-connect/openid-connect) · [Kakao REST API](https://developers.kakao.com/docs/ko/kakaologin/rest-api) · [NAVER 로그인 API](https://developers.naver.com/docs/login/api/api.md)

기기 앱과 관리자 화면에서는 공유 버튼의 비활성 표시를 소비한다. 이 변경의 소셜 인증은 통합·커뮤니티 웹에서만 지원한다. 로컬에 공급사 연동 설정이 없으므로 실제 공급사 계정 검증과 배포는 남아 있다.

## 화면 검증

실제 Chrome에서 320px의 미설정 상태, 390px와 1280px의 공식 버튼, 키보드 선택을 확인했다. 가상 공급사 인증으로 첫 연결·잘못된 비밀번호·다시 로그인·취소를 조작했다. 인증 성공 뒤 이전 판매 주문 검색어와 같은 주문이 복원됐다. 공급사와 판매 주문 응답은 격리된 가상 자료다. [390px 화면](../assets/changes/2026-11-09-social-login/role01-390.png) · [1280px 화면](../assets/changes/2026-11-09-social-login/role01-1280.png) · [전후 비교](../../artifacts/local/social-login-brand-flow-20261109/comparison.html)

공급사 고유 ID의 대소문자를 구분하도록 서버 연결 키를 보완한 판본에서, 동일 ID를 두 계정이 공유하지 못하고 대소문자가 다른 ID는 각자 올바른 계정에 연결되는 회귀를 확인했다. Fast 62건·Task 17,524건이 통과했으며 Task의 환경 의존 시험 17건은 미실행이다. 두 실행 모두 소셜 로그인 회귀 7건을 포함한다. 기존 Chrome의 모의 인증 결과를 재사용했다. [원본 시험과 화면 증거](../../artifacts/local/social-login-brand-flow-20261109/account-key-closeout.json)
