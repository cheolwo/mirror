# [기획 · 모바일 경영 게임 · PLAN-SYSTEM-MOBILE-OPERATOR-GAME · r1]

- 기준일: 2026-11-11
- 상태: `UserApprovedHistory / CoreAndMobileUIImplemented / DesktopLayoutVerified / DeferredByUserWpfFirst`
- 승인 근거: 사용자가 모바일 중심 대체 계획에서 가로 화면·게임 안의 업무 연습·별도 실제 업무 앱을 선택하고 `Implement the proposed plan`으로 구현을 요청했다.
- 상위 맥락: [지역 경영 게임](../PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/operator-expansion-map-ui.concept.r137.md), [게임 자료 여섯 묶음](../../../DataCollection/GameMaterialGroups/README.md)

## 최신 우선순위 변경

2026-11-11 사용자가 기존 WPF에서 데이터·규칙·업무 흐름을 고도화한 뒤 Unity로 전환하는 방향을 선택했다. 현재 우선순위는 [WPF 우선 r139](../PLAN-SYSTEM-REGIONAL-OPERATOR-SIMULATION/wpf-first-development-direction.r139.md)다. 아래 모바일 구현과 미완료 검증은 과거 결과·후속 재개 근거로 보존하며 Android 설치·APK·기기 검증을 지금 이어가는 지시로 해석하지 않는다.

## 플레이 약속

안드로이드 휴대폰 한 대에서 기사 선택 → 주문 매장 연결 → 도로 이동 → 업무 연습 화면의 픽업 확인 → 전달·가상 정산까지 플레이한다. PC는 제작·검증 도구이며 실행 중 PC 연결이나 Windows 엔진을 요구하지 않는다. 지도·오토바이 중심의 기존 디자인을 유지한다.

## 구현 범위

1. Windows 외부 프로세스 호출을 앱 내 순수 C# 코어로 바꾸고 기존 공통 규칙을 재사용한다. WPF·Windows 검증판과 같은 조건의 후보·경로·사건·정산을 대조한다.
2. 가로 화면에서 탭 선택/연결, 한 손가락 지도 이동, 두 손가락 확대·축소를 제공한다. UI 터치와 지도 동작을 분리하고 상세/업무 패널은 접고 같은 위치로 복귀한다.
3. 가상 회차와 배달권 설정을 구분해 저장한다. 백그라운드에서는 가상 시간이 멈추며 강제 종료·복귀·중복 명령에서 정산이 중복되지 않는다.
4. 게임 안의 업무 연습은 같은 회차를 조작한다. 현재 시간 기반 자동 흐름은 보존하고 모바일 연습에서만 픽업 확인 대기를 사용한다.
5. 자료 교환은 사용자가 선택한 파일로 하고 원본/합성/가상 결과를 구분한다. 기존 실제 업무 앱·운영 권한·원장은 별도로 유지한다.
6. 승인 사례 참고와 규칙 기반 가상 배차 시험을 연결할 수 있는 경험 기록을 둔다. 모델 미연결은 규칙 추천으로 표시하고 새 모델 설치·가중치 학습·현실 자동 배차를 시작하지 않는다.

## 실행과 검증

Hongdal 공통 코어와 기존 Unity checkout의 신규 `Assets/Ssalddel/MobileGame` 모듈을 사용한다. 이전 Windows 검토판은 보존한다. 새 모듈의 정확한 소스를 최소 검토 프로젝트에 같은 지문으로 연결해 컴파일·검증하며, 별도 검토 Scene을 기존 공식 World Scene의 완료로 주장하지 않는다.

코어 회귀·Windows 소비 빌드·Unity 컴파일과 Android ARM64 IL2CPP 빌드를 구분한다. 등록 휴대폰의 실제 터치·픽업·정산·앱 복귀 확인 전에는 모바일 완료로 표시하지 않는다. 기기 확인은 기존 OS 잠금·등록 기기·사용자 점유·보안 경계를 따른다.

실제 원본/고객 자료 취득, 운영 쓰기, 유료 AI 호출·학습, 업무 앱 제거·데이터 삭제, 배포·commit/push는 이 작업에서 실행하지 않는다. [현재 구현·검증 기록](../../../../../artifacts/local/hongdal-mobile-unity-20261111/start.json)에서 시작 상태를 확인한다.

## 현재 마감 상태

공통 코어 52검사, 기존 배차 관련 70회귀, 보존 Windows 엔진 5시나리오·54명령의 공개 결과 동등성이 통과했다. 모바일 모듈의 Unity Windows 검토판에서 같은 회차의 uGUI 지도 배차·수동 픽업·정산·저장 복원·손상 복구·자료 경계·규칙 추천·완료 경험 기록을 43항목으로 확인했다. 이것은 실제 Android 터치나 IL2CPP 재생 확인이 아니다.

Android JNI 측 C# 분기와 Java 문서 Activity는 각각 정적 컴파일했으나 Android Build Support 미설치로 APK가 생성되지 않았다. 모듈 설치 도구 표시량은 약 12GB이며 현재 C 여유 약 5GB로 환경 준비가 필요하다. 현재 게임 반입은 비공개 참고 사본이고 실운영 설정의 자동 적용이나 실제 AI 모델 연결은 하지 않았다. 정확한 재개는 빌드 모듈과 저장 공간을 확보한 뒤 Android 빌드 → 등록 휴대폰의 기본 플레이·SAF·백그라운드 복귀를 확인하는 것이다. [구현·검증 결과](../../../../../artifacts/local/hongdal-mobile-unity-20261111/completion.json)

