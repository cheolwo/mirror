# 검토된 제품 합성 안내음과 저장소 푸시 r19

상태: Approved / ReadyToDispatch. 2026-10-27 사용자는 19개 맥락별 로컬 커밋 뒤 합성 안내음 두 개의 기존 푸시 차단 상태를 안내받았으며, 이어서 **푸시**를 명시적으로 요청했다. 이번 인계는 같은 로컬 checkout에서 확인된 제품 자산 두 개만 수동 검토하고 저장소로 푸시하는 범위다. 기존 자동 예외 승인 금지는 유지한다.

- 기준: `dev/mirror-integration`, HEAD `a5c8f1108966fe35dce0336b6ef1c46d634de1f9`, 원격 `origin`은 `https://github.com/cheolwo/mirror.git`. 원격 최신 조회에서 behind0/ahead19를 확인했다.
- 자료: 기존 [생성 근거](../../../../assets/changes/2026-10-27-driver-recommendation-signal-r18/audio-provenance.json), 로컬 합성 manifest·단문 생성 스크립트·PCM 검증 및 실제 파일 SHA를 대조한다. 현재 공개 허가 자료/배포약관/청취 상태를 검증 완료로 바꾸지 않는다.
- `FDriverApp/Platforms/Android/Resources/raw/ssalddel_delivery.wav`: 기존 beep SHA `19846118acc73172933e20068874138c799fbf802701fc1027fedaa07d9f2e61`와 검토된 ‘살뜰 배달!’ 합성 SHA `d0ed818d6e9d6449d7224f849d5dc71ea88f3d2a4b788be1d5aa3828765d577b`만 허용한다. 과거 이력의 기존 beep도 계속 검증한다.
- `DriverApp/Platforms/Android/Resources/raw/hongdal_dispatch.wav`: 검토된 ‘홍달배달’ 합성 SHA `6e0da9aaa1b36521b71a7ce0dff5412edd315cc6531d96c308e385a4c4cbbab2`만 허용한다.
- 정확한 Git 경로와 정적 SHA 집합을 모두 만족해야 한다. 다른 경로로 복사·변조·삭제/복원해 숨긴 이력·조회 실패는 계속 차단한다. 배달 저장소 모드에는 예외를 적용하지 않는다. 원본 영상·녹음·고객 정보·비공개 다운로드 링크·APK·로컬 실행 자료는 올리지 않는다.
- 허용 writePaths: `eng/git/raw_media_guard.py`, `eng/git/test_raw_media_guard.py`, `eng/git/README.md`, 위 합성 근거 JSON의 이번 저장소 푸시 승인 기록만 추가, 이 PLAN, `docs/AI/CURRENT_WORK.md`, `docs/Changes/README.md`, `docs/Changes/2026-10-27-reviewed-notification-assets-push-r19.md`, `artifacts/local/reviewed-assets-push-r19/`의 점검 기록.
- 알림 코드·음원 바이트·다른 작업·운영 플래그를 바꾸지 않는다. hook 삭제·`--no-verify`·force push·기존 19커밋 재작성은 하지 않는다.
- 검증: 신규/기존 음원 판본의 이력·index·다중 ref·위장 경로 회귀, 실제 HEAD/index 및 정확한 원격 경계를 검사한다. 명시적인 현재 브랜치 refspec으로 정상 푸시하고 `ls-remote`의 SHA와 로컬 HEAD 일치를 따로 확인한다. 실패하면 원인과 실제 상태를 보존하며 보호를 우회하지 않는다.
