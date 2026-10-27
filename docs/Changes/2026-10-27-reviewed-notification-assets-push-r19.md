# 검토한 제품 합성 안내음의 저장소 푸시 r19

사용자가 [19개 로컬 커밋](2026-10-27-context-commits.md) 이후 **푸시**를 명시적으로 요청했다. 기존 차단 원인은 직접 생성한 제품 안내음 두 개가 정확한 허용 판본에 아직 등록되지 않은 것이었다. 녹음·배달 촬영 원음이 아닌 고정 단문 합성물임을 기존 생성 코드/manifest·실제 PCM·SHA로 대조하고, 이번 요청의 저장소 전송 범위에 한정해 수동 검토했다.

[승인 범위와 검증 지시](../AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/generated-notification-assets-push.r19.md)의 SHA-256은 `a59b3bf3b064d16de43364c1c3071929ad5afae7f42e06229f1a79b91fc19228`이며 기준 HEAD는 `a5c8f1108966fe35dce0336b6ef1c46d634de1f9`다. 기존 커밋 재작성·force push·hook 우회·원본/비공개 다운로드 링크 업로드는 포함하지 않는다.

## 정확한 허용 범위

- 화물 기사 `DriverApp/Platforms/Android/Resources/raw/hongdal_dispatch.wav`: ‘홍달배달’, SHA `6e0da9aaa1b36521b71a7ce0dff5412edd315cc6531d96c308e385a4c4cbbab2` 한 개. PCM16/mono/22050Hz, 84,000바이트·약1.904초다.
- 음식 기사 `FDriverApp/Platforms/Android/Resources/raw/ssalddel_delivery.wav`: ‘살뜰 배달!’, SHA `d0ed818d6e9d6449d7224f849d5dc71ea88f3d2a4b788be1d5aa3828765d577b`. PCM16/mono/22050Hz, 86,692바이트·약1.965초다. 기존 beep SHA `19846118acc73172933e20068874138c799fbf802701fc1027fedaa07d9f2e61`도 같은 경로의 과거 이력 판본으로 유지한다.
- 기존 음식점·회원 앱의 기계음 허용은 유지한다. 새로운 경로/바이트를 실행 때 해시 계산만 해서 자동 허용하지 않는다. 정적으로 명시한 경로별 SHA 집합의 모든 이력 blob을 검사한다.
- 다른 경로로 복사한 동일 바이트·변조·정상 파일 복원/삭제로 숨긴 잘못된 이력·다중 ref 혼합·검사 실패는 계속 차단한다. 배달 저장소 모드는 예외 없이 기존 차단을 유지한다.

새 합성이나 소리 재생 없이 저장소에 이미 있는 자산을 확인했다. [생성 근거와 이번 푸시 검토](../assets/changes/2026-10-27-driver-recommendation-signal-r18/audio-provenance.json)는 청취·실기기 진동·상업 배포약관 확인의 증거가 아니다. 이 요청은 소스 저장소 전송이며 앱스토어/SNS·운영 배포와 구분한다.

## 확인과 실제 전송 구분

현재 브랜치 `dev/mirror-integration`, 원격 `origin`의 `https://github.com/cheolwo/mirror.git`를 확인했고 처음 fetch 결과는 뒤처짐0/앞섬19였다. 정상 푸시의 실제 성공은 명령 결과와 원격 SHA가 로컬 HEAD와 같음을 따로 대조해 판단한다. 로컬 검토 기록은 `artifacts/local/reviewed-assets-push-r19/`에 보존하며 비공개 실행 자료는 추적하지 않는다.

| 준비 검증 | 실제 결과 |
| --- | --- |
| 미디어 보호 단독 회귀 | 기존14개+신규3개, 17/17 통과. 구 beep→새 TTS 혼합 이력, 새 음원별 변조/다른 경로/복원·삭제로 숨긴 이력 차단 포함 |
| 실제 Git index 검사 | mirror 모드 통과. 자산 바이트나 일반 원본 차단은 변경하지 않음 |
| 범위 Fast / Task | [Fast](../../artifacts/local/validation/20261027-152707/) · [Task](../../artifacts/local/validation/20261027-152753/) 공백 검사 통과. 현재 분류는 GuidanceOnly라 앱 빌드·C# 시험은 선택되지 않았으며 17개 Python 회귀와 구분 |
| 원격 경계 | `origin/dev/mirror-integration`을 fetch 후 기존19커밋과 이번 검토 커밋만 명시적 refspec으로 전송. 실제 pre-push hook이 그 경계의 모든 자산 판본을 검사하며 실패하면 전송 중단 |

단독 회귀 로그와 실제 전송/원격 SHA 영수증은 위 로컬 검토 폴더에 별도로 남긴다. 이 문서는 푸시 준비의 근거이며 실제 전송 성공이나 CI·앱 배포 성공을 미리 채우는 기록이 아니다.
